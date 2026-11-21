using System;
using System.Configuration;
using System.Data;
using System.Web;
using System.Web.Security;
using System.Web.UI;

namespace PoultryERP
{
    public partial class LoginPage : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack && User.Identity.IsAuthenticated && Request.QueryString["ReturnUrl"] == null)
                Response.Redirect("~/Default.aspx");
            if (!IsPostBack) txtUser.Focus();
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            var name = (txtUser.Text ?? "").Trim();
            var pw = txtPassword.Text ?? "";
            if (name.Length == 0 || pw.Length == 0) { Show("Enter user name and password."); return; }

            DataRow r;
            try
            {
                r = Db.Row(@"SELECT u.*, c.IsActive AS CompanyActive,
                               (SELECT TOP (1) s.Status FROM dbo.Subscription s WHERE s.CompanyId = u.CompanyId ORDER BY s.StartDate DESC) AS SubStatus,
                               (SELECT TOP (1) s.EndDate FROM dbo.Subscription s WHERE s.CompanyId = u.CompanyId ORDER BY s.StartDate DESC) AS SubEnd
                             FROM dbo.AppUser u LEFT JOIN dbo.Company c ON c.CompanyId = u.CompanyId
                             WHERE u.UserName = @n", Db.P("@n", name));
            }
            catch (Exception ex)
            {
                Show("Cannot reach the database. Check the connection string in Web.config. (" + ex.Message + ")");
                return;
            }

            const string generic = "Invalid user name or password.";
            if (r == null) { Show(generic); return; }

            int userId = Db.Int(r["UserId"]);
            var lockedUntil = Db.NDate(r["LockedUntil"]);
            if (lockedUntil.HasValue && lockedUntil.Value > DateTime.Now)
            {
                Show("Account locked after failed attempts. Try again after " + lockedUntil.Value.ToString("HH:mm") + ".");
                return;
            }
            if (!Convert.ToBoolean(r["IsActive"])) { Show("This account is disabled. Contact your administrator."); return; }

            if (!PasswordHasher.Verify(pw, Db.Str(r["PasswordHash"])))
            {
                int max;
                if (!int.TryParse(ConfigurationManager.AppSettings["MaxFailedLogins"], out max)) max = 5;
                int fails = Db.Int(r["FailedAttempts"]) + 1;
                Db.Exec("UPDATE dbo.AppUser SET FailedAttempts = @f, LockedUntil = @l WHERE UserId = @id",
                    Db.P("@f", fails >= max ? 0 : fails),
                    Db.PTime("@l", fails >= max ? DateTime.Now.AddMinutes(15) : (DateTime?)null),
                    Db.P("@id", userId));
                Db.Exec("INSERT INTO dbo.AuditLog (CompanyId, UserId, UserName, Action, EntityName, EntityId, IpAddress) VALUES (@c, @u, @n, 'LOGIN_FAILED', 'AppUser', @u, @ip)",
                    Db.P("@c", r["CompanyId"]), Db.P("@u", userId), Db.P("@n", name), Db.P("@ip", Request.UserHostAddress));
                Show(generic);
                return;
            }

            var role = Db.Str(r["Role"]);
            if (role != Roles.SuperAdmin)
            {
                if (r["CompanyActive"] != DBNull.Value && !Convert.ToBoolean(r["CompanyActive"])) { Show("Your company account is inactive."); return; }
                var sub = Db.Str(r["SubStatus"]);
                var subEnd = Db.NDate(r["SubEnd"]);
                if (sub == "Suspended" || sub == "Expired" || (subEnd.HasValue && subEnd.Value < DateTime.Today))
                { Show("Your company's subscription is not active. Please contact the system administrator."); return; }
            }

            Db.Exec("UPDATE dbo.AppUser SET FailedAttempts = 0, LockedUntil = NULL, LastLoginOn = SYSDATETIME() WHERE UserId = @id", Db.P("@id", userId));

            var info = new UserInfo
            {
                UserId = userId,
                UserName = Db.Str(r["UserName"]),
                FullName = Db.Str(r["FullName"]),
                Role = role,
                CompanyId = Db.NInt(r["CompanyId"]),
                MustChangePassword = Convert.ToBoolean(r["MustChangePassword"])
            };
            AuthCookie.SignIn(info, Response);
            Db.Exec("INSERT INTO dbo.AuditLog (CompanyId, UserId, UserName, Action, EntityName, EntityId, IpAddress) VALUES (@c, @u, @n, 'LOGIN', 'AppUser', @u, @ip)",
                Db.P("@c", r["CompanyId"]), Db.P("@u", userId), Db.P("@n", info.UserName), Db.P("@ip", Request.UserHostAddress));

            if (info.MustChangePassword) { Response.Redirect("~/ChangePassword.aspx", false); return; }
            var ret = Request.QueryString["ReturnUrl"];
            if (!string.IsNullOrEmpty(ret) && ret.StartsWith("/") && !ret.StartsWith("//")) Response.Redirect(ret, false);
            else Response.Redirect("~/Default.aspx", false);
        }

        void Show(string msg)
        {
            pnlError.Visible = true;
            litError.Text = HttpUtility.HtmlEncode(msg);
        }
    }
}
