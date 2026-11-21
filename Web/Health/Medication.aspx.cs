using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

namespace PoultryERP
{
    public partial class MedicationPage : BasePage
    {
        protected override string[] AllowedRoles { get { return new[] { Roles.SuperAdmin, Roles.FarmManager, Roles.Supervisor, Roles.Veterinarian }; } }
        bool IsVetOrManager { get { return Ctx.InRole(Roles.SuperAdmin, Roles.FarmManager, Roles.Veterinarian); } }

        protected void Page_Load(object sender, EventArgs e)
        {
            pnlGen.Visible = IsVetOrManager;
            if (IsPostBack) return;
            fFrom.Text = Ui.DateValue(DateTime.Today.AddDays(-14));
            fTo.Text = Ui.DateValue(DateTime.Today.AddDays(30));
            Ui.Bind(ddlShed, "SELECT v.ShedId AS Value, v.Code + ' - ' + v.Name + ISNULL(' [' + v.FlockCode + ']', '') AS Text FROM dbo.vShedInfo v WHERE v.CompanyId = @CompanyId AND v.Birds > 0" +
                             Ctx.ShedFilter("v.ShedId") + " ORDER BY v.Code", "-- select shed --", Ctx.Scope());
            Ui.Bind(ddlGenFlock, "SELECT FlockId AS Value, FlockCode + ' (hatch ' + CONVERT(VARCHAR(10), HatchDate, 105) + ')' AS Text FROM dbo.Flock WHERE CompanyId = @c AND Status = N'Active' ORDER BY HatchDate DESC",
                    "-- select flock --", Db.P("@c", Ctx.CompanyId));
            Reset();
            BindList();
        }

        void BindItems()
        {
            var cat = ddlKind.SelectedValue == "Vaccination" ? "VACCINE" : "MEDICINE";
            Ui.Bind(ddlItem, "SELECT ItemId AS Value, Name + ' [' + Unit + ']' AS Text FROM dbo.Item WHERE CompanyId = @c AND Category = @cat AND IsActive = 1 ORDER BY Name",
                    "-- select --", Db.P("@c", Ctx.CompanyId), Db.P("@cat", cat));
        }

        protected void Kind_Changed(object sender, EventArgs e) { BindItems(); }

        void Reset()
        {
            hidId.Value = "";
            txtDate.Text = Ui.DateValue(DateTime.Today);
            txtQty.Text = txtDose.Text = txtReason.Text = "";
            txtBy.Text = Ctx.User.FullName;
            ddlStatus.SelectedValue = "Done";
            BindItems();
            litFormTitle.Text = "Record medication / vaccination";
            pnlErr.Visible = false;
        }

        protected void btnReset_Click(object sender, EventArgs e) { Reset(); }
        protected void btnFilter_Click(object sender, EventArgs e) { BindList(); }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            var err = new List<string>();
            var d = Ui.Date(txtDate.Text);
            var shed = Ui.SelInt(ddlShed); var item = Ui.SelInt(ddlItem);
            var qty = Ui.Dec(txtQty.Text) ?? 0;
            var status = ddlStatus.SelectedValue;
            if (!d.HasValue) err.Add("Date is required.");
            if (!shed.HasValue) err.Add("Select the shed."); else if (!Ctx.CanAccessShed(shed.Value)) err.Add("This shed is not assigned to you.");
            if (!item.HasValue) err.Add("Select the item.");
            if (qty < 0) err.Add("Quantity cannot be negative.");
            if (d.HasValue && status == "Done" && d > DateTime.Today) err.Add("A future date can only be saved as Planned.");
            if (d.HasValue && shed.HasValue && status == "Done" && Ctx.Role == Roles.Supervisor)
            {
                string reason;
                if (!Ops.CanEditDay(shed.Value, d.Value, out reason)) err.Add(reason);
            }
            ShedPosition occ = null;
            if (d.HasValue && shed.HasValue) occ = Ops.Occupant(shed.Value, d.Value > DateTime.Today ? DateTime.Today : d.Value);
            if (err.Count == 0 && occ == null) err.Add("The shed has no flock on that date.");
            if (err.Count > 0) { ShowErr(err); return; }

