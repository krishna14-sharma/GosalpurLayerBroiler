using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web.UI.WebControls;

namespace PoultryERP
{
    public partial class DailyEntryPage : BasePage
    {
        protected override string[] AllowedRoles { get { return new[] { Roles.SuperAdmin, Roles.FarmManager, Roles.Supervisor }; } }

        DateTime EntryDate { get { return Ui.Date(txtDate.Text) ?? DateTime.Today; } }
        int? ShedId { get { int v; return int.TryParse(hidShed.Value, out v) ? v : (int?)null; } }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            var qd = Ui.Date(Request.QueryString["date"]);
            txtDate.Text = Ui.DateValue(qd.HasValue && qd.Value <= DateTime.Today ? qd.Value : DateTime.Today);
            txtDate.Attributes["max"] = Ui.DateValue(DateTime.Today);
            Ui.Bind(ddlFarm, "SELECT FarmId AS Value, Name AS Text FROM dbo.Farm WHERE CompanyId = @c AND IsActive = 1 ORDER BY Name", "All farms", Db.P("@c", Ctx.CompanyId));
            phFarm.Visible = ddlFarm.Items.Count > 2;
            Ui.Bind(ddlFeed, "SELECT ItemId AS Value, Name + ISNULL(' (' + FeedStage + ')', '') AS Text FROM dbo.Item WHERE CompanyId = @c AND Category = 'FEED' AND IsActive = 1 ORDER BY FeedStage, Name", "-- feed --", Db.P("@c", Ctx.CompanyId));
            Ui.Bind(ddlGrit, "SELECT ItemId AS Value, Name AS Text FROM dbo.Item WHERE CompanyId = @c AND Category = 'GRIT' AND IsActive = 1 ORDER BY Name", "(none)", Db.P("@c", Ctx.CompanyId));
            int qs;
            var shedList = BindSheds();
            if (int.TryParse(Request.QueryString["shed"], out qs)) LoadShed(qs);
            else if (shedList.Count > 0) LoadShed(shedList[0]);
        }

        protected void txtDate_TextChanged(object sender, EventArgs e)
        {
            if (EntryDate > DateTime.Today) txtDate.Text = Ui.DateValue(DateTime.Today);
            var list = BindSheds();
            if (ShedId.HasValue && list.Contains(ShedId.Value)) LoadShed(ShedId.Value);
            else if (list.Count > 0) LoadShed(list[0]);
            else ClearForm("No occupied sheds on this date.");
        }

        protected void ddlFarm_Changed(object sender, EventArgs e)
        {
            var list = BindSheds();
            if (list.Count > 0) LoadShed(list[0]); else ClearForm("No occupied sheds for this farm.");
        }

        protected void btnCmd_Click(object sender, EventArgs e)
        {
            var cmd = hidCmd.Value ?? ""; hidCmd.Value = "";
            int id;
            if (cmd.StartsWith("shed:") && int.TryParse(cmd.Substring(5), out id)) { LoadShed(id); BindSheds(); }
        }

        // ------------------------------------------------------------------ shed list

