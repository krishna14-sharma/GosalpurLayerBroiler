using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.UI.WebControls;
using PoultryERP.Reports;

namespace PoultryERP
{
    /// <summary>
    /// Multi-line sale invoice: one header (date, farm, customer, invoice no., amount received) and several lines,
    /// each with its own product, quantity and rate. Every line is stored as one dbo.Sale row sharing the invoice no.,
    /// so all existing reports (sales register, egg stock, reconciliation, P&amp;L) keep working unchanged.
    /// </summary>
    public partial class SaleInvoicePage : BasePage
    {
        protected override string[] AllowedRoles { get { return new[] { Roles.SuperAdmin, Roles.FarmManager, Roles.Accountant }; } }

        static readonly string[] Products = { "Table Egg", "Cracked Egg", "Double Yolk", "Misshapen Egg", "Cull Bird", "Manure", "Other" };
        const int StartLines = 5;

        class Line
        {
            public string Product, Basis = "Per Unit";
            public int? ShedId;
            public decimal? Qty, Kg, Rate;
            public decimal Amount { get { return Math.Round(((Basis == "Per Kg" ? Kg : Qty) ?? 0m) * (Rate ?? 0m), 2); } }
            public bool Egg { get { return Array.IndexOf(Rep.EggProducts, Product) >= 0; } }
        }

        DateTime SDate { get { return Ui.Date(txtDate.Text) ?? DateTime.Today; } }
        int LineCount { get { return ViewState["n"] == null ? StartLines : (int)ViewState["n"]; } set { ViewState["n"] = value; } }
        DataTable shedTable;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            txtDate.Text = Ui.DateValue(DateTime.Today);
            txtDate.Attributes["max"] = Ui.DateValue(DateTime.Today);
            txtFrom.Text = Ui.DateValue(DateTime.Today.AddDays(-30)); txtTo.Text = Ui.DateValue(DateTime.Today);
            Ui.Bind(ddlFarm, "SELECT FarmId AS Value, Code + ' - ' + Name AS Text FROM dbo.Farm WHERE CompanyId = @c AND IsActive = 1 ORDER BY Name", "-- select --", Db.P("@c", Ctx.CompanyId));
            if (ddlFarm.Items.Count == 2) ddlFarm.SelectedIndex = 1;
            Ui.Bind(ddlCustomer, "SELECT CustomerId AS Value, Name AS Text FROM dbo.Customer WHERE CompanyId = @c AND IsActive = 1 ORDER BY Name", "(cash / walk-in)", Db.P("@c", Ctx.CompanyId));
            BindLines(new List<Line>());
            ShowStock();
            BindList();
        }

        // ------------------------------------------------------------------ lines

        void BindLines(List<Line> lines)
        {
            while (lines.Count < LineCount) lines.Add(new Line());
            LineCount = lines.Count;
            shedTable = Db.Query("SELECT s.ShedId AS Value, s.Code + ' - ' + s.Name AS Text FROM dbo.Shed s JOIN dbo.Location l ON l.LocationId = s.LocationId " +
                                 "WHERE s.CompanyId = @c AND s.IsActive = 1 AND (@f IS NULL OR l.FarmId = @f) ORDER BY s.Code",
                                 Db.P("@c", Ctx.CompanyId), Db.P("@f", Ui.SelInt(ddlFarm)));
            rptLines.DataSource = lines;
            rptLines.DataBind();
        }

