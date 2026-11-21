using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web.UI.WebControls;

namespace PoultryERP
{
    public partial class FlocksPage : BasePage
    {
        protected override string[] AllowedRoles { get { return new[] { Roles.SuperAdmin, Roles.FarmManager, Roles.Veterinarian, Roles.Accountant }; } }
        bool CanEdit { get { return Ctx.IsManager; } }
        int? FlockId { get { int v; return int.TryParse(hidFlock.Value, out v) ? v : (int?)null; } }

        const string FreeShedsSql = @"SELECT v.ShedId AS Value, v.Code + ' - ' + v.Name + ' (' + v.Purpose + ', cap ' + CAST(v.Capacity AS VARCHAR(10)) + ')' AS Text
                                      FROM dbo.vShedInfo v WHERE v.CompanyId = @c AND v.IsActive = 1 AND v.Status IN (N'Active', N'Vacant') AND (v.Birds = 0 OR v.FlockId = @f)
                                      ORDER BY CASE WHEN @br = 1 AND v.Purpose = N'Broiler' THEN 0 WHEN @br = 0 AND v.Purpose = N'Brooding' THEN 0 WHEN v.Purpose = N'Grower' THEN 1 ELSE 2 END, v.Code";

        protected void Page_Load(object sender, EventArgs e)
        {
            btnNew.Visible = CanEdit;
            if (IsPostBack) return;
            Ui.Bind(ddlSupplier, "SELECT SupplierId AS Value, Name AS Text FROM dbo.Supplier WHERE CompanyId = @c AND SupplierType = 'Hatchery' AND IsActive = 1 ORDER BY Name", "(not specified)", Db.P("@c", Ctx.CompanyId));
            BindBreeds();
            BindList();
        }

        protected void Filter_Changed(object sender, EventArgs e) { BindList(); }

        bool BroilerSelected { get { return ddlType.SelectedValue == "Broiler"; } }

        void BindBreeds()
        {
            Ui.Bind(ddlBreed, "SELECT BreedId AS Value, Name AS Text FROM dbo.Breed WHERE CompanyId = @c AND IsActive = 1 AND BirdType = @t ORDER BY Name", "-- select --",
                    Db.P("@c", Ctx.CompanyId), Db.P("@t", ddlType.SelectedValue));
            if (ddlBreed.Items.Count == 2) ddlBreed.SelectedIndex = 1;
        }

        protected void Type_Changed(object sender, EventArgs e)
        {
            BindBreeds();
            phBroiler.Visible = BroilerSelected;
            phLayer.Visible = !BroilerSelected;
            // new inward only (editing an existing flock locks the bird type). Do not test phFirstPlace.Visible here:
            // it reads false while the parent pnlForm is still hidden, so the shed list was never filled.
            if (string.IsNullOrEmpty(hidFlock.Value))
            {
                Ui.Bind(ddlPlaceShed, FreeShedsSql, "-- select shed --", Db.P("@c", Ctx.CompanyId), Db.P("@f", 0), Db.P("@br", BroilerSelected ? 1 : 0));
                if (ddlPlaceShed.Items.Count == 1) ddlPlaceShed.Items[0].Text = "-- no empty shed (Active / Vacant with 0 birds) --";
                if (string.IsNullOrEmpty(hidFlock.Value)) txtCode.Text = NextCode();
                if (BroilerSelected) txtHatch.Text = txtArrival.Text;
            }
        }

