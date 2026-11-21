using System;
using System.Collections.Specialized;
using System.Web.UI.WebControls;
using PoultryERP.Reports;

namespace PoultryERP
{
    public partial class ReportPage : BasePage
    {
        ReportDef def;
        ReportDef Def
        {
            get
            {
                if (def == null) def = ReportCatalog.Get(Request.QueryString["r"] ?? "");
                return def;
            }
        }

        protected override string[] AllowedRoles { get { return Def == null ? null : Def.Roles; } }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Def == null) { Response.Redirect("~/Reports/Report.aspx?r=dtr", true); return; }
            Title = Def.Title;
            chkWt.InputAttributes["class"] = "form-check-input";
            chkWt.LabelAttributes["class"] = "form-check-label small ms-1";
            if (IsPostBack) return;

            litTitle.Text = Ui.H(Def.Title);
            litDesc.Text = Ui.H(Def.Description);
            litOld.Text = Ui.H(Def.OldName == "(new)" ? "New report" : "Old MIS: " + Def.OldName);
            phDate.Visible = Def.Has("date");
            phRange.Visible = Def.Has("range");
            phFarm.Visible = Def.Has("farm") || Def.Has("shed");
            phShed.Visible = Def.Has("shed");
            phFlock.Visible = Def.Has("flock");
            phGroup.Visible = Def.Has("group");
            phProduct.Visible = Def.Has("product");
            phWt.Visible = Def.Has("wt");
            litShedReq.Text = Def.ShedRequired ? " <span class=\"text-danger\">*</span>" : "";
            litFlockReq.Text = Def.FlockRequired ? " <span class=\"text-danger\">*</span>" : "";

            var last = Db.NDate(Db.Scalar("SELECT MAX(LogDate) FROM dbo.DailyLog WHERE CompanyId = @c AND LogDate <= @t",
                Db.P("@c", Ctx.CompanyId), Db.P("@t", DateTime.Today)));
            var d = last ?? DateTime.Today;
            txtDate.Text = Ui.DateValue(d);
            int days = Def.Key == "costegg" || Def.Key == "recon" ? 90 : Def.Key == "transfers" || Def.Key == "flockdaily" ? 180 : 30;
            txtFrom.Text = Ui.DateValue(d.AddDays(-days + 1));
            txtTo.Text = Ui.DateValue(d);
            if (Def.Key == "costegg") ddlGroup.SelectedValue = "Weekly";

            Ui.Bind(ddlFarm, "SELECT FarmId AS Value, Name AS Text FROM dbo.Farm WHERE CompanyId = @c AND IsActive = 1 ORDER BY Name", "All farms", Db.P("@c", Ctx.CompanyId));
            BindSheds();
            BindFlocks();
            if (Def.ProductOptions != null) Ui.BindList(ddlProduct, Def.ProductOptions, "All");

