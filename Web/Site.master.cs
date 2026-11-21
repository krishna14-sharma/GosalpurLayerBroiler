using System;
using System.Web.UI;

namespace PoultryERP
{
    public partial class SiteMaster : MasterPage
    {
        protected string PageHeading = "";

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            PageHeading = Page.Title;
            Page.Title = (string.IsNullOrEmpty(Page.Title) ? "" : Page.Title + " | ") + "Phoenix Poultry ERP";
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            var u = Ctx.User;
            if (u == null) return;

            litNav.Text = Nav.RenderHtml(Request.Url.PathAndQuery);
            litUser.Text = Ui.H(u.FullName);
            litRole.Text = Ui.H(Roles.Display(u.Role));
            var parts = (u.FullName ?? u.UserName).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            litInitials.Text = Ui.H(parts.Length > 1 ? (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)) : (parts.Length == 1 ? parts[0].Substring(0, 1) : "?"));

            if (Ctx.IsSuperAdmin)
            {
                phCompany.Visible = true;
                if (!IsPostBack)
                {
                    Ui.Bind(ddlCompany, "SELECT CompanyId AS Value, Code + ' - ' + Name AS Text FROM dbo.Company WHERE IsActive = 1 ORDER BY Code", null);
                    Ui.Select(ddlCompany, Ctx.CompanyId);
                }
            }
            else
            {
                litCompanyName.Text = "<span class=\"badge text-bg-light border d-none d-md-inline\">" + Ui.H(Ctx.Company.Name) + "</span>";
            }
        }

        protected void ddlCompany_SelectedIndexChanged(object sender, EventArgs e)
        {
            int id;
            if (int.TryParse(ddlCompany.SelectedValue, out id))
            {
                Ctx.CompanyId = id;
                Audit.Log("SWITCH_COMPANY", "Company", id, null);
            }
            // reload the current page cleanly with the new company context
            Response.Redirect(Request.Url.AbsolutePath + (Request.Url.Query.Contains("e=") || Request.Url.Query.Contains("r=") ? Request.Url.Query : ""), false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
