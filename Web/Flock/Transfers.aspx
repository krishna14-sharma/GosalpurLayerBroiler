<%@ Page Title="Flock Transfers / Shifting" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="Transfers.aspx.cs" Inherits="PoultryERP.TransfersPage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:HiddenField ID="hidCmd" runat="server" />
            <asp:Button ID="btnCmd" runat="server" OnClick="btnCmd_Click" style="display:none" UseSubmitBehavior="false" />
            <div class="row g-3">
                <div class="col-xl-5">
                    <asp:Panel ID="pnlForm" runat="server" CssClass="card" DefaultButton="btnSubmit">
                        <div class="card-header"><i class="bi bi-arrow-left-right me-1"></i>New shifting</div>
                        <div class="card-body">
                            <asp:Panel ID="pnlErr" runat="server" CssClass="alert alert-danger py-2 small" Visible="false"><asp:Literal ID="litErr" runat="server" /></asp:Panel>
                            <div class="row g-2">
                                <div class="col-6"><label class="form-label req">Transfer date</label>
                                    <asp:TextBox ID="txtDate" runat="server" TextMode="Date" CssClass="form-control" AutoPostBack="true" OnTextChanged="Source_Changed" /></div>
                                <div class="col-6"><label class="form-label req">Flock</label>
                                    <asp:DropDownList ID="ddlFlock" runat="server" CssClass="form-select" AutoPostBack="true" OnSelectedIndexChanged="Source_Changed" /></div>
                                <div class="col-12"><label class="form-label req">From shed</label>
                                    <asp:DropDownList ID="ddlFrom" runat="server" CssClass="form-select" AutoPostBack="true" OnSelectedIndexChanged="From_Changed" /></div>
                                <div class="col-12"><asp:Literal ID="litInfo" runat="server" /></div>
                                <div class="col-12"><label class="form-label req">To shed</label>
                                    <asp:DropDownList ID="ddlTo" runat="server" CssClass="form-select" /></div>
                                <div class="col-12"><div class="form-check form-switch">
                                    <asp:CheckBox ID="chkFull" runat="server" AutoPostBack="true" OnCheckedChanged="From_Changed" Text="Full transfer (empty the source shed)" /></div></div>
                                <div class="col-4"><label class="form-label req">Birds dispatched</label><asp:TextBox ID="txtCount" runat="server" CssClass="form-control text-end" inputmode="numeric" /></div>
                                <div class="col-4"><label class="form-label">Transit mortality</label><asp:TextBox ID="txtTMort" runat="server" CssClass="form-control text-end" inputmode="numeric" Text="0" /></div>
                                <div class="col-4"><label class="form-label">Transit culls</label><asp:TextBox ID="txtTCull" runat="server" CssClass="form-control text-end" inputmode="numeric" Text="0" /></div>
                                <div class="col-12"><label class="form-label">Remarks</label><asp:TextBox ID="txtRemarks" runat="server" CssClass="form-control" MaxLength="400" /></div>
                                <asp:PlaceHolder ID="phApproveNow" runat="server">
                                    <div class="col-12"><div class="form-check"><asp:CheckBox ID="chkApproveNow" runat="server" Checked="true" Text="Approve immediately (manager)" /></div></div>
                                </asp:PlaceHolder>
                            </div>
                            <asp:LinkButton ID="btnSubmit" runat="server" CssClass="btn btn-primary mt-3" OnClick="btnSubmit_Click"><i class="bi bi-send me-1"></i>Submit transfer</asp:LinkButton>
                        </div>
                    </asp:Panel>
                </div>
                <div class="col-xl-7">
                    <div class="card mb-3">
                        <div class="card-header d-flex justify-content-between align-items-center"><span><i class="bi bi-hourglass-split me-1"></i>Waiting for approval</span>
                            <asp:TextBox ID="txtNote" runat="server" CssClass="form-control form-control-sm" style="max-width:260px" placeholder="Approval / rejection note" MaxLength="400" /></div>
                        <div class="table-responsive"><asp:Literal ID="litPending" runat="server" Mode="PassThrough" /></div>
                    </div>
                    <div class="card">
                        <div class="card-header d-flex justify-content-between"><span>Recent movements</span>
                            <a class="small" href="<%= ResolveUrl("~/Reports/Report.aspx?r=transfers") %>">Full transfer report &rarr;</a></div>
                        <div class="table-responsive"><asp:Literal ID="litHistory" runat="server" Mode="PassThrough" /></div>
                    </div>
                </div>
            </div>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
<asp:Content ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        window.trfCmd = function (cmd, msg) {
            if (msg && !confirm(msg)) return false;
            document.getElementById('<%= hidCmd.ClientID %>').value = cmd;
            document.getElementById('<%= btnCmd.ClientID %>').click(); return false;
        };
    </script>
</asp:Content>
