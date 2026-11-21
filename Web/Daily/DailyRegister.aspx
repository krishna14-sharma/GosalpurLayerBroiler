<%@ Page Title="Daily Log Register" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="DailyRegister.aspx.cs" Inherits="PoultryERP.DailyRegisterPage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:HiddenField ID="hidPage" runat="server" Value="1" />
            <asp:HiddenField ID="hidCmd" runat="server" />
            <asp:Button ID="btnCmd" runat="server" OnClick="btnCmd_Click" style="display:none" UseSubmitBehavior="false" />
            <div class="card mb-3"><div class="card-body pb-2">
                <div class="row g-2 align-items-end">
                    <div class="col-6 col-md-2"><label class="form-label">From</label><asp:TextBox ID="txtFrom" runat="server" TextMode="Date" CssClass="form-control form-control-sm" /></div>
                    <div class="col-6 col-md-2"><label class="form-label">To</label><asp:TextBox ID="txtTo" runat="server" TextMode="Date" CssClass="form-control form-control-sm" /></div>
                    <div class="col-6 col-md-3"><label class="form-label">Farm</label><asp:DropDownList ID="ddlFarm" runat="server" CssClass="form-select form-select-sm" /></div>
                    <div class="col-6 col-md-3"><label class="form-label">Shed</label><asp:DropDownList ID="ddlShed" runat="server" CssClass="form-select form-select-sm" /></div>
                    <div class="col-12 col-md-2"><asp:LinkButton ID="btnShow" runat="server" CssClass="btn btn-primary btn-sm w-100" OnClick="btnShow_Click"><i class="bi bi-funnel me-1"></i>Show</asp:LinkButton></div>
                </div>
            </div></div>
            <div class="card">
                <div class="card-header d-flex justify-content-between"><span>Entries</span><span class="sub"><asp:Literal ID="litCount" runat="server" /></span></div>
                <div class="table-responsive"><asp:Literal ID="litList" runat="server" Mode="PassThrough" /></div>
                <asp:Literal ID="litPager" runat="server" Mode="PassThrough" />
            </div>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
<asp:Content ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        window.regPage = function (p) {
            document.getElementById('<%= hidCmd.ClientID %>').value = 'page:' + p;
            document.getElementById('<%= btnCmd.ClientID %>').click(); return false;
        };
    </script>
</asp:Content>