            // sensible default selections so most reports open immediately
            if (Def.ShedRequired && ddlShed.Items.Count > 1)
            {
                var sid = Db.Scalar("SELECT TOP (1) v.ShedId FROM dbo.vShedInfo v WHERE v.CompanyId = @CompanyId AND v.Birds > 0 AND v.Purpose IN (N'Brooding', N'Grower')" + Ctx.ShedFilter("v.ShedId") + " ORDER BY v.Code", Ctx.Scope());
                if (sid != null) Ui.Select(ddlShed, sid); else ddlShed.SelectedIndex = 1;
            }
            if (Def.FlockRequired && ddlFlock.Items.Count > 1)
            {
                string pref = Def.FlockType == "Broiler"
                    ? "SELECT TOP (1) FlockId FROM dbo.Flock WHERE CompanyId = @c AND FlockType = N'Broiler' ORDER BY CASE WHEN Status = N'Depleted' THEN 0 ELSE 1 END, ISNULL(DepletionDate, HatchDate) DESC"
                    : Def.Key == "henhouse" || Def.Key == "flockcost"
                    ? "SELECT TOP (1) FlockId FROM dbo.Flock WHERE CompanyId = @c AND Status = N'Active' AND FlockType = N'Layer' AND DATEDIFF(DAY, HatchDate, GETDATE()) >= @lay ORDER BY HatchDate"
                    : "SELECT TOP (1) FlockId FROM dbo.Flock WHERE CompanyId = @c AND Status = N'Active' ORDER BY HatchDate DESC";
                var fid = Db.Scalar(pref, Db.P("@c", Ctx.CompanyId), Db.P("@lay", Company.LayStartAgeDays));
                if (fid != null) Ui.Select(ddlFlock, fid); else ddlFlock.SelectedIndex = 1;
            }
            Run(false);
        }

        void BindSheds()
        {
            Ui.Bind(ddlShed, "SELECT s.ShedId AS Value, s.Code + ' - ' + s.Name AS Text FROM dbo.Shed s JOIN dbo.Location l ON l.LocationId = s.LocationId " +
                             "WHERE s.CompanyId = @CompanyId AND (@farm IS NULL OR l.FarmId = @farm)" + Ctx.ShedFilter("s.ShedId") + " ORDER BY s.Code",
                    Def.ShedRequired ? "-- select shed --" : "All sheds", Ctx.Scope(Db.P("@farm", Ui.SelInt(ddlFarm))));
        }

        void BindFlocks()
        {
            Ui.Bind(ddlFlock, "SELECT f.FlockId AS Value, f.FlockCode + ' (hatch ' + CONVERT(VARCHAR(10), f.HatchDate, 105) + CASE WHEN f.Status = N'Depleted' THEN ', depleted' ELSE '' END + ')' AS Text " +
                              "FROM dbo.Flock f WHERE f.CompanyId = @CompanyId AND (@ft IS NULL OR f.FlockType = @ft) AND (@Restricted = 0 OR f.FlockId IN (SELECT m.FlockId FROM dbo.vBirdMovement m JOIN dbo.UserShed us ON us.ShedId = m.ShedId WHERE us.UserId = @UserId)) " +
                              "ORDER BY CASE WHEN f.Status = N'Active' THEN 0 ELSE 1 END, f.HatchDate DESC",
                    Def.FlockRequired ? "-- select " + (Def.FlockType == "Broiler" ? "batch" : "flock") + " --" : "All flocks", Ctx.Scope(Db.P("@ft", Def.FlockType)));
        }

        protected void ddlFarm_Changed(object sender, EventArgs e) { BindSheds(); }

        protected void btnShow_Click(object sender, EventArgs e) { Run(true); }

        ReportParams Params()
        {
            var q = new NameValueCollection();
            q["date"] = txtDate.Text; q["from"] = txtFrom.Text; q["to"] = txtTo.Text;
            if (phFarm.Visible) q["farm"] = ddlFarm.SelectedValue;
            if (phShed.Visible) q["shed"] = ddlShed.SelectedValue;
            if (phFlock.Visible) q["flock"] = ddlFlock.SelectedValue;
            if (phGroup.Visible) q["group"] = ddlGroup.SelectedValue;
            if (phProduct.Visible) q["product"] = ddlProduct.SelectedValue;
            q["wt"] = !phWt.Visible || chkWt.Checked ? "1" : "0";
            return ReportParams.Parse(q);
        }

        void Run(bool userAction)
        {
            var p = Params();
            if (Def.ShedRequired && !p.ShedId.HasValue) { if (userAction) Warn("Select a shed."); return; }
            if (Def.FlockRequired && !p.FlockId.HasValue) { if (userAction) Warn("Select a flock."); return; }
            try
            {
                var m = Def.Build(p);
                litReport.Text = HtmlRenderer.Render(m);
                var baseUrl = ResolveUrl("~/Handlers/Export.ashx") + "?r=" + Def.Key + "&" + p.ToQuery() + "&fmt=";
                lnkPdf.NavigateUrl = baseUrl + "pdf";
                lnkXlsx.NavigateUrl = baseUrl + "xlsx";
                lnkCsv.NavigateUrl = baseUrl + "csv";
                pnlToolbar.Visible = true;
            }
            catch (Exception ex)
            {
                litReport.Text = "";
                pnlToolbar.Visible = false;
                Fail("Could not build the report: " + Db.FriendlyError(ex));
            }
        }
    }
}
