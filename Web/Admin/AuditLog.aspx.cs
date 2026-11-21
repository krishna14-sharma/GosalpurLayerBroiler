using System;
using System.Data;
using System.Text;
using System.Web.UI.WebControls;

namespace PoultryERP
{
    public partial class AuditLogPage : BasePage
    {
        protected override string[] AllowedRoles { get { return new[] { Roles.SuperAdmin }; } }
        const int PageSize = 100;

        protected void Page_Load(object sender, EventArgs e)
        {
            chkAll.InputAttributes["class"] = "form-check-input"; chkAll.LabelAttributes["class"] = "form-check-label small";
            if (IsPostBack) return;
            txtFrom.Text = Ui.DateValue(DateTime.Today.AddDays(-7));
            txtTo.Text = Ui.DateValue(DateTime.Today);
            ddlAction.Items.Add(new ListItem("All", ""));
            foreach (var a in new[] { "LOGIN", "LOGIN_FAILED", "LOGOUT", "INSERT", "UPDATE", "UPDATE_LOCKED", "DELETE", "REQUEST", "APPROVE", "REJECT", "OVERRIDE", "REVOKE", "EXPORT", "ERROR" })
                ddlAction.Items.Add(new ListItem(a, a));
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
            var from = Ui.Date(txtFrom.Text) ?? DateTime.Today.AddDays(-7);
            var to = (Ui.Date(txtTo.Text) ?? DateTime.Today).AddDays(1);
            int page = int.Parse(hidPage.Value);
            const string where = @" FROM dbo.AuditLog a LEFT JOIN dbo.Company c ON c.CompanyId = a.CompanyId
                                    WHERE a.CreatedOn >= @f AND a.CreatedOn < @t AND (@all = 1 OR a.CompanyId = @c OR a.CompanyId IS NULL)
                                    AND (@u = '' OR a.UserName LIKE @ul) AND (@a = '' OR a.Action = @a) AND (@e = '' OR a.EntityName LIKE @el)";
            Func<System.Data.SqlClient.SqlParameter[]> ps = () => new[]
            {
                Db.P("@f", from), Db.P("@t", to), Db.P("@all", chkAll.Checked ? 1 : 0), Db.P("@c", Ctx.CompanyId),
                Db.P("@u", (txtUser.Text ?? "").Trim()), Db.P("@ul", "%" + (txtUser.Text ?? "").Trim() + "%"),
                Db.P("@a", ddlAction.SelectedValue ?? ""), Db.P("@e", (txtEntity.Text ?? "").Trim()), Db.P("@el", "%" + (txtEntity.Text ?? "").Trim() + "%")
            };
            int total = Db.Int(Db.Scalar("SELECT COUNT(*)" + where, ps()));
            int pages = Math.Max(1, (total + PageSize - 1) / PageSize);
            if (page > pages) page = pages;
            var list = new System.Collections.Generic.List<System.Data.SqlClient.SqlParameter>(ps());
            list.Add(Db.P("@skip", (page - 1) * PageSize)); list.Add(Db.P("@take", PageSize));
            var dt = Db.Query("SELECT a.*, c.Code AS CompanyCode" + where + " ORDER BY a.AuditId DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", list.ToArray());
            litCount.Text = total.ToString("N0") + " events";
            var sb = new StringBuilder("<table class=\"table table-erp\"><thead><tr><th>When</th><th>Company</th><th>User</th><th>Action</th><th>Entity</th><th>Id</th><th>Details</th><th>IP</th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                var act = Db.Str(r["Action"]);
                string cls = act.StartsWith("DELETE") || act == "ERROR" || act == "LOGIN_FAILED" ? "danger" : act.Contains("LOCKED") || act == "OVERRIDE" ? "warning" : act == "INSERT" || act == "APPROVE" ? "success" : "secondary";
                sb.Append("<tr><td class=\"text-nowrap small\">").Append(Convert.ToDateTime(r["CreatedOn"]).ToString("dd-MM-yy HH:mm:ss")).Append("</td><td>").Append(Ui.H(r["CompanyCode"]))
                  .Append("</td><td>").Append(Ui.H(r["UserName"])).Append("</td><td><span class=\"badge text-bg-").Append(cls).Append("\">").Append(Ui.H(act)).Append("</span></td><td>")
                  .Append(Ui.H(r["EntityName"])).Append("</td><td>").Append(Ui.H(r["EntityId"])).Append("</td><td class=\"small\" style=\"max-width:420px;white-space:normal\">")
                  .Append(Ui.H(r["Details"])).Append("</td><td class=\"small\">").Append(Ui.H(r["IpAddress"])).Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            litList.Text = dt.Rows.Count == 0 ? "<div class=\"empty-state\">No events.</div>" : sb.ToString();
            var pg = new StringBuilder();
            if (pages > 1)
            {
                pg.Append("<div class=\"d-flex justify-content-center p-2 grid-pager\"><div>");
                for (int p = Math.Max(1, page - 4); p <= Math.Min(pages, page + 4); p++)
                    pg.Append(p == page ? "<span>" + p + "</span>" : "<a href=\"#\" onclick=\"return audPage(" + p + ")\">" + p + "</a>");
                pg.Append("</div></div>");
            }
            litPager.Text = pg.ToString();
        }
    }
}
