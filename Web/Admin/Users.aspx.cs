using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web.UI.WebControls;

namespace PoultryERP
{
    public partial class UsersPage : BasePage
    {
        protected override string[] AllowedRoles { get { return new[] { Roles.SuperAdmin }; } }

        protected void Page_Load(object sender, EventArgs e)
        {
            foreach (var cb in new[] { chkMust, chkActive }) { cb.InputAttributes["class"] = "form-check-input"; cb.LabelAttributes["class"] = "form-check-label"; }
            if (IsPostBack) return;
            ddlRole.Items.Clear();
            foreach (var r in Roles.All) ddlRole.Items.Add(new ListItem(Roles.Display(r), r));
            ddlRole.SelectedValue = Roles.Supervisor;
            Ui.Bind(ddlCompany, "SELECT CompanyId AS Value, Code + ' - ' + Name AS Text FROM dbo.Company ORDER BY Code", null);
            Ui.Select(ddlCompany, Ctx.CompanyId);
            Role_Changed(null, null);
            BindList();
        }

        protected void Role_Changed(object sender, EventArgs e)
        {
            bool sa = ddlRole.SelectedValue == Roles.SuperAdmin;
            ddlCompany.Enabled = !sa;
            pnlSheds.Visible = ddlRole.SelectedValue == Roles.Supervisor;
            var keep = new HashSet<string>();
            foreach (ListItem li in cblSheds.Items) if (li.Selected) keep.Add(li.Value);
            cblSheds.Items.Clear();
            int c; if (!int.TryParse(ddlCompany.SelectedValue, out c)) return;
            foreach (DataRow r in Db.Query("SELECT s.ShedId, s.Code, f.Code AS Farm FROM dbo.Shed s JOIN dbo.Location l ON l.LocationId = s.LocationId JOIN dbo.Farm f ON f.FarmId = l.FarmId WHERE s.CompanyId = @c AND s.IsActive = 1 ORDER BY f.Code, s.Code",
                Db.P("@c", c)).Rows)
            {
                var li = new ListItem(" " + Ui.H(r["Code"]) + " (" + Ui.H(r["Farm"]) + ")", Db.Str(r["ShedId"]));
                li.Selected = keep.Contains(li.Value);
                cblSheds.Items.Add(li);
            }
        }

