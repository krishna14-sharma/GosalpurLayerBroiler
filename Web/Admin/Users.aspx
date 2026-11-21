<%@ Page Title="Users & Shed Access" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="Users.aspx.cs" Inherits="PoultryERP.UsersPage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:HiddenField ID="hidCmd" runat="server" />
            <asp:HiddenField ID="hidId" runat="server" />
            <asp:Button ID="btnCmd" runat="server" OnClick="btnCmd_Click" style="display:none" UseSubmitBehavior="false" />
            <div class="row g-3">
                <div class="col-xl-5">
                    <asp:Panel ID="pnlForm" runat="server" CssClass="card" DefaultButton="btnSave">
                        <div class="card-header"><asp:Literal ID="litTitle" runat="server" Text="New user" /></div>
                        <div class="card-body">
                            <asp:Panel ID="pnlErr" runat="server" CssClass="alert alert-danger py-2 small" Visible="false"><asp:Literal ID="litErr" runat="server" /></asp:Panel>
                            <div class="row g-2">
                                <div class="col-6"><label class="form-label req">User name</label><asp:TextBox ID="txtUser" runat="server" CssClass="form-control" MaxLength="50" autocomplete="off" /></div>
                                <div class="col-6"><label class="form-label req">Full name</label><asp:TextBox ID="txtName" runat="server" CssClass="form-control" MaxLength="100" /></div>
                                <div class="col-6"><label class="form-label">Email</label><asp:TextBox ID="txtEmail" runat="server" CssClass="form-control" MaxLength="150" /></div>
                                <div class="col-6"><label class="form-label">Phone</label><asp:TextBox ID="txtPhone" runat="server" CssClass="form-control" MaxLength="40" /></div>
                                <div class="col-6"><label class="form-label req">Role</label>
                                    <asp:DropDownList ID="ddlRole" runat="server" CssClass="form-select" AutoPostBack="true" OnSelectedIndexChanged="Role_Changed" /></div>
                                <div class="col-6"><label class="form-label req">Company</label>
                                    <asp:DropDownList ID="ddlCompany" runat="server" CssClass="form-select" AutoPostBack="true" OnSelectedIndexChanged="Role_Changed" /></div>
                                <div class="col-6"><label class="form-label"><asp:Literal ID="litPw" runat="server" Text="Password" /></label>
                                    <asp:TextBox ID="txtPw" runat="server" CssClass="form-control" TextMode="Password" autocomplete="new-password" /></div>
                                <div class="col-6 d-flex flex-column justify-content-end">
                                    <div class="form-check"><asp:CheckBox ID="chkMust" runat="server" Checked="true" Text="Must change at next login" /></div>
                                    <div class="form-check"><asp:CheckBox ID="chkActive" runat="server" Checked="true" Text="Active" /></div>
                                </div>
                            </div>
                            <asp:Panel ID="pnlSheds" runat="server" CssClass="mt-3">
                                <div class="form-section-title"><i class="bi bi-house-door"></i>Assigned sheds (supervisor data entry)</div>
                                <asp:CheckBoxList ID="cblSheds" runat="server" RepeatLayout="Flow" RepeatDirection="Horizontal" CssClass="d-flex flex-wrap gap-3 small" />
                            </asp:Panel>
                            <div class="d-flex gap-2 mt-3">
                                <asp:LinkButton ID="btnSave" runat="server" CssClass="btn btn-primary" OnClick="btnSave_Click"><i class="bi bi-check2 me-1"></i>Save user</asp:LinkButton>
                                <asp:LinkButton ID="btnNew" runat="server" CssClass="btn btn-light border" OnClick="btnNew_Click">New</asp:LinkButton>
                            </div>
                        </div>
                    </asp:Panel>
                </div>
                <div class="col-xl-7">
                    <div class="card">
                        <div class="card-header">Users</div>
                        <div class="table-responsive"><asp:Literal ID="litList" runat="server" Mode="PassThrough" /></div>
                    </div>
                </div>
            </div>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
<asp:Content ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        window.usrCmd = function (cmd, msg) {
            if (msg && !confirm(msg)) return false;
            document.getElementById('<%= hidCmd.ClientID %>').value = cmd;
            document.getElementById('<%= btnCmd.ClientID %>').click(); return false;
        };
    </script>
</asp:Content>
