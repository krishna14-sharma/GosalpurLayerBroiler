<%@ Application Language="C#" %>
<%@ Import Namespace="System.Web.Security" %>
<%@ Import Namespace="System.Security.Principal" %>
<script runat="server">
    void Application_PostAuthenticateRequest(object sender, EventArgs e)
    {
        // Attach the role carried in the forms ticket so User.IsInRole() works everywhere.
        var ctx = HttpContext.Current;
        if (ctx.User == null || !ctx.User.Identity.IsAuthenticated) return;
        var fi = ctx.User.Identity as FormsIdentity;
        if (fi == null) return;
        var info = PoultryERP.UserInfo.Parse(fi.Name, fi.Ticket.UserData);
        if (info == null)
        {
            FormsAuthentication.SignOut();
            return;
        }
        ctx.User = new GenericPrincipal(fi, new[] { info.Role });
    }

    void Application_Error(object sender, EventArgs e)
    {
        var ex = Server.GetLastError();
        if (ex == null) return;
        try
        {
            var root = ex.GetBaseException();
            System.Diagnostics.Trace.TraceError("Unhandled: " + root);
            PoultryERP.Audit.Log("ERROR", "Application", null,
                (Request.RawUrl ?? "") + " :: " + root.GetType().Name + ": " + root.Message);
        }
        catch { }
    }
</script>