        protected void btnNew_Click(object sender, EventArgs e)
        {
            hidId.Value = "";
            txtUser.Text = txtName.Text = txtEmail.Text = txtPhone.Text = txtPw.Text = "";
            txtUser.Enabled = true; chkMust.Checked = true; chkActive.Checked = true;
            ddlRole.SelectedValue = Roles.Supervisor; Role_Changed(null, null);
            foreach (ListItem li in cblSheds.Items) li.Selected = false;
            litTitle.Text = "New user"; litPw.Text = "Password"; pnlErr.Visible = false;
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            var err = new List<string>();
            var user = (txtUser.Text ?? "").Trim(); var name = (txtName.Text ?? "").Trim();
            var role = ddlRole.SelectedValue;
            int? company = role == Roles.SuperAdmin ? (int?)null : Ui.SelInt(ddlCompany);
            int? id = string.IsNullOrEmpty(hidId.Value) ? (int?)null : int.Parse(hidId.Value, CultureInfo.InvariantCulture);
            if (user.Length < 3 || user.IndexOf(' ') >= 0) err.Add("User name must be at least 3 characters with no spaces.");
            if (name.Length == 0) err.Add("Full name is required.");
            if (role != Roles.SuperAdmin && !company.HasValue) err.Add("Select the company.");
            var pw = txtPw.Text ?? "";
            if (!id.HasValue || pw.Length > 0) { var pe = PasswordHasher.StrengthError(pw); if (pe != null) err.Add(pe); }
            if (id.HasValue && id.Value == Ctx.UserId && (!chkActive.Checked || role != Roles.SuperAdmin)) err.Add("You cannot disable or demote your own account.");
            var sheds = new List<int>();
            if (role == Roles.Supervisor) foreach (ListItem li in cblSheds.Items) if (li.Selected) sheds.Add(int.Parse(li.Value, CultureInfo.InvariantCulture));
            if (role == Roles.Supervisor && sheds.Count == 0) err.Add("Assign at least one shed to the supervisor.");
            if (err.Count > 0) { ShowErr(err); return; }
            try
            {
                Db.Tx((c, t) =>
                {
                    int uid;
                    if (id.HasValue)
                    {
                        Db.Exec(c, t, @"UPDATE dbo.AppUser SET FullName = @n, Email = @e, Phone = @p, Role = @r, CompanyId = @c, IsActive = @a, MustChangePassword = @m,
                                        PasswordHash = CASE WHEN @h IS NULL THEN PasswordHash ELSE @h END WHERE UserId = @id",
                            Db.P("@n", name), Db.P("@e", Nz(txtEmail.Text)), Db.P("@p", Nz(txtPhone.Text)), Db.P("@r", role), Db.P("@c", company), Db.P("@a", chkActive.Checked),
                            Db.P("@m", chkMust.Checked), Db.P("@h", pw.Length > 0 ? PasswordHasher.Hash(pw) : null), Db.P("@id", id.Value));
                        uid = id.Value;
                        Audit.Log(c, t, "UPDATE", "AppUser", uid, "role=" + role + "; company=" + company + "; active=" + chkActive.Checked + (pw.Length > 0 ? "; password reset" : ""));
                    }
                    else
                    {
                        uid = Db.Int(Db.Scalar(c, t, @"INSERT INTO dbo.AppUser (CompanyId, UserName, FullName, Email, Phone, Role, PasswordHash, MustChangePassword, IsActive)
                                                      VALUES (@c, @u, @n, @e, @p, @r, @h, @m, @a); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                            Db.P("@c", company), Db.P("@u", user), Db.P("@n", name), Db.P("@e", Nz(txtEmail.Text)), Db.P("@p", Nz(txtPhone.Text)), Db.P("@r", role),
                            Db.P("@h", PasswordHasher.Hash(pw)), Db.P("@m", chkMust.Checked), Db.P("@a", chkActive.Checked)));
                        Audit.Log(c, t, "INSERT", "AppUser", uid, "user=" + user + "; role=" + role + "; company=" + company);
                    }
                    Db.Exec(c, t, "DELETE FROM dbo.UserShed WHERE UserId = @u", Db.P("@u", uid));
                    foreach (var s in sheds) Db.Exec(c, t, "INSERT INTO dbo.UserShed (UserId, ShedId) VALUES (@u, @s)", Db.P("@u", uid), Db.P("@s", s));
                    return uid;
                });
                Ok("User saved." + (id.HasValue ? " Changes apply at the user's next sign-in." : ""));
                btnNew_Click(null, null);
                BindList();
            }
            catch (Exception ex) { ShowErr(new List<string> { Db.FriendlyError(ex) }); }
        }

