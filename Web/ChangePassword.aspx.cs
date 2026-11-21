using System;

namespace PoultryERP
{
    public partial class ChangePasswordPage : BasePage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            pnlForce.Visible = Ctx.User.MustChangePassword;
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            var row = Db.Row("SELECT PasswordHash FROM dbo.AppUser WHERE UserId = @id", Db.P("@id", Ctx.UserId));
            if (row == null || !PasswordHasher.Verify(txtOld.Text, Db.Str(row["PasswordHash"]))) { Msg("Current password is incorrect.", false); return; }
            if (txtNew.Text != txtConfirm.Text) { Msg("New password and confirmation do not match.", false); return; }
            if (txtNew.Text == txtOld.Text) { Msg("Choose a password different from the current one.", false); return; }
            var err = PasswordHasher.StrengthError(txtNew.Text);
            if (err != null) { Msg(err, false); return; }

            Db.Exec("UPDATE dbo.AppUser SET PasswordHash = @h, MustChangePassword = 0 WHERE UserId = @id",
                Db.P("@h", PasswordHasher.Hash(txtNew.Text)), Db.P("@id", Ctx.UserId));
            Audit.Log("PASSWORD_CHANGE", "AppUser", Ctx.UserId, null);

            var u = Ctx.User;
            u.MustChangePassword = false;
            AuthCookie.SignIn(u, Response);
            Response.Redirect("~/Default.aspx", false);
        }

        void Msg(string m, bool ok)
        {
            pnlMsg.Visible = true;
            pnlMsg.CssClass = "alert py-2 small " + (ok ? "alert-success" : "alert-danger");
            litMsg.Text = Ui.H(m);
        }
    }
}