        /// <summary>Renders the shed pills for the date and returns occupied shed ids (pending first).</summary>
        List<int> BindSheds()
        {
            var d = EntryDate;
            var dt = Db.Query(@"
SELECT s.ShedId, s.Code, s.Name, s.Purpose, f.Name AS FarmName, x.FlockId, fl.FlockCode, fl.HatchDate, fl.FlockType, ISNULL(x.Avail, 0) AS Avail, dl.DailyLogId
FROM dbo.Shed s JOIN dbo.Location l ON l.LocationId = s.LocationId JOIN dbo.Farm f ON f.FarmId = l.FarmId
OUTER APPLY (SELECT TOP (1) m.FlockId, SUM(CASE WHEN m.MoveDate < @d OR m.Kind IN (N'PLACE', N'TIN', N'TOUT') THEN m.Qty ELSE 0 END) AS Avail
             FROM dbo.vBirdMovement m WHERE m.ShedId = s.ShedId AND m.MoveDate <= @d GROUP BY m.FlockId ORDER BY 2 DESC) x
LEFT JOIN dbo.Flock fl ON fl.FlockId = x.FlockId
LEFT JOIN dbo.DailyLog dl ON dl.ShedId = s.ShedId AND dl.LogDate = @d
WHERE s.CompanyId = @CompanyId AND s.IsActive = 1 AND (@farm IS NULL OR l.FarmId = @farm)" + Ctx.ShedFilter("s.ShedId") + @"
ORDER BY CASE WHEN dl.DailyLogId IS NULL THEN 0 ELSE 1 END, s.Code", Ctx.Scope(Db.P("@d", d), Db.P("@farm", Ui.SelInt(ddlFarm))));

            var sb = new StringBuilder();
            var ids = new List<int>();
            int done = 0, total = 0;
            foreach (DataRow r in dt.Rows)
            {
                int avail = Db.Int(r["Avail"]);
                bool has = r["DailyLogId"] != DBNull.Value;
                if (avail <= 0 && !has) continue;
                int id = Db.Int(r["ShedId"]);
                ids.Add(id); total++; if (has) done++;
                var hatch = Db.NDate(r["HatchDate"]);
                bool active = ShedId.HasValue && ShedId.Value == id;
                sb.Append("<a href=\"#\" class=\"shed-pill").Append(active ? " active" : "").Append("\" onclick=\"return deCmd('shed:").Append(id).Append("')\">")
                  .Append("<span><span class=\"s-name\">").Append(Ui.H(r["Code"])).Append("</span><span class=\"s-meta d-block\">")
                  .Append(Ui.H(Db.Str(r["FlockCode"]))).Append(hatch.HasValue ? " &middot; " + (r.Table.Columns.Contains("FlockType") && Db.Str(r["FlockType"]) == "Broiler" ? "day " + Age.Days(hatch.Value, d) : Age.Text(hatch.Value, d)) : "").Append(" &middot; ").Append(Ui.N0(avail)).Append("</span></span>")
                  .Append(has ? "<span class=\"badge text-bg-success\"><i class=\"bi bi-check2\"></i></span>" : "<span class=\"badge text-bg-warning\">due</span>")
                  .Append("</a>");
            }
            litSheds.Text = sb.Length > 0 ? sb.ToString() : "<div class=\"text-muted-2 small p-2\">No occupied sheds" + (Ctx.ShedRestricted ? " assigned to you" : "") + " on this date.</div>";
            litProgress.Text = total > 0 ? "<b>" + done + " of " + total + "</b> sheds entered for " + Ui.D(d) : "";
            return ids;
        }

        // ------------------------------------------------------------------ form

        void ClearForm(string msg)
        {
            hidShed.Value = hidFlock.Value = hidLog.Value = "";
            pnlForm.Visible = false; pnlHistory.Visible = false; pnlEmpty.Visible = true;
            litEmpty.Text = Ui.H(msg);
        }

        void LoadShed(int shedId)
        {
            var d = EntryDate;
            var shed = Ops.Shed(shedId);
            if (shed == null || !Ctx.CanAccessShed(shedId)) { ClearForm("Shed not available."); return; }
            var log = Db.Row("SELECT * FROM dbo.DailyLog WHERE ShedId = @s AND LogDate = @d", Db.P("@s", shedId), Db.P("@d", d));
            ShedPosition pos = null;
            var positions = Ops.Positions(null, null, shedId, d);
            if (log != null) foreach (var p in positions) if (p.FlockId == Db.Int(log["FlockId"])) pos = p;
            if (pos == null) foreach (var p in positions) if (p.Available > 0 && (pos == null || p.Available > pos.Available)) pos = p;
            if (pos == null) { ClearForm("Shed " + Db.Str(shed["Code"]) + " has no birds on " + Ui.D(d) + "."); return; }

            var flock = Ops.Flock(pos.FlockId);
            int age = Age.Days(Convert.ToDateTime(flock["HatchDate"]), d);
            var std = Ops.Standard(Db.Int(flock["BreedId"]), age);
            bool broiler = Ops.IsBroiler(flock);
            var stage = Company.StageOf(age, broiler);

            hidShed.Value = shedId.ToString(CultureInfo.InvariantCulture);
            hidFlock.Value = pos.FlockId.ToString(CultureInfo.InvariantCulture);
            hidLog.Value = log == null ? "" : Db.Str(log["DailyLogId"]);
            pnlEmpty.Visible = false; pnlForm.Visible = true; pnlErr.Visible = false;

            litShedTitle.Text = "<b>" + Ui.H(shed["Code"]) + "</b> &middot; " + Ui.H(shed["Name"]) + " <span class=\"badge text-bg-light border ms-1\">" + Ui.H(shed["Purpose"]) + "</span>";
            litFlockInfo.Text = "Flock <b>" + Ui.H(flock["FlockCode"]) + "</b> (" + Ui.H(flock["BreedName"]) + ") &middot; hatched " + Ui.D(flock["HatchDate"]) +
                                " &middot; age <b>" + Age.Text(Convert.ToDateTime(flock["HatchDate"]), d) + "</b> (" + age + " d) &middot; <b>" + stage + "</b>";
            litState.Text = log == null ? "<span class=\"badge text-bg-warning\">New entry</span>"
                : "<span class=\"badge text-bg-success\">Saved</span>" + (log["ModifiedOn"] != DBNull.Value ? " <span class=\"sub\">edited " + Convert.ToDateTime(log["ModifiedOn"]).ToString("dd-MM HH:mm") + "</span>" : "");
            litOpening.Text = Ui.N0(pos.Opening);
            litTrf.Text = "+" + Ui.N0(pos.InQty) + " / -" + Ui.N0(pos.OutQty);
            litStdBW.Text = std != null && std.BodyWt.HasValue ? Ui.N0(std.BodyWt) : "-";
            litStdEW.Text = std != null && std.EggWt.HasValue ? Ui.N1(std.EggWt) : "-";

            pnlForm.Attributes["data-avail"] = pos.Available.ToString(CultureInfo.InvariantCulture);
            pnlForm.Attributes["data-tray"] = Company.EggsPerTray.ToString(CultureInfo.InvariantCulture);
            pnlForm.Attributes["data-stdfeed"] = std != null && std.FeedG.HasValue ? std.FeedG.Value.ToString(CultureInfo.InvariantCulture) : "";
            pnlForm.Attributes["data-stdhd"] = std != null && std.HDP.HasValue ? std.HDP.Value.ToString(CultureInfo.InvariantCulture) : "";

            bool showEggs = !broiler && (age >= Company.PreLayEndWk * 7 - 14 || (log != null && Db.Int(log["TotalEggs"]) > 0));
            pnlEggs.Visible = showEggs;
            phGrit.Visible = !broiler; phEggWt.Visible = !broiler; phSold.Visible = !broiler; phLifted.Visible = broiler; pnlBroiler.Visible = broiler;
            pnlForm.Attributes["data-broiler"] = broiler ? "1" : "0";
            if (broiler)
            {
                var b = Db.Row(@"SELECT
                    (SELECT ISNULL(SUM(Qty),0) FROM dbo.vBirdMovement WHERE ShedId = @s AND FlockId = @f AND Kind IN (N'PLACE', N'TIN') AND MoveDate <= @d) AS Placed,
                    (SELECT ISNULL(SUM(FeedKg),0) FROM dbo.DailyLog WHERE ShedId = @s AND FlockId = @f AND LogDate < @d) AS CumFeed,
                    (SELECT ISNULL(SUM(QtyKg),0) FROM dbo.Sale WHERE ShedId = @s AND FlockId = @f AND Product = N'Broiler Bird' AND SaleDate <= @d) AS SoldKg,
                    (SELECT ISNULL(SUM(CAST(Qty AS INT)),0) FROM dbo.Sale WHERE ShedId = @s AND FlockId = @f AND Product = N'Broiler Bird' AND SaleDate = @d) AS LiftedToday",
                    Db.P("@s", shedId), Db.P("@f", pos.FlockId), Db.P("@d", d));
                int lifted = Db.Int(b["LiftedToday"]);
                litLifted.Text = Ui.N0(lifted);
                pnlForm.Attributes["data-avail"] = Math.Max(0, pos.Available - lifted).ToString(CultureInfo.InvariantCulture);
                pnlForm.Attributes["data-placed"] = Db.Int(b["Placed"]).ToString(CultureInfo.InvariantCulture);
                pnlForm.Attributes["data-cumfeed"] = Db.Dec(b["CumFeed"]).ToString(CultureInfo.InvariantCulture);
                pnlForm.Attributes["data-soldkg"] = Db.Dec(b["SoldKg"]).ToString(CultureInfo.InvariantCulture);
                pnlForm.Attributes["data-stdbw"] = std != null && std.BodyWt.HasValue ? std.BodyWt.Value.ToString(CultureInfo.InvariantCulture) : "";
                pnlForm.Attributes["data-stdcum"] = std != null && std.CumFeedG.HasValue ? std.CumFeedG.Value.ToString(CultureInfo.InvariantCulture) : "";
                pnlForm.Attributes["data-stdfcr"] = std != null && std.FCR.HasValue ? std.FCR.Value.ToString(CultureInfo.InvariantCulture) : "";
                litFlockInfo.Text = "Broiler batch <b>" + Ui.H(flock["FlockCode"]) + "</b> (" + Ui.H(flock["BreedName"]) + ") &middot; placed " + Ui.D(flock["ArrivalDate"]) +
                                    " &middot; age <b>day " + age + "</b>" + (age > Company.BroilerMaxDays ? " <span class=\"badge text-bg-danger\">over " + Company.BroilerMaxDays + " days - lift now</span>" : "");
            }

            if (log != null)
            {
                txtMort.Text = Db.Str(log["Mortality"]); txtCull.Text = Db.Str(log["Culls"]); txtSold.Text = Db.Str(log["BirdsSold"]);
                Ui.Select(ddlFeed, log["FeedItemId"]); txtFeed.Text = Ui.NumValue(log["FeedKg"]);
                Ui.Select(ddlGrit, log["GritItemId"]); txtGrit.Text = Ui.NumValue(log["GritKg"]); txtGritTime.Text = Ui.TimeValue(log["GritTime"]);
                txtWater.Text = Ui.NumValue(log["WaterLtr"]);
                txtTable.Text = Db.Str(log["TableEggs"]); txtCrack.Text = Db.Str(log["CrackedEggs"]); txtMis.Text = Db.Str(log["MisshapenEggs"]); txtDY.Text = Db.Str(log["DoubleYolkEggs"]);
                txtBW.Text = Ui.NumValue(log["AvgBodyWtG"]); txtEW.Text = Ui.NumValue(log["AvgEggWtG"]);
                txtTmin.Text = Ui.NumValue(log["TempMinC"]); txtTmax.Text = Ui.NumValue(log["TempMaxC"]); txtHum.Text = Ui.NumValue(log["HumidityPct"]);
                txtRemarks.Text = Db.Str(log["Remarks"]);
            }
            else
            {
                foreach (var tb in new[] { txtMort, txtCull, txtSold, txtFeed, txtGrit, txtWater, txtTable, txtCrack, txtMis, txtDY, txtBW, txtEW, txtTmin, txtTmax, txtHum, txtRemarks, txtGritTime })
                    tb.Text = "";
                // defaults: last feed and grit used in this shed, else feed for the stage
                var last = Db.Row("SELECT TOP (1) FeedItemId, GritItemId, GritTime FROM dbo.DailyLog WHERE ShedId = @s AND FlockId = @f AND LogDate < @d ORDER BY LogDate DESC",
                    Db.P("@s", shedId), Db.P("@f", pos.FlockId), Db.P("@d", d));
                object feedId = last == null ? null : last["FeedItemId"];
                if (feedId == null || feedId == DBNull.Value)
                    feedId = Db.Scalar("SELECT TOP (1) ItemId FROM dbo.Item WHERE CompanyId = @c AND Category = 'FEED' AND FeedStage = @st AND IsActive = 1 ORDER BY ItemId",
                        Db.P("@c", Ctx.CompanyId), Db.P("@st", stage));
                Ui.Select(ddlFeed, feedId);
                Ui.Select(ddlGrit, last == null ? null : last["GritItemId"]);
                if (last != null) txtGritTime.Text = Ui.TimeValue(last["GritTime"]);
                txtMort.Text = "0"; txtCull.Text = "0"; txtSold.Text = "0";
            }

            string reason;
            bool canEdit = Ops.CanEditDay(shedId, d, out reason);
            pnlLock.Visible = !canEdit;
            litLock.Text = Ui.H(reason);
            foreach (var c in new WebControl[] { txtMort, txtCull, txtSold, ddlFeed, txtFeed, ddlGrit, txtGrit, txtGritTime, txtWater, txtTable, txtCrack, txtMis, txtDY, txtBW, txtEW, txtTmin, txtTmax, txtHum, txtRemarks })
                c.Enabled = canEdit;
            btnSave.Visible = btnSaveNext.Visible = canEdit;
            btnDelete.Visible = canEdit && log != null && Ctx.IsManager;

            BindHistory(shedId, d);
        }

        void BindHistory(int shedId, DateTime d)
        {
            var dt = Db.Query(@"SELECT TOP (7) d.LogDate, d.Mortality + d.Culls AS Depl, d.FeedKg, d.TotalEggs, d.AvgBodyWtG, d.AvgEggWtG, fl.HatchDate,
                                       ISNULL((SELECT SUM(m.Qty) FROM dbo.vBirdMovement m WHERE m.ShedId = d.ShedId AND m.FlockId = d.FlockId
                                               AND (m.MoveDate < d.LogDate OR (m.MoveDate = d.LogDate AND m.Kind IN (N'PLACE', N'TIN', N'TOUT')))), 0) AS Birds
                                FROM dbo.DailyLog d JOIN dbo.Flock fl ON fl.FlockId = d.FlockId
                                WHERE d.ShedId = @s AND d.LogDate <= @d ORDER BY d.LogDate DESC", Db.P("@s", shedId), Db.P("@d", d));
            pnlHistory.Visible = dt.Rows.Count > 0;
            var sb = new StringBuilder("<table class=\"table table-erp\"><thead><tr><th>Date</th><th>Age</th><th class=\"num\">Birds</th><th class=\"num\">Mort+Cull</th><th class=\"num\">Feed Kg</th><th class=\"num\">g/bird</th><th class=\"num\">Eggs</th><th class=\"num\">HD %</th><th class=\"num\">Egg wt</th><th class=\"num\">Body wt</th><th></th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                int birds = Db.Int(r["Birds"]); decimal feed = Db.Dec(r["FeedKg"]); int eggs = Db.Int(r["TotalEggs"]);
                var ld = Convert.ToDateTime(r["LogDate"]);
                sb.Append("<tr><td>").Append(Ui.D(ld)).Append("</td><td>").Append(Age.Text(Convert.ToDateTime(r["HatchDate"]), ld)).Append("</td><td class=\"num\">").Append(Ui.N0(birds))
                  .Append("</td><td class=\"num\">").Append(Ui.N0(r["Depl"])).Append("</td><td class=\"num\">").Append(Ui.N1(feed))
                  .Append("</td><td class=\"num\">").Append(birds > 0 ? Ui.N1(feed * 1000m / birds) : "-").Append("</td><td class=\"num\">").Append(Ui.N0(eggs))
                  .Append("</td><td class=\"num\">").Append(birds > 0 && eggs > 0 ? Ui.N2(eggs * 100m / birds) : "-").Append("</td><td class=\"num\">").Append(r["AvgEggWtG"] == DBNull.Value ? "-" : Ui.N1(Db.Dec(r["AvgEggWtG"])))
                  .Append("</td><td class=\"num\">").Append(Ui.N0(r["AvgBodyWtG"])).Append("</td><td class=\"text-end\">")
                  .Append(ld == d ? "" : "<a class=\"btn btn-sm btn-light border\" href=\"?shed=" + shedId + "&date=" + Ui.DateValue(ld) + "\">open</a>").Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            litHistory.Text = sb.ToString();
        }

        // ------------------------------------------------------------------ save / delete

        int ReqInt(TextBox tb, string label, List<string> err)
        {
            var s = (tb.Text ?? "").Trim();
            if (s.Length == 0) return 0;
            var v = Ui.Int(s);
            if (!v.HasValue || v.Value < 0) { err.Add(label + " must be a whole number of 0 or more."); return 0; }
            return v.Value;
        }

        decimal? OptDec(TextBox tb, string label, decimal min, decimal max, List<string> err)
        {
            var s = (tb.Text ?? "").Trim();
            if (s.Length == 0) return null;
            var v = Ui.Dec(s);
            if (!v.HasValue) { err.Add(label + " must be a number."); return null; }
            if (v.Value < min || v.Value > max) { err.Add(label + " must be between " + min.ToString("0.##") + " and " + max.ToString("0.##") + "."); return null; }
            return v;
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (!ShedId.HasValue) return;
            int shedId = ShedId.Value, flockId = int.Parse(hidFlock.Value, CultureInfo.InvariantCulture);
            var d = EntryDate;
            string reason;
            if (!Ops.CanEditDay(shedId, d, out reason)) { Fail(reason); return; }

            var err = new List<string>();
            int mort = ReqInt(txtMort, "Mortality", err), cull = ReqInt(txtCull, "Culls", err), sold = ReqInt(txtSold, "Birds sold", err);
            int table = ReqInt(txtTable, "Table eggs", err), crack = ReqInt(txtCrack, "Cracked eggs", err), mis = ReqInt(txtMis, "Misshapen eggs", err), dy = ReqInt(txtDY, "Double yolk", err);
            decimal feed = OptDec(txtFeed, "Feed", 0, 1000000, err) ?? 0;
            decimal grit = OptDec(txtGrit, "Shell grit", 0, 100000, err) ?? 0;
            var water = OptDec(txtWater, "Water", 0, 10000000, err);
            var bw = OptDec(txtBW, "Body weight", 20, 5000, err);
            var ew = OptDec(txtEW, "Egg weight", 20, 100, err);
            var tmin = OptDec(txtTmin, "Min temperature", -10, 60, err);
            var tmax = OptDec(txtTmax, "Max temperature", -10, 60, err);
            var hum = OptDec(txtHum, "Humidity", 0, 100, err);
            var gritTime = Ui.Time(txtGritTime.Text);
            if ((txtGritTime.Text ?? "").Trim().Length > 0 && !gritTime.HasValue) err.Add("Grit time must be HH:mm.");
            if (tmin.HasValue && tmax.HasValue && tmin > tmax) err.Add("Minimum temperature cannot be above maximum.");
            int? feedItem = Ui.SelInt(ddlFeed), gritItem = Ui.SelInt(ddlGrit);
            if (feed > 0 && !feedItem.HasValue) err.Add("Select the feed formulation.");
            if (grit > 0 && !gritItem.HasValue) err.Add("Select the shell grit item.");

            ShedPosition pos = null;
            foreach (var p in Ops.Positions(null, null, shedId, d)) if (p.FlockId == flockId) pos = p;
            bool broiler = Ops.IsBroiler(Ops.Flock(flockId));
            int avail = pos == null ? 0 : pos.Available;
            if (broiler)
            {
                sold = 0; grit = 0; gritItem = null;
                avail -= Db.Int(Db.Scalar("SELECT ISNULL(SUM(CAST(Qty AS INT)),0) FROM dbo.Sale WHERE ShedId = @s AND FlockId = @f AND Product = N'Broiler Bird' AND SaleDate = @d",
                    Db.P("@s", shedId), Db.P("@f", flockId), Db.P("@d", d)));
            }
            int eggs = table + crack + mis + dy;
            if (mort + cull + sold > avail) err.Add("Mortality + culls + sold (" + (mort + cull + sold) + ") is more than the birds in the shed (" + avail + ").");
            if (avail > 0 && eggs > avail * 1.02m) err.Add("Total eggs (" + eggs.ToString("N0") + ") cannot exceed the hens present (" + avail.ToString("N0") + ").");
            if (avail > 0 && feed * 1000m / avail > 350) err.Add("Feed works out to " + (feed * 1000m / avail).ToString("N0") + " g per bird - please check the quantity (Kg).");
            if (err.Count > 0) { ShowErr(err); return; }

            int logId = 0;
            try
            {
                logId = Db.Tx((c, t) =>
                {
                    var ps = new[]
                    {
                        Db.P("@c", Ctx.CompanyId), Db.P("@s", shedId), Db.P("@f", flockId), Db.P("@d", d), Db.P("@mort", mort), Db.P("@cull", cull), Db.P("@sold", sold),
                        Db.P("@fi", feedItem), Db.P("@fk", feed), Db.P("@gi", gritItem), Db.P("@gk", grit), Db.P("@gt", gritTime),
                        Db.P("@te", table), Db.P("@ce", crack), Db.P("@me", mis), Db.P("@de", dy), Db.P("@bw", bw), Db.P("@ew", ew), Db.P("@w", water),
                        Db.P("@tmin", tmin), Db.P("@tmax", tmax), Db.P("@h", hum), Db.P("@r", NullIfEmpty(txtRemarks.Text)), Db.P("@u", Ctx.UserId)
                    };
                    var existing = Db.NInt(Db.Scalar(c, t, "SELECT DailyLogId FROM dbo.DailyLog WITH (UPDLOCK, HOLDLOCK) WHERE ShedId = @s AND LogDate = @d", Db.P("@s", shedId), Db.P("@d", d)));
                    int id;
                    if (existing.HasValue)
                    {
                        var list = new List<System.Data.SqlClient.SqlParameter>(ps); list.Add(Db.P("@id", existing.Value));
                        Db.Exec(c, t, @"UPDATE dbo.DailyLog SET FlockId = @f, Mortality = @mort, Culls = @cull, BirdsSold = @sold, FeedItemId = @fi, FeedKg = @fk, GritItemId = @gi, GritKg = @gk,
                                        GritTime = @gt, TableEggs = @te, CrackedEggs = @ce, MisshapenEggs = @me, DoubleYolkEggs = @de, AvgBodyWtG = @bw, AvgEggWtG = @ew, WaterLtr = @w,
                                        TempMinC = @tmin, TempMaxC = @tmax, HumidityPct = @h, Remarks = @r, ModifiedBy = @u, ModifiedOn = SYSDATETIME() WHERE DailyLogId = @id", list.ToArray());
                        id = existing.Value;
                        Audit.Log(c, t, d < DateTime.Today.AddDays(-Company.EditWindowDays) ? "UPDATE_LOCKED" : "UPDATE", "DailyLog", id, Describe(shedId, d, mort, cull, sold, feed, eggs));
                    }
                    else
                    {
                        id = Db.Int(Db.Scalar(c, t, @"INSERT INTO dbo.DailyLog (CompanyId, ShedId, FlockId, LogDate, Mortality, Culls, BirdsSold, FeedItemId, FeedKg, GritItemId, GritKg, GritTime,
                                        TableEggs, CrackedEggs, MisshapenEggs, DoubleYolkEggs, AvgBodyWtG, AvgEggWtG, WaterLtr, TempMinC, TempMaxC, HumidityPct, Remarks, CreatedBy)
                                        VALUES (@c, @s, @f, @d, @mort, @cull, @sold, @fi, @fk, @gi, @gk, @gt, @te, @ce, @me, @de, @bw, @ew, @w, @tmin, @tmax, @h, @r, @u);
                                        SELECT CAST(SCOPE_IDENTITY() AS INT);", ps));
                        Audit.Log(c, t, "INSERT", "DailyLog", id, Describe(shedId, d, mort, cull, sold, feed, eggs));
                    }
                    Ops.EnsureNoNegative(c, t, shedId, flockId);
                    return id;
                });
            }
            catch (Exception ex)
            {
                ShowErr(new List<string> { Db.FriendlyError(ex) });
                return;
            }

            // soft checks against the standard - saved, but flagged
            var warn = new List<string>();
            var f = Ops.Flock(flockId);
            int age = Age.Days(Convert.ToDateTime(f["HatchDate"]), d);
            var std = Ops.Standard(Db.Int(f["BreedId"]), age);
            if (avail > 0 && (mort + cull) * 100m / avail > 0.5m) warn.Add("Mortality is " + ((mort + cull) * 100m / avail).ToString("0.00") + "% today");
            if (std != null && std.FeedG.HasValue && avail > 0 && feed > 0 && Math.Abs(feed * 1000m / avail - std.FeedG.Value) > std.FeedG.Value * 0.2m)
                warn.Add("feed per bird differs from standard by more than 20%");
            if (std != null && std.HDP.HasValue && avail > 0 && Company.IsLaying(age, Ops.IsBroiler(f)) && eggs * 100m / avail < std.HDP.Value - 10) warn.Add("HD% is more than 10 points below standard");
            if (broiler)
            {
                if (age > Company.BroilerMaxDays) warn.Add("batch is " + age + " days old - over the " + Company.BroilerMaxDays + "-day limit");
                if (std != null && std.BodyWt.HasValue && bw.HasValue && bw.Value < std.BodyWt.Value * 0.9m) warn.Add("body weight is more than 10% below standard");
            }
            if (warn.Count > 0) Warn("Saved. Please check: " + string.Join("; ", warn.ToArray()) + ".");
            else Ok("Saved " + Ops.ShedCode(null, null, shedId) + " for " + Ui.D(d) + ".");

            var ids = BindSheds();
            if (((LinkButton)sender).CommandArgument == "next")
            {
                int? next = null;
                foreach (var id in ids)
                    if (id != shedId && Db.Scalar("SELECT 1 FROM dbo.DailyLog WHERE ShedId = @s AND LogDate = @d", Db.P("@s", id), Db.P("@d", d)) == null) { next = id; break; }
                if (next.HasValue) { LoadShed(next.Value); BindSheds(); return; }
                Ok("All sheds are entered for " + Ui.D(d) + ".");
            }
            LoadShed(shedId);
        }

        protected void btnDelete_Click(object sender, EventArgs e)
        {
            if (!ShedId.HasValue || string.IsNullOrEmpty(hidLog.Value) || !Ctx.IsManager) return;
            int shedId = ShedId.Value, flockId = int.Parse(hidFlock.Value, CultureInfo.InvariantCulture), logId = int.Parse(hidLog.Value, CultureInfo.InvariantCulture);
            try
            {
                Db.Tx((c, t) =>
                {
                    Db.Exec(c, t, "DELETE FROM dbo.DailyLog WHERE DailyLogId = @id AND CompanyId = @c", Db.P("@id", logId), Db.P("@c", Ctx.CompanyId));
                    Ops.EnsureNoNegative(c, t, shedId, flockId);
                    Audit.Log(c, t, "DELETE", "DailyLog", logId, "shed " + shedId + " date " + Ui.D(EntryDate));
                    return 0;
                });
                Ok("Entry deleted.");
            }
            catch (Exception ex) { Fail(Db.FriendlyError(ex)); }
            BindSheds();
            LoadShed(shedId);
        }

        void ShowErr(List<string> err)
        {
            pnlErr.Visible = true;
            var sb = new StringBuilder("<i class=\"bi bi-exclamation-triangle me-1\"></i>");
            for (int i = 0; i < err.Count; i++) { if (i > 0) sb.Append("<br/>"); sb.Append(Ui.H(err[i])); }
            litErr.Text = sb.ToString();
        }

        static string NullIfEmpty(string s) { s = (s ?? "").Trim(); return s.Length == 0 ? null : s; }

        static string Describe(int shed, DateTime d, int mort, int cull, int sold, decimal feed, int eggs)
        {
            return "shed=" + shed + "; date=" + d.ToString("yyyy-MM-dd") + "; mort=" + mort + "; culls=" + cull + "; sold=" + sold + "; feedKg=" + feed + "; eggs=" + eggs;
        }
    }
}
