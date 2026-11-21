<%@ Page Title="Access denied" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" Inherits="PoultryERP.BasePage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <div class="card"><div class="empty-state">
        <i class="bi bi-shield-lock text-danger"></i>
        <h5 class="fw-bold">You don't have access to this page</h5>
        <p class="mb-3">Your role is <b><%: PoultryERP.Roles.Display(PoultryERP.Ctx.Role) %></b>. Ask your administrator if you need this screen.</p>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/Default.aspx") %>">Back to dashboard</a>
    </div></div>
</asp:Content>