        void BindList()
        {
            var dt = Db.Query(@"
SELECT f.*, b.Name AS BreedName, sp.Name AS Hatchery,
       ISNULL((SELECT SUM(Qty) FROM dbo.vBirdMovement m WHERE m.FlockId = f.FlockId), 0) AS Birds,
       ISNULL((SELECT SUM(BirdCount) FROM dbo.FlockPlacement p WHERE p.FlockId = f.FlockId), 0) AS Placed,
       STUFF((SELECT ', ' + s.Code FROM dbo.vShedOccupancy o JOIN dbo.Shed s ON s.ShedId = o.ShedId WHERE o.FlockId = f.FlockId AND o.Birds > 0 ORDER BY s.Code FOR XML PATH('')), 1, 2, '') AS Sheds
FROM dbo.Flock f JOIN dbo.Breed b ON b.BreedId = f.BreedId LEFT JOIN dbo.Supplier sp ON sp.SupplierId = f.SupplierId
WHERE f.CompanyId = @c AND (@st = '' OR f.Status = @st OR (@st = 'L' AND f.Status = N'Active' AND f.FlockType = N'Layer') OR (@st = 'B' AND f.Status = N'Active' AND f.FlockType = N'Broiler'))
ORDER BY f.HatchDate DESC", Db.P("@c", Ctx.CompanyId), Db.P("@st", ddlStatus.SelectedValue ?? ""));
            if (dt.Rows.Count == 0) { litList.Text = "<div class=\"empty-state\"><i class=\"bi bi-egg\"></i>No flocks yet. Use <b>New inward</b> when chicks arrive.</div>"; return; }
            var sb = new StringBuilder("<table class=\"table table-erp table-hover\"><thead><tr><th>Flock</th><th>Breed / source</th><th>Hatch</th><th>Arrival</th><th>Age</th><th>Stage</th>" +
                                       "<th class=\"num\">Received</th><th class=\"num\">Placed</th><th class=\"num\">Birds now</th><th class=\"num\">Livability</th><th>Sheds</th><th>Status</th><th class=\"text-end\">Actions</th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                var hatch = Convert.ToDateTime(r["HatchDate"]);
                int id = Db.Int(r["FlockId"]), birds = Db.Int(r["Birds"]), placed = Db.Int(r["Placed"]);
                int start = Db.Int(r["InitialCount"]) - Db.Int(r["ArrivalMortality"]);
                var status = Db.Str(r["Status"]);
                var endDate = status == "Depleted" && r["DepletionDate"] != DBNull.Value ? Convert.ToDateTime(r["DepletionDate"]) : DateTime.Today;
                sb.Append("<tr><td><b>").Append(Ui.H(r["FlockCode"])).Append("</b> ").Append(Db.Str(r["FlockType"]) == "Broiler" ? "<span class=\"badge text-bg-warning\">Broiler</span>" : "<span class=\"badge text-bg-light border\">Layer</span>").Append("<div class=\"small text-muted-2\">").Append(Ui.H(r["BatchNo"])).Append(r["LotNo"] == DBNull.Value ? "" : " / " + Ui.H(r["LotNo"])).Append("</div></td>")
                  .Append("<td>").Append(Ui.H(r["BreedName"])).Append("<div class=\"small text-muted-2\">").Append(Ui.H(r["Hatchery"])).Append("</div></td>")
                  .Append("<td>").Append(Ui.D(hatch)).Append("</td><td>").Append(Ui.D(r["ArrivalDate"])).Append("</td><td>").Append(Db.Str(r["FlockType"]) == "Broiler" ? "<b>" + Age.Days(hatch, endDate) + "</b> d" + (status == "Active" && Age.Days(hatch, endDate) > Company.BroilerMaxDays ? " <span class=\"badge text-bg-danger\">over " + Company.BroilerMaxDays + " d</span>" : "") : Age.Text(hatch, endDate))
                  .Append("</td><td>").Append(status == "Active" ? Company.StageOf(Age.Days(hatch, DateTime.Today), Db.Str(r["FlockType"]) == "Broiler") : "-").Append("</td>")
                  .Append("<td class=\"num\">").Append(Ui.N0(r["InitialCount"])).Append("</td><td class=\"num\">").Append(Ui.N0(placed))
                  .Append(placed < start ? " <i class=\"bi bi-exclamation-circle text-warning\" title=\"" + Ui.N0(start - placed) + " live chicks not yet placed\"></i>" : "")
                  .Append("</td><td class=\"num\"><b>").Append(Ui.N0(birds)).Append("</b></td><td class=\"num\">").Append(start > 0 && placed > 0 ? Ui.N2(birds * 100m / start) + "%" : "-")
                  .Append("</td><td>").Append(Ui.H(r["Sheds"])).Append("</td><td>").Append(Ui.StatusBadge(status)).Append("</td><td class=\"text-end text-nowrap\">");
                if (CanEdit)
                {
                    sb.Append("<a href=\"#\" class=\"btn btn-outline-primary\" title=\"Edit\" onclick=\"return flkCmd('edit:").Append(id).Append("')\"><i class=\"bi bi-pencil\"></i></a> ")
                      .Append("<a href=\"#\" class=\"btn btn-outline-primary\" title=\"Placements\" onclick=\"return flkCmd('place:").Append(id).Append("')\"><i class=\"bi bi-house-add\"></i></a> ");
                    if (status == "Active" && birds == 0 && placed > 0)
                        sb.Append("<a href=\"#\" class=\"btn btn-outline-secondary\" title=\"Close flock\" onclick=\"return flkCmd('close:").Append(id).Append("','Mark this flock as depleted?')\"><i class=\"bi bi-archive\"></i></a>");
                    if (status == "Depleted")
                        sb.Append("<a href=\"#\" class=\"btn btn-outline-secondary\" title=\"Reopen\" onclick=\"return flkCmd('reopen:").Append(id).Append("','Reopen this flock?')\"><i class=\"bi bi-arrow-counterclockwise\"></i></a>");
                }
                sb.Append(" <a class=\"btn btn-outline-dark\" title=\"Hen house report\" href=\"").Append(ResolveUrl("~/Reports/Report.aspx?r=henhouse")).Append("\"><i class=\"bi bi-graph-up\"></i></a>");
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            litList.Text = sb.ToString();
        }

        protected void btnNew_Click(object sender, EventArgs e)
        {
            if (!CanEdit) return;
            hidFlock.Value = "";
            foreach (var tb in new[] { txtCode, txtBatch, txtLot, txtInitial, txtRate, txtHoused, txtRemarks, txtPlaceCount }) tb.Text = "";
            txtDoa.Text = "0"; txtFreight.Text = "0"; txtDeplWk.Text = "80"; txtSalvage.Text = "0";
            txtHatch.Text = Ui.DateValue(DateTime.Today.AddDays(-1)); txtArrival.Text = Ui.DateValue(DateTime.Today);
            ddlType.Enabled = true; ddlType.SelectedValue = "Layer"; txtLift.Text = "42";
            txtCode.Text = NextCode();
            ddlSupplier.SelectedIndex = ddlSupplier.Items.Count > 1 ? 1 : 0;
            phFirstPlace.Visible = true;
            Type_Changed(null, null);
            txtCode.Enabled = true;
            litFormTitle.Text = "<i class=\"bi bi-plus-circle me-1\"></i>New flock inward";
            pnlErr.Visible = false; pnlForm.Visible = true; pnlPlace.Visible = false;
        }

        string NextCode()
        {
            int n = Db.Int(Db.Scalar("SELECT COUNT(*) FROM dbo.Flock WHERE CompanyId = @c", Db.P("@c", Ctx.CompanyId))) + 1;
            return (BroilerSelected ? "BR-" : "FLK-") + DateTime.Today.ToString("yyMM") + "-" + n.ToString("000");
        }

        protected void btnCancel_Click(object sender, EventArgs e) { pnlForm.Visible = false; pnlPlace.Visible = false; hidFlock.Value = ""; }

        protected void btnCmd_Click(object sender, EventArgs e)
        {
            var cmd = (hidCmd.Value ?? "").Split(':'); hidCmd.Value = "";
            int id;
            if (cmd.Length != 2 || !int.TryParse(cmd[1], out id) || !CanEdit) return;
            switch (cmd[0])
            {
                case "edit": LoadFlock(id); break;
                case "place": ShowPlacements(id); break;
                case "close": CloseFlock(id, true); break;
                case "reopen": CloseFlock(id, false); break;
                case "delplace": DeletePlacement(id); break;
            }
        }

        void LoadFlock(int id)
        {
            var r = Ops.Flock(id);
            if (r == null) return;
            hidFlock.Value = id.ToString(CultureInfo.InvariantCulture);
            txtCode.Text = Db.Str(r["FlockCode"]); txtBatch.Text = Db.Str(r["BatchNo"]); txtLot.Text = Db.Str(r["LotNo"]);
            ddlType.SelectedValue = Ops.IsBroiler(r) ? "Broiler" : "Layer"; ddlType.Enabled = false;
            phFirstPlace.Visible = false; Type_Changed(null, null);
            txtLift.Text = r.Table.Columns.Contains("TargetLiftDays") && r["TargetLiftDays"] != DBNull.Value ? Db.Str(r["TargetLiftDays"]) : "42";
            Ui.Select(ddlSupplier, r["SupplierId"]); Ui.Select(ddlBreed, r["BreedId"]);
            txtHatch.Text = Ui.DateValue(r["HatchDate"]); txtArrival.Text = Ui.DateValue(r["ArrivalDate"]);
            txtInitial.Text = Db.Str(r["InitialCount"]); txtDoa.Text = Db.Str(r["ArrivalMortality"]);
            txtRate.Text = Ui.NumValue(r["ChickRate"]); txtFreight.Text = Ui.NumValue(r["FreightCost"]);
            txtDeplWk.Text = Db.Str(r["PlannedDepletionWeek"]); txtSalvage.Text = Ui.NumValue(r["ExpectedSalvagePerBird"]);
            txtHoused.Text = Db.Str(r["HenHousedCount"]); txtRemarks.Text = Db.Str(r["Remarks"]);
            phFirstPlace.Visible = false;
            litFormTitle.Text = "<i class=\"bi bi-pencil-square me-1\"></i>Edit flock " + Ui.H(r["FlockCode"]);
            pnlErr.Visible = false; pnlForm.Visible = true; pnlPlace.Visible = false;
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (!CanEdit) return;
            var err = new List<string>();
            var code = (txtCode.Text ?? "").Trim();
            if (code.Length == 0) err.Add("Flock code is required.");
            var breed = Ui.SelInt(ddlBreed); if (!breed.HasValue) err.Add("Select the breed.");
            var hatch = Ui.Date(txtHatch.Text); var arrival = Ui.Date(txtArrival.Text);
            if (!hatch.HasValue) err.Add("Hatch date is required."); if (!arrival.HasValue) err.Add("Arrival date is required.");
            if (hatch.HasValue && arrival.HasValue && arrival < hatch) err.Add("Arrival cannot be before the hatch date.");
            if (arrival.HasValue && arrival > DateTime.Today) err.Add("Arrival date cannot be in the future.");
            var initial = Ui.Int(txtInitial.Text); if (!initial.HasValue || initial <= 0) err.Add("Chicks received must be a positive whole number.");
            var doa = Ui.Int(txtDoa.Text) ?? 0; if (doa < 0 || (initial.HasValue && doa > initial)) err.Add("Dead on arrival is invalid.");
            var rate = Ui.Dec(txtRate.Text) ?? 0; var freight = Ui.Dec(txtFreight.Text) ?? 0; var salvage = Ui.Dec(txtSalvage.Text) ?? 0;
            bool broiler = BroilerSelected;
            var deplWk = broiler ? 30 : (Ui.Int(txtDeplWk.Text) ?? 80); if (deplWk < 30 || deplWk > 120) err.Add("Planned depletion week must be between 30 and 120.");
            int? housed = broiler ? null : Ui.Int(txtHoused.Text); if (!broiler && (txtHoused.Text ?? "").Trim().Length > 0 && (!housed.HasValue || housed <= 0)) err.Add("Hens housed must be a positive number.");
            int? lift = broiler ? Ui.Int(txtLift.Text) : null;
            if (broiler && (!lift.HasValue || lift < 20 || lift > Company.BroilerMaxDays)) err.Add("Target lifting age must be between 20 and " + Company.BroilerMaxDays + " days.");
            if (broiler) salvage = 0;
            if (rate < 0 || freight < 0 || salvage < 0) err.Add("Rates and charges cannot be negative.");

            int? placeShed = null; int placeCount = 0;
            if (!FlockId.HasValue)
            {
                placeShed = Ui.SelInt(ddlPlaceShed);
                if (!placeShed.HasValue) err.Add("Select the shed for initial placement.");
                placeCount = Ui.Int(txtPlaceCount.Text) ?? ((initial ?? 0) - doa);
                if (placeCount <= 0 || placeCount > (initial ?? 0) - doa) err.Add("Birds placed must be between 1 and the live chicks received (" + ((initial ?? 0) - doa) + ").");
            }
            else
            {
                int placed = Db.Int(Db.Scalar("SELECT ISNULL(SUM(BirdCount),0) FROM dbo.FlockPlacement WHERE FlockId = @f", Db.P("@f", FlockId.Value)));
                if (initial.HasValue && placed > initial.Value - doa) err.Add("Already " + placed + " birds are placed; live chicks cannot be less than that.");
                var first = Db.NDate(Db.Scalar("SELECT MIN(PlaceDate) FROM dbo.FlockPlacement WHERE FlockId = @f", Db.P("@f", FlockId.Value)));
                if (first.HasValue && arrival.HasValue && arrival > first) err.Add("Arrival date cannot be after the first placement (" + Ui.D(first) + ").");
            }
            if (err.Count > 0) { ShowErr(err); return; }

            try
            {
                Db.Tx((c, t) =>
                {
                    var ps = new[] { Db.P("@c", Ctx.CompanyId), Db.P("@code", code), Db.P("@batch", Nz(txtBatch.Text)), Db.P("@lot", Nz(txtLot.Text)), Db.P("@sup", Ui.SelInt(ddlSupplier)),
                                     Db.P("@breed", breed), Db.P("@hatch", hatch), Db.P("@arr", arrival), Db.P("@init", initial), Db.P("@doa", doa), Db.P("@rate", rate),
                                     Db.P("@fr", freight), Db.P("@housed", housed), Db.P("@salv", salvage), Db.P("@dw", deplWk), Db.P("@rem", Nz(txtRemarks.Text)), Db.P("@u", Ctx.UserId),
                                     Db.P("@type", broiler ? "Broiler" : "Layer"), Db.P("@lift", lift) };
                    if (FlockId.HasValue)
                    {
                        var list = new List<System.Data.SqlClient.SqlParameter>(ps); list.Add(Db.P("@id", FlockId.Value));
                        Db.Exec(c, t, @"UPDATE dbo.Flock SET FlockCode = @code, BatchNo = @batch, LotNo = @lot, SupplierId = @sup, BreedId = @breed, HatchDate = @hatch, ArrivalDate = @arr,
                                        InitialCount = @init, ArrivalMortality = @doa, ChickRate = @rate, FreightCost = @fr, HenHousedCount = @housed, ExpectedSalvagePerBird = @salv,
                                        PlannedDepletionWeek = @dw, Remarks = @rem, TargetLiftDays = @lift WHERE FlockId = @id AND CompanyId = @c", list.ToArray());
                        Audit.Log(c, t, "UPDATE", "Flock", FlockId.Value, "code=" + code + "; initial=" + initial + "; doa=" + doa + "; rate=" + rate);
                        return FlockId.Value;
                    }
                    int id = Db.Int(Db.Scalar(c, t, @"INSERT INTO dbo.Flock (CompanyId, FlockCode, BatchNo, LotNo, SupplierId, BreedId, HatchDate, ArrivalDate, InitialCount, ArrivalMortality,
                                        ChickRate, FreightCost, HenHousedCount, ExpectedSalvagePerBird, PlannedDepletionWeek, Remarks, CreatedBy, FlockType, TargetLiftDays)
                                        VALUES (@c, @code, @batch, @lot, @sup, @breed, @hatch, @arr, @init, @doa, @rate, @fr, @housed, @salv, @dw, @rem, @u, @type, @lift);
                                        SELECT CAST(SCOPE_IDENTITY() AS INT);", ps));
                    var msg = Ops.CheckDestination(c, t, placeShed.Value, id, arrival.Value);
                    if (msg != null) throw new InvalidOperationException(msg);
                    Db.Exec(c, t, "INSERT INTO dbo.FlockPlacement (CompanyId, FlockId, ShedId, PlaceDate, BirdCount, Remarks, CreatedBy) VALUES (@c, @f, @s, @d, @n, N'Initial placement', @u)",
                        Db.P("@c", Ctx.CompanyId), Db.P("@f", id), Db.P("@s", placeShed.Value), Db.P("@d", arrival.Value), Db.P("@n", placeCount), Db.P("@u", Ctx.UserId));
                    Ops.MarkShedOccupied(c, t, placeShed.Value);
                    Audit.Log(c, t, "INSERT", "Flock", id, "code=" + code + "; initial=" + initial + "; placed " + placeCount + " in shed " + placeShed);
                    return id;
                });
                Ok(FlockId.HasValue ? "Flock updated." : "Flock " + code + " created and placed.");
                pnlForm.Visible = false; hidFlock.Value = "";
                BindList();
            }
            catch (Exception ex) { ShowErr(new List<string> { Db.FriendlyError(ex) }); }
        }

        void ShowPlacements(int id)
        {
            var f = Ops.Flock(id);
            if (f == null) return;
            hidFlock.Value = id.ToString(CultureInfo.InvariantCulture);
            pnlForm.Visible = false; pnlPlace.Visible = true;
            int live = Db.Int(f["InitialCount"]) - Db.Int(f["ArrivalMortality"]);
            var dt = Db.Query(@"SELECT p.PlacementId, p.PlaceDate, s.Code, s.Name, p.BirdCount, p.Remarks,
                                (SELECT COUNT(*) FROM dbo.DailyLog d WHERE d.FlockId = p.FlockId AND d.ShedId = p.ShedId) AS Logs
                                FROM dbo.FlockPlacement p JOIN dbo.Shed s ON s.ShedId = p.ShedId WHERE p.FlockId = @f ORDER BY p.PlaceDate", Db.P("@f", id));
            int placed = 0;
            var sb = new StringBuilder("<table class=\"table table-erp\"><thead><tr><th>Date</th><th>Shed</th><th class=\"num\">Birds</th><th>Remarks</th><th></th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                placed += Db.Int(r["BirdCount"]);
                sb.Append("<tr><td>").Append(Ui.D(r["PlaceDate"])).Append("</td><td><b>").Append(Ui.H(r["Code"])).Append("</b> ").Append(Ui.H(r["Name"]))
                  .Append("</td><td class=\"num\">").Append(Ui.N0(r["BirdCount"])).Append("</td><td>").Append(Ui.H(r["Remarks"])).Append("</td><td class=\"text-end\">")
                  .Append(Db.Int(r["Logs"]) == 0 ? "<a href=\"#\" class=\"btn btn-outline-danger\" onclick=\"return flkCmd('delplace:" + r["PlacementId"] + "','Remove this placement?')\"><i class=\"bi bi-trash\"></i></a>" : "<span class=\"small text-muted-2\">has daily logs</span>")
                  .Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            litPlacements.Text = sb.ToString();
            litPlaceTitle.Text = "Placements &mdash; <b>" + Ui.H(f["FlockCode"]) + "</b> <span class=\"sub\">" + Ui.N0(placed) + " of " + Ui.N0(live) + " live chicks placed</span>";
            pnlAddPlace.Visible = placed < live;
            Ui.Bind(ddlAddShed, FreeShedsSql, "-- select shed --", Db.P("@c", Ctx.CompanyId), Db.P("@f", id), Db.P("@br", Ops.IsBroiler(f) ? 1 : 0));
            txtAddDate.Text = Ui.DateValue(f["ArrivalDate"]);
            txtAddCount.Text = (live - placed).ToString(CultureInfo.InvariantCulture);
        }

        protected void btnAddPlace_Click(object sender, EventArgs e)
        {
            if (!CanEdit || !FlockId.HasValue) return;
            int id = FlockId.Value;
            var f = Ops.Flock(id);
            var shed = Ui.SelInt(ddlAddShed); var d = Ui.Date(txtAddDate.Text); var n = Ui.Int(txtAddCount.Text);
            int live = Db.Int(f["InitialCount"]) - Db.Int(f["ArrivalMortality"]);
            int placed = Db.Int(Db.Scalar("SELECT ISNULL(SUM(BirdCount),0) FROM dbo.FlockPlacement WHERE FlockId = @f", Db.P("@f", id)));
            if (!shed.HasValue || !d.HasValue || !n.HasValue || n <= 0) { Fail("Select a shed, date and number of birds."); return; }
            if (d < Convert.ToDateTime(f["ArrivalDate"]) || d > DateTime.Today) { Fail("Placement date must be between arrival and today."); return; }
            if (placed + n > live) { Fail("Only " + (live - placed) + " live chicks are left to place."); return; }
            try
            {
                Db.Tx((c, t) =>
                {
                    var msg = Ops.CheckDestination(c, t, shed.Value, id, d.Value);
                    if (msg != null) throw new InvalidOperationException(msg);
                    Db.Exec(c, t, "INSERT INTO dbo.FlockPlacement (CompanyId, FlockId, ShedId, PlaceDate, BirdCount, CreatedBy) VALUES (@c, @f, @s, @d, @n, @u)",
                        Db.P("@c", Ctx.CompanyId), Db.P("@f", id), Db.P("@s", shed.Value), Db.P("@d", d.Value), Db.P("@n", n.Value), Db.P("@u", Ctx.UserId));
                    Ops.MarkShedOccupied(c, t, shed.Value);
                    Audit.Log(c, t, "INSERT", "FlockPlacement", id, "shed " + shed + " birds " + n + " on " + Ui.D(d));
                    return 0;
                });
                Ok("Placement added.");
            }
            catch (Exception ex) { Fail(Db.FriendlyError(ex)); }
            ShowPlacements(id); BindList();
        }

        void DeletePlacement(int placementId)
        {
            var r = Db.Row("SELECT FlockId, ShedId FROM dbo.FlockPlacement WHERE PlacementId = @p AND CompanyId = @c", Db.P("@p", placementId), Db.P("@c", Ctx.CompanyId));
            if (r == null) return;
            try
            {
                Db.Tx((c, t) =>
                {
                    Db.Exec(c, t, "DELETE FROM dbo.FlockPlacement WHERE PlacementId = @p", Db.P("@p", placementId));
                    Ops.EnsureNoNegative(c, t, Db.Int(r["ShedId"]), Db.Int(r["FlockId"]));
                    Ops.MarkShedVacantIfEmpty(c, t, Db.Int(r["ShedId"]));
                    Audit.Log(c, t, "DELETE", "FlockPlacement", placementId, null);
                    return 0;
                });
                Ok("Placement removed.");
            }
            catch (Exception ex) { Fail(Db.FriendlyError(ex)); }
            ShowPlacements(Db.Int(r["FlockId"])); BindList();
        }

        void CloseFlock(int id, bool close)
        {
            if (close)
            {
                int birds = Db.Int(Db.Scalar("SELECT ISNULL(SUM(Qty),0) FROM dbo.vBirdMovement WHERE FlockId = @f", Db.P("@f", id)));
                if (birds != 0) { Fail("The flock still has " + birds + " birds. Record them as sold / culled in the daily entry first."); return; }
                var last = Db.NDate(Db.Scalar("SELECT MAX(MoveDate) FROM dbo.vBirdMovement WHERE FlockId = @f", Db.P("@f", id)));
                Db.Exec("UPDATE dbo.Flock SET Status = N'Depleted', DepletionDate = @d WHERE FlockId = @f AND CompanyId = @c",
                    Db.P("@d", last ?? DateTime.Today), Db.P("@f", id), Db.P("@c", Ctx.CompanyId));
                Audit.Log("CLOSE", "Flock", id, null);
                Ok("Flock closed as depleted.");
            }
            else
            {
                Db.Exec("UPDATE dbo.Flock SET Status = N'Active', DepletionDate = NULL WHERE FlockId = @f AND CompanyId = @c", Db.P("@f", id), Db.P("@c", Ctx.CompanyId));
                Audit.Log("REOPEN", "Flock", id, null);
                Ok("Flock reopened.");
            }
            BindList();
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
