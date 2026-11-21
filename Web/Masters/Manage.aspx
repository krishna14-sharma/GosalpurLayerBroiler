<%@ Page Title="Manage" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="Manage.aspx.cs" Inherits="PoultryERP.ManagePage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:HiddenField ID="hidId" runat="server" />
            <asp:HiddenField ID="hidCmd" runat="server" />
            <asp:Button ID="btnCmd" runat="server" OnClick="btnCmd_Click" style="display:none" UseSubmitBehavior="false" CausesValidation="false" />

            <p class="text-muted-2 small mb-3"><asp:Literal ID="litDesc" runat="server" /></p>

            <asp:Panel ID="pnlForm" runat="server" CssClass="card mb-3" DefaultButton="btnSave">
                <div class="card-header d-flex justify-content-between align-items-center">
                    <span><i class="bi bi-pencil-square me-1"></i><asp:Literal ID="litFormTitle" runat="server" /></span>
                    <asp:LinkButton ID="btnNew" runat="server" CssClass="btn btn-sm btn-light border" OnClick="btnNew_Click" CausesValidation="false"><i class="bi bi-plus-lg"></i> New</asp:LinkButton>
                </div>
                <div class="card-body">
                    <asp:Panel ID="pnlErr" runat="server" Visible="false" CssClass="alert alert-danger py-2 small"><asp:Literal ID="litErr" runat="server" /></asp:Panel>
                    <div class="row g-3"><asp:PlaceHolder ID="phFields" runat="server" /></div>
                    <div class="d-flex flex-wrap gap-2 mt-3">
                        <asp:LinkButton ID="btnSave" runat="server" CssClass="btn btn-primary" OnClick="btnSave_Click"><i class="bi bi-check2 me-1"></i>Save</asp:LinkButton>
                        <asp:LinkButton ID="btnCancel" runat="server" CssClass="btn btn-light border" OnClick="btnNew_Click" CausesValidation="false">Cancel</asp:LinkButton>
                    </div>
                </div>
            </asp:Panel>

            <div class="card">
                <div class="card-header d-flex flex-wrap gap-2 justify-content-between align-items-center">
                    <span><asp:Literal ID="litListTitle" runat="server" /> <span class="sub ms-1"><asp:Literal ID="litCount" runat="server" /></span></span>
                    <asp:Panel ID="pnlSearch" runat="server" CssClass="d-flex gap-2" DefaultButton="btnSearch">
                        <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control form-control-sm" placeholder="Search..." MaxLength="60" />
                        <asp:LinkButton ID="btnSearch" runat="server" CssClass="btn btn-sm btn-outline-primary" OnClick="btnSearch_Click" CausesValidation="false"><i class="bi bi-search"></i></asp:LinkButton>
                    </asp:Panel>
                </div>
                <div class="table-responsive"><asp:Literal ID="litList" runat="server" Mode="PassThrough" /></div>
                <asp:Literal ID="litPager" runat="server" Mode="PassThrough" />
            </div>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
<asp:Content ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        window.crudCmd = function (cmd, confirmText) {
            if (confirmText && !window.confirm(confirmText)) return false;
            document.getElementById('<%= hidCmd.ClientID %>').value = cmd;
            document.getElementById('<%= btnCmd.ClientID %>').click();
            return false;
        };
        // Sales: eggs come from the farm's egg godown, so the shed box only applies to cull birds
        (function () {
            var eggs = ['Table Egg', 'Cracked Egg', 'Double Yolk', 'Misshapen Egg'];
            function sync() {
                var p = document.querySelector('select[id$="_f_Product"]'), s = document.querySelector('select[id$="_f_ShedId"]');
                if (!p || !s) return;
                var egg = eggs.indexOf(p.value) >= 0, box = s.closest('[class*="col"]') || s.parentNode;
                if (egg) s.value = '';
                s.disabled = egg;
                box.style.opacity = egg ? '.45' : '';
            }
            document.addEventListener('change', function (e) { if (e.target && /_f_Product$/.test(e.target.id)) sync(); });
            window.addEventListener('load', function () {
                sync();
                if (window.Sys && Sys.WebForms) Sys.WebForms.PageRequestManager.getInstance().add_endRequest(sync);   // after New / Edit (partial postback)
            });
        })();
    </script>
</asp:Content>
