<%@ Page Title="Opening Balances (Go-Live)" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="OpeningBalance.aspx.cs" Inherits="PoultryERP.OpeningBalancePage" %>
<asp:Content ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .ob-table td { min-width: 92px; }
        .ob-table td.shed { min-width: 120px; }
        .ob-table td.breed { min-width: 200px; }
        .ob-table .form-control, .ob-table .form-select { font-size: .82rem; padding: .25rem .4rem; }
        .ob-hint { font-size: .72rem; color: var(--erp-muted); font-weight: 400; text-transform: none; letter-spacing: 0; }
    </style>
</asp:Content>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <div class="card mb-3"><div class="card-body">
                <div class="row g-3 align-items-end">
                    <div class="col-md-3"><label class="form-label req">Go-live date</label>
                        <asp:TextBox ID="txtGoLive" runat="server" TextMode="Date" CssClass="form-control" /></div>
                    <div class="col-md-9 small text-muted-2">
                        Birds are placed on the go-live date. Egg and store stock are recorded as at the end of the day before.
                        Daily entries start on the go-live date. Sheds that already hold birds in the system are not listed.
                    </div>
                </div>
            </div></div>

            <asp:Panel ID="pnlErr" runat="server" CssClass="alert alert-danger small" Visible="false"><asp:Literal ID="litErr" runat="server" /></asp:Panel>
            <asp:Panel ID="pnlDone" runat="server" CssClass="alert alert-success small" Visible="false"><asp:Literal ID="litDone" runat="server" /></asp:Panel>

            <div class="card mb-3">
                <div class="card-header">1. Birds in sheds today <span class="sub ms-1">leave "Birds today" empty for an empty shed</span></div>
                <div class="table-responsive">
                    <table class="table table-erp ob-table">
                        <thead><tr>
                            <th>Shed</th><th>Flock / batch code</th><th>Breed (type)</th><th>Hatch date</th><th class="num">Birds today</th>
                            <th class="num">Value / bird (Rs)</th><th class="num">Hens housed<div class="ob-hint">layers in lay</div></th>
                            <th>Placed on<div class="ob-hint">optional - history</div></th><th class="num">Chicks placed<div class="ob-hint">optional</div></th>
                            <th class="num">Feed to date (Kg)<div class="ob-hint">optional</div></th>
                        </tr></thead>
                        <tbody>
                        <asp:Repeater ID="rptSheds" runat="server" OnItemDataBound="rptSheds_ItemDataBound">
                            <ItemTemplate>
                                <tr>
                                    <td class="shed"><b><%# Ui.H(Eval("Code")) %></b><div class="small text-muted-2"><%# Ui.H(Eval("Purpose")) %> &middot; cap <%# Ui.N0(Eval("Capacity")) %></div>
                                        <asp:HiddenField ID="hidShed" runat="server" Value='<%# Eval("ShedId") %>' /></td>
                                    <td><asp:TextBox ID="txtCode" runat="server" CssClass="form-control" MaxLength="30" /></td>
                                    <td class="breed"><asp:DropDownList ID="ddlBreed" runat="server" CssClass="form-select" /></td>
                                    <td><asp:TextBox ID="txtHatch" runat="server" TextMode="Date" CssClass="form-control" /></td>
                                    <td><asp:TextBox ID="txtBirds" runat="server" CssClass="form-control text-end" inputmode="numeric" /></td>
                                    <td><asp:TextBox ID="txtValue" runat="server" CssClass="form-control text-end" inputmode="decimal" /></td>
                                    <td><asp:TextBox ID="txtHoused" runat="server" CssClass="form-control text-end" inputmode="numeric" /></td>
                                    <td><asp:TextBox ID="txtPlaced" runat="server" TextMode="Date" CssClass="form-control" /></td>
                                    <td><asp:TextBox ID="txtChicks" runat="server" CssClass="form-control text-end" inputmode="numeric" /></td>
                                    <td><asp:TextBox ID="txtFeed" runat="server" CssClass="form-control text-end" inputmode="decimal" /></td>
                                </tr>
                            </ItemTemplate>
                        </asp:Repeater>
                        </tbody>
                    </table>
                    <asp:Literal ID="litNoSheds" runat="server" />
                </div>
                <div class="card-body pt-0 small text-muted-2">
                    <b>Simple start:</b> fill shed, code, breed, hatch date, birds today and value per bird. <b>One flock in several sheds:</b> type the same code (with the same breed and hatch date) on each of its sheds; a code that already exists gets these sheds added.
                    <b>With history</b> (e.g. a running broiler batch): also give the original placement date, chicks placed and feed eaten so far.
                    The system then places the original chicks on that date and books one catch-up entry the day before go-live with the mortality (chicks placed &minus; birds today) and the feed to date.
                    This keeps FCR, livability and batch P&amp;L correct.
                </div>
            </div>

            <div class="row g-3">
                <div class="col-lg-5">
                    <div class="card h-100">
                        <div class="card-header">2. Eggs in godown</div>
                        <div class="table-responsive">
                            <table class="table table-erp ob-table">
                                <thead><tr><th>Farm</th><th class="num">Eggs (nos.)</th><th class="num">Current</th></tr></thead>
                                <tbody>
                                <asp:Repeater ID="rptEggs" runat="server">
                                    <ItemTemplate>
                                        <tr><td><b><%# Ui.H(Eval("Name")) %></b><asp:HiddenField ID="hidFarm" runat="server" Value='<%# Eval("FarmId") %>' /></td>
                                            <td><asp:TextBox ID="txtEggs" runat="server" CssClass="form-control text-end" inputmode="numeric" /></td>
                                            <td class="num small text-muted-2"><%# Ui.N0(Eval("OpeningEggStock")) %><%# Eval("OpeningEggDate") == DBNull.Value ? "" : " on " + Ui.D(Eval("OpeningEggDate")) %></td></tr>
                                    </ItemTemplate>
                                </asp:Repeater>
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
                <div class="col-lg-7">
                    <div class="card h-100">
                        <div class="card-header d-flex flex-wrap gap-2 justify-content-between align-items-center">
                            <span>3. Feed / grit / medicine in store</span>
                            <asp:DropDownList ID="ddlStoreFarm" runat="server" CssClass="form-select form-select-sm" style="max-width:260px" />
                        </div>
                        <div class="table-responsive" style="max-height:420px">
                            <table class="table table-erp ob-table">
                                <thead><tr><th>Item</th><th>Unit</th><th class="num">Quantity</th><th class="num">Rate (Rs)</th></tr></thead>
                                <tbody>
                                <asp:Repeater ID="rptStock" runat="server">
                                    <ItemTemplate>
                                        <tr><td><%# Ui.H(Eval("Name")) %> <span class="badge text-bg-light border"><%# Ui.H(Eval("Category")) %></span>
                                                <asp:HiddenField ID="hidItem" runat="server" Value='<%# Eval("ItemId") %>' /></td>
                                            <td><%# Ui.H(Eval("Unit")) %></td>
                                            <td><asp:TextBox ID="txtQty" runat="server" CssClass="form-control text-end" inputmode="decimal" /></td>
                                            <td><asp:TextBox ID="txtRate" runat="server" CssClass="form-control text-end" inputmode="decimal" Text='<%# Ui.NumValue(Eval("Rate")) %>' /></td></tr>
                                    </ItemTemplate>
                                </asp:Repeater>
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
            </div>

            <div class="d-flex gap-2 mt-3">
                <asp:LinkButton ID="btnSave" runat="server" CssClass="btn btn-primary btn-lg" OnClick="btnSave_Click"
                    OnClientClick="return confirm('Save all opening balances? Flocks, placements and stock will be created.');"><i class="bi bi-check2-all me-1"></i>Save opening balances</asp:LinkButton>
            </div>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
