<%@ Page Title="Edit Overrides" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="Overrides.aspx.cs" Inherits="PoultryERP.OverridesPage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:HiddenField ID="hidCmd" runat="server" />
            <asp:Button ID="btnCmd" runat="server" OnClick="btnCmd_Click" style="display:none" UseSubmitBehavior="false" />
            <p class="text-muted-2 small">Supervisors can change daily records only for the last <b><%= Company.EditWindowDays %></b> day(s).
                A manager override temporarily unlocks older dates for a shed (and optionally one user). Every change made under an override is written to the audit log.</p>
            <div class="row g-3">
                <div class="col-xl-4">
                    <asp:Panel ID="pnlForm" runat="server" CssClass="card" DefaultButton="btnGrant">
                        <div class="card-header"><i class="bi bi-unlock me-1"></i>Grant override</div>
                        <div class="card-body">
                            <div class="row g-2">
                                <div class="col-12"><label class="form-label">Shed</label><asp:DropDownList ID="ddlShed" runat="server" CssClass="form-select" /></div>
                                <div class="col-12"><label class="form-label">Supervisor</label><asp:DropDownList ID="ddlUser" runat="server" CssClass="form-select" /></div>
                                <div class="col-6"><label class="form-label req">From date</label><asp:TextBox ID="txtFrom" runat="server" TextMode="Date" CssClass="form-control" /></div>
                                <div class="col-6"><label class="form-label req">To date</label><asp:TextBox ID="txtTo" runat="server" TextMode="Date" CssClass="form-control" /></div>
                                <div class="col-6"><label class="form-label">Valid for (hours)</label><asp:TextBox ID="txtHours" runat="server" CssClass="form-control text-end" Text="24" /></div>
                                <div class="col-12"><label class="form-label req">Reason</label><asp:TextBox ID="txtReason" runat="server" CssClass="form-control" MaxLength="300" /></div>
                            </div>
                            <asp:LinkButton ID="btnGrant" runat="server" CssClass="btn btn-primary mt-3" OnClick="btnGrant_Click"><i class="bi bi-unlock me-1"></i>Grant</asp:LinkButton>
                        </div>
                    </asp:Panel>
                </div>
                <div class="col-xl-8">
                    <div class="card"><div class="card-header">Overrides</div>
                        <div class="table-responsive"><asp:Literal ID="litList" runat="server" Mode="PassThrough" /></div></div>
                </div>
            </div>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
<asp:Content ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        window.ovrCmd = function (cmd) {
            if (!confirm('Revoke this override?')) return false;
            document.getElementById('<%= hidCmd.ClientID %>').value = cmd;
            document.getElementById('<%= btnCmd.ClientID %>').click(); return false;
        };
    </script>
</asp:Content>
