using System;
using System.Collections.Generic;
using System.Data;

namespace PoultryERP.Reports
{
    /// <summary>All cost components booked to one shed + flock on one day.</summary>
    public class CostRow
    {
        public int ShedId, FlockId, FarmId, AgeDays, BirdsPresent, TotalEggs, SaleableEggs;
        public DateTime Date;
        public string Stage;
        public bool Laying;
        public decimal FeedKg, Feed, Grit, Med, Labor, Utility, Overhead, Other, Depreciation;

        public decimal Variable { get { return Feed + Grit + Med; } }
        public decimal FixedOps { get { return Labor + Utility + Overhead + Other; } }
        /// <summary>Spec formula numerator: Feed + Shell grit + Meds + Labour + Overheads.</summary>
        public decimal Operating { get { return Variable + FixedOps; } }
        public decimal Total { get { return Operating + Depreciation; } }
    }

    public class CostResult
    {
        public List<CostRow> Rows = new List<CostRow>();
        public decimal UnallocatedOverhead;
        public int MissingFeedRates;
        public Dictionary<int, decimal> DepPerBirdDay = new Dictionary<int, decimal>();
        public Dictionary<int, decimal> CapitalisedPerBird = new Dictionary<int, decimal>();
        public Dictionary<int, int> HousedBirds = new Dictionary<int, int>();
    }

    /// <summary>
    /// Cost engine.
    ///  * Feed, grit and medicine are valued at the item rate effective on the day (ItemRate history).
    ///  * Overheads (labour, utility, other) entered for a shed are spread evenly over their period;
    ///    farm-level entries are allocated to the sheds of that farm in proportion to bird-days.
    ///  * Costs of the rearing period (chick, grower, pre-lay) are capitalised into the bird and
    ///    recovered through daily depreciation over the planned laying life, net of expected salvage value.
    /// </summary>
    public static class CostEngine
    {
        public static CostResult Compute(int companyId, DateTime from, DateTime to, bool withDepreciation)
        {
            var cs = Ctx.LoadCompany(companyId);
            var md = MasterData.Load(companyId);
            var rb = RateBook.Load(companyId);
            return Compute(cs, md, rb, from, to, withDepreciation);
        }

        public static CostResult Compute(CompanySettings cs, MasterData md, RateBook rb, DateTime from, DateTime to, bool withDepreciation)
        {
            var res = new CostResult();
            var ledger = BirdLedger.Load(cs.CompanyId, from, to);
            var logs = LogData.Load(cs.CompanyId, from, to);
            var rows = new Dictionary<string, CostRow>();
            var byDate = new Dictionary<DateTime, List<CostRow>>();

            Func<int, int, DateTime, CostRow> get = (shed, flock, d) =>
            {
                var key = shed + "|" + flock + "|" + d.Ticks;
                CostRow cr;
                if (rows.TryGetValue(key, out cr)) return cr;
                FlockMeta fm; ShedMeta sm;
                md.Flocks.TryGetValue(flock, out fm);
                md.Sheds.TryGetValue(shed, out sm);
                int age = fm == null ? 0 : Age.Days(fm.HatchDate, d);
                cr = new CostRow
                {
                    ShedId = shed, FlockId = flock, Date = d, FarmId = sm == null ? 0 : sm.FarmId, AgeDays = age,
                    Stage = cs.StageOf(age, fm != null && fm.IsBroiler), Laying = cs.IsLaying(age, fm != null && fm.IsBroiler)
                };
                rows[key] = cr;
                List<CostRow> l;
                if (!byDate.TryGetValue(d, out l)) { l = new List<CostRow>(); byDate[d] = l; }
                l.Add(cr);
                return cr;
            };

            foreach (var ld in ledger.Days)
            {
                if (ld.Present <= 0 && ld.Closing <= 0) continue;
                var cr = get(ld.ShedId, ld.FlockId, ld.Date);
                cr.BirdsPresent = Math.Max(ld.Present, 0);
            }

            foreach (var lg in logs.Values)
            {
                var cr = get(lg.ShedId, lg.FlockId, lg.Date);
                cr.FeedKg += lg.FeedKg;
                decimal fr = rb.Rate(lg.FeedItemId, lg.Date);
                if (lg.FeedKg > 0 && fr == 0) res.MissingFeedRates++;
                cr.Feed += lg.FeedKg * fr;
                cr.Grit += lg.GritKg * rb.Rate(lg.GritItemId ?? rb.DefaultGritItemId, lg.Date);
                cr.TotalEggs += lg.TotalEggs;
                cr.SaleableEggs += cs.SaleableEggs(lg.Table, lg.Cracked, lg.Misshapen, lg.DoubleYolk);
            }

            foreach (DataRow r in Db.Query(@"SELECT ShedId, FlockId, LogDate, ItemId, Qty FROM dbo.MedicationLog
                                             WHERE CompanyId = @c AND Status = N'Done' AND LogDate BETWEEN @f AND @t",
                                             Db.P("@c", cs.CompanyId), Db.P("@f", from), Db.P("@t", to)).Rows)
            {
                var d = Convert.ToDateTime(r["LogDate"]).Date;
                var cr = get(Db.Int(r["ShedId"]), Db.Int(r["FlockId"]), d);
                cr.Med += Db.Dec(r["Qty"]) * rb.Rate(Db.Int(r["ItemId"]), d);
            }

            // ---- overhead allocation on bird-days ----
            foreach (DataRow r in Db.Query(@"SELECT FarmId, ShedId, CostHead, FromDate, ToDate, Amount FROM dbo.Overhead
                                             WHERE CompanyId = @c AND ToDate >= @f AND FromDate <= @t",
                                             Db.P("@c", cs.CompanyId), Db.P("@f", from), Db.P("@t", to)).Rows)
            {
                var f = Convert.ToDateTime(r["FromDate"]).Date;
                var t = Convert.ToDateTime(r["ToDate"]).Date;
                int days = (int)(t - f).TotalDays + 1;
                decimal daily = Db.Dec(r["Amount"]) / days;
                int farm = Db.Int(r["FarmId"]);
                int? shed = Db.NInt(r["ShedId"]);
                string head = Db.Str(r["CostHead"]);
                var start = f < from ? from.Date : f;
                var end = t > to ? to.Date : t;
                for (var d = start; d <= end; d = d.AddDays(1))
                {
                    List<CostRow> l;
                    var targets = new List<CostRow>();
                    long basis = 0;
                    if (byDate.TryGetValue(d, out l))
                        foreach (var cr in l)
                        {
                            if (cr.BirdsPresent <= 0) continue;
                            if (shed.HasValue ? cr.ShedId == shed.Value : cr.FarmId == farm) { targets.Add(cr); basis += cr.BirdsPresent; }
                        }
                    if (basis == 0) { res.UnallocatedOverhead += daily; continue; }
                    foreach (var cr in targets)
                    {
                        decimal share = daily * cr.BirdsPresent / basis;
                        switch (head)
                        {
                            case "Labor": cr.Labor += share; break;
                            case "Utility": cr.Utility += share; break;
                            case "Overhead": cr.Overhead += share; break;
                            default: cr.Other += share; break;
                        }
                    }
                }
            }

            res.Rows.AddRange(rows.Values);
            res.Rows.Sort((a, b) => a.Date != b.Date ? a.Date.CompareTo(b.Date) : a.ShedId.CompareTo(b.ShedId));

            if (withDepreciation) ApplyDepreciation(cs, md, rb, res);
            return res;
        }

