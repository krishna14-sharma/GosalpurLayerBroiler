using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PoultryERP.Reports
{
    /// <summary>Hen House (HHR), Flock Transfer and Mortality Analysis reports.</summary>
    public static class PerformanceReports
    {
        // =====================================================================
        // 5. HEN HOUSE PRODUCTION REPORT  (old: frmHenHouseRpt / frmHenHouseRptebwt)
        // =====================================================================
        public static ReportModel HenHouse(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var scope = Scope.From(p);
            var m = Rep.New("henhouse", "Hen House Production Report", "");
            FlockMeta fm;
            if (!p.FlockId.HasValue || !md.Flocks.TryGetValue(p.FlockId.Value, out fm))
            {
                m.Subtitle = "Select a flock";
                m.Notes.Add("Choose a laying flock (optionally one shed) to see weekly production against the breeder standard.");
                return m;
            }
            if (p.ShedId.HasValue && !scope.Shed(p.ShedId.Value, md)) p.ShedId = null;
            int startWeek = cs.PreLayEndWk;
            var from = fm.HatchDate.AddDays(startWeek * 7);
            if (from < fm.ArrivalDate) from = fm.ArrivalDate;
            var to = DateTime.Today.AddDays(-1);
            if (fm.DepletionDate.HasValue && fm.DepletionDate.Value < to) to = fm.DepletionDate.Value;
            m.Subtitle = "Flock: " + fm.Code + "   |   Breed: " + fm.BreedName + "   |   Hatch date: " + Rep.D(fm.HatchDate)
                         + (p.ShedId.HasValue ? "   |   Shed: " + md.ShedCode(p.ShedId.Value) : "   |   All sheds of the flock");
            if (from > to)
            {
                m.Notes.Add("This flock has not reached laying age yet (laying starts at week " + cs.LayStartWeek + ").");
                return m;
            }

            var led = BirdLedger.Load(cs.CompanyId, from, to);
            var logs = LogData.Load(cs.CompanyId, from, to);
            var std = StandardBook.Load(cs.CompanyId);
            var layStart = fm.HatchDate.AddDays(cs.LayStartAgeDays);

            // day -> aggregated figures for the flock within scope
            var byDay = new Dictionary<DateTime, int[]>(); // open, present, mort, cull, sold, trfOut, trfIn, eggs
            var feedDay = new Dictionary<DateTime, decimal>();
            var ewSum = new Dictionary<DateTime, decimal[]>(); // eggwt*eggs, eggs-with-wt, bw sum, bw count
            foreach (var ld in led.Days)
            {
                if (ld.FlockId != fm.FlockId || !scope.Includes(ld.ShedId, ld.FlockId, md)) continue;
                int[] a; if (!byDay.TryGetValue(ld.Date, out a)) { a = new int[8]; byDay[ld.Date] = a; }
                a[0] += ld.Opening; a[1] += Math.Max(ld.Present, 0); a[2] += ld.Mort; a[3] += ld.Cull; a[4] += ld.Sold; a[5] += ld.TrfOut; a[6] += ld.TrfIn + ld.Placed;
                LogRow lg;
                if (logs.TryGetValue(LogData.Key(ld.ShedId, ld.Date), out lg) && lg.FlockId == fm.FlockId)
                {
                    a[7] += lg.TotalEggs;
                    decimal f; feedDay.TryGetValue(ld.Date, out f); feedDay[ld.Date] = f + lg.FeedKg;
                    decimal[] w; if (!ewSum.TryGetValue(ld.Date, out w)) { w = new decimal[4]; ewSum[ld.Date] = w; }
                    if (lg.EggWt.HasValue && lg.TotalEggs > 0) { w[0] += lg.EggWt.Value * lg.TotalEggs; w[1] += lg.TotalEggs; }
                    if (lg.BodyWt.HasValue) { w[2] += lg.BodyWt.Value; w[3] += 1; }
                }
            }

            int housed = 0;
            if (!p.ShedId.HasValue && fm.HenHousedCount.HasValue && fm.HenHousedCount.Value > 0) housed = fm.HenHousedCount.Value;
            else
            {
                foreach (var d in byDay.Keys.OrderBy(k => k))
                    if (d >= layStart && byDay[d][1] > 0) { housed = byDay[d][1]; break; }
                if (housed == 0 && byDay.Count > 0) housed = byDay[byDay.Keys.Min()][1];
            }

            bool wt = p.WithWeights;
            var s = m.Section("Weekly Performance" + (wt ? " (with egg & body weight)" : ""));
            s.Col("Age (Wk)", "N0").Col("Week From", "D").Col("Opening", "N0").Col("Mort", "N0").Col("Culls", "N0").Col("Cum Depl", "N0")
             .Col("Cum Depl %", "N2").Col("Eggs", "N0").Col("HD %", "N2").Col("Std HD %", "N2").Col("HH %", "N2").Col("Eggs/HH (Wk)", "N2")
             .Col("Cum Eggs/HH", "N1").Col("Std Cum HH", "N1").Col("Feed (Kg)", "N1").Col("g/Bird/Day", "N1").Col("Std g", "N1")
             .Col("Kg Feed/Dozen", "N3");
            if (wt) s.Col("FCR Kg/Kg Egg", "N2").Col("Egg Wt (g)", "N1").Col("Std EW (g)", "N1").Col("Body Wt (g)", "N0").Col("Std BW (g)", "N0");

            var chart = new ChartSpec { Title = "HD % vs standard", YTitle = "HD %", Y2Title = "Cum eggs / HH" };
            var sHd = new ChartSeries("HD % (actual)"); var sStd = new ChartSeries("HD % (standard)");
            var sCum = new ChartSeries("Cum eggs/HH") { Axis = "y2" }; var sCumStd = new ChartSeries("Std cum eggs/HH") { Axis = "y2" };

            int cumDepl = 0; decimal cumEggs = 0, tFeed = 0, tMass = 0; long tBirdDays = 0; int tEggs = 0;
            decimal peakHd = 0; int peakWk = 0; int weeks = 0; decimal sumHd = 0;
            for (int wk = startWeek; wk < 200; wk++)
            {
                var ws = fm.HatchDate.AddDays(wk * 7);
                if (ws > to) break;
                var we = ws.AddDays(6); if (we > to) we = to;
                int open = -1, mort = 0, cull = 0, sold = 0, eggs = 0, tout = 0, tin = 0; long birdDays = 0;
                decimal feed = 0, ew = 0, ewN = 0, bw = 0, bwN = 0;
                for (var d = ws; d <= we; d = d.AddDays(1))
                {
                    int[] a;
                    if (!byDay.TryGetValue(d, out a)) continue;
                    if (open < 0) open = a[0];
                    birdDays += a[1]; mort += a[2]; cull += a[3]; sold += a[4]; tout += a[5]; tin += a[6]; eggs += a[7];
                    decimal f; if (feedDay.TryGetValue(d, out f)) feed += f;
                    decimal[] w; if (ewSum.TryGetValue(d, out w)) { ew += w[0]; ewN += w[1]; bw += w[2]; bwN += w[3]; }
                }
                if (birdDays == 0) continue;
                if (open < 0) open = 0;
                cumDepl += mort + cull + (p.ShedId.HasValue ? 0 : Math.Max(tout - tin, 0));
                cumEggs += eggs;
                var st = std.Get(fm.BreedId, wk);
                decimal? hd = Rep.Pct(eggs, birdDays);
                decimal? eggWt = ewN > 0 ? Math.Round(ew / ewN, 1) : (decimal?)null;
                decimal? mass = eggWt.HasValue ? eggs * eggWt.Value / 1000m : (decimal?)null;
                var row = new List<object>
                {
                    wk, ws, open, mort, cull, cumDepl, Rep.Pct(cumDepl, housed), eggs, hd, st == null ? null : st.HDP,
                    housed > 0 ? Math.Round(eggs * 100m / (housed * (decimal)((we - ws).TotalDays + 1)), 2) : (decimal?)null,
                    housed > 0 ? Math.Round((decimal)eggs / housed, 2) : (decimal?)null,
                    housed > 0 ? Math.Round(cumEggs / housed, 1) : (decimal?)null, st == null ? null : st.HHCum,
                    feed, Math.Round(feed * 1000m / birdDays, 1), st == null ? null : st.FeedG,
                    eggs > 0 ? Math.Round(feed / (eggs / 12m), 3) : (decimal?)null
                };
                if (wt)
                {
                    row.Add(mass.HasValue && mass.Value > 0 ? Math.Round(feed / mass.Value, 2) : (decimal?)null);
                    row.Add(eggWt); row.Add(st == null ? null : st.EggWt);
                    row.Add(bwN > 0 ? Math.Round(bw / bwN, 0) : (decimal?)null); row.Add(st == null ? null : st.BodyWt);
                }
                s.Add(row.ToArray());
                chart.Labels.Add("W" + wk);
                sHd.Values.Add(hd); sStd.Values.Add(st == null ? null : st.HDP);
                sCum.Values.Add(housed > 0 ? Math.Round(cumEggs / housed, 1) : (decimal?)null); sCumStd.Values.Add(st == null ? null : st.HHCum);
                tFeed += feed; tBirdDays += birdDays; tEggs += eggs; if (mass.HasValue) tMass += mass.Value;
                if (hd.HasValue && wk >= cs.LayStartWeek) { if (hd.Value > peakHd) { peakHd = hd.Value; peakWk = wk; } sumHd += hd.Value; weeks++; }
            }
            chart.Series.Add(sHd); chart.Series.Add(sStd); chart.Series.Add(sCum); chart.Series.Add(sCumStd);
            m.Chart = chart;

            int now = 0;
            foreach (var kv in byDay) if (kv.Key == to) now = kv.Value[1] - kv.Value[2] - kv.Value[3] - kv.Value[4];
            var lastStd = std.Get(fm.BreedId, Age.Days(fm.HatchDate, to) / 7);
            m.Summary("Flock").Add("Flock", fm.Code).Add("Breed", fm.BreedName).Add("Hatch date", Rep.D(fm.HatchDate))
                .Add("Age on " + Rep.D(to), Age.Text(fm.HatchDate, to)).Add("Hens housed", Fmt.N(housed, 0))
                .Add("Birds now", Fmt.N(now, 0)).Add("Livability (on housed)", Rep.N(housed > 0 ? 100m - cumDepl * 100m / housed : (decimal?)null, 2) + " %");
            m.Summary("Production to Date").Add("Total eggs", Fmt.N(tEggs, 0)).Add("Eggs per hen housed", Rep.N(housed > 0 ? cumEggs / housed : (decimal?)null, 1)
                + "  (std " + Rep.N(lastStd == null ? null : lastStd.HHCum, 1) + ")")
                .Add("Peak HD %", Rep.N(peakHd, 2) + (peakWk > 0 ? " at week " + peakWk : ""))
                .Add("Average HD % in lay", Rep.N(weeks > 0 ? sumHd / weeks : (decimal?)null, 2))
                .Add("Feed g/bird/day (avg)", Rep.N(tBirdDays > 0 ? tFeed * 1000m / tBirdDays : (decimal?)null, 1))
                .Add("Kg feed per dozen eggs", Rep.N(tEggs > 0 ? tFeed / (tEggs / 12m) : (decimal?)null, 3))
                .Add("FCR (kg feed / kg egg mass)", Rep.N(tMass > 0 ? tFeed / tMass : (decimal?)null, 2));
            m.Notes.Add("HD % = eggs / bird-days present x 100.  HH % = eggs / (hens housed x days) x 100.  Hens housed = " +
                        (fm.HenHousedCount.HasValue && !p.ShedId.HasValue ? "figure stored on the flock." : "birds present on the first day of lay (week " + cs.LayStartWeek + ")."));
            m.Notes.Add("FCR per kg egg mass uses the egg weights recorded in the daily entry; weeks without an egg weight are left blank.");
            return m;
        }

        // =====================================================================
        // 6. FLOCK TRANSFER / SHIFTING REPORT
        // =====================================================================
        public static ReportModel Transfers(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var m = Rep.New("transfers", "Flock Transfer / Shifting Report", "Period: " + Rep.D(p.From) + " to " + Rep.D(p.To) + "   |   " + Rep.ScopeText(p, md, false, true));
            var dt = Db.Query(@"
SELECT t.*, f.FlockCode, f.HatchDate, f.FlockType, fs.Code AS FromCode, ts.Code AS ToCode, fl.FarmId AS FromFarm, tl.FarmId AS ToFarm,
       ru.FullName AS ReqName, au.FullName AS AppName
FROM dbo.FlockTransfer t
JOIN dbo.Flock f ON f.FlockId = t.FlockId
JOIN dbo.Shed fs ON fs.ShedId = t.FromShedId JOIN dbo.Location fl ON fl.LocationId = fs.LocationId
JOIN dbo.Shed ts ON ts.ShedId = t.ToShedId   JOIN dbo.Location tl ON tl.LocationId = ts.LocationId
LEFT JOIN dbo.AppUser ru ON ru.UserId = t.RequestedBy
LEFT JOIN dbo.AppUser au ON au.UserId = t.ApprovedBy
WHERE t.CompanyId = @CompanyId AND t.TransferDate BETWEEN @f AND @t
  AND (@farm IS NULL OR fl.FarmId = @farm OR tl.FarmId = @farm)
  AND (@flock IS NULL OR t.FlockId = @flock)
  AND (@Restricted = 0 OR t.FromShedId IN (SELECT ShedId FROM dbo.UserShed WHERE UserId = @UserId) OR t.ToShedId IN (SELECT ShedId FROM dbo.UserShed WHERE UserId = @UserId))
ORDER BY t.TransferDate, t.TransferId", Ctx.Scope(Db.P("@f", p.From), Db.P("@t", p.To), Db.P("@farm", p.FarmId), Db.P("@flock", p.FlockId)));

            var s = m.Section("Movements");
            s.Col("Date", "D").Col("Trf No", "N0").Col("Flock", "T").Col("From Shed", "T").Col("To Shed", "T").Col("Age (W-D)", "T", Align.Center)
             .Col("Age Days", "N0").Col("Stage", "T").Col("Dispatched", "N0").Col("Transit Mort", "N0").Col("Transit Culls", "N0")
             .Col("Received", "N0").Col("Shrinkage %", "N2").Col("Type", "T").Col("Status", "T").Col("Requested By", "T").Col("Approved By", "T").Col("Remarks", "T");
            int tD = 0, tM = 0, tC = 0, tR = 0, n = 0;
            foreach (DataRow r in dt.Rows)
            {
                var date = Convert.ToDateTime(r["TransferDate"]);
                var hatch = Convert.ToDateTime(r["HatchDate"]);
                int disp = Db.Int(r["BirdsDispatched"]), tmort = Db.Int(r["TransitMortality"]), tcull = Db.Int(r["TransitCulls"]), recv = Db.Int(r["BirdsReceived"]);
                var status = Db.Str(r["Status"]);
                int ageDays = Age.Days(hatch, date);
                s.Add(date, Db.Int(r["TransferId"]), Db.Str(r["FlockCode"]), Db.Str(r["FromCode"]), Db.Str(r["ToCode"]), Age.Text(hatch, date), ageDays,
                      cs.StageOf(ageDays, Db.Str(r["FlockType"]) == "Broiler"), disp, tmort, tcull, recv, Rep.Pct(tmort + tcull, disp), Convert.ToBoolean(r["IsFullTransfer"]) ? "Full" : "Partial",
                      status, Db.Str(r["ReqName"]), Db.Str(r["AppName"]), Db.Str(r["Remarks"]));
                if (status == "Approved") { tD += disp; tM += tmort; tC += tcull; tR += recv; n++; }
            }
            s.AddTotal("APPROVED TOTAL", n, "", "", "", "", null, "", tD, tM, tC, tR, Rep.Pct(tM + tC, tD), "", "", "", "", "");
            m.Summary("Summary").Add("Approved movements", Fmt.N(n, 0)).Add("Birds dispatched", Fmt.N(tD, 0))
                .Add("Transit mortality", Fmt.N(tM, 0)).Add("Transit culls", Fmt.N(tC, 0)).Add("Birds received", Fmt.N(tR, 0))
                .Add("Average shrinkage %", Rep.N(Rep.Pct(tM + tC, tD), 2));
            m.Notes.Add("Only approved transfers change shed inventory. Pending and rejected requests are listed for the audit trail.");
            return m;
        }

        // =====================================================================
        // 7. MORTALITY ANALYSIS (veterinary)
        // =====================================================================
        public static ReportModel Mortality(ReportParams p)
        {
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var scope = Scope.From(p);
            var m = Rep.New("mortality", "Mortality Analysis", "Period: " + Rep.D(p.From) + " to " + Rep.D(p.To) + "   |   " + Rep.ScopeText(p, md, true, true));
            var led = BirdLedger.Load(cs.CompanyId, p.From, p.To);

            var agg = new Dictionary<string, long[]>(); // shed|flock -> birdDays, mort, cull, firstOpen, peakMort, peakDateTicks, days
            var weekly = new SortedDictionary<DateTime, long[]>(); // weekStart -> opening birds, mort, cull, birdDays
            var seenInWeek = new HashSet<string>();
            foreach (var ld in led.Days)
            {
                if (!scope.Includes(ld.ShedId, ld.FlockId, md) || ld.Present <= 0) continue;
                var k = ld.ShedId + "|" + ld.FlockId;
                long[] a; if (!agg.TryGetValue(k, out a)) { a = new long[7]; a[3] = ld.Present; agg[k] = a; }
                a[0] += ld.Present; a[1] += ld.Mort; a[2] += ld.Cull; a[6]++;
                if (ld.Mort > a[4]) { a[4] = ld.Mort; a[5] = ld.Date.Ticks; }
                int offset = ((int)(ld.Date - p.From).TotalDays) % 7;
                var wsd = ld.Date.AddDays(-offset);
                long[] w; if (!weekly.TryGetValue(wsd, out w)) { w = new long[4]; weekly[wsd] = w; }
                if (seenInWeek.Add(wsd.Ticks + "|" + k)) w[0] += Math.Max(ld.Opening, ld.Present);
                w[1] += ld.Mort; w[2] += ld.Cull; w[3] += ld.Present;
            }

            var s = m.Section("By Shed & Flock");
            s.Col("Shed", "T").Col("Flock", "T").Col("Age at End", "T", Align.Center).Col("Stage", "T").Col("Avg Birds", "N0").Col("Mortality", "N0")
             .Col("Culls", "N0").Col("Total", "N0").Col("Period Mort %", "N2").Col("Avg Weekly Mort %", "N3").Col("Peak Day Mort", "N0").Col("Peak Day", "D");
            long tM = 0, tC = 0, tStart = 0;
            foreach (var kv in agg.OrderBy(k => md.ShedCode(int.Parse(k.Key.Split('|')[0]))))
            {
                var ids = kv.Key.Split('|');
                int shed = int.Parse(ids[0]), flock = int.Parse(ids[1]);
                var a = kv.Value;
                FlockMeta fm; md.Flocks.TryGetValue(flock, out fm);
                int ageEnd = fm == null ? 0 : Age.Days(fm.HatchDate, p.To);
                decimal avgBirds = a[6] > 0 ? (decimal)a[0] / a[6] : 0;
                s.Add(md.ShedCode(shed), md.FlockCode(flock), fm == null ? "" : Age.Text(fm.HatchDate, p.To), cs.StageOf(ageEnd, fm != null && fm.IsBroiler), Math.Round(avgBirds, 0),
                      a[1], a[2], a[1] + a[2], Rep.Pct(a[1] + a[2], a[3]), a[0] > 0 ? Math.Round((a[1] + a[2]) * 100m * 7 / a[0], 3) : (decimal?)null,
                      a[4] > 0 ? (object)a[4] : null, a[4] > 0 ? (object)new DateTime(a[5]) : null);
                tM += a[1]; tC += a[2]; tStart += a[3];
            }
            s.AddTotal("TOTAL", "", "", "", null, tM, tC, tM + tC, Rep.Pct(tM + tC, tStart), null, null, null);

            var w2 = m.Section("Weekly Trend (all selected sheds)");
            w2.Col("Week From", "D").Col("Opening", "N0").Col("Mortality", "N0").Col("Culls", "N0").Col("Mort %", "N3").Col("Mort per 1000 bird-days", "N2");
            var chart = new ChartSpec { Title = "Weekly mortality %", YTitle = "%" };
            var cser = new ChartSeries("Mort + cull %") { Type = "bar" };
            foreach (var kv in weekly)
            {
                var w = kv.Value;
                var pct = Rep.Pct(w[1] + w[2], w[0]);
                w2.Add(kv.Key, w[0], w[1], w[2], pct, w[3] > 0 ? Math.Round((w[1] + w[2]) * 1000m / w[3], 2) : (decimal?)null);
                chart.Labels.Add(kv.Key.ToString("dd-MMM")); cser.Values.Add(pct);
            }
            chart.Series.Add(cser);
            m.Chart = chart;

            var pm = m.Section("Post-Mortem Findings");
            pm.Col("Date", "D").Col("Shed", "T").Col("Birds", "N0").Col("Findings", "T").Col("Diagnosis", "T").Col("Action Taken", "T").Col("Vet", "T");
            foreach (DataRow r in Db.Query(@"SELECT p.*, s.Code FROM dbo.PostMortem p JOIN dbo.Shed s ON s.ShedId = p.ShedId JOIN dbo.Location l ON l.LocationId = s.LocationId
                                             WHERE p.CompanyId = @CompanyId AND p.PmDate BETWEEN @f AND @t AND (@farm IS NULL OR l.FarmId = @farm)
                                             AND (@shed IS NULL OR p.ShedId = @shed)" + Ctx.ShedFilter("p.ShedId") + " ORDER BY p.PmDate",
                                             Ctx.Scope(Db.P("@f", p.From), Db.P("@t", p.To), Db.P("@farm", p.FarmId), Db.P("@shed", p.ShedId))).Rows)
                pm.Add(Convert.ToDateTime(r["PmDate"]), Db.Str(r["Code"]), Db.Int(r["BirdsExamined"]), Db.Str(r["Findings"]), Db.Str(r["Diagnosis"]),
                       Db.Str(r["ActionTaken"]), Db.Str(r["VetName"]));
            pm.EmptyText = "No post-mortem records in this period.";
            m.Summary("Summary").Add("Mortality", Fmt.N(tM, 0)).Add("Culls", Fmt.N(tC, 0)).Add("Total depletion", Fmt.N(tM + tC, 0))
                .Add("Depletion % on starting birds", Rep.N(Rep.Pct(tM + tC, tStart), 2));
            return m;
        }
    }
}
