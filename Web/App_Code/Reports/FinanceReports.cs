using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace PoultryERP.Reports
{
    /// <summary>Cost per Egg, Flock Cost of Production, Stock, Sales and Reconciliation reports.</summary>
    public static class FinanceReports
    {
        static string PeriodKey(DateTime d, string group, out DateTime start)
        {
            switch ((group ?? "").ToLowerInvariant())
            {
                case "daily":
                    start = d;
                    return d.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
                case "monthly":
                    start = new DateTime(d.Year, d.Month, 1);
                    return start.ToString("MMM yyyy", CultureInfo.InvariantCulture);
                default:
                    int diff = ((int)d.DayOfWeek + 6) % 7; // Monday start
                    start = d.AddDays(-diff);
                    return "Wk " + start.ToString("dd-MM-yy", CultureInfo.InvariantCulture);
            }
        }

        class Acc
        {
            public DateTime Start; public string Label;
            public long BirdDays; public int Saleable, Eggs;
            public decimal Feed, Grit, Med, Labor, Utility, Overhead, Dep;
            public decimal Operating { get { return Feed + Grit + Med + Labor + Utility + Overhead; } }
            public decimal Total { get { return Operating + Dep; } }
            public void Add(CostRow r)
            {
                BirdDays += r.BirdsPresent; Saleable += r.SaleableEggs; Eggs += r.TotalEggs;
                Feed += r.Feed; Grit += r.Grit; Med += r.Med; Labor += r.Labor; Utility += r.Utility; Overhead += r.Overhead + r.Other; Dep += r.Depreciation;
            }
        }

        static decimal? PerEgg(decimal v, int eggs) { return eggs > 0 ? Math.Round(v / eggs, 3) : (decimal?)null; }

        // =====================================================================
        // 8. COST PER EGG REPORT
        // =====================================================================
        public static ReportModel CostPerEgg(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var scope = Scope.From(p);
            var rb = RateBook.Load(cs.CompanyId);
            var m = Rep.New("costegg", "Cost per Egg Report", "Period: " + Rep.D(p.From) + " to " + Rep.D(p.To) + "   |   " + p.GroupBy + "   |   " + Rep.ScopeText(p, md, true, true));
            var res = CostEngine.Compute(cs, md, rb, p.From, p.To, true);

            var groups = new SortedDictionary<DateTime, Acc>();
            var all = new Acc();
            foreach (var r in res.Rows)
            {
                if (!r.Laying || !scope.Includes(r.ShedId, r.FlockId, md)) continue;
                DateTime st; var label = PeriodKey(r.Date, p.GroupBy, out st);
                Acc a; if (!groups.TryGetValue(st, out a)) { a = new Acc { Start = st, Label = label }; groups[st] = a; }
                a.Add(r); all.Add(r);
            }

            var s = m.Section("Cost Drivers per Period (laying sheds)");
            s.Col("Period", "T").Col("Hen-Days", "N0").Col("Saleable Eggs", "N0").Col("Feed", "N0").Col("Shell Grit", "N0").Col("Med & Vacc", "N0")
             .Col("Labour", "N0").Col("Utility", "N0").Col("Overheads", "N0").Col("Depreciation", "N0").Col("Operating Cost", "N0").Col("Total Cost", "N0")
             .Col("Feed / Egg", "N3").Col("Variable / Egg", "N3").Col("Fixed / Egg", "N3").Col("Op. Cost / Egg", "N3").Col("Total Cost / Egg", "N3");
            var chart = new ChartSpec { Title = "Cost per saleable egg (Rs)", YTitle = "Rs / egg" };
            var c1 = new ChartSeries("Operating cost / egg"); var c2 = new ChartSeries("Total cost / egg (incl. depreciation)"); var c3 = new ChartSeries("Feed / egg");
            foreach (var a in groups.Values)
            {
                s.Add(a.Label, a.BirdDays, a.Saleable, a.Feed, a.Grit, a.Med, a.Labor, a.Utility, a.Overhead, a.Dep, a.Operating, a.Total,
                      PerEgg(a.Feed, a.Saleable), PerEgg(a.Feed + a.Grit + a.Med, a.Saleable), PerEgg(a.Labor + a.Utility + a.Overhead + a.Dep, a.Saleable),
                      PerEgg(a.Operating, a.Saleable), PerEgg(a.Total, a.Saleable));
                chart.Labels.Add(a.Label); c1.Values.Add(PerEgg(a.Operating, a.Saleable)); c2.Values.Add(PerEgg(a.Total, a.Saleable)); c3.Values.Add(PerEgg(a.Feed, a.Saleable));
            }
            s.AddTotal("TOTAL", all.BirdDays, all.Saleable, all.Feed, all.Grit, all.Med, all.Labor, all.Utility, all.Overhead, all.Dep, all.Operating, all.Total,
                       PerEgg(all.Feed, all.Saleable), PerEgg(all.Feed + all.Grit + all.Med, all.Saleable), PerEgg(all.Labor + all.Utility + all.Overhead + all.Dep, all.Saleable),
                       PerEgg(all.Operating, all.Saleable), PerEgg(all.Total, all.Saleable));
            chart.Series.Add(c1); chart.Series.Add(c2); chart.Series.Add(c3);
            m.Chart = chart;

            decimal op = all.Operating;
            m.Summary("Cost per Saleable Egg").Add("Saleable eggs", Fmt.N(all.Saleable, 0))
                .Add("Operating cost / egg", Rep.N(PerEgg(op, all.Saleable), 3)).Add("Depreciation / egg", Rep.N(PerEgg(all.Dep, all.Saleable), 3))
                .Add("Total cost / egg", Rep.N(PerEgg(all.Total, all.Saleable), 3))
                .Add("Feed cost per kg feed", Rep.N(FeedKg(res, scope, md) > 0 ? all.Feed / FeedKg(res, scope, md) : (decimal?)null, 2));
            m.Summary("Operating Cost Structure").Add("Feed", Share(all.Feed, op)).Add("Shell grit", Share(all.Grit, op)).Add("Medicine & vaccines", Share(all.Med, op))
                .Add("Labour", Share(all.Labor, op)).Add("Utility", Share(all.Utility, op)).Add("Overheads & other", Share(all.Overhead, op))
                .Add("Operating cost (Rs)", Fmt.N(op, 0));
            m.Notes.Add("Cost per egg = (Feed + Shell grit + Medicine/vaccines + Labour + Utility/overheads) / saleable eggs. Saleable eggs = table + double yolk + misshapen" +
                        (cs.CrackedIsSaleable ? " + cracked" : " (cracked/broken excluded)") + ".");
            m.Notes.Add("Only laying-stage shed-days are included. Rearing costs (chick to pre-lay) are capitalised into the bird and recovered through depreciation over the planned laying life, net of expected salvage value.");
            m.Notes.Add("Farm-level overheads are allocated to sheds in proportion to bird-days. Feed, grit and medicine are valued at the price effective on each day.");
            if (res.UnallocatedOverhead > 0) m.Notes.Add("Rs " + Fmt.N(res.UnallocatedOverhead, 0) + " of overheads fell on days with no birds on the farm and could not be allocated.");
            if (res.MissingFeedRates > 0) m.Notes.Add("Warning: " + res.MissingFeedRates + " daily entries use a feed item without a price - enter it under Finance > Item Prices.");
            return m;
        }

        static decimal FeedKg(CostResult res, Scope scope, MasterData md)
        {
            decimal k = 0;
            foreach (var r in res.Rows) if (r.Laying && scope.Includes(r.ShedId, r.FlockId, md)) k += r.FeedKg;
            return k;
        }

        static string Share(decimal v, decimal total)
        {
            return Fmt.N(v, 0) + "  (" + (total > 0 ? Fmt.N(v * 100m / total, 1) : "0.0") + "%)";
        }

        // =====================================================================
        // 9. FLOCK COST OF PRODUCTION  (old: frmProdRep / crCost)
        // =====================================================================
        public static ReportModel FlockCost(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var m = Rep.New("flockcost", "Flock Cost of Production", "");
            FlockMeta fm;
            if (!p.FlockId.HasValue || !md.Flocks.TryGetValue(p.FlockId.Value, out fm))
            {
                m.Subtitle = "Select a flock";
                m.Notes.Add("Choose a flock to see its life-to-date cost sheet.");
                return m;
            }
            var from = fm.ArrivalDate;
            var to = DateTime.Today.AddDays(-1);
            if (fm.DepletionDate.HasValue && fm.DepletionDate.Value < to) to = fm.DepletionDate.Value;
            m.Subtitle = "Flock: " + fm.Code + "   |   Hatch: " + Rep.D(fm.HatchDate) + "   |   Arrival: " + Rep.D(fm.ArrivalDate) + "   |   Costed to: " + Rep.D(to);
            var rb = RateBook.Load(cs.CompanyId);
            var res = CostEngine.Compute(cs, md, rb, from, to < from ? from : to, true);

            var rear = new Acc(); var lay = new Acc();
            var sheds = new HashSet<int>();
            foreach (var r in res.Rows)
            {
                if (r.FlockId != fm.FlockId) continue;
                if (r.Laying) lay.Add(r); else rear.Add(r);
                if (r.BirdsPresent > 0) sheds.Add(r.ShedId);
            }
            int housed;
            if (!res.HousedBirds.TryGetValue(fm.FlockId, out housed)) housed = Rep.HenHoused(fm, cs);
            decimal acq = fm.InitialCount * fm.ChickRate;

            var s = m.Section("Cost Sheet (Rs)");
            s.Col("Cost Head", "T").Col("Rearing (Chick to Pre-Lay)", "N0").Col("Laying", "N0").Col("Total", "N0").Col("Per Bird Housed", "N2").Col("Laying Cost / Saleable Egg", "N3");
            Action<string, decimal, decimal> line = (name, r, l) =>
                s.Add(name, r, l, r + l, housed > 0 ? Math.Round((r + l) / housed, 2) : (decimal?)null, PerEgg(l, lay.Saleable));
            line("Chick / pullet purchase (" + Fmt.N(fm.InitialCount, 0) + " @ " + Fmt.N(fm.ChickRate, 2) + ")", acq, 0);
            line("Freight & arrival charges", fm.FreightCost, 0);
            line("Feed", rear.Feed, lay.Feed);
            line("Shell grit", rear.Grit, lay.Grit);
            line("Medicine & vaccines", rear.Med, lay.Med);
            line("Labour", rear.Labor, lay.Labor);
            line("Utility", rear.Utility, lay.Utility);
            line("Overheads & other", rear.Overhead, lay.Overhead);
            decimal rt = acq + fm.FreightCost + rear.Operating, lt = lay.Operating;
            s.AddTotal("TOTAL COST", rt, lt, rt + lt, housed > 0 ? Math.Round((rt + lt) / housed, 2) : (decimal?)null, PerEgg(lt, lay.Saleable));

            var cum = Db.Row("SELECT ISNULL(SUM(Mortality),0) M, ISNULL(SUM(Culls),0) C, ISNULL(SUM(BirdsSold),0) S, ISNULL(SUM(TotalEggs),0) E, ISNULL(SUM(FeedKg),0) F FROM dbo.DailyLog WHERE FlockId = @f AND LogDate <= @t",
                Db.P("@f", fm.FlockId), Db.P("@t", to));
            int transit = Db.Int(Db.Scalar("SELECT ISNULL(SUM(TransitMortality + TransitCulls),0) FROM dbo.FlockTransfer WHERE FlockId = @f AND Status = N'Approved' AND TransferDate <= @t",
                Db.P("@f", fm.FlockId), Db.P("@t", to)));
            int placed = fm.InitialCount - fm.ArrivalMortality;
            int now = BirdLedger.FlockBirdsBefore(fm.FlockId, to.AddDays(1));
            int activeSheds = Db.Int(Db.Scalar("SELECT COUNT(DISTINCT ShedId) FROM dbo.vShedOccupancy WHERE CompanyId = @c AND Birds > 0", Db.P("@c", cs.CompanyId)));

            m.Summary("Flock").Add("Flock / batch / lot", fm.Code + " / " + fm.BatchNo + " / " + fm.LotNo).Add("Breed", fm.BreedName).Add("Source", fm.Source)
                .Add("Age on " + Rep.D(to), Age.Text(fm.HatchDate, to)).Add("Stage", cs.StageOf(Age.Days(fm.HatchDate, to), fm.IsBroiler))
                .Add("Sheds used by flock", sheds.Count.ToString()).Add("Occupied sheds (company)", activeSheds.ToString());
            m.Summary("Birds").Add("Chicks received", Fmt.N(fm.InitialCount, 0)).Add("Arrival mortality", Fmt.N(fm.ArrivalMortality, 0))
                .Add("Placed", Fmt.N(placed, 0)).Add("Hens housed (start of lay)", Fmt.N(housed, 0))
                .Add("Mortality + culls", Fmt.N(Db.Int(cum["M"]) + Db.Int(cum["C"]), 0)).Add("Transit losses", Fmt.N(transit, 0))
                .Add("Birds sold", Fmt.N(Db.Int(cum["S"]), 0)).Add("Birds now", Fmt.N(now, 0))
                .Add("Livability on placed", Rep.N(placed > 0 ? (placed - Db.Int(cum["M"]) - Db.Int(cum["C"]) - transit) * 100m / placed : (decimal?)null, 2) + " %");
            decimal cap; res.CapitalisedPerBird.TryGetValue(fm.FlockId, out cap);
            decimal dep; res.DepPerBirdDay.TryGetValue(fm.FlockId, out dep);
            m.Summary("Production & Unit Cost").Add("Total eggs", Fmt.N(Db.Int(cum["E"]), 0)).Add("Saleable eggs (laying)", Fmt.N(lay.Saleable, 0))
                .Add("Eggs per hen housed", Rep.N(housed > 0 ? Db.Dec(cum["E"]) / housed : (decimal?)null, 1))
                .Add("Feed consumed (Kg)", Fmt.N(Db.Dec(cum["F"]), 0))
                .Add("Rearing cost per bird housed", cap > 0 ? Fmt.N(cap, 2) : Rep.N(housed > 0 ? rt / housed : (decimal?)null, 2))
                .Add("Depreciation per bird per day", Fmt.N(dep, 4))
                .Add("Laying operating cost / egg", Rep.N(PerEgg(lt, lay.Saleable), 3))
                .Add("Depreciation / egg", Rep.N(PerEgg(lay.Dep, lay.Saleable), 3))
                .Add("Total cost / egg", Rep.N(PerEgg(lt + lay.Dep, lay.Saleable), 3));
            m.Notes.Add("Rearing column = all costs up to the end of week " + cs.PreLayEndWk + " (pre-lay). Overheads are allocated to the flock on its share of farm bird-days.");
            m.Notes.Add("Depreciation per bird/day = (rearing cost per hen housed - expected salvage Rs " + Fmt.N(fm.SalvagePerBird, 2) + ") / laying days to week " + fm.PlannedDepletionWeek + ".");
            return m;
        }

        // =====================================================================
        // 10. FEED & MEDICINE STOCK  (old: frmMediStock / CrMediStock)
        // =====================================================================
        public static ReportModel Stock(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            string cat = string.IsNullOrEmpty(p.Product) ? null : p.Product;
            var m = Rep.New("stock", "Feed & Medicine Stock", "Period: " + Rep.D(p.From) + " to " + Rep.D(p.To) + "   |   " + (cat ?? "All categories") + "   |   " + Rep.ScopeText(p, md, false, false));
            var rb = RateBook.Load(cs.CompanyId);
            var open = new Dictionary<int, decimal>();
            foreach (DataRow r in Db.Query(@"SELECT ItemId, SUM(Qty) Q FROM dbo.vStockMovement WHERE CompanyId = @c AND MoveDate < @f AND (@farm IS NULL OR FarmId = @farm) GROUP BY ItemId",
                                           Db.P("@c", cs.CompanyId), Db.P("@f", p.From), Db.P("@farm", p.FarmId)).Rows)
                open[Db.Int(r["ItemId"])] = Db.Dec(r["Q"]);
            var mv = new Dictionary<string, decimal>();
            foreach (DataRow r in Db.Query(@"SELECT ItemId, Kind, SUM(Qty) Q FROM dbo.vStockMovement WHERE CompanyId = @c AND MoveDate BETWEEN @f AND @t AND (@farm IS NULL OR FarmId = @farm) GROUP BY ItemId, Kind",
                                           Db.P("@c", cs.CompanyId), Db.P("@f", p.From), Db.P("@t", p.To), Db.P("@farm", p.FarmId)).Rows)
                mv[Db.Int(r["ItemId"]) + "|" + Db.Str(r["Kind"])] = Db.Dec(r["Q"]);

            var s = m.Section("Stock Position");
            s.Col("Code", "T").Col("Item", "T").Col("Unit", "T", Align.Center).Col("Opening", "N2").Col("Received", "N2").Col("Feed Used", "N2")
             .Col("Grit Used", "N2").Col("Medication Used", "N2").Col("Closing", "N2").Col("Rate", "N2").Col("Value (Rs)", "N0");
            string lastCat = null; decimal tVal = 0; int negatives = 0;
            foreach (DataRow r in Db.Query("SELECT ItemId, Code, Name, Category, Unit FROM dbo.Item WHERE CompanyId = @c AND (@cat IS NULL OR Category = @cat) ORDER BY Category, Name",
                                           Db.P("@c", cs.CompanyId), Db.P("@cat", cat)).Rows)
            {
                int id = Db.Int(r["ItemId"]);
                decimal o; open.TryGetValue(id, out o);
                decimal rec, fu, gu, mu;
                mv.TryGetValue(id + "|RECEIPT", out rec); mv.TryGetValue(id + "|FEED", out fu); mv.TryGetValue(id + "|GRIT", out gu); mv.TryGetValue(id + "|MEDICATION", out mu);
                if (o == 0 && rec == 0 && fu == 0 && gu == 0 && mu == 0) continue;
                var category = Db.Str(r["Category"]);
                if (category != lastCat) { s.AddGroup(category); lastCat = category; }
                decimal close = o + rec + fu + gu + mu;
                decimal rate = rb.Rate(id, p.To);
                decimal val = Math.Max(close, 0) * rate;
                if (close < 0) negatives++;
                tVal += val;
                s.Add(Db.Str(r["Code"]), Db.Str(r["Name"]), Db.Str(r["Unit"]), o, rec, -fu, -gu, -mu, close, rate, val);
            }
            s.AddTotal("TOTAL VALUE", "", "", null, null, null, null, null, null, null, tVal);
            m.Summary("Valuation").Add("Stock value (Rs)", Fmt.N(tVal, 0)).Add("Valued at", "Item price effective " + Rep.D(p.To));
            m.Notes.Add("Consumption is taken automatically from daily shed entries (feed, shell grit) and medication records marked Done.");
            if (negatives > 0) m.Notes.Add(negatives + " item(s) show negative stock - receipts have probably not been entered under Finance > Stock Receipts.");
            return m;
        }

        // =====================================================================
        // 11. EGGS / BIRD SALE REGISTER  (old: frmItemSale / CrItemSale)
        // =====================================================================
        public static ReportModel Sales(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            string prod = string.IsNullOrEmpty(p.Product) ? null : p.Product;
            var m = Rep.New("sales", "Eggs / Bird Sale Register", "Period: " + Rep.D(p.From) + " to " + Rep.D(p.To) + "   |   " + (prod ?? "All products") + "   |   " + Rep.ScopeText(p, md, false, false));
            var dt = Db.Query(@"SELECT s.*, c.Name AS Cust, sh.Code AS ShedCode FROM dbo.Sale s LEFT JOIN dbo.Customer c ON c.CustomerId = s.CustomerId
                                LEFT JOIN dbo.Shed sh ON sh.ShedId = s.ShedId
                                WHERE s.CompanyId = @c AND s.SaleDate BETWEEN @f AND @t AND (@farm IS NULL OR s.FarmId = @farm) AND (@p IS NULL OR s.Product = @p)
                                ORDER BY s.SaleDate, s.SaleId",
                                Db.P("@c", cs.CompanyId), Db.P("@f", p.From), Db.P("@t", p.To), Db.P("@farm", p.FarmId), Db.P("@p", prod));
            var s = m.Section("Sales");
            s.Col("Date", "D").Col("Invoice", "T").Col("Customer", "T").Col("Product", "T").Col("Shed", "T").Col("Qty", "N0").Col("Kg", "N2")
             .Col("Rate", "N2").Col("Amount", "N2").Col("Received", "N2").Col("Balance", "N2");
            decimal tA = 0, tR = 0;
            var byProd = new SortedDictionary<string, decimal[]>();
            foreach (DataRow r in dt.Rows)
            {
                decimal amt = Db.Dec(r["Amount"]), rec = Db.Dec(r["AmountReceived"]), q = Db.Dec(r["Qty"]);
                s.Add(Convert.ToDateTime(r["SaleDate"]), Db.Str(r["InvoiceNo"]), Db.Str(r["Cust"]), Db.Str(r["Product"]), Db.Str(r["ShedCode"]),
                      q, Db.NDec(r["QtyKg"]), Db.Dec(r["Rate"]), amt, rec, amt - rec);
                tA += amt; tR += rec;
                decimal[] v; var pr = Db.Str(r["Product"]);
                if (!byProd.TryGetValue(pr, out v)) { v = new decimal[3]; byProd[pr] = v; }
                v[0] += q; v[1] += amt; v[2] += rec;
            }
            s.AddTotal("TOTAL", "", "", "", "", null, null, null, tA, tR, tA - tR);
            var ps = m.Section("Product Summary");
            ps.Col("Product", "T").Col("Qty", "N0").Col("Amount", "N2").Col("Received", "N2").Col("Outstanding", "N2").Col("Avg Rate", "N3");
            foreach (var kv in byProd) ps.Add(kv.Key, kv.Value[0], kv.Value[1], kv.Value[2], kv.Value[1] - kv.Value[2], kv.Value[0] > 0 ? Math.Round(kv.Value[1] / kv.Value[0], 3) : (decimal?)null);
            ps.AddTotal("TOTAL", null, tA, tR, tA - tR, null);
            return m;
        }

        // =====================================================================
        // 12. EGG & REVENUE RECONCILIATION
        // =====================================================================
        public static ReportModel Reconciliation(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var m = Rep.New("recon", "Egg & Revenue Reconciliation", "Period: " + Rep.D(p.From) + " to " + Rep.D(p.To) + "   |   " + Rep.ScopeText(p, md, false, false));
            int opening = Rep.EggStockBefore(cs.CompanyId, p.FarmId, p.From);
            var prod = Db.Row(@"SELECT ISNULL(SUM(d.TableEggs),0) T, ISNULL(SUM(d.CrackedEggs),0) C, ISNULL(SUM(d.MisshapenEggs),0) M, ISNULL(SUM(d.DoubleYolkEggs),0) D
                                FROM dbo.DailyLog d JOIN dbo.Shed s ON s.ShedId = d.ShedId JOIN dbo.Location l ON l.LocationId = s.LocationId
                                WHERE d.CompanyId = @c AND d.LogDate BETWEEN @f AND @t AND (@farm IS NULL OR l.FarmId = @farm)",
                                Db.P("@c", cs.CompanyId), Db.P("@f", p.From), Db.P("@t", p.To), Db.P("@farm", p.FarmId));
            var sales = Db.Query(@"SELECT Product, SUM(Qty) Q, SUM(Amount) A, SUM(AmountReceived) R FROM dbo.Sale
                                   WHERE CompanyId = @c AND SaleDate BETWEEN @f AND @t AND (@farm IS NULL OR FarmId = @farm) GROUP BY Product ORDER BY Product",
                                   Db.P("@c", cs.CompanyId), Db.P("@f", p.From), Db.P("@t", p.To), Db.P("@farm", p.FarmId));
            int waste = Db.Int(Db.Scalar("SELECT ISNULL(SUM(Qty),0) FROM dbo.EggWastage WHERE CompanyId = @c AND WasteDate BETWEEN @f AND @t AND (@farm IS NULL OR FarmId = @farm)",
                Db.P("@c", cs.CompanyId), Db.P("@f", p.From), Db.P("@t", p.To), Db.P("@farm", p.FarmId)));

            var e = m.Section("Egg Stock Reconciliation");
            e.Col("Particulars", "T").Col("Eggs", "N0").Col("Trays", "N0");
            e.Add("Opening stock on " + Rep.D(p.From), opening, Rep.EggTrays(opening));
            int t = Db.Int(prod["T"]), c = Db.Int(prod["C"]), mi = Db.Int(prod["M"]), dy = Db.Int(prod["D"]), tp = t + c + mi + dy;
            e.Add("  + Table eggs produced", t, Rep.EggTrays(t));
            e.Add("  + Cracked / broken produced", c, Rep.EggTrays(c));
            e.Add("  + Misshapen / thin-shell produced", mi, Rep.EggTrays(mi));
            e.Add("  + Double yolk produced", dy, Rep.EggTrays(dy));
            e.AddSubtotal("Total production", tp, Rep.EggTrays(tp));
            int soldEggs = 0; decimal eggRevenue = 0, otherRevenue = 0;
            foreach (DataRow r in sales.Rows)
            {
                var pr = Db.Str(r["Product"]);
                if (pr == "Broiler Bird") continue;
                if (Array.IndexOf(Rep.EggProducts, pr) < 0) { otherRevenue += Db.Dec(r["A"]); continue; }
                int q = (int)Db.Dec(r["Q"]);
                soldEggs += q; eggRevenue += Db.Dec(r["A"]);
                e.Add("  - Sold: " + pr, -q, -Rep.EggTrays(q));
            }
            e.Add("  - Wastage", -waste, -Rep.EggTrays(waste));
            int closing = opening + tp - soldEggs - waste;
            e.AddTotal("Closing book stock on " + Rep.D(p.To), closing, Rep.EggTrays(closing));

            var rv = m.Section("Revenue");
            rv.Col("Product", "T").Col("Qty", "N0").Col("Amount", "N2").Col("Received", "N2").Col("Outstanding", "N2").Col("Avg Rate", "N3");
            decimal tA = 0, tR = 0;
            foreach (DataRow r in sales.Rows)
            {
                decimal q = Db.Dec(r["Q"]), a = Db.Dec(r["A"]), rc = Db.Dec(r["R"]);
                rv.Add(Db.Str(r["Product"]), q, a, rc, a - rc, q > 0 ? Math.Round(a / q, 3) : (decimal?)null);
                tA += a; tR += rc;
            }
            rv.AddTotal("TOTAL", null, tA, tR, tA - tR, null);

            var scope = new Scope { FarmId = p.FarmId };
            var res = CostEngine.Compute(cs, md, RateBook.Load(cs.CompanyId), p.From, p.To, true);
            var lay = new Acc(); var rear = new Acc();
            foreach (var r in res.Rows)
            {
                if (!scope.Shed(r.ShedId, md)) continue;
                FlockMeta rf; if (md.Flocks.TryGetValue(r.FlockId, out rf) && rf.IsBroiler) continue; // broilers: see Broiler P&L
                if (r.Laying) lay.Add(r); else rear.Add(r);
            }
            var cv = m.Section("Cost versus Revenue (Rs)");
            cv.Col("Particulars", "T").Col("Amount", "N0").Col("Per Saleable Egg", "N3");
            cv.Add("Egg revenue", eggRevenue, PerEgg(eggRevenue, lay.Saleable));
            cv.Add("Other revenue (birds, manure, other)", otherRevenue, null);
            cv.Add("Laying operating cost", -lay.Operating, PerEgg(-lay.Operating, lay.Saleable));
            cv.Add("Bird depreciation", -lay.Dep, PerEgg(-lay.Dep, lay.Saleable));
            cv.AddTotal("Laying margin", eggRevenue + otherRevenue - lay.Total, PerEgg(eggRevenue + otherRevenue - lay.Total, lay.Saleable));
            cv.Add("Rearing cost incurred (capitalised, not in margin)", rear.Operating, null);

            m.Summary("Key Figures").Add("Eggs produced", Fmt.N(tp, 0)).Add("Eggs sold", Fmt.N(soldEggs, 0)).Add("Wastage %", Rep.N(Rep.Pct(waste, tp), 2))
                .Add("Average realisation / egg", Rep.N(soldEggs > 0 ? eggRevenue / soldEggs : (decimal?)null, 3))
                .Add("Total cost / saleable egg", Rep.N(PerEgg(lay.Total, lay.Saleable), 3))
                .Add("Collection efficiency", Rep.N(Rep.Pct(tR, tA), 1) + " %");
            m.Notes.Add("Broiler batches are excluded from the cost-versus-revenue section - see the Broiler P&L reports. Broiler sales still appear in the Revenue table.");
            m.Notes.Add("Closing book stock should match the physical count in the egg store; investigate any difference (breakage, unrecorded sales).");
            return m;
        }
    }
}
