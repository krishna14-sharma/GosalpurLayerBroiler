<%@ Page Title="Audit Log" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="AuditLog.aspx.cs" Inherits="PoultryERP.AuditLogPage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:HiddenField ID="hidPage" runat="server" Value="1" />
            <asp:HiddenField ID="hidCmd" runat="server" />
            <asp:Button ID="btnCmd" runat="server" OnClick="btnCmd_Click" style="display:none" UseSubmitBehavior="false" />
            <div class="card mb-3"><div class="card-body pb-2"><div class="row g-2 align-items-end">
                <div class="col-6 col-md-2"><label class="form-label">From</label><asp:TextBox ID="txtFrom" runat="server" TextMode="Date" CssClass="form-control form-control-sm" /></div>
                <div class="col-6 col-md-2"><label class="form-label">To</label><asp:TextBox ID="txtTo" runat="server" TextMode="Date" CssClass="form-control form-control-sm" /></div>
                <div class="col-6 col-md-2"><label class="form-label">User</label><asp:TextBox ID="txtUser" runat="server" CssClass="form-control form-control-sm" /></div>
                <div class="col-6 col-md-2"><label class="form-label">Action</label><asp:DropDownList ID="ddlAction" runat="server" CssClass="form-select form-select-sm" /></div>
                <div class="col-6 col-md-2"><label class="form-label">Entity</label><asp:TextBox ID="txtEntity" runat="server" CssClass="form-control form-control-sm" placeholder="e.g. DailyLog" /></div>
                <div class="col-6 col-md-2"><div class="form-check mb-1"><asp:CheckBox ID="chkAll" runat="server" Text="All companies" /></div>
                    <asp:LinkButton ID="btnShow" runat="server" CssClass="btn btn-primary btn-sm w-100" OnClick="btnShow_Click"><i class="bi bi-funnel me-1"></i>Show</asp:LinkButton></div>
            </div></div></div>
            <div class="card">
                <div class="card-header d-flex justify-content-between"><span>Audit trail</span><span class="sub"><asp:Literal ID="litCount" runat="server" /></span></div>
                <div class="table-responsive"><asp:Literal ID="litList" runat="server" Mode="PassThrough" /></div>
                <asp:Literal ID="litPager" runat="server" Mode="PassThrough" />
            </div>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
<asp:Content ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        window.audPage = function (p) {
            document.getElementById('<%= hidCmd.ClientID %>').value = 'page:' + p;
            document.getElementById('<%= btnCmd.ClientID %>').click(); return false;
        };
    </script>
</asp:Content>
