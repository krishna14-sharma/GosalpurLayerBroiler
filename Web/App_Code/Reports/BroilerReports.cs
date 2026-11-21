using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace PoultryERP.Reports
{
    /// <summary>Life-to-date figures of one broiler batch up to a date.</summary>
    public class BatchMetrics
    {
        public FlockMeta F;
        public DateTime To;
        public int Placed, Mort, Cull, Transit, SoldBirds, BirdsNow;
        public decimal SoldKg, SalesAmt, Received, FeedKg, AgeBirdSum, OtherIncome;
        public decimal? LastBw;
        public bool BwIsStd;

        public int AgeDays { get { return Age.Days(F.HatchDate, To); } }
        public decimal? AvgWtSold { get { return SoldBirds > 0 ? Math.Round(SoldKg / SoldBirds, 3) : (decimal?)null; } }
        public decimal? AvgAgeSold { get { return SoldBirds > 0 ? Math.Round(AgeBirdSum / SoldBirds, 1) : (decimal?)null; } }
        public decimal? AvgRatePerKg { get { return SoldKg > 0 ? Math.Round(SalesAmt / SoldKg, 2) : (decimal?)null; } }
        public decimal InHouseKg { get { return LastBw.HasValue ? BirdsNow * LastBw.Value / 1000m : 0; } }
        public decimal LiveKg { get { return SoldKg + InHouseKg; } }
        public decimal? FCR { get { return LiveKg > 0 && FeedKg > 0 ? Math.Round(FeedKg / LiveKg, 3) : (decimal?)null; } }
        public decimal? Livability { get { return Placed > 0 ? Math.Round((Placed - Mort - Cull - Transit) * 100m / Placed, 2) : (decimal?)null; } }
        public decimal? MortPct { get { return Placed > 0 ? Math.Round((Mort + Cull + Transit) * 100m / Placed, 2) : (decimal?)null; } }
        /// <summary>European Production Efficiency Factor = livability% x avg wt kg x 100 / (avg age days x FCR). Uses sold birds when lifted, else birds in house.</summary>
        public decimal? EPEF
        {
            get
            {
                decimal? wt = AvgWtSold ?? (LastBw.HasValue ? LastBw / 1000m : null);
                decimal? age = AvgAgeSold ?? AgeDays;
                if (!wt.HasValue || !age.HasValue || age <= 0 || !FCR.HasValue || FCR <= 0 || !Livability.HasValue) return null;
                return Math.Round(Livability.Value * wt.Value * 100m / (age.Value * FCR.Value), 0);
            }
        }

        public static BatchMetrics Load(FlockMeta f, DateTime to, StandardBook std)
        {
            var m = new BatchMetrics { F = f, To = to };
            var r = Db.Row(@"SELECT
                (SELECT ISNULL(SUM(BirdCount),0) FROM dbo.FlockPlacement WHERE FlockId = @f AND PlaceDate <= @t) AS Placed,
                (SELECT ISNULL(SUM(Mortality),0) FROM dbo.DailyLog WHERE FlockId = @f AND LogDate <= @t) AS Mort,
                (SELECT ISNULL(SUM(Culls),0) FROM dbo.DailyLog WHERE FlockId = @f AND LogDate <= @t) AS Cull,
                (SELECT ISNULL(SUM(FeedKg),0) FROM dbo.DailyLog WHERE FlockId = @f AND LogDate <= @t) AS Feed,
                (SELECT ISNULL(SUM(TransitMortality + TransitCulls),0) FROM dbo.FlockTransfer WHERE FlockId = @f AND Status = N'Approved' AND TransferDate <= @t) AS Transit,
                (SELECT ISNULL(SUM(Qty),0) FROM dbo.Sale WHERE FlockId = @f AND Product = N'Broiler Bird' AND SaleDate <= @t) AS SoldBirds,
                (SELECT ISNULL(SUM(QtyKg),0) FROM dbo.Sale WHERE FlockId = @f AND Product = N'Broiler Bird' AND SaleDate <= @t) AS SoldKg,
                (SELECT ISNULL(SUM(Amount),0) FROM dbo.Sale WHERE FlockId = @f AND Product = N'Broiler Bird' AND SaleDate <= @t) AS SalesAmt,
                (SELECT ISNULL(SUM(AmountReceived),0) FROM dbo.Sale WHERE FlockId = @f AND SaleDate <= @t) AS Recv,
                (SELECT ISNULL(SUM(Amount),0) FROM dbo.Sale WHERE FlockId = @f AND Product <> N'Broiler Bird' AND SaleDate <= @t) AS Other,
                (SELECT ISNULL(SUM(Qty * DATEDIFF(DAY, @h, SaleDate)),0) FROM dbo.Sale WHERE FlockId = @f AND Product = N'Broiler Bird' AND SaleDate <= @t) AS AgeBirds,
                (SELECT ISNULL(SUM(Qty),0) FROM dbo.vBirdMovement WHERE FlockId = @f AND MoveDate <= @t) AS BirdsNow",
                Db.P("@f", f.FlockId), Db.P("@t", to), Db.P("@h", f.HatchDate));
            m.Placed = Db.Int(r["Placed"]); m.Mort = Db.Int(r["Mort"]); m.Cull = Db.Int(r["Cull"]); m.FeedKg = Db.Dec(r["Feed"]);
            m.Transit = Db.Int(r["Transit"]); m.SoldBirds = (int)Db.Dec(r["SoldBirds"]); m.SoldKg = Db.Dec(r["SoldKg"]); m.SalesAmt = Db.Dec(r["SalesAmt"]);
            m.Received = Db.Dec(r["Recv"]); m.OtherIncome = Db.Dec(r["Other"]); m.AgeBirdSum = Db.Dec(r["AgeBirds"]); m.BirdsNow = Db.Int(r["BirdsNow"]);
            // latest body-weight sample (weighted by birds is not needed: usually one shed per batch)
            var bw = Db.Row("SELECT TOP (1) LogDate, AVG(AvgBodyWtG) AS Bw FROM dbo.DailyLog WHERE FlockId = @f AND LogDate <= @t AND AvgBodyWtG IS NOT NULL GROUP BY LogDate ORDER BY LogDate DESC",
                Db.P("@f", f.FlockId), Db.P("@t", to));
            if (bw != null && Convert.ToDateTime(bw["LogDate"]) >= to.AddDays(-7)) m.LastBw = Db.Dec(bw["Bw"]);
            else if (m.BirdsNow > 0)
            {
                var s = std == null ? null : std.Daily(f.BreedId, Age.Days(f.HatchDate, to));
                if (s != null && s.BodyWt.HasValue) { m.LastBw = s.BodyWt; m.BwIsStd = true; }
                else if (bw != null) m.LastBw = Db.Dec(bw["Bw"]);
            }
            return m;
        }
    }

    /// <summary>Profit &amp; loss of one batch from the cost engine (feed, medicine, allocated overheads) plus chick cost and sales.</summary>
    public class BatchPL
    {
        public BatchMetrics M;
        public decimal Chicks, Freight, Feed, Med, Labor, Utility, Overhead;
        public decimal Income { get { return M.SalesAmt + M.OtherIncome; } }
        public decimal Expenses { get { return Chicks + Freight + Feed + Med + Labor + Utility + Overhead; } }
        public decimal Profit { get { return Income - Expenses; } }
        public decimal StockValue { get { return M.BirdsNow > 0 && M.AvgRatePerKg.HasValue ? Math.Round(M.InHouseKg * M.AvgRatePerKg.Value, 2) : 0; } }

        public static BatchPL Build(BatchMetrics m, CostResult res)
        {
            var pl = new BatchPL { M = m, Chicks = m.F.InitialCount * m.F.ChickRate, Freight = m.F.FreightCost };
            foreach (var r in res.Rows)
            {
                if (r.FlockId != m.F.FlockId || r.Date > m.To) continue;
                pl.Feed += r.Feed + r.Grit; pl.Med += r.Med; pl.Labor += r.Labor; pl.Utility += r.Utility; pl.Overhead += r.Overhead + r.Other;
            }
            return pl;
        }
    }

    public static class BroilerReports
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static DateTime BatchEnd(FlockMeta f)
        {
            var to = DateTime.Today.AddDays(-1);
            if (f.DepletionDate.HasValue && f.DepletionDate.Value < to) to = f.DepletionDate.Value;
            if (to < f.ArrivalDate) to = f.ArrivalDate;
            return to;
        }

        static bool FlockInScope(FlockMeta f, Scope scope, MasterData md)
        {
            if (scope.AllowedSheds == null && !scope.FarmId.HasValue && !scope.ShedId.HasValue) return true;
            foreach (DataRow r in Db.Query("SELECT DISTINCT ShedId FROM dbo.vBirdMovement WHERE FlockId = @f", Db.P("@f", f.FlockId)).Rows)
                if (scope.Shed(Db.Int(r["ShedId"]), md)) return true;
            return false;
        }

        static string BatchTitle(FlockMeta f)
        {
            return "Batch: " + f.Code + " (" + f.BreedName + ")   |   Placed: " + Rep.D(f.ArrivalDate) + (f.Status == "Depleted" && f.DepletionDate.HasValue ? "   |   Closed: " + Rep.D(f.DepletionDate.Value) : "   |   Running");
        }

        static void BatchSummary(ReportModel m, BatchMetrics b, StdRow stdNow)
        {
            m.Summary("Batch").Add("Batch / breed", b.F.Code + " / " + b.F.BreedName).Add("Source", b.F.Source).Add("Placed on", Rep.D(b.F.ArrivalDate))
                .Add("Age " + (b.F.Status == "Depleted" ? "at close" : "today"), b.AgeDays + " days").Add("Chicks placed", Fmt.N(b.Placed, 0))
                .Add("Mortality + culls", Fmt.N(b.Mort + b.Cull + b.Transit, 0) + "  (" + Rep.N(b.MortPct, 2) + "%)").Add("Birds in house", Fmt.N(b.BirdsNow, 0));
            m.Summary("Performance").Add("Birds lifted", Fmt.N(b.SoldBirds, 0)).Add("Live weight lifted (Kg)", Fmt.N(b.SoldKg, 2))
                .Add("Average weight sold (Kg)", Rep.N(b.AvgWtSold, 3)).Add("Average age at sale (days)", Rep.N(b.AvgAgeSold, 1))
                .Add("Livability %", Rep.N(b.Livability, 2)).Add("Feed consumed (Kg)", Fmt.N(b.FeedKg, 1))
                .Add("FCR" + (b.BirdsNow > 0 ? " (incl. birds in house" + (b.BwIsStd ? " @ std wt" : "") + ")" : ""), Rep.N(b.FCR, 3) + (stdNow != null && stdNow.FCR.HasValue ? "  (std " + Fmt.N(stdNow.FCR.Value, 3) + ")" : ""))
                .Add("EPEF (production efficiency)", Rep.N(b.EPEF, 0));
        }

        // =====================================================================
        // B1. BROILER DAILY REPORT
        // =====================================================================
        public static ReportModel Daily(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var scope = Scope.From(p);
            var d = p.Date.Date;
            var std = StandardBook.Load(cs.CompanyId);
            var led = BirdLedger.Load(cs.CompanyId, d, d);
            var logs = LogData.Load(cs.CompanyId, d, d);
            var m = Rep.New("brdaily", "Broiler Daily Report", "Date: " + Rep.D(d) + "   |   " + Rep.ScopeText(p, md, false, false));

            var s = m.Section("Broiler Sheds");
            s.Col("Shed", "T").Col("Batch", "T").Col("Age (d)", "N0").Col("Opening", "N0").Col("Mort", "N0").Col("Culls", "N0").Col("Lifted", "N0")
             .Col("Closing", "N0").Col("Mort %", "N3").Col("Cum Mort %", "N2").Col("Feed (Kg)", "N1").Col("g/Bird", "N1").Col("Std g", "N1")
             .Col("Cum Feed/Bird (g)", "N0").Col("Std Cum (g)", "N0").Col("Body Wt (g)", "N0").Col("Std Wt (g)", "N0").Col("Est. FCR", "N3").Col("Std FCR", "N3");
            var metrics = new Dictionary<int, BatchMetrics>();
            int tO = 0, tM = 0, tC = 0, tL = 0, tCl = 0; decimal tF = 0; long tH = 0;
            foreach (var ld in led.Days.Where(x => scope.Includes(x.ShedId, x.FlockId, md)).OrderBy(x => md.ShedCode(x.ShedId)))
            {
                FlockMeta fm;
                if (!md.Flocks.TryGetValue(ld.FlockId, out fm) || !fm.IsBroiler) continue;
                if (ld.Opening <= 0 && ld.Present <= 0) continue;
                BatchMetrics b;
                if (!metrics.TryGetValue(fm.FlockId, out b)) { b = BatchMetrics.Load(fm, d, std); metrics[fm.FlockId] = b; }
                LogRow lg; logs.TryGetValue(LogData.Key(ld.ShedId, d), out lg);
                if (lg != null && lg.FlockId != fm.FlockId) lg = null;
                int age = Age.Days(fm.HatchDate, d);
                var st = std.Daily(fm.BreedId, age);
                int hens = Math.Max(ld.Present, 0);
                decimal feed = lg == null ? 0 : lg.FeedKg;
                s.Add(md.ShedCode(ld.ShedId), fm.Code, age, ld.Opening, ld.Mort, ld.Cull, ld.Sold, ld.Closing, Rep.Pct(ld.Mort + ld.Cull, hens), b.MortPct,
                      lg == null ? (decimal?)null : feed, hens > 0 && lg != null ? Math.Round(feed * 1000m / hens, 1) : (decimal?)null, st == null ? null : st.FeedG,
                      b.Placed > 0 ? Math.Round(b.FeedKg * 1000m / b.Placed, 0) : (decimal?)null, st == null ? null : st.CumFeedG,
                      lg == null ? null : lg.BodyWt, st == null ? null : st.BodyWt, b.FCR, st == null ? null : st.FCR);
                tO += ld.Opening; tM += ld.Mort; tC += ld.Cull; tL += ld.Sold; tCl += ld.Closing; tF += feed; if (lg != null) tH += hens;
                if (age > cs.BroilerMaxDays) m.Notes.Add("Batch " + fm.Code + " is " + age + " days old - above the " + cs.BroilerMaxDays + "-day limit.");
            }
            s.AddTotal("TOTAL", "", null, tO, tM, tC, tL, tCl, Rep.Pct(tM + tC, tO), null, tF, tH > 0 ? Math.Round(tF * 1000m / tH, 1) : (decimal?)null,
                       null, null, null, null, null, null, null);
            var sales = Db.Row(@"SELECT ISNULL(SUM(Qty),0) B, ISNULL(SUM(QtyKg),0) K, ISNULL(SUM(Amount),0) A FROM dbo.Sale s JOIN dbo.Shed sh ON sh.ShedId = s.ShedId
                                JOIN dbo.Location l ON l.LocationId = sh.LocationId
                                WHERE s.CompanyId = @CompanyId AND s.Product = N'Broiler Bird' AND s.SaleDate = @d AND (@farm IS NULL OR l.FarmId = @farm)" + Ctx.ShedFilter("s.ShedId"),
                                Ctx.Scope(Db.P("@d", d), Db.P("@farm", p.FarmId)));
            m.Summary("Today").Add("Opening birds", Fmt.N(tO, 0)).Add("Mortality + culls", Fmt.N(tM + tC, 0) + "  (" + Rep.N(Rep.Pct(tM + tC, tO), 3) + "%)")
                .Add("Closing birds", Fmt.N(tCl, 0)).Add("Feed (Kg)", Fmt.N(tF, 1));
            m.Summary("Lifting Today").Add("Birds lifted", Fmt.N(Db.Dec(sales["B"]), 0)).Add("Live weight (Kg)", Fmt.N(Db.Dec(sales["K"]), 2))
                .Add("Average weight (Kg)", Rep.N(Db.Dec(sales["B"]) > 0 ? Db.Dec(sales["K"]) / Db.Dec(sales["B"]) : (decimal?)null, 3))
                .Add("Sale value (Rs)", Fmt.N(Db.Dec(sales["A"]), 2));
            m.Notes.Add("Cum Mort %, Cum Feed/Bird and Est. FCR are batch-to-date. Est. FCR = feed eaten / (live weight lifted + birds in house x latest sample weight). Standards are interpolated for the exact day of age.");
            return m;
        }

        // =====================================================================
        // B2. BROILER PERFORMANCE & FCR REPORT (daily or weekly)
        // =====================================================================
        public static ReportModel Performance(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var scope = Scope.From(p);
            var m = Rep.New("brperf", "Broiler Performance & FCR Report", "");
            FlockMeta fm;
            if (!p.FlockId.HasValue || !md.Flocks.TryGetValue(p.FlockId.Value, out fm) || !fm.IsBroiler || !FlockInScope(fm, scope, md))
            {
                m.Subtitle = "Select a broiler batch"; m.Notes.Add("Choose a broiler batch to see growth, feed and FCR against the breeder standard."); return m;
            }
            bool weekly = !string.Equals(p.GroupBy, "Daily", StringComparison.OrdinalIgnoreCase);
            var from = fm.ArrivalDate; var to = BatchEnd(fm);
            m.Subtitle = BatchTitle(fm) + "   |   " + (weekly ? "Weekly" : "Daily");
            var std = StandardBook.Load(cs.CompanyId);
            var led = BirdLedger.Load(cs.CompanyId, from, to);
            var logs = LogData.Load(cs.CompanyId, from, to);
            var saleKg = new Dictionary<DateTime, decimal>();
            foreach (DataRow r in Db.Query("SELECT SaleDate, SUM(QtyKg) K FROM dbo.Sale WHERE FlockId = @f AND Product = N'Broiler Bird' GROUP BY SaleDate", Db.P("@f", fm.FlockId)).Rows)
                saleKg[Convert.ToDateTime(r["SaleDate"]).Date] = Db.Dec(r["K"]);

            int placed = 0;
            var s = m.Section(weekly ? "Weekly Growth & FCR" : "Daily Growth & FCR");
            s.Col(weekly ? "Week" : "Date", weekly ? "T" : "D").Col("Age (d)", "N0").Col("Opening", "N0").Col("Mort", "N0").Col("Culls", "N0").Col("Lifted", "N0")
             .Col("Lifted Kg", "N1").Col("Closing", "N0").Col("Cum Mort %", "N2").Col("Feed (Kg)", "N1").Col("g/Bird/Day", "N1").Col("Std g", "N1")
             .Col("Cum Feed/Bird (g)", "N0").Col("Std Cum (g)", "N0").Col("Body Wt (g)", "N0").Col("Std Wt (g)", "N0").Col("Gain g/Day", "N1")
             .Col("FCR", "N3").Col("Std FCR", "N3");
            var chart = new ChartSpec { Title = "Body weight and FCR vs standard", YTitle = "Body weight (g)", Y2Title = "FCR" };
            var cBw = new ChartSeries("Body wt (actual)"); var cBwS = new ChartSeries("Body wt (standard)");
            var cF = new ChartSeries("FCR (actual)") { Axis = "y2" }; var cFS = new ChartSeries("FCR (standard)") { Axis = "y2" };

            decimal cumFeed = 0, cumSoldKg = 0; int cumDepl = 0;
            // per-day aggregation, then emit per day or per week
            int gOpen = -1, gMort = 0, gCull = 0, gSold = 0, gClose = 0; decimal gFeed = 0, gSoldKg = 0; long gBirdDays = 0; decimal? gBw = null; DateTime gStart = from;
            decimal? gFcr = null; int gAge = 0;
            Action<string, int> emit = (label, ageEnd) =>
            {
                var st = std.Daily(fm.BreedId, ageEnd);
                var stFeed = std.Get(fm.BreedId, ageEnd / 7);
                decimal? gain = null;
                s.Add(weekly ? (object)label : gStart, ageEnd, gOpen < 0 ? 0 : gOpen, gMort, gCull, gSold, gSoldKg, gClose, placed > 0 ? Math.Round(cumDepl * 100m / placed, 2) : (decimal?)null,
                      gFeed, gBirdDays > 0 ? Math.Round(gFeed * 1000m / gBirdDays, 1) : (decimal?)null, stFeed == null ? null : stFeed.FeedG,
                      placed > 0 ? Math.Round(cumFeed * 1000m / placed, 0) : (decimal?)null, st == null ? null : st.CumFeedG,
                      gBw, st == null ? null : st.BodyWt, gain, gFcr, st == null ? null : st.FCR);
                chart.Labels.Add(weekly ? label : "D" + ageEnd);
                cBw.Values.Add(gBw); cBwS.Values.Add(st == null ? null : st.BodyWt); cF.Values.Add(gFcr); cFS.Values.Add(st == null ? null : st.FCR);
            };
            int currentWeek = -1;
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                int age = Age.Days(fm.HatchDate, d);
                int wk = age / 7;
                if (weekly && currentWeek >= 0 && wk != currentWeek) { emit("Wk " + (currentWeek + 1), gAge); ResetGroup(ref gOpen, ref gMort, ref gCull, ref gSold, ref gClose, ref gFeed, ref gSoldKg, ref gBirdDays, ref gBw, ref gFcr); gStart = d; }
                if (!weekly) { ResetGroup(ref gOpen, ref gMort, ref gCull, ref gSold, ref gClose, ref gFeed, ref gSoldKg, ref gBirdDays, ref gBw, ref gFcr); gStart = d; }
                currentWeek = wk; gAge = age;
                int open = 0, mort = 0, cull = 0, sold = 0, close = 0, present = 0, plc = 0; decimal feed = 0, bwSum = 0; int bwN = 0; bool any = false;
                foreach (var ld in led.Days)
                {
                    if (ld.Date != d || ld.FlockId != fm.FlockId || !scope.Includes(ld.ShedId, ld.FlockId, md)) continue;
                    any = true;
                    open += ld.Opening; mort += ld.Mort; cull += ld.Cull; sold += ld.Sold; close += ld.Closing; present += Math.Max(ld.Present, 0); plc += ld.Placed;
                    LogRow lg;
                    if (logs.TryGetValue(LogData.Key(ld.ShedId, d), out lg) && lg.FlockId == fm.FlockId)
                    {
                        feed += lg.FeedKg;
                        if (lg.BodyWt.HasValue) { bwSum += lg.BodyWt.Value; bwN++; }
                    }
                }
                if (!any) continue;
                placed += plc;
                decimal sk; saleKg.TryGetValue(d, out sk);
                cumFeed += feed; cumSoldKg += sk; cumDepl += mort + cull;
                if (gOpen < 0) gOpen = open;
                gMort += mort; gCull += cull; gSold += sold; gClose = close; gFeed += feed; gSoldKg += sk; gBirdDays += present;
                if (bwN > 0)
                {
                    decimal bw = Math.Round(bwSum / bwN, 0);
                    gBw = bw;
                    decimal live = close * bw / 1000m + cumSoldKg;
                    gFcr = live > 0 && cumFeed > 0 ? Math.Round(cumFeed / live, 3) : (decimal?)null;
                }
                else if (close == 0 && cumSoldKg > 0) gFcr = Math.Round(cumFeed / cumSoldKg, 3);
                if (!weekly) emit(null, age);
            }
            if (weekly && currentWeek >= 0) emit("Wk " + (currentWeek + 1), gAge);
            // daily gain column: fill from consecutive body weights
            decimal? lastW = null; int lastAge = 0;
            foreach (var row in s.Rows)
            {
                var w = row.Cells[14] as decimal?;
                if (row.Cells[14] is decimal) w = (decimal)row.Cells[14];
                int a = Convert.ToInt32(row.Cells[1]);
                if (w.HasValue)
                {
                    if (lastW.HasValue && a > lastAge) row.Cells[16] = Math.Round((w.Value - lastW.Value) / (a - lastAge), 1);
                    lastW = w; lastAge = a;
                }
            }
            chart.Series.Add(cBw); chart.Series.Add(cBwS); chart.Series.Add(cF); chart.Series.Add(cFS);
            m.Chart = chart;
            var b = BatchMetrics.Load(fm, to, std);
            BatchSummary(m, b, std.Daily(fm.BreedId, b.AvgAgeSold.HasValue ? (int)Math.Round(b.AvgAgeSold.Value) : b.AgeDays));
            m.Notes.Add("FCR = cumulative feed / (live weight lifted + birds in house x sampled body weight); shown on days with a body-weight sample. Std values are the breeder curve interpolated by day.");
            return m;
        }

        static void ResetGroup(ref int open, ref int mort, ref int cull, ref int sold, ref int close, ref decimal feed, ref decimal soldKg, ref long birdDays, ref decimal? bw, ref decimal? fcr)
        {
            open = -1; mort = 0; cull = 0; sold = 0; close = 0; feed = 0; soldKg = 0; birdDays = 0; bw = null; fcr = null;
        }

        // =====================================================================
        // B3. BROILER BATCH PROFIT & LOSS
        // =====================================================================
        public static ReportModel BatchPnL(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var scope = Scope.From(p);
            var m = Rep.New("brpl", "Broiler Batch Profit & Loss", "");
            FlockMeta fm;
            if (!p.FlockId.HasValue || !md.Flocks.TryGetValue(p.FlockId.Value, out fm) || !fm.IsBroiler || !FlockInScope(fm, scope, md))
            {
                m.Subtitle = "Select a broiler batch"; m.Notes.Add("Choose a broiler batch to see its profit and loss."); return m;
            }
            var to = BatchEnd(fm);
            m.Subtitle = BatchTitle(fm) + "   |   Accounts to " + Rep.D(to);
            var std = StandardBook.Load(cs.CompanyId);
            var rb = RateBook.Load(cs.CompanyId);
            var res = CostEngine.Compute(cs, md, rb, fm.ArrivalDate, to, false);
            var b = BatchMetrics.Load(fm, to, std);
            var pl = BatchPL.Build(b, res);
            decimal kg = b.LiveKg;

            var s = m.Section("Profit & Loss Account (Rs)");
            s.Col("Particulars", "T").Col("Quantity", "T", Align.Right).Col("Amount (Rs)", "N2").Col("Per Bird Placed", "N2").Col("Per Kg Live Wt", "N2");
            Func<decimal, decimal?> perBird = v => b.Placed > 0 ? Math.Round(v / b.Placed, 2) : (decimal?)null;
            Func<decimal, decimal?> perKg = v => kg > 0 ? Math.Round(v / kg, 2) : (decimal?)null;
            s.AddGroup("INCOME");
            s.Add("Broiler sales", Fmt.N(b.SoldBirds, 0) + " birds / " + Fmt.N(b.SoldKg, 1) + " kg", b.SalesAmt, perBird(b.SalesAmt), perKg(b.SalesAmt));
            if (b.OtherIncome != 0) s.Add("Other sales linked to batch", "", b.OtherIncome, perBird(b.OtherIncome), perKg(b.OtherIncome));
            s.AddSubtotal("Total income", "", pl.Income, perBird(pl.Income), perKg(pl.Income));
            s.AddGroup("EXPENDITURE");
            s.Add("Chicks", Fmt.N(fm.InitialCount, 0) + " @ " + Fmt.N(fm.ChickRate, 2), pl.Chicks, perBird(pl.Chicks), perKg(pl.Chicks));
            s.Add("Chick freight / charges", "", pl.Freight, perBird(pl.Freight), perKg(pl.Freight));
            s.Add("Feed", Fmt.N(b.FeedKg, 1) + " kg", pl.Feed, perBird(pl.Feed), perKg(pl.Feed));
            s.Add("Medicine & vaccines", "", pl.Med, perBird(pl.Med), perKg(pl.Med));
            s.Add("Labour (allocated)", "", pl.Labor, perBird(pl.Labor), perKg(pl.Labor));
            s.Add("Electricity / brooding / utility (allocated)", "", pl.Utility, perBird(pl.Utility), perKg(pl.Utility));
            s.Add("Overheads & other (allocated)", "", pl.Overhead, perBird(pl.Overhead), perKg(pl.Overhead));
            s.AddSubtotal("Total expenditure", "", pl.Expenses, perBird(pl.Expenses), perKg(pl.Expenses));
            s.AddTotal(pl.Profit >= 0 ? "NET PROFIT" : "NET LOSS", "", pl.Profit, perBird(pl.Profit), perKg(pl.Profit));
            if (b.BirdsNow > 0)
            {
                s.Add("Birds still in house (est. at avg rate/kg)", Fmt.N(b.BirdsNow, 0) + " birds / " + Fmt.N(b.InHouseKg, 1) + " kg", pl.StockValue, null, null);
                s.AddTotal("PROVISIONAL RESULT incl. birds in house", "", pl.Profit + pl.StockValue, perBird(pl.Profit + pl.StockValue), perKg(pl.Profit + pl.StockValue));
            }
            BatchSummary(m, b, std.Daily(fm.BreedId, b.AvgAgeSold.HasValue ? (int)Math.Round(b.AvgAgeSold.Value) : b.AgeDays));
            m.Summary("Unit Economics").Add("Production cost per kg live", Rep.N(perKg(pl.Expenses), 2))
                .Add("Feed cost per kg live", Rep.N(perKg(pl.Feed), 2)).Add("Feed cost per kg feed", Rep.N(b.FeedKg > 0 ? pl.Feed / b.FeedKg : (decimal?)null, 2))
                .Add("Average selling rate per kg", Rep.N(b.AvgRatePerKg, 2))
                .Add("Margin per kg", Rep.N(b.AvgRatePerKg.HasValue && kg > 0 ? b.AvgRatePerKg - pl.Expenses / kg : null, 2))
                .Add("Cost per bird placed", Rep.N(perBird(pl.Expenses), 2)).Add("Profit per bird placed", Rep.N(perBird(pl.Profit), 2));
            m.Summary("Collections").Add("Invoiced", Fmt.N(pl.Income, 2)).Add("Received", Fmt.N(b.Received, 2)).Add("Outstanding", Fmt.N(pl.Income - b.Received, 2));
            m.Notes.Add("Feed and medicine are valued at the item price in force on each day. Farm labour, electricity and overheads are shared with other batches on the farm in proportion to bird-days.");
            if (b.BirdsNow > 0) m.Notes.Add("The batch is still running: per-kg figures include birds in house at " + (b.BwIsStd ? "the standard weight for their age" : "the latest sample weight") + ".");
            if (res.MissingFeedRates > 0) m.Notes.Add("Warning: some daily entries use a feed without a price - enter it under Finance > Item Prices.");
            return m;
        }

        // =====================================================================
        // B4. BROILER P&L SUMMARY (all batches in a period)
        // =====================================================================
        public static ReportModel PnLSummary(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var scope = Scope.From(p);
            var m = Rep.New("brplsum", "Broiler Batch P&L Summary", "Batches active between " + Rep.D(p.From) + " and " + Rep.D(p.To) + "   |   " + Rep.ScopeText(p, md, false, false));
            var batches = md.Flocks.Values.Where(f => f.IsBroiler && f.ArrivalDate <= p.To && (!f.DepletionDate.HasValue || f.DepletionDate.Value >= p.From))
                            .OrderBy(f => f.ArrivalDate).Where(f => FlockInScope(f, scope, md)).ToList();
            var s = m.Section("Batch Results");
            s.Col("Batch", "T").Col("Shed(s)", "T").Col("Placed On", "D").Col("Closed On", "D").Col("Chicks", "N0").Col("Lifted", "N0").Col("Live Kg", "N0")
             .Col("Avg Wt Kg", "N3").Col("Avg Age", "N1").Col("Livability %", "N2").Col("FCR", "N3").Col("EPEF", "N0").Col("Income", "N0").Col("Expenses", "N0")
             .Col("Profit", "N0").Col("Profit/Bird", "N2").Col("Cost/Kg", "N2").Col("Rate/Kg", "N2");
            if (batches.Count == 0) { m.Notes.Add("No broiler batches in this period."); return m; }
            var std = StandardBook.Load(cs.CompanyId);
            var rb = RateBook.Load(cs.CompanyId);
            var from = batches.Min(f => f.ArrivalDate);
            var to = batches.Max(f => BatchEnd(f));
            var res = CostEngine.Compute(cs, md, rb, from, to, false);
            decimal tInc = 0, tExp = 0, tKg = 0, tSoldKg = 0, tSales = 0; int tPlaced = 0, tLifted = 0;
            var chart = new ChartSpec { Title = "Profit per batch (Rs)", YTitle = "Rs" };
            var cp = new ChartSeries("Profit") { Type = "bar" };
            foreach (var f in batches)
            {
                var end = BatchEnd(f);
                var b = BatchMetrics.Load(f, end, std);
                var pl = BatchPL.Build(b, res);
                var sheds = string.Join(", ", Db.Query("SELECT DISTINCT s.Code FROM dbo.vBirdMovement m JOIN dbo.Shed s ON s.ShedId = m.ShedId WHERE m.FlockId = @f",
                    Db.P("@f", f.FlockId)).Rows.Cast<DataRow>().Select(r => Db.Str(r["Code"])).ToArray());
                s.Add(f.Code + (f.Status == "Active" ? " *" : ""), sheds, f.ArrivalDate, f.DepletionDate, b.Placed, b.SoldBirds, b.LiveKg, b.AvgWtSold, b.AvgAgeSold, b.Livability,
                      b.FCR, b.EPEF, pl.Income, pl.Expenses, pl.Profit, b.Placed > 0 ? Math.Round(pl.Profit / b.Placed, 2) : (decimal?)null,
                      b.LiveKg > 0 ? Math.Round(pl.Expenses / b.LiveKg, 2) : (decimal?)null, b.AvgRatePerKg);
                tInc += pl.Income; tExp += pl.Expenses; tKg += b.LiveKg; tPlaced += b.Placed; tLifted += b.SoldBirds; tSoldKg += b.SoldKg; tSales += b.SalesAmt;
                chart.Labels.Add(f.Code); cp.Values.Add(Math.Round(pl.Profit, 0));
            }
            s.AddTotal("TOTAL", "", null, null, tPlaced, tLifted, tKg, tLifted > 0 ? Math.Round(tSoldKg / tLifted, 3) : (decimal?)null, null, null, null, null,
                       tInc, tExp, tInc - tExp, tPlaced > 0 ? Math.Round((tInc - tExp) / tPlaced, 2) : (decimal?)null,
                       tKg > 0 ? Math.Round(tExp / tKg, 2) : (decimal?)null, tSoldKg > 0 ? Math.Round(tSales / tSoldKg, 2) : (decimal?)null);
            chart.Series.Add(cp);
            m.Chart = chart;
            m.Summary("Totals").Add("Batches", batches.Count.ToString()).Add("Chicks placed", Fmt.N(tPlaced, 0)).Add("Birds lifted", Fmt.N(tLifted, 0))
                .Add("Income (Rs)", Fmt.N(tInc, 0)).Add("Expenses (Rs)", Fmt.N(tExp, 0)).Add((tInc - tExp) >= 0 ? "Net profit (Rs)" : "Net loss (Rs)", Fmt.N(tInc - tExp, 0));
            m.Notes.Add("* = batch still running: live kg includes birds in house, and the result is provisional until the batch is fully lifted.");
            m.Notes.Add("Expenses include chicks, feed, medicine and the batch's share of farm labour, utility and overheads (allocated on bird-days).");
            return m;
        }
    }
}
