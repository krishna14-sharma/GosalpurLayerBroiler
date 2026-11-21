<%@ Page Title="Flocks & Placement" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="Flocks.aspx.cs" Inherits="PoultryERP.FlocksPage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:HiddenField ID="hidCmd" runat="server" />
            <asp:Button ID="btnCmd" runat="server" OnClick="btnCmd_Click" style="display:none" UseSubmitBehavior="false" />
            <asp:HiddenField ID="hidFlock" runat="server" />

            <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
                <p class="text-muted-2 small mb-0">A flock is one batch of chicks from a hatchery. Its age always runs from the hatch date, whichever shed it is in.</p>
                <div class="d-flex gap-2">
                    <asp:DropDownList ID="ddlStatus" runat="server" CssClass="form-select form-select-sm" AutoPostBack="true" OnSelectedIndexChanged="Filter_Changed">
                        <asp:ListItem Value="Active" Selected="True">Active flocks</asp:ListItem><asp:ListItem Value="L">Active layers</asp:ListItem><asp:ListItem Value="B">Active broilers</asp:ListItem><asp:ListItem Value="Depleted">Depleted</asp:ListItem><asp:ListItem Value="">All</asp:ListItem>
                    </asp:DropDownList>
                    <asp:LinkButton ID="btnNew" runat="server" CssClass="btn btn-yolk btn-sm text-nowrap" OnClick="btnNew_Click"><i class="bi bi-plus-lg me-1"></i>New inward</asp:LinkButton>
                </div>
            </div>

            <asp:Panel ID="pnlForm" runat="server" CssClass="card mb-3" Visible="false" DefaultButton="btnSave">
                <div class="card-header"><asp:Literal ID="litFormTitle" runat="server" /></div>
                <div class="card-body">
                    <asp:Panel ID="pnlErr" runat="server" CssClass="alert alert-danger py-2 small" Visible="false"><asp:Literal ID="litErr" runat="server" /></asp:Panel>
                    <div class="form-section-title"><i class="bi bi-truck"></i>Inward / source</div>
                    <div class="row g-2">
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label req">Bird type</label>
                            <asp:DropDownList ID="ddlType" runat="server" CssClass="form-select" AutoPostBack="true" OnSelectedIndexChanged="Type_Changed">
                                <asp:ListItem Value="Layer">Layer (eggs)</asp:ListItem><asp:ListItem Value="Broiler">Broiler (meat)</asp:ListItem></asp:DropDownList></div>
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label req">Flock / batch code</label><asp:TextBox ID="txtCode" runat="server" CssClass="form-control" MaxLength="30" /></div>
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label">Batch no.</label><asp:TextBox ID="txtBatch" runat="server" CssClass="form-control" MaxLength="40" /></div>
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label">Lot no.</label><asp:TextBox ID="txtLot" runat="server" CssClass="form-control" MaxLength="40" /></div>
                        <div class="col-6 col-md-3 col-lg-3"><label class="form-label">Source hatchery</label><asp:DropDownList ID="ddlSupplier" runat="server" CssClass="form-select" /></div>
                        <div class="col-md-6 col-lg-3"><label class="form-label req">Breed / strain</label><asp:DropDownList ID="ddlBreed" runat="server" CssClass="form-select" /></div>
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label req">Hatch date</label><asp:TextBox ID="txtHatch" runat="server" TextMode="Date" CssClass="form-control" /></div>
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label req">Arrival date</label><asp:TextBox ID="txtArrival" runat="server" TextMode="Date" CssClass="form-control" /></div>
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label req">Chicks received</label><asp:TextBox ID="txtInitial" runat="server" CssClass="form-control text-end" inputmode="numeric" /></div>
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label">Dead on arrival</label><asp:TextBox ID="txtDoa" runat="server" CssClass="form-control text-end" inputmode="numeric" Text="0" /></div>
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label">Rate per chick (Rs)</label><asp:TextBox ID="txtRate" runat="server" CssClass="form-control text-end" inputmode="decimal" /></div>
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label">Freight / charges (Rs)</label><asp:TextBox ID="txtFreight" runat="server" CssClass="form-control text-end" inputmode="decimal" Text="0" /></div>
                    </div>
                    <asp:PlaceHolder ID="phBroiler" runat="server" Visible="false">
                    <div class="form-section-title"><i class="bi bi-calendar-check"></i>Broiler batch</div>
                    <div class="row g-2">
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label">Target lifting age (days)</label><asp:TextBox ID="txtLift" runat="server" CssClass="form-control text-end" Text="42" />
                            <div class="form-text">Maximum <%= Company.BroilerMaxDays %> days</div></div>
                    </div>
                    </asp:PlaceHolder>
                    <asp:PlaceHolder ID="phLayer" runat="server">
                    <div class="form-section-title"><i class="bi bi-calculator"></i>Costing &amp; lay planning</div>
                    <div class="row g-2">
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label">Planned depletion week</label><asp:TextBox ID="txtDeplWk" runat="server" CssClass="form-control text-end" Text="80" /></div>
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label">Salvage value / bird (Rs)</label><asp:TextBox ID="txtSalvage" runat="server" CssClass="form-control text-end" Text="0" /></div>
                        <div class="col-6 col-md-3 col-lg-2"><label class="form-label">Hens housed</label><asp:TextBox ID="txtHoused" runat="server" CssClass="form-control text-end" />
                            <div class="form-text">Leave blank to use birds at start of lay</div></div>
                    </div>
                    </asp:PlaceHolder>
                    <div class="row g-2 mt-1">
                        <div class="col-md-6 col-lg-6"><label class="form-label">Remarks</label><asp:TextBox ID="txtRemarks" runat="server" CssClass="form-control" MaxLength="400" /></div>
                    </div>
                    <asp:PlaceHolder ID="phFirstPlace" runat="server">
                        <div class="form-section-title"><i class="bi bi-house-door"></i>Initial placement</div>
                        <div class="row g-2">
                            <div class="col-md-5 col-lg-4"><label class="form-label req">Shed</label><asp:DropDownList ID="ddlPlaceShed" runat="server" CssClass="form-select" /></div>
                            <div class="col-6 col-md-3 col-lg-2"><label class="form-label">Birds placed</label><asp:TextBox ID="txtPlaceCount" runat="server" CssClass="form-control text-end" />
                                <div class="form-text">Blank = all live chicks</div></div>
                        </div>
                    </asp:PlaceHolder>
                    <div class="d-flex gap-2 mt-3">
                        <asp:LinkButton ID="btnSave" runat="server" CssClass="btn btn-primary" OnClick="btnSave_Click"><i class="bi bi-check2 me-1"></i>Save flock</asp:LinkButton>
                        <asp:LinkButton ID="btnCancel" runat="server" CssClass="btn btn-light border" OnClick="btnCancel_Click">Cancel</asp:LinkButton>
                    </div>
                </div>
            </asp:Panel>

            <asp:Panel ID="pnlPlace" runat="server" CssClass="card mb-3" Visible="false">
                <div class="card-header d-flex justify-content-between"><span><asp:Literal ID="litPlaceTitle" runat="server" /></span>
                    <asp:LinkButton ID="btnClosePlace" runat="server" CssClass="btn btn-sm btn-light border" OnClick="btnCancel_Click"><i class="bi bi-x-lg"></i></asp:LinkButton></div>
                <div class="card-body">
                    <asp:Literal ID="litPlacements" runat="server" Mode="PassThrough" />
                    <asp:Panel ID="pnlAddPlace" runat="server" CssClass="row g-2 align-items-end mt-2">
                        <div class="col-md-4"><label class="form-label">Shed</label><asp:DropDownList ID="ddlAddShed" runat="server" CssClass="form-select" /></div>
                        <div class="col-6 col-md-3"><label class="form-label">Date</label><asp:TextBox ID="txtAddDate" runat="server" TextMode="Date" CssClass="form-control" /></div>
                        <div class="col-6 col-md-2"><label class="form-label">Birds</label><asp:TextBox ID="txtAddCount" runat="server" CssClass="form-control text-end" /></div>
                        <div class="col-md-3"><asp:LinkButton ID="btnAddPlace" runat="server" CssClass="btn btn-primary w-100" OnClick="btnAddPlace_Click"><i class="bi bi-plus-lg me-1"></i>Add placement</asp:LinkButton></div>
                    </asp:Panel>
                    <div class="small text-muted-2 mt-2">Placement is for chicks arriving from the hatchery. To move birds between sheds later, use <a href="Transfers.aspx">Flock Transfers</a>.</div>
                </div>
            </asp:Panel>

            <div class="card">
                <div class="table-responsive"><asp:Literal ID="litList" runat="server" Mode="PassThrough" /></div>
            </div>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
<asp:Content ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        window.flkCmd = function (cmd, msg) {
            if (msg && !confirm(msg)) return false;
            document.getElementById('<%= hidCmd.ClientID %>').value = cmd;
            document.getElementById('<%= btnCmd.ClientID %>').click(); return false;
        };
    </script>
</asp:Content>
