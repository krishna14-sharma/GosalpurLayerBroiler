using System;
using System.Globalization;
using System.Text;
using System.Web;

namespace PoultryERP.Reports
{
    /// <summary>Renders a report as printable HTML in the same layout as the old Crystal reports.</summary>
    public static class HtmlRenderer
    {
        static string H(string s) { return HttpUtility.HtmlEncode(s ?? ""); }

        public static string Render(ReportModel m)
        {
            var sb = new StringBuilder();
            sb.Append("<div class=\"report-paper").Append(m.OnePage ? " rp-onepage" : "").Append(m.Landscape ? "" : " rp-portrait").Append("\" id=\"reportPaper\">");
            sb.Append("<div class=\"rp-head\"><div class=\"rp-company\">").Append(H(m.CompanyName)).Append("</div>");
            sb.Append("<div class=\"rp-title\">").Append(H(m.Title)).Append("</div>");
            if (!string.IsNullOrEmpty(m.Subtitle)) sb.Append("<div class=\"rp-sub\">").Append(H(m.Subtitle)).Append("</div>");
            sb.Append("</div>");

            foreach (var s in m.Sections)
            {
                if (!string.IsNullOrEmpty(s.Title)) sb.Append("<div class=\"rp-section-title\">").Append(H(s.Title)).Append("</div>");
                sb.Append("<div class=\"rp-scroll\"><table class=\"rp-table\"><thead><tr>");
                foreach (var c in s.Columns) sb.Append("<th>").Append(H(c.Header)).Append("</th>");
                sb.Append("</tr></thead><tbody>");
                if (!s.HasData)
                    sb.Append("<tr><td colspan=\"").Append(s.Columns.Count).Append("\" class=\"c\" style=\"padding:10px;color:#666\">").Append(H(s.EmptyText)).Append("</td></tr>");
                foreach (var r in s.Rows)
                {
                    if (r.Kind == RowKind.Group)
                    {
                        sb.Append("<tr class=\"rp-group\"><td colspan=\"").Append(s.Columns.Count).Append("\">").Append(H(Convert.ToString(r.Cells[0]))).Append("</td></tr>");
                        continue;
                    }
                    if (r.Kind != RowKind.Normal && !s.HasData) continue;
                    sb.Append(r.Kind == RowKind.Total ? "<tr class=\"rp-total\">" : r.Kind == RowKind.Subtotal ? "<tr class=\"rp-sub\">" : "<tr>");
                    for (int i = 0; i < s.Columns.Count; i++)
                    {
                        var c = s.Columns[i];
                        object v = i < r.Cells.Length ? r.Cells[i] : null;
                        string cls = c.Align == Align.Right ? "r" : c.Align == Align.Center ? "c" : "";
                        if (Fmt.IsNumber(v) && Convert.ToDecimal(v) < 0) cls += " neg";
                        sb.Append("<td").Append(cls.Length > 0 ? " class=\"" + cls.Trim() + "\"" : "").Append(">").Append(H(Fmt.Cell(v, c))).Append("</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table></div>");
            }

            if (m.Chart != null && m.Chart.Labels.Count > 1)
            {
                sb.Append("<div class=\"rp-section-title\">").Append(H(m.Chart.Title)).Append("</div>");
                sb.Append("<div class=\"rp-chart\"><canvas id=\"rpChart\" data-chart=\"").Append(H(ChartJson(m.Chart))).Append("\"></canvas></div>");
            }

            if (m.Summaries.Count > 0)
            {
                sb.Append("<div class=\"rp-summaries\">");
                foreach (var b in m.Summaries)
                {
                    sb.Append("<div class=\"rp-box\"").Append(m.OnePage ? " style=\"flex:" + b.Weight.ToString("0.##", CultureInfo.InvariantCulture) + " 1 0\"" : "")
                      .Append("><div class=\"rp-box-title\">").Append(H(b.Title)).Append("</div>");
                    if (b.Table != null)
                    {
                        sb.Append("<table class=\"rp-box-grid\"><tr>");
                        foreach (var c in b.Table.Columns) sb.Append("<th>").Append(H(c.Header)).Append("</th>");
                        sb.Append("</tr>");
                        foreach (var r in b.Table.Rows)
                        {
                            sb.Append(r.Kind == RowKind.Total ? "<tr class=\"t\">" : "<tr>");
                            for (int i = 0; i < b.Table.Columns.Count; i++)
                            {
                                var c = b.Table.Columns[i];
                                object v = i < r.Cells.Length ? r.Cells[i] : null;
                                sb.Append(c.Align == Align.Right ? "<td class=\"v\">" : "<td>").Append(H(Fmt.Cell(v, c))).Append("</td>");
                            }
                            sb.Append("</tr>");
                        }
                        sb.Append("</table></div>");
                        continue;
                    }
                    sb.Append("<table>");
                    foreach (var kv in b.Items)
                        sb.Append("<tr><td>").Append(H(kv.Key)).Append("</td><td class=\"v\">").Append(H(kv.Value)).Append("</td></tr>");
                    sb.Append("</table></div>");
                }
                sb.Append("</div>");
            }

            if (m.Notes.Count > 0)
            {
                sb.Append("<div class=\"rp-notes\">");
                foreach (var n in m.Notes) sb.Append("<div>&bull; ").Append(H(n)).Append("</div>");
                sb.Append("</div>");
            }
            sb.Append("<div class=\"rp-foot\"><span>Printed by ").Append(H(m.GeneratedBy)).Append(" on ")
              .Append(m.GeneratedOn.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)).Append("</span><span>Phoenix Poultry ERP</span></div>");
            sb.Append("</div>");
            return sb.ToString();
        }

        public static string ChartJson(ChartSpec c)
        {
            var sb = new StringBuilder();
            sb.Append("{\"labels\":[");
            for (int i = 0; i < c.Labels.Count; i++) { if (i > 0) sb.Append(','); sb.Append(Q(c.Labels[i])); }
            sb.Append("],\"y\":").Append(Q(c.YTitle)).Append(",\"y2\":").Append(Q(c.Y2Title)).Append(",\"series\":[");
            for (int i = 0; i < c.Series.Count; i++)
            {
                var s = c.Series[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":").Append(Q(s.Name)).Append(",\"type\":").Append(Q(s.Type)).Append(",\"axis\":").Append(Q(s.Axis)).Append(",\"data\":[");
                for (int j = 0; j < s.Values.Count; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append(s.Values[j].HasValue ? s.Values[j].Value.ToString(CultureInfo.InvariantCulture) : "null");
                }
                sb.Append("]}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static string Q(string s) { return HttpUtility.JavaScriptStringEncode(s ?? "", true); }
    }

    public static class CsvWriter
    {
        public static byte[] Write(ReportModel m)
        {
            var sb = new StringBuilder();
            Line(sb, m.CompanyName); Line(sb, m.Title); Line(sb, m.Subtitle); sb.Append("\r\n");
            foreach (var s in m.Sections)
            {
                if (!string.IsNullOrEmpty(s.Title)) Line(sb, s.Title);
                for (int i = 0; i < s.Columns.Count; i++) { if (i > 0) sb.Append(','); sb.Append(Esc(s.Columns[i].Header)); }
                sb.Append("\r\n");
                foreach (var r in s.Rows)
                {
                    for (int i = 0; i < s.Columns.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        object v = i < r.Cells.Length ? r.Cells[i] : null;
                        sb.Append(Esc(Raw(v)));
                    }
                    sb.Append("\r\n");
                }
                sb.Append("\r\n");
            }
            foreach (var b in m.Summaries)
            {
                Line(sb, b.Title);
                if (b.Table != null)
                {
                    for (int i = 0; i < b.Table.Columns.Count; i++) { if (i > 0) sb.Append(','); sb.Append(Esc(b.Table.Columns[i].Header)); }
                    sb.Append("\r\n");
                    foreach (var r in b.Table.Rows)
                    {
                        for (int i = 0; i < b.Table.Columns.Count; i++) { if (i > 0) sb.Append(','); sb.Append(Esc(Raw(i < r.Cells.Length ? r.Cells[i] : null))); }
                        sb.Append("\r\n");
                    }
                }
                foreach (var kv in b.Items) sb.Append(Esc(kv.Key)).Append(',').Append(Esc(kv.Value)).Append("\r\n");
                sb.Append("\r\n");
            }
            foreach (var n in m.Notes) Line(sb, n);
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var bom = Encoding.UTF8.GetPreamble();
            var all = new byte[bom.Length + bytes.Length];
            Buffer.BlockCopy(bom, 0, all, 0, bom.Length);
            Buffer.BlockCopy(bytes, 0, all, bom.Length, bytes.Length);
            return all;
        }

        static string Raw(object v)
        {
            if (v == null || v == DBNull.Value) return "";
            if (v is DateTime) return ((DateTime)v).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (Fmt.IsNumber(v)) return Convert.ToDecimal(v).ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        static void Line(StringBuilder sb, string s) { sb.Append(Esc(s)).Append("\r\n"); }

        static string Esc(string s)
        {
            s = s ?? "";
            // neutralise spreadsheet formula injection
            if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0 && !IsNumeric(s)) s = "'" + s;
            if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        static bool IsNumeric(string s) { decimal d; return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d); }
    }
}
