using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;

namespace PoultryERP.Reports
{
    public enum Align { Left, Right, Center }
    public enum RowKind { Normal, Group, Subtotal, Total }

    public class ReportColumn
    {
        public string Header;
        public Align Align;
        /// <summary>T = text, N0/N1/N2/N3 = number with decimals, D = date, P1 = percent 1dp</summary>
        public string Format;
        public ReportColumn(string header, string format)
        {
            Header = header;
            Format = format ?? "T";
            Align = Format == "T" ? Align.Left : (Format == "D" ? Align.Center : Align.Right);
        }
        public ReportColumn(string header, string format, Align align) : this(header, format) { Align = align; }

        public int Decimals
        {
            get
            {
                if (Format.StartsWith("N") || Format.StartsWith("P"))
                {
                    int d;
                    if (int.TryParse(Format.Substring(1), out d)) return d;
                }
                return 0;
            }
        }
        public bool IsNumeric { get { return Format.StartsWith("N") || Format.StartsWith("P"); } }
    }

    public class ReportRow
    {
        public object[] Cells;
        public RowKind Kind;
        public ReportRow(RowKind kind, object[] cells) { Kind = kind; Cells = cells; }
    }

    public class ReportSection
    {
        public string Title;
        public List<ReportColumn> Columns = new List<ReportColumn>();
        public List<ReportRow> Rows = new List<ReportRow>();
        public string EmptyText = "No data for the selected filters.";

        public ReportSection(string title) { Title = title; }

        public ReportSection Col(string header, string format) { Columns.Add(new ReportColumn(header, format)); return this; }
        public ReportSection Col(string header, string format, Align a) { Columns.Add(new ReportColumn(header, format, a)); return this; }

        public void Add(params object[] cells) { Rows.Add(new ReportRow(RowKind.Normal, cells)); }
        public void AddTotal(params object[] cells) { Rows.Add(new ReportRow(RowKind.Total, cells)); }
        public void AddSubtotal(params object[] cells) { Rows.Add(new ReportRow(RowKind.Subtotal, cells)); }
        public void AddGroup(string label)
        {
            var cells = new object[Columns.Count];
            cells[0] = label;
            Rows.Add(new ReportRow(RowKind.Group, cells));
        }
        public bool HasData
        {
            get { foreach (var r in Rows) if (r.Kind == RowKind.Normal) return true; return false; }
        }
    }

    public class SummaryBlock
    {
        public string Title;
        public List<KeyValuePair<string, string>> Items = new List<KeyValuePair<string, string>>();
        /// <summary>Optional small table shown inside the box instead of key/value lines (e.g. Birds by Stage).</summary>
        public ReportSection Table;
        /// <summary>Relative width when boxes are laid out in one strip (one-page reports).</summary>
        public float Weight = 1f;
        /// <summary>Excel columns the box spans in a one-page layout (key columns = span - 1).</summary>
        public int XlsxSpan = 2;
        public SummaryBlock(string title) { Title = title; }
        public SummaryBlock Add(string k, string v) { Items.Add(new KeyValuePair<string, string>(k, v)); return this; }
    }

    public class ChartSeries
    {
        public string Name;
        public List<decimal?> Values = new List<decimal?>();
        public string Type = "line";
        public string Axis = "y";
        public ChartSeries(string name) { Name = name; }
    }

    public class ChartSpec
    {
        public string Title;
        public List<string> Labels = new List<string>();
        public List<ChartSeries> Series = new List<ChartSeries>();
        public string YTitle, Y2Title;
    }

    public class ReportModel
    {
        public string Key;
        public string CompanyName;
        public string Title;
        public string Subtitle;
        public bool Landscape = true;
        /// <summary>Fit the whole report on one A4 page: smaller rows/fonts, summary boxes side by side in one strip.</summary>
        public bool OnePage;
        public List<ReportSection> Sections = new List<ReportSection>();
        public List<SummaryBlock> Summaries = new List<SummaryBlock>();
        public List<string> Notes = new List<string>();
        public ChartSpec Chart;
        public DateTime GeneratedOn = DateTime.Now;
        public string GeneratedBy;

        public ReportSection Section(string title)
        {
            var s = new ReportSection(title);
            Sections.Add(s);
            return s;
        }

        public SummaryBlock Summary(string title)
        {
            var b = new SummaryBlock(title);
            Summaries.Add(b);
            return b;
        }

