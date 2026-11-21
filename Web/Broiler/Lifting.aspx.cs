using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

namespace PoultryERP
{
    public partial class LiftingPage : BasePage
    {
        protected override string[] AllowedRoles { get { return new[] { Roles.SuperAdmin, Roles.FarmManager, Roles.Supervisor, Roles.Accountant }; } }
        bool CanModify { get { return Ctx.InRole(Roles.SuperAdmin, Roles.FarmManager, Roles.Accountant); } }
        DateTime LDate { get { return Ui.Date(txtDate.Text) ?? DateTime.Today; } }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            txtDate.Text = Ui.DateValue(DateTime.Today);
            txtDate.Attributes["max"] = Ui.DateValue(DateTime.Today);
            Ui.Bind(ddlCustomer, "SELECT CustomerId AS Value, Name AS Text FROM dbo.Customer WHERE CompanyId = @c AND IsActive = 1 ORDER BY Name", "(cash / walk-in)", Db.P("@c", Ctx.CompanyId));
            Ui.Bind(ddlBatch, @"SELECT FlockId AS Value, FlockCode + CASE WHEN Status = N'Depleted' THEN ' (closed)' ELSE ' (running)' END AS Text FROM dbo.Flock
                                WHERE CompanyId = @c AND FlockType = N'Broiler' ORDER BY HatchDate DESC", "All batches - last 60 days", Db.P("@c", Ctx.CompanyId));
            BindSheds();
            BindList();
        }

        void BindSheds()
        {
            Ui.Bind(ddlShed, @"SELECT v.ShedId AS Value, v.Code + ' - ' + f.FlockCode + ' (' + CAST(v.Birds AS VARCHAR(10)) + ' birds)' AS Text
                               FROM dbo.vShedInfo v JOIN dbo.Flock f ON f.FlockId = v.FlockId
                               WHERE v.CompanyId = @CompanyId AND v.Birds > 0 AND f.FlockType = N'Broiler'" + Ctx.ShedFilter("v.ShedId") + " ORDER BY v.Code",
                    "-- select shed --", Ctx.Scope());
            Shed_Changed(null, null);
        }

        protected void Shed_Changed(object sender, EventArgs e)
        {
            var shed = Ui.SelInt(ddlShed);
            litInfo.Text = "";
            if (!shed.HasValue) return;
            var d = LDate;
            var occ = Ops.Occupant(shed.Value, d);
            if (occ == null) { litInfo.Text = "<div class=\"small text-danger\">No birds in this shed on " + Ui.D(d) + ".</div>"; return; }
            var f = Ops.Flock(occ.FlockId);
            int age = Age.Days(Convert.ToDateTime(f["HatchDate"]), d);
            int movable = Ops.Movable(null, null, shed.Value, occ.FlockId, d, Ui.Int(hidId.Value));
            var std = Ops.Standard(Db.Int(f["BreedId"]), age);
            var lastBw = Db.Row("SELECT TOP (1) LogDate, AvgBodyWtG FROM dbo.DailyLog WHERE ShedId = @s AND FlockId = @f AND AvgBodyWtG IS NOT NULL AND LogDate <= @d ORDER BY LogDate DESC",
                Db.P("@s", shed.Value), Db.P("@f", occ.FlockId), Db.P("@d", d));
            litInfo.Text = "<div class=\"calc-chip\">Batch <b>" + Ui.H(f["FlockCode"]) + "</b> &middot; day <b>" + age + "</b> &middot; can lift <b>" + Ui.N0(movable) +
                           "</b> birds<br/>Std weight <b>" + (std != null && std.BodyWt.HasValue ? Ui.N0(std.BodyWt) + " g" : "-") + "</b> &middot; last sample <b>" +
                           (lastBw == null ? "-" : Ui.N0(lastBw["AvgBodyWtG"]) + " g</b> on " + Ui.D(lastBw["LogDate"])) + "</div>";
        }

        protected void btnNew_Click(object sender, EventArgs e) { Reset(); }

