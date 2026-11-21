using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace PoultryERP
{
    /// <summary>Birds of one flock in a shed on a given day, before that day's mortality/culls/sales.</summary>
    public class ShedPosition
    {
        public int ShedId, FlockId, Opening, InQty, OutQty;
        public int Available { get { return Opening + InQty - OutQty; } }
    }

    /// <summary>Business rules shared by Daily Entry, Flocks, Transfers and Medication screens.</summary>
    public static class Ops
    {
        const string PositionSql = @"
SELECT FlockId,
       SUM(CASE WHEN MoveDate < @d THEN Qty ELSE 0 END) AS Opening,
       SUM(CASE WHEN MoveDate = @d AND Kind IN (N'PLACE', N'TIN') THEN Qty ELSE 0 END) AS InQty,
       SUM(CASE WHEN MoveDate = @d AND Kind = N'TOUT' THEN -Qty ELSE 0 END) AS OutQty
FROM dbo.vBirdMovement
WHERE ShedId = @s AND MoveDate <= @d
GROUP BY FlockId
HAVING SUM(CASE WHEN MoveDate < @d THEN Qty ELSE 0 END) <> 0
    OR SUM(CASE WHEN MoveDate = @d AND Kind IN (N'PLACE', N'TIN', N'TOUT') THEN 1 ELSE 0 END) > 0";

        public static List<ShedPosition> Positions(SqlConnection c, SqlTransaction t, int shedId, DateTime d)
        {
            var dt = c == null ? Db.Query(PositionSql, Db.P("@s", shedId), Db.P("@d", d)) : Db.Query(c, t, PositionSql, Db.P("@s", shedId), Db.P("@d", d));
            var list = new List<ShedPosition>();
            foreach (DataRow r in dt.Rows)
                list.Add(new ShedPosition { ShedId = shedId, FlockId = Db.Int(r["FlockId"]), Opening = Db.Int(r["Opening"]), InQty = Db.Int(r["InQty"]), OutQty = Db.Int(r["OutQty"]) });
            return list;
        }

        /// <summary>The flock that occupies the shed on the date (largest available count), or null.</summary>
        public static ShedPosition Occupant(int shedId, DateTime d) { return Occupant(null, null, shedId, d); }

        public static ShedPosition Occupant(SqlConnection c, SqlTransaction t, int shedId, DateTime d)
        {
            ShedPosition best = null;
            foreach (var p in Positions(c, t, shedId, d))
                if (p.Available > 0 && (best == null || p.Available > best.Available)) best = p;
            return best;
        }

        /// <summary>
        /// After any change to movements, the running balance of a shed+flock must never go negative on
        /// a later date (e.g. raising mortality after birds were already shifted out). Returns the first bad date.
        /// </summary>
        public static DateTime? FirstNegativeBalance(SqlConnection c, SqlTransaction t, int shedId, int flockId)
        {
            const string sql = @"
SELECT TOP (1) MoveDate FROM (
    SELECT MoveDate, SUM(Q) OVER (ORDER BY MoveDate ROWS UNBOUNDED PRECEDING) AS Bal
    FROM (SELECT MoveDate, SUM(Qty) AS Q FROM dbo.vBirdMovement WHERE ShedId = @s AND FlockId = @f GROUP BY MoveDate) x
) y WHERE Bal < 0 ORDER BY MoveDate";
            return Db.NDate(Db.Scalar(c, t, sql, Db.P("@s", shedId), Db.P("@f", flockId)));
        }

        public static void EnsureNoNegative(SqlConnection c, SqlTransaction t, int shedId, int flockId)
        {
            var bad = FirstNegativeBalance(c, t, shedId, flockId);
            if (bad.HasValue)
                throw new InvalidOperationException("This change would make the bird count of shed " + ShedCode(c, t, shedId) +
                    " negative on " + bad.Value.ToString("dd-MM-yyyy") + ". Check later transfers / entries first.");
        }

        public static string ShedCode(SqlConnection c, SqlTransaction t, int shedId)
        {
            return Db.Str(c == null ? Db.Scalar("SELECT Code FROM dbo.Shed WHERE ShedId = @s", Db.P("@s", shedId))
                                    : Db.Scalar(c, t, "SELECT Code FROM dbo.Shed WHERE ShedId = @s", Db.P("@s", shedId)));
        }

        /// <summary>
        /// Edit-lock rule for daily records. Managers may edit any past date (audited).
        /// Supervisors: only their sheds, only within the company edit window, unless a manager override exists.
        /// </summary>
        public static bool CanEditDay(int shedId, DateTime d, out string reason)
        {
            reason = null;
            if (d.Date > DateTime.Today) { reason = "Future dates cannot be entered."; return false; }
            if (Ctx.IsManager) return true;
            if (Ctx.Role != Roles.Supervisor) { reason = "Your role can view but not enter daily records."; return false; }
            if (!Ctx.CanAccessShed(shedId)) { reason = "This shed is not assigned to you."; return false; }
            var window = Ctx.Company.EditWindowDays;
            if (d.Date >= DateTime.Today.AddDays(-window)) return true;
            int n = Db.Int(Db.Scalar(@"SELECT COUNT(*) FROM dbo.EditOverride WHERE CompanyId = @c AND IsRevoked = 0 AND ExpiresOn > SYSDATETIME()
                                       AND (ShedId IS NULL OR ShedId = @s) AND @d BETWEEN FromDate AND ToDate AND (GrantedToUserId IS NULL OR GrantedToUserId = @u)",
                Db.P("@c", Ctx.CompanyId), Db.P("@s", shedId), Db.P("@d", d), Db.P("@u", Ctx.UserId)));
            if (n > 0) return true;
            reason = "Records older than " + (window == 0 ? "today" : window + " day(s)") + " are locked. Ask your manager for an edit override.";
            return false;
        }

        public static DataRow Shed(int shedId)
        {
            return Db.Row("SELECT * FROM dbo.vShedInfo WHERE ShedId = @s AND CompanyId = @c", Db.P("@s", shedId), Db.P("@c", Ctx.CompanyId));
        }

        public static DataRow Flock(int flockId)
        {
            return Db.Row("SELECT f.*, b.Name AS BreedName FROM dbo.Flock f JOIN dbo.Breed b ON b.BreedId = f.BreedId WHERE f.FlockId = @f AND f.CompanyId = @c",
                Db.P("@f", flockId), Db.P("@c", Ctx.CompanyId));
        }

        public static Reports.StdRow Standard(int breedId, int ageDays)
        {
            var dt = Db.Query(@"SELECT * FROM dbo.BreedStandard WHERE BreedId = @b AND AgeWeek IN
                                (SELECT MAX(AgeWeek) FROM dbo.BreedStandard WHERE BreedId = @b AND AgeWeek <= @w
                                 UNION SELECT MIN(AgeWeek) FROM dbo.BreedStandard WHERE BreedId = @b AND AgeWeek > @w) ORDER BY AgeWeek",
                                Db.P("@b", breedId), Db.P("@w", ageDays / 7));
            if (dt.Rows.Count == 0) return null;
            var wk = Reports.StdRow.From(dt.Rows[0]);
            if (wk.AgeWeek > ageDays / 7) return null;
            return Reports.StdRow.Interpolate(wk, dt.Rows.Count > 1 ? Reports.StdRow.From(dt.Rows[1]) : null, ageDays);
        }

        public static bool IsBroiler(DataRow flock)
        {
            return flock != null && flock.Table.Columns.Contains("FlockType") && Db.Str(flock["FlockType"]) == "Broiler";
        }

        /// <summary>Birds of a flock in a shed that can still leave on a date: available, less that day's
        /// mortality / culls / sold entries and other broiler liftings (optionally excluding one sale).</summary>
        public static int Movable(SqlConnection c, SqlTransaction t, int shedId, int flockId, DateTime d, int? excludeSaleId)
        {
            int avail = 0;
            foreach (var p in Positions(c, t, shedId, d)) if (p.FlockId == flockId) avail = p.Available;
            int used = Db.Int(Db.Scalar(c, t, "SELECT ISNULL(SUM(Mortality + Culls + BirdsSold),0) FROM dbo.DailyLog WHERE ShedId = @s AND FlockId = @f AND LogDate = @d",
                Db.P("@s", shedId), Db.P("@f", flockId), Db.P("@d", d)));
            int lifted = Db.Int(Db.Scalar(c, t, @"SELECT ISNULL(SUM(CAST(Qty AS INT)),0) FROM dbo.Sale WHERE ShedId = @s AND FlockId = @f AND SaleDate = @d
                                                 AND Product = N'Broiler Bird' AND (@x IS NULL OR SaleId <> @x)",
                Db.P("@s", shedId), Db.P("@f", flockId), Db.P("@d", d), Db.P("@x", excludeSaleId)));
            return Math.Max(0, avail - used - lifted);
        }

        /// <summary>A destination shed may receive birds only if it is empty or holds the same flock, and is not being cleaned.</summary>
        public static string CheckDestination(SqlConnection c, SqlTransaction t, int shedId, int flockId, DateTime d)
        {
            var st = Db.Str(Db.Scalar(c, t, "SELECT Status FROM dbo.Shed WHERE ShedId = @s", Db.P("@s", shedId)));
            if (st == "Cleaning") return "Destination shed is marked Cleaning. Change its status to Active or Vacant first.";
            if (st == "Quarantine") return "Destination shed is under Quarantine.";
            foreach (var p in Positions(c, t, shedId, d))
                if (p.FlockId != flockId && p.Available > 0)
                    return "Destination shed already holds another flock (" + Db.Str(Db.Scalar(c, t, "SELECT FlockCode FROM dbo.Flock WHERE FlockId = @f", Db.P("@f", p.FlockId))) +
                           "). Flocks of different age cannot be mixed.";
            return null;
        }

        public static void MarkShedOccupied(SqlConnection c, SqlTransaction t, int shedId)
        {
            Db.Exec(c, t, "UPDATE dbo.Shed SET Status = N'Active' WHERE ShedId = @s AND Status = N'Vacant'", Db.P("@s", shedId));
        }

        public static void MarkShedVacantIfEmpty(SqlConnection c, SqlTransaction t, int shedId)
        {
            int birds = Db.Int(Db.Scalar(c, t, "SELECT ISNULL(SUM(Qty),0) FROM dbo.vBirdMovement WHERE ShedId = @s", Db.P("@s", shedId)));
            if (birds == 0) Db.Exec(c, t, "UPDATE dbo.Shed SET Status = N'Vacant' WHERE ShedId = @s AND Status = N'Active'", Db.P("@s", shedId));
        }
    }
}
