using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace PoultryERP
{
    /// <summary>
    /// Base class for every secured page: enforces login, forced password change,
    /// role-based access and CSRF protection (ViewStateUserKey).
    /// </summary>
    public class BasePage : Page
    {
        /// <summary>Roles allowed to open the page; null = any signed-in user.</summary>
        protected virtual string[] AllowedRoles { get { return null; } }

        protected override void OnInit(EventArgs e)
        {
            var u = Ctx.User;
            // Response.Redirect(url, true) ends the request so no page code or postback event can run.
            if (u == null)
            {
                FormsAuthentication.RedirectToLoginPage();
                Response.End();
                return;
            }
            if (Session != null) ViewStateUserKey = Session.SessionID;

            if (u.MustChangePassword && !Request.Path.EndsWith("ChangePassword.aspx", StringComparison.OrdinalIgnoreCase))
            {
                Response.Redirect("~/ChangePassword.aspx", true);
                return;
            }
            if (AllowedRoles != null && !Ctx.InRole(AllowedRoles))
            {
                Response.Redirect("~/AccessDenied.aspx", true);
                return;
            }
            base.OnInit(e);
        }

        protected CompanySettings Company { get { return Ctx.Company; } }

        /// <summary>Shows a Bootstrap toast; works for full and partial (UpdatePanel) postbacks.</summary>
        protected void Toast(string message, string type)
        {
            Ui.Toast(this, message, type);
        }

        protected void Ok(string message) { Toast(message, "success"); }
        protected void Fail(string message) { Toast(message, "danger"); }
        protected void Warn(string message) { Toast(message, "warning"); }
    }

    public static class Ui
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

        public static void Toast(Page page, string message, string type)
        {
            var js = "window.App&&App.toast(" + Js(message) + "," + Js(type ?? "info") + ");";
            ScriptManager.RegisterStartupScript(page, page.GetType(), "toast" + Guid.NewGuid().ToString("N"), js, true);
        }

        public static void Script(Page page, string js)
        {
            ScriptManager.RegisterStartupScript(page, page.GetType(), "js" + Guid.NewGuid().ToString("N"), js, true);
        }

        public static string Js(string s)
        {
            return HttpUtility.JavaScriptStringEncode(s ?? "", true);
        }

        public static string H(object o) { return HttpUtility.HtmlEncode(Convert.ToString(o)); }

        // ---------- dropdowns ----------
        public static void Bind(DropDownList ddl, DataTable dt, string valueField, string textField, string emptyText)
        {
            string keep = ddl.SelectedValue;
            ddl.Items.Clear();
            if (emptyText != null) ddl.Items.Add(new ListItem(emptyText, ""));
            foreach (DataRow r in dt.Rows)
                ddl.Items.Add(new ListItem(Convert.ToString(r[textField]), Convert.ToString(r[valueField])));
            if (!string.IsNullOrEmpty(keep) && ddl.Items.FindByValue(keep) != null) ddl.SelectedValue = keep;
        }

        public static void Bind(DropDownList ddl, string sql, string emptyText, params System.Data.SqlClient.SqlParameter[] ps)
        {
            Bind(ddl, Db.Query(sql, ps), "Value", "Text", emptyText);
        }

        public static void BindList(DropDownList ddl, IEnumerable<string> values, string emptyText)
        {
            string keep = ddl.SelectedValue;
            ddl.Items.Clear();
            if (emptyText != null) ddl.Items.Add(new ListItem(emptyText, ""));
            foreach (var v in values) ddl.Items.Add(new ListItem(v, v));
            if (!string.IsNullOrEmpty(keep) && ddl.Items.FindByValue(keep) != null) ddl.SelectedValue = keep;
        }

        public static void Select(DropDownList ddl, object value)
        {
            var v = value == null || value == DBNull.Value ? "" : Convert.ToString(value, Inv);
            ddl.ClearSelection();
            var it = ddl.Items.FindByValue(v);
            if (it != null) it.Selected = true;
        }

        public static int? SelInt(DropDownList ddl)
        {
            int v;
            return int.TryParse(ddl.SelectedValue, out v) ? v : (int?)null;
        }

        // ---------- parsing (accepts 1,234.5 and 1234.5) ----------
        public static decimal? Dec(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            decimal d;
            s = s.Trim().Replace(",", "");
            return decimal.TryParse(s, NumberStyles.Number, Inv, out d) ? d : (decimal?)null;
        }

        public static int? Int(string s)
        {
            var d = Dec(s);
            if (!d.HasValue) return null;
            if (d.Value != Math.Truncate(d.Value)) return null;
            if (d.Value > int.MaxValue || d.Value < int.MinValue) return null;
            return (int)d.Value;
        }

        public static DateTime? Date(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            DateTime d;
            string[] fmts = { "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "d/M/yyyy", "d-M-yyyy" };
            return DateTime.TryParseExact(s.Trim(), fmts, Inv, DateTimeStyles.None, out d) ? d : (DateTime?)null;
        }

        public static TimeSpan? Time(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            TimeSpan t;
            return TimeSpan.TryParseExact(s.Trim(), new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" }, Inv, out t) ? t : (TimeSpan?)null;
        }

        /// <summary>Value for an &lt;input type=date&gt;.</summary>
        public static string DateValue(object o)
        {
            if (o == null || o == DBNull.Value) return "";
            return Convert.ToDateTime(o).ToString("yyyy-MM-dd", Inv);
        }

        public static string NumValue(object o)
        {
            if (o == null || o == DBNull.Value) return "";
            var d = Convert.ToDecimal(o);
            return d.ToString("0.####", Inv);
        }

        public static string TimeValue(object o)
        {
            if (o == null || o == DBNull.Value) return "";
            if (o is TimeSpan) return ((TimeSpan)o).ToString(@"hh\:mm", Inv);
            return Convert.ToString(o);
        }

        // ---------- display ----------
        public static string D(object o) { return o == null || o == DBNull.Value ? "" : Convert.ToDateTime(o).ToString("dd-MM-yyyy", Inv); }
        public static string N0(object o) { return o == null || o == DBNull.Value ? "" : Convert.ToDecimal(o).ToString("#,##0", India); }
        public static string N1(object o) { return o == null || o == DBNull.Value ? "" : Convert.ToDecimal(o).ToString("#,##0.0", India); }
        public static string N2(object o) { return o == null || o == DBNull.Value ? "" : Convert.ToDecimal(o).ToString("#,##0.00", India); }

        public static DateTime Today { get { return DateTime.Today; } }

        public static string StatusBadge(string status)
        {
            string cls;
            switch (status)
            {
                case "Approved": case "Done": case "Active": cls = "success"; break;
                case "Pending": case "Planned": case "Cleaning": case "Trial": cls = "warning"; break;
                case "Rejected": case "Skipped": case "Quarantine": case "Suspended": case "Expired": cls = "danger"; break;
                case "Vacant": case "Depleted": cls = "secondary"; break;
                default: cls = "info"; break;
            }
            return "<span class=\"badge rounded-pill text-bg-" + cls + "\">" + H(status) + "</span>";
        }
    }
}
