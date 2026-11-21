using System.Collections.Generic;
using System.Text;
using System.Web;

namespace PoultryERP
{
    public class NavItem
    {
        public string Title, Url, Icon;
        public string[] Roles;
        public NavItem(string title, string url, string icon, params string[] roles)
        { Title = title; Url = url; Icon = icon; Roles = roles; }
    }

    public class NavSection
    {
        public string Title;
        public List<NavItem> Items = new List<NavItem>();
        public NavSection(string title) { Title = title; }
        public NavSection Add(string title, string url, string icon, params string[] roles)
        { Items.Add(new NavItem(title, url, icon, roles)); return this; }
    }

    /// <summary>Single source of truth for the menu. Page-level checks live in each page's AllowedRoles.</summary>
    public static class Nav
    {
        const string SA = Roles.SuperAdmin, FM = Roles.FarmManager, SUP = Roles.Supervisor, VET = Roles.Veterinarian, ACC = Roles.Accountant;

        public static List<NavSection> Build()
        {
            var list = new List<NavSection>();
            list.Add(new NavSection("")
                .Add("Dashboard", "~/Default.aspx", "speedometer2"));

            list.Add(new NavSection("Daily Operations")
                .Add("Daily Shed Entry", "~/Daily/DailyEntry.aspx", "journal-check", SA, FM, SUP)
                .Add("Daily Log Register", "~/Daily/DailyRegister.aspx", "table", SA, FM, SUP, VET, ACC)
                .Add("Flock Transfers", "~/Flock/Transfers.aspx", "arrow-left-right", SA, FM, SUP)
                .Add("Flocks & Placement", "~/Flock/Flocks.aspx", "egg", SA, FM, VET, ACC)
                .Add("Broiler Sales / Lifting", "~/Broiler/Lifting.aspx", "truck", SA, FM, SUP, ACC));

            list.Add(new NavSection("Health")
                .Add("Medication & Vaccination", "~/Health/Medication.aspx", "capsule", SA, FM, SUP, VET)
                .Add("Vaccination Schedule", "~/Masters/Manage.aspx?e=vaccine-schedule", "calendar2-week", SA, FM, VET)
                .Add("Post-Mortem Notes", "~/Masters/Manage.aspx?e=postmortem", "clipboard2-pulse", SA, FM, VET));

            list.Add(new NavSection("Finance")
                .Add("Item Prices (Feed / Grit / Meds)", "~/Masters/Manage.aspx?e=item-rates", "currency-rupee", SA, FM, ACC)
                .Add("Overheads & Labour", "~/Masters/Manage.aspx?e=overheads", "cash-stack", SA, FM, ACC)
                .Add("Sales (Eggs / Birds)", "~/Finance/SaleInvoice.aspx", "cart-check", SA, FM, ACC)
                .Add("Egg Wastage", "~/Masters/Manage.aspx?e=egg-wastage", "trash3", SA, FM, ACC)
                .Add("Stock Receipts", "~/Masters/Manage.aspx?e=stock-receipts", "box-seam", SA, FM, ACC));

            var rep = new NavSection("Reports");
            foreach (var d in PoultryERP.Reports.ReportCatalog.All)
                rep.Add(d.Title, "~/Reports/Report.aspx?r=" + d.Key, d.Icon, d.Roles);
            list.Add(rep);

            list.Add(new NavSection("Masters")
                .Add("Farms", "~/Masters/Manage.aspx?e=farms", "geo-alt", SA, FM)
                .Add("Locations / Blocks", "~/Masters/Manage.aspx?e=locations", "signpost-split", SA, FM)
                .Add("Sheds", "~/Masters/Manage.aspx?e=sheds", "house-door", SA, FM)
                .Add("Breeds", "~/Masters/Manage.aspx?e=breeds", "bookmark-star", SA, FM, VET)
                .Add("Breed Standards", "~/Masters/Manage.aspx?e=breed-standards", "graph-up", SA, FM, VET)
                .Add("Items", "~/Masters/Manage.aspx?e=items", "boxes", SA, FM, ACC)
                .Add("Suppliers / Hatcheries", "~/Masters/Manage.aspx?e=suppliers", "truck", SA, FM, ACC)
                .Add("Customers", "~/Masters/Manage.aspx?e=customers", "people", SA, FM, ACC));

            list.Add(new NavSection("Administration")
                .Add("Users & Shed Access", "~/Admin/Users.aspx", "person-gear", SA)
                .Add("Opening Balances (Go-Live)", "~/Admin/OpeningBalance.aspx", "flag", SA, FM)
                .Add("Edit Overrides", "~/Admin/Overrides.aspx", "unlock", SA, FM)
                .Add("Company Settings", "~/Masters/Manage.aspx?e=company-settings", "sliders", SA, FM)
                .Add("Companies", "~/Masters/Manage.aspx?e=companies", "buildings", SA)
                .Add("Subscriptions & Billing", "~/Masters/Manage.aspx?e=subscriptions", "receipt", SA)
                .Add("Audit Log", "~/Admin/AuditLog.aspx", "shield-check", SA));
            return list;
        }

        public static string RenderHtml(string currentPathAndQuery)
        {
            var sb = new StringBuilder();
            var cur = (currentPathAndQuery ?? "").ToLowerInvariant();
            foreach (var sec in Build())
            {
                var visible = new List<NavItem>();
                foreach (var it in sec.Items)
                    if (it.Roles == null || it.Roles.Length == 0 || Ctx.InRole(it.Roles)) visible.Add(it);
                if (visible.Count == 0) continue;
                if (sec.Title.Length > 0)
                    sb.Append("<div class=\"nav-section\">").Append(HttpUtility.HtmlEncode(sec.Title)).Append("</div>");
                foreach (var it in visible)
                {
                    var url = VirtualPathUtility.ToAbsolute(it.Url.Split('?')[0]) + (it.Url.Contains("?") ? "?" + it.Url.Split('?')[1] : "");
                    bool active = cur == url.ToLowerInvariant();
                    sb.Append("<a class=\"nav-link").Append(active ? " active" : "").Append("\" href=\"")
                      .Append(HttpUtility.HtmlAttributeEncode(url)).Append("\"><i class=\"bi bi-").Append(it.Icon)
                      .Append("\"></i><span>").Append(HttpUtility.HtmlEncode(it.Title)).Append("</span></a>");
                }
            }
            return sb.ToString();
        }
    }
}
