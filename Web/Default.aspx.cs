using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web.Services;
using PoultryERP.Reports;

namespace PoultryERP
{
    public partial class DashboardPage : BasePage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Title = "Dashboard";
        }

        protected bool CanEnter { get { return Ctx.InRole(Roles.SuperAdmin, Roles.FarmManager, Roles.Supervisor); } }
        protected bool CanSeeCost { get { return Ctx.InRole(Roles.SuperAdmin, Roles.FarmManager, Roles.Accountant); } }

        static void Guard()
        {
            if (Ctx.User == null) throw new UnauthorizedAccessException("Session expired - please sign in again.");
        }

        [WebMethod(EnableSession = true)]
        public static object GetDashboard()
        {
            Guard();
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var scope = Scope.From(new ReportParams());
            var today = DateTime.Today;
            var last = Db.NDate(Db.Scalar("SELECT MAX(LogDate) FROM dbo.DailyLog WHERE CompanyId = @c AND LogDate <= @t", Db.P("@c", cs.CompanyId), Db.P("@t", today)));
            var d = last ?? today;
            var from = d.AddDays(-29);
            var led = BirdLedger.Load(cs.CompanyId, from, today);
            var logs = LogData.Load(cs.CompanyId, from, today);

            var labels = new List<string>();
            var eggs = new List<int>(); var hd = new List<decimal?>(); var mort = new List<int>(); var feedG = new List<decimal?>();
            int kBirds = 0, kEggs = 0, kMort = 0, kLayHens = 0, kOpen = 0; decimal kFeed = 0; long kFeedHens = 0;
            var flocksAlive = new HashSet<int>();
            var stageBirds = new Dictionary<string, int> { { "Chick", 0 }, { "Grower", 0 }, { "Pre-Lay", 0 }, { "Layer", 0 }, { "Broiler", 0 } };

            var byDate = led.Days.Where(x => scope.Includes(x.ShedId, x.FlockId, md)).GroupBy(x => x.Date).ToDictionary(g => g.Key, g => g.ToList());
            for (var day = from; day <= d; day = day.AddDays(1))
            {
                int e = 0, m = 0, layHens = 0; decimal f = 0; long fh = 0;
                List<LedgerDay> list;
                if (byDate.TryGetValue(day, out list))
                {
                    foreach (var ld in list)
                    {
                        FlockMeta fm; if (!md.Flocks.TryGetValue(ld.FlockId, out fm)) continue;
                        int age = Age.Days(fm.HatchDate, day);
                        LogRow lg; logs.TryGetValue(LogData.Key(ld.ShedId, day), out lg);
                        if (lg != null && lg.FlockId != ld.FlockId) lg = null;
                        m += ld.Mort + ld.Cull;
                        if (lg != null) { e += lg.TotalEggs; f += lg.FeedKg; fh += Math.Max(ld.Present, 0); }
                        if (cs.IsLaying(age, fm.IsBroiler)) layHens += Math.Max(ld.Present, 0);
                        if (day == d)
                        {
                            kOpen += ld.Opening;
                            if (ld.Closing > 0) { kBirds += ld.Closing; flocksAlive.Add(ld.FlockId); stageBirds[cs.StageOf(age, fm.IsBroiler)] += ld.Closing; }
                        }
                    }
                }
                labels.Add(day.ToString("dd MMM"));
                eggs.Add(e); mort.Add(m); hd.Add(Rep.Pct(e, layHens)); feedG.Add(fh > 0 ? Math.Round(f * 1000m / fh, 1) : (decimal?)null);
                if (day == d) { kEggs = e; kMort = m; kLayHens = layHens; kFeed = f; kFeedHens = fh; }
            }

            // shed status
            var sheds = new List<object>();
            int occupied = 0, entered = 0;
            foreach (DataRow r in Db.Query(@"SELECT v.*, fl.FlockType, (SELECT MAX(LogDate) FROM dbo.DailyLog dl WHERE dl.ShedId = v.ShedId) AS LastLog
                                             FROM dbo.vShedInfo v LEFT JOIN dbo.Flock fl ON fl.FlockId = v.FlockId WHERE v.CompanyId = @CompanyId AND v.IsActive = 1" + Ctx.ShedFilter("v.ShedId") +
                                             " ORDER BY v.FarmName, v.Code", Ctx.Scope()).Rows)
            {
                int birds = Db.Int(r["Birds"]);
                var hatch = Db.NDate(r["HatchDate"]);
                var lastLog = Db.NDate(r["LastLog"]);
                bool done = lastLog.HasValue && lastLog.Value.Date == today;
                if (birds > 0) { occupied++; if (done) entered++; }
                sheds.Add(new
                {
                    id = Db.Int(r["ShedId"]), code = Db.Str(r["Code"]), name = Db.Str(r["Name"]), farm = Db.Str(r["FarmName"]),
                    purpose = Db.Str(r["Purpose"]), status = Db.Str(r["Status"]), flock = Db.Str(r["FlockCode"]),
                    age = hatch.HasValue && birds > 0 ? Age.Text(hatch.Value, today) : "",
                    stage = hatch.HasValue && birds > 0 ? cs.StageOf(Age.Days(hatch.Value, today), Db.Str(r["FlockType"]) == "Broiler") : "",
                    birds = birds, capacity = Db.Int(r["Capacity"]),
                    fill = Db.Int(r["Capacity"]) > 0 ? Math.Round(birds * 100m / Db.Int(r["Capacity"]), 0) : 0,
                    lastLog = lastLog.HasValue ? lastLog.Value.ToString("dd-MM-yyyy") : "-", today = done
                });
            }

            var pend = Db.Row(@"SELECT
                (SELECT COUNT(*) FROM dbo.FlockTransfer t WHERE t.CompanyId = @CompanyId AND t.Status = N'Pending'
                    AND (@Restricted = 0 OR t.FromShedId IN (SELECT ShedId FROM dbo.UserShed WHERE UserId = @UserId))) AS Trf,
                (SELECT COUNT(*) FROM dbo.MedicationLog m WHERE m.CompanyId = @CompanyId AND m.ApprovalStatus = N'Pending'" + Ctx.ShedFilter("m.ShedId") + @") AS Vac,
                (SELECT COUNT(*) FROM dbo.MedicationLog m WHERE m.CompanyId = @CompanyId AND m.Status = N'Planned' AND m.LogDate <= @t" + Ctx.ShedFilter("m.ShedId") + @") AS Due",
                Ctx.Scope(Db.P("@t", today)));

            return new
            {
                company = cs.Name,
                asOf = d.ToString("dd-MM-yyyy"),
                isToday = d == today,
                kpi = new
                {
                    birds = kBirds, flocks = flocksAlive.Count, eggs = kEggs, trays = Rep.EggTrays(kEggs),
                    hd = Rep.Pct(kEggs, kLayHens), mort = kMort, mortPct = Rep.Pct(kMort, kOpen),
                    feed = Math.Round(kFeed, 0), gPerBird = kFeedHens > 0 ? Math.Round(kFeed * 1000m / kFeedHens, 1) : (decimal?)null,
                    occupied = occupied, entered = entered
                },
                series = new { labels = labels, eggs = eggs, hd = hd, mort = mort, feedG = feedG },
                stages = stageBirds,
                sheds = sheds,
                pending = new { transfers = Db.Int(pend["Trf"]), vaccines = Db.Int(pend["Vac"]), due = Db.Int(pend["Due"]) }
            };
        }

        [WebMethod(EnableSession = true)]
        public static object GetCost()
        {
            Guard();
            if (!Ctx.InRole(Roles.SuperAdmin, Roles.FarmManager, Roles.Accountant)) return null;
            var cs = Ctx.Company;
            var md = MasterData.Load(cs.CompanyId);
            var to = DateTime.Today.AddDays(-1);
            var from = to.AddDays(-29);
            var res = CostEngine.Compute(cs, md, RateBook.Load(cs.CompanyId), from, to, true);
            decimal op = 0, dep = 0, feed = 0; int eggs = 0;
            foreach (var r in res.Rows) if (r.Laying) { op += r.Operating; dep += r.Depreciation; feed += r.Feed; eggs += r.SaleableEggs; }
            return new
            {
                from = from.ToString("dd-MM"), to = to.ToString("dd-MM"), eggs = eggs,
                opPerEgg = eggs > 0 ? Math.Round(op / eggs, 3) : (decimal?)null,
                totalPerEgg = eggs > 0 ? Math.Round((op + dep) / eggs, 3) : (decimal?)null,
                feedShare = op > 0 ? Math.Round(feed * 100m / op, 1) : (decimal?)null
            };
        }
    }
}
