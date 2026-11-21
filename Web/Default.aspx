<%@ Page Title="Dashboard" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="Default.aspx.cs" Inherits="PoultryERP.DashboardPage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <div class="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-3">
        <div>
            <div class="small text-muted-2">Figures as of <b id="asOf">...</b> <span id="asOfNote" class="badge text-bg-warning ms-1 d-none">latest entry date</span></div>
        </div>
        <div class="d-flex flex-wrap gap-2">
            <% if (CanEnter) { %>
            <a class="btn btn-yolk btn-sm" href="<%= ResolveUrl("~/Daily/DailyEntry.aspx") %>"><i class="bi bi-journal-plus me-1"></i>Daily entry</a>
            <a class="btn btn-outline-primary btn-sm" href="<%= ResolveUrl("~/Flock/Transfers.aspx") %>"><i class="bi bi-arrow-left-right me-1"></i>Transfer</a>
            <% } %>
            <a class="btn btn-outline-primary btn-sm" href="<%= ResolveUrl("~/Reports/Report.aspx?r=dtr") %>"><i class="bi bi-file-earmark-spreadsheet me-1"></i>Daily report</a>
            <button type="button" class="btn btn-light btn-sm border" id="btnRefresh" title="Refresh"><i class="bi bi-arrow-clockwise"></i></button>
        </div>
    </div>

    <div id="alerts" class="mb-3"></div>

    <div class="row g-3 mb-3">
        <div class="col-6 col-lg-4 col-xxl-2"><div class="card h-100"><div class="kpi"><div class="ico ico-green"><i class="bi bi-feather"></i></div>
            <div><div class="lbl">Live birds</div><div class="val" id="kBirds">-</div><div class="delta text-muted-2" id="kFlocks"></div></div></div></div></div>
        <div class="col-6 col-lg-4 col-xxl-2"><div class="card h-100"><div class="kpi"><div class="ico ico-yolk"><i class="bi bi-egg"></i></div>
            <div><div class="lbl">Eggs</div><div class="val" id="kEggs">-</div><div class="delta text-muted-2" id="kTrays"></div></div></div></div></div>
        <div class="col-6 col-lg-4 col-xxl-2"><div class="card h-100"><div class="kpi"><div class="ico ico-blue"><i class="bi bi-graph-up"></i></div>
            <div><div class="lbl">Hen-day %</div><div class="val" id="kHd">-</div><div class="delta text-muted-2">laying sheds</div></div></div></div></div>
        <div class="col-6 col-lg-4 col-xxl-2"><div class="card h-100"><div class="kpi"><div class="ico ico-red"><i class="bi bi-heartbreak"></i></div>
            <div><div class="lbl">Mortality + culls</div><div class="val" id="kMort">-</div><div class="delta text-muted-2" id="kMortPct"></div></div></div></div></div>
        <div class="col-6 col-lg-4 col-xxl-2"><div class="card h-100"><div class="kpi"><div class="ico ico-slate"><i class="bi bi-basket"></i></div>
            <div><div class="lbl">Feed (Kg)</div><div class="val" id="kFeed">-</div><div class="delta text-muted-2" id="kG"></div></div></div></div></div>
        <div class="col-6 col-lg-4 col-xxl-2"><div class="card h-100"><div class="kpi"><div class="ico ico-green"><i class="bi bi-currency-rupee"></i></div>
            <div><div class="lbl">Cost / egg (30 d)</div><div class="val" id="kCost"><% if (!CanSeeCost) { %>&ndash;<% } else { %><span class="spinner-border spinner-border-sm text-secondary"></span><% } %></div>
            <div class="delta text-muted-2" id="kCostNote"><% if (!CanSeeCost) { %>finance roles only<% } %></div></div></div></div></div>
    </div>

    <div class="row g-3 mb-3">
        <div class="col-xl-8"><div class="card h-100">
            <div class="card-header d-flex justify-content-between"><span>Production &mdash; last 30 days</span><span class="sub">eggs (bars) &middot; HD % (line)</span></div>
            <div class="card-body"><div style="height:290px"><canvas id="chProd"></canvas></div></div>
        </div></div>
        <div class="col-xl-4"><div class="card h-100">
            <div class="card-header">Birds by stage</div>
            <div class="card-body"><div style="height:180px"><canvas id="chStage"></canvas></div>
                <div class="mt-3" style="height:110px"><canvas id="chMort"></canvas></div></div>
        </div></div>
    </div>

    <div class="card">
        <div class="card-header d-flex flex-wrap justify-content-between gap-2">
            <span>Shed status</span>
            <span class="sub" id="entryStatus"></span>
        </div>
        <div class="table-responsive">
            <table class="table table-erp table-hover">
                <thead><tr><th>Shed</th><th class="d-none d-md-table-cell">Farm</th><th>Flock</th><th>Age</th><th>Stage</th><th class="num">Birds</th>
                    <th class="d-none d-md-table-cell" style="min-width:110px">Occupancy</th><th>Status</th><th>Today's entry</th></tr></thead>
                <tbody id="shedRows"><tr><td colspan="9" class="empty-state">Loading...</td></tr></tbody>
            </table>
        </div>
    </div>
