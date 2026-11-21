using System;
using System.Web.UI.WebControls;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;

namespace PoultryERP
{
    public partial class TransfersPage : BasePage
    {
        protected override string[] AllowedRoles { get { return new[] { Roles.SuperAdmin, Roles.FarmManager, Roles.Supervisor }; } }

        DateTime TDate { get { return Ui.Date(txtDate.Text) ?? DateTime.Today; } }

        protected void Page_Load(object sender, EventArgs e)
        {
            chkFull.InputAttributes["class"] = "form-check-input"; chkFull.LabelAttributes["class"] = "form-check-label";
            chkApproveNow.InputAttributes["class"] = "form-check-input"; chkApproveNow.LabelAttributes["class"] = "form-check-label";
            phApproveNow.Visible = Ctx.IsManager;
            txtNote.Visible = Ctx.IsManager;
            if (IsPostBack) return;
            txtDate.Text = Ui.DateValue(DateTime.Today);
            txtDate.Attributes["max"] = Ui.DateValue(DateTime.Today);
            BindFlocks();
            Source_Changed(null, null);
            BindLists();
        }

        void BindFlocks()
        {
            Ui.Bind(ddlFlock, @"SELECT f.FlockId AS Value, f.FlockCode + ' (hatch ' + CONVERT(VARCHAR(10), f.HatchDate, 105) + ')' AS Text FROM dbo.Flock f
                                WHERE f.CompanyId = @CompanyId AND f.Status = N'Active'
                                AND EXISTS (SELECT 1 FROM dbo.vShedOccupancy o WHERE o.FlockId = f.FlockId AND o.Birds > 0" + Ctx.ShedFilter("o.ShedId") + @")
                                ORDER BY f.HatchDate", "-- select flock --", Ctx.Scope());
        }

        /// <summary>Birds of the flock in a shed that can still leave on the date (after that day's mortality, culls, sales).</summary>
        static int Movable(SqlConnection c, SqlTransaction t, int shedId, int flockId, DateTime d, int? excludeTransferId)
        {
            int pending = Db.Int(Db.Scalar(c, t, "SELECT ISNULL(SUM(BirdsDispatched),0) FROM dbo.FlockTransfer WHERE FromShedId = @s AND FlockId = @f AND TransferDate = @d AND Status = N'Pending' AND (@x IS NULL OR TransferId <> @x)",
                Db.P("@s", shedId), Db.P("@f", flockId), Db.P("@d", d), Db.P("@x", excludeTransferId)));
            return Math.Max(0, Ops.Movable(c, t, shedId, flockId, d, null) - pending);
        }

        protected void Source_Changed(object sender, EventArgs e)
        {
            var flock = Ui.SelInt(ddlFlock);
            var d = TDate;
            ddlFrom.Items.Clear(); ddlTo.Items.Clear();
            if (!flock.HasValue) { litInfo.Text = ""; return; }
            Ui.Bind(ddlFrom, @"SELECT v.ShedId AS Value, v.Code + ' - ' + v.Name AS Text FROM dbo.vShedInfo v
                               WHERE v.CompanyId = @CompanyId AND EXISTS (SELECT 1 FROM dbo.vBirdMovement m WHERE m.ShedId = v.ShedId AND m.FlockId = @f AND m.MoveDate <= @d
                                     GROUP BY m.ShedId HAVING SUM(m.Qty) > 0)" + Ctx.ShedFilter("v.ShedId") + " ORDER BY v.Code",
                    null, Ctx.Scope(Db.P("@f", flock.Value), Db.P("@d", d)));
            From_Changed(null, null);
        }

        /// <summary>Destination list, rebuilt whenever the flock or the From shed changes (so a shed hidden as "from" comes back).</summary>
        void BindTo(int flockId, int? from)
        {
            string keep = ddlTo.SelectedValue;
            ddlTo.Items.Clear();
            var flock = (int?)flockId;
            // every shed is listed; the ones that cannot take this flock are greyed out with the reason, so it is clear why
            ddlTo.Items.Add(new ListItem("-- select destination --", ""));
            var dest = Db.Query(@"SELECT v.ShedId, v.Code, v.Name, v.Purpose, v.Status, v.IsActive, v.Birds, v.FlockId, v.FlockCode
                                  FROM dbo.vShedInfo v WHERE v.CompanyId = @CompanyId
                                  ORDER BY CASE WHEN v.IsActive = 1 AND v.Status IN (N'Active', N'Vacant') AND (v.Birds = 0 OR v.FlockId = @f) THEN 0 ELSE 1 END,
                                           CASE v.Purpose WHEN N'Laying' THEN 0 WHEN N'Grower' THEN 1 ELSE 2 END, v.Code", Ctx.Scope(Db.P("@f", flock.Value)));
            foreach (DataRow r in dest.Rows)
            {
                if (from.HasValue && Db.Int(r["ShedId"]) == from.Value) continue;   // cannot move into the same shed
                int birds = Db.Int(r["Birds"]);
                bool same = r["FlockId"] != DBNull.Value && Db.Int(r["FlockId"]) == flock.Value;
                string status = Db.Str(r["Status"]), why = null;
                if (!Convert.ToBoolean(r["IsActive"])) why = "shed inactive";
                else if (status != "Active" && status != "Vacant") why = "status " + status;
                else if (birds > 0 && !same) why = "holds flock " + Db.Str(r["FlockCode"]) + ", " + birds.ToString("N0") + " birds";
                string text = Db.Str(r["Code"]) + " - " + Db.Str(r["Name"]) + " [" + Db.Str(r["Purpose"]) + ", " +
                              (why ?? status + (birds > 0 ? ", " + birds.ToString("N0") + " birds (same flock)" : ", empty")) + "]";
                var li = new ListItem(text, Db.Str(r["ShedId"]));
                if (why != null) { li.Attributes["disabled"] = "disabled"; li.Attributes["style"] = "color:#999"; }
                ddlTo.Items.Add(li);
            }
            if (!string.IsNullOrEmpty(keep) && ddlTo.Items.FindByValue(keep) != null && ddlTo.Items.FindByValue(keep).Attributes["disabled"] == null)
                ddlTo.SelectedValue = keep;
        }

        protected void From_Changed(object sender, EventArgs e)
        {
            var flock = Ui.SelInt(ddlFlock); var from = Ui.SelInt(ddlFrom);
            if (flock.HasValue) BindTo(flock.Value, from);
            if (!flock.HasValue || !from.HasValue) { litInfo.Text = ""; return; }
            var d = TDate;
            int movable = Movable(null, null, from.Value, flock.Value, d, null);
            var f = Ops.Flock(flock.Value);
            var hatch = Convert.ToDateTime(f["HatchDate"]);
            int ageDays = Age.Days(hatch, d);
            litInfo.Text = "<div class=\"calc-chip\">Movable birds <b>" + Ui.N0(movable) + "</b> &middot; age at movement <b>" + Age.Text(hatch, d) + "</b> (" + ageDays +
                           " d) &middot; stage <b>" + Company.StageOf(ageDays, Ops.IsBroiler(f)) + "</b></div>";
            if (chkFull.Checked) { txtCount.Text = movable.ToString(CultureInfo.InvariantCulture); txtCount.Enabled = false; }
            else txtCount.Enabled = true;
        }

        protected void btnSubmit_Click(object sender, EventArgs e)
        {
            var err = new List<string>();
            var d = TDate;
            var flock = Ui.SelInt(ddlFlock); var from = Ui.SelInt(ddlFrom); var to = Ui.SelInt(ddlTo);
            if (d > DateTime.Today) err.Add("Transfer date cannot be in the future.");
            if (!flock.HasValue) err.Add("Select the flock.");
            if (!from.HasValue) err.Add("Select the source shed.");
            if (!to.HasValue) err.Add("Select the destination shed.");
            if (from.HasValue && to.HasValue && from == to) err.Add("Source and destination must be different sheds.");
            if (from.HasValue && !Ctx.CanAccessShed(from.Value)) err.Add("The source shed is not assigned to you.");
            var n = Ui.Int(txtCount.Text); var tm = Ui.Int(txtTMort.Text) ?? 0; var tc = Ui.Int(txtTCull.Text) ?? 0;
            if (!n.HasValue || n <= 0) err.Add("Birds dispatched must be a positive number.");
            if (tm < 0 || tc < 0) err.Add("Transit losses cannot be negative.");
            if (n.HasValue && tm + tc > n) err.Add("Transit losses cannot exceed birds dispatched.");
            if (from.HasValue && flock.HasValue && n.HasValue)
            {
                int movable = Movable(null, null, from.Value, flock.Value, d, null);
                if (chkFull.Checked) n = movable;
                if (n > movable) err.Add("Only " + movable.ToString("N0") + " birds can leave this shed on " + Ui.D(d) + ".");
            }
            if (err.Count > 0) { ShowErr(err); return; }

            bool approveNow = Ctx.IsManager && chkApproveNow.Checked;
            try
            {
                int id = Db.Tx((c, t) =>
                {
                    var msg = Ops.CheckDestination(c, t, to.Value, flock.Value, d);
                    if (msg != null) throw new InvalidOperationException(msg);
                    int tid = Db.Int(Db.Scalar(c, t, @"INSERT INTO dbo.FlockTransfer (CompanyId, FlockId, FromShedId, ToShedId, TransferDate, BirdsDispatched, TransitMortality, TransitCulls,
                                                       IsFullTransfer, Status, Remarks, RequestedBy) VALUES (@c, @f, @fr, @to, @d, @n, @tm, @tc, @full, N'Pending', @rem, @u);
                                                       SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        Db.P("@c", Ctx.CompanyId), Db.P("@f", flock.Value), Db.P("@fr", from.Value), Db.P("@to", to.Value), Db.P("@d", d), Db.P("@n", n.Value),
                        Db.P("@tm", tm), Db.P("@tc", tc), Db.P("@full", chkFull.Checked), Db.P("@rem", Nz(txtRemarks.Text)), Db.P("@u", Ctx.UserId)));
                    Audit.Log(c, t, "REQUEST", "FlockTransfer", tid, "flock " + flock + " " + from + "->" + to + " birds " + n + " transit " + tm + "/" + tc);
                    if (approveNow) Approve(c, t, tid, null);
                    return tid;
                });
                Ok(approveNow ? "Transfer #" + id + " approved and applied." : "Transfer request #" + id + " submitted for manager approval.");
                txtCount.Text = ""; txtTMort.Text = "0"; txtTCull.Text = "0"; txtRemarks.Text = ""; chkFull.Checked = false; pnlErr.Visible = false;
                BindFlocks(); Source_Changed(null, null); BindLists();
            }
            catch (Exception ex) { ShowErr(new List<string> { Db.FriendlyError(ex) }); }
        }

        /// <summary>Applies a pending transfer after re-validating stock and destination inside the transaction.</summary>
        static void Approve(SqlConnection c, SqlTransaction t, int transferId, string note)
        {
            var r = Db.Query(c, t, "SELECT * FROM dbo.FlockTransfer WITH (UPDLOCK, HOLDLOCK) WHERE TransferId = @id AND CompanyId = @c",
                Db.P("@id", transferId), Db.P("@c", Ctx.CompanyId));
            if (r.Rows.Count == 0) throw new InvalidOperationException("Transfer not found.");
            var row = r.Rows[0];
            if (Db.Str(row["Status"]) != "Pending") throw new InvalidOperationException("This transfer is already " + Db.Str(row["Status"]) + ".");
            int flock = Db.Int(row["FlockId"]), from = Db.Int(row["FromShedId"]), to = Db.Int(row["ToShedId"]), n = Db.Int(row["BirdsDispatched"]);
            var d = Convert.ToDateTime(row["TransferDate"]);
            int movable = Movable(c, t, from, flock, d, transferId);
            if (n > movable) throw new InvalidOperationException("Only " + movable.ToString("N0") + " birds are available in the source shed on " + d.ToString("dd-MM-yyyy") + ".");
            var msg = Ops.CheckDestination(c, t, to, flock, d);
            if (msg != null) throw new InvalidOperationException(msg);
            Db.Exec(c, t, "UPDATE dbo.FlockTransfer SET Status = N'Approved', ApprovedBy = @u, ApprovedOn = SYSDATETIME(), ApprovalNote = @n WHERE TransferId = @id",
                Db.P("@u", Ctx.UserId), Db.P("@n", note), Db.P("@id", transferId));
            Ops.EnsureNoNegative(c, t, from, flock);
            Ops.EnsureNoNegative(c, t, to, flock);
            Ops.MarkShedOccupied(c, t, to);
            Ops.MarkShedVacantIfEmpty(c, t, from);
            Audit.Log(c, t, "APPROVE", "FlockTransfer", transferId, note);
        }

        protected void btnCmd_Click(object sender, EventArgs e)
        {
            var cmd = (hidCmd.Value ?? "").Split(':'); hidCmd.Value = "";
            int id;
            if (cmd.Length != 2 || !int.TryParse(cmd[1], out id)) return;
            var note = Nz(txtNote.Text);
            try
            {
                switch (cmd[0])
                {
                    case "approve":
                        if (!Ctx.IsManager) return;
                        Db.Tx((c, t) => { Approve(c, t, id, note); return 0; });
                        var cap = Db.Row(@"SELECT s.Code, s.Capacity, (SELECT ISNULL(SUM(Qty),0) FROM dbo.vBirdMovement m WHERE m.ShedId = s.ShedId) AS Birds
                                           FROM dbo.FlockTransfer t JOIN dbo.Shed s ON s.ShedId = t.ToShedId WHERE t.TransferId = @id", Db.P("@id", id));
                        if (cap != null && Db.Int(cap["Birds"]) > Db.Int(cap["Capacity"]))
                            Warn("Approved. Note: shed " + Db.Str(cap["Code"]) + " now holds " + Ui.N0(cap["Birds"]) + " birds, above its capacity of " + Ui.N0(cap["Capacity"]) + ".");
                        else Ok("Transfer #" + id + " approved.");
                        break;
                    case "reject":
                        if (!Ctx.IsManager) return;
                        Db.Exec("UPDATE dbo.FlockTransfer SET Status = N'Rejected', ApprovedBy = @u, ApprovedOn = SYSDATETIME(), ApprovalNote = @n WHERE TransferId = @id AND CompanyId = @c AND Status = N'Pending'",
                            Db.P("@u", Ctx.UserId), Db.P("@n", note), Db.P("@id", id), Db.P("@c", Ctx.CompanyId));
                        Audit.Log("REJECT", "FlockTransfer", id, note);
                        Ok("Transfer #" + id + " rejected.");
                        break;
                    case "cancel":
                        int n = Db.Exec("DELETE FROM dbo.FlockTransfer WHERE TransferId = @id AND CompanyId = @c AND Status = N'Pending' AND (@m = 1 OR RequestedBy = @u)",
                            Db.P("@id", id), Db.P("@c", Ctx.CompanyId), Db.P("@m", Ctx.IsManager ? 1 : 0), Db.P("@u", Ctx.UserId));
                        if (n > 0) { Audit.Log("CANCEL", "FlockTransfer", id, null); Ok("Request withdrawn."); }
                        break;
                }
                txtNote.Text = "";
            }
            catch (Exception ex) { Fail(Db.FriendlyError(ex)); }
            BindFlocks(); Source_Changed(null, null); BindLists();
        }

        void BindLists()
        {
            const string sql = @"SELECT TOP (@top) t.*, f.FlockCode, f.HatchDate, fs.Code AS FromCode, ts.Code AS ToCode, ru.FullName AS ReqName, au.FullName AS AppName
                                 FROM dbo.FlockTransfer t JOIN dbo.Flock f ON f.FlockId = t.FlockId JOIN dbo.Shed fs ON fs.ShedId = t.FromShedId JOIN dbo.Shed ts ON ts.ShedId = t.ToShedId
                                 LEFT JOIN dbo.AppUser ru ON ru.UserId = t.RequestedBy LEFT JOIN dbo.AppUser au ON au.UserId = t.ApprovedBy
                                 WHERE t.CompanyId = @CompanyId AND t.Status {0}
                                 AND (@Restricted = 0 OR t.FromShedId IN (SELECT ShedId FROM dbo.UserShed WHERE UserId = @UserId) OR t.ToShedId IN (SELECT ShedId FROM dbo.UserShed WHERE UserId = @UserId))
                                 ORDER BY t.TransferDate DESC, t.TransferId DESC";
            var pending = Db.Query(string.Format(sql, "= N'Pending'"), Ctx.Scope(Db.P("@top", 100)));
            var hist = Db.Query(string.Format(sql, "<> N'Pending'"), Ctx.Scope(Db.P("@top", 25)));
            litPending.Text = Table(pending, true);
            litHistory.Text = Table(hist, false);
        }

        string Table(DataTable dt, bool pending)
        {
            if (dt.Rows.Count == 0) return "<div class=\"empty-state py-3\">" + (pending ? "No requests waiting." : "No movements yet.") + "</div>";
            var sb = new StringBuilder("<table class=\"table table-erp\"><thead><tr><th>#</th><th>Date</th><th>Flock</th><th>Age</th><th>From &rarr; To</th><th class=\"num\">Sent</th><th class=\"num\">Transit loss</th><th class=\"num\">Received</th><th>" +
                                       (pending ? "Requested by" : "Status") + "</th><th></th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                var d = Convert.ToDateTime(r["TransferDate"]);
                int id = Db.Int(r["TransferId"]), sent = Db.Int(r["BirdsDispatched"]), loss = Db.Int(r["TransitMortality"]) + Db.Int(r["TransitCulls"]);
                sb.Append("<tr><td>").Append(id).Append("</td><td>").Append(Ui.D(d)).Append("</td><td><b>").Append(Ui.H(r["FlockCode"])).Append("</b></td><td>")
                  .Append(Age.Text(Convert.ToDateTime(r["HatchDate"]), d)).Append("</td><td>").Append(Ui.H(r["FromCode"])).Append(" &rarr; ").Append(Ui.H(r["ToCode"]))
                  .Append(Convert.ToBoolean(r["IsFullTransfer"]) ? " <span class=\"badge text-bg-light border\">full</span>" : "")
                  .Append("</td><td class=\"num\">").Append(Ui.N0(sent)).Append("</td><td class=\"num\">").Append(Ui.N0(loss))
                  .Append(sent > 0 && loss > 0 ? " <span class=\"small text-muted-2\">(" + Ui.N2(loss * 100m / sent) + "%)</span>" : "")
                  .Append("</td><td class=\"num\">").Append(Ui.N0(r["BirdsReceived"])).Append("</td><td class=\"small\">");
                if (pending) sb.Append(Ui.H(r["ReqName"])).Append("<div class=\"text-muted-2\">").Append(Ui.H(r["Remarks"])).Append("</div>");
                else sb.Append(Ui.StatusBadge(Db.Str(r["Status"]))).Append(" <span class=\"text-muted-2\">").Append(Ui.H(r["AppName"])).Append("</span>");
                sb.Append("</td><td class=\"text-end text-nowrap\">");
                if (pending)
                {
                    if (Ctx.IsManager)
                        sb.Append("<a href=\"#\" class=\"btn btn-success\" onclick=\"return trfCmd('approve:").Append(id).Append("','Approve and move the birds?')\"><i class=\"bi bi-check2\"></i></a> ")
                          .Append("<a href=\"#\" class=\"btn btn-outline-danger\" onclick=\"return trfCmd('reject:").Append(id).Append("','Reject this request?')\"><i class=\"bi bi-x-lg\"></i></a> ");
                    if (Ctx.IsManager || Db.Int(r["RequestedBy"]) == Ctx.UserId)
                        sb.Append("<a href=\"#\" class=\"btn btn-outline-secondary\" title=\"Withdraw\" onclick=\"return trfCmd('cancel:").Append(id).Append("','Withdraw this request?')\"><i class=\"bi bi-trash\"></i></a>");
                }
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        void ShowErr(List<string> err)
        {
            pnlErr.Visible = true;
            var sb = new StringBuilder();
            foreach (var x in err) sb.Append("<div><i class=\"bi bi-exclamation-triangle me-1\"></i>").Append(Ui.H(x)).Append("</div>");
            litErr.Text = sb.ToString();
        }

        static string Nz(string s) { s = (s ?? "").Trim(); return s.Length == 0 ? null : s; }
    }
}
