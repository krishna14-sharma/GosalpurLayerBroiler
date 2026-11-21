using System;
using System.Data;
using System.Text;

namespace PoultryERP
{
    public partial class OverridesPage : BasePage
    {
        protected override string[] AllowedRoles { get { return Roles.Managers; } }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            Ui.Bind(ddlShed, "SELECT ShedId AS Value, Code + ' - ' + Name AS Text FROM dbo.Shed WHERE CompanyId = @c AND IsActive = 1 ORDER BY Code", "(all sheds)", Db.P("@c", Ctx.CompanyId));
            Ui.Bind(ddlUser, "SELECT UserId AS Value, FullName + ' (' + UserName + ')' AS Text FROM dbo.AppUser WHERE CompanyId = @c AND Role = 'Supervisor' AND IsActive = 1 ORDER BY FullName",
                    "(any supervisor)", Db.P("@c", Ctx.CompanyId));
            txtFrom.Text = Ui.DateValue(DateTime.Today.AddDays(-7));
            txtTo.Text = Ui.DateValue(DateTime.Today.AddDays(-Company.EditWindowDays - 1));
            BindList();
        }

        protected void btnGrant_Click(object sender, EventArgs e)
        {
            var f = Ui.Date(txtFrom.Text); var t = Ui.Date(txtTo.Text); var h = Ui.Int(txtHours.Text) ?? 24;
            var reason = (txtReason.Text ?? "").Trim();
            if (!f.HasValue || !t.HasValue || f > t) { Fail("Enter a valid date range."); return; }
            if (t > DateTime.Today) { Fail("Overrides are for past dates only."); return; }
            if (reason.Length < 5) { Fail("Give a reason (at least 5 characters)."); return; }
            if (h < 1 || h > 168) { Fail("Validity must be between 1 and 168 hours."); return; }
            var id = Db.Scalar(@"INSERT INTO dbo.EditOverride (CompanyId, ShedId, FromDate, ToDate, GrantedToUserId, Reason, GrantedBy, ExpiresOn)
                                VALUES (@c, @s, @f, @t, @u, @r, @by, DATEADD(HOUR, @h, SYSDATETIME())); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                Db.P("@c", Ctx.CompanyId), Db.P("@s", Ui.SelInt(ddlShed)), Db.P("@f", f.Value), Db.P("@t", t.Value), Db.P("@u", Ui.SelInt(ddlUser)),
                Db.P("@r", reason), Db.P("@by", Ctx.UserId), Db.P("@h", h));
            Audit.Log("OVERRIDE", "EditOverride", id, Ui.D(f) + " to " + Ui.D(t) + " shed " + ddlShed.SelectedItem.Text + " user " + ddlUser.SelectedItem.Text + " : " + reason);
            Ok("Override granted for " + h + " hours.");
            txtReason.Text = "";
            BindList();
        }

        protected void btnCmd_Click(object sender, EventArgs e)
        {
            int id;
            var cmd = hidCmd.Value ?? ""; hidCmd.Value = "";
            if (!cmd.StartsWith("revoke:") || !int.TryParse(cmd.Substring(7), out id)) return;
            Db.Exec("UPDATE dbo.EditOverride SET IsRevoked = 1 WHERE OverrideId = @id AND CompanyId = @c", Db.P("@id", id), Db.P("@c", Ctx.CompanyId));
            Audit.Log("REVOKE", "EditOverride", id, null);
            Ok("Override revoked.");
            BindList();
        }

        void BindList()
        {
            var dt = Db.Query(@"SELECT TOP (100) o.*, s.Code AS ShedCode, u.FullName AS UserName, g.FullName AS GrantedName,
                                CASE WHEN o.IsRevoked = 1 THEN N'Revoked' WHEN o.ExpiresOn <= SYSDATETIME() THEN N'Expired' ELSE N'Active' END AS State
                                FROM dbo.EditOverride o LEFT JOIN dbo.Shed s ON s.ShedId = o.ShedId LEFT JOIN dbo.AppUser u ON u.UserId = o.GrantedToUserId
                                LEFT JOIN dbo.AppUser g ON g.UserId = o.GrantedBy WHERE o.CompanyId = @c ORDER BY o.GrantedOn DESC", Db.P("@c", Ctx.CompanyId));
            if (dt.Rows.Count == 0) { litList.Text = "<div class=\"empty-state\">No overrides granted.</div>"; return; }
            var sb = new StringBuilder("<table class=\"table table-erp\"><thead><tr><th>Dates</th><th>Shed</th><th>User</th><th>Reason</th><th>Granted</th><th>Expires</th><th>State</th><th></th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                var state = Db.Str(r["State"]);
                sb.Append("<tr><td>").Append(Ui.D(r["FromDate"])).Append(" &ndash; ").Append(Ui.D(r["ToDate"])).Append("</td><td>").Append(r["ShedCode"] == DBNull.Value ? "all" : Ui.H(r["ShedCode"]))
                  .Append("</td><td>").Append(r["UserName"] == DBNull.Value ? "any supervisor" : Ui.H(r["UserName"])).Append("</td><td class=\"small\">").Append(Ui.H(r["Reason"]))
                  .Append("</td><td class=\"small\">").Append(Ui.H(r["GrantedName"])).Append("<div class=\"text-muted-2\">").Append(Convert.ToDateTime(r["GrantedOn"]).ToString("dd-MM HH:mm"))
                  .Append("</div></td><td class=\"small\">").Append(Convert.ToDateTime(r["ExpiresOn"]).ToString("dd-MM HH:mm")).Append("</td><td>")
                  .Append(Ui.StatusBadge(state == "Revoked" ? "Rejected" : state == "Expired" ? "Expired" : "Active").Replace(">Rejected<", ">Revoked<"))
                  .Append("</td><td>").Append(state == "Active" ? "<a href=\"#\" class=\"btn btn-outline-danger\" onclick=\"return ovrCmd('revoke:" + r["OverrideId"] + "')\">Revoke</a>" : "")
                  .Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            litList.Text = sb.ToString();
        }
    }
}
