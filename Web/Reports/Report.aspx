<%@ Page Title="Report" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="Report.aspx.cs" Inherits="PoultryERP.ReportPage" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <asp:UpdatePanel ID="up" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <div class="card mb-3 report-filters">
                <div class="card-body pb-2">
                    <div class="d-flex flex-wrap justify-content-between align-items-start gap-2 mb-2">
                        <div>
                            <div class="fw-bold"><asp:Literal ID="litTitle" runat="server" /></div>
                            <div class="small text-muted-2"><asp:Literal ID="litDesc" runat="server" /></div>
                        </div>
                        <span class="badge text-bg-light border small"><i class="bi bi-clock-history me-1"></i><asp:Literal ID="litOld" runat="server" /></span>
                    </div>
                    <div class="row g-2 align-items-end">
                        <asp:PlaceHolder ID="phDate" runat="server">
                            <div class="col-6 col-md-3 col-xl-2"><label class="form-label">Date</label>
                                <asp:TextBox ID="txtDate" runat="server" TextMode="Date" CssClass="form-control form-control-sm" /></div>
                        </asp:PlaceHolder>
                        <asp:PlaceHolder ID="phRange" runat="server">
                            <div class="col-6 col-md-3 col-xl-2"><label class="form-label">From</label>
                                <asp:TextBox ID="txtFrom" runat="server" TextMode="Date" CssClass="form-control form-control-sm" /></div>
                            <div class="col-6 col-md-3 col-xl-2"><label class="form-label">To</label>
                                <asp:TextBox ID="txtTo" runat="server" TextMode="Date" CssClass="form-control form-control-sm" /></div>
                        </asp:PlaceHolder>
                        <asp:PlaceHolder ID="phFarm" runat="server">
                            <div class="col-6 col-md-3 col-xl-2"><label class="form-label">Farm</label>
                                <asp:DropDownList ID="ddlFarm" runat="server" CssClass="form-select form-select-sm" AutoPostBack="true" OnSelectedIndexChanged="ddlFarm_Changed" /></div>
                        </asp:PlaceHolder>
                        <asp:PlaceHolder ID="phShed" runat="server">
                            <div class="col-6 col-md-3 col-xl-2"><label class="form-label">Shed<asp:Literal ID="litShedReq" runat="server" /></label>
                                <asp:DropDownList ID="ddlShed" runat="server" CssClass="form-select form-select-sm" /></div>
                        </asp:PlaceHolder>
                        <asp:PlaceHolder ID="phFlock" runat="server">
                            <div class="col-6 col-md-3 col-xl-2"><label class="form-label">Flock<asp:Literal ID="litFlockReq" runat="server" /></label>
                                <asp:DropDownList ID="ddlFlock" runat="server" CssClass="form-select form-select-sm" /></div>
                        </asp:PlaceHolder>
                        <asp:PlaceHolder ID="phGroup" runat="server">
                            <div class="col-6 col-md-3 col-xl-2"><label class="form-label">Group by</label>
                                <asp:DropDownList ID="ddlGroup" runat="server" CssClass="form-select form-select-sm">
                                    <asp:ListItem>Daily</asp:ListItem><asp:ListItem Selected="True">Weekly</asp:ListItem><asp:ListItem>Monthly</asp:ListItem>
                                </asp:DropDownList></div>
                        </asp:PlaceHolder>
                        <asp:PlaceHolder ID="phProduct" runat="server">
                            <div class="col-6 col-md-3 col-xl-2"><label class="form-label">Category / Product</label>
                                <asp:DropDownList ID="ddlProduct" runat="server" CssClass="form-select form-select-sm" /></div>
                        </asp:PlaceHolder>
                        <asp:PlaceHolder ID="phWt" runat="server">
                            <div class="col-6 col-md-3 col-xl-2"><div class="form-check mb-1">
                                <asp:CheckBox ID="chkWt" runat="server" Checked="true" Text="With egg &amp; body weight" /></div></div>
                        </asp:PlaceHolder>
                        <div class="col-12 col-md-auto">
                            <asp:LinkButton ID="btnShow" runat="server" CssClass="btn btn-primary btn-sm px-3" OnClick="btnShow_Click"><i class="bi bi-play-fill me-1"></i>Show report</asp:LinkButton>
                        </div>
                    </div>
                </div>
            </div>

            <asp:Panel ID="pnlToolbar" runat="server" CssClass="d-flex flex-wrap gap-2 mb-2 report-toolbar" Visible="false">
                <asp:HyperLink ID="lnkPdf" runat="server" CssClass="btn btn-outline-danger btn-sm" Target="_blank"><i class="bi bi-filetype-pdf me-1"></i>PDF</asp:HyperLink>
                <asp:HyperLink ID="lnkXlsx" runat="server" CssClass="btn btn-outline-success btn-sm"><i class="bi bi-file-earmark-excel me-1"></i>Excel</asp:HyperLink>
                <asp:HyperLink ID="lnkCsv" runat="server" CssClass="btn btn-outline-secondary btn-sm"><i class="bi bi-filetype-csv me-1"></i>CSV</asp:HyperLink>
                <button type="button" class="btn btn-outline-dark btn-sm" onclick="window.print()"><i class="bi bi-printer me-1"></i>Print</button>
            </asp:Panel>

            <asp:Literal ID="litReport" runat="server" Mode="PassThrough" />
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
<asp:Content ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="<%= ResolveUrl("~/lib/chartjs/chart.umd.min.js") %>"></script>
    <script>
        (function () {
            var chart;
            function draw() {
                var el = document.getElementById('rpChart');
                if (!el || !window.Chart) return;
                if (chart) { chart.destroy(); chart = null; }
                var spec = JSON.parse(el.getAttribute('data-chart'));
                var palette = ['#1d6b4c', '#f2a900', '#2a5ea8', '#b4302b', '#6b7a72'];
                var hasY2 = spec.series.some(function (s) { return s.axis === 'y2'; });
                chart = new Chart(el, {
                    type: 'line',
                    data: {
                        labels: spec.labels,
                        datasets: spec.series.map(function (s, i) {
                            var dashed = /standard|std/i.test(s.name);
                            return { label: s.name, data: s.data, type: s.type, yAxisID: s.axis, borderColor: palette[i % palette.length],
                                backgroundColor: s.type === 'bar' ? 'rgba(29,107,76,.55)' : palette[i % palette.length],
                                borderDash: dashed ? [5, 4] : [], pointRadius: spec.labels.length > 40 ? 0 : 2, tension: .25, spanGaps: true, borderWidth: 2 };
                        })
                    },
                    options: {
                        maintainAspectRatio: false, interaction: { mode: 'index', intersect: false },
                        plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } } },
                        scales: { y: { title: { display: !!spec.y, text: spec.y } },
                                  y2: { display: hasY2, position: 'right', grid: { drawOnChartArea: false }, title: { display: !!spec.y2, text: spec.y2 } } }
                    }
                });
            }
            $(document).on('app:init', draw);
            $(draw);
        })();
    </script>
</asp:Content>
