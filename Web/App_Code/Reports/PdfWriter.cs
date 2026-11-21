using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PoultryERP.Reports
{
    /// <summary>
    /// Dependency-free PDF writer (PDF 1.4, standard Helvetica fonts, WinAnsi encoding).
    /// Lays out report sections as ruled tables on A4 landscape with repeating headers,
    /// summary boxes and "Page x of y" footers - similar to the old Crystal output.
    /// </summary>
    public static class PdfWriter
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        // page geometry is per report (landscape by default, portrait for one-page reports); one report is written per thread
        [ThreadStatic] static float PageW, PageH, Margin;
        const float FooterH = 16f;
        [ThreadStatic] static float FitRowH, FitSize;   // one-page mode: row height / font chosen so the main table fits

        // Helvetica / Helvetica-Bold advance widths for chars 32..126 (per 1000 em)
        static readonly int[] WReg = {
            278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
            1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,
            333,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,556,556,333,500,278,556,500,722,500,500,500,334,260,334,584 };
        static readonly int[] WBold = {
            278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,
            975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,
            333,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,611,611,389,556,333,611,556,778,556,556,500,389,280,389,584 };

        public static float TextWidth(string s, bool bold, float size)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            var w = bold ? WBold : WReg;
            int sum = 0;
            foreach (var ch in s)
            {
                int c = ch;
                sum += (c >= 32 && c <= 126) ? w[c - 32] : 556;
            }
            return sum * size / 1000f;
        }

        class Page { public StringBuilder Ops = new StringBuilder(); }

        class LayoutCtx
        {
            public List<Page> Pages = new List<Page>();
            public Page Cur;
            public float Y;
            public ReportModel M;
            public float Bottom { get { return Margin + FooterH; } }
            public float Width { get { return PageW - 2 * Margin; } }
        }

        public static byte[] Write(ReportModel m)
        {
            if (m.Landscape) { PageW = 842f; PageH = 595f; } else { PageW = 595f; PageH = 842f; }
            Margin = m.OnePage ? 18f : 26f;
            FitRowH = 0; FitSize = 0;
            var c = new LayoutCtx { M = m };
            NewPage(c, true);

            if (m.OnePage)
            {
                // reserve room for the summary strip and notes, then size the table rows to use the rest of the page
                int noteLines = 0;
                foreach (var n in m.Notes) noteLines += Wrap("- " + n, false, 6f, c.Width).Count;
                float reserve = 14f + StripHeight(m.Summaries) + noteLines * 8.5f + 8f;
                int rows = 0; foreach (var s in m.Sections) rows += s.Rows.Count;
                float avail = c.Y - c.Bottom - reserve - m.Sections.Count * 42f;     // 42 = section title + 3-line header + gap
                if (rows > 0)
                {
                    FitRowH = Math.Max(7.2f, Math.Min(12.5f, avail / rows));
                    FitSize = Math.Max(5f, Math.Min(7.5f, (FitRowH - 1.2f) / 1.3f));
                }
            }
            foreach (var s in m.Sections) DrawSection(c, s);
            if (m.Summaries.Count > 0) { if (m.OnePage) DrawStrip(c, m.Summaries); else DrawSummaries(c, m.Summaries); }
            if (m.Notes.Count > 0)
            {
                Need(c, 14);
                c.Y -= 6;
                float ns = m.OnePage ? 6f : 7f;
                foreach (var n in m.Notes)
                    foreach (var line in Wrap("- " + n, false, ns, c.Width))
                    {
                        Need(c, ns + 3);
                        c.Y -= ns + 2.5f;
                        Text(c.Cur, Margin, c.Y, line, false, ns, 0.25f);
                    }
            }
            return Build(c);
        }

        // ------------------------------------------------------------------ layout

        static void NewPage(LayoutCtx c, bool first)
        {
            var p = new Page();
            c.Pages.Add(p);
            c.Cur = p;
            float y = PageH - Margin;
            var m = c.M;
            if (first)
            {
                y -= 13; Center(p, y, m.CompanyName, true, 13f);
                y -= 13; Center(p, y, m.Title, true, 10.5f);
                float tw = TextWidth(m.Title, true, 10.5f);
                Line(p, (PageW - tw) / 2, y - 2, (PageW + tw) / 2, y - 2, 0.7f, 0f);
                if (!string.IsNullOrEmpty(m.Subtitle)) { y -= 12; Center(p, y, m.Subtitle, false, 8f); }
                y -= 8;
            }
            else
            {
                y -= 10;
                Text(p, Margin, y, m.CompanyName + "  -  " + m.Title + " (continued)", true, 8.5f, 0f);
                y -= 6;
            }
            Line(p, Margin, y, PageW - Margin, y, 0.8f, 0f);
            c.Y = y - 4;
        }

        static void Need(LayoutCtx c, float h)
        {
            if (c.Y - h < c.Bottom) NewPage(c, false);
        }

        static void DrawSection(LayoutCtx c, ReportSection s)
        {
            int n = s.Columns.Count;
            if (n == 0) return;
            float avail = c.Width;
            float size = FitSize > 0 ? FitSize : 7.5f;
            float[] w = null;
            List<string>[] heads = null;

            // fit columns: shrink font from 7.5pt down to 5.5pt, then truncate
            for (; size >= 5.5f; size -= 0.25f)
            {
                w = NaturalWidths(s, size, out heads, avail);
                float sum = 0; foreach (var x in w) sum += x;
                if (sum <= avail) break;
            }
            if (size < 5.5f) size = 5.5f;
            float total = 0; foreach (var x in w) total += x;
            if (total > avail) { float k = avail / total; for (int i = 0; i < n; i++) w[i] *= k; }
            else if (FitRowH > 0) { float k = avail / total; for (int i = 0; i < n; i++) w[i] *= k; }   // one-page: use the full width
            else if (total < avail * 0.75f && n > 3)
            {
                // spread a little extra space so narrow tables look balanced (max 35% stretch)
                float k = Math.Min(1.35f, avail * 0.9f / total);
                for (int i = 0; i < n; i++) w[i] *= k;
            }
            float tableW = 0; foreach (var x in w) tableW += x;

            float rowH = FitRowH > 0 ? Math.Max(FitRowH, size * 1.3f + 1.2f) : size * 1.55f + 2f;
            int headLines = 1;
            foreach (var h in heads) headLines = Math.Max(headLines, h.Count);
            float headH = headLines * (size + 1.5f) + 5f;

            Need(c, 14 + headH + rowH * 2);
            if (!string.IsNullOrEmpty(s.Title))
            {
                c.Y -= 11;
                Text(c.Cur, Margin, c.Y, s.Title, true, 8.5f, 0f);
                Line(c.Cur, Margin, c.Y - 2, Margin + TextWidth(s.Title, true, 8.5f), c.Y - 2, 0.9f, 0f);
                c.Y -= 5;
            }
            bool any = s.HasData;
            if (!any)
            {
                c.Y -= 11;
                Text(c.Cur, Margin + 2, c.Y, s.EmptyText, false, 7.5f, 0.4f);
                c.Y -= 10;
                return;
            }
            DrawHeader(c, s, w, heads, size, headH);
            int idx = 0;
            for (int ri = 0; ri < s.Rows.Count; ri++)
            {
                var r = s.Rows[ri];
                // keep a closing total row together with the row above it
                bool lastPair = ri == s.Rows.Count - 2 && s.Rows[ri + 1].Kind == RowKind.Total;
                if (c.Y - rowH * (lastPair ? 2 : 1) < c.Bottom)
                {
                    NewPage(c, false);
                    DrawHeader(c, s, w, heads, size, headH);
                }
                float y0 = c.Y - rowH;
                bool bold = r.Kind != RowKind.Normal;
                if (r.Kind == RowKind.Group)
                {
                    Rect(c.Cur, Margin, y0, tableW, rowH, new[] { 0.984f, 0.953f, 0.863f }, 0.4f);
                    Text(c.Cur, Margin + 3, y0 + rowH / 2 - size * 0.35f, Convert.ToString(r.Cells[0]), true, size, 0f);
                    c.Y = y0;
                    continue;
                }
                float[] fill = r.Kind == RowKind.Total ? new[] { 0.93f, 0.945f, 0.93f } : r.Kind == RowKind.Subtotal ? new[] { 0.965f, 0.965f, 0.945f } : (idx++ % 2 == 1 ? new[] { 0.985f, 0.99f, 0.985f } : null);
                if (fill != null) Rect(c.Cur, Margin, y0, tableW, rowH, fill, -1);
                float x = Margin;
                for (int i = 0; i < n; i++)
                {
                    var col = s.Columns[i];
                    object v = i < r.Cells.Length ? r.Cells[i] : null;
                    string txt = Fmt.Cell(v, col);
                    txt = Fit(txt, bold, size, w[i] - 4);
                    float tw = TextWidth(txt, bold, size);
                    float tx = col.Align == Align.Right ? x + w[i] - 2 - tw : col.Align == Align.Center ? x + (w[i] - tw) / 2 : x + 2;
                    bool neg = Fmt.IsNumber(v) && Convert.ToDecimal(v) < 0;
                    Text(c.Cur, tx, y0 + rowH / 2 - size * 0.35f, txt, bold, size, neg ? -1f : 0f);
                    x += w[i];
                }
                // ruling: bottom line + vertical separators
                Line(c.Cur, Margin, y0, Margin + tableW, y0, r.Kind == RowKind.Total ? 0.9f : 0.3f, r.Kind == RowKind.Total ? 0f : 0.55f);
                if (r.Kind == RowKind.Total) Line(c.Cur, Margin, y0 + rowH, Margin + tableW, y0 + rowH, 0.9f, 0f);
                float vx = Margin;
                for (int i = 0; i <= n; i++)
                {
                    Line(c.Cur, vx, y0, vx, y0 + rowH, 0.3f, 0.55f);
                    if (i < n) vx += w[i];
                }
                c.Y = y0;
            }
            c.Y -= 8;
        }

        static float[] NaturalWidths(ReportSection s, float size, out List<string>[] heads, float avail)
        {
            int n = s.Columns.Count;
            var w = new float[n];
            heads = new List<string>[n];
            for (int i = 0; i < n; i++)
            {
                var col = s.Columns[i];
                float max = 0;
                foreach (var r in s.Rows)
                {
                    if (r.Kind == RowKind.Group) continue;
                    object v = i < r.Cells.Length ? r.Cells[i] : null;
                    float tw = TextWidth(Fmt.Cell(v, col), r.Kind != RowKind.Normal, size);
                    if (tw > max) max = tw;
                }
                // long free-text columns are capped and truncated
                float cap = col.Format == "T" ? Math.Max(avail * 0.22f, 60f) : avail * 0.15f;
                max = Math.Min(max, cap);
                // header: wrap on words to at most 3 lines, width = widest line
                float headWord = 0;
                foreach (var word in col.Header.Split(' ')) headWord = Math.Max(headWord, TextWidth(word, true, size));
                float colW = Math.Max(max, headWord) + 5f;
                var lines = Wrap(col.Header, true, size, Math.Max(colW - 4f, headWord));
                while (lines.Count > 3) { colW += 8; lines = Wrap(col.Header, true, size, colW - 4f); }
                heads[i] = lines;
                w[i] = Math.Max(colW, 18f);
            }
            return w;
        }

        static void DrawHeader(LayoutCtx c, ReportSection s, float[] w, List<string>[] heads, float size, float headH)
        {
            float tableW = 0; foreach (var x in w) tableW += x;
            float y0 = c.Y - headH;
            Rect(c.Cur, Margin, y0, tableW, headH, new[] { 0.91f, 0.925f, 0.915f }, 0.8f);
            float xx = Margin;
            for (int i = 0; i < w.Length; i++)
            {
                var lines = heads[i];
                float ly = y0 + headH - 3 - size;
                float startY = y0 + (headH + lines.Count * (size + 1.5f)) / 2 - size;
                ly = startY;
                foreach (var l in lines)
                {
                    float tw = TextWidth(l, true, size);
                    Text(c.Cur, xx + (w[i] - tw) / 2, ly, l, true, size, 0f);
                    ly -= size + 1.5f;
                }
                if (i > 0) Line(c.Cur, xx, y0, xx, y0 + headH, 0.5f, 0.2f);
                xx += w[i];
            }
            c.Y = y0;
        }

        // ------------------------------------------------------------------ one-page summary strip

        const float StripLine = 9.2f, StripSize = 6.3f, StripHead = 12f;

        static int StripLines(SummaryBlock b) { return b.Table != null ? b.Table.Rows.Count + 1 : b.Items.Count; }

        static float StripHeight(List<SummaryBlock> blocks)
        {
            int max = 0; foreach (var b in blocks) max = Math.Max(max, StripLines(b));
            return blocks.Count == 0 ? 0 : StripHead + max * StripLine + 4;
        }

        /// <summary>All summary boxes side by side in one strip; widths follow SummaryBlock.Weight.</summary>
        static void DrawStrip(LayoutCtx c, List<SummaryBlock> blocks)
        {
            float gap = 6f, h = StripHeight(blocks), wsum = 0;
            foreach (var b in blocks) wsum += b.Weight;
            float usable = c.Width - gap * (blocks.Count - 1);
            Need(c, h + 6);
            c.Y -= 4;
            float top = c.Y, x = Margin;
            foreach (var b in blocks)
            {
                float bw = usable * b.Weight / wsum;
                Rect(c.Cur, x, top - h, bw, h, null, 0.7f);
                Rect(c.Cur, x, top - StripHead, bw, StripHead, new[] { 0.91f, 0.925f, 0.915f }, 0.7f);
                Text(c.Cur, x + 3, top - 8.6f, Fit(b.Title, true, 7f, bw - 6), true, 7f, 0f);
                float y = top - StripHead;
                if (b.Table != null)
                {
                    var t = b.Table; int n = t.Columns.Count;
                    // natural width of each column (header and values), then scaled to the box width
                    var cw = new float[n]; float nat = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float mx = TextWidth(t.Columns[i].Header, true, StripSize);
                        foreach (var r in t.Rows) mx = Math.Max(mx, TextWidth(Fmt.Cell(i < r.Cells.Length ? r.Cells[i] : null, t.Columns[i]), true, StripSize));
                        cw[i] = mx + 5f; nat += cw[i];
                    }
                    for (int i = 0; i < n; i++) cw[i] *= bw / nat;
                    y -= StripLine;
                    float xx = x;
                    for (int i = 0; i < n; i++)
                    {
                        string hd = Fit(t.Columns[i].Header, true, StripSize, cw[i] - 3);
                        float tw = TextWidth(hd, true, StripSize);
                        Text(c.Cur, i == 0 ? xx + 2 : xx + cw[i] - 2 - tw, y + 2.6f, hd, true, StripSize, 0f);
                        xx += cw[i];
                    }
                    Line(c.Cur, x + 1, y, x + bw - 1, y, 0.5f, 0.2f);
                    foreach (var r in t.Rows)
                    {
                        y -= StripLine;
                        bool bold = r.Kind != RowKind.Normal;
                        xx = x;
                        for (int i = 0; i < n; i++)
                        {
                            var col = t.Columns[i];
                            string v = Fit(Fmt.Cell(i < r.Cells.Length ? r.Cells[i] : null, col), bold, StripSize, cw[i] - 3);
                            float tw = TextWidth(v, bold, StripSize);
                            Text(c.Cur, col.Align == Align.Right ? xx + cw[i] - 2 - tw : xx + 2, y + 2.6f, v, bold, StripSize, 0f);
                            xx += cw[i];
                        }
                        Line(c.Cur, x + 1, y, x + bw - 1, y, bold ? 0.6f : 0.25f, bold ? 0f : 0.75f);
                    }
                }
                else
                {
                    foreach (var kv in b.Items)
                    {
                        y -= StripLine;
                        float vw = TextWidth(kv.Value, true, StripSize);
                        Text(c.Cur, x + 3, y + 2.6f, Fit(kv.Key, false, StripSize, bw - vw - 9), false, StripSize, 0f);
                        Text(c.Cur, x + bw - 3 - vw, y + 2.6f, kv.Value, true, StripSize, 0f);
                        Line(c.Cur, x + 1, y, x + bw - 1, y, 0.25f, 0.75f);
                    }
                }
                x += bw + gap;
            }
            c.Y = top - h - 4;
        }

        static void DrawSummaries(LayoutCtx c, List<SummaryBlock> blocks)
        {
            float boxW = 255f, gap = 12f, lineH = 10.5f, size = 7.5f;
            int perRow = Math.Max(1, (int)((c.Width + gap) / (boxW + gap)));
            boxW = (c.Width - gap * (perRow - 1)) / perRow;
            if (boxW > 300) boxW = 300;
            c.Y -= 4;
            for (int i = 0; i < blocks.Count; i += perRow)
            {
                float maxH = 0;
                for (int j = i; j < Math.Min(blocks.Count, i + perRow); j++) maxH = Math.Max(maxH, 14 + blocks[j].Items.Count * lineH + 4);
                Need(c, maxH + 6);
                float top = c.Y;
                for (int j = i; j < Math.Min(blocks.Count, i + perRow); j++)
                {
                    var b = blocks[j];
                    float x = Margin + (j - i) * (boxW + gap);
                    float h = 14 + b.Items.Count * lineH + 4;
                    Rect(c.Cur, x, top - h, boxW, h, null, 0.7f);
                    Rect(c.Cur, x, top - 14, boxW, 14, new[] { 0.91f, 0.925f, 0.915f }, 0.7f);
                    Text(c.Cur, x + 4, top - 10, Fit(b.Title, true, 8f, boxW - 8), true, 8f, 0f);
                    float y = top - 14;
                    foreach (var kv in b.Items)
                    {
                        y -= lineH;
                        float vw = TextWidth(kv.Value, true, size);
                        Text(c.Cur, x + 4, y + 3, Fit(kv.Key, false, size, boxW - vw - 14), false, size, 0f);
                        Text(c.Cur, x + boxW - 4 - vw, y + 3, kv.Value, true, size, 0f);
                        Line(c.Cur, x + 2, y, x + boxW - 2, y, 0.25f, 0.75f);
                    }
                }
                c.Y = top - maxH - gap / 2;
            }
        }

        // ------------------------------------------------------------------ drawing primitives

        static string Esc(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            foreach (var ch in s)
            {
                char o = ch;
                if (ch == '\u20B9') { sb.Append("Rs"); continue; }       // rupee sign is not in WinAnsi
                if (ch == '\u2013' || ch == '\u2014') o = '-';
                else if (ch == '\u2018' || ch == '\u2019') o = '\'';
                else if (ch == '\u201C' || ch == '\u201D') o = '"';
                else if (ch > 255 || (ch < 32)) o = ch < 32 ? ' ' : '?';
                if (o == '(' || o == ')' || o == '\\') sb.Append('\\');
                sb.Append(o);
            }
            return sb.ToString();
        }

        static string F(float v) { return v.ToString("0.##", Inv); }

        /// <summary>gray: 0 = black, &gt;0 grey level, -1 = red (negative numbers)</summary>
        static void Text(Page p, float x, float y, string s, bool bold, float size, float gray)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (gray < 0) p.Ops.Append("0.7 0.15 0.12 rg ");
            else p.Ops.Append(F(gray)).Append(" g ");
            p.Ops.Append("BT /").Append(bold ? "F2 " : "F1 ").Append(F(size)).Append(" Tf ").Append(F(x)).Append(' ').Append(F(y))
                 .Append(" Td (").Append(Esc(s)).Append(") Tj ET\n");
        }

        static void Center(Page p, float y, string s, bool bold, float size) { Center(p, y, s, bold, size, PageW / 2); }

        static void Center(Page p, float y, string s, bool bold, float size, float cx)
        {
            if (string.IsNullOrEmpty(s)) return;
            var t = Fit(s, bold, size, PageW - 2 * Margin);
            Text(p, cx - TextWidth(t, bold, size) / 2, y, t, bold, size, 0f);
        }

        static void Line(Page p, float x1, float y1, float x2, float y2, float width, float gray)
        {
            p.Ops.Append(F(gray)).Append(" G ").Append(F(width)).Append(" w ").Append(F(x1)).Append(' ').Append(F(y1)).Append(" m ")
                 .Append(F(x2)).Append(' ').Append(F(y2)).Append(" l S\n");
        }

        /// <summary>fill = rgb or null; strokeWidth &lt; 0 = no border</summary>
        static void Rect(Page p, float x, float y, float w, float h, float[] fill, float strokeWidth)
        {
            if (fill != null)
                p.Ops.Append(F(fill[0])).Append(' ').Append(F(fill[1])).Append(' ').Append(F(fill[2])).Append(" rg ")
                     .Append(F(x)).Append(' ').Append(F(y)).Append(' ').Append(F(w)).Append(' ').Append(F(h)).Append(" re f\n");
            if (strokeWidth > 0)
                p.Ops.Append("0.2 G ").Append(F(strokeWidth)).Append(" w ").Append(F(x)).Append(' ').Append(F(y)).Append(' ')
                     .Append(F(w)).Append(' ').Append(F(h)).Append(" re S\n");
        }

        static string Fit(string s, bool bold, float size, float width)
        {
            if (string.IsNullOrEmpty(s) || TextWidth(s, bold, size) <= width) return s ?? "";
            const string ell = "..";
            int len = s.Length;
            while (len > 1 && TextWidth(s.Substring(0, len) + ell, bold, size) > width) len--;
            return s.Substring(0, len) + ell;
        }

        static List<string> Wrap(string s, bool bold, float size, float width)
        {
            var lines = new List<string>();
            var cur = "";
            foreach (var word in (s ?? "").Split(' '))
            {
                var tryS = cur.Length == 0 ? word : cur + " " + word;
                if (TextWidth(tryS, bold, size) <= width || cur.Length == 0) cur = tryS;
                else { lines.Add(cur); cur = word; }
            }
            if (cur.Length > 0 || lines.Count == 0) lines.Add(cur);
            return lines;
        }

        // ------------------------------------------------------------------ file structure

        static byte[] Build(LayoutCtx c)
        {
            var enc = Encoding.GetEncoding(28591); // ISO-8859-1 ~ WinAnsi for the characters we emit
            int total = c.Pages.Count;
            for (int i = 0; i < total; i++)
            {
                var p = c.Pages[i];
                Line(p, Margin, Margin + 10, PageW - Margin, Margin + 10, 0.4f, 0.4f);
                Text(p, Margin, Margin + 2, "Printed by " + c.M.GeneratedBy + " on " + c.M.GeneratedOn.ToString("dd-MM-yyyy HH:mm", Inv) + "   |   Phoenix Poultry ERP", false, 6.5f, 0.35f);
                var pg = "Page " + (i + 1) + " of " + total;
                Text(p, PageW - Margin - TextWidth(pg, false, 6.5f), Margin + 2, pg, false, 6.5f, 0.35f);
            }

            var ms = new MemoryStream();
            var offsets = new List<long>();
            Action<string> w = s => { var b = enc.GetBytes(s); ms.Write(b, 0, b.Length); };
            w("%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");

            // object numbers: 1 catalog, 2 pages, 3 font regular, 4 font bold, 5 info, then (page, content) pairs
            int firstPageObj = 6;
            var kids = new StringBuilder();
            for (int i = 0; i < total; i++) kids.Append(firstPageObj + i * 2).Append(" 0 R ");

            Action<int, string> obj = (num, body) =>
            {
                while (offsets.Count < num) offsets.Add(0);
                offsets[num - 1] = ms.Position;
                w(num + " 0 obj\n" + body + "\nendobj\n");
            };
            obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
            obj(2, "<< /Type /Pages /Kids [" + kids + "] /Count " + total + " >>");
            obj(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            obj(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
            obj(5, "<< /Title (" + Esc(c.M.Title ?? "") + ") /Producer (Phoenix Poultry ERP) /CreationDate (D:" + DateTime.Now.ToString("yyyyMMddHHmmss", Inv) + ") >>");
            for (int i = 0; i < total; i++)
            {
                int pageObj = firstPageObj + i * 2, contentObj = pageObj + 1;
                obj(pageObj, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + F(PageW) + " " + F(PageH) + "] " +
                             "/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents " + contentObj + " 0 R >>");
                var bytes = enc.GetBytes(c.Pages[i].Ops.ToString());
                while (offsets.Count < contentObj) offsets.Add(0);
                offsets[contentObj - 1] = ms.Position;
                w(contentObj + " 0 obj\n<< /Length " + bytes.Length + " >>\nstream\n");
                ms.Write(bytes, 0, bytes.Length);
                w("\nendstream\nendobj\n");
            }
            long xref = ms.Position;
            var sb = new StringBuilder();
            sb.Append("xref\n0 ").Append(offsets.Count + 1).Append("\n0000000000 65535 f \n");
            foreach (var o in offsets) sb.Append(o.ToString("0000000000", Inv)).Append(" 00000 n \n");
            sb.Append("trailer\n<< /Size ").Append(offsets.Count + 1).Append(" /Root 1 0 R /Info 5 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
            w(sb.ToString());
            return ms.ToArray();
        }
    }
}
