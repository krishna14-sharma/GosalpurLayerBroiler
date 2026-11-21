<%@ Page Title="Sales (Eggs / Birds)" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="SaleInvoice.aspx.cs" Inherits="PoultryERP.SaleInvoicePage" %>
<asp:Content ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .inv-lines td { vertical-align: middle; }
        .inv-lines .form-control, .inv-lines .form-select { font-size: .85rem; padding: .3rem .45rem; }
        .inv-lines td.prod { min-width: 150px; } .inv-lines td.shed { min-width: 170px; }
        .inv-lines td.num-in { min-width: 95px; } .inv-lines td.basis { min-width: 105px; }
        .inv-total { font-size: 1.05rem; }
    </style>
</asp:Content>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:HiddenField ID="hidCmd" runat="server" />
            <asp:HiddenField ID="hidIds" runat="server" />
            <asp:Button ID="btnCmd" runat="server" OnClick="btnCmd_Click" style="display:none" UseSubmitBehavior="false" />
            <p class="text-muted-2 small">One invoice for a customer can carry several lines with different products and rates (e.g. table eggs at one rate and cracked eggs at another).
                Eggs are sold from the farm's egg godown (no shed) and cannot exceed the godown stock. Cull birds need the shed they left and must also be entered as
                'Birds sold' in the Daily Shed Entry. Broiler sales are entered under Broiler Sales / Lifting.</p>

            <asp:Panel ID="pnlForm" runat="server" CssClass="card mb-3 inv-card">
                <div class="card-header"><i class="bi bi-receipt me-1"></i><asp:Literal ID="litTitle" runat="server" Text="New sale invoice" /></div>
                <div class="card-body">
                    <asp:Panel ID="pnlErr" runat="server" CssClass="alert alert-danger py-2 small" Visible="false"><asp:Literal ID="litErr" runat="server" /></asp:Panel>
                    <div class="row g-2">
                        <div class="col-6 col-md-2"><label class="form-label req">Sale date</label><asp:TextBox ID="txtDate" runat="server" TextMode="Date" CssClass="form-control" AutoPostBack="true" OnTextChanged="Farm_Changed" /></div>
                        <div class="col-6 col-md-3"><label class="form-label req">Farm</label><asp:DropDownList ID="ddlFarm" runat="server" CssClass="form-select" AutoPostBack="true" OnSelectedIndexChanged="Farm_Changed" /></div>
                        <div class="col-12 col-md-4"><label class="form-label">Customer</label><asp:DropDownList ID="ddlCustomer" runat="server" CssClass="form-select" /></div>
                        <div class="col-6 col-md-3"><label class="form-label">Invoice / DM no.</label><asp:TextBox ID="txtInv" runat="server" CssClass="form-control" MaxLength="40" placeholder="auto if blank" /></div>
                        <div class="col-12"><asp:Literal ID="litStock" runat="server" /></div>
                    </div>

                    <div class="table-responsive mt-2">
                        <table class="table table-erp inv-lines mb-1">
                            <thead><tr><th>#</th><th>Product</th><th>From shed <span class="small fw-normal">(cull birds)</span></th><th class="num">Qty (nos)</th>
                                <th class="num">Weight (Kg)</th><th class="num">Rate (Rs)</th><th>Rate is</th><th class="num">Amount (Rs)</th></tr></thead>
                            <tbody>
                            <asp:Repeater ID="rptLines" runat="server" OnItemDataBound="rptLines_ItemDataBound">
                                <ItemTemplate>
                                    <tr class="inv-line">
                                        <td class="text-muted-2"><%# Container.ItemIndex + 1 %></td>
                                        <td class="prod"><asp:DropDownList ID="ddlProd" runat="server" CssClass="form-select ln-prod" /></td>
                                        <td class="shed"><asp:DropDownList ID="ddlShed" runat="server" CssClass="form-select ln-shed" /></td>
                                        <td class="num-in"><asp:TextBox ID="txtQty" runat="server" CssClass="form-control text-end ln-qty" inputmode="decimal" /></td>
                                        <td class="num-in"><asp:TextBox ID="txtKg" runat="server" CssClass="form-control text-end ln-kg" inputmode="decimal" /></td>
                                        <td class="num-in"><asp:TextBox ID="txtRate" runat="server" CssClass="form-control text-end ln-rate" inputmode="decimal" /></td>
                                        <td class="basis"><asp:DropDownList ID="ddlBasis" runat="server" CssClass="form-select ln-basis" /></td>
                                        <td class="num ln-amt fw-semibold">-</td>
                                    </tr>
                                </ItemTemplate>
                            </asp:Repeater>
                            </tbody>
                            <tfoot><tr class="fw-bold"><td colspan="7" class="text-end">Invoice total</td><td class="num inv-total" id="invTotal">-</td></tr></tfoot>
                        </table>
                    </div>
                    <asp:LinkButton ID="btnMore" runat="server" CssClass="btn btn-sm btn-light border mb-3" OnClick="btnMore_Click"><i class="bi bi-plus-lg me-1"></i>Add 3 more lines</asp:LinkButton>

                    <div class="row g-2">
                        <div class="col-6 col-md-3"><label class="form-label">Amount received (Rs)</label><asp:TextBox ID="txtRecv" runat="server" CssClass="form-control text-end" inputmode="decimal" Text="0" /></div>
                        <div class="col-12 col-md-9"><label class="form-label">Remarks</label><asp:TextBox ID="txtRemarks" runat="server" CssClass="form-control" MaxLength="300" /></div>
                    </div>
                    <div class="d-flex gap-2 mt-3">
                        <asp:LinkButton ID="btnSave" runat="server" CssClass="btn btn-primary" OnClick="btnSave_Click"><i class="bi bi-check2 me-1"></i>Save invoice</asp:LinkButton>
                        <asp:LinkButton ID="btnNew" runat="server" CssClass="btn btn-light border" OnClick="btnNew_Click">New / Cancel</asp:LinkButton>
                    </div>
                </div>
            </asp:Panel>

            <div class="card">
                <div class="card-header d-flex flex-wrap gap-2 justify-content-between align-items-center">
                    <span><i class="bi bi-cart-check me-1"></i>Sale invoices</span>
                    <div class="d-flex gap-2">
                        <asp:TextBox ID="txtFrom" runat="server" TextMode="Date" CssClass="form-control form-control-sm" AutoPostBack="true" OnTextChanged="List_Changed" />
                        <asp:TextBox ID="txtTo" runat="server" TextMode="Date" CssClass="form-control form-control-sm" AutoPostBack="true" OnTextChanged="List_Changed" />
                        <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control form-control-sm" placeholder="Customer / invoice" AutoPostBack="true" OnTextChanged="List_Changed" />
                    </div>
                </div>
                <div class="table-responsive"><asp:Literal ID="litList" runat="server" Mode="PassThrough" /></div>
            </div>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