</asp:Content>
<asp:Content ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="<%= ResolveUrl("~/lib/chartjs/chart.umd.min.js") %>"></script>
    <script>
        (function () {
            var charts = {}, entryUrl = '<%= ResolveUrl("~/Daily/DailyEntry.aspx") %>', canCost = <%= CanSeeCost ? "true" : "false" %>;
            function esc(s) { return $('<div>').text(s == null ? '' : s).html(); }
            function mk(id, cfg) { if (charts[id]) charts[id].destroy(); charts[id] = new Chart(document.getElementById(id), cfg); }
            function load() {
                App.pageMethod('GetDashboard').then(function (d) {
                    $('#asOf').text(d.asOf); $('#asOfNote').toggleClass('d-none', d.isToday);
                    var k = d.kpi;
                    $('#kBirds').text(App.fmt(k.birds)); $('#kFlocks').text(k.flocks + ' active flock' + (k.flocks === 1 ? '' : 's'));
                    $('#kEggs').text(App.fmt(k.eggs)); $('#kTrays').text(App.fmt(k.trays) + ' trays');
                    $('#kHd').text(k.hd == null ? '-' : App.fmt(k.hd, 2));
                    $('#kMort').text(App.fmt(k.mort)); $('#kMortPct').text(k.mortPct == null ? '' : App.fmt(k.mortPct, 3) + ' % of birds');
                    $('#kFeed').text(App.fmt(k.feed)); $('#kG').text(k.gPerBird == null ? '' : App.fmt(k.gPerBird, 1) + ' g/bird');
                    $('#entryStatus').html('Entries today: <b>' + k.entered + ' / ' + k.occupied + '</b> occupied sheds');

                    var al = [];
                    if (d.pending.transfers) al.push('<a class="alert alert-warning d-flex align-items-center gap-2 py-2 mb-2 text-decoration-none" href="<%= ResolveUrl("~/Flock/Transfers.aspx") %>"><i class="bi bi-arrow-left-right"></i><span><b>' + d.pending.transfers + '</b> flock transfer request(s) waiting for approval</span></a>');
                    if (d.pending.vaccines) al.push('<a class="alert alert-info d-flex align-items-center gap-2 py-2 mb-2 text-decoration-none" href="<%= ResolveUrl("~/Health/Medication.aspx") %>"><i class="bi bi-capsule"></i><span><b>' + d.pending.vaccines + '</b> vaccination record(s) waiting for vet approval</span></a>');
                    if (d.pending.due) al.push('<a class="alert alert-secondary d-flex align-items-center gap-2 py-2 mb-2 text-decoration-none" href="<%= ResolveUrl("~/Health/Medication.aspx") %>"><i class="bi bi-calendar2-week"></i><span><b>' + d.pending.due + '</b> planned medication / vaccination due or overdue</span></a>');
                    $('#alerts').html(al.join(''));

                    mk('chProd', { data: { labels: d.series.labels, datasets: [
                        { type: 'bar', label: 'Eggs', data: d.series.eggs, backgroundColor: 'rgba(242,169,0,.55)', borderRadius: 3, yAxisID: 'y' },
                        { type: 'line', label: 'HD %', data: d.series.hd, borderColor: '#1d6b4c', backgroundColor: '#1d6b4c', tension: .3, pointRadius: 2, yAxisID: 'y2', spanGaps: true }] },
                        options: { maintainAspectRatio: false, interaction: { mode: 'index', intersect: false }, plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } } },
                            scales: { y: { beginAtZero: true, ticks: { callback: function (v) { return App.fmt(v); } } }, y2: { position: 'right', grid: { drawOnChartArea: false }, suggestedMin: 0, suggestedMax: 100 } } } });
                    var st = d.stages;
                    mk('chStage', { type: 'doughnut', data: { labels: Object.keys(st), datasets: [{ data: Object.keys(st).map(function (x) { return st[x]; }),
                        backgroundColor: ['#f6d77a', '#8cc5a8', '#2a8a63', '#123829', '#d9822b'], borderWidth: 0 }] },
                        options: { maintainAspectRatio: false, cutout: '62%', plugins: { legend: { position: 'right', labels: { boxWidth: 12 } } } } });
                    mk('chMort', { type: 'bar', data: { labels: d.series.labels, datasets: [{ label: 'Mortality + culls', data: d.series.mort, backgroundColor: 'rgba(180,48,43,.6)', borderRadius: 2 }] },
                        options: { maintainAspectRatio: false, plugins: { legend: { display: false }, title: { display: true, text: 'Daily mortality + culls', align: 'start', font: { size: 11 } } },
                            scales: { x: { display: false }, y: { beginAtZero: true, ticks: { maxTicksLimit: 3 } } } } });

                    var rows = d.sheds.map(function (s) {
                        var bar = '<div class="progress" style="height:6px" title="' + s.fill + '% of ' + App.fmt(s.capacity) + '"><div class="progress-bar ' + (s.fill > 100 ? 'bg-danger' : 'bg-success') + '" style="width:' + Math.min(s.fill, 100) + '%"></div></div>';
                        var entry = s.birds <= 0 ? '<span class="text-muted-2 small">-</span>' : s.today
                            ? '<span class="badge text-bg-success"><i class="bi bi-check2"></i> done</span>'
                            : '<a class="badge text-bg-warning text-decoration-none" href="' + entryUrl + '?shed=' + s.id + '">pending &middot; last ' + esc(s.lastLog) + '</a>';
                        var cls = { Active: 'success', Vacant: 'secondary', Cleaning: 'warning', Quarantine: 'danger' }[s.status] || 'info';
                        return '<tr><td><b>' + esc(s.code) + '</b><div class="small text-muted-2">' + esc(s.purpose) + '</div></td><td class="d-none d-md-table-cell">' + esc(s.farm) +
                            '</td><td>' + esc(s.flock || '-') + '</td><td>' + esc(s.age) + '</td><td>' + esc(s.stage) + '</td><td class="num">' + (s.birds > 0 ? App.fmt(s.birds) : '-') +
                            '</td><td class="d-none d-md-table-cell">' + bar + '</td><td><span class="badge rounded-pill text-bg-' + cls + '">' + esc(s.status) + '</span></td><td>' + entry + '</td></tr>';
                    });
                    $('#shedRows').html(rows.length ? rows.join('') : '<tr><td colspan="9"><div class="empty-state"><i class="bi bi-house"></i>No sheds yet. Add farms, locations and sheds under Masters.</div></td></tr>');
                }).fail(function (x) { App.toast('Dashboard could not load: ' + ((x.responseJSON && x.responseJSON.Message) || x.statusText), 'danger'); });

                if (canCost) App.pageMethod('GetCost').then(function (c) {
                    if (!c) return;
                    $('#kCost').text(c.opPerEgg == null ? '-' : 'Rs ' + App.fmt(c.opPerEgg, 2));
                    $('#kCostNote').text(c.totalPerEgg == null ? 'no laying data' : 'Rs ' + App.fmt(c.totalPerEgg, 2) + ' incl. depreciation');
                }).fail(function () { $('#kCost').text('-'); });
            }
            $(load);
            $(document).on('click', '#btnRefresh', load);
        })();
    </script>
</asp:Content>