        protected void btnCmd_Click(object sender, EventArgs e)
        {
            var cmd = (hidCmd.Value ?? "").Split(':'); hidCmd.Value = "";
            int id;
            if (cmd.Length != 2 || !int.TryParse(cmd[1], out id)) return;
            if (cmd[0] == "unlock")
            {
                Db.Exec("UPDATE dbo.AppUser SET FailedAttempts = 0, LockedUntil = NULL WHERE UserId = @id", Db.P("@id", id));
                Audit.Log("UNLOCK", "AppUser", id, null); Ok("Account unlocked."); BindList(); return;
            }
            if (cmd[0] != "edit") return;
            var r = Db.Row("SELECT * FROM dbo.AppUser WHERE UserId = @id", Db.P("@id", id));
            if (r == null) return;
            hidId.Value = id.ToString(CultureInfo.InvariantCulture);
            txtUser.Text = Db.Str(r["UserName"]); txtUser.Enabled = false;
            txtName.Text = Db.Str(r["FullName"]); txtEmail.Text = Db.Str(r["Email"]); txtPhone.Text = Db.Str(r["Phone"]);
            ddlRole.SelectedValue = Db.Str(r["Role"]);
            if (r["CompanyId"] != DBNull.Value) Ui.Select(ddlCompany, r["CompanyId"]);
            chkActive.Checked = Convert.ToBoolean(r["IsActive"]); chkMust.Checked = Convert.ToBoolean(r["MustChangePassword"]);
            Role_Changed(null, null);
            var assigned = new HashSet<string>();
            foreach (DataRow s in Db.Query("SELECT ShedId FROM dbo.UserShed WHERE UserId = @id", Db.P("@id", id)).Rows) assigned.Add(Db.Str(s["ShedId"]));
            foreach (ListItem li in cblSheds.Items) li.Selected = assigned.Contains(li.Value);
            litTitle.Text = "Edit user <b>" + Ui.H(r["UserName"]) + "</b>"; litPw.Text = "New password (leave blank to keep)"; pnlErr.Visible = false;
        }

        void BindList()
        {
            var dt = Db.Query(@"SELECT u.*, c.Code AS CompanyCode,
                                STUFF((SELECT ', ' + s.Code FROM dbo.UserShed us JOIN dbo.Shed s ON s.ShedId = us.ShedId WHERE us.UserId = u.UserId ORDER BY s.Code FOR XML PATH('')), 1, 2, '') AS Sheds
                                FROM dbo.AppUser u LEFT JOIN dbo.Company c ON c.CompanyId = u.CompanyId ORDER BY CASE WHEN u.Role = 'SuperAdmin' THEN 0 ELSE 1 END, c.Code, u.UserName");
            var sb = new StringBuilder("<table class=\"table table-erp table-hover\"><thead><tr><th>User</th><th>Role</th><th>Company</th><th>Sheds</th><th>Last login</th><th>Status</th><th></th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                int id = Db.Int(r["UserId"]);
                bool locked = r["LockedUntil"] != DBNull.Value && Convert.ToDateTime(r["LockedUntil"]) > DateTime.Now;
                sb.Append("<tr><td><b>").Append(Ui.H(r["UserName"])).Append("</b><div class=\"small text-muted-2\">").Append(Ui.H(r["FullName"])).Append("</div></td><td class=\"small\">")
                  .Append(Ui.H(Roles.Display(Db.Str(r["Role"])))).Append("</td><td>").Append(r["CompanyCode"] == DBNull.Value ? "<span class=\"text-muted-2\">all</span>" : Ui.H(r["CompanyCode"]))
                  .Append("</td><td class=\"small\">").Append(Ui.H(r["Sheds"])).Append("</td><td class=\"small\">")
                  .Append(r["LastLoginOn"] == DBNull.Value ? "never" : Convert.ToDateTime(r["LastLoginOn"]).ToString("dd-MM-yy HH:mm")).Append("</td><td>")
                  .Append(locked ? "<span class=\"badge text-bg-danger\">Locked</span>" : Convert.ToBoolean(r["IsActive"]) ? "<span class=\"badge text-bg-success\">Active</span>" : "<span class=\"badge text-bg-secondary\">Disabled</span>")
                  .Append("</td><td class=\"text-end text-nowrap\">")
                  .Append(locked ? "<a href=\"#\" class=\"btn btn-outline-warning\" title=\"Unlock\" onclick=\"return usrCmd('unlock:" + id + "')\"><i class=\"bi bi-unlock\"></i></a> " : "")
                  .Append("<a href=\"#\" class=\"btn btn-outline-primary\" onclick=\"return usrCmd('edit:").Append(id).Append("')\"><i class=\"bi bi-pencil\"></i></a></td></tr>");
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
