<%@ Page Title="Medication & Vaccination" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="Medication.aspx.cs" Inherits="PoultryERP.MedicationPage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:HiddenField ID="hidCmd" runat="server" />
            <asp:HiddenField ID="hidId" runat="server" />
            <asp:Button ID="btnCmd" runat="server" OnClick="btnCmd_Click" style="display:none" UseSubmitBehavior="false" />
            <div class="row g-3">
                <div class="col-xl-4">
                    <asp:Panel ID="pnlForm" runat="server" CssClass="card mb-3" DefaultButton="btnSave">
                        <div class="card-header"><asp:Literal ID="litFormTitle" runat="server" Text="Record medication / vaccination" /></div>
                        <div class="card-body">
                            <asp:Panel ID="pnlErr" runat="server" CssClass="alert alert-danger py-2 small" Visible="false"><asp:Literal ID="litErr" runat="server" /></asp:Panel>
                            <div class="row g-2">
                                <div class="col-6"><label class="form-label req">Date</label><asp:TextBox ID="txtDate" runat="server" TextMode="Date" CssClass="form-control" /></div>
                                <div class="col-6"><label class="form-label req">Type</label>
                                    <asp:DropDownList ID="ddlKind" runat="server" CssClass="form-select" AutoPostBack="true" OnSelectedIndexChanged="Kind_Changed">
                                        <asp:ListItem>Medication</asp:ListItem><asp:ListItem>Vaccination</asp:ListItem></asp:DropDownList></div>
                                <div class="col-12"><label class="form-label req">Shed</label><asp:DropDownList ID="ddlShed" runat="server" CssClass="form-select" /></div>
                                <div class="col-12"><label class="form-label req">Item</label><asp:DropDownList ID="ddlItem" runat="server" CssClass="form-select" /></div>
                                <div class="col-6"><label class="form-label">Quantity used</label><asp:TextBox ID="txtQty" runat="server" CssClass="form-control text-end" inputmode="decimal" />
                                    <div class="form-text">In the item's unit (ml, g, doses)</div></div>
                                <div class="col-6"><label class="form-label">Dose</label><asp:TextBox ID="txtDose" runat="server" CssClass="form-control" MaxLength="60" placeholder="e.g. 1 ml / L" /></div>
                                <div class="col-6"><label class="form-label">Route</label>
                                    <asp:DropDownList ID="ddlRoute" runat="server" CssClass="form-select">
                                        <asp:ListItem></asp:ListItem><asp:ListItem>Drinking water</asp:ListItem><asp:ListItem>Feed</asp:ListItem><asp:ListItem>Eye drop</asp:ListItem>
                                        <asp:ListItem>Nasal</asp:ListItem><asp:ListItem>Injection</asp:ListItem><asp:ListItem>Wing web</asp:ListItem><asp:ListItem>Spray</asp:ListItem></asp:DropDownList></div>
                                <div class="col-6"><label class="form-label">Status</label>
                                    <asp:DropDownList ID="ddlStatus" runat="server" CssClass="form-select"><asp:ListItem>Done</asp:ListItem><asp:ListItem>Planned</asp:ListItem><asp:ListItem>Skipped</asp:ListItem></asp:DropDownList></div>
                                <div class="col-12"><label class="form-label">Reason / disease</label><asp:TextBox ID="txtReason" runat="server" CssClass="form-control" MaxLength="200" /></div>
                                <div class="col-12"><label class="form-label">Administered by</label><asp:TextBox ID="txtBy" runat="server" CssClass="form-control" MaxLength="100" /></div>
                            </div>
                            <div class="d-flex gap-2 mt-3">
                                <asp:LinkButton ID="btnSave" runat="server" CssClass="btn btn-primary" OnClick="btnSave_Click"><i class="bi bi-check2 me-1"></i>Save</asp:LinkButton>
                                <asp:LinkButton ID="btnReset" runat="server" CssClass="btn btn-light border" OnClick="btnReset_Click">New</asp:LinkButton>
                            </div>
                        </div>
                    </asp:Panel>
                    <asp:Panel ID="pnlGen" runat="server" CssClass="card">
                        <div class="card-header"><i class="bi bi-calendar2-plus me-1"></i>Generate vaccination plan</div>
                        <div class="card-body">
                            <p class="small text-muted-2">Creates <b>Planned</b> entries for a flock from the Vaccination Schedule (skips those already recorded).</p>
                            <asp:DropDownList ID="ddlGenFlock" runat="server" CssClass="form-select mb-2" />
                            <asp:LinkButton ID="btnGen" runat="server" CssClass="btn btn-outline-primary w-100" OnClick="btnGen_Click">Generate plan</asp:LinkButton>
                        </div>
                    </asp:Panel>
                </div>
                <div class="col-xl-8">
                    <div class="card">
                        <div class="card-header">
                            <div class="row g-2 align-items-end">
                                <div class="col-6 col-md-3"><label class="form-label">From</label><asp:TextBox ID="fFrom" runat="server" TextMode="Date" CssClass="form-control form-control-sm" /></div>
                                <div class="col-6 col-md-3"><label class="form-label">To</label><asp:TextBox ID="fTo" runat="server" TextMode="Date" CssClass="form-control form-control-sm" /></div>
                                <div class="col-6 col-md-3"><label class="form-label">Show</label>
                                    <asp:DropDownList ID="fShow" runat="server" CssClass="form-select form-select-sm">
                                        <asp:ListItem Value="">All</asp:ListItem><asp:ListItem Value="due">Planned / due</asp:ListItem><asp:ListItem Value="appr">Awaiting vet approval</asp:ListItem>
                                        <asp:ListItem Value="Vaccination">Vaccinations</asp:ListItem><asp:ListItem Value="Medication">Medications</asp:ListItem></asp:DropDownList></div>
                                <div class="col-6 col-md-3"><asp:LinkButton ID="btnFilter" runat="server" CssClass="btn btn-sm btn-primary w-100" OnClick="btnFilter_Click"><i class="bi bi-funnel me-1"></i>Show</asp:LinkButton></div>
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
        window.medCmd = function (cmd, msg) {
            if (msg && !confirm(msg)) return false;
            document.getElementById('<%= hidCmd.ClientID %>').value = cmd;
            document.getElementById('<%= btnCmd.ClientID %>').click(); return false;
        };
    </script>
</asp:Content>
