<%@ Page Title="Change password" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="ChangePassword.aspx.cs" Inherits="PoultryERP.ChangePasswordPage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <div class="row justify-content-center"><div class="col-md-7 col-lg-5">
        <div class="card">
            <div class="card-header">Change password</div>
            <div class="card-body">
                <asp:Panel ID="pnlForce" runat="server" Visible="false" CssClass="alert alert-warning small py-2">
                    <i class="bi bi-info-circle me-1"></i>Please set your own password before continuing.
                </asp:Panel>
                <asp:Panel ID="pnlMsg" runat="server" Visible="false" CssClass="alert py-2 small"><asp:Literal ID="litMsg" runat="server" /></asp:Panel>
                <div class="mb-3"><label class="form-label req">Current password</label>
                    <asp:TextBox ID="txtOld" runat="server" TextMode="Password" CssClass="form-control" autocomplete="current-password" /></div>
                <div class="mb-3"><label class="form-label req">New password</label>
                    <asp:TextBox ID="txtNew" runat="server" TextMode="Password" CssClass="form-control" autocomplete="new-password" />
                    <div class="form-text">At least 8 characters with upper-case, lower-case and a digit.</div></div>
                <div class="mb-3"><label class="form-label req">Confirm new password</label>
                    <asp:TextBox ID="txtConfirm" runat="server" TextMode="Password" CssClass="form-control" autocomplete="new-password" /></div>
                <asp:Button ID="btnSave" runat="server" Text="Update password" CssClass="btn btn-primary" OnClick="btnSave_Click" />
            </div>
        </div>
    </div></div>
</asp:Content>
