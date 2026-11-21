<%@ Page Title="Broiler Sales / Lifting" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="Lifting.aspx.cs" Inherits="PoultryERP.LiftingPage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:HiddenField ID="hidCmd" runat="server" />
            <asp:HiddenField ID="hidId" runat="server" />
            <asp:Button ID="btnCmd" runat="server" OnClick="btnCmd_Click" style="display:none" UseSubmitBehavior="false" />
            <p class="text-muted-2 small">Each lifting records birds and live weight sold from a broiler shed. It reduces the shed's bird count automatically.
                When the last birds of a batch are lifted, the batch is closed and the shed is marked Vacant.</p>
            <div class="row g-3">
                <div class="col-xl-4 col-xxl-3">
                    <asp:Panel ID="pnlForm" runat="server" CssClass="card lift-card" DefaultButton="btnSave">
                        <div class="card-header"><asp:Literal ID="litTitle" runat="server" Text="New lifting" /></div>
                        <div class="card-body">
                            <asp:Panel ID="pnlErr" runat="server" CssClass="alert alert-danger py-2 small" Visible="false"><asp:Literal ID="litErr" runat="server" /></asp:Panel>
                            <div class="row g-2">
                                <div class="col-6"><label class="form-label req">Date</label><asp:TextBox ID="txtDate" runat="server" TextMode="Date" CssClass="form-control" AutoPostBack="true" OnTextChanged="Shed_Changed" /></div>
                                <div class="col-6"><label class="form-label">Invoice / DM no.</label><asp:TextBox ID="txtInv" runat="server" CssClass="form-control" MaxLength="40" /></div>
                                <div class="col-12"><label class="form-label req">Broiler shed</label><asp:DropDownList ID="ddlShed" runat="server" CssClass="form-select" AutoPostBack="true" OnSelectedIndexChanged="Shed_Changed" /></div>
                                <div class="col-12"><asp:Literal ID="litInfo" runat="server" /></div>
                                <div class="col-12"><label class="form-label">Customer / trader</label><asp:DropDownList ID="ddlCustomer" runat="server" CssClass="form-select" /></div>
                                <div class="col-6"><label class="form-label req">Birds</label><asp:TextBox ID="txtBirds" runat="server" CssClass="form-control text-end lcalc" inputmode="numeric" /></div>
                                <div class="col-6"><label class="form-label req">Live weight (Kg)</label><asp:TextBox ID="txtKg" runat="server" CssClass="form-control text-end lcalc" inputmode="decimal" /></div>
                                <div class="col-6"><label class="form-label req">Rate per Kg (Rs)</label><asp:TextBox ID="txtRate" runat="server" CssClass="form-control text-end lcalc" inputmode="decimal" /></div>
                                <div class="col-6"><label class="form-label">Amount received (Rs)</label><asp:TextBox ID="txtRecv" runat="server" CssClass="form-control text-end" inputmode="decimal" Text="0" /></div>
                                <div class="col-12"><div class="calc-chip">Avg weight <b id="lAvg">-</b> kg/bird &middot; Amount <b id="lAmt">-</b></div></div>
                                <div class="col-12"><label class="form-label">Vehicle no. / remarks</label><asp:TextBox ID="txtRemarks" runat="server" CssClass="form-control" MaxLength="300" /></div>
                            </div>
                            <div class="d-flex gap-2 mt-3">
                                <asp:LinkButton ID="btnSave" runat="server" CssClass="btn btn-primary" OnClick="btnSave_Click"><i class="bi bi-check2 me-1"></i>Save lifting</asp:LinkButton>
                                <asp:LinkButton ID="btnNew" runat="server" CssClass="btn btn-light border" OnClick="btnNew_Click">New</asp:LinkButton>
                            </div>
                        </div>
                    </asp:Panel>
                </div>
                <div class="col-xl-8 col-xxl-9">
                    <div class="card">
                        <div class="card-header d-flex flex-wrap gap-2 justify-content-between align-items-center">
                            <span>Liftings</span>
                            <div class="d-flex gap-2">
                                <asp:DropDownList ID="ddlBatch" runat="server" CssClass="form-select form-select-sm" AutoPostBack="true" OnSelectedIndexChanged="Batch_Changed" />
                            </div>
                        </div>
                        <div class="table-responsive"><asp:Literal ID="litList" runat="server" Mode="PassThrough" /></div>
                    </div>
                </div>
            </div>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
<asp:Content ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        window.liftCmd = function (cmd, msg) {
            if (msg && !confirm(msg)) return false;
            document.getElementById('<%= hidCmd.ClientID %>').value = cmd;
            document.getElementById('<%= btnCmd.ClientID %>').click(); return false;
        };
        (function () {
            function calc() {
                var b = App.num('#<%= txtBirds.ClientID %>'), kg = App.num('#<%= txtKg.ClientID %>'), r = App.num('#<%= txtRate.ClientID %>');
                $('#lAvg').text(b && kg ? (kg / b).toFixed(3) : '-');
                $('#lAmt').text(kg && r ? 'Rs ' + App.fmt(kg * r, 2) : '-');
            }
            $(document).on('input', '.lift-card .lcalc', calc);
            $(document).on('app:init', calc);
        })();
    </script>
</asp:Content>
