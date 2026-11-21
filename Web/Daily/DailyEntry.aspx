<%@ Page Title="Daily Shed Entry" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="DailyEntry.aspx.cs" Inherits="PoultryERP.DailyEntryPage" %>
<asp:Content ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        @media (max-width: 991.98px) {
            .shed-list { display: flex; gap: .5rem; overflow-x: auto; padding-bottom: .35rem; scroll-snap-type: x mandatory; }
            .shed-list .shed-pill { min-width: 170px; margin-bottom: 0; scroll-snap-align: start; }
        }
        .entry-card .form-control { font-variant-numeric: tabular-nums; }
        .metric { font-size: .78rem; color: var(--erp-muted); }
        .metric b { color: #1f2a24; font-size: .9rem; }
        .entry-card[data-broiler="1"] .grit-only { display: none; }
        .delta-good { color: #1d6b4c; } .delta-bad { color: #b4302b; }
    </style>
</asp:Content>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:HiddenField ID="hidCmd" runat="server" />
            <asp:Button ID="btnCmd" runat="server" OnClick="btnCmd_Click" style="display:none" UseSubmitBehavior="false" />
            <asp:HiddenField ID="hidShed" runat="server" />
            <asp:HiddenField ID="hidFlock" runat="server" />
            <asp:HiddenField ID="hidLog" runat="server" />

            <div class="row g-3">
                <div class="col-lg-3">
                    <div class="card mb-3">
                        <div class="card-body">
                            <label class="form-label">Entry date</label>
                            <asp:TextBox ID="txtDate" runat="server" TextMode="Date" CssClass="form-control mb-2" AutoPostBack="true" OnTextChanged="txtDate_TextChanged" />
                            <asp:PlaceHolder ID="phFarm" runat="server">
                                <label class="form-label">Farm</label>
                                <asp:DropDownList ID="ddlFarm" runat="server" CssClass="form-select" AutoPostBack="true" OnSelectedIndexChanged="ddlFarm_Changed" />
                            </asp:PlaceHolder>
                            <div class="small text-muted-2 mt-2"><asp:Literal ID="litProgress" runat="server" /></div>
                        </div>
                    </div>
                    <div class="shed-list"><asp:Literal ID="litSheds" runat="server" Mode="PassThrough" /></div>
                </div>

                <div class="col-lg-9">
                    <asp:Panel ID="pnlEmpty" runat="server" CssClass="card">
                        <div class="empty-state"><i class="bi bi-hand-index"></i><asp:Literal ID="litEmpty" runat="server" Text="Select a shed to enter today's figures." /></div>
                    </asp:Panel>

                    <asp:Panel ID="pnlForm" runat="server" CssClass="card entry-card" Visible="false" DefaultButton="btnSave">
                        <div class="card-header d-flex flex-wrap justify-content-between align-items-center gap-2">
                            <div><span class="fs-6"><asp:Literal ID="litShedTitle" runat="server" /></span>
                                <div class="sub"><asp:Literal ID="litFlockInfo" runat="server" /></div></div>
                            <asp:Literal ID="litState" runat="server" />
                        </div>
                        <div class="card-body">
                            <asp:Panel ID="pnlLock" runat="server" CssClass="lock-banner mb-3" Visible="false"><i class="bi bi-lock me-1"></i><asp:Literal ID="litLock" runat="server" /></asp:Panel>
                            <asp:Panel ID="pnlErr" runat="server" CssClass="alert alert-danger py-2 small" Visible="false"><asp:Literal ID="litErr" runat="server" /></asp:Panel>

                            <div class="form-section-title"><i class="bi bi-feather"></i>Bird inventory</div>
                            <div class="row g-2 align-items-end">
                                <div class="col-6 col-md-2"><label class="form-label">Opening</label><div class="form-control bg-light text-end" id="vOpening"><asp:Literal ID="litOpening" runat="server" /></div></div>
                                <div class="col-6 col-md-2"><label class="form-label">Trf in / out</label><div class="form-control bg-light text-end"><asp:Literal ID="litTrf" runat="server" /></div></div>
                                <div class="col-4 col-md-2"><label class="form-label">Mortality</label><asp:TextBox ID="txtMort" runat="server" CssClass="form-control text-end calc" inputmode="numeric" /></div>
                                <div class="col-4 col-md-2"><label class="form-label">Culls</label><asp:TextBox ID="txtCull" runat="server" CssClass="form-control text-end calc" inputmode="numeric" /></div>
                                <asp:PlaceHolder ID="phSold" runat="server">
                                <div class="col-4 col-md-2"><label class="form-label">Birds sold</label><asp:TextBox ID="txtSold" runat="server" CssClass="form-control text-end calc" inputmode="numeric" /></div>
                                </asp:PlaceHolder>
                                <asp:PlaceHolder ID="phLifted" runat="server" Visible="false">
                                <div class="col-4 col-md-2"><label class="form-label">Lifted (sold)</label><div class="form-control bg-light text-end"><asp:Literal ID="litLifted" runat="server" /></div>
                                    <div class="metric"><a href="<%= ResolveUrl("~/Broiler/Lifting.aspx") %>">enter sales</a></div></div>
                                </asp:PlaceHolder>
                                <div class="col-12 col-md-2"><div class="calc-chip">Closing <b id="cClosing" class="float-end">-</b><br />Mort % <b id="cMort" class="float-end">-</b></div></div>
                            </div>

                            <div class="form-section-title"><i class="bi bi-basket"></i>Feed, shell grit &amp; water</div>
                            <div class="row g-2 align-items-end">
                                <div class="col-md-4"><label class="form-label">Feed formulation</label><asp:DropDownList ID="ddlFeed" runat="server" CssClass="form-select" /></div>
                                <div class="col-6 col-md-2"><label class="form-label">Feed (Kg)</label><asp:TextBox ID="txtFeed" runat="server" CssClass="form-control text-end calc" inputmode="decimal" /></div>
                                <div class="col-6 col-md-3"><div class="calc-chip">g / bird <b id="cGpb">-</b> <span class="ms-1">std <b id="cGstd">-</b></span> <span id="cGdiff"></span></div></div>
                                <div class="col-md-3"><label class="form-label">Water (Litres)</label><asp:TextBox ID="txtWater" runat="server" CssClass="form-control text-end calc" inputmode="decimal" /></div>
                                <asp:PlaceHolder ID="phGrit" runat="server">
                                <div class="col-md-4"><label class="form-label">Shell grit item</label><asp:DropDownList ID="ddlGrit" runat="server" CssClass="form-select" /></div>
                                <div class="col-6 col-md-2"><label class="form-label">Grit (Kg)</label><asp:TextBox ID="txtGrit" runat="server" CssClass="form-control text-end calc" inputmode="decimal" /></div>
                                <div class="col-6 col-md-3"><label class="form-label">Grit given at</label><asp:TextBox ID="txtGritTime" runat="server" TextMode="Time" CssClass="form-control" /></div>
                                </asp:PlaceHolder>
                                <div class="col-md-3"><div class="calc-chip"><span class="grit-only">Grit g/bird <b id="cGrit">-</b><br /></span>Water ml/bird <b id="cWater">-</b></div></div>
                            </div>

                            <asp:Panel ID="pnlBroiler" runat="server" Visible="false">
                                <div class="form-section-title"><i class="bi bi-speedometer"></i>Broiler growth &amp; FCR (live estimate)</div>
                                <div class="row g-2">
                                    <div class="col-md-4"><div class="calc-chip">Cum feed / bird (g) <b id="cCum">-</b> <span class="ms-1">std <b id="cCumStd">-</b></span></div></div>
                                    <div class="col-md-4"><div class="calc-chip">Estimated FCR <b id="cFcr">-</b> <span class="ms-1">std <b id="cFcrStd">-</b></span> <span id="cFcrDiff"></span></div></div>
                                    <div class="col-md-4"><div class="calc-chip">Body wt vs std <b id="cBw">-</b> <span id="cBwDiff"></span></div></div>
                                </div>
                                <div class="small text-muted-2 mt-1">FCR = total feed eaten in this shed &divide; (live weight in house + live weight already lifted). Enter today's sample body weight to see it.</div>
                            </asp:Panel>

                            <asp:Panel ID="pnlEggs" runat="server">
                                <div class="form-section-title"><i class="bi bi-egg"></i>Egg production (numbers)</div>
                                <div class="row g-2 align-items-end">
                                    <div class="col-6 col-md-2"><label class="form-label">Table / good</label><asp:TextBox ID="txtTable" runat="server" CssClass="form-control text-end calc" inputmode="numeric" /></div>
                                    <div class="col-6 col-md-2"><label class="form-label">Cracked / broken</label><asp:TextBox ID="txtCrack" runat="server" CssClass="form-control text-end calc" inputmode="numeric" /></div>
                                    <div class="col-6 col-md-2"><label class="form-label">Misshapen / thin</label><asp:TextBox ID="txtMis" runat="server" CssClass="form-control text-end calc" inputmode="numeric" /></div>
                                    <div class="col-6 col-md-2"><label class="form-label">Double yolk</label><asp:TextBox ID="txtDY" runat="server" CssClass="form-control text-end calc" inputmode="numeric" /></div>
                                    <div class="col-12 col-md-4"><div class="calc-chip">Total <b id="cEggs">-</b> &middot; Trays <b id="cTrays">-</b><br />HD % <b id="cHd">-</b> <span class="ms-1">std <b id="cHdStd">-</b></span> <span id="cHdDiff"></span></div></div>
                                </div>
                            </asp:Panel>

                            <div class="form-section-title"><i class="bi bi-thermometer-half"></i>Weights &amp; environment</div>
                            <div class="row g-2 align-items-end">
                                <asp:PlaceHolder ID="phEggWt" runat="server">
                                <div class="col-6 col-md-2"><label class="form-label">Avg egg wt (g)</label><asp:TextBox ID="txtEW" runat="server" CssClass="form-control text-end" inputmode="decimal" /><div class="metric">std <b><asp:Literal ID="litStdEW" runat="server" /></b></div></div>
                                </asp:PlaceHolder>
                                <div class="col-6 col-md-2"><label class="form-label">Avg body wt (g)</label><asp:TextBox ID="txtBW" runat="server" CssClass="form-control text-end" inputmode="decimal" /><div class="metric">std <b><asp:Literal ID="litStdBW" runat="server" /></b></div></div>
                                <div class="col-4 col-md-2"><label class="form-label">Temp min &deg;C</label><asp:TextBox ID="txtTmin" runat="server" CssClass="form-control text-end" inputmode="decimal" /><div class="metric">&nbsp;</div></div>
                                <div class="col-4 col-md-2"><label class="form-label">Temp max &deg;C</label><asp:TextBox ID="txtTmax" runat="server" CssClass="form-control text-end" inputmode="decimal" /><div class="metric">&nbsp;</div></div>
                                <div class="col-4 col-md-2"><label class="form-label">Humidity %</label><asp:TextBox ID="txtHum" runat="server" CssClass="form-control text-end" inputmode="decimal" /><div class="metric">&nbsp;</div></div>
                                <div class="col-12"><label class="form-label">Remarks</label><asp:TextBox ID="txtRemarks" runat="server" CssClass="form-control" MaxLength="400" /></div>
                            </div>

                            <div class="d-flex flex-wrap gap-2 mt-4">
                                <asp:LinkButton ID="btnSave" runat="server" CssClass="btn btn-primary" OnClick="btnSave_Click" CommandArgument="stay"><i class="bi bi-check2 me-1"></i>Save</asp:LinkButton>
                                <asp:LinkButton ID="btnSaveNext" runat="server" CssClass="btn btn-yolk" OnClick="btnSave_Click" CommandArgument="next"><i class="bi bi-skip-forward me-1"></i>Save &amp; next shed</asp:LinkButton>
                                <asp:LinkButton ID="btnDelete" runat="server" CssClass="btn btn-outline-danger ms-auto" OnClick="btnDelete_Click" Visible="false"
                                    OnClientClick="return confirm('Delete this day\'s entry for the shed?');"><i class="bi bi-trash me-1"></i>Delete entry</asp:LinkButton>
                            </div>
                        </div>
                    </asp:Panel>

                    <asp:Panel ID="pnlHistory" runat="server" CssClass="card mt-3" Visible="false">
                        <div class="card-header">Last 7 days &mdash; this shed</div>
                        <div class="table-responsive"><asp:Literal ID="litHistory" runat="server" Mode="PassThrough" /></div>
                    </asp:Panel>
                </div>
            </div>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
<asp:Content ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        window.deCmd = function (cmd) {
            document.getElementById('<%= hidCmd.ClientID %>').value = cmd;
            document.getElementById('<%= btnCmd.ClientID %>').click();
            return false;
        };
        (function () {
            function v(id) { var x = parseFloat(String($('#' + id).val() || '').replace(/,/g, '')); return isNaN(x) ? 0 : x; }
            function delta(el, actual, std, goodIfHigher, tolPct) {
                var $e = $(el); if (!std || !actual) { $e.html(''); return; }
                var d = (actual - std) / std * 100, ok = goodIfHigher ? d >= -tolPct : Math.abs(d) <= tolPct;
                $e.html('<span class="' + (ok ? 'delta-good' : 'delta-bad') + '">' + (d >= 0 ? '+' : '') + d.toFixed(1) + '%</span>');
            }
            function calc() {
                var $f = $('.entry-card'); if (!$f.length) return;
                var avail = +$f.data('avail') || 0, tray = +$f.data('tray') || 30;
                var mort = v('<%= txtMort.ClientID %>'), cull = v('<%= txtCull.ClientID %>'), sold = v('<%= txtSold.ClientID %>');
                var close = avail - mort - cull - sold;
                $('#cClosing').text(App.fmt(close)).toggleClass('text-danger', close < 0);
                $('#cMort').text(avail ? ((mort + cull) / avail * 100).toFixed(3) : '-');
                var feed = v('<%= txtFeed.ClientID %>'), gpb = avail ? feed * 1000 / avail : 0, gstd = +$f.data('stdfeed') || 0;
                $('#cGpb').text(feed ? gpb.toFixed(1) : '-'); $('#cGstd').text(gstd ? gstd.toFixed(1) : '-'); delta('#cGdiff', gpb, gstd, false, 10);
                var grit = v('<%= txtGrit.ClientID %>'), water = v('<%= txtWater.ClientID %>');
                $('#cGrit').text(grit && avail ? (grit * 1000 / avail).toFixed(1) : '-'); $('#cWater').text(water && avail ? App.fmt(water * 1000 / avail) : '-');
                var eggs = v('<%= txtTable.ClientID %>') + v('<%= txtCrack.ClientID %>') + v('<%= txtMis.ClientID %>') + v('<%= txtDY.ClientID %>');
                var hd = avail ? eggs / avail * 100 : 0, hstd = +$f.data('stdhd') || 0;
                $('#cEggs').text(App.fmt(eggs)); $('#cTrays').text(App.fmt(Math.floor(eggs / tray)) + (eggs % tray ? ' + ' + (eggs % tray) : ''));
                if (+$f.data('broiler') === 1) {
                    var placed = +$f.data('placed') || 0, cum = (+$f.data('cumfeed') || 0) + feed, bw = v('<%= txtBW.ClientID %>'), sbw = +$f.data('stdbw') || 0;
                    var cstd = +$f.data('stdcum') || 0, fstd = +$f.data('stdfcr') || 0, soldKg = +$f.data('soldkg') || 0;
                    $('#cCum').text(placed ? App.fmt(cum * 1000 / placed) : '-'); $('#cCumStd').text(cstd ? App.fmt(cstd) : '-');
                    var live = close * bw / 1000 + soldKg, fcr = bw && live > 0 ? cum / live : 0;
                    $('#cFcr').text(fcr ? fcr.toFixed(3) : '-'); $('#cFcrStd').text(fstd ? fstd.toFixed(3) : '-'); $('#cFcrDiff').html(fcr && fstd ? '<span class="' + (fcr <= fstd * 1.03 ? 'delta-good' : 'delta-bad') + '">' + (fcr - fstd >= 0 ? '+' : '') + (fcr - fstd).toFixed(3) + '</span>' : '');
                    $('#cBw').text(bw ? App.fmt(bw) + ' / ' + (sbw ? App.fmt(sbw) : '-') : '-'); delta('#cBwDiff', bw, sbw, true, 5);
                }
                $('#cHd').text(eggs ? hd.toFixed(2) : '-').toggleClass('text-danger', hd > 100); $('#cHdStd').text(hstd ? hstd.toFixed(2) : '-'); delta('#cHdDiff', hd, hstd, true, 5);
            }
            $(document).on('input', '.entry-card .calc, .entry-card input[id$=txtBW]', calc);
            $(document).on('app:init', calc);
            $(calc);
        })();
    </script>
</asp:Content>