        void Reset()
        {
            hidId.Value = "";
            txtInv.Text = txtBirds.Text = txtKg.Text = txtRate.Text = txtRemarks.Text = "";
            txtRecv.Text = "0";
            ddlShed.Enabled = true; txtDate.Enabled = true;
            litTitle.Text = "New lifting"; pnlErr.Visible = false;
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            var err = new List<string>();
            var d = LDate;
            int? id = Ui.Int(hidId.Value);
            if (id.HasValue && !CanModify) { Fail("Only managers and accounts can change a saved lifting."); return; }
            var shed = Ui.SelInt(ddlShed);
            var birds = Ui.Int(txtBirds.Text); var kg = Ui.Dec(txtKg.Text); var rate = Ui.Dec(txtRate.Text); var recv = Ui.Dec(txtRecv.Text) ?? 0;
            if (d > DateTime.Today) err.Add("Date cannot be in the future.");
            if (!shed.HasValue) err.Add("Select the broiler shed.");
            if (!birds.HasValue || birds <= 0) err.Add("Birds must be a positive whole number.");
            if (!kg.HasValue || kg <= 0) err.Add("Enter the total live weight in Kg.");
            if (!rate.HasValue || rate < 0) err.Add("Enter the rate per Kg.");
            if (birds > 0 && kg > 0 && (kg / birds < 0.3m || kg / birds > 6m)) err.Add("Average weight " + (kg.Value / birds.Value).ToString("0.000") + " kg/bird looks wrong - check birds and Kg.");
            if (kg.HasValue && rate.HasValue && recv > kg.Value * rate.Value + 0.01m) err.Add("Amount received cannot exceed the invoice amount.");
            string reason;
            if (shed.HasValue && !Ops.CanEditDay(shed.Value, d, out reason) && !CanModify) err.Add(reason);
            if (shed.HasValue && !Ctx.CanAccessShed(shed.Value)) err.Add("This shed is not assigned to you.");
            if (err.Count > 0) { ShowErr(err); return; }

            try
            {
                string closed = null;
                Db.Tx((c, t) =>
                {
                    var occ = Ops.Occupant(c, t, shed.Value, d);
                    if (occ == null) throw new InvalidOperationException("No birds in this shed on " + Ui.D(d) + ".");
                    var f = Db.Query(c, t, "SELECT FlockId, FlockCode, FlockType FROM dbo.Flock WHERE FlockId = @f", Db.P("@f", occ.FlockId)).Rows[0];
                    if (Db.Str(f["FlockType"]) != "Broiler") throw new InvalidOperationException("This shed holds a layer flock. Layer bird sales go through Daily Entry and Sales.");
                    int movable = Ops.Movable(c, t, shed.Value, occ.FlockId, d, id);
                    if (birds.Value > movable) throw new InvalidOperationException("Only " + movable.ToString("N0") + " birds can be lifted from this shed on " + Ui.D(d) + ".");
                    int farm = Db.Int(Db.Scalar(c, t, "SELECT l.FarmId FROM dbo.Shed s JOIN dbo.Location l ON l.LocationId = s.LocationId WHERE s.ShedId = @s", Db.P("@s", shed.Value)));
                    var ps = new List<System.Data.SqlClient.SqlParameter>
                    {
                        Db.P("@c", Ctx.CompanyId), Db.P("@farm", farm), Db.P("@d", d), Db.P("@cust", Ui.SelInt(ddlCustomer)), Db.P("@inv", Nz(txtInv.Text)),
                        Db.P("@s", shed.Value), Db.P("@f", occ.FlockId), Db.P("@q", (decimal)birds.Value), Db.P("@kg", kg.Value), Db.P("@r", rate.Value),
                        Db.P("@recv", recv), Db.P("@rem", Nz(txtRemarks.Text)), Db.P("@u", Ctx.UserId)
                    };
                    if (id.HasValue)
                    {
                        ps.Add(Db.P("@id", id.Value));
                        Db.Exec(c, t, @"UPDATE dbo.Sale SET CustomerId = @cust, InvoiceNo = @inv, Qty = @q, QtyKg = @kg, Rate = @r, AmountReceived = @recv, Remarks = @rem
                                        WHERE SaleId = @id AND CompanyId = @c AND Product = N'Broiler Bird'", ps.ToArray());
                        Audit.Log(c, t, "UPDATE", "BroilerSale", id.Value, "birds=" + birds + "; kg=" + kg + "; rate=" + rate);
                    }
                    else
                    {
                        var nid = Db.Scalar(c, t, @"INSERT INTO dbo.Sale (CompanyId, FarmId, SaleDate, CustomerId, InvoiceNo, Product, ShedId, FlockId, Qty, QtyKg, Rate, RateBasis, AmountReceived, Remarks, CreatedBy)
                                                    VALUES (@c, @farm, @d, @cust, @inv, N'Broiler Bird', @s, @f, @q, @kg, @r, N'Per Kg', @recv, @rem, @u); SELECT CAST(SCOPE_IDENTITY() AS INT);", ps.ToArray());
                        Audit.Log(c, t, "INSERT", "BroilerSale", nid, "batch " + Db.Str(f["FlockCode"]) + "; birds=" + birds + "; kg=" + kg + "; rate=" + rate);
                    }
                    Ops.EnsureNoNegative(c, t, shed.Value, occ.FlockId);
                    closed = CloseIfEmpty(c, t, occ.FlockId, shed.Value, d);
                    return 0;
                });
                Ok("Lifting saved: " + birds.Value.ToString("N0") + " birds, " + kg.Value.ToString("N2") + " kg." + (closed != null ? " Batch " + closed + " is now fully lifted and closed." : ""));
                Reset(); BindSheds(); BindList();
            }
            catch (Exception ex) { ShowErr(new List<string> { Db.FriendlyError(ex) }); }
        }

        /// <summary>Closes the batch (and frees the shed) when no birds remain; reopens it if birds came back after an edit.</summary>
        static string CloseIfEmpty(System.Data.SqlClient.SqlConnection c, System.Data.SqlClient.SqlTransaction t, int flockId, int shedId, DateTime d)
        {
            int left = Db.Int(Db.Scalar(c, t, "SELECT ISNULL(SUM(Qty),0) FROM dbo.vBirdMovement WHERE FlockId = @f", Db.P("@f", flockId)));
            if (left == 0)
            {
                var last = Db.NDate(Db.Scalar(c, t, "SELECT MAX(MoveDate) FROM dbo.vBirdMovement WHERE FlockId = @f", Db.P("@f", flockId))) ?? d;
                Db.Exec(c, t, "UPDATE dbo.Flock SET Status = N'Depleted', DepletionDate = @d WHERE FlockId = @f", Db.P("@d", last), Db.P("@f", flockId));
                Ops.MarkShedVacantIfEmpty(c, t, shedId);
                Audit.Log(c, t, "CLOSE", "Flock", flockId, "all birds lifted");
                return Db.Str(Db.Scalar(c, t, "SELECT FlockCode FROM dbo.Flock WHERE FlockId = @f", Db.P("@f", flockId)));
            }
            Db.Exec(c, t, "UPDATE dbo.Flock SET Status = N'Active', DepletionDate = NULL WHERE FlockId = @f AND Status = N'Depleted'", Db.P("@f", flockId));
            Ops.MarkShedOccupied(c, t, shedId);
            return null;
        }

        protected void Batch_Changed(object sender, EventArgs e) { BindList(); }

        protected void btnCmd_Click(object sender, EventArgs e)
        {
            var cmd = (hidCmd.Value ?? "").Split(':'); hidCmd.Value = "";
            int id;
            if (cmd.Length != 2 || !int.TryParse(cmd[1], out id) || !CanModify) return;
            var r = Db.Row("SELECT * FROM dbo.Sale WHERE SaleId = @id AND CompanyId = @c AND Product = N'Broiler Bird'", Db.P("@id", id), Db.P("@c", Ctx.CompanyId));
            if (r == null) return;
            if (cmd[0] == "edit")
            {
                hidId.Value = id.ToString(CultureInfo.InvariantCulture);
                txtDate.Text = Ui.DateValue(r["SaleDate"]); txtDate.Enabled = false;
                BindSheds();
                if (ddlShed.Items.FindByValue(Db.Str(r["ShedId"])) == null)
                    ddlShed.Items.Add(new System.Web.UI.WebControls.ListItem(Ops.ShedCode(null, null, Db.Int(r["ShedId"])) + " (current entry)", Db.Str(r["ShedId"])));
                Ui.Select(ddlShed, r["ShedId"]); ddlShed.Enabled = false;
                Ui.Select(ddlCustomer, r["CustomerId"]); txtInv.Text = Db.Str(r["InvoiceNo"]);
                txtBirds.Text = Convert.ToInt32(r["Qty"]).ToString(CultureInfo.InvariantCulture); txtKg.Text = Ui.NumValue(r["QtyKg"]);
                txtRate.Text = Ui.NumValue(r["Rate"]); txtRecv.Text = Ui.NumValue(r["AmountReceived"]); txtRemarks.Text = Db.Str(r["Remarks"]);
                litTitle.Text = "Edit lifting #" + id; pnlErr.Visible = false;
                Shed_Changed(null, null);
                return;
            }
            if (cmd[0] == "del")
            {
                try
                {
                    Db.Tx((c, t) =>
                    {
                        Db.Exec(c, t, "DELETE FROM dbo.Sale WHERE SaleId = @id", Db.P("@id", id));
                        Ops.EnsureNoNegative(c, t, Db.Int(r["ShedId"]), Db.Int(r["FlockId"]));
                        CloseIfEmpty(c, t, Db.Int(r["FlockId"]), Db.Int(r["ShedId"]), Convert.ToDateTime(r["SaleDate"]));
                        Audit.Log(c, t, "DELETE", "BroilerSale", id, null);
                        return 0;
                    });
                    Ok("Lifting deleted; birds returned to the shed.");
                }
                catch (Exception ex) { Fail(Db.FriendlyError(ex)); }
                BindSheds(); BindList();
            }
        }

        void BindList()
        {
            var batch = Ui.SelInt(ddlBatch);
            var dt = Db.Query(@"SELECT s.*, f.FlockCode, f.HatchDate, sh.Code AS ShedCode, c.Name AS Cust FROM dbo.Sale s
                                JOIN dbo.Flock f ON f.FlockId = s.FlockId JOIN dbo.Shed sh ON sh.ShedId = s.ShedId LEFT JOIN dbo.Customer c ON c.CustomerId = s.CustomerId
                                WHERE s.CompanyId = @CompanyId AND s.Product = N'Broiler Bird' AND (@b IS NULL OR s.FlockId = @b)
                                AND (@b IS NOT NULL OR s.SaleDate >= DATEADD(DAY, -60, CAST(GETDATE() AS DATE)))" + Ctx.ShedFilter("s.ShedId") + " ORDER BY s.SaleDate DESC, s.SaleId DESC",
                                Ctx.Scope(Db.P("@b", batch)));
            if (dt.Rows.Count == 0) { litList.Text = "<div class=\"empty-state\"><i class=\"bi bi-truck\"></i>No liftings yet.</div>"; return; }
            var sb = new StringBuilder("<table class=\"table table-erp table-hover\"><thead><tr><th>Date</th><th>Batch / shed</th><th class=\"num\">Age</th><th>Customer</th>" +
                                       "<th class=\"num\">Birds</th><th class=\"num\">Kg</th><th class=\"num\">Avg kg</th><th class=\"num\">Rate/kg</th><th class=\"num\">Amount</th><th class=\"num\">Balance</th><th></th></tr></thead><tbody>");
            decimal tb = 0, tk = 0, ta = 0, tr = 0;
            foreach (DataRow r in dt.Rows)
            {
                decimal birds = Db.Dec(r["Qty"]), kg = Db.Dec(r["QtyKg"]), amt = Db.Dec(r["Amount"]), rec = Db.Dec(r["AmountReceived"]);
                var d = Convert.ToDateTime(r["SaleDate"]);
                tb += birds; tk += kg; ta += amt; tr += rec;
                sb.Append("<tr><td class=\"text-nowrap\">").Append(Ui.D(d)).Append("<div class=\"small text-muted-2\">").Append(Ui.H(r["InvoiceNo"])).Append("</div></td><td><b>").Append(Ui.H(r["FlockCode"]))
                  .Append("</b><div class=\"small text-muted-2\">").Append(Ui.H(r["ShedCode"])).Append("</div>").Append("</td><td class=\"num\">").Append(Age.Days(Convert.ToDateTime(r["HatchDate"]), d))
                  .Append("</td><td class=\"small\">").Append(Ui.H(r["Cust"])).Append("</td><td class=\"num\">").Append(Ui.N0(birds)).Append("</td><td class=\"num\">").Append(Ui.N2(kg))
                  .Append("</td><td class=\"num\">").Append(birds > 0 ? (kg / birds).ToString("0.000") : "-").Append("</td><td class=\"num\">").Append(Ui.N2(r["Rate"]))
                  .Append("</td><td class=\"num\">").Append(Ui.N2(amt)).Append("</td><td class=\"num\">").Append(amt - rec > 0 ? "<span class=\"text-danger\">" + Ui.N2(amt - rec) + "</span>" : "0.00")
                  .Append("</td><td class=\"text-end text-nowrap\">");
                if (CanModify)
                    sb.Append("<a href=\"#\" class=\"btn btn-outline-primary\" onclick=\"return liftCmd('edit:").Append(r["SaleId"]).Append("')\"><i class=\"bi bi-pencil\"></i></a> ")
                      .Append("<a href=\"#\" class=\"btn btn-outline-danger\" onclick=\"return liftCmd('del:").Append(r["SaleId"]).Append("','Delete this lifting? The birds will be added back to the shed.')\"><i class=\"bi bi-trash\"></i></a>");
                sb.Append("</td></tr>");
            }
            sb.Append("<tr class=\"fw-bold table-light\"><td colspan=\"4\">Total</td><td class=\"num\">").Append(Ui.N0(tb)).Append("</td><td class=\"num\">").Append(Ui.N2(tk))
              .Append("</td><td class=\"num\">").Append(tb > 0 ? (tk / tb).ToString("0.000") : "-").Append("</td><td class=\"num\">").Append(tk > 0 ? Ui.N2(ta / tk) : "-")
              .Append("</td><td class=\"num\">").Append(Ui.N2(ta)).Append("</td><td class=\"num\">").Append(Ui.N2(ta - tr)).Append("</td><td></td></tr>");
            sb.Append("</tbody></table>");
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
