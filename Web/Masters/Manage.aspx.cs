using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace PoultryERP
{
    /// <summary>Generic AJAX list + form screen driven by a CrudDef (see App_Code/Crud.cs).</summary>
    public partial class ManagePage : BasePage
    {
        CrudDef def;
        readonly Dictionary<string, WebControl> inputs = new Dictionary<string, WebControl>();

        CrudDef Def
        {
            get
            {
                if (def == null) def = CrudRegistry.Get(Request.QueryString["e"]);
                return def;
            }
        }

        protected override string[] AllowedRoles { get { return Def == null ? null : Def.ViewRoles; } }
        bool CanEdit { get { return Def != null && Ctx.InRole(Def.EditRoles); } }

        int CurrentPage
        {
            get { return ViewState["pg"] is int ? (int)ViewState["pg"] : 1; }
            set { ViewState["pg"] = value; }
        }

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            if (Def == null)
            {
                Response.Redirect("~/Default.aspx", true);
                return;
            }
            BuildForm();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Def == null) return;
            Title = Def.Title;
            litDesc.Text = Ui.H(Def.Description);
            litListTitle.Text = "<i class=\"bi bi-" + Def.Icon + " me-1\"></i>" + Ui.H(Def.Title);
            pnlSearch.Visible = Def.SearchCols.Length > 0;
            if (!IsPostBack)
            {
                ResetForm();
                BindList();
            }
        }

        // ------------------------------------------------------------------ form building

        void BuildForm()
        {
            foreach (var f in Def.Fields)
            {
                var col = new Panel { CssClass = f.Col };
                string id = "f_" + f.Name;
                if (f.Type == FieldType.Bool)
                {
                    var cb = new CheckBox { ID = id, Text = f.Label };
                    cb.InputAttributes["class"] = "form-check-input";
                    cb.InputAttributes["role"] = "switch";
                    cb.LabelAttributes["class"] = "form-check-label";
                    var wrap = new Panel { CssClass = "form-check form-switch mt-lg-4 pt-lg-2" };
                    wrap.Controls.Add(cb);
                    col.Controls.Add(wrap);
                    inputs[f.Name] = cb;
                }
                else
                {
                    WebControl ctl;
                    if (f.Type == FieldType.Select)
                    {
                        var ddl = new DropDownList { ID = id, CssClass = "form-select" };
                        ddl.EnableViewState = false;
                        if (!f.Required || f.EmptyText != null) ddl.Items.Add(new ListItem(f.EmptyText ?? "", ""));
                        else ddl.Items.Add(new ListItem("-- select --", ""));
                        if (f.Options != null) foreach (var o in f.Options) ddl.Items.Add(new ListItem(o, o));
                        if (f.LookupSql != null)
                            foreach (DataRow r in Db.Query(f.LookupSql, Ctx.Scope()).Rows)
                                ddl.Items.Add(new ListItem(Convert.ToString(r["Text"]), Convert.ToString(r["Value"])));
                        ctl = ddl;
                    }
                    else
                    {
                        var tb = new TextBox { ID = id, CssClass = "form-control" };
                        switch (f.Type)
                        {
                            case FieldType.TextArea: tb.TextMode = TextBoxMode.MultiLine; tb.Rows = 2; tb.MaxLength = f.MaxLength; break;
                            case FieldType.Date: tb.TextMode = TextBoxMode.Date; break;
                            case FieldType.Time: tb.TextMode = TextBoxMode.Time; break;
                            case FieldType.Int: tb.Attributes["inputmode"] = "numeric"; tb.CssClass += " num text-end"; break;
                            case FieldType.Decimal: tb.Attributes["inputmode"] = "decimal"; tb.CssClass += " num text-end"; break;
                            default: tb.MaxLength = f.MaxLength; break;
                        }
                        ctl = tb;
                    }
                    col.Controls.Add(new LiteralControl("<label class=\"form-label" + (f.Required ? " req" : "") + "\" for=\"" + "{0}" + "\">" + Ui.H(f.Label) + "</label>"));
                    col.Controls.Add(ctl);
                    if (!string.IsNullOrEmpty(f.Help)) col.Controls.Add(new LiteralControl("<div class=\"form-text\">" + Ui.H(f.Help) + "</div>"));
                    inputs[f.Name] = ctl;
                }
                phFields.Controls.Add(col);
            }
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            // fix label "for" attributes now that client IDs are known
            foreach (Control col in phFields.Controls)
                foreach (Control c in col.Controls)
                {
                    var lit = c as LiteralControl;
                    if (lit == null || !lit.Text.Contains("for=\"{0}\"")) continue;
                    var idx = col.Controls.IndexOf(c);
                    if (idx + 1 < col.Controls.Count) lit.Text = lit.Text.Replace("{0}", col.Controls[idx + 1].ClientID);
                }
        }

        bool Editing { get { return !string.IsNullOrEmpty(hidId.Value); } }

        void ResetForm()
        {
            hidId.Value = "";
            pnlErr.Visible = false;
            foreach (var f in Def.Fields) SetValue(f, f.Default == null ? null : f.Default());
            foreach (var f in Def.Fields) inputs[f.Name].Enabled = true;
            litFormTitle.Text = "New " + Ui.H(Singular(Def.Title));
            btnNew.Visible = false;
            pnlForm.Visible = CanEdit && Def.AllowInsert;
        }

        static string Singular(string t)
        {
            var s = t.Split('(')[0].Split('/')[0].Trim();
            if (s.EndsWith("ies")) return s.Substring(0, s.Length - 3) + "y";
            if (s.EndsWith("s") && !s.EndsWith("ss")) return s.Substring(0, s.Length - 1);
            return s;
        }

        void LoadRecord(int id)
        {
            var row = Db.Row("SELECT * FROM dbo.[" + Def.Table + "] WHERE [" + Def.Pk + "] = @id" + (Def.CompanyScoped ? " AND CompanyId = @CompanyId" : ""),
                Ctx.Scope(Db.P("@id", id)));
            if (row == null) { Fail("Record not found."); return; }
            hidId.Value = id.ToString(CultureInfo.InvariantCulture);
            foreach (var f in Def.Fields)
            {
                SetValue(f, row.Table.Columns.Contains(f.Name) ? row[f.Name] : null);
                inputs[f.Name].Enabled = !f.InsertOnly;
            }
            pnlErr.Visible = false;
            litFormTitle.Text = "Edit " + Ui.H(Singular(Def.Title)) + " <span class=\"badge text-bg-light border ms-1\">#" + id + "</span>";
            btnNew.Visible = Def.AllowInsert;
            pnlForm.Visible = true;
            Ui.Script(this, "document.querySelector('.app-content').scrollIntoView({behavior:'smooth'});");
        }

        void SetValue(CrudField f, object v)
        {
            var c = inputs[f.Name];
            if (f.Type == FieldType.Bool) { ((CheckBox)c).Checked = v != null && v != DBNull.Value && Convert.ToBoolean(v); return; }
            if (f.Type == FieldType.Select) { Ui.Select((DropDownList)c, v); return; }
            var tb = (TextBox)c;
            switch (f.Type)
            {
                case FieldType.Date: tb.Text = Ui.DateValue(v); break;
                case FieldType.Time: tb.Text = Ui.TimeValue(v); break;
                case FieldType.Int:
                case FieldType.Decimal: tb.Text = Ui.NumValue(v); break;
                default: tb.Text = v == null || v == DBNull.Value ? "" : Convert.ToString(v); break;
            }
        }

        Dictionary<string, object> ReadValues(List<string> errors)
        {
            var vals = new Dictionary<string, object>();
            foreach (var f in Def.Fields)
            {
                var c = inputs[f.Name];
                object v = null;
                string raw = null;
                switch (f.Type)
                {
                    case FieldType.Bool: v = ((CheckBox)c).Checked; break;
                    case FieldType.Select:
                        raw = ((DropDownList)c).SelectedValue;
                        if (!string.IsNullOrEmpty(raw)) { int iv; v = f.LookupIsText || !int.TryParse(raw, out iv) ? (object)raw : iv; }
                        break;
                    default:
                        raw = (((TextBox)c).Text ?? "").Trim();
                        if (raw.Length == 0) break;
                        switch (f.Type)
                        {
                            case FieldType.Int: { var x = Ui.Int(raw); if (!x.HasValue) errors.Add(f.Label + " must be a whole number."); v = x; break; }
                            case FieldType.Decimal: { var x = Ui.Dec(raw); if (!x.HasValue) errors.Add(f.Label + " must be a number."); v = x; break; }
                            case FieldType.Date: { var x = Ui.Date(raw); if (!x.HasValue) errors.Add(f.Label + " is not a valid date."); v = x; break; }
                            case FieldType.Time: { var x = Ui.Time(raw); if (!x.HasValue) errors.Add(f.Label + " is not a valid time (HH:mm)."); v = x; break; }
                            default:
                                if (raw.Length > f.MaxLength) errors.Add(f.Label + " is longer than " + f.MaxLength + " characters.");
                                v = raw; break;
                        }
                        break;
                }
                if (f.Required && (v == null || (v is string && ((string)v).Length == 0)) && f.Type != FieldType.Bool) errors.Add(f.Label + " is required.");
                if (v != null && (f.Min.HasValue || f.Max.HasValue) && (f.Type == FieldType.Int || f.Type == FieldType.Decimal))
                {
                    var d = Convert.ToDecimal(v);
                    if (f.Min.HasValue && d < f.Min.Value) errors.Add(f.Label + " must be at least " + f.Min.Value.ToString("0.###") + ".");
                    if (f.Max.HasValue && d > f.Max.Value) errors.Add(f.Label + " must not exceed " + f.Max.Value.ToString("0.###") + ".");
                }
                vals[f.Name] = v;
            }
            return vals;
        }

        // ------------------------------------------------------------------ events

        protected void btnNew_Click(object sender, EventArgs e) { ResetForm(); }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (!CanEdit) { Fail("You can view but not change this list."); return; }
            if (!Editing && !Def.AllowInsert) { Fail("New records cannot be added here."); return; }
            var errors = new List<string>();
            var vals = ReadValues(errors);
            int? id = Editing ? int.Parse(hidId.Value, CultureInfo.InvariantCulture) : (int?)null;
            if (errors.Count == 0 && Def.Validate != null)
            {
                var msg = Def.Validate(vals, id);
                if (msg != null) errors.Add(msg);
            }
            if (errors.Count > 0) { ShowErrors(errors); return; }

            var fields = Def.Fields.Where(f => !(id.HasValue && f.InsertOnly)).ToList();
            try
            {
                int newId = Db.Tx((c, t) =>
                {
                    var ps = new List<SqlParameter>();
                    int i = 0;
                    foreach (var f in fields) ps.Add(Db.P("@p" + (i++), vals[f.Name]));
                    ps.AddRange(Ctx.Scope());
                    if (id.HasValue)
                    {
                        var set = string.Join(", ", fields.Select((f, k) => "[" + f.Name + "] = @p" + k).ToArray());
                        ps.Add(Db.P("@id", id.Value));
                        int n = Db.Exec(c, t, "UPDATE dbo.[" + Def.Table + "] SET " + set + " WHERE [" + Def.Pk + "] = @id" + (Def.CompanyScoped ? " AND CompanyId = @CompanyId" : ""), ps.ToArray());
                        if (n == 0) throw new InvalidOperationException("The record no longer exists.");
                        Audit.Log(c, t, "UPDATE", Def.Table, id.Value, Audit.Describe(vals));
                        return id.Value;
                    }
                    var cols = fields.Select(f => "[" + f.Name + "]").ToList();
                    var pars = fields.Select((f, k) => "@p" + k).ToList();
                    if (Def.CompanyScoped) { cols.Add("CompanyId"); pars.Add("@CompanyId"); }
                    if (Def.SetCreatedBy) { cols.Add("CreatedBy"); pars.Add("@UserId"); }
                    var nid = Db.Int(Db.Scalar(c, t, "INSERT INTO dbo.[" + Def.Table + "] (" + string.Join(", ", cols.ToArray()) + ") VALUES (" +
                                                   string.Join(", ", pars.ToArray()) + "); SELECT CAST(SCOPE_IDENTITY() AS INT);", ps.ToArray()));
                    Audit.Log(c, t, "INSERT", Def.Table, nid, Audit.Describe(vals));
                    if (Def.AfterInsert != null) Def.AfterInsert(c, t, nid);
                    return nid;
                });
                Ok(id.HasValue ? "Changes saved." : "Record added.");
                ResetForm();
                BindList();
            }
            catch (Exception ex)
            {
                ShowErrors(new List<string> { Db.FriendlyError(ex) });
            }
        }

        protected void btnSearch_Click(object sender, EventArgs e) { CurrentPage = 1; BindList(); }

        protected void btnCmd_Click(object sender, EventArgs e)
        {
            var cmd = hidCmd.Value ?? "";
            hidCmd.Value = "";
            var parts = cmd.Split(':');
            int arg;
            if (parts.Length != 2 || !int.TryParse(parts[1], out arg)) return;
            switch (parts[0])
            {
                case "edit":
                    if (CanEdit) LoadRecord(arg);
                    break;
                case "del":
                    if (!CanEdit || !Def.AllowDelete) { Fail("Delete is not allowed here."); return; }
                    try
                    {
                        Db.Tx((c, t) =>
                        {
                            int n = Db.Exec(c, t, "DELETE FROM dbo.[" + Def.Table + "] WHERE [" + Def.Pk + "] = @id" + (Def.CompanyScoped ? " AND CompanyId = @CompanyId" : ""),
                                Ctx.Scope(Db.P("@id", arg)));
                            if (n > 0) Audit.Log(c, t, "DELETE", Def.Table, arg, null);
                            return n;
                        });
                        if (hidId.Value == arg.ToString(CultureInfo.InvariantCulture)) ResetForm();
                        Ok("Record deleted.");
                    }
                    catch (Exception ex)
                    {
                        Fail(ex is SqlException && ((SqlException)ex).Number == 547
                            ? "This record is used by other records and cannot be deleted. Mark it inactive instead."
                            : Db.FriendlyError(ex));
                    }
                    BindList();
                    break;
                case "page":
                    CurrentPage = Math.Max(1, arg);
                    BindList();
                    break;
            }
        }

        void ShowErrors(List<string> errors)
        {
            pnlErr.Visible = true;
            litErr.Text = "<i class=\"bi bi-exclamation-triangle me-1\"></i>" + string.Join("<br/>", errors.Select(x => Ui.H(x)).ToArray());
        }

        // ------------------------------------------------------------------ list

        void BindList()
        {
            var q = (txtSearch.Text ?? "").Trim();
            string where = "";
            if (q.Length > 0 && Def.SearchCols.Length > 0)
                where = " WHERE (" + string.Join(" OR ", Def.SearchCols.Select(c => "CAST(q.[" + c + "] AS NVARCHAR(400)) LIKE @q").ToArray()) + ")";
            var baseSql = "FROM (" + Def.ListSql + ") q" + where;
            var qp = Db.P("@q", "%" + q.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%");
            int total = Db.Int(Db.Scalar("SELECT COUNT(*) " + baseSql, Ctx.Scope(qp)));
            int pages = Math.Max(1, (total + Def.PageSize - 1) / Def.PageSize);
            if (CurrentPage > pages) CurrentPage = pages;
            var dt = Db.Query("SELECT * " + baseSql + " ORDER BY " + Def.OrderBy + " OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
                Ctx.Scope(Db.P("@q", qp.Value), Db.P("@skip", (CurrentPage - 1) * Def.PageSize), Db.P("@take", Def.PageSize)));
            litCount.Text = total.ToString("N0") + " record" + (total == 1 ? "" : "s");
            litList.Text = RenderTable(dt);
            litPager.Text = RenderPager(pages);
        }

        string RenderTable(DataTable dt)
        {
            if (dt.Rows.Count == 0)
                return "<div class=\"empty-state\"><i class=\"bi bi-inbox\"></i>No records" + ((txtSearch.Text ?? "").Trim().Length > 0 ? " match your search" : " yet") + ".</div>";
            var sb = new StringBuilder("<table class=\"table table-erp table-hover\"><thead><tr>");
            var cols = dt.Columns.Cast<DataColumn>().Where(c => c.ColumnName != "Id").ToList();
            foreach (var c in cols) sb.Append("<th").Append(IsNum(c) ? " class=\"num\"" : "").Append(">").Append(Ui.H(c.ColumnName)).Append("</th>");
            bool actions = CanEdit;
            if (actions) sb.Append("<th class=\"text-end\">Actions</th>");
            sb.Append("</tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<tr>");
                foreach (var c in cols) sb.Append("<td").Append(IsNum(c) ? " class=\"num\"" : "").Append(">").Append(Cell(r[c], c)).Append("</td>");
                if (actions)
                {
                    var id = Convert.ToString(r["Id"], CultureInfo.InvariantCulture);
                    sb.Append("<td class=\"text-end text-nowrap\"><a href=\"#\" class=\"btn btn-outline-primary\" onclick=\"return crudCmd('edit:").Append(id)
                      .Append("')\"><i class=\"bi bi-pencil\"></i></a>");
                    if (Def.AllowDelete)
                        sb.Append(" <a href=\"#\" class=\"btn btn-outline-danger\" onclick=\"return crudCmd('del:").Append(id)
                          .Append("','Delete this record? This cannot be undone.')\"><i class=\"bi bi-trash\"></i></a>");
                    sb.Append("</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        static bool IsNum(DataColumn c)
        {
            var t = c.DataType;
            return t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double) || t == typeof(short);
        }

        string Cell(object v, DataColumn c)
        {
            if (v == null || v == DBNull.Value) return "<span class=\"text-muted-2\">-</span>";
            string fmt;
            Def.ListFormats.TryGetValue(c.ColumnName, out fmt);
            if (v is bool) return (bool)v ? "<span class=\"badge text-bg-success\">Yes</span>" : "<span class=\"badge text-bg-secondary\">No</span>";
            if (v is DateTime)
            {
                var d = (DateTime)v;
                return Ui.H(d.TimeOfDay.TotalSeconds > 0 ? d.ToString("dd-MM-yyyy HH:mm") : d.ToString("dd-MM-yyyy"));
            }
            if (c.ColumnName == "Status") return Ui.StatusBadge(Convert.ToString(v));
            if (IsNum(c))
            {
                var dec = Convert.ToDecimal(v);
                if (fmt == null) fmt = (c.DataType == typeof(int) || c.DataType == typeof(long) || c.DataType == typeof(short)) ? "N0" : "N2";
                int places = int.Parse(fmt.Substring(1));
                return Ui.H(dec.ToString("#,##0" + (places > 0 ? "." + new string('0', places) : ""), Ui.India));
            }
            var s = Convert.ToString(v);
            if (s.Length > 90) return "<span title=\"" + Ui.H(s) + "\">" + Ui.H(s.Substring(0, 88)) + "&hellip;</span>";
            return Ui.H(s);
        }

        string RenderPager(int pages)
        {
            if (pages <= 1) return "";
            var sb = new StringBuilder("<div class=\"d-flex justify-content-center p-2 grid-pager\"><div>");
            int cur = CurrentPage;
            int from = Math.Max(1, cur - 3), to = Math.Min(pages, cur + 3);
            if (from > 1) sb.Append("<a href=\"#\" onclick=\"return crudCmd('page:1')\">1</a>").Append(from > 2 ? "&hellip;" : "");
            for (int p = from; p <= to; p++)
            {
                if (p == cur) sb.Append("<span>").Append(p).Append("</span>");
                else sb.Append("<a href=\"#\" onclick=\"return crudCmd('page:").Append(p).Append("')\">").Append(p).Append("</a>");
            }
            if (to < pages) sb.Append(to < pages - 1 ? "&hellip;" : "").Append("<a href=\"#\" onclick=\"return crudCmd('page:").Append(pages).Append("')\">").Append(pages).Append("</a>");
            sb.Append("</div></div>");
            return sb.ToString();
        }
    }
}