        protected void rptLines_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            var ln = (Line)e.Item.DataItem;
            var p = (DropDownList)e.Item.FindControl("ddlProd");
            Ui.BindList(p, Products, "-- product --"); Ui.Select(p, ln.Product);
            var s = (DropDownList)e.Item.FindControl("ddlShed");
            Ui.Bind(s, shedTable, "Value", "Text", "(egg godown)"); Ui.Select(s, ln.ShedId);
            var b = (DropDownList)e.Item.FindControl("ddlBasis");
            Ui.BindList(b, new[] { "Per Unit", "Per Kg" }, null); Ui.Select(b, ln.Basis);
            ((TextBox)e.Item.FindControl("txtQty")).Text = Ui.NumValue(ln.Qty);
            ((TextBox)e.Item.FindControl("txtKg")).Text = Ui.NumValue(ln.Kg);
            ((TextBox)e.Item.FindControl("txtRate")).Text = Ui.NumValue(ln.Rate);
        }

        List<Line> ReadLines(List<string> err)
        {
            var list = new List<Line>();
            int n = 0;
            foreach (RepeaterItem it in rptLines.Items)
            {
                n++;
                var ln = new Line
                {
                    Product = ((DropDownList)it.FindControl("ddlProd")).SelectedValue,
                    ShedId = Ui.SelInt((DropDownList)it.FindControl("ddlShed")),
                    Basis = ((DropDownList)it.FindControl("ddlBasis")).SelectedValue,
                    Qty = Ui.Dec(((TextBox)it.FindControl("txtQty")).Text),
                    Kg = Ui.Dec(((TextBox)it.FindControl("txtKg")).Text),
                    Rate = Ui.Dec(((TextBox)it.FindControl("txtRate")).Text)
                };
                if (string.IsNullOrEmpty(ln.Basis)) ln.Basis = "Per Unit";
                bool blank = string.IsNullOrEmpty(ln.Product) && !ln.Qty.HasValue && !ln.Kg.HasValue && !ln.Rate.HasValue;
                if (blank) continue;
                string p = "Line " + n + ": ";
                if (string.IsNullOrEmpty(ln.Product)) err.Add(p + "select the product.");
                if (!ln.Qty.HasValue || ln.Qty <= 0) err.Add(p + "quantity must be more than 0.");
                if (!ln.Rate.HasValue || ln.Rate < 0) err.Add(p + "enter the rate.");
                if (ln.Basis == "Per Kg" && (!ln.Kg.HasValue || ln.Kg <= 0)) err.Add(p + "enter the weight (Kg) when the rate is per Kg.");
                if (ln.Kg.HasValue && ln.Kg < 0) err.Add(p + "weight cannot be negative.");
                if (ln.Egg) ln.ShedId = null;                                  // eggs come from the farm's egg godown
                else if (ln.Product == "Cull Bird" && !ln.ShedId.HasValue) err.Add(p + "select the shed the cull birds left.");
                if (ln.Egg && ln.Qty.HasValue && ln.Qty != Math.Round(ln.Qty.Value)) err.Add(p + "eggs must be a whole number.");
                list.Add(ln);
            }
            return list;
        }

        protected void btnMore_Click(object sender, EventArgs e)
        {
            var err = new List<string>();
            var lines = ReadLines(err);
            LineCount = Math.Max(LineCount, lines.Count) + 3;
            BindLines(lines);
        }

        // ------------------------------------------------------------------ header

        protected void Farm_Changed(object sender, EventArgs e)
        {
            var err = new List<string>();
            var lines = ReadLines(err);
            BindLines(lines);
            ShowStock();
        }

        List<int> EditingIds
        {
            get
            {
                return (hidIds.Value ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToList();
            }
        }

        /// <summary>Eggs of the invoice being edited that are already counted in the godown stock (added back for the check).</summary>
        int OwnEggs(int farm, DateTime day)
        {
            var ids = EditingIds;
            if (ids.Count == 0) return 0;
            return Db.Int(Db.Scalar("SELECT ISNULL(SUM(Qty), 0) FROM dbo.Sale WHERE CompanyId = @c AND FarmId = @f AND SaleDate <= @d AND SaleId IN (" +
                                    string.Join(",", ids.Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray()) + ") AND Product IN (N'Table Egg', N'Cracked Egg', N'Double Yolk', N'Misshapen Egg')",
                                    Db.P("@c", Ctx.CompanyId), Db.P("@f", farm), Db.P("@d", day)));
        }

        void ShowStock()
        {
            var farm = Ui.SelInt(ddlFarm);
            if (!farm.HasValue) { litStock.Text = ""; return; }
            int stock = Rep.EggStockBefore(Ctx.CompanyId, farm.Value, SDate.AddDays(1)) + OwnEggs(farm.Value, SDate);
            litStock.Text = "<div class=\"calc-chip\"><i class=\"bi bi-box-seam me-1\"></i>Egg godown stock on " + Ui.D(SDate) + ": <b>" + Ui.N0(stock) +
                            "</b> eggs (" + Ui.N0(Rep.EggTrays(stock)) + " trays)</div>";
        }

        // ------------------------------------------------------------------ save

        protected void btnNew_Click(object sender, EventArgs e) { Reset(); }

        void Reset()
        {
            hidIds.Value = ""; txtInv.Text = ""; txtRecv.Text = "0"; txtRemarks.Text = "";
            ddlFarm.Enabled = true; txtDate.Enabled = true;
            litTitle.Text = "New sale invoice"; pnlErr.Visible = false;
            LineCount = StartLines;
            BindLines(new List<Line>());
            ShowStock();
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            var err = new List<string>();
            var d = SDate;
            var farm = Ui.SelInt(ddlFarm);
            var recv = Ui.Dec(txtRecv.Text) ?? 0m;
            if (d > DateTime.Today) err.Add("Sale date cannot be in the future.");
            if (!farm.HasValue) err.Add("Select the farm.");
            if (recv < 0) err.Add("Amount received cannot be negative.");
            var lines = ReadLines(err);
            if (lines.Count == 0 && err.Count == 0) err.Add("Enter at least one line (product, quantity and rate).");
            decimal total = lines.Sum(l => l.Amount);
            if (recv > total + 0.01m) err.Add("Amount received (" + recv.ToString("N2") + ") cannot exceed the invoice total (" + total.ToString("N2") + ").");
            if (farm.HasValue)
            {
                foreach (var ln in lines.Where(l => l.ShedId.HasValue))
                {
                    int sf = Db.Int(Db.Scalar("SELECT l.FarmId FROM dbo.Shed s JOIN dbo.Location l ON l.LocationId = s.LocationId WHERE s.ShedId = @s AND s.CompanyId = @c",
                                              Db.P("@s", ln.ShedId.Value), Db.P("@c", Ctx.CompanyId)));
                    if (sf != farm.Value) { err.Add("A shed on the invoice does not belong to the selected farm."); break; }
                }
                int eggQty = (int)Math.Round(lines.Where(l => l.Egg).Sum(l => l.Qty ?? 0m));
                if (eggQty > 0)
                {
                    int stock = Rep.EggStockBefore(Ctx.CompanyId, farm.Value, d.AddDays(1)) + OwnEggs(farm.Value, d);
                    if (eggQty > stock)
                        err.Add("This invoice sells " + eggQty.ToString("N0") + " eggs but only " + stock.ToString("N0") + " are in the egg godown on " + Ui.D(d) +
                                ". Check that the daily shed entries (egg collection) and the opening egg stock are complete.");
                }
            }
            if (err.Count > 0) { ShowErr(err); BindLines(lines); return; }

            string inv = (txtInv.Text ?? "").Trim();
            var oldIds = EditingIds;
            try
            {
                Db.Tx((c, t) =>
                {
                    if (inv.Length == 0) inv = NextInvoiceNo(c, t, d);
                    if (oldIds.Count > 0)
                    {
                        int n = Db.Exec(c, t, "DELETE FROM dbo.Sale WHERE CompanyId = @c AND Product <> N'Broiler Bird' AND SaleId IN (" +
                                              string.Join(",", oldIds.Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray()) + ")", Db.P("@c", Ctx.CompanyId));
                        if (n != oldIds.Count) throw new InvalidOperationException("The invoice was changed by someone else. Open it again.");
                    }
                    decimal left = recv;
                    var newIds = new List<string>();
                    foreach (var ln in lines)
                    {
                        // the amount received is spread over the lines in order, so each line's balance is correct
                        decimal r = Math.Min(left, ln.Amount); left -= r;
                        var id = Db.Scalar(c, t, @"INSERT INTO dbo.Sale (CompanyId, FarmId, SaleDate, CustomerId, InvoiceNo, Product, ShedId, Qty, QtyKg, Rate, RateBasis, AmountReceived, Remarks, CreatedBy)
                                                   VALUES (@c, @f, @d, @cust, @inv, @p, @s, @q, @kg, @r, @rb, @recv, @rem, @u); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                            Db.P("@c", Ctx.CompanyId), Db.P("@f", farm.Value), Db.P("@d", d), Db.P("@cust", Ui.SelInt(ddlCustomer)), Db.P("@inv", inv),
                            Db.P("@p", ln.Product), Db.P("@s", ln.ShedId), Db.P("@q", ln.Qty.Value), Db.P("@kg", ln.Kg), Db.P("@r", ln.Rate.Value), Db.P("@rb", ln.Basis),
                            Db.P("@recv", r), Db.P("@rem", Nz(txtRemarks.Text)), Db.P("@u", Ctx.UserId));
                        newIds.Add(Convert.ToString(id, CultureInfo.InvariantCulture));
                    }
                    Audit.Log(c, t, oldIds.Count > 0 ? "UPDATE" : "INSERT", "SaleInvoice", Db.Int(newIds[0]),
                              "invoice " + inv + "; lines=" + lines.Count + "; total=" + total.ToString("0.00", CultureInfo.InvariantCulture) +
                              (oldIds.Count > 0 ? "; replaced ids " + string.Join(",", oldIds.Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray()) : ""));
                    return 0;
                });
                Ok("Invoice " + inv + " saved: " + lines.Count + " line(s), total Rs " + total.ToString("N2") + ".");
                Reset(); BindList();
            }
            catch (Exception ex) { ShowErr(new List<string> { Db.FriendlyError(ex) }); BindLines(lines); }
        }

        static string NextInvoiceNo(SqlConnection c, SqlTransaction t, DateTime d)
        {
            string prefix = "SI-" + d.ToString("yyMMdd", CultureInfo.InvariantCulture) + "-";
            int n = Db.Int(Db.Scalar(c, t, "SELECT COUNT(DISTINCT InvoiceNo) FROM dbo.Sale WHERE CompanyId = @c AND InvoiceNo LIKE @p",
                                     Db.P("@c", Ctx.CompanyId), Db.P("@p", prefix + "%")));
            string inv;
            do { n++; inv = prefix + n.ToString("00", CultureInfo.InvariantCulture); }
            while (Db.Int(Db.Scalar(c, t, "SELECT COUNT(*) FROM dbo.Sale WHERE CompanyId = @c AND InvoiceNo = @i", Db.P("@c", Ctx.CompanyId), Db.P("@i", inv))) > 0);
            return inv;
        }

        // ------------------------------------------------------------------ list / edit / delete

        const string GroupKey = "s.FarmId, s.SaleDate, ISNULL(s.CustomerId, 0), ISNULL(s.InvoiceNo, N'#' + CAST(s.SaleId AS NVARCHAR(12)))";

        /// <summary>All rows of the invoice that row <paramref name="id"/> belongs to.</summary>
        DataTable InvoiceRows(int id)
        {
            return Db.Query(@"SELECT s.* FROM dbo.Sale s JOIN dbo.Sale k ON k.SaleId = @id AND k.CompanyId = @c
                              WHERE s.CompanyId = @c AND s.Product <> N'Broiler Bird' AND s.FarmId = k.FarmId AND s.SaleDate = k.SaleDate
                                AND ISNULL(s.CustomerId, 0) = ISNULL(k.CustomerId, 0)
                                AND ((k.InvoiceNo IS NULL AND s.SaleId = k.SaleId) OR s.InvoiceNo = k.InvoiceNo)
                              ORDER BY s.SaleId", Db.P("@id", id), Db.P("@c", Ctx.CompanyId));
        }

        protected void btnCmd_Click(object sender, EventArgs e)
        {
            var cmd = (hidCmd.Value ?? "").Split(':'); hidCmd.Value = "";
            int id;
            if (cmd.Length != 2 || !int.TryParse(cmd[1], out id)) return;
            var rows = InvoiceRows(id);
            if (rows.Rows.Count == 0) return;
            var h = rows.Rows[0];
            if (cmd[0] == "edit")
            {
                hidIds.Value = string.Join(",", rows.Rows.Cast<DataRow>().Select(r => Db.Str(r["SaleId"])).ToArray());
                txtDate.Text = Ui.DateValue(h["SaleDate"]); txtDate.Enabled = false;
                Ui.Select(ddlFarm, h["FarmId"]); ddlFarm.Enabled = false;
                Ui.Select(ddlCustomer, h["CustomerId"]);
                txtInv.Text = Db.Str(h["InvoiceNo"]);
                txtRecv.Text = Ui.NumValue(rows.Rows.Cast<DataRow>().Sum(r => Db.Dec(r["AmountReceived"])));
                txtRemarks.Text = Db.Str(h["Remarks"]);
                var lines = rows.Rows.Cast<DataRow>().Select(r => new Line
                {
                    Product = Db.Str(r["Product"]), ShedId = r["ShedId"] == DBNull.Value ? (int?)null : Db.Int(r["ShedId"]),
                    Qty = Db.Dec(r["Qty"]), Kg = r["QtyKg"] == DBNull.Value ? (decimal?)null : Db.Dec(r["QtyKg"]), Rate = Db.Dec(r["Rate"]),
                    Basis = r.Table.Columns.Contains("RateBasis") ? Db.Str(r["RateBasis"]) : "Per Unit"
                }).ToList();
                LineCount = Math.Max(StartLines, lines.Count + 1);
                BindLines(lines);
                litTitle.Text = "Edit invoice " + Ui.H(txtInv.Text.Length > 0 ? txtInv.Text : "#" + id);
                pnlErr.Visible = false;
                ShowStock();
                return;
            }
            if (cmd[0] == "del")
            {
                try
                {
                    var ids = rows.Rows.Cast<DataRow>().Select(r => Db.Str(r["SaleId"])).ToArray();
                    Db.Tx((c, t) =>
                    {
                        Db.Exec(c, t, "DELETE FROM dbo.Sale WHERE CompanyId = @c AND Product <> N'Broiler Bird' AND SaleId IN (" + string.Join(",", ids) + ")", Db.P("@c", Ctx.CompanyId));
                        Audit.Log(c, t, "DELETE", "SaleInvoice", id, "invoice " + Db.Str(h["InvoiceNo"]) + "; ids " + string.Join(",", ids));
                        return 0;
                    });
                    Ok("Invoice deleted.");
                }
                catch (Exception ex) { Fail(Db.FriendlyError(ex)); }
                Reset(); BindList();
            }
        }

        protected void List_Changed(object sender, EventArgs e) { BindList(); }

        void BindList()
        {
            var from = Ui.Date(txtFrom.Text) ?? DateTime.Today.AddDays(-30);
            var to = Ui.Date(txtTo.Text) ?? DateTime.Today;
            string q = (txtSearch.Text ?? "").Trim();
            var dt = Db.Query(@"SELECT s.SaleId, s.FarmId, s.SaleDate, s.CustomerId, s.InvoiceNo, s.Product, s.Qty, s.QtyKg, s.Rate, s.RateBasis, s.Amount, s.AmountReceived,
                                       c.Name AS Cust, f.Code AS FarmCode
                                FROM dbo.Sale s LEFT JOIN dbo.Customer c ON c.CustomerId = s.CustomerId JOIN dbo.Farm f ON f.FarmId = s.FarmId
                                WHERE s.CompanyId = @c AND s.Product <> N'Broiler Bird' AND s.SaleDate BETWEEN @a AND @b
                                  AND (@q = N'' OR c.Name LIKE N'%' + @q + N'%' OR s.InvoiceNo LIKE N'%' + @q + N'%')
                                ORDER BY s.SaleDate DESC, s.InvoiceNo DESC, s.SaleId",
                                Db.P("@c", Ctx.CompanyId), Db.P("@a", from), Db.P("@b", to), Db.P("@q", q));
            if (dt.Rows.Count == 0) { litList.Text = "<div class=\"empty-state\"><i class=\"bi bi-cart\"></i>No sales in this period.</div>"; return; }

            var groups = dt.Rows.Cast<DataRow>().GroupBy(r => Db.Str(r["FarmId"]) + "|" + Ui.DateValue(r["SaleDate"]) + "|" + Db.Str(r["CustomerId"]) + "|" +
                                                             (r["InvoiceNo"] == DBNull.Value ? "#" + Db.Str(r["SaleId"]) : Db.Str(r["InvoiceNo"])));
            var sb = new StringBuilder("<table class=\"table table-erp table-hover\"><thead><tr><th>Date</th><th>Invoice</th><th>Customer</th><th>Items (qty @ rate)</th>" +
                                       "<th class=\"num\">Eggs</th><th class=\"num\">Amount</th><th class=\"num\">Received</th><th class=\"num\">Balance</th><th></th></tr></thead><tbody>");
            decimal ta = 0, tr = 0, te = 0;
            foreach (var g in groups)
            {
                var h = g.First();
                decimal amt = g.Sum(r => Db.Dec(r["Amount"])), rec = g.Sum(r => Db.Dec(r["AmountReceived"]));
                decimal eggs = g.Where(r => Array.IndexOf(Rep.EggProducts, Db.Str(r["Product"])) >= 0).Sum(r => Db.Dec(r["Qty"]));
                ta += amt; tr += rec; te += eggs;
                var items = string.Join("<br/>", g.Select(r => Ui.H(r["Product"]) + " " + Ui.N0(r["Qty"]) +
                                (Db.Str(r["RateBasis"]) == "Per Kg" ? " (" + Ui.N2(r["QtyKg"]) + " kg)" : "") + " @ " + Ui.N2(r["Rate"]) +
                                (Db.Str(r["RateBasis"]) == "Per Kg" ? "/kg" : "")).ToArray());
                sb.Append("<tr><td class=\"text-nowrap\">").Append(Ui.D(h["SaleDate"])).Append("<div class=\"small text-muted-2\">").Append(Ui.H(h["FarmCode"])).Append("</div></td><td>")
                  .Append(Ui.H(h["InvoiceNo"])).Append("</td><td>").Append(h["Cust"] == DBNull.Value ? "<span class=\"text-muted-2\">cash / walk-in</span>" : Ui.H(h["Cust"]))
                  .Append("</td><td class=\"small\">").Append(items).Append("</td><td class=\"num\">").Append(eggs > 0 ? Ui.N0(eggs) : "")
                  .Append("</td><td class=\"num\">").Append(Ui.N2(amt)).Append("</td><td class=\"num\">").Append(Ui.N2(rec)).Append("</td><td class=\"num\">")
                  .Append(amt - rec > 0.004m ? "<span class=\"text-danger\">" + Ui.N2(amt - rec) + "</span>" : "0.00").Append("</td><td class=\"text-end text-nowrap\">")
                  .Append("<a href=\"#\" class=\"btn btn-outline-primary\" onclick=\"return invCmd('edit:").Append(h["SaleId"]).Append("')\"><i class=\"bi bi-pencil\"></i></a> ")
                  .Append("<a href=\"#\" class=\"btn btn-outline-danger\" onclick=\"return invCmd('del:").Append(h["SaleId"]).Append("','Delete this whole invoice (all its lines)?')\"><i class=\"bi bi-trash\"></i></a>")
                  .Append("</td></tr>");
            }
            sb.Append("<tr class=\"fw-bold table-light\"><td colspan=\"4\">Total</td><td class=\"num\">").Append(Ui.N0(te)).Append("</td><td class=\"num\">").Append(Ui.N2(ta))
              .Append("</td><td class=\"num\">").Append(Ui.N2(tr)).Append("</td><td class=\"num\">").Append(Ui.N2(ta - tr)).Append("</td><td></td></tr></tbody></table>");
            litList.Text = sb.ToString();
        }

        void ShowErr(List<string> err)
        {
            pnlErr.Visible = true;
            var sb = new StringBuilder();
            foreach (var x in err) sb.Append("<div><i class=\"bi bi-exclamation-triangle me-1\"></i>").Append(Ui.H(x)).Append("</div>");
            litErr.Text = sb.ToString();
        }

        static string Nz(string s) { s = (s ?? "").Trim(); return s.Length == 0 ? null : s; }
    }
}
