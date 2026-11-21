<%@ Page Language="C#" AutoEventWireup="true" %>
<%@ Import Namespace="System.Web.Security" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e)
    {
        if (User.Identity.IsAuthenticated) PoultryERP.Audit.Log("LOGOUT", "AppUser", PoultryERP.Ctx.UserId, null);
        FormsAuthentication.SignOut();
        if (Session != null) Session.Abandon();
        Response.Redirect("~/Login.aspx");
    }
</script>
