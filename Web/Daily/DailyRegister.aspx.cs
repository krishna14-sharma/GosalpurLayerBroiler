using System;
using System.Data;
using System.Text;

namespace PoultryERP
{
    public partial class DailyRegisterPage : BasePage
    {
        const int PageSize = 50;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            txtFrom.Text = Ui.DateValue(DateTime.Today.AddDays(-6));
            txtTo.Text = Ui.DateValue(DateTime.Today);
            Ui.Bind(ddlFarm, "SELECT FarmId AS Value, Name AS Text FROM dbo.Farm WHERE CompanyId = @c ORDER BY Name", "All farms", Db.P("@c", Ctx.CompanyId));
            Ui.Bind(ddlShed, "SELECT s.ShedId AS Value, s.Code + ' - ' + s.Name AS Text FROM dbo.Shed s WHERE s.CompanyId = @CompanyId" + Ctx.ShedFilter("s.ShedId") + " ORDER BY s.Code", "All sheds", Ctx.Scope());
            Bind();
        }

        protected void btnShow_Click(object sender, EventArgs e) { hidPage.Value = "1"; Bind(); }

        protected void btnCmd_Click(object sender, EventArgs e)
        {
            int p;
            if ((hidCmd.Value ?? "").StartsWith("page:") && int.TryParse(hidCmd.Value.Substring(5), out p)) hidPage.Value = Math.Max(1, p).ToString();
            hidCmd.Value = "";
            Bind();
        }

        void Bind()
        {
            var from = Ui.Date(txtFrom.Text) ?? DateTime.Today.AddDays(-6);
            var to = Ui.Date(txtTo.Text) ?? DateTime.Today;
            int page = int.Parse(hidPage.Value);
            const string where = @" FROM dbo.DailyLog d JOIN dbo.Shed s ON s.ShedId = d.ShedId JOIN dbo.Location l ON l.LocationId = s.LocationId
                                    JOIN dbo.Flock f ON f.FlockId = d.FlockId LEFT JOIN dbo.Item i ON i.ItemId = d.FeedItemId
                                    LEFT JOIN dbo.AppUser cu ON cu.UserId = d.CreatedBy LEFT JOIN dbo.AppUser mu ON mu.UserId = d.ModifiedBy
                                    WHERE d.CompanyId = @CompanyId AND d.LogDate BETWEEN @f AND @t AND (@farm IS NULL OR l.FarmId = @farm) AND (@shed IS NULL OR d.ShedId = @shed)";
            var ps = Ctx.Scope(Db.P("@f", from), Db.P("@t", to), Db.P("@farm", Ui.SelInt(ddlFarm)), Db.P("@shed", Ui.SelInt(ddlShed)));
            int total = Db.Int(Db.Scalar("SELECT COUNT(*)" + where + Ctx.ShedFilter("d.ShedId"), ps));
            int pages = Math.Max(1, (total + PageSize - 1) / PageSize);
            if (page > pages) page = pages;
            var dt = Db.Query("SELECT d.*, s.Code AS ShedCode, f.FlockCode, f.HatchDate, i.Name AS FeedName, cu.FullName AS CreatedName, mu.FullName AS ModName" + where + Ctx.ShedFilter("d.ShedId") +
                              " ORDER BY d.LogDate DESC, s.Code OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
                              Ctx.Scope(Db.P("@f", from), Db.P("@t", to), Db.P("@farm", Ui.SelInt(ddlFarm)), Db.P("@shed", Ui.SelInt(ddlShed)),
                                        Db.P("@skip", (page - 1) * PageSize), Db.P("@take", PageSize)));
            litCount.Text = total.ToString("N0") + " entries";
            bool canEnter = Ctx.InRole(Roles.SuperAdmin, Roles.FarmManager, Roles.Supervisor);
            var url = ResolveUrl("~/Daily/DailyEntry.aspx");
            if (dt.Rows.Count == 0) { litList.Text = "<div class=\"empty-state\"><i class=\"bi bi-inbox\"></i>No entries for these filters.</div>"; litPager.Text = ""; return; }
            var sb = new StringBuilder("<table class=\"table table-erp table-hover\"><thead><tr><th>Date</th><th>Shed</th><th>Flock</th><th>Age</th><th class=\"num\">Mort</th><th class=\"num\">Culls</th><th class=\"num\">Sold</th>" +
                                       "<th>Feed</th><th class=\"num\">Feed Kg</th><th class=\"num\">Grit Kg</th><th class=\"num\">Eggs</th><th class=\"num\">Cracked</th><th class=\"num\">Water L</th>" +
                                       "<th class=\"num\">Body wt</th><th class=\"num\">Egg wt</th><th>Temp</th><th>Entered by</th><th></th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                var d = Convert.ToDateTime(r["LogDate"]);
                sb.Append("<tr><td>").Append(Ui.D(d)).Append("</td><td><b>").Append(Ui.H(r["ShedCode"])).Append("</b></td><td>").Append(Ui.H(r["FlockCode"]))
                  .Append("</td><td>").Append(Age.Text(Convert.ToDateTime(r["HatchDate"]), d)).Append("</td><td class=\"num\">").Append(Ui.N0(r["Mortality"]))
                  .Append("</td><td class=\"num\">").Append(Ui.N0(r["Culls"])).Append("</td><td class=\"num\">").Append(Ui.N0(r["BirdsSold"]))
                  .Append("</td><td>").Append(Ui.H(r["FeedName"])).Append("</td><td class=\"num\">").Append(Ui.N1(r["FeedKg"])).Append("</td><td class=\"num\">").Append(Ui.N1(r["GritKg"]))
                  .Append("</td><td class=\"num\">").Append(Ui.N0(r["TotalEggs"])).Append("</td><td class=\"num\">").Append(Ui.N0(r["CrackedEggs"]))
                  .Append("</td><td class=\"num\">").Append(Ui.N0(r["WaterLtr"])).Append("</td><td class=\"num\">").Append(Ui.N0(r["AvgBodyWtG"]))
                  .Append("</td><td class=\"num\">").Append(Ui.N1(r["AvgEggWtG"])).Append("</td><td class=\"text-nowrap\">")
                  .Append(r["TempMinC"] == DBNull.Value ? "" : Ui.N1(r["TempMinC"]) + " - " + Ui.N1(r["TempMaxC"]))
                  .Append("</td><td class=\"small\">").Append(Ui.H(r["CreatedName"])).Append(r["ModName"] == DBNull.Value ? "" : "<div class=\"text-muted-2\">edited by " + Ui.H(r["ModName"]) + "</div>")
                  .Append("</td><td>").Append(canEnter ? "<a class=\"btn btn-outline-primary\" href=\"" + url + "?shed=" + r["ShedId"] + "&date=" + Ui.DateValue(d) + "\"><i class=\"bi bi-pencil\"></i></a>" : "")
                  .Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            litList.Text = sb.ToString();
            var pg = new StringBuilder();
            if (pages > 1)
            {
                pg.Append("<div class=\"d-flex justify-content-center p-2 grid-pager\"><div>");
                for (int p = Math.Max(1, page - 4); p <= Math.Min(pages, page + 4); p++)
                    pg.Append(p == page ? "<span>" + p + "</span>" : "<a href=\"#\" onclick=\"return regPage(" + p + ")\">" + p + "</a>");
                pg.Append("</div></div>");
            }
            litPager.Text = pg.ToString();
        }
    }
}
