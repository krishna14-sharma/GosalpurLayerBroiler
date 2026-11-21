using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace PoultryERP
{
    /// <summary>
    /// Go-live screen: creates flocks + placements for the birds in each shed today, the opening egg stock of
    /// each farm and the opening store stock, in one transaction. Optional history columns book a single
    /// catch-up daily entry (mortality and feed to date) so FCR / livability / P&amp;L of running batches are correct.
    /// </summary>
    public partial class OpeningBalancePage : BasePage
    {
        protected override string[] AllowedRoles { get { return Roles.Managers; } }

        DataTable breeds;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            txtGoLive.Text = Ui.DateValue(DateTime.Today);
            txtGoLive.Attributes["max"] = Ui.DateValue(DateTime.Today.AddDays(1));
            BindAll();
        }

        void BindAll()
        {
            breeds = Db.Query("SELECT BreedId, Name, BirdType FROM dbo.Breed WHERE CompanyId = @c AND IsActive = 1 ORDER BY BirdType DESC, Name", Db.P("@c", Ctx.CompanyId));
            var sheds = Db.Query(@"SELECT v.ShedId, v.Code, v.Purpose, v.Capacity FROM dbo.vShedInfo v
                                   WHERE v.CompanyId = @c AND v.IsActive = 1 AND v.Birds = 0 ORDER BY v.FarmName, v.Code", Db.P("@c", Ctx.CompanyId));
            rptSheds.DataSource = sheds; rptSheds.DataBind();
            litNoSheds.Text = sheds.Rows.Count == 0 ? "<div class=\"empty-state py-3\">All sheds already hold birds, or no sheds exist yet (Masters &gt; Sheds).</div>" : "";
            rptEggs.DataSource = Db.Query("SELECT FarmId, Name, OpeningEggStock, OpeningEggDate FROM dbo.Farm WHERE CompanyId = @c AND IsActive = 1 ORDER BY Name", Db.P("@c", Ctx.CompanyId));
            rptEggs.DataBind();
            Ui.Bind(ddlStoreFarm, "SELECT FarmId AS Value, Name AS Text FROM dbo.Farm WHERE CompanyId = @c AND IsActive = 1 ORDER BY Name", null, Db.P("@c", Ctx.CompanyId));
            rptStock.DataSource = Db.Query(@"SELECT ItemId, Name, Category, Unit, dbo.fn_ItemRate(ItemId, CAST(GETDATE() AS DATE)) AS Rate FROM dbo.Item
                                             WHERE CompanyId = @c AND IsActive = 1 AND Category IN ('FEED','GRIT','MEDICINE','VACCINE','PACKING') ORDER BY Category, Name", Db.P("@c", Ctx.CompanyId));
            rptStock.DataBind();
        }

        protected void rptSheds_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            if (e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem) return;
            var ddl = (DropDownList)e.Item.FindControl("ddlBreed");
            ddl.Items.Add(new ListItem("-- breed --", ""));
            var purpose = Convert.ToString(DataBinder.Eval(e.Item.DataItem, "Purpose"));
            foreach (DataRow r in breeds.Rows)
                ddl.Items.Add(new ListItem(Db.Str(r["Name"]) + " (" + Db.Str(r["BirdType"]) + ")", Db.Str(r["BreedId"])));
            // preselect the only breed of the matching type
            string want = purpose == "Broiler" ? "Broiler" : "Layer";
            DataRow only = null; int n = 0;
            foreach (DataRow r in breeds.Rows) if (Db.Str(r["BirdType"]) == want) { only = r; n++; }
            if (n == 1) ddl.SelectedValue = Db.Str(only["BreedId"]);
        }

        static string T(RepeaterItem it, string id) { return (((TextBox)it.FindControl(id)).Text ?? "").Trim(); }

        class ShedLine
        {
            public int ShedId, BreedId, Birds, Chicks;
            public string ShedCode, Code, Type;
            public DateTime Hatch, Placed;
            public decimal Value, Feed;
            public int? Housed;
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            pnlDone.Visible = false;
            var err = new List<string>();
            var goLive = Ui.Date(txtGoLive.Text);
            if (!goLive.HasValue) { ShowErr(new List<string> { "Enter the go-live date." }); return; }
            var opening = goLive.Value.AddDays(-1);
            var breedType = new Dictionary<int, string>();
            foreach (DataRow r in Db.Query("SELECT BreedId, BirdType FROM dbo.Breed WHERE CompanyId = @c", Db.P("@c", Ctx.CompanyId)).Rows)
                breedType[Db.Int(r["BreedId"])] = Db.Str(r["BirdType"]);

            // ---- sheds ----
            var lines = new List<ShedLine>();
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RepeaterItem it in rptSheds.Items)
            {
                var birdsTxt = T(it, "txtBirds");
                if (birdsTxt.Length == 0) continue;
                var l = new ShedLine { ShedId = int.Parse(((HiddenField)it.FindControl("hidShed")).Value, CultureInfo.InvariantCulture) };
                l.ShedCode = Ops.ShedCode(null, null, l.ShedId);
                string p = "Shed " + l.ShedCode + ": ";
                var birds = Ui.Int(birdsTxt);
                if (!birds.HasValue || birds <= 0) { err.Add(p + "birds today must be a positive number."); continue; }
                l.Birds = birds.Value;
                l.Code = T(it, "txtCode");
                if (l.Code.Length == 0) err.Add(p + "enter a flock / batch code.");
                else codes.Add(l.Code);   // the same code on several sheds = one flock placed in several sheds
                var breed = Ui.SelInt((DropDownList)it.FindControl("ddlBreed"));
                if (!breed.HasValue || !breedType.ContainsKey(breed.Value)) err.Add(p + "select the breed.");
                else { l.BreedId = breed.Value; l.Type = breedType[breed.Value]; }
                var hatch = Ui.Date(T(it, "txtHatch"));
                if (!hatch.HasValue) err.Add(p + "enter the hatch date.");
                else if (hatch > goLive) err.Add(p + "hatch date is after the go-live date.");
                else l.Hatch = hatch.Value;
                l.Value = Ui.Dec(T(it, "txtValue")) ?? 0;
                if (l.Value < 0) err.Add(p + "value per bird cannot be negative.");
                if (T(it, "txtHoused").Length > 0)
                {
                    l.Housed = Ui.Int(T(it, "txtHoused"));
                    if (!l.Housed.HasValue || l.Housed <= 0) err.Add(p + "hens housed must be a positive number.");
                }
                var placed = Ui.Date(T(it, "txtPlaced"));
                l.Placed = placed ?? goLive.Value;
                l.Chicks = Ui.Int(T(it, "txtChicks")) ?? l.Birds;
                l.Feed = Ui.Dec(T(it, "txtFeed")) ?? 0;
                if (placed.HasValue)
                {
                    if (hatch.HasValue && placed < hatch) err.Add(p + "placement date is before the hatch date.");
                    if (placed > opening) err.Add(p + "placement date (history) must be before the go-live date.");
                }
                if (l.Chicks < l.Birds) err.Add(p + "chicks placed cannot be less than birds today.");
                if (!placed.HasValue && (l.Chicks > l.Birds || l.Feed > 0)) err.Add(p + "give the original placement date to record mortality / feed before go-live.");
                if (l.Feed < 0) err.Add(p + "feed cannot be negative.");
                if (l.Type == "Broiler" && hatch.HasValue && Age.Days(hatch.Value, goLive.Value) > Company.BroilerMaxDays)
                    err.Add(p + "broiler batch would be " + Age.Days(hatch.Value, goLive.Value) + " days old - above the " + Company.BroilerMaxDays + "-day limit.");
                lines.Add(l);
            }
            // rows sharing a code are one flock: breed and hatch date must agree; an existing flock with that code gets the new sheds added
            var existing = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in lines.Where(x => x.Code.Length > 0 && x.BreedId > 0 && x.Hatch != DateTime.MinValue).GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
            {
                var first = g.First();
                if (g.Any(x => x.BreedId != first.BreedId)) err.Add("Flock " + g.Key + ": all its sheds must have the same breed.");
                if (g.Any(x => x.Hatch != first.Hatch)) err.Add("Flock " + g.Key + ": all its sheds must have the same hatch date.");
                if (g.Select(x => x.ShedId).Distinct().Count() != g.Count()) err.Add("Flock " + g.Key + ": a shed is listed twice.");
                var dt = Db.Query("SELECT FlockId, BreedId, HatchDate, Status, InitialCount, HenHousedCount, ChickRate FROM dbo.Flock WHERE CompanyId = @c AND FlockCode = @f",
                    Db.P("@c", Ctx.CompanyId), Db.P("@f", g.Key));
                if (dt.Rows.Count == 0) continue;
                var r = dt.Rows[0];
                if (Db.Str(r["Status"]) != "Active") err.Add("Flock " + g.Key + " already exists and is closed (" + Db.Str(r["Status"]) + "). Use a different code.");
                else if (Db.Int(r["BreedId"]) != first.BreedId || Convert.ToDateTime(r["HatchDate"]).Date != first.Hatch.Date)
                    err.Add("Flock " + g.Key + " already exists with breed/hatch date " + Ui.D(r["HatchDate"]) + ". Use the same breed and hatch date to add sheds to it, or a different code.");
                else existing[g.Key] = r;
            }

            // ---- eggs ----
            var eggs = new List<KeyValuePair<int, int>>();
            foreach (RepeaterItem it in rptEggs.Items)
            {
                var s = T(it, "txtEggs");
                if (s.Length == 0) continue;
                var n = Ui.Int(s);
                if (!n.HasValue || n < 0) { err.Add("Egg stock must be a whole number of 0 or more."); continue; }
                eggs.Add(new KeyValuePair<int, int>(int.Parse(((HiddenField)it.FindControl("hidFarm")).Value, CultureInfo.InvariantCulture), n.Value));
            }

            // ---- store ----
            var stock = new List<decimal[]>(); // item, qty, rate
            int? storeFarm = Ui.SelInt(ddlStoreFarm);
            foreach (RepeaterItem it in rptStock.Items)
            {
                var q = Ui.Dec(T(it, "txtQty"));
                if (!q.HasValue || q == 0) continue;
                var rate = Ui.Dec(T(it, "txtRate")) ?? 0;
                if (q < 0 || rate < 0) { err.Add("Store quantities and rates cannot be negative."); continue; }
                stock.Add(new[] { decimal.Parse(((HiddenField)it.FindControl("hidItem")).Value, CultureInfo.InvariantCulture), q.Value, rate });
            }
            if (stock.Count > 0 && !storeFarm.HasValue) err.Add("Select the farm for the store stock.");
            if (stock.Count > 0 && storeFarm.HasValue &&
                Db.Int(Db.Scalar("SELECT COUNT(*) FROM dbo.StockReceipt WHERE CompanyId = @c AND FarmId = @f AND InvoiceNo = N'OPENING'", Db.P("@c", Ctx.CompanyId), Db.P("@f", storeFarm.Value))) > 0)
                err.Add("Opening store stock was already entered for this farm. Correct it under Finance > Stock Receipts (invoice no. OPENING).");

            if (lines.Count == 0 && eggs.Count == 0 && stock.Count == 0) err.Add("Nothing to save - enter birds, egg stock or store stock.");
            if (err.Count > 0) { ShowErr(err); return; }

            try
            {
                Db.Tx((c, t) =>
                {
                    var flockIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var g in lines.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
                    {
                        var first = g.First();
                        bool broilerG = first.Type == "Broiler";
                        int chicks = g.Sum(x => x.Chicks);
                        int? housed = g.Any(x => x.Housed.HasValue) ? g.Sum(x => x.Housed ?? 0) : (int?)null;
                        decimal value = chicks > 0 ? Math.Round(g.Sum(x => x.Value * x.Chicks) / chicks, 2) : first.Value;   // weighted by birds
                        DateTime arrival = g.Min(x => x.Placed);
                        DataRow ex;
                        if (existing.TryGetValue(g.Key, out ex))
                        {
                            int oldN = Db.Int(ex["InitialCount"]);
                            decimal oldV = Db.Dec(ex["ChickRate"]);
                            Db.Exec(c, t, @"UPDATE dbo.Flock SET InitialCount = InitialCount + @n,
                                              HenHousedCount = CASE WHEN @hh IS NULL THEN HenHousedCount ELSE ISNULL(HenHousedCount, 0) + @hh END,
                                              ChickRate = @v, ArrivalDate = CASE WHEN @a < ArrivalDate THEN @a ELSE ArrivalDate END
                                            WHERE FlockId = @f AND CompanyId = @c",
                                Db.P("@n", chicks), Db.P("@hh", housed), Db.P("@v", oldN + chicks > 0 ? Math.Round((oldV * oldN + value * chicks) / (oldN + chicks), 2) : value),
                                Db.P("@a", arrival), Db.P("@f", Db.Int(ex["FlockId"])), Db.P("@c", Ctx.CompanyId));
                            flockIds[g.Key] = Db.Int(ex["FlockId"]);
                        }
                        else
                            flockIds[g.Key] = Db.Int(Db.Scalar(c, t, @"INSERT INTO dbo.Flock (CompanyId, FlockCode, BatchNo, BreedId, HatchDate, ArrivalDate, InitialCount, ArrivalMortality, ChickRate,
                                                FreightCost, HenHousedCount, PlannedDepletionWeek, FlockType, TargetLiftDays, Remarks, CreatedBy)
                                                VALUES (@c, @code, N'OPENING', @b, @h, @a, @n, 0, @v, 0, @hh, @dw, @type, @lift, N'Opening balance at go-live', @u);
                                                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                                Db.P("@c", Ctx.CompanyId), Db.P("@code", first.Code), Db.P("@b", first.BreedId), Db.P("@h", first.Hatch), Db.P("@a", arrival), Db.P("@n", chicks),
                                Db.P("@v", value), Db.P("@hh", housed), Db.P("@dw", broilerG ? 30 : 80), Db.P("@type", first.Type),
                                Db.P("@lift", broilerG ? (object)Math.Min(42, Company.BroilerMaxDays) : null), Db.P("@u", Ctx.UserId)));
                    }
                    foreach (var l in lines)
                    {
                        bool broiler = l.Type == "Broiler";
                        int fid = flockIds[l.Code];
                        var msg = Ops.CheckDestination(c, t, l.ShedId, fid, l.Placed);
                        if (msg != null) throw new InvalidOperationException("Shed " + l.ShedCode + ": " + msg);
                        Db.Exec(c, t, "INSERT INTO dbo.FlockPlacement (CompanyId, FlockId, ShedId, PlaceDate, BirdCount, Remarks, CreatedBy) VALUES (@c, @f, @s, @d, @n, N'Opening balance', @u)",
                            Db.P("@c", Ctx.CompanyId), Db.P("@f", fid), Db.P("@s", l.ShedId), Db.P("@d", l.Placed), Db.P("@n", l.Chicks), Db.P("@u", Ctx.UserId));
                        if (l.Chicks > l.Birds || l.Feed > 0)
                        {
                            string stage = Company.StageOf(Age.Days(l.Hatch, opening), broiler);
                            var feedItem = Db.Scalar(c, t, "SELECT TOP (1) ItemId FROM dbo.Item WHERE CompanyId = @c AND Category = 'FEED' AND IsActive = 1 ORDER BY CASE WHEN FeedStage = @st THEN 0 ELSE 1 END, ItemId",
                                Db.P("@c", Ctx.CompanyId), Db.P("@st", stage));
                            Db.Exec(c, t, @"INSERT INTO dbo.DailyLog (CompanyId, ShedId, FlockId, LogDate, Mortality, FeedItemId, FeedKg, Remarks, CreatedBy)
                                            VALUES (@c, @s, @f, @d, @m, @fi, @fk, N'Opening catch-up: mortality and feed from placement to go-live', @u)",
                                Db.P("@c", Ctx.CompanyId), Db.P("@s", l.ShedId), Db.P("@f", fid), Db.P("@d", opening), Db.P("@m", l.Chicks - l.Birds),
                                Db.P("@fi", l.Feed > 0 ? feedItem : null), Db.P("@fk", l.Feed), Db.P("@u", Ctx.UserId));
                        }
                        Ops.EnsureNoNegative(c, t, l.ShedId, fid);
                        Ops.MarkShedOccupied(c, t, l.ShedId);
                        Audit.Log(c, t, "OPENING", "Flock", fid, "shed " + l.ShedCode + "; code " + l.Code + "; birds " + l.Birds + "; chicks " + l.Chicks + "; feed " + l.Feed);
                    }
                    foreach (var kv in eggs)
                    {
                        Db.Exec(c, t, "UPDATE dbo.Farm SET OpeningEggStock = @n, OpeningEggDate = @d WHERE FarmId = @f AND CompanyId = @c",
                            Db.P("@n", kv.Value), Db.P("@d", opening), Db.P("@f", kv.Key), Db.P("@c", Ctx.CompanyId));
                        Audit.Log(c, t, "OPENING", "Farm", kv.Key, "egg stock " + kv.Value + " as at " + Ui.D(opening));
                    }
                    foreach (var s in stock)
                    {
                        Db.Exec(c, t, @"INSERT INTO dbo.StockReceipt (CompanyId, FarmId, ItemId, ReceiptDate, InvoiceNo, Qty, Rate, Remarks)
                                        VALUES (@c, @f, @i, @d, N'OPENING', @q, @r, N'Opening stock')",
                            Db.P("@c", Ctx.CompanyId), Db.P("@f", storeFarm.Value), Db.P("@i", (int)s[0]), Db.P("@d", opening), Db.P("@q", s[1]), Db.P("@r", s[2]));
                    }
                    if (stock.Count > 0) Audit.Log(c, t, "OPENING", "StockReceipt", storeFarm.Value, stock.Count + " items");
                    return 0;
                });
                pnlErr.Visible = false;
                pnlDone.Visible = true;
                int birdsTotal = 0; foreach (var l in lines) birdsTotal += l.Birds;
                litDone.Text = "<i class=\"bi bi-check-circle me-1\"></i>Saved: <b>" + lines.Select(x => x.Code.ToUpperInvariant()).Distinct().Count() + "</b> flock(s) in <b>" + lines.Count + "</b> shed(s) with <b>" + birdsTotal.ToString("N0") + "</b> birds, egg stock for <b>" +
                               eggs.Count + "</b> farm(s), <b>" + stock.Count + "</b> store item(s). Daily entries can start on <b>" + Ui.D(goLive) + "</b>.";
                Ok("Opening balances saved.");
                BindAll();
            }
            catch (Exception ex) { ShowErr(new List<string> { Db.FriendlyError(ex) }); }
        }

        void ShowErr(List<string> err)
        {
            pnlErr.Visible = true;
            var sb = new StringBuilder();
            foreach (var x in err) sb.Append("<div><i class=\"bi bi-exclamation-triangle me-1\"></i>").Append(Ui.H(x)).Append("</div>");
            litErr.Text = sb.ToString();
        }
    }
}