            var kind = ddlKind.SelectedValue;
            // vaccinations entered by supervisors need vet / manager approval
            string appr = kind == "Vaccination" ? (IsVetOrManager ? "Approved" : "Pending") : "Not Required";
            int? id = string.IsNullOrEmpty(hidId.Value) ? (int?)null : int.Parse(hidId.Value, CultureInfo.InvariantCulture);
            var ps = new List<System.Data.SqlClient.SqlParameter>
            {
                Db.P("@c", Ctx.CompanyId), Db.P("@s", shed.Value), Db.P("@f", occ.FlockId), Db.P("@d", d.Value), Db.P("@k", kind), Db.P("@i", item.Value), Db.P("@q", qty),
                Db.P("@dose", Nz(txtDose.Text)), Db.P("@route", Nz(ddlRoute.SelectedValue)), Db.P("@reason", Nz(txtReason.Text)), Db.P("@by", Nz(txtBy.Text)),
                Db.P("@st", status), Db.P("@ap", appr), Db.P("@u", Ctx.UserId), Db.P("@vet", IsVetOrManager ? 1 : 0)
            };
            try
            {
                if (id.HasValue)
                {
                    ps.Add(Db.P("@id", id.Value));
                    Db.Exec(@"UPDATE dbo.MedicationLog SET ShedId = @s, FlockId = @f, LogDate = @d, Kind = @k, ItemId = @i, Qty = @q, Dose = @dose, Route = @route, Reason = @reason,
                              AdministeredBy = @by, Status = @st, ApprovalStatus = CASE WHEN @k = N'Vaccination' AND @vet = 0 THEN N'Pending' WHEN @k = N'Vaccination' THEN N'Approved' ELSE N'Not Required' END,
                              ApprovedBy = CASE WHEN @k = N'Vaccination' AND @vet = 1 THEN @u ELSE NULL END, ApprovedOn = CASE WHEN @k = N'Vaccination' AND @vet = 1 THEN SYSDATETIME() ELSE NULL END
                              WHERE MedLogId = @id AND CompanyId = @c", ps.ToArray());
                    Audit.Log("UPDATE", "MedicationLog", id.Value, kind + " item " + item + " qty " + qty + " " + status);
                }
                else
                {
                    var nid = Db.Scalar(@"INSERT INTO dbo.MedicationLog (CompanyId, ShedId, FlockId, LogDate, Kind, ItemId, Qty, Dose, Route, Reason, AdministeredBy, Status, ApprovalStatus, ApprovedBy, ApprovedOn, CreatedBy)
                                          VALUES (@c, @s, @f, @d, @k, @i, @q, @dose, @route, @reason, @by, @st, @ap, CASE WHEN @ap = N'Approved' THEN @u END, CASE WHEN @ap = N'Approved' THEN SYSDATETIME() END, @u);
                                          SELECT CAST(SCOPE_IDENTITY() AS INT);", ps.ToArray());
                    Audit.Log("INSERT", "MedicationLog", nid, kind + " item " + item + " qty " + qty + " " + status);
                }
                Ok(appr == "Pending" ? "Saved - waiting for veterinarian approval." : "Saved.");
                Reset();
                BindList();
            }
            catch (Exception ex) { ShowErr(new List<string> { Db.FriendlyError(ex) }); }
        }

        protected void btnGen_Click(object sender, EventArgs e)
        {
            if (!IsVetOrManager) return;
            var flock = Ui.SelInt(ddlGenFlock);
            if (!flock.HasValue) { Warn("Select a flock."); return; }
            var f = Ops.Flock(flock.Value);
            var occ = Db.Row("SELECT TOP (1) ShedId, Birds FROM dbo.vShedOccupancy WHERE FlockId = @f AND Birds > 0 ORDER BY Birds DESC", Db.P("@f", flock.Value));
            if (occ == null) { Fail("The flock has no birds in any shed."); return; }
            int birds = Db.Int(Db.Scalar("SELECT ISNULL(SUM(Birds),0) FROM dbo.vShedOccupancy WHERE FlockId = @f", Db.P("@f", flock.Value)));
            int n = Db.Exec(@"INSERT INTO dbo.MedicationLog (CompanyId, ShedId, FlockId, LogDate, Kind, ItemId, Qty, Dose, Route, Reason, Status, ApprovalStatus, ApprovedBy, ApprovedOn, CreatedBy)
                              SELECT @c, @s, @f, DATEADD(DAY, v.AgeDay, @hatch), N'Vaccination', v.ItemId, CEILING(@birds * v.DosePerBird), CAST(v.DosePerBird AS NVARCHAR(20)) + N' dose/bird', v.Route, v.Notes,
                                     N'Planned', N'Approved', @u, SYSDATETIME(), @u
                              FROM dbo.VaccineSchedule v
                              WHERE v.CompanyId = @c AND (v.BreedId = @b OR (v.BreedId IS NULL AND @type = N'Layer')) AND DATEADD(DAY, v.AgeDay, @hatch) >= CAST(GETDATE() AS DATE)
                                AND NOT EXISTS (SELECT 1 FROM dbo.MedicationLog m WHERE m.FlockId = @f AND m.ItemId = v.ItemId AND m.LogDate = DATEADD(DAY, v.AgeDay, @hatch))",
                Db.P("@c", Ctx.CompanyId), Db.P("@s", Db.Int(occ["ShedId"])), Db.P("@f", flock.Value), Db.P("@hatch", Convert.ToDateTime(f["HatchDate"])),
                Db.P("@birds", birds), Db.P("@b", Db.Int(f["BreedId"])), Db.P("@u", Ctx.UserId), Db.P("@type", Ops.IsBroiler(f) ? "Broiler" : "Layer"));
            Audit.Log("GENERATE", "MedicationLog", flock.Value, n + " planned vaccinations");
            Ok(n == 0 ? "Nothing to add - the plan is already up to date." : n + " planned vaccination(s) added for " + Db.Str(f["FlockCode"]) + ".");
            BindList();
        }

        protected void btnCmd_Click(object sender, EventArgs e)
        {
            var cmd = (hidCmd.Value ?? "").Split(':'); hidCmd.Value = "";
            int id;
            if (cmd.Length != 2 || !int.TryParse(cmd[1], out id)) return;
            var row = Db.Row("SELECT * FROM dbo.MedicationLog WHERE MedLogId = @id AND CompanyId = @c", Db.P("@id", id), Db.P("@c", Ctx.CompanyId));
            if (row == null || !Ctx.CanAccessShed(Db.Int(row["ShedId"]))) return;
            switch (cmd[0])
            {
                case "done":
                    var d = Convert.ToDateTime(row["LogDate"]);
                    if (d > DateTime.Today) { Fail("This is planned for " + Ui.D(d) + " - it can be marked done on or after that date."); break; }
                    Db.Exec("UPDATE dbo.MedicationLog SET Status = N'Done', AdministeredBy = ISNULL(AdministeredBy, @n) WHERE MedLogId = @id", Db.P("@id", id), Db.P("@n", Ctx.User.FullName));
                    Audit.Log("DONE", "MedicationLog", id, null); Ok("Marked as done."); break;
                case "skip":
                    Db.Exec("UPDATE dbo.MedicationLog SET Status = N'Skipped' WHERE MedLogId = @id", Db.P("@id", id));
                    Audit.Log("SKIP", "MedicationLog", id, null); Ok("Marked as skipped."); break;
                case "approve":
                case "reject":
                    if (!IsVetOrManager) return;
                    Db.Exec("UPDATE dbo.MedicationLog SET ApprovalStatus = @s, ApprovedBy = @u, ApprovedOn = SYSDATETIME() WHERE MedLogId = @id",
                        Db.P("@s", cmd[0] == "approve" ? "Approved" : "Rejected"), Db.P("@u", Ctx.UserId), Db.P("@id", id));
                    Audit.Log(cmd[0].ToUpperInvariant(), "MedicationLog", id, null); Ok(cmd[0] == "approve" ? "Vaccination approved." : "Vaccination rejected."); break;
                case "edit":
                    hidId.Value = id.ToString(CultureInfo.InvariantCulture);
                    txtDate.Text = Ui.DateValue(row["LogDate"]); ddlKind.SelectedValue = Db.Str(row["Kind"]); BindItems();
                    Ui.Select(ddlShed, row["ShedId"]); Ui.Select(ddlItem, row["ItemId"]); txtQty.Text = Ui.NumValue(row["Qty"]);
                    txtDose.Text = Db.Str(row["Dose"]); Ui.Select(ddlRoute, row["Route"]); Ui.Select(ddlStatus, row["Status"]);
                    txtReason.Text = Db.Str(row["Reason"]); txtBy.Text = Db.Str(row["AdministeredBy"]);
                    litFormTitle.Text = "Edit entry #" + id; pnlErr.Visible = false;
                    return;
                case "del":
                    if (!IsVetOrManager && Db.Int(row["CreatedBy"]) != Ctx.UserId) { Fail("Only the person who entered it, a vet or a manager can delete this."); return; }
                    Db.Exec("DELETE FROM dbo.MedicationLog WHERE MedLogId = @id", Db.P("@id", id));
                    Audit.Log("DELETE", "MedicationLog", id, null); Ok("Deleted."); break;
            }
            BindList();
        }

        void BindList()
        {
            var from = Ui.Date(fFrom.Text) ?? DateTime.Today.AddDays(-14);
            var to = Ui.Date(fTo.Text) ?? DateTime.Today.AddDays(30);
            var show = fShow.SelectedValue ?? "";
            var dt = Db.Query(@"SELECT TOP (300) m.*, s.Code AS ShedCode, f.FlockCode, f.HatchDate, i.Name AS ItemName, i.Unit, au.FullName AS ApprName
                                FROM dbo.MedicationLog m JOIN dbo.Shed s ON s.ShedId = m.ShedId JOIN dbo.Flock f ON f.FlockId = m.FlockId JOIN dbo.Item i ON i.ItemId = m.ItemId
                                LEFT JOIN dbo.AppUser au ON au.UserId = m.ApprovedBy
                                WHERE m.CompanyId = @CompanyId AND m.LogDate BETWEEN @f AND @t" + Ctx.ShedFilter("m.ShedId") + @"
                                AND (@show = '' OR (@show = 'due' AND m.Status = N'Planned') OR (@show = 'appr' AND m.ApprovalStatus = N'Pending') OR m.Kind = @show)
                                ORDER BY CASE WHEN m.Status = N'Planned' AND m.LogDate <= CAST(GETDATE() AS DATE) THEN 0 ELSE 1 END, m.LogDate DESC",
                                Ctx.Scope(Db.P("@f", from), Db.P("@t", to), Db.P("@show", show)));
            if (dt.Rows.Count == 0) { litList.Text = "<div class=\"empty-state\"><i class=\"bi bi-capsule\"></i>No entries for these filters.</div>"; return; }
            var sb = new StringBuilder("<table class=\"table table-erp table-hover\"><thead><tr><th>Date</th><th>Shed / flock</th><th>Age</th><th>Item</th><th class=\"num\">Qty</th><th>Route</th><th>Status</th><th>Approval</th><th class=\"text-end\">Actions</th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                int id = Db.Int(r["MedLogId"]);
                var d = Convert.ToDateTime(r["LogDate"]);
                var st = Db.Str(r["Status"]); var ap = Db.Str(r["ApprovalStatus"]);
                bool overdue = st == "Planned" && d < DateTime.Today;
                sb.Append("<tr").Append(overdue ? " class=\"table-warning\"" : "").Append("><td>").Append(Ui.D(d)).Append(overdue ? " <span class=\"badge text-bg-danger\">overdue</span>" : "")
                  .Append("</td><td><b>").Append(Ui.H(r["ShedCode"])).Append("</b> <span class=\"text-muted-2\">").Append(Ui.H(r["FlockCode"])).Append("</span></td><td>")
                  .Append(Age.Text(Convert.ToDateTime(r["HatchDate"]), d)).Append("</td><td>").Append(Db.Str(r["Kind"]) == "Vaccination" ? "<i class=\"bi bi-shield-plus text-success me-1\" title=\"Vaccination\"></i>" : "<i class=\"bi bi-capsule me-1\"></i>")
                  .Append(Ui.H(r["ItemName"])).Append("<div class=\"small text-muted-2\">").Append(Ui.H(r["Reason"])).Append("</div></td><td class=\"num\">").Append(Ui.N1(r["Qty"])).Append(" ").Append(Ui.H(r["Unit"]))
                  .Append("</td><td>").Append(Ui.H(r["Route"])).Append("</td><td>").Append(Ui.StatusBadge(st)).Append("</td><td>")
                  .Append(ap == "Not Required" ? "<span class=\"text-muted-2 small\">-</span>" : Ui.StatusBadge(ap) + (r["ApprName"] == DBNull.Value ? "" : "<div class=\"small text-muted-2\">" + Ui.H(r["ApprName"]) + "</div>"))
                  .Append("</td><td class=\"text-end text-nowrap\">");
                if (st == "Planned") sb.Append("<a href=\"#\" class=\"btn btn-success\" title=\"Mark done\" onclick=\"return medCmd('done:").Append(id).Append("')\"><i class=\"bi bi-check2\"></i></a> ")
                                       .Append("<a href=\"#\" class=\"btn btn-outline-secondary\" title=\"Skip\" onclick=\"return medCmd('skip:").Append(id).Append("','Mark as skipped?')\"><i class=\"bi bi-slash-circle\"></i></a> ");
                if (ap == "Pending" && IsVetOrManager) sb.Append("<a href=\"#\" class=\"btn btn-outline-success\" title=\"Approve\" onclick=\"return medCmd('approve:").Append(id).Append("')\"><i class=\"bi bi-patch-check\"></i></a> ")
                                                     .Append("<a href=\"#\" class=\"btn btn-outline-danger\" title=\"Reject\" onclick=\"return medCmd('reject:").Append(id).Append("','Reject this vaccination record?')\"><i class=\"bi bi-patch-minus\"></i></a> ");
                sb.Append("<a href=\"#\" class=\"btn btn-outline-primary\" title=\"Edit\" onclick=\"return medCmd('edit:").Append(id).Append("')\"><i class=\"bi bi-pencil\"></i></a> ")
                  .Append("<a href=\"#\" class=\"btn btn-outline-danger\" title=\"Delete\" onclick=\"return medCmd('del:").Append(id).Append("','Delete this entry?')\"><i class=\"bi bi-trash\"></i></a>");
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            litList.Text = sb.ToString();
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
