using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace PoultryERP.Reports
{
    public class ShedMeta
    {
        public int ShedId, FarmId, LocationId, Capacity;
        public string Code, Name, FarmName, LocationName, Purpose, Status;
        public bool IsActive = true;
    }

    public class FlockMeta
    {
        public int FlockId, BreedId, InitialCount, ArrivalMortality, PlannedDepletionWeek;
        public int? HenHousedCount;
        public string Code, BreedName, Status, BatchNo, LotNo, Source;
        public DateTime HatchDate, ArrivalDate;
        public DateTime? DepletionDate;
        public decimal ChickRate, FreightCost, SalvagePerBird;
        public decimal AcquisitionCost { get { return InitialCount * ChickRate + FreightCost; } }
        public string FlockType = "Layer";
        public int? TargetLiftDays;
        public bool IsBroiler { get { return FlockType == "Broiler"; } }
    }

    /// <summary>Sheds and flocks of one company, loaded once per report.</summary>
    public class MasterData
    {
        public Dictionary<int, ShedMeta> Sheds = new Dictionary<int, ShedMeta>();
        public Dictionary<int, FlockMeta> Flocks = new Dictionary<int, FlockMeta>();
        public Dictionary<int, string> FarmNames = new Dictionary<int, string>();

        public static MasterData Load(int companyId)
        {
            var md = new MasterData();
            foreach (DataRow r in Db.Query(@"SELECT s.ShedId, s.Code, s.Name, s.Capacity, s.Purpose, s.Status, s.IsActive, l.LocationId, l.Name AS LocationName,
                                                    f.FarmId, f.Name AS FarmName
                                             FROM dbo.Shed s JOIN dbo.Location l ON l.LocationId = s.LocationId JOIN dbo.Farm f ON f.FarmId = l.FarmId
                                             WHERE s.CompanyId = @c", Db.P("@c", companyId)).Rows)
            {
                var s = new ShedMeta
                {
                    ShedId = Db.Int(r["ShedId"]), Code = Db.Str(r["Code"]), Name = Db.Str(r["Name"]), Capacity = Db.Int(r["Capacity"]),
                    Purpose = Db.Str(r["Purpose"]), Status = Db.Str(r["Status"]), LocationId = Db.Int(r["LocationId"]),
                    LocationName = Db.Str(r["LocationName"]), FarmId = Db.Int(r["FarmId"]), FarmName = Db.Str(r["FarmName"]),
                    IsActive = Convert.ToBoolean(r["IsActive"])
                };
                md.Sheds[s.ShedId] = s;
                md.FarmNames[s.FarmId] = s.FarmName;
            }
            foreach (DataRow r in Db.Query(@"SELECT f.*, b.Name AS BreedName, sp.Name AS SourceName FROM dbo.Flock f
                                             JOIN dbo.Breed b ON b.BreedId = f.BreedId LEFT JOIN dbo.Supplier sp ON sp.SupplierId = f.SupplierId
                                             WHERE f.CompanyId = @c", Db.P("@c", companyId)).Rows)
            {
                var f = new FlockMeta
                {
                    FlockId = Db.Int(r["FlockId"]), Code = Db.Str(r["FlockCode"]), BreedId = Db.Int(r["BreedId"]), BreedName = Db.Str(r["BreedName"]),
                    HatchDate = Convert.ToDateTime(r["HatchDate"]), ArrivalDate = Convert.ToDateTime(r["ArrivalDate"]),
                    InitialCount = Db.Int(r["InitialCount"]), ArrivalMortality = Db.Int(r["ArrivalMortality"]),
                    ChickRate = Db.Dec(r["ChickRate"]), FreightCost = Db.Dec(r["FreightCost"]), HenHousedCount = Db.NInt(r["HenHousedCount"]),
                    SalvagePerBird = Db.Dec(r["ExpectedSalvagePerBird"]), PlannedDepletionWeek = Db.Int(r["PlannedDepletionWeek"]),
                    Status = Db.Str(r["Status"]), DepletionDate = Db.NDate(r["DepletionDate"]), BatchNo = Db.Str(r["BatchNo"]),
                    LotNo = Db.Str(r["LotNo"]), Source = Db.Str(r["SourceName"])
                };
                if (r.Table.Columns.Contains("FlockType")) { f.FlockType = Db.Str(r["FlockType"]); f.TargetLiftDays = Db.NInt(r["TargetLiftDays"]); }
                md.Flocks[f.FlockId] = f;
            }
            return md;
        }

        public string ShedCode(int id) { ShedMeta s; return Sheds.TryGetValue(id, out s) ? s.Code : "#" + id; }
        public string FlockCode(int id) { FlockMeta f; return Flocks.TryGetValue(id, out f) ? f.Code : "#" + id; }
    }

    /// <summary>Opening / movements / closing for one shed + flock on one day.</summary>
    public class LedgerDay
    {
        public int ShedId, FlockId;
        public DateTime Date;
        public int Opening, Placed, TrfIn, TrfOut, Mort, Cull, Sold;
        public int Closing { get { return Opening + Placed + TrfIn - TrfOut - Mort - Cull - Sold; } }
        /// <summary>Birds in the shed during the day after transfers (denominator for hen-day %).</summary>
        public int Present { get { return Opening + Placed + TrfIn - TrfOut; } }
    }

    /// <summary>
    /// Derives the bird balance of every shed/flock for every day of a period from vBirdMovement.
    /// Same idea as SUM(No) over the old Trans_Daily table, but computed in one pass.
    /// </summary>
    public class BirdLedger
    {
        public readonly List<LedgerDay> Days = new List<LedgerDay>();
        readonly Dictionary<string, LedgerDay> index = new Dictionary<string, LedgerDay>();

        static string K(int shed, int flock, DateTime d) { return shed + "|" + flock + "|" + d.ToString("yyyyMMdd", CultureInfo.InvariantCulture); }

        public class Move { public int Shed, Flock, Qty; public string Kind; public DateTime Date; }

        public static BirdLedger Load(int companyId, DateTime from, DateTime to)
        {
            var opening = new List<Move>();
            foreach (DataRow r in Db.Query(@"SELECT ShedId, FlockId, SUM(Qty) AS Q FROM dbo.vBirdMovement
                                             WHERE CompanyId = @c AND MoveDate < @f GROUP BY ShedId, FlockId HAVING SUM(Qty) <> 0",
                                             Db.P("@c", companyId), Db.P("@f", from)).Rows)
                opening.Add(new Move { Shed = Db.Int(r["ShedId"]), Flock = Db.Int(r["FlockId"]), Qty = Db.Int(r["Q"]) });
            var moves = new List<Move>();
            foreach (DataRow r in Db.Query(@"SELECT ShedId, FlockId, MoveDate, Kind, SUM(Qty) AS Q FROM dbo.vBirdMovement
                                             WHERE CompanyId = @c AND MoveDate BETWEEN @f AND @t GROUP BY ShedId, FlockId, MoveDate, Kind",
                                             Db.P("@c", companyId), Db.P("@f", from), Db.P("@t", to)).Rows)
                moves.Add(new Move { Shed = Db.Int(r["ShedId"]), Flock = Db.Int(r["FlockId"]), Kind = Db.Str(r["Kind"]), Qty = Db.Int(r["Q"]), Date = Convert.ToDateTime(r["MoveDate"]).Date });
            return Build(opening, moves, from, to);
        }

        /// <summary>Pure calculation (no database): opening balances + dated movements -> daily ledger.</summary>
        public static BirdLedger Build(List<Move> openingBalances, List<Move> movements, DateTime from, DateTime to)
        {
            var led = new BirdLedger();
            var bal = new Dictionary<string, int[]>(); // key shed|flock -> [shed, flock, balance]
            foreach (var o in openingBalances) bal[o.Shed + "|" + o.Flock] = new[] { o.Shed, o.Flock, o.Qty };
            var moves = new Dictionary<DateTime, List<Move>>();
            foreach (var m in movements)
            {
                List<Move> list;
                if (!moves.TryGetValue(m.Date.Date, out list)) { list = new List<Move>(); moves[m.Date.Date] = list; }
                list.Add(m);
            }

            for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            {
                var today = new Dictionary<string, LedgerDay>();
                foreach (var kv in bal)
                {
                    if (kv.Value[2] == 0) continue;
                    today[kv.Key] = new LedgerDay { ShedId = kv.Value[0], FlockId = kv.Value[1], Date = d, Opening = kv.Value[2] };
                }
                List<Move> ml;
                if (moves.TryGetValue(d, out ml))
                {
                    foreach (var m in ml)
                    {
                        var key = m.Shed + "|" + m.Flock;
                        LedgerDay ld;
                        if (!today.TryGetValue(key, out ld))
                        {
                            int[] b;
                            ld = new LedgerDay { ShedId = m.Shed, FlockId = m.Flock, Date = d, Opening = bal.TryGetValue(key, out b) ? b[2] : 0 };
                            today[key] = ld;
                        }
                        switch (m.Kind)
                        {
                            case "PLACE": ld.Placed += m.Qty; break;
                            case "TIN": ld.TrfIn += m.Qty; break;
                            case "TOUT": ld.TrfOut += -m.Qty; break;
                            case "MORT": ld.Mort += -m.Qty; break;
                            case "CULL": ld.Cull += -m.Qty; break;
                            case "SALE": ld.Sold += -m.Qty; break;
                        }
                    }
                }
                foreach (var kv in today)
                {
                    var ld = kv.Value;
                    bal[kv.Key] = new[] { ld.ShedId, ld.FlockId, ld.Closing };
                    led.Days.Add(ld);
                    led.index[K(ld.ShedId, ld.FlockId, d)] = ld;
                }
            }
            return led;
        }

        public LedgerDay Get(int shed, int flock, DateTime d)
        {
            LedgerDay ld;
            return index.TryGetValue(K(shed, flock, d), out ld) ? ld : null;
        }

        /// <summary>Birds of a flock (all sheds) at the start of a date - straight from the database.</summary>
        public static int FlockBirdsBefore(int flockId, DateTime d)
        {
            return Db.Int(Db.Scalar("SELECT ISNULL(SUM(Qty),0) FROM dbo.vBirdMovement WHERE FlockId = @f AND MoveDate < @d",
                Db.P("@f", flockId), Db.P("@d", d)));
        }
    }

    public class LogRow
    {
        public int DailyLogId, ShedId, FlockId, Mort, Cull, Sold, Table, Cracked, Misshapen, DoubleYolk;
        public DateTime Date;
        public int? FeedItemId, GritItemId;
        public string FeedName = "", FeedStage = "", Remarks = "";
        public decimal FeedKg, GritKg;
        public decimal? BodyWt, EggWt, Water, TempMin, TempMax, Humidity;
        public TimeSpan? GritTime;
        public int TotalEggs { get { return Table + Cracked + Misshapen + DoubleYolk; } }
    }

    public static class LogData
    {
        public static string Key(int shed, DateTime d) { return shed + "|" + d.ToString("yyyyMMdd", CultureInfo.InvariantCulture); }

        public static Dictionary<string, LogRow> Load(int companyId, DateTime from, DateTime to)
        {
            var map = new Dictionary<string, LogRow>();
            foreach (DataRow r in Db.Query(@"SELECT d.*, i.Name AS FeedName, i.FeedStage FROM dbo.DailyLog d
                                             LEFT JOIN dbo.Item i ON i.ItemId = d.FeedItemId
                                             WHERE d.CompanyId = @c AND d.LogDate BETWEEN @f AND @t",
                                             Db.P("@c", companyId), Db.P("@f", from), Db.P("@t", to)).Rows)
            {
                var x = new LogRow
                {
                    DailyLogId = Db.Int(r["DailyLogId"]), ShedId = Db.Int(r["ShedId"]), FlockId = Db.Int(r["FlockId"]),
                    Date = Convert.ToDateTime(r["LogDate"]).Date, Mort = Db.Int(r["Mortality"]), Cull = Db.Int(r["Culls"]), Sold = Db.Int(r["BirdsSold"]),
                    FeedItemId = Db.NInt(r["FeedItemId"]), FeedName = Db.Str(r["FeedName"]), FeedStage = Db.Str(r["FeedStage"]),
                    FeedKg = Db.Dec(r["FeedKg"]), GritItemId = Db.NInt(r["GritItemId"]), GritKg = Db.Dec(r["GritKg"]),
                    GritTime = r["GritTime"] == DBNull.Value ? (TimeSpan?)null : (TimeSpan)r["GritTime"],
                    Table = Db.Int(r["TableEggs"]), Cracked = Db.Int(r["CrackedEggs"]), Misshapen = Db.Int(r["MisshapenEggs"]),
                    DoubleYolk = Db.Int(r["DoubleYolkEggs"]), BodyWt = Db.NDec(r["AvgBodyWtG"]), EggWt = Db.NDec(r["AvgEggWtG"]),
                    Water = Db.NDec(r["WaterLtr"]), TempMin = Db.NDec(r["TempMinC"]), TempMax = Db.NDec(r["TempMaxC"]),
                    Humidity = Db.NDec(r["HumidityPct"]), Remarks = Db.Str(r["Remarks"])
                };
                map[Key(x.ShedId, x.Date)] = x;
            }
            return map;
        }
    }

    /// <summary>Item price history: rate effective on a date.</summary>
    public class RateBook
    {
        readonly Dictionary<int, List<KeyValuePair<DateTime, decimal>>> rates = new Dictionary<int, List<KeyValuePair<DateTime, decimal>>>();
        public int? DefaultGritItemId;

        public static RateBook Load(int companyId)
        {
            var rb = new RateBook();
            foreach (DataRow r in Db.Query("SELECT ItemId, EffectiveFrom, Rate FROM dbo.ItemRate WHERE CompanyId = @c ORDER BY ItemId, EffectiveFrom", Db.P("@c", companyId)).Rows)
            {
                int id = Db.Int(r["ItemId"]);
                List<KeyValuePair<DateTime, decimal>> l;
                if (!rb.rates.TryGetValue(id, out l)) { l = new List<KeyValuePair<DateTime, decimal>>(); rb.rates[id] = l; }
                l.Add(new KeyValuePair<DateTime, decimal>(Convert.ToDateTime(r["EffectiveFrom"]).Date, Db.Dec(r["Rate"])));
            }
            rb.DefaultGritItemId = Db.NInt(Db.Scalar("SELECT TOP (1) ItemId FROM dbo.Item WHERE CompanyId = @c AND Category = 'GRIT' AND IsActive = 1 ORDER BY ItemId", Db.P("@c", companyId)));
            return rb;
        }

        public decimal Rate(int? itemId, DateTime d)
        {
            if (!itemId.HasValue) return 0;
            List<KeyValuePair<DateTime, decimal>> l;
            if (!rates.TryGetValue(itemId.Value, out l) || l.Count == 0) return 0;
            decimal r = l[0].Value;
            foreach (var kv in l) { if (kv.Key <= d) r = kv.Value; else break; }
            return r;
        }
    }

    public class StdRow
    {
        public int AgeWeek;
        public decimal? BodyWt, FeedG, HDP, HHCum, EggWt, Livability, CumFeedG, FCR;

        internal static StdRow From(DataRow r)
        {
            var s = new StdRow
            {
                AgeWeek = Db.Int(r["AgeWeek"]), BodyWt = Db.NDec(r["BodyWtG"]), FeedG = Db.NDec(r["FeedGBirdDay"]), HDP = Db.NDec(r["HDPPct"]),
                HHCum = Db.NDec(r["HHEggsCum"]), EggWt = Db.NDec(r["EggWtG"]), Livability = Db.NDec(r["LivabilityPct"])
            };
            if (r.Table.Columns.Contains("CumFeedG")) { s.CumFeedG = Db.NDec(r["CumFeedG"]); s.FCR = Db.NDec(r["StdFCR"]); }
            return s;
        }

        static decimal? Lerp(decimal? a, decimal? b, decimal t)
        {
            if (!a.HasValue) return b.HasValue && t >= 1 ? b : null;
            if (!b.HasValue) return a;
            return Math.Round(a.Value + (b.Value - a.Value) * t, 3);
        }

        /// <summary>
        /// Broiler standards are weekly (body weight / cumulative feed at the end of each week);
        /// this gives the value for an exact age in days by straight-line interpolation.
        /// </summary>
        public static StdRow Interpolate(StdRow wk, StdRow next, int ageDays)
        {
            if (wk == null) return null;
            if (next == null || next == wk) return wk;
            decimal t = (ageDays - wk.AgeWeek * 7) / 7m;
            if (t < 0) t = 0; if (t > 1) t = 1;
            var r = new StdRow
            {
                AgeWeek = wk.AgeWeek, FeedG = wk.FeedG, Livability = Lerp(wk.Livability, next.Livability, t),
                BodyWt = Lerp(wk.BodyWt, next.BodyWt, t), CumFeedG = Lerp(wk.CumFeedG, next.CumFeedG, t),
                HDP = wk.HDP, HHCum = wk.HHCum, EggWt = wk.EggWt
            };
            if (r.CumFeedG.HasValue && r.BodyWt.HasValue && r.BodyWt.Value > 0 && r.CumFeedG.Value > 0) r.FCR = Math.Round(r.CumFeedG.Value / r.BodyWt.Value, 3);
            return r;
        }
    }

    /// <summary>Breeder standard lookup by breed and completed week of age.</summary>
    public class StandardBook
    {
        readonly Dictionary<int, SortedList<int, StdRow>> map = new Dictionary<int, SortedList<int, StdRow>>();

        public static StandardBook Load(int companyId)
        {
            var sb = new StandardBook();
            foreach (DataRow r in Db.Query("SELECT * FROM dbo.BreedStandard WHERE CompanyId = @c", Db.P("@c", companyId)).Rows)
            {
                int b = Db.Int(r["BreedId"]);
                SortedList<int, StdRow> l;
                if (!sb.map.TryGetValue(b, out l)) { l = new SortedList<int, StdRow>(); sb.map[b] = l; }
                var s = StdRow.From(r);
                l[s.AgeWeek] = s;
            }
            return sb;
        }

        public StdRow Get(int breedId, int ageWeek)
        {
            SortedList<int, StdRow> l;
            if (!map.TryGetValue(breedId, out l) || l.Count == 0) return null;
            StdRow best = null;
            foreach (var kv in l) { if (kv.Key <= ageWeek) best = kv.Value; else break; }
            return best;
        }

        /// <summary>Standard for an exact age in days (interpolated between weeks - used for broilers).</summary>
        public StdRow Daily(int breedId, int ageDays)
        {
            var wk = Get(breedId, ageDays / 7);
            if (wk == null) return null;
            SortedList<int, StdRow> l;
            map.TryGetValue(breedId, out l);
            StdRow next = null;
            foreach (var kv in l) if (kv.Key > wk.AgeWeek) { next = kv.Value; break; }
            return StdRow.Interpolate(wk, next, ageDays);
        }
    }

    /// <summary>Which sheds/flocks a report may include (filters + supervisor shed restriction).</summary>
    public class Scope
    {
        public int? FarmId, ShedId, FlockId;
        public HashSet<int> AllowedSheds; // null = no restriction

        public static Scope From(ReportParams p)
        {
            var s = new Scope { FarmId = p.FarmId, ShedId = p.ShedId, FlockId = p.FlockId };
            if (Ctx.ShedRestricted)
            {
                s.AllowedSheds = new HashSet<int>();
                foreach (DataRow r in Db.Query("SELECT ShedId FROM dbo.UserShed WHERE UserId = @u", Db.P("@u", Ctx.UserId)).Rows)
                    s.AllowedSheds.Add(Db.Int(r["ShedId"]));
            }
            return s;
        }

        public bool Shed(int shedId, MasterData md)
        {
            if (AllowedSheds != null && !AllowedSheds.Contains(shedId)) return false;
            if (ShedId.HasValue && ShedId.Value != shedId) return false;
            if (FarmId.HasValue)
            {
                ShedMeta sm;
                if (!md.Sheds.TryGetValue(shedId, out sm) || sm.FarmId != FarmId.Value) return false;
            }
            return true;
        }

        public bool Includes(int shedId, int flockId, MasterData md)
        {
            if (FlockId.HasValue && FlockId.Value != flockId) return false;
            return Shed(shedId, md);
        }
    }
}
