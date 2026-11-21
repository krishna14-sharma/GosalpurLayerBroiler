using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PoultryERP.Reports
{
    /// <summary>Daily Transaction, Daily Supervisor, Chicks/Grower and Flock Transaction (II) reports.</summary>
    public static class DailyReports
    {
        class Line
        {
            public LedgerDay L;
            public LogRow Log;
            public ShedMeta Shed;
            public FlockMeta Flock;
            public int AgeDays;
            public StdRow Std;
        }

        static List<Line> Lines(CompanySettings cs, MasterData md, Scope scope, BirdLedger led, Dictionary<string, LogRow> logs, StandardBook std, DateTime d)
        {
            var list = new List<Line>();
            var seen = new HashSet<string>();
            foreach (var ld in led.Days)
            {
                if (ld.Date != d || !scope.Includes(ld.ShedId, ld.FlockId, md)) continue;
                LogRow lg; logs.TryGetValue(LogData.Key(ld.ShedId, d), out lg);
                if (lg != null && lg.FlockId != ld.FlockId) lg = null;
                if (ld.Opening <= 0 && ld.Present <= 0 && lg == null) continue;
                list.Add(Mk(cs, md, std, ld, lg, d));
                seen.Add(ld.ShedId + "|" + ld.FlockId);
            }
            foreach (var lg in logs.Values)
            {
                if (lg.Date != d || seen.Contains(lg.ShedId + "|" + lg.FlockId) || !scope.Includes(lg.ShedId, lg.FlockId, md)) continue;
                list.Add(Mk(cs, md, std, new LedgerDay { ShedId = lg.ShedId, FlockId = lg.FlockId, Date = d }, lg, d));
            }
            list.Sort((a, b) =>
            {
                int c = string.Compare(a.Shed == null ? "" : a.Shed.FarmName, b.Shed == null ? "" : b.Shed.FarmName, StringComparison.OrdinalIgnoreCase);
                return c != 0 ? c : string.Compare(a.Shed == null ? "" : a.Shed.Code, b.Shed == null ? "" : b.Shed.Code, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        static Line Mk(CompanySettings cs, MasterData md, StandardBook std, LedgerDay ld, LogRow lg, DateTime d)
        {
            var x = new Line { L = ld, Log = lg };
            md.Sheds.TryGetValue(ld.ShedId, out x.Shed);
            md.Flocks.TryGetValue(ld.FlockId, out x.Flock);
            if (x.Flock != null)
            {
                x.AgeDays = Age.Days(x.Flock.HatchDate, d);
                x.Std = std.Get(x.Flock.BreedId, x.AgeDays / 7);
            }
            return x;
        }

        static string FeedStageKey(Line x, CompanySettings cs)
        {
            if (x.Log != null && !string.IsNullOrEmpty(x.Log.FeedStage)) return x.Log.FeedStage;
            return cs.StageOf(x.AgeDays, x.Flock != null && x.Flock.IsBroiler);
        }

        // =====================================================================
        // 1. DAILY TRANSACTION REPORT  (old: frmTransRepMain / crTrans)
        // =====================================================================
        public static ReportModel Dtr(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var scope = Scope.From(p);
            var d = p.Date.Date;
            var led = BirdLedger.Load(cs.CompanyId, d, d);
            var logs = LogData.Load(cs.CompanyId, d, d);
            var std = StandardBook.Load(cs.CompanyId);
            var m = Rep.New("dtr", "Daily Transaction Report", "Date: " + Rep.D(d) + "   |   " + Rep.ScopeText(p, md, false, false));
            // youngest birds first (age ascending), then shed code; farms stay together when several are shown
            var lines = Lines(cs, md, scope, led, logs, std, d)
                .OrderBy(x => x.Shed == null ? "" : x.Shed.FarmName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Flock == null ? int.MaxValue : x.AgeDays)
                .ThenBy(x => x.Shed == null ? "" : x.Shed.Code, StringComparer.OrdinalIgnoreCase).ToList();

            var a = m.Section("A. Bird Inventory & Feed");
            a.Col("Shed", "T").Col("Flock", "T").Col("Age (W-D)", "T", Align.Center).Col("Stage", "T")
             .Col("Opening", "N0").Col("Mort", "N0").Col("Culls", "N0").Col("Sold", "N0").Col("Trf In", "N0").Col("Trf Out", "N0")
             .Col("Closing", "N0").Col("Mort %", "N3").Col("Feed Type", "T").Col("Feed (Kg)", "N1").Col("g/Bird", "N1")
             .Col("Std g/Bird", "N1").Col("Grit (Kg)", "N1").Col("Body Wt (g)", "N0").Col("Std Wt (g)", "N0");

            var b = m.Section("B. Egg Production");
            b.Col("Shed", "T").Col("Flock", "T").Col("Age (W-D)", "T", Align.Center).Col("Hens", "N0").Col("Table", "N0")
             .Col("Cracked", "N0").Col("Misshapen", "N0").Col("Double Yolk", "N0").Col("Total Eggs", "N0").Col("Trays", "N0")
             .Col("HD %", "N2").Col("Std HD %", "N2").Col("Diff", "N2").Col("Egg Wt (g)", "N1").Col("Std EW (g)", "N1");

            var c = m.Section("C. Water & Environment");
            c.Col("Shed", "T").Col("Water (L)", "N0").Col("ml/Bird", "N0").Col("Water:Feed", "N2").Col("Temp Min (C)", "N1")
             .Col("Temp Max (C)", "N1").Col("Humidity %", "N0").Col("Remarks", "T");

            int tOpen = 0, tMort = 0, tCull = 0, tSold = 0, tIn = 0, tOut = 0, tClose = 0, tHensLay = 0, tTable = 0, tCrack = 0, tMis = 0, tDY = 0, tHensFeed = 0;
            decimal tFeed = 0, tGrit = 0, tWater = 0;
            var feedByStage = new Dictionary<string, decimal> { { "Chick", 0 }, { "Grower", 0 }, { "Pre-Lay", 0 }, { "Layer", 0 } };
            string lastFarm = null;
            bool multiFarm = lines.Select(x => x.Shed == null ? 0 : x.Shed.FarmId).Distinct().Count() > 1;

            foreach (var x in lines)
            {
                var ld = x.L; var lg = x.Log;
                string shed = x.Shed == null ? "#" + ld.ShedId : x.Shed.Code;
                string flock = x.Flock == null ? "" : x.Flock.Code;
                string age = x.Flock == null ? "" : Age.Text(x.Flock.HatchDate, d);
                string stage = cs.StageOf(x.AgeDays, x.Flock != null && x.Flock.IsBroiler);
                if (multiFarm && x.Shed != null && x.Shed.FarmName != lastFarm)
                {
                    a.AddGroup(x.Shed.FarmName); b.AddGroup(x.Shed.FarmName); c.AddGroup(x.Shed.FarmName);
                    lastFarm = x.Shed.FarmName;
                }
                int hens = Math.Max(ld.Present, 0);
                decimal feed = lg == null ? 0 : lg.FeedKg;
                a.Add(shed, flock, age, stage, ld.Opening, ld.Mort, ld.Cull, ld.Sold, ld.Placed + ld.TrfIn, ld.TrfOut, ld.Closing,
                      Rep.Pct(ld.Mort + ld.Cull, hens), lg == null ? "(no entry)" : lg.FeedName, lg == null ? (decimal?)null : feed,
                      hens > 0 && lg != null ? Math.Round(feed * 1000m / hens, 1) : (decimal?)null,
                      x.Std == null ? null : x.Std.FeedG, lg == null ? (decimal?)null : lg.GritKg,
                      lg == null ? null : lg.BodyWt, x.Std == null ? null : x.Std.BodyWt);

                tOpen += ld.Opening; tMort += ld.Mort; tCull += ld.Cull; tSold += ld.Sold; tIn += ld.Placed + ld.TrfIn; tOut += ld.TrfOut; tClose += ld.Closing;
                if (lg != null) { tFeed += feed; tGrit += lg.GritKg; tHensFeed += hens; feedByStage[FeedStageKey(x, cs)] = feedByStage.ContainsKey(FeedStageKey(x, cs)) ? feedByStage[FeedStageKey(x, cs)] + feed : feed; }

                bool laying = cs.IsLaying(x.AgeDays, x.Flock != null && x.Flock.IsBroiler) || (lg != null && lg.TotalEggs > 0);
                if (laying)
                {
                    int tot = lg == null ? 0 : lg.TotalEggs;
                    decimal? hd = Rep.Pct(tot, hens);
                    b.Add(shed, flock, age, hens, lg == null ? 0 : lg.Table, lg == null ? 0 : lg.Cracked, lg == null ? 0 : lg.Misshapen,
                          lg == null ? 0 : lg.DoubleYolk, tot, Rep.EggTrays(tot), hd, x.Std == null ? null : x.Std.HDP,
                          Rep.Diff(hd, x.Std == null ? null : x.Std.HDP), lg == null ? null : lg.EggWt, x.Std == null ? null : x.Std.EggWt);
                    tHensLay += hens;
                    if (lg != null) { tTable += lg.Table; tCrack += lg.Cracked; tMis += lg.Misshapen; tDY += lg.DoubleYolk; }
                }

                decimal? water = lg == null ? null : lg.Water;
                if (water.HasValue) tWater += water.Value;
                c.Add(shed, water, water.HasValue && hens > 0 ? Math.Round(water.Value * 1000m / hens, 0) : (decimal?)null,
                      water.HasValue && feed > 0 ? Math.Round(water.Value / feed, 2) : (decimal?)null,
                      lg == null ? null : lg.TempMin, lg == null ? null : lg.TempMax, lg == null ? null : lg.Humidity, lg == null ? "" : lg.Remarks);
            }

            a.AddTotal("TOTAL", "", "", "", tOpen, tMort, tCull, tSold, tIn, tOut, tClose, Rep.Pct(tMort + tCull, tOpen + tIn - tOut), "",
                       tFeed, tHensFeed > 0 ? Math.Round(tFeed * 1000m / tHensFeed, 1) : (decimal?)null, null, tGrit, null, null);
            int tEggs = tTable + tCrack + tMis + tDY;
            b.AddTotal("TOTAL", "", "", tHensLay, tTable, tCrack, tMis, tDY, tEggs, Rep.EggTrays(tEggs), Rep.Pct(tEggs, tHensLay), null, null, null, null);
            c.AddTotal("TOTAL", tWater, null, tFeed > 0 ? Math.Round(tWater / tFeed, 2) : (decimal?)null, null, null, null, "");

            // ---- egg stock (farm level, as in the old report footer) ----
            int opn = Rep.EggStockBefore(cs.CompanyId, p.FarmId, d);
            var prodRow = Db.Row(@"SELECT ISNULL(SUM(d.TotalEggs),0) AS Tot, ISNULL(SUM(d.TableEggs + d.DoubleYolkEggs),0) AS Good, ISNULL(SUM(d.CrackedEggs),0) AS Crk
                                   FROM dbo.DailyLog d JOIN dbo.Shed s ON s.ShedId = d.ShedId JOIN dbo.Location l ON l.LocationId = s.LocationId
                                   WHERE d.CompanyId = @c AND d.LogDate = @d AND (@farm IS NULL OR l.FarmId = @farm)",
                                   Db.P("@c", cs.CompanyId), Db.P("@d", d), Db.P("@farm", p.FarmId));
            var sold = Db.Query(@"SELECT Product, SUM(Qty) AS Q FROM dbo.Sale WHERE CompanyId = @c AND SaleDate = @d AND (@farm IS NULL OR FarmId = @farm)
                                  AND Product IN (N'Table Egg', N'Cracked Egg', N'Double Yolk', N'Misshapen Egg') GROUP BY Product",
                                  Db.P("@c", cs.CompanyId), Db.P("@d", d), Db.P("@farm", p.FarmId));
            int soldGood = 0, soldCrack = 0, soldOther = 0;
            foreach (DataRow r in sold.Rows)
            {
                int q = (int)Db.Dec(r["Q"]);
                var pr = Db.Str(r["Product"]);
                if (pr == "Table Egg" || pr == "Double Yolk") soldGood += q; else if (pr == "Cracked Egg") soldCrack += q; else soldOther += q;
            }
            int waste = Db.Int(Db.Scalar("SELECT ISNULL(SUM(Qty),0) FROM dbo.EggWastage WHERE CompanyId = @c AND WasteDate = @d AND (@farm IS NULL OR FarmId = @farm)",
                Db.P("@c", cs.CompanyId), Db.P("@d", d), Db.P("@farm", p.FarmId)));
            int prod = Db.Int(prodRow["Tot"]);
            int closing = opn + prod - soldGood - soldCrack - soldOther - waste;
            m.Summary("Egg Stock (nos.)")
                .Add("Opening stock", Fmt.N(opn, 0))
                .Add("Production today", Fmt.N(prod, 0))
                .Add("Sold - table / double yolk", Fmt.N(soldGood, 0))
                .Add("Sold - cracked / broken", Fmt.N(soldCrack, 0))
                .Add("Sold - other grades", Fmt.N(soldOther, 0))
                .Add("Wastage", Fmt.N(waste, 0))
                .Add("Closing stock", Fmt.N(closing, 0))
                .Add("Closing stock (trays of " + cs.EggsPerTray + ")", Fmt.N(Rep.EggTrays(closing), 0) + " + " + (cs.EggsPerTray > 0 ? closing % cs.EggsPerTray : 0));

            var fs = m.Summary("Feed Consumption by Stage (Kg)");
            foreach (var kv in feedByStage) fs.Add(kv.Key, Fmt.N(kv.Value, 1));
            fs.Add("Total feed", Fmt.N(tFeed, 1)).Add("Shell grit", Fmt.N(tGrit, 1));

            m.Summary("Birds")
                .Add("Opening", Fmt.N(tOpen, 0)).Add("Placed / transferred in", Fmt.N(tIn, 0)).Add("Transferred out", Fmt.N(tOut, 0))
                .Add("Mortality", Fmt.N(tMort, 0)).Add("Culls", Fmt.N(tCull, 0)).Add("Sold", Fmt.N(tSold, 0)).Add("Closing", Fmt.N(tClose, 0))
                .Add("Laying HD % (all layer sheds)", Rep.N(Rep.Pct(tEggs, tHensLay), 2));

            int missing = lines.Count(x => x.Log == null && x.L.Present > 0);
            if (missing > 0) m.Notes.Add(missing + " occupied shed(s) have no daily entry for " + Rep.D(d) + " - shown as '(no entry)'.");
            m.Notes.Add("Age is shown as completed weeks-days from hatch date. HD % = total eggs / hens present x 100. Std values from the breed standard for the completed week of age.");
            return m;
        }

        // =====================================================================
        // 2. DAILY SUPERVISOR REPORT  (old: frmTransRep / crSupervisor)
        // =====================================================================
        public static ReportModel Supervisor(ReportParams p)
        {
            // Layout follows the farm's Excel "DAILY SUPERVISOR REPORT":
            // FLOCK No. | SHED NO. | AGE | OPENING CHICKS | %PROD EGGS | %STAND EGGS PROD | BAL FEED/DAY(gm) | EGG WT. | STD EGG WT
            // | BODY WT. | STAND BODY WT. | eggs | feed, then farm production %, wastage %, today vs previous day,
            // birds by stage (lay / chick / grower-prelay), feed per bird / per egg, feed rate, feed cost and egg production cost.
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var scope = Scope.From(p);
            var d = p.Date.Date;
            var led = BirdLedger.Load(cs.CompanyId, d, d);
            var logs = LogData.Load(cs.CompanyId, d, d);
            var std = StandardBook.Load(cs.CompanyId);
            var rb = RateBook.Load(cs.CompanyId);
            var m = Rep.New("supervisor", "Daily Supervisor Report", "Transaction Date: " + Rep.D(d) + "   |   " + Rep.ScopeText(p, md, false, false));
            m.Landscape = false; m.OnePage = true;    // prints on one A4 portrait page, like the farm's Excel
            var lines = Lines(cs, md, scope, led, logs, std, d);

            var s = m.Section("");
            s.Col("Flock No.", "T").Col("Shed No.", "T").Col("Age (W-D)", "T", Align.Center).Col("Opening Birds", "N0")
             .Col("% Prod Eggs", "N2").Col("% Std Eggs Prod", "N2").Col("Feed/Bird/Day (g)", "N2").Col("Egg Wt (g)", "N2")
             .Col("Std Egg Wt (g)", "N2").Col("Body Wt (Kg)", "N3").Col("Std Body Wt (Kg)", "N3").Col("Eggs (Nos.)", "N0").Col("Feed (Kg)", "N2");

            // one row per shed of the farm, in shed order, including empty sheds (as in the Excel layout)
            var byShed = new Dictionary<int, List<Line>>();
            foreach (var x in lines) { List<Line> l; if (!byShed.TryGetValue(x.L.ShedId, out l)) { l = new List<Line>(); byShed[x.L.ShedId] = l; } l.Add(x); }
            var sheds = md.Sheds.Values.Where(sh => scope.Shed(sh.ShedId, md) && byShed.ContainsKey(sh.ShedId))   // only sheds holding birds (empty sheds left out to fit one page)
                          .OrderBy(sh => sh.FarmName, StringComparer.OrdinalIgnoreCase)
                          // youngest birds first (age ascending); empty sheds at the end of each farm
                          .ThenBy(sh => byShed.ContainsKey(sh.ShedId) ? byShed[sh.ShedId].Min(x => x.Flock == null ? int.MaxValue : x.AgeDays) : int.MaxValue)
                          .ThenBy(sh => sh.Code, StringComparer.OrdinalIgnoreCase).ToList();
            bool multiFarm = sheds.Select(sh => sh.FarmId).Distinct().Count() > 1;
            string lastFarm = null;

            int layOpen = 0, layMort = 0, layCull = 0, layClose = 0, chOpen = 0, chMort = 0, chCull = 0, chClose = 0, grOpen = 0, grMort = 0, grCull = 0, grClose = 0;
            int tEggs = 0, tMort = 0, tCull = 0, tBroken = 0;
            decimal layFeed = 0, cgFeed = 0, layFeedCost = 0, layFeedPriced = 0;
            foreach (var sh in sheds)
            {
                if (multiFarm && sh.FarmName != lastFarm) { s.AddGroup(sh.FarmName); lastFarm = sh.FarmName; }
                List<Line> ls;
                if (!byShed.TryGetValue(sh.ShedId, out ls)) { s.Add("", sh.Code, "", null, null, null, null, null, null, null, null, null, null); continue; }
                foreach (var x in ls)
                {
                    var fm = x.Flock; var lg = x.Log; var ld = x.L;
                    bool broiler = fm != null && fm.IsBroiler;
                    int open = Math.Max(ld.Opening + ld.Placed + ld.TrfIn, 0);
                    int eggs = lg == null ? 0 : lg.TotalEggs;
                    decimal feed = lg == null ? 0 : lg.FeedKg;
                    bool laying = !broiler && (cs.IsLaying(x.AgeDays, false) || eggs > 0);
                    string stage = cs.StageOf(x.AgeDays, broiler);
                    string lot = fm == null ? "" : fm.Code;   // Flock No.
                    s.Add(lot, sh.Code, fm == null ? "" : (broiler ? x.AgeDays + " d" : Age.Text(fm.HatchDate, d)), open,
                          laying ? Rep.Pct(eggs, open) ?? 0m : 0m, laying && x.Std != null ? x.Std.HDP ?? 0m : 0m,
                          lg != null && open > 0 ? Math.Round(feed * 1000m / open, 2) : (decimal?)null,
                          laying && lg != null ? lg.EggWt ?? 0m : 0m, laying && x.Std != null ? x.Std.EggWt ?? 0m : 0m,
                          lg != null && lg.BodyWt.HasValue ? Math.Round(lg.BodyWt.Value / 1000m, 3) : (decimal?)null,
                          x.Std != null && x.Std.BodyWt.HasValue ? Math.Round(x.Std.BodyWt.Value / 1000m, 3) : (decimal?)null,
                          eggs, lg == null ? (decimal?)null : feed);
                    tEggs += eggs; tMort += ld.Mort; tCull += ld.Cull; if (lg != null) tBroken += lg.Cracked;
                    if (laying)
                    {
                        layOpen += open; layMort += ld.Mort; layCull += ld.Cull; layClose += ld.Closing; layFeed += feed;
                        if (lg != null && feed > 0)
                        {
                            decimal r = rb.Rate(lg.FeedItemId, d);
                            if (r > 0) { layFeedCost += feed * r; layFeedPriced += feed; }
                        }
                    }
                    else
                    {
                        cgFeed += feed;
                        if (stage == "Chick") { chOpen += open; chMort += ld.Mort; chCull += ld.Cull; chClose += ld.Closing; }
                        else { grOpen += open; grMort += ld.Mort; grCull += ld.Cull; grClose += ld.Closing; }
                    }
                }
            }
            decimal? farmProd = Rep.Pct(tEggs, layOpen);
            s.AddTotal("", "FARM PRODUCTION", "", null, farmProd, null, null, null, null, null, null, tEggs, layFeed + cgFeed);

            // ---- production: today vs previous day, wastage ----
            Func<DateTime, int> eggsOn = day => Db.Int(Db.Scalar(@"SELECT ISNULL(SUM(d.TotalEggs),0) FROM dbo.DailyLog d JOIN dbo.Shed s ON s.ShedId = d.ShedId
                                   JOIN dbo.Location l ON l.LocationId = s.LocationId WHERE d.CompanyId = @c AND d.LogDate = @d AND (@farm IS NULL OR l.FarmId = @farm)",
                                   Db.P("@c", cs.CompanyId), Db.P("@d", day), Db.P("@farm", p.FarmId)));
            int prev = eggsOn(d.AddDays(-1));
            int waste = Db.Int(Db.Scalar("SELECT ISNULL(SUM(Qty),0) FROM dbo.EggWastage WHERE CompanyId = @c AND WasteDate = @d AND (@farm IS NULL OR FarmId = @farm)",
                Db.P("@c", cs.CompanyId), Db.P("@d", d), Db.P("@farm", p.FarmId)));
            int diff = tEggs - prev;
            m.Summary("Production")
                .Add("Farm production %", Rep.N(farmProd, 2) + " %")
                .Add("Wastage %", Rep.N(Rep.Pct(waste, tEggs) ?? 0m, 2) + " %")
                .Add("Total production " + Rep.D(d), Fmt.N(tEggs, 0) + " Nos.")
                .Add("Total production " + Rep.D(d.AddDays(-1)), Fmt.N(prev, 0) + " Nos.")
                .Add("Difference (-)(+)", (diff > 0 ? "+" : "") + Fmt.N(diff, 0))
                .Add("Breakage", Fmt.N(tBroken, 0) + " Nos. (" + Rep.N(Rep.Pct(tBroken, tEggs) ?? 0m, 2) + " %)")
                .Add("Wastage", Fmt.N(waste, 0) + " Nos.")
                .Add("Total mortality", Fmt.N(tMort, 0) + " Nos." + (tCull > 0 ? "  (+ " + Fmt.N(tCull, 0) + " culls)" : ""));
            m.Summaries[m.Summaries.Count - 1].Weight = 1.45f;

            var bs = m.SummaryTable("Birds by Stage", 1.5f, 5);
            bs.Col("Stage", "T").Col("Opening", "N0").Col("Mort.", "N0").Col("Culls", "N0").Col("Closing", "N0");
            bs.Add("Lay", layOpen, layMort, layCull, layClose);
            bs.Add("Chick", chOpen, chMort, chCull, chClose);
            bs.Add("Grower / Pre-lay", grOpen, grMort, grCull, grClose);
            bs.AddTotal("TOTAL", layOpen + chOpen + grOpen, layMort + chMort + grMort, layCull + chCull + grCull, layClose + chClose + grClose);

            // ---- feed per bird / per egg and cost ----
            int cgOpen = chOpen + grOpen;
            decimal feedRate = layFeedPriced > 0 ? Math.Round(layFeedCost / layFeedPriced, 2) : 0m;
            decimal? layPerBird = Rep.Div(layFeed * 1000m, layOpen, 2);
            decimal? cgPerBird = Rep.Div(cgFeed * 1000m, cgOpen, 2);
            decimal? layPerEgg = Rep.Div(layFeed * 1000m, tEggs, 2);
            decimal? costPerBird = layOpen > 0 && feedRate > 0 ? Math.Round(layFeed * feedRate / layOpen, 3) : (decimal?)null;
            decimal? costPerEgg = tEggs > 0 && feedRate > 0 ? Math.Round(layFeed * feedRate / tEggs, 3) : (decimal?)null;

            // other laying-shed costs of the day from the cost engine: grit, medicine, labour, utility, overheads
            decimal other = 0, labour = 0;
            var cr = CostEngine.Compute(cs, md, rb, d, d, false);
            foreach (var r in cr.Rows)
            {
                if (r.Date != d || !scope.Shed(r.ShedId, md)) continue;
                labour += r.Labor;
                if (r.Laying) other += r.Grit + r.Med + r.Labor + r.Utility + r.Overhead + r.Other;
            }
            decimal? otherPerEgg = Rep.Div(other, tEggs, 3);
            var bf = m.Summary("Birds & Feed")
                .Add("Chicks & grower feed", Fmt.N(cgFeed, 2) + " Kg")
                .Add("Layer feed", Fmt.N(layFeed, 2) + " Kg")
                .Add("Layer feed / bird", Rep.N(layPerBird, 2) + " g")
                .Add("Chick-grower feed / bird", Rep.N(cgPerBird, 2) + " g")
                .Add("Layer feed / egg", Rep.N(layPerEgg, 2) + " g");
            bf.Weight = 1.1f; bf.XlsxSpan = 3;
            var cb = m.Summary("Cost")
                .Add("Layer feed rate", feedRate > 0 ? "Rs " + Fmt.N(feedRate, 2) + " / Kg" : "no price entered")
                .Add("Layer feed cost / bird", costPerBird.HasValue ? "Rs " + Fmt.N(costPerBird.Value, 3) : "-")
                .Add("Layer feed cost / egg", costPerEgg.HasValue ? "Rs " + Fmt.N(costPerEgg.Value, 3) : "-")
                .Add("Other cost / egg", otherPerEgg.HasValue ? "Rs " + Fmt.N(otherPerEgg.Value, 3) : "-")
                .Add("Egg production cost / egg", costPerEgg.HasValue ? "Rs " + Fmt.N(costPerEgg.Value + (otherPerEgg ?? 0m), 3) : "-");
            cb.Weight = 1.15f; cb.XlsxSpan = 3;

            int missing = lines.Count(x => x.Log == null && x.L.Present > 0);
            if (missing > 0) m.Notes.Add(missing + " occupied shed(s) have no daily entry for " + Rep.D(d) + ".");
            if (layFeed > 0 && layFeedPriced < layFeed) m.Notes.Add("Some layer feed used today has no price in Item Prices; feed rate and cost use the priced feed only.");
            m.Notes.Add("% Prod = eggs / opening birds x 100. Farm production % = all eggs / layer birds. Feed/bird and feed/egg in grams. " +
                        "Egg production cost = layer feed cost / egg + other cost / egg (grit, medicine, labour, utility, overheads of laying sheds).");
            return m;
        }

        // =====================================================================
        // 3. CHICKS / GROWER REPORT  (old: frmTransRepMainforChiks / crTransChicks)
        // =====================================================================
        public static ReportModel ChicksGrower(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var scope = Scope.From(p);
            var m = Rep.New("chicks", "Chicks / Grower Report", "");
            if (!p.ShedId.HasValue || !scope.Shed(p.ShedId.Value, md))
            {
                m.Subtitle = "Select a shed";
                m.Notes.Add("Choose a shed (brooding or grower) and a date range to see its day-by-day record.");
                return m;
            }
            int shedId = p.ShedId.Value;
            m.Subtitle = "Shed: " + md.ShedCode(shedId) + "   |   Period: " + Rep.D(p.From) + " to " + Rep.D(p.To);
            var led = BirdLedger.Load(cs.CompanyId, p.From, p.To);
            var logs = LogData.Load(cs.CompanyId, p.From, p.To);
            var std = StandardBook.Load(cs.CompanyId);

            var meds = new Dictionary<string, List<string>>();
            foreach (DataRow r in Db.Query(@"SELECT m.LogDate, m.FlockId, m.Kind, i.Name, m.Route, m.Status FROM dbo.MedicationLog m JOIN dbo.Item i ON i.ItemId = m.ItemId
                                             WHERE m.ShedId = @s AND m.LogDate BETWEEN @f AND @t AND m.Status <> N'Skipped' ORDER BY m.LogDate",
                                             Db.P("@s", shedId), Db.P("@f", p.From), Db.P("@t", p.To)).Rows)
            {
                var k = Convert.ToDateTime(r["LogDate"]).ToString("yyyyMMdd") + "|" + Db.Int(r["FlockId"]);
                List<string> l; if (!meds.TryGetValue(k, out l)) { l = new List<string>(); meds[k] = l; }
                l.Add((Db.Str(r["Kind"]) == "Vaccination" ? "[V] " : "") + Db.Str(r["Name"]) + (Db.Str(r["Status"]) == "Planned" ? " (planned)" : ""));
            }

            var s = m.Section("Daily Record");
            s.Col("Date", "D").Col("Flock", "T").Col("Age (W-D)", "T", Align.Center).Col("Stage", "T").Col("Opening", "N0").Col("Mort", "N0")
             .Col("Culls", "N0").Col("Sold", "N0").Col("Trf +/-", "N0").Col("Closing", "N0").Col("Mort %", "N3").Col("Cum Mort %", "N2")
             .Col("Feed Type", "T").Col("Feed (Kg)", "N1").Col("g/Bird", "N1").Col("Std g/Bird", "N1").Col("Cum Feed/Bird (g)", "N0")
             .Col("Body Wt (g)", "N0").Col("Std Wt (g)", "N0").Col("Water (L)", "N0").Col("Temp Min-Max", "T", Align.Center)
             .Col("Hum %", "N0").Col("Medication / Vaccination", "T");

            var days = led.Days.Where(x => x.ShedId == shedId && (x.Present > 0 || x.Opening > 0)).OrderBy(x => x.Date).ThenBy(x => x.FlockId).ToList();
            var firstOpen = new Dictionary<int, int>();
            var cumMort = new Dictionary<int, int>();
            var cumFeed = new Dictionary<int, decimal>();
            int tM = 0, tC = 0, tS = 0;
            decimal tF = 0, tW = 0, sumStdFeedBirdDays = 0;
            long birdDays = 0;
            decimal? lastBw = null, lastStdBw = null;
            foreach (var ld in days)
            {
                FlockMeta fm; md.Flocks.TryGetValue(ld.FlockId, out fm);
                if (fm == null) continue;
                LogRow lg; logs.TryGetValue(LogData.Key(shedId, ld.Date), out lg);
                if (lg != null && lg.FlockId != ld.FlockId) lg = null;
                int ageDays = Age.Days(fm.HatchDate, ld.Date);
                var st = std.Get(fm.BreedId, ageDays / 7);
                int hens = Math.Max(ld.Present, 0);
                if (!firstOpen.ContainsKey(ld.FlockId)) { firstOpen[ld.FlockId] = Math.Max(ld.Present, ld.Opening); cumMort[ld.FlockId] = 0; cumFeed[ld.FlockId] = 0; }
                cumMort[ld.FlockId] += ld.Mort + ld.Cull;
                decimal feed = lg == null ? 0 : lg.FeedKg;
                cumFeed[ld.FlockId] += feed;
                string medTxt = "";
                List<string> ml; if (meds.TryGetValue(ld.Date.ToString("yyyyMMdd") + "|" + ld.FlockId, out ml)) medTxt = string.Join("; ", ml.ToArray());
                s.Add(ld.Date, fm.Code, Age.Text(fm.HatchDate, ld.Date), cs.StageOf(ageDays, fm.IsBroiler), ld.Opening, ld.Mort, ld.Cull, ld.Sold,
                      ld.Placed + ld.TrfIn - ld.TrfOut, ld.Closing, Rep.Pct(ld.Mort + ld.Cull, hens),
                      Rep.Pct(cumMort[ld.FlockId], firstOpen[ld.FlockId]), lg == null ? "(no entry)" : lg.FeedName,
                      lg == null ? (decimal?)null : feed, hens > 0 && lg != null ? Math.Round(feed * 1000m / hens, 1) : (decimal?)null,
                      st == null ? null : st.FeedG, firstOpen[ld.FlockId] > 0 ? Math.Round(cumFeed[ld.FlockId] * 1000m / firstOpen[ld.FlockId], 0) : (decimal?)null,
                      lg == null ? null : lg.BodyWt, st == null ? null : st.BodyWt, lg == null ? null : lg.Water,
                      lg == null || !lg.TempMin.HasValue ? "" : Fmt.N(lg.TempMin.Value, 1) + " - " + (lg.TempMax.HasValue ? Fmt.N(lg.TempMax.Value, 1) : "?"),
                      lg == null ? null : lg.Humidity, medTxt);
                tM += ld.Mort; tC += ld.Cull; tS += ld.Sold; tF += feed;
                if (lg != null) { birdDays += hens; if (lg.Water.HasValue) tW += lg.Water.Value; if (st != null && st.FeedG.HasValue) sumStdFeedBirdDays += st.FeedG.Value * hens; }
                if (lg != null && lg.BodyWt.HasValue) { lastBw = lg.BodyWt; lastStdBw = st == null ? null : st.BodyWt; }
            }
            s.AddTotal("TOTAL", "", "", "", null, tM, tC, tS, null, null, null, null, "", tF, birdDays > 0 ? Math.Round(tF * 1000m / birdDays, 1) : (decimal?)null,
                       birdDays > 0 ? Math.Round(sumStdFeedBirdDays / birdDays, 1) : (decimal?)null, null, null, null, tW, "", null, "");
            int startBirds = firstOpen.Values.Sum();
            m.Summary("Period Summary")
                .Add("Birds at start", Fmt.N(startBirds, 0))
                .Add("Mortality / culls", Fmt.N(tM, 0) + " / " + Fmt.N(tC, 0))
                .Add("Period mortality %", Rep.N(Rep.Pct(tM + tC, startBirds), 2))
                .Add("Feed consumed (Kg)", Fmt.N(tF, 1))
                .Add("Avg feed g/bird/day (actual vs std)", Rep.N(birdDays > 0 ? tF * 1000m / birdDays : (decimal?)null, 1) + " vs " + Rep.N(birdDays > 0 ? sumStdFeedBirdDays / birdDays : (decimal?)null, 1))
                .Add("Latest body wt (actual vs std)", Rep.N(lastBw, 0) + " vs " + Rep.N(lastStdBw, 0))
                .Add("Water (L)", Fmt.N(tW, 0));
            m.Notes.Add("[V] = vaccination. Cum Mort % and Cum Feed/Bird are calculated from the birds in the shed at the start of the period.");
            return m;
        }

        // =====================================================================
        // 4. FLOCK TRANSACTION REPORT (II)  (old: frmTransRepNew / crProd)
        // =====================================================================
        public static ReportModel FlockDaily(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var scope = Scope.From(p);
            var m = Rep.New("flockdaily", "Flock Transaction Report (II)", "");
            FlockMeta fm;
            if (!p.FlockId.HasValue || !md.Flocks.TryGetValue(p.FlockId.Value, out fm))
            {
                m.Subtitle = "Select a flock";
                m.Notes.Add("Choose a flock to see its day-by-day movement across all sheds.");
                return m;
            }
            var from = p.From < fm.ArrivalDate ? fm.ArrivalDate : p.From;
            var to = p.To;
            m.Subtitle = "Flock: " + fm.Code + " (" + fm.BreedName + ", hatched " + Rep.D(fm.HatchDate) + ")   |   Period: " + Rep.D(from) + " to " + Rep.D(to)
                         + (p.ShedId.HasValue ? "   |   Shed: " + md.ShedCode(p.ShedId.Value) : "");
            var led = BirdLedger.Load(cs.CompanyId, from, to);
            var logs = LogData.Load(cs.CompanyId, from, to);

            var s = m.Section("Daily Movement");
            s.Col("Date", "D").Col("Age (W-D)", "T", Align.Center).Col("Stage", "T").Col("Shed(s)", "T").Col("Opening", "N0").Col("Placed", "N0")
             .Col("Mort", "N0").Col("Culls", "N0").Col("Sold", "N0").Col("Trf Out", "N0").Col("Trf In", "N0").Col("Transit Loss", "N0")
             .Col("Closing", "N0").Col("Feed Type", "T").Col("Feed (Kg)", "N1").Col("g/Bird", "N1").Col("Shell Grit (Kg)", "N1")
             .Col("Eggs", "N0").Col("HD %", "N2");

            int tP = 0, tM = 0, tC = 0, tS = 0, tO = 0, tI = 0, tE = 0;
            decimal tF = 0, tG = 0;
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                int open = 0, placed = 0, mort = 0, cull = 0, sold = 0, tout = 0, tin = 0, close = 0, present = 0, eggs = 0;
                decimal feed = 0, grit = 0;
                var sheds = new List<string>();
                var feeds = new List<string>();
                bool any = false;
                foreach (var ld in led.Days)
                {
                    if (ld.Date != d || ld.FlockId != fm.FlockId || !scope.Includes(ld.ShedId, ld.FlockId, md)) continue;
                    any = true;
                    open += ld.Opening; placed += ld.Placed; mort += ld.Mort; cull += ld.Cull; sold += ld.Sold; tout += ld.TrfOut; tin += ld.TrfIn;
                    close += ld.Closing; present += Math.Max(ld.Present, 0);
                    if (ld.Closing > 0 || ld.Present > 0) sheds.Add(md.ShedCode(ld.ShedId));
                    LogRow lg;
                    if (logs.TryGetValue(LogData.Key(ld.ShedId, d), out lg) && lg.FlockId == fm.FlockId)
                    {
                        feed += lg.FeedKg; grit += lg.GritKg; eggs += lg.TotalEggs;
                        if (lg.FeedName.Length > 0 && !feeds.Contains(lg.FeedName)) feeds.Add(lg.FeedName);
                    }
                }
                if (!any) continue;
                int ageDays = Age.Days(fm.HatchDate, d);
                s.Add(d, Age.Text(fm.HatchDate, d), cs.StageOf(ageDays, fm.IsBroiler), string.Join(", ", sheds.ToArray()), open, placed, mort, cull, sold, tout, tin,
                      tout - tin > 0 && !p.ShedId.HasValue ? (object)(tout - tin) : null, close, string.Join(", ", feeds.ToArray()), feed,
                      present > 0 ? Math.Round(feed * 1000m / present, 1) : (decimal?)null, grit, eggs, cs.IsLaying(ageDays, fm.IsBroiler) || eggs > 0 ? Rep.Pct(eggs, present) : null);
                tP += placed; tM += mort; tC += cull; tS += sold; tO += tout; tI += tin; tE += eggs; tF += feed; tG += grit;
            }
            s.AddTotal("TOTAL", "", "", "", null, tP, tM, tC, tS, tO, tI, p.ShedId.HasValue ? (object)null : tO - tI, null, "", tF, null, tG, tE, null);

            int placedTotal = fm.InitialCount - fm.ArrivalMortality;
            m.Summary("Flock").Add("Flock code", fm.Code).Add("Breed", fm.BreedName).Add("Source / hatchery", fm.Source)
                .Add("Batch / Lot", fm.BatchNo + " / " + fm.LotNo).Add("Hatch date", Rep.D(fm.HatchDate)).Add("Arrival date", Rep.D(fm.ArrivalDate))
                .Add("Chicks received", Fmt.N(fm.InitialCount, 0)).Add("Arrival mortality (DOA)", Fmt.N(fm.ArrivalMortality, 0))
                .Add("Birds placed", Fmt.N(placedTotal, 0)).Add("Birds now", Fmt.N(BirdLedger.FlockBirdsBefore(fm.FlockId, DateTime.Today.AddDays(1)), 0));
            m.Summary("Period Totals").Add("Mortality", Fmt.N(tM, 0)).Add("Culls", Fmt.N(tC, 0)).Add("Sold", Fmt.N(tS, 0))
                .Add("Transit loss", Fmt.N(tO - tI, 0)).Add("Feed (Kg)", Fmt.N(tF, 1)).Add("Shell grit (Kg)", Fmt.N(tG, 1)).Add("Eggs", Fmt.N(tE, 0));
            return m;
        }
    }
}