        /// <summary>Daily depreciation of laying birds = (capitalised cost per hen housed - salvage) / planned laying days.</summary>
        static void ApplyDepreciation(CompanySettings cs, MasterData md, RateBook rb, CostResult res)
        {
            var layingFlocks = new HashSet<int>();
            foreach (var r in res.Rows) if (r.Laying && r.BirdsPresent > 0) layingFlocks.Add(r.FlockId);
            if (layingFlocks.Count == 0) return;

            // one pass over the combined rearing window of all laying flocks
            DateTime rearFrom = DateTime.MaxValue, rearTo = DateTime.MinValue;
            foreach (var id in layingFlocks)
            {
                var fm = md.Flocks[id];
                var ls = fm.HatchDate.AddDays(cs.LayStartAgeDays);
                if (fm.ArrivalDate < rearFrom) rearFrom = fm.ArrivalDate;
                if (ls.AddDays(-1) > rearTo) rearTo = ls.AddDays(-1);
            }
            var rearingCost = new Dictionary<int, decimal>();
            if (rearFrom <= rearTo)
            {
                var rear = Compute(cs, md, rb, rearFrom, rearTo, false);
                foreach (var r in rear.Rows)
                {
                    if (!layingFlocks.Contains(r.FlockId) || r.Laying) continue;
                    decimal v; rearingCost.TryGetValue(r.FlockId, out v);
                    rearingCost[r.FlockId] = v + r.Operating;
                }
            }

            int layingDays = 0;
            foreach (var id in layingFlocks)
            {
                var fm = md.Flocks[id];
                var layStart = fm.HatchDate.AddDays(cs.LayStartAgeDays);
                int housed = fm.HenHousedCount ?? BirdLedger.FlockBirdsBefore(id, layStart > fm.ArrivalDate ? layStart : fm.ArrivalDate.AddDays(1));
                if (housed <= 0) housed = fm.InitialCount - fm.ArrivalMortality;
                decimal rc; rearingCost.TryGetValue(id, out rc);
                decimal capPerBird = housed > 0 ? (fm.AcquisitionCost + rc) / housed : 0;
                layingDays = Math.Max(7, (fm.PlannedDepletionWeek - cs.LayStartWeek) * 7);
                decimal dep = Math.Max(0, capPerBird - fm.SalvagePerBird) / layingDays;
                res.DepPerBirdDay[id] = dep;
                res.CapitalisedPerBird[id] = capPerBird;
                res.HousedBirds[id] = housed;
            }
            foreach (var r in res.Rows)
            {
                decimal dep;
                if (!r.Laying || r.BirdsPresent <= 0 || !res.DepPerBirdDay.TryGetValue(r.FlockId, out dep)) continue;
                var fm = md.Flocks[r.FlockId];
                if (r.AgeDays >= fm.PlannedDepletionWeek * 7) continue; // fully depreciated
                r.Depreciation = dep * r.BirdsPresent;
            }
        }
    }
}
