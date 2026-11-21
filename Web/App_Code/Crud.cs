using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace PoultryERP
{
    public enum FieldType { Text, TextArea, Int, Decimal, Date, Time, Bool, Select }

    public class CrudField
    {
        public string Name, Label, Help;
        public FieldType Type = FieldType.Text;
        public bool Required, InsertOnly, LookupIsText;
        public int MaxLength = 150;
        public string LookupSql;          // must return Value, Text (may use @CompanyId, @UserId, @Restricted)
        public string EmptyText;          // first option for optional selects
        public string[] Options;          // static options for Select
        public string Col = "col-sm-6 col-lg-4";
        public Func<object> Default;
        public decimal? Min, Max;

        public CrudField(string name, string label, FieldType type) { Name = name; Label = label; Type = type; }
    }

    public class CrudDef
    {
        public string Key, Title, Table, Pk, Description, Icon = "table";
        public string[] ViewRoles, EditRoles;
        public bool CompanyScoped = true, AllowInsert = true, AllowDelete = true, SetCreatedBy;
        public List<CrudField> Fields = new List<CrudField>();
        public string ListSql;            // SELECT <pk> AS Id, ... FROM ... WHERE ...CompanyId = @CompanyId  (no ORDER BY)
        public string OrderBy = "Id DESC";
        public string[] SearchCols = new string[0];
        public int PageSize = 25;
        public Dictionary<string, string> ListFormats = new Dictionary<string, string>();
        /// <summary>Extra validation: (values, id or null) -> error text or null.</summary>
        public Func<Dictionary<string, object>, int?, string> Validate;
        /// <summary>Runs inside the save transaction after an insert: (conn, tx, newId).</summary>
        public Action<SqlConnection, SqlTransaction, int> AfterInsert;

        public CrudField F(string name, string label, FieldType type)
        {
            var f = new CrudField(name, label, type);
            Fields.Add(f);
            return f;
        }

        public CrudField Field(string name)
        {
            foreach (var f in Fields) if (f.Name == name) return f;
            return null;
        }
    }

    /// <summary>All master / finance screens that follow the simple list + form pattern.</summary>
    public static class CrudRegistry
    {
        const string SA = Roles.SuperAdmin, FM = Roles.FarmManager, SUP = Roles.Supervisor, VET = Roles.Veterinarian, ACC = Roles.Accountant;

        static readonly Dictionary<string, Func<CrudDef>> defs = new Dictionary<string, Func<CrudDef>>(StringComparer.OrdinalIgnoreCase)
        {
            { "farms", Farms }, { "locations", Locations }, { "sheds", Sheds }, { "breeds", Breeds }, { "breed-standards", BreedStandards },
            { "items", Items }, { "item-rates", ItemRates }, { "suppliers", Suppliers }, { "customers", Customers },
            { "overheads", Overheads }, { "sales", Sales }, { "egg-wastage", EggWastage }, { "stock-receipts", StockReceipts },
            { "vaccine-schedule", VaccineSchedule }, { "postmortem", PostMortem },
            { "company-settings", CompanySettingsDef }, { "companies", Companies }, { "subscriptions", Subscriptions }
        };

        public static CrudDef Get(string key)
        {
            Func<CrudDef> f;
            if (key == null || !defs.TryGetValue(key, out f)) return null;
            var d = f();
            d.Key = key.ToLowerInvariant();
            return d;
        }

        // ---------- shared lookups ----------
        const string LkFarms = "SELECT FarmId AS Value, Code + ' - ' + Name AS Text FROM dbo.Farm WHERE CompanyId = @CompanyId AND IsActive = 1 ORDER BY Name";
        const string LkSheds = "SELECT s.ShedId AS Value, s.Code + ' - ' + s.Name AS Text FROM dbo.Shed s WHERE s.CompanyId = @CompanyId AND s.IsActive = 1 ORDER BY s.Code";
        const string LkBreeds = "SELECT BreedId AS Value, Name + ' (' + BirdType + ')' AS Text FROM dbo.Breed WHERE CompanyId = @CompanyId AND IsActive = 1 ORDER BY Name";
        const string LkCustomers = "SELECT CustomerId AS Value, Name AS Text FROM dbo.Customer WHERE CompanyId = @CompanyId AND IsActive = 1 ORDER BY Name";
        const string LkSuppliers = "SELECT SupplierId AS Value, Name + ' (' + SupplierType + ')' AS Text FROM dbo.Supplier WHERE CompanyId = @CompanyId AND IsActive = 1 ORDER BY Name";
        const string LkItems = "SELECT ItemId AS Value, Code + ' - ' + Name + ' [' + Unit + ']' AS Text FROM dbo.Item WHERE CompanyId = @CompanyId AND IsActive = 1 ORDER BY Category, Name";

        static object Today() { return DateTime.Today; }

        static bool ShedInFarm(object shed, object farm)
        {
            if (shed == null || farm == null) return true;
            return Db.Int(Db.Scalar("SELECT COUNT(*) FROM dbo.Shed s JOIN dbo.Location l ON l.LocationId = s.LocationId WHERE s.ShedId = @s AND l.FarmId = @f",
                Db.P("@s", shed), Db.P("@f", farm))) > 0;
        }

        // ================= MASTERS =================
        static CrudDef Farms()
        {
            var d = new CrudDef { Title = "Farms", Table = "Farm", Pk = "FarmId", Icon = "geo-alt", ViewRoles = new[] { SA, FM }, EditRoles = new[] { SA, FM },
                Description = "Each farm with its geographic location. Locations (blocks) and sheds sit under a farm." };
            d.F("Code", "Farm code", FieldType.Text).Required = true;
            d.Fields[0].MaxLength = 20; d.Fields[0].Col = "col-sm-4 col-lg-2";
            d.F("Name", "Farm name", FieldType.Text).Required = true;
            d.F("State", "State", FieldType.Text).MaxLength = 60;
            d.F("District", "District", FieldType.Text).MaxLength = 60;
            d.F("Village", "Village / town", FieldType.Text).MaxLength = 100;
            var lat = d.F("Latitude", "Latitude", FieldType.Decimal); lat.Min = -90; lat.Max = 90; lat.Col = "col-6 col-lg-2"; lat.Help = "e.g. 29.6856";
            var lng = d.F("Longitude", "Longitude", FieldType.Decimal); lng.Min = -180; lng.Max = 180; lng.Col = "col-6 col-lg-2";
            d.F("ManagerName", "Farm manager", FieldType.Text).MaxLength = 100;
            var oe = d.F("OpeningEggStock", "Opening egg stock", FieldType.Int); oe.Min = 0; oe.Default = () => 0; oe.Help = "Eggs in store at the end of the opening date";
            d.F("OpeningEggDate", "Opening stock date", FieldType.Date);
            d.F("Address", "Address", FieldType.TextArea).MaxLength = 400;
            d.F("IsActive", "Active", FieldType.Bool).Default = () => true;
            d.ListSql = @"SELECT f.FarmId AS Id, f.Code, f.Name, f.State, f.District, f.Latitude, f.Longitude, f.ManagerName AS Manager,
                          (SELECT COUNT(*) FROM dbo.Shed s JOIN dbo.Location l ON l.LocationId = s.LocationId WHERE l.FarmId = f.FarmId) AS Sheds, f.IsActive AS Active
                          FROM dbo.Farm f WHERE f.CompanyId = @CompanyId";
            d.OrderBy = "Code"; d.SearchCols = new[] { "Code", "Name", "State", "District" };
            d.ListFormats["Latitude"] = "N4"; d.ListFormats["Longitude"] = "N4";
            return d;
        }

        static CrudDef Locations()
        {
            var d = new CrudDef { Title = "Locations / Blocks", Table = "Location", Pk = "LocationId", Icon = "signpost-split", ViewRoles = new[] { SA, FM }, EditRoles = new[] { SA, FM },
                Description = "Blocks or areas inside a farm (e.g. Brooding block, Laying block)." };
            var f = d.F("FarmId", "Farm", FieldType.Select); f.Required = true; f.LookupSql = LkFarms;
            d.F("Name", "Location name", FieldType.Text).Required = true;
            d.F("Description", "Description", FieldType.Text).MaxLength = 300;
            d.F("IsActive", "Active", FieldType.Bool).Default = () => true;
            d.ListSql = @"SELECT l.LocationId AS Id, f.Name AS Farm, l.Name AS Location, l.Description,
                          (SELECT COUNT(*) FROM dbo.Shed s WHERE s.LocationId = l.LocationId) AS Sheds, l.IsActive AS Active
                          FROM dbo.Location l JOIN dbo.Farm f ON f.FarmId = l.FarmId WHERE l.CompanyId = @CompanyId";
            d.OrderBy = "Farm, Location"; d.SearchCols = new[] { "Farm", "Location" };
            return d;
        }

        static CrudDef Sheds()
        {
            var d = new CrudDef { Title = "Sheds", Table = "Shed", Pk = "ShedId", Icon = "house-door", ViewRoles = new[] { SA, FM }, EditRoles = new[] { SA, FM },
                Description = "Shed master: capacity, ventilation, housing system and operational status. Current birds come from the flock ledger." };
            var l = d.F("LocationId", "Farm / location", FieldType.Select); l.Required = true;
            l.LookupSql = "SELECT l.LocationId AS Value, f.Name + ' / ' + l.Name AS Text FROM dbo.Location l JOIN dbo.Farm f ON f.FarmId = l.FarmId WHERE l.CompanyId = @CompanyId AND l.IsActive = 1 ORDER BY f.Name, l.Name";
            var c = d.F("Code", "Shed code", FieldType.Text); c.Required = true; c.MaxLength = 20; c.Col = "col-sm-4 col-lg-2"; c.Help = "e.g. LHD-1, CH-2";
            d.F("Name", "Shed name", FieldType.Text).Required = true;
            var cap = d.F("Capacity", "Bird capacity", FieldType.Int); cap.Required = true; cap.Min = 1; cap.Col = "col-sm-4 col-lg-2";
            var pu = d.F("Purpose", "Purpose", FieldType.Select); pu.Options = new[] { "Brooding", "Grower", "Laying", "Multi-stage", "Broiler" }; pu.Required = true; pu.LookupIsText = true;
            var v = d.F("VentilationType", "Ventilation", FieldType.Select); v.Options = new[] { "Open-sided", "Environment Controlled" }; v.Required = true; v.LookupIsText = true;
            var h = d.F("HousingSystem", "Housing system", FieldType.Select); h.Options = new[] { "Cage", "Floor", "Deep Litter", "Aviary" }; h.Required = true; h.LookupIsText = true;
            var st = d.F("Status", "Operational status", FieldType.Select); st.Options = new[] { "Active", "Vacant", "Cleaning", "Quarantine" }; st.Required = true; st.LookupIsText = true;
            var ln = d.F("LengthFt", "Length (ft)", FieldType.Decimal); ln.Min = 0; ln.Col = "col-6 col-lg-2";
            var wd = d.F("WidthFt", "Width (ft)", FieldType.Decimal); wd.Min = 0; wd.Col = "col-6 col-lg-2";
            d.F("IsActive", "Active", FieldType.Bool).Default = () => true;
            d.ListSql = @"SELECT v.ShedId AS Id, v.Code, v.Name, v.FarmName AS Farm, v.LocationName AS Location, v.Purpose, v.VentilationType AS Ventilation,
                          v.HousingSystem AS Housing, v.Capacity, v.Birds, v.FlockCode AS Flock, v.Status, v.IsActive AS Active
                          FROM dbo.vShedInfo v WHERE v.CompanyId = @CompanyId";
            d.OrderBy = "Farm, Code"; d.SearchCols = new[] { "Code", "Name", "Farm", "Location", "Status", "Flock" };
            d.Validate = (vals, id) =>
            {
                var status = Convert.ToString(vals["Status"]);
                if (id.HasValue && (status == "Vacant" || status == "Cleaning"))
                {
                    int birds = Db.Int(Db.Scalar("SELECT ISNULL(SUM(Birds),0) FROM dbo.vShedOccupancy WHERE ShedId = @s", Db.P("@s", id.Value)));
                    if (birds > 0) return "The shed still has " + birds.ToString("N0") + " birds. Transfer or deplete them before marking it " + status + ".";
                }
                return null;
            };
            return d;
        }

        static CrudDef Breeds()
        {
            var d = new CrudDef { Title = "Breeds / Strains", Table = "Breed", Pk = "BreedId", Icon = "bookmark-star", ViewRoles = new[] { SA, FM, VET }, EditRoles = new[] { SA, FM, VET },
                Description = "Breed / strain master. Enter the breeder's weekly performance standard under Breed Standards." };
            d.F("Name", "Breed / strain name", FieldType.Text).Required = true;
            var bt = d.F("BirdType", "Bird type", FieldType.Select); bt.Required = true; bt.LookupIsText = true; bt.Options = new[] { "Layer", "Broiler" }; bt.Default = () => "Layer";
            d.F("StrainCode", "Strain code", FieldType.Text).MaxLength = 40;
            d.F("BreederCompany", "Breeder company", FieldType.Text).MaxLength = 100;
            d.F("Notes", "Notes", FieldType.TextArea).MaxLength = 400;
            d.F("IsActive", "Active", FieldType.Bool).Default = () => true;
            d.ListSql = @"SELECT b.BreedId AS Id, b.Name, b.BirdType AS [Type], b.StrainCode AS Strain, b.BreederCompany AS Breeder,
                          (SELECT COUNT(*) FROM dbo.BreedStandard s WHERE s.BreedId = b.BreedId) AS [Std Weeks],
                          (SELECT COUNT(*) FROM dbo.Flock f WHERE f.BreedId = b.BreedId) AS Flocks, b.IsActive AS Active
                          FROM dbo.Breed b WHERE b.CompanyId = @CompanyId";
            d.OrderBy = "Name"; d.SearchCols = new[] { "Name", "Strain", "Breeder" };
            return d;
        }

        static CrudDef BreedStandards()
        {
            var d = new CrudDef { Title = "Breed Standards", Table = "BreedStandard", Pk = "BreedStandardId", Icon = "graph-up", ViewRoles = new[] { SA, FM, VET }, EditRoles = new[] { SA, FM, VET },
                Description = "Weekly breeder benchmarks (age = completed weeks). Layers: body wt, feed, HD %, eggs/HH, egg wt. Broilers: body wt and cumulative feed at the end of the week, daily feed during the week and FCR - values for each day are interpolated.", PageSize = 100 };
            var b = d.F("BreedId", "Breed", FieldType.Select); b.Required = true; b.LookupSql = LkBreeds;
            var a = d.F("AgeWeek", "Age (weeks)", FieldType.Int); a.Required = true; a.Min = 0; a.Max = 120; a.Col = "col-6 col-lg-2";
            var bw = d.F("BodyWtG", "Body wt (g)", FieldType.Decimal); bw.Min = 0; bw.Col = "col-6 col-lg-2";
            var fg = d.F("FeedGBirdDay", "Feed g/bird/day", FieldType.Decimal); fg.Min = 0; fg.Max = 300; fg.Col = "col-6 col-lg-2";
            var hd = d.F("HDPPct", "HD %", FieldType.Decimal); hd.Min = 0; hd.Max = 100; hd.Col = "col-6 col-lg-2";
            var hh = d.F("HHEggsCum", "Cum eggs / HH", FieldType.Decimal); hh.Min = 0; hh.Col = "col-6 col-lg-2";
            var ew = d.F("EggWtG", "Egg wt (g)", FieldType.Decimal); ew.Min = 0; ew.Max = 100; ew.Col = "col-6 col-lg-2";
            var lv = d.F("LivabilityPct", "Livability %", FieldType.Decimal); lv.Min = 0; lv.Max = 100; lv.Col = "col-6 col-lg-2";
            var cf = d.F("CumFeedG", "Cum feed g/bird (broiler)", FieldType.Decimal); cf.Min = 0; cf.Col = "col-6 col-lg-2";
            var fc = d.F("StdFCR", "Std FCR (broiler)", FieldType.Decimal); fc.Min = 0; fc.Max = 5; fc.Col = "col-6 col-lg-2";
            d.ListSql = @"SELECT s.BreedStandardId AS Id, b.Name AS Breed, s.AgeWeek AS [Age Wk], s.BodyWtG AS [Body Wt g], s.FeedGBirdDay AS [Feed g/day],
                          s.HDPPct AS [HD %], s.HHEggsCum AS [Cum HH], s.EggWtG AS [Egg Wt g], s.LivabilityPct AS [Livability %], s.CumFeedG AS [Cum Feed g], s.StdFCR AS FCR
                          FROM dbo.BreedStandard s JOIN dbo.Breed b ON b.BreedId = s.BreedId WHERE s.CompanyId = @CompanyId";
            d.OrderBy = "Breed, [Age Wk]"; d.SearchCols = new[] { "Breed" };
            d.ListFormats["Age Wk"] = "N0"; d.ListFormats["Body Wt g"] = "N0"; d.ListFormats["Feed g/day"] = "N1"; d.ListFormats["Cum HH"] = "N1"; d.ListFormats["Egg Wt g"] = "N1"; d.ListFormats["Cum Feed g"] = "N0"; d.ListFormats["FCR"] = "N3";
            return d;
        }

        static CrudDef Items()
        {
            var d = new CrudDef { Title = "Items", Table = "Item", Pk = "ItemId", Icon = "boxes", ViewRoles = new[] { SA, FM, ACC }, EditRoles = new[] { SA, FM, ACC },
                Description = "Feed formulations, shell grit, medicines, vaccines and packing. Prices are kept with history under Item Prices." };
            var c = d.F("Code", "Item code", FieldType.Text); c.Required = true; c.MaxLength = 30; c.Col = "col-sm-4 col-lg-2";
            d.F("Name", "Item name", FieldType.Text).Required = true;
            var cat = d.F("Category", "Category", FieldType.Select); cat.Required = true; cat.LookupIsText = true; cat.Options = new[] { "FEED", "GRIT", "MEDICINE", "VACCINE", "PACKING", "OTHER" };
            var u = d.F("Unit", "Unit", FieldType.Select); u.Required = true; u.LookupIsText = true; u.Options = new[] { "KG", "G", "LTR", "ML", "DOSE", "NOS", "BAG" }; u.Col = "col-6 col-lg-2";
            var fs = d.F("FeedStage", "Feed stage (feed only)", FieldType.Select); fs.LookupIsText = true; fs.Options = new[] { "Chick", "Grower", "Pre-Lay", "Layer", "Broiler" }; fs.EmptyText = "(not feed)";
            d.F("IsActive", "Active", FieldType.Bool).Default = () => true;
            d.ListSql = @"SELECT i.ItemId AS Id, i.Code, i.Name, i.Category, i.Unit, i.FeedStage AS [Feed Stage], dbo.fn_ItemRate(i.ItemId, CAST(GETDATE() AS DATE)) AS [Current Rate], i.IsActive AS Active
                          FROM dbo.Item i WHERE i.CompanyId = @CompanyId";
            d.OrderBy = "Category, Name"; d.SearchCols = new[] { "Code", "Name", "Category" };
            d.Validate = (v, id) => v["FeedStage"] != null && Convert.ToString(v["Category"]) != "FEED" ? "Feed stage applies only to FEED items." : null;
            return d;
        }

        static CrudDef ItemRates()
        {
            var d = new CrudDef { Title = "Item Prices", Table = "ItemRate", Pk = "ItemRateId", Icon = "currency-rupee", ViewRoles = new[] { SA, FM, ACC }, EditRoles = new[] { SA, FM, ACC },
                Description = "Price per unit with an effective date. Costing uses the price in force on each day, so add a new row for a price change rather than editing the old one." };
            var it = d.F("ItemId", "Item", FieldType.Select); it.Required = true; it.LookupSql = LkItems; it.Col = "col-lg-5";
            var ef = d.F("EffectiveFrom", "Effective from", FieldType.Date); ef.Required = true; ef.Default = Today;
            var r = d.F("Rate", "Rate per unit (Rs)", FieldType.Decimal); r.Required = true; r.Min = 0;
            d.F("Remarks", "Remarks", FieldType.Text).MaxLength = 200;
            d.ListSql = @"SELECT r.ItemRateId AS Id, i.Code, i.Name AS Item, i.Category, i.Unit, r.EffectiveFrom AS [Effective From], r.Rate, r.Remarks
                          FROM dbo.ItemRate r JOIN dbo.Item i ON i.ItemId = r.ItemId WHERE r.CompanyId = @CompanyId";
            d.OrderBy = "Category, Item, [Effective From] DESC"; d.SearchCols = new[] { "Code", "Item", "Category" };
            d.ListFormats["Rate"] = "N4";
            return d;
        }

        static CrudDef Suppliers()
        {
            var d = new CrudDef { Title = "Suppliers / Hatcheries", Table = "Supplier", Pk = "SupplierId", Icon = "truck", ViewRoles = new[] { SA, FM, ACC }, EditRoles = new[] { SA, FM, ACC },
                Description = "Hatcheries (chick source / parent lineage), feed mills and medicine suppliers." };
            d.F("Name", "Name", FieldType.Text).Required = true;
            var t = d.F("SupplierType", "Type", FieldType.Select); t.Required = true; t.LookupIsText = true; t.Options = new[] { "Hatchery", "Feed", "Medicine", "Other" };
            d.F("ContactPerson", "Contact person", FieldType.Text).MaxLength = 100;
            d.F("Phone", "Phone", FieldType.Text).MaxLength = 40;
            d.F("GSTIN", "GSTIN", FieldType.Text).MaxLength = 20;
            d.F("Address", "Address", FieldType.TextArea).MaxLength = 400;
            d.F("IsActive", "Active", FieldType.Bool).Default = () => true;
            d.ListSql = "SELECT SupplierId AS Id, Name, SupplierType AS Type, ContactPerson AS Contact, Phone, GSTIN, IsActive AS Active FROM dbo.Supplier WHERE CompanyId = @CompanyId";
            d.OrderBy = "Name"; d.SearchCols = new[] { "Name", "Type", "Phone" };
            return d;
        }

        static CrudDef Customers()
        {
            var d = new CrudDef { Title = "Customers", Table = "Customer", Pk = "CustomerId", Icon = "people", ViewRoles = new[] { SA, FM, ACC }, EditRoles = new[] { SA, FM, ACC },
                Description = "Egg and bird buyers." };
            d.F("Name", "Customer name", FieldType.Text).Required = true;
            d.F("Phone", "Phone", FieldType.Text).MaxLength = 40;
            d.F("GSTIN", "GSTIN", FieldType.Text).MaxLength = 20;
            d.F("Address", "Address", FieldType.TextArea).MaxLength = 400;
            d.F("IsActive", "Active", FieldType.Bool).Default = () => true;
            d.ListSql = @"SELECT c.CustomerId AS Id, c.Name, c.Phone, c.GSTIN,
                          (SELECT ISNULL(SUM(s.Amount - s.AmountReceived),0) FROM dbo.Sale s WHERE s.CustomerId = c.CustomerId) AS Outstanding, c.IsActive AS Active
                          FROM dbo.Customer c WHERE c.CompanyId = @CompanyId";
            d.OrderBy = "Name"; d.SearchCols = new[] { "Name", "Phone" };
            return d;
        }

        // ================= FINANCE =================
        static CrudDef Overheads()
        {
            var d = new CrudDef { Title = "Overheads & Labour", Table = "Overhead", Pk = "OverheadId", Icon = "cash-stack", ViewRoles = new[] { SA, FM, ACC }, EditRoles = new[] { SA, FM, ACC }, SetCreatedBy = true,
                Description = "Labour, utility and other overheads for a period. Leave Shed empty to share the cost across the farm's sheds on bird-days." };
            var f = d.F("FarmId", "Farm", FieldType.Select); f.Required = true; f.LookupSql = LkFarms;
            var s = d.F("ShedId", "Shed", FieldType.Select); s.LookupSql = LkSheds; s.EmptyText = "(all sheds - allocate on bird-days)";
            var h = d.F("CostHead", "Cost head", FieldType.Select); h.Required = true; h.LookupIsText = true; h.Options = new[] { "Labor", "Utility", "Overhead", "Other" };
            var fr = d.F("FromDate", "From", FieldType.Date); fr.Required = true; fr.Default = () => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var to = d.F("ToDate", "To", FieldType.Date); to.Required = true; to.Default = () => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1);
            var a = d.F("Amount", "Amount (Rs)", FieldType.Decimal); a.Required = true; a.Min = 0;
            d.F("Description", "Description", FieldType.Text).MaxLength = 300;
            d.ListSql = @"SELECT o.OverheadId AS Id, f.Name AS Farm, ISNULL(s.Code, 'All sheds') AS Shed, o.CostHead AS Head, o.FromDate AS [From], o.ToDate AS [To],
                          o.Amount, o.Description FROM dbo.Overhead o JOIN dbo.Farm f ON f.FarmId = o.FarmId LEFT JOIN dbo.Shed s ON s.ShedId = o.ShedId
                          WHERE o.CompanyId = @CompanyId";
            d.OrderBy = "[From] DESC, Id DESC"; d.SearchCols = new[] { "Farm", "Shed", "Head", "Description" };
            d.Validate = (v, id) =>
            {
                if (Convert.ToDateTime(v["FromDate"]) > Convert.ToDateTime(v["ToDate"])) return "From date must be on or before To date.";
                if (!ShedInFarm(v["ShedId"], v["FarmId"])) return "The selected shed does not belong to the selected farm.";
                return null;
            };
            return d;
        }

        static CrudDef Sales()
        {
            var d = new CrudDef { Title = "Sales (Eggs / Birds)", Table = "Sale", Pk = "SaleId", Icon = "cart-check", ViewRoles = new[] { SA, FM, ACC }, EditRoles = new[] { SA, FM, ACC }, SetCreatedBy = true,
                Description = "Invoice-level sales of eggs, cull birds, manure etc. Eggs collected in the sheds go to the farm's egg godown; egg sales are made from the godown (no shed) and cannot exceed the godown stock. Cull birds are sold from a shed and must also be entered as 'Birds sold' in the Daily Shed Entry. Broiler sales are entered under Broiler Sales / Lifting." };
            var dt = d.F("SaleDate", "Sale date", FieldType.Date); dt.Required = true; dt.Default = Today;
            var f = d.F("FarmId", "Farm", FieldType.Select); f.Required = true; f.LookupSql = LkFarms;
            var c = d.F("CustomerId", "Customer", FieldType.Select); c.LookupSql = LkCustomers; c.EmptyText = "(cash / walk-in)";
            d.F("InvoiceNo", "Invoice / DM no.", FieldType.Text).MaxLength = 40;
            var p = d.F("Product", "Product", FieldType.Select); p.Required = true; p.LookupIsText = true;
            p.Options = new[] { "Table Egg", "Cracked Egg", "Double Yolk", "Misshapen Egg", "Cull Bird", "Manure", "Other" };
            var s = d.F("ShedId", "From shed (cull birds only)", FieldType.Select); s.LookupSql = LkSheds; s.EmptyText = "(egg godown / not shed specific)";
            s.Help = "Leave blank for eggs: eggs are sold from the farm's egg godown.";
            var q = d.F("Qty", "Quantity (nos / units)", FieldType.Decimal); q.Required = true; q.Min = 0.01m;
            var kg = d.F("QtyKg", "Weight (Kg)", FieldType.Decimal); kg.Min = 0;
            var r = d.F("Rate", "Rate (Rs)", FieldType.Decimal); r.Required = true; r.Min = 0;
            var rb = d.F("RateBasis", "Rate is", FieldType.Select); rb.Required = true; rb.LookupIsText = true; rb.Options = new[] { "Per Unit", "Per Kg" }; rb.Default = () => "Per Unit";
            var rc = d.F("AmountReceived", "Amount received (Rs)", FieldType.Decimal); rc.Min = 0; rc.Default = () => 0m;
            d.F("Remarks", "Remarks", FieldType.Text).MaxLength = 300;
            d.ListSql = @"SELECT s.SaleId AS Id, s.SaleDate AS [Date], s.InvoiceNo AS Invoice, c.Name AS Customer, s.Product, s.Qty, s.Rate, s.Amount,
                          s.AmountReceived AS Received, s.Amount - s.AmountReceived AS Balance
                          FROM dbo.Sale s LEFT JOIN dbo.Customer c ON c.CustomerId = s.CustomerId WHERE s.CompanyId = @CompanyId AND s.Product <> N'Broiler Bird'";
            d.OrderBy = "[Date] DESC, Id DESC"; d.SearchCols = new[] { "Invoice", "Customer", "Product" };
            d.ListFormats["Qty"] = "N0";
            d.Validate = (v, id) =>
            {
                if (Convert.ToString(v["RateBasis"]) == "Per Kg" && v["QtyKg"] == null) return "Enter the weight (Kg) when the rate is per Kg.";
                decimal amt = (Convert.ToString(v["RateBasis"]) == "Per Kg" ? Convert.ToDecimal(v["QtyKg"]) : Convert.ToDecimal(v["Qty"])) * Convert.ToDecimal(v["Rate"]);
                if (v["AmountReceived"] != null && Convert.ToDecimal(v["AmountReceived"]) > amt + 0.01m) return "Amount received cannot exceed the invoice amount (" + amt.ToString("N2") + ").";
                string product = Convert.ToString(v["Product"]);
                bool egg = Array.IndexOf(PoultryERP.Reports.Rep.EggProducts, product) >= 0;
                if (egg)
                {
                    // eggs are sold from the farm's egg godown, never from a shed
                    v["ShedId"] = null;
                    int farm = Convert.ToInt32(v["FarmId"]);
                    var day = Convert.ToDateTime(v["SaleDate"]).Date;
                    int qty = (int)Math.Round(Convert.ToDecimal(v["Qty"]));
                    // godown stock at the end of the sale date (includes that day's collection, sales and wastage)
                    int stock = PoultryERP.Reports.Rep.EggStockBefore(Ctx.CompanyId, farm, day.AddDays(1));
                    if (id.HasValue)
                    {
                        // the record being edited is already counted in the stock: add it back
                        var old = Db.Row("SELECT FarmId, SaleDate, Product, Qty FROM dbo.Sale WHERE SaleId = @id AND CompanyId = @c", Db.P("@id", id.Value), Db.P("@c", Ctx.CompanyId));
                        if (old != null && Db.Int(old["FarmId"]) == farm && Convert.ToDateTime(old["SaleDate"]).Date <= day
                            && Array.IndexOf(PoultryERP.Reports.Rep.EggProducts, Db.Str(old["Product"])) >= 0)
                            stock += (int)Math.Round(Db.Dec(old["Qty"]));
                    }
                    if (qty > stock)
                        return "Only " + stock.ToString("N0") + " eggs are in the egg godown of this farm on " + day.ToString("dd-MM-yyyy") +
                               ". Check that the daily shed entries (egg collection) are complete, or the opening egg stock.";
                }
                else if (product == "Cull Bird" && v["ShedId"] == null)
                    return "Select the shed the cull birds were sold from (and enter them as 'Birds sold' in the Daily Shed Entry).";
                if (!ShedInFarm(v["ShedId"], v["FarmId"])) return "The selected shed does not belong to the selected farm.";
                return null;
            };
            return d;
        }

        static CrudDef EggWastage()
        {
            var d = new CrudDef { Title = "Egg Wastage", Table = "EggWastage", Pk = "WastageId", Icon = "trash3", ViewRoles = new[] { SA, FM, ACC }, EditRoles = new[] { SA, FM, ACC }, SetCreatedBy = true,
                Description = "Eggs discarded from the egg store (breakage, thin shell, rejected)." };
            var dt = d.F("WasteDate", "Date", FieldType.Date); dt.Required = true; dt.Default = Today;
            var f = d.F("FarmId", "Farm", FieldType.Select); f.Required = true; f.LookupSql = LkFarms;
            var q = d.F("Qty", "Eggs wasted", FieldType.Int); q.Required = true; q.Min = 1;
            d.F("Reason", "Reason", FieldType.Text).MaxLength = 200;
            d.ListSql = "SELECT w.WastageId AS Id, w.WasteDate AS [Date], f.Name AS Farm, w.Qty AS Eggs, w.Reason FROM dbo.EggWastage w JOIN dbo.Farm f ON f.FarmId = w.FarmId WHERE w.CompanyId = @CompanyId";
            d.OrderBy = "[Date] DESC, Id DESC"; d.SearchCols = new[] { "Farm", "Reason" };
            return d;
        }

        static CrudDef StockReceipts()
        {
            var d = new CrudDef { Title = "Stock Receipts", Table = "StockReceipt", Pk = "ReceiptId", Icon = "box-seam", ViewRoles = new[] { SA, FM, ACC }, EditRoles = new[] { SA, FM, ACC },
                Description = "Feed, grit, medicine and vaccine purchases received at a farm. Consumption is deducted automatically from daily entries." };
            var dt = d.F("ReceiptDate", "Receipt date", FieldType.Date); dt.Required = true; dt.Default = Today;
            var f = d.F("FarmId", "Farm", FieldType.Select); f.Required = true; f.LookupSql = LkFarms;
            var it = d.F("ItemId", "Item", FieldType.Select); it.Required = true; it.LookupSql = LkItems;
            var s = d.F("SupplierId", "Supplier", FieldType.Select); s.LookupSql = LkSuppliers; s.EmptyText = "(none)";
            d.F("InvoiceNo", "Invoice no.", FieldType.Text).MaxLength = 40;
            var q = d.F("Qty", "Quantity", FieldType.Decimal); q.Required = true; q.Min = 0.001m;
            var r = d.F("Rate", "Rate per unit (Rs)", FieldType.Decimal); r.Required = true; r.Min = 0;
            d.F("Remarks", "Remarks", FieldType.Text).MaxLength = 200;
            d.ListSql = @"SELECT r.ReceiptId AS Id, r.ReceiptDate AS [Date], f.Name AS Farm, i.Name AS Item, i.Unit, r.Qty, r.Rate, r.Amount, s.Name AS Supplier, r.InvoiceNo AS Invoice
                          FROM dbo.StockReceipt r JOIN dbo.Farm f ON f.FarmId = r.FarmId JOIN dbo.Item i ON i.ItemId = r.ItemId LEFT JOIN dbo.Supplier s ON s.SupplierId = r.SupplierId
                          WHERE r.CompanyId = @CompanyId";
            d.OrderBy = "[Date] DESC, Id DESC"; d.SearchCols = new[] { "Item", "Supplier", "Invoice" };
            return d;
        }

        // ================= HEALTH =================
        static CrudDef VaccineSchedule()
        {
            var d = new CrudDef { Title = "Vaccination Schedule", Table = "VaccineSchedule", Pk = "ScheduleId", Icon = "calendar2-week", ViewRoles = new[] { SA, FM, VET }, EditRoles = new[] { SA, FM, VET },
                Description = "Template programme by age in days. Use 'Generate schedule' on the Medication screen to create planned entries for a flock." };
            var b = d.F("BreedId", "Breed", FieldType.Select); b.LookupSql = LkBreeds; b.EmptyText = "(all layer breeds)";
            var a = d.F("AgeDay", "Age (days)", FieldType.Int); a.Required = true; a.Min = 0; a.Max = 800; a.Col = "col-6 col-lg-2";
            var it = d.F("ItemId", "Vaccine", FieldType.Select); it.Required = true;
            it.LookupSql = "SELECT ItemId AS Value, Name AS Text FROM dbo.Item WHERE CompanyId = @CompanyId AND Category IN ('VACCINE','MEDICINE') AND IsActive = 1 ORDER BY Category DESC, Name";
            var r = d.F("Route", "Route", FieldType.Select); r.LookupIsText = true; r.Options = new[] { "Drinking water", "Eye drop", "Nasal", "Injection", "Wing web", "Spray", "Feed" }; r.EmptyText = "";
            var dose = d.F("DosePerBird", "Doses per bird", FieldType.Decimal); dose.Required = true; dose.Min = 0; dose.Default = () => 1m; dose.Col = "col-6 col-lg-2";
            d.F("Notes", "Notes", FieldType.Text).MaxLength = 200;
            d.ListSql = @"SELECT v.ScheduleId AS Id, ISNULL(b.Name, 'All layer breeds') AS Breed, v.AgeDay AS [Age Day], CAST(v.AgeDay / 7 AS VARCHAR(5)) + '-' + CAST(v.AgeDay % 7 AS VARCHAR(2)) AS [Age W-D],
                          i.Name AS Vaccine, v.Route, v.DosePerBird AS [Dose/Bird], v.Notes
                          FROM dbo.VaccineSchedule v JOIN dbo.Item i ON i.ItemId = v.ItemId LEFT JOIN dbo.Breed b ON b.BreedId = v.BreedId WHERE v.CompanyId = @CompanyId";
            d.OrderBy = "[Age Day]"; d.SearchCols = new[] { "Vaccine", "Breed", "Route" }; d.PageSize = 100;
            d.ListFormats["Age Day"] = "N0";
            return d;
        }

        static CrudDef PostMortem()
        {
            var d = new CrudDef { Title = "Post-Mortem Notes", Table = "PostMortem", Pk = "PostMortemId", Icon = "clipboard2-pulse", ViewRoles = new[] { SA, FM, VET }, EditRoles = new[] { SA, FM, VET }, SetCreatedBy = true,
                Description = "Veterinary post-mortem findings. They appear in the Mortality Analysis report." };
            var dt = d.F("PmDate", "Date", FieldType.Date); dt.Required = true; dt.Default = Today;
            var s = d.F("ShedId", "Shed", FieldType.Select); s.Required = true; s.LookupSql = LkSheds;
            var b = d.F("BirdsExamined", "Birds examined", FieldType.Int); b.Required = true; b.Min = 1; b.Default = () => 1;
            var v = d.F("VetName", "Veterinarian", FieldType.Text); v.MaxLength = 100; v.Default = () => Ctx.User == null ? "" : Ctx.User.FullName;
            var fi = d.F("Findings", "Findings", FieldType.TextArea); fi.Required = true; fi.MaxLength = 1000; fi.Col = "col-lg-6";
            var ac = d.F("ActionTaken", "Action taken / advice", FieldType.TextArea); ac.MaxLength = 500; ac.Col = "col-lg-6";
            d.F("Diagnosis", "Diagnosis", FieldType.Text).MaxLength = 300;
            d.ListSql = @"SELECT p.PostMortemId AS Id, p.PmDate AS [Date], s.Code AS Shed, p.BirdsExamined AS Birds, p.Diagnosis, p.Findings, p.VetName AS Vet
                          FROM dbo.PostMortem p JOIN dbo.Shed s ON s.ShedId = p.ShedId WHERE p.CompanyId = @CompanyId";
            d.OrderBy = "[Date] DESC, Id DESC"; d.SearchCols = new[] { "Shed", "Diagnosis", "Findings" };
            return d;
        }

        // ================= ADMINISTRATION =================
        static CrudDef CompanySettingsDef()
        {
            var d = new CrudDef { Title = "Company Settings", Table = "Company", Pk = "CompanyId", Icon = "sliders", ViewRoles = new[] { SA, FM }, EditRoles = new[] { SA, FM },
                AllowInsert = false, AllowDelete = false,
                Description = "Lifecycle stage boundaries, tray size, supervisor edit window and costing rules for the active company." };
            d.F("Name", "Company name", FieldType.Text).Required = true;
            d.F("Phone", "Phone", FieldType.Text).MaxLength = 40;
            d.F("Email", "Email", FieldType.Text).MaxLength = 150;
            d.F("GSTIN", "GSTIN", FieldType.Text).MaxLength = 20;
            d.F("Address", "Address", FieldType.TextArea).MaxLength = 400;
            var c = d.F("StageChickEndWk", "Chicks (brooding) up to week", FieldType.Int); c.Required = true; c.Min = 1; c.Max = 20; c.Help = "Default 8";
            var g = d.F("StageGrowerEndWk", "Grower up to week", FieldType.Int); g.Required = true; g.Min = 2; g.Max = 25; g.Help = "Default 15";
            var p = d.F("StagePreLayEndWk", "Pre-lay up to week", FieldType.Int); p.Required = true; p.Min = 3; p.Max = 30; p.Help = "Default 18; laying starts the week after";
            var t = d.F("EggsPerTray", "Eggs per tray", FieldType.Int); t.Required = true; t.Min = 1; t.Max = 100;
            var e = d.F("EditWindowDays", "Supervisor edit window (days back)", FieldType.Int); e.Required = true; e.Min = 0; e.Max = 30;
            e.Help = "0 = today only, 1 = today and yesterday. Older records need a manager override.";
            d.F("CrackedIsSaleable", "Count cracked eggs as saleable", FieldType.Bool);
            var bm = d.F("BroilerMaxDays", "Broiler maximum age (days)", FieldType.Int); bm.Required = true; bm.Min = 30; bm.Max = 120; bm.Default = () => 70;
            bm.Help = "Broiler batches older than this are flagged for lifting";
            d.ListSql = @"SELECT CompanyId AS Id, Code, Name, StageChickEndWk AS [Chick to Wk], StageGrowerEndWk AS [Grower to Wk], StagePreLayEndWk AS [Pre-Lay to Wk],
                          EggsPerTray AS [Eggs/Tray], EditWindowDays AS [Edit Window], CrackedIsSaleable AS [Cracked Saleable], BroilerMaxDays AS [Broiler Max Days] FROM dbo.Company WHERE CompanyId = @CompanyId";
            d.OrderBy = "Code";
            foreach (var k in new[] { "Chick to Wk", "Grower to Wk", "Pre-Lay to Wk", "Eggs/Tray", "Edit Window", "Broiler Max Days" }) d.ListFormats[k] = "N0";
            d.Validate = (v, id) =>
            {
                int a = Convert.ToInt32(v["StageChickEndWk"]), b = Convert.ToInt32(v["StageGrowerEndWk"]), cc = Convert.ToInt32(v["StagePreLayEndWk"]);
                return a < b && b < cc ? null : "Stage weeks must increase: Chick < Grower < Pre-lay.";
            };
            return d;
        }

        static CrudDef Companies()
        {
            var d = new CrudDef { Title = "Companies (Tenants)", Table = "Company", Pk = "CompanyId", Icon = "buildings", ViewRoles = new[] { SA }, EditRoles = new[] { SA },
                CompanyScoped = false, AllowDelete = false,
                Description = "Each company is a separate tenant with its own farms, users and data. New companies receive a copy of the breed standards, items and vaccine schedule of the first company." };
            var c = d.F("Code", "Company code", FieldType.Text); c.Required = true; c.MaxLength = 20; c.InsertOnly = true;
            d.F("Name", "Company name", FieldType.Text).Required = true;
            d.F("Phone", "Phone", FieldType.Text).MaxLength = 40;
            d.F("Email", "Email", FieldType.Text).MaxLength = 150;
            d.F("GSTIN", "GSTIN", FieldType.Text).MaxLength = 20;
            d.F("Address", "Address", FieldType.TextArea).MaxLength = 400;
            d.F("IsActive", "Active", FieldType.Bool).Default = () => true;
            d.ListSql = @"SELECT c.CompanyId AS Id, c.Code, c.Name, (SELECT COUNT(*) FROM dbo.Farm f WHERE f.CompanyId = c.CompanyId) AS Farms,
                          (SELECT COUNT(*) FROM dbo.AppUser u WHERE u.CompanyId = c.CompanyId) AS Users, c.CreatedOn AS Created, c.IsActive AS Active FROM dbo.Company c";
            d.OrderBy = "Code"; d.SearchCols = new[] { "Code", "Name" };
            d.AfterInsert = (cn, tx, newId) => Db.Exec(cn, tx, TemplateCopySql, Db.P("@new", newId));
            return d;
        }

        const string TemplateCopySql = @"
DECLARE @src INT = (SELECT MIN(CompanyId) FROM dbo.Company WHERE CompanyId <> @new);
IF @src IS NOT NULL
BEGIN
    INSERT INTO dbo.Breed (CompanyId, Name, StrainCode, BreederCompany, Notes, IsActive)
    SELECT @new, Name, StrainCode, BreederCompany, Notes, IsActive FROM dbo.Breed WHERE CompanyId = @src;
    INSERT INTO dbo.BreedStandard (CompanyId, BreedId, AgeWeek, BodyWtG, FeedGBirdDay, HDPPct, HHEggsCum, EggWtG, LivabilityPct)
    SELECT @new, nb.BreedId, s.AgeWeek, s.BodyWtG, s.FeedGBirdDay, s.HDPPct, s.HHEggsCum, s.EggWtG, s.LivabilityPct
    FROM dbo.BreedStandard s JOIN dbo.Breed ob ON ob.BreedId = s.BreedId JOIN dbo.Breed nb ON nb.CompanyId = @new AND nb.Name = ob.Name
    WHERE s.CompanyId = @src;
    INSERT INTO dbo.Item (CompanyId, Code, Name, Category, Unit, FeedStage, IsActive)
    SELECT @new, Code, Name, Category, Unit, FeedStage, IsActive FROM dbo.Item WHERE CompanyId = @src;
    INSERT INTO dbo.ItemRate (CompanyId, ItemId, EffectiveFrom, Rate, Remarks)
    SELECT @new, ni.ItemId, r.EffectiveFrom, r.Rate, r.Remarks FROM dbo.ItemRate r JOIN dbo.Item oi ON oi.ItemId = r.ItemId
    JOIN dbo.Item ni ON ni.CompanyId = @new AND ni.Code = oi.Code WHERE r.CompanyId = @src;
    INSERT INTO dbo.VaccineSchedule (CompanyId, BreedId, AgeDay, ItemId, Route, DosePerBird, Notes)
    SELECT @new, NULL, v.AgeDay, ni.ItemId, v.Route, v.DosePerBird, v.Notes FROM dbo.VaccineSchedule v JOIN dbo.Item oi ON oi.ItemId = v.ItemId
    JOIN dbo.Item ni ON ni.CompanyId = @new AND ni.Code = oi.Code WHERE v.CompanyId = @src;
END";

        static CrudDef Subscriptions()
        {
            var d = new CrudDef { Title = "Subscriptions & Billing", Table = "Subscription", Pk = "SubscriptionId", Icon = "receipt", ViewRoles = new[] { SA }, EditRoles = new[] { SA },
                CompanyScoped = false,
                Description = "Plan, limits and billing per company. Users of a Suspended / Expired company (or past the end date) cannot sign in." };
            var c = d.F("CompanyId", "Company", FieldType.Select); c.Required = true;
            c.LookupSql = "SELECT CompanyId AS Value, Code + ' - ' + Name AS Text FROM dbo.Company ORDER BY Code";
            d.F("PlanName", "Plan", FieldType.Text).Required = true;
            var mf = d.F("MaxFarms", "Max farms", FieldType.Int); mf.Min = 1; mf.Col = "col-6 col-lg-2";
            var mu = d.F("MaxUsers", "Max users", FieldType.Int); mu.Min = 1; mu.Col = "col-6 col-lg-2";
            var sd = d.F("StartDate", "Start date", FieldType.Date); sd.Required = true; sd.Default = Today;
            d.F("EndDate", "End date", FieldType.Date);
            var a = d.F("AmountPerMonth", "Amount / month (Rs)", FieldType.Decimal); a.Min = 0; a.Default = () => 0m;
            var st = d.F("Status", "Status", FieldType.Select); st.Required = true; st.LookupIsText = true; st.Options = new[] { "Trial", "Active", "Suspended", "Expired" };
            d.F("Notes", "Notes", FieldType.Text).MaxLength = 400;
            d.ListSql = @"SELECT s.SubscriptionId AS Id, c.Code AS Company, s.PlanName AS [Plan], s.MaxFarms AS [Max Farms], s.MaxUsers AS [Max Users], s.StartDate AS [Start],
                          s.EndDate AS [End], s.AmountPerMonth AS [Rs / Month], s.Status FROM dbo.Subscription s JOIN dbo.Company c ON c.CompanyId = s.CompanyId";
            d.OrderBy = "Company, [Start] DESC"; d.SearchCols = new[] { "Company", "Plan", "Status" };
            d.ListFormats["Max Farms"] = "N0"; d.ListFormats["Max Users"] = "N0";
            return d;
        }
    }
}