        /// <summary>A summary box holding a small table (shown beside the other boxes).</summary>
        public ReportSection SummaryTable(string title, float weight, int xlsxSpan)
        {
            var b = new SummaryBlock(title) { Weight = weight, XlsxSpan = xlsxSpan, Table = new ReportSection(title) };
            Summaries.Add(b);
            return b.Table;
        }
    }

    /// <summary>Report filters. Read from a query string so screen and export share one code path.</summary>
    public class ReportParams
    {
        public DateTime Date = DateTime.Today.AddDays(-1);
        public DateTime From = DateTime.Today.AddDays(-30);
        public DateTime To = DateTime.Today.AddDays(-1);
        public int? FarmId, ShedId, FlockId;
        public string GroupBy = "Weekly";
        public string Product = "";
        public bool WithWeights = true;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static ReportParams Parse(NameValueCollection q)
        {
            var p = new ReportParams();
            DateTime d;
            if (DateTime.TryParseExact(q["date"] ?? "", "yyyy-MM-dd", Inv, DateTimeStyles.None, out d)) p.Date = d;
            if (DateTime.TryParseExact(q["from"] ?? "", "yyyy-MM-dd", Inv, DateTimeStyles.None, out d)) p.From = d;
            if (DateTime.TryParseExact(q["to"] ?? "", "yyyy-MM-dd", Inv, DateTimeStyles.None, out d)) p.To = d;
            p.FarmId = I(q["farm"]);
            p.ShedId = I(q["shed"]);
            p.FlockId = I(q["flock"]);
            if (!string.IsNullOrEmpty(q["group"])) p.GroupBy = q["group"];
            p.Product = q["product"] ?? "";
            p.WithWeights = q["wt"] != "0";
            if (p.From > p.To) { var t = p.From; p.From = p.To; p.To = t; }
            if ((p.To - p.From).TotalDays > 800) p.From = p.To.AddDays(-800);
            return p;
        }

        static int? I(string s) { int v; return int.TryParse(s, out v) && v > 0 ? v : (int?)null; }

        public string ToQuery()
        {
            var parts = new List<string>();
            parts.Add("date=" + Date.ToString("yyyy-MM-dd", Inv));
            parts.Add("from=" + From.ToString("yyyy-MM-dd", Inv));
            parts.Add("to=" + To.ToString("yyyy-MM-dd", Inv));
            if (FarmId.HasValue) parts.Add("farm=" + FarmId.Value);
            if (ShedId.HasValue) parts.Add("shed=" + ShedId.Value);
            if (FlockId.HasValue) parts.Add("flock=" + FlockId.Value);
            parts.Add("group=" + Uri.EscapeDataString(GroupBy ?? ""));
            if (!string.IsNullOrEmpty(Product)) parts.Add("product=" + Uri.EscapeDataString(Product));
            parts.Add("wt=" + (WithWeights ? "1" : "0"));
            return string.Join("&", parts.ToArray());
        }
    }

    public static class Fmt
    {
        static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Formats a cell for display (HTML/PDF/CSV). Excel receives raw numbers instead.</summary>
        public static string Cell(object v, ReportColumn c)
        {
            if (v == null || v == DBNull.Value) return "";
            if (v is string) return (string)v;
            if (v is DateTime) return ((DateTime)v).ToString("dd-MM-yyyy", Inv);
            if (c != null && c.IsNumeric && IsNumber(v))
            {
                var d = Convert.ToDecimal(v);
                var s = d.ToString("#,##0" + (c.Decimals > 0 ? "." + new string('0', c.Decimals) : ""), India);
                return c.Format.StartsWith("P") ? s : s;
            }
            return Convert.ToString(v, India);
        }

        public static bool IsNumber(object v)
        {
            return v is int || v is long || v is decimal || v is double || v is float || v is short;
        }

        public static string N(decimal v, int dec)
        {
            return v.ToString("#,##0" + (dec > 0 ? "." + new string('0', dec) : ""), India);
        }

        public static string Money(decimal v) { return "Rs " + v.ToString("#,##0.00", India); }

        public static decimal? Div(decimal a, decimal b) { return b == 0 ? (decimal?)null : a / b; }
        public static decimal? Pct(decimal a, decimal b) { return b == 0 ? (decimal?)null : a * 100m / b; }
    }
}