<asp:Content ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        window.invCmd = function (cmd, msg) {
            if (msg && !confirm(msg)) return false;
            document.getElementById('<%= hidCmd.ClientID %>').value = cmd;
            document.getElementById('<%= btnCmd.ClientID %>').click(); return false;
        };
        (function () {
            var eggs = ['Table Egg', 'Cracked Egg', 'Double Yolk', 'Misshapen Egg'];
            function calc() {
                var tot = 0;
                $('.inv-line').each(function () {
                    var row = $(this), prod = row.find('.ln-prod').val(), shed = row.find('.ln-shed');
                    var egg = eggs.indexOf(prod) >= 0;
                    if (egg) shed.val('');
                    shed.prop('disabled', egg || !prod); shed.css('opacity', egg || !prod ? .45 : 1);
                    var q = App.num(row.find('.ln-qty')), kg = App.num(row.find('.ln-kg')), r = App.num(row.find('.ln-rate'));
                    var amt = (row.find('.ln-basis').val() === 'Per Kg' ? kg : q) * r;
                    row.find('.ln-amt').text(amt ? App.fmt(amt, 2) : '-'); tot += amt || 0;
                });
                $('#invTotal').text(tot ? 'Rs ' + App.fmt(tot, 2) : '-');
            }
            $(document).on('input change', '.inv-card .inv-lines input, .inv-card .inv-lines select', calc);
            $(document).on('app:init', calc);
        })();
    </script>
</asp:Content>
