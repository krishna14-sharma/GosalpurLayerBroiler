using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace PoultryERP.Reports
{
    public class ReportDef
    {
        public string Key, Title, Icon, Description, OldName;
        public string[] Roles;
        /// <summary>Filters shown: date, range, farm, shed, flock, group, product, wt</summary>
        public HashSet<string> Filters;
        public bool ShedRequired, FlockRequired;
        /// <summary>Restricts the flock dropdown: "Layer", "Broiler" or null (all).</summary>
        public string FlockType;
        public string[] ProductOptions;
        public Func<ReportParams, ReportModel> Build;

        public bool Has(string f) { return Filters.Contains(f); }
    }

    public static class ReportCatalog
    {
        const string SA = PoultryERP.Roles.SuperAdmin, FM = PoultryERP.Roles.FarmManager, SUP = PoultryERP.Roles.Supervisor,
                     VET = PoultryERP.Roles.Veterinarian, ACC = PoultryERP.Roles.Accountant;

        static ReportDef D(string key, string title, string icon, string oldName, string filters, string desc,
                           Func<ReportParams, ReportModel> build, params string[] roles)
        {
            return new ReportDef
            {
                Key = key, Title = title, Icon = icon, OldName = oldName, Description = desc, Build = build, Roles = roles,
                Filters = new HashSet<string>(filters.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            };
        }

        public static readonly List<ReportDef> All = BuildList();

        static List<ReportDef> BuildList()
        {
            var l = new List<ReportDef>();
            l.Add(D("dtr", "Daily Transaction Report", "file-earmark-spreadsheet", "Daily Transaction Report (crTrans)", "date,farm",
                "Shed-wise birds, mortality, feed per bird, egg grades, trays and HD% for one day, with egg stock and stage-wise feed totals.",
                DailyReports.Dtr, SA, FM, SUP, VET, ACC));
            l.Add(D("supervisor", "Daily Supervisor Report", "person-badge", "Daily Supervisor Report (crSupervisor)", "date,farm",
                "Shed-wise lot, age, opening birds, % production, feed g/bird, egg and body weight versus standard, with farm production %, birds by stage, feed per bird/egg and egg production cost.",
                DailyReports.Supervisor, SA, FM, SUP, VET));
            l.Add(D("chicks", "Chicks / Grower Report", "egg-fried", "Chicks/Grower Report (crTransChicks)", "range,shed",
                "Day-by-day brooding and growing record of one shed: livability, feed vs standard, body weight, water, climate and medication.",
                DailyReports.ChicksGrower, SA, FM, SUP, VET));
            l.Add(D("flockdaily", "Flock Transaction Report (II)", "list-columns", "Transaction Report II (crProd)", "range,flock",
                "Daily movement of one flock across all its sheds: opening, mortality, sale, transit loss, feed, grit and eggs.",
                DailyReports.FlockDaily, SA, FM, SUP, VET));
            l.Add(D("henhouse", "Hen House Production Report", "graph-up-arrow", "Hen House / Hen House with weight (crHenHouse)", "flock,shed,wt",
                "Weekly HD%, HH%, eggs per hen housed, feed per bird, FCR per dozen and per kg egg mass against the breeder standard.",
                PerformanceReports.HenHouse, SA, FM, SUP, VET, ACC));
            l.Add(D("transfers", "Flock Transfer / Shifting Report", "arrow-left-right", "(new)", "range,farm,flock",
                "Audit trail of every shifting: from/to shed, age at movement, transit mortality and culls, shrinkage, approval.",
                PerformanceReports.Transfers, SA, FM, SUP, VET));
            l.Add(D("mortality", "Mortality Analysis", "heart-pulse", "(new)", "range,farm,shed,flock",
                "Mortality and culls by shed and week, peak days, and post-mortem findings for the veterinary team.",
                PerformanceReports.Mortality, SA, FM, VET));
            l.Add(D("costegg", "Cost per Egg Report", "currency-rupee", "(new)", "range,farm,shed,flock,group",
                "Trend of variable and fixed cost drivers per saleable egg (feed, grit, medicine, labour, utility, overheads, depreciation).",
                FinanceReports.CostPerEgg, SA, FM, ACC));
            l.Add(D("flockcost", "Flock Cost of Production", "calculator", "Cost of Production (crCost)", "flock",
                "Life-to-date cost sheet of one flock split into rearing and laying, per bird housed and per saleable egg.",
                FinanceReports.FlockCost, SA, FM, ACC));
            var stock = D("stock", "Feed & Medicine Stock", "box-seam", "Medicine Stock (CrMediStock)", "range,farm,product",
                "Opening, receipts, consumption (daily feed, grit, medication) and closing stock with value.",
                FinanceReports.Stock, SA, FM, ACC, VET);
            stock.ProductOptions = new[] { "FEED", "GRIT", "MEDICINE", "VACCINE", "PACKING", "OTHER" };
            l.Add(stock);
            var sales = D("sales", "Eggs / Bird Sale Register", "cart-check", "Eggs/Bird Sale (CrItemSale)", "range,farm,product",
                "Invoice-wise sales with quantity, rate, amount, receipts and balance.",
                FinanceReports.Sales, SA, FM, ACC);
            sales.ProductOptions = new[] { "Table Egg", "Cracked Egg", "Double Yolk", "Misshapen Egg", "Cull Bird", "Manure", "Other" };
            l.Add(sales);
            l.Add(D("recon", "Egg & Revenue Reconciliation", "arrow-repeat", "(new)", "range,farm",
                "Egg stock reconciliation (production, sales, wastage), revenue received and outstanding, cost versus revenue.",
                FinanceReports.Reconciliation, SA, FM, ACC));

            l.Add(D("brdaily", "Broiler Daily Report", "egg-fried", "(new - broiler)", "date,farm",
                "Broiler sheds for one day: birds, mortality, lifting, feed per bird, cumulative feed, body weight and estimated FCR against the standard.",
                BroilerReports.Daily, SA, FM, SUP, VET, ACC));
            l.Add(D("brperf", "Broiler Performance & FCR", "speedometer", "(new - broiler)", "flock,group",
                "Daily or weekly growth of a broiler batch: body weight, gain, feed, cumulative feed per bird and FCR versus the breeder standard, with EPEF.",
                BroilerReports.Performance, SA, FM, SUP, VET, ACC));
            l.Add(D("brpl", "Broiler Batch Profit & Loss", "piggy-bank", "(new - broiler)", "flock",
                "Profit and loss of one batch: bird sales against chicks, feed, medicine and allocated labour / utility / overheads, per bird and per kg.",
                BroilerReports.BatchPnL, SA, FM, ACC));
            l.Add(D("brplsum", "Broiler P&L Summary", "bar-chart-line", "(new - broiler)", "range,farm",
                "All broiler batches of a period side by side: livability, FCR, EPEF, cost and rate per kg, profit per batch.",
                BroilerReports.PnLSummary, SA, FM, ACC));

            foreach (var d in l)
            {
                if (d.Key == "chicks") d.ShedRequired = true;
                if (d.Key == "flockdaily" || d.Key == "henhouse" || d.Key == "flockcost" || d.Key == "brperf" || d.Key == "brpl") d.FlockRequired = true;
                if (d.Key == "henhouse" || d.Key == "flockcost") d.FlockType = "Layer";
                if (d.Key == "brperf" || d.Key == "brpl") d.FlockType = "Broiler";
            }
            return l;
        }

        public static ReportDef Get(string key)
        {
            foreach (var d in All) if (string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase)) return d;
            return null;
        }
    }

    /// <summary>Helpers shared by the report builders.</summary>
    public static class Rep
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static ReportModel New(string key, string title, string subtitle)
        {
            var u = Ctx.User;
            return new ReportModel
            {
                Key = key, Title = title, Subtitle = subtitle, CompanyName = Ctx.Company.Name,
                GeneratedBy = u == null ? "" : u.FullName
            };
        }

        public static string D(DateTime d) { return d.ToString("dd-MM-yyyy", Inv); }

        public static string ScopeText(ReportParams p, MasterData md, bool includeShed, bool includeFlock)
        {
            var parts = new List<string>();
            if (p.FarmId.HasValue)
            {
                string n;
                parts.Add("Farm: " + (md.FarmNames.TryGetValue(p.FarmId.Value, out n) ? n : "#" + p.FarmId));
            }
            if (includeShed && p.ShedId.HasValue) parts.Add("Shed: " + md.ShedCode(p.ShedId.Value));
            if (includeFlock && p.FlockId.HasValue) parts.Add("Flock: " + md.FlockCode(p.FlockId.Value));
            if (Ctx.ShedRestricted) parts.Add("Assigned sheds only");
            return parts.Count == 0 ? "All farms" : string.Join("  |  ", parts.ToArray());
        }

        public static decimal? Pct(decimal a, decimal b) { return b == 0 ? (decimal?)null : Math.Round(a * 100m / b, 2); }
        public static decimal? Div(decimal a, decimal b, int dec) { return b == 0 ? (decimal?)null : Math.Round(a / b, dec); }
        public static decimal? Diff(decimal? a, decimal? b) { return a.HasValue && b.HasValue ? a.Value - b.Value : (decimal?)null; }

        public static string N(decimal? v, int dec) { return v.HasValue ? Fmt.N(v.Value, dec) : "-"; }

        public static int EggTrays(int eggs) { int t = Ctx.Company.EggsPerTray; return t <= 0 ? 0 : eggs / t; }

        public static readonly string[] EggProducts = { "Table Egg", "Cracked Egg", "Double Yolk", "Misshapen Egg" };

        /// <summary>Book egg stock at the start of a date (opening stock + production - egg sales - wastage before it).</summary>
        public static int EggStockBefore(int companyId, int? farmId, DateTime before)
        {
            const string sql = @"
SELECT
  ISNULL((SELECT SUM(f.OpeningEggStock) FROM dbo.Farm f WHERE f.CompanyId = @c AND (@farm IS NULL OR f.FarmId = @farm)
          AND (f.OpeningEggDate IS NULL OR f.OpeningEggDate < @d)), 0)
+ ISNULL((SELECT SUM(d.TotalEggs) FROM dbo.DailyLog d JOIN dbo.Shed s ON s.ShedId = d.ShedId JOIN dbo.Location l ON l.LocationId = s.LocationId
          WHERE d.CompanyId = @c AND d.LogDate < @d AND (@farm IS NULL OR l.FarmId = @farm)
          AND (l.FarmId NOT IN (SELECT FarmId FROM dbo.Farm WHERE OpeningEggDate IS NOT NULL AND OpeningEggDate >= d.LogDate))), 0)
- ISNULL((SELECT SUM(x.Qty) FROM dbo.Sale x WHERE x.CompanyId = @c AND x.SaleDate < @d AND (@farm IS NULL OR x.FarmId = @farm)
          AND x.Product IN (N'Table Egg', N'Cracked Egg', N'Double Yolk', N'Misshapen Egg')
          AND (x.FarmId NOT IN (SELECT FarmId FROM dbo.Farm WHERE OpeningEggDate IS NOT NULL AND OpeningEggDate >= x.SaleDate))), 0)
- ISNULL((SELECT SUM(w.Qty) FROM dbo.EggWastage w WHERE w.CompanyId = @c AND w.WasteDate < @d AND (@farm IS NULL OR w.FarmId = @farm)
          AND (w.FarmId NOT IN (SELECT FarmId FROM dbo.Farm WHERE OpeningEggDate IS NOT NULL AND OpeningEggDate >= w.WasteDate))), 0)";
            return Db.Int(Db.Scalar(sql, Db.P("@c", companyId), Db.P("@farm", farmId), Db.P("@d", before)));
        }

        /// <summary>Hen-housed birds of a flock: the stored figure, else birds alive at the start of the laying stage.</summary>
        public static int HenHoused(FlockMeta fm, CompanySettings cs)
        {
            if (fm.HenHousedCount.HasValue && fm.HenHousedCount.Value > 0) return fm.HenHousedCount.Value;
            var ls = fm.HatchDate.AddDays(cs.LayStartAgeDays);
            var at = ls > fm.ArrivalDate ? ls : fm.ArrivalDate.AddDays(1);
            int n = BirdLedger.FlockBirdsBefore(fm.FlockId, at);
            return n > 0 ? n : fm.InitialCount - fm.ArrivalMortality;
        }

        public static List<FlockMeta> SortedFlocks(MasterData md)
        {
            var l = new List<FlockMeta>(md.Flocks.Values);
            l.Sort((a, b) => a.HatchDate.CompareTo(b.HatchDate));
            return l;
        }
    }
}
