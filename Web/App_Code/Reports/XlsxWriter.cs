using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace PoultryERP.Reports
{
    /// <summary>
    /// Minimal, dependency-free .xlsx (Office Open XML) writer for report models:
    /// real numeric cells with number formats, bold headers, borders, merged title.
    /// </summary>
    public static class XlsxWriter
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // cellXfs indexes (see Styles())
        const int sTitle = 1, sSub = 2, sHead = 3, sText = 4, sInt = 5, s1 = 6, s2 = 7, s3 = 8, sDate = 9,
                  sTotText = 10, sTotInt = 11, sTot1 = 12, sTot2 = 13, sTot3 = 14, sGroup = 15, sSection = 16, sKey = 17, sVal = 18;

        public static byte[] Write(ReportModel m)
        {
            int maxCols = 2;
            foreach (var s in m.Sections) maxCols = Math.Max(maxCols, s.Columns.Count);
            var widths = new double[maxCols];
            for (int i = 0; i < maxCols; i++) widths[i] = 8;

            var rows = new StringBuilder();
            var merges = new List<string>();
            int r = 0;
            Action<string, int> fullRow = (text, style) =>
            {
                r++;
                rows.Append("<row r=\"").Append(r).Append("\">").Append(Str(Ref(0, r), text, style)).Append("</row>");
                if (maxCols > 1) merges.Add(Ref(0, r) + ":" + Ref(maxCols - 1, r));
            };
            fullRow(m.CompanyName, sTitle);
            fullRow(m.Title, sSection);
            if (!string.IsNullOrEmpty(m.Subtitle)) fullRow(m.Subtitle, sSub);
            r++;

            foreach (var s in m.Sections)
            {
                if (!string.IsNullOrEmpty(s.Title))
                {
                    r++;
                    rows.Append("<row r=\"").Append(r).Append("\">").Append(Str(Ref(0, r), s.Title, sSection)).Append("</row>");
                }
                r++;
                rows.Append("<row r=\"").Append(r).Append("\" ht=\"30\" customHeight=\"1\">");
                for (int i = 0; i < s.Columns.Count; i++)
                {
                    rows.Append(Str(Ref(i, r), s.Columns[i].Header, sHead));
                    widths[i] = Math.Max(widths[i], Math.Min(18, s.Columns[i].Header.Length * 0.75 + 2));
                }
                rows.Append("</row>");
                foreach (var row in s.Rows)
                {
                    r++;
                    rows.Append("<row r=\"").Append(r).Append("\">");
                    if (row.Kind == RowKind.Group)
                    {
                        rows.Append(Str(Ref(0, r), Convert.ToString(row.Cells[0]), sGroup));
                        for (int i = 1; i < s.Columns.Count; i++) rows.Append("<c r=\"").Append(Ref(i, r)).Append("\" s=\"").Append(sGroup).Append("\"/>");
                        if (s.Columns.Count > 1) merges.Add(Ref(0, r) + ":" + Ref(s.Columns.Count - 1, r));
                        rows.Append("</row>");
                        continue;
                    }
                    bool tot = row.Kind != RowKind.Normal;
                    for (int i = 0; i < s.Columns.Count; i++)
                    {
                        var c = s.Columns[i];
                        object v = i < row.Cells.Length ? row.Cells[i] : null;
                        string reference = Ref(i, r);
                        if (v == null || v == DBNull.Value)
                        {
                            rows.Append("<c r=\"").Append(reference).Append("\" s=\"").Append(tot ? sTotText : sText).Append("\"/>");
                        }
                        else if (v is DateTime)
                        {
                            rows.Append("<c r=\"").Append(reference).Append("\" s=\"").Append(sDate).Append("\"><v>")
                                .Append(((DateTime)v).ToOADate().ToString(Inv)).Append("</v></c>");
                            widths[i] = Math.Max(widths[i], 11);
                        }
                        else if (Fmt.IsNumber(v))
                        {
                            int dec = c.Decimals;
                            int st = tot ? (dec == 0 ? sTotInt : dec == 1 ? sTot1 : dec == 2 ? sTot2 : sTot3) : (dec == 0 ? sInt : dec == 1 ? s1 : dec == 2 ? s2 : s3);
                            var num = Convert.ToDecimal(v);
                            rows.Append("<c r=\"").Append(reference).Append("\" s=\"").Append(st).Append("\"><v>").Append(num.ToString(Inv)).Append("</v></c>");
                            widths[i] = Math.Max(widths[i], Math.Min(16, Fmt.Cell(v, c).Length + 2));
                        }
                        else
                        {
                            var txt = Convert.ToString(v, Inv);
                            rows.Append(Str(reference, txt, tot ? sTotText : sText));
                            widths[i] = Math.Max(widths[i], Math.Min(45, txt.Length + 2));
                        }
                    }
                    rows.Append("</row>");
                }
                r++;
            }

            if (m.OnePage && m.Summaries.Count > 0)
            {
                // summary boxes side by side (as in the farm's Excel): each box spans SummaryBlock.XlsxSpan columns
                int lines = 0;
                foreach (var b in m.Summaries) lines = Math.Max(lines, b.Table != null ? b.Table.Rows.Count + 1 : b.Items.Count);
                var grid = new Dictionary<int, SortedDictionary<int, string>>();   // row -> col -> cell xml
                Action<int, int, string> put = (row, col, xml) =>
                {
                    SortedDictionary<int, string> cells;
                    if (!grid.TryGetValue(row, out cells)) { cells = new SortedDictionary<int, string>(); grid[row] = cells; }
                    cells[col] = xml;
                };
                int top = r + 2, col0 = 0;
                foreach (var b in m.Summaries)
                {
                    int span = Math.Max(2, b.Table != null ? b.Table.Columns.Count : b.XlsxSpan);
                    put(top, col0, Str(Ref(col0, top), b.Title, sSection));
                    if (b.Table != null)
                    {
                        for (int i = 0; i < b.Table.Columns.Count; i++) put(top + 1, col0 + i, Str(Ref(col0 + i, top + 1), b.Table.Columns[i].Header, sHead));
                        int rr = top + 2;
                        foreach (var row in b.Table.Rows)
                        {
                            bool tot = row.Kind != RowKind.Normal;
                            for (int i = 0; i < b.Table.Columns.Count; i++)
                            {
                                object v = i < row.Cells.Length ? row.Cells[i] : null;
                                string reference = Ref(col0 + i, rr);
                                if (Fmt.IsNumber(v))
                                    put(rr, col0 + i, "<c r=\"" + reference + "\" s=\"" + (tot ? sTotInt : sInt) + "\"><v>" + Convert.ToDecimal(v).ToString(Inv) + "</v></c>");
                                else put(rr, col0 + i, Str(reference, Convert.ToString(v, Inv), tot ? sTotText : sText));
                            }
                            rr++;
                        }
                    }
                    else
                    {
                        int rr = top + 1;
                        foreach (var kv in b.Items)
                        {
                            put(rr, col0, Str(Ref(col0, rr), kv.Key, sKey));
                            if (span > 2) merges.Add(Ref(col0, rr) + ":" + Ref(col0 + span - 2, rr));
                            put(rr, col0 + span - 1, Str(Ref(col0 + span - 1, rr), kv.Value, sVal));
                            if (span == 2 && col0 < widths.Length) widths[col0] = Math.Max(widths[col0], Math.Min(28, kv.Key.Length + 1));
                            if (col0 + span - 1 < widths.Length) widths[col0 + span - 1] = Math.Max(widths[col0 + span - 1], Math.Min(16, kv.Value.Length + 1));
                            rr++;
                        }
                    }
                    col0 += span;
                }
                foreach (var kv in grid.OrderBy(k => k.Key))
                {
                    rows.Append("<row r=\"").Append(kv.Key).Append("\">");
                    foreach (var cell in kv.Value.Values) rows.Append(cell);
                    rows.Append("</row>");
                }
                r = top + lines + 1;
            }
            else
            foreach (var b in m.Summaries)
            {
                r++;
                rows.Append("<row r=\"").Append(r).Append("\">").Append(Str(Ref(0, r), b.Title, sSection)).Append("</row>");
                foreach (var kv in b.Items)
                {
                    r++;
                    rows.Append("<row r=\"").Append(r).Append("\">").Append(Str(Ref(0, r), kv.Key, sKey)).Append(Str(Ref(1, r), kv.Value, sVal)).Append("</row>");
                    widths[0] = Math.Max(widths[0], Math.Min(40, kv.Key.Length + 2));
                    widths[1] = Math.Max(widths[1], Math.Min(30, kv.Value.Length + 2));
                }
                r++;
            }
            foreach (var n in m.Notes)
            {
                r++;
                rows.Append("<row r=\"").Append(r).Append("\">").Append(Str(Ref(0, r), n, sSub)).Append("</row>");
            }
            r++;
            rows.Append("<row r=\"").Append(r + 1).Append("\">").Append(Str(Ref(0, r + 1), "Generated by " + m.GeneratedBy + " on " + m.GeneratedOn.ToString("dd-MM-yyyy HH:mm", Inv), sSub)).Append("</row>");

            var sheet = new StringBuilder();
            sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            sheet.Append("<sheetPr><pageSetUpPr fitToPage=\"1\"/></sheetPr>");
            sheet.Append("<sheetViews><sheetView workbookViewId=\"0\" showGridLines=\"0\"/></sheetViews>");
            sheet.Append("<sheetFormatPr defaultRowHeight=\"15\"/><cols>");
            for (int i = 0; i < maxCols; i++)
                sheet.Append("<col min=\"").Append(i + 1).Append("\" max=\"").Append(i + 1).Append("\" width=\"").Append(widths[i].ToString("0.0", Inv)).Append("\" customWidth=\"1\"/>");
            sheet.Append("</cols><sheetData>").Append(rows).Append("</sheetData>");
            if (merges.Count > 0)
            {
                sheet.Append("<mergeCells count=\"").Append(merges.Count).Append("\">");
                foreach (var mg in merges) sheet.Append("<mergeCell ref=\"").Append(mg).Append("\"/>");
                sheet.Append("</mergeCells>");
            }
            if (m.OnePage)
                sheet.Append("<pageMargins left=\"0.16\" right=\"0.16\" top=\"0.16\" bottom=\"0.16\" header=\"0.1\" footer=\"0.1\"/>");
            else
                sheet.Append("<pageMargins left=\"0.4\" right=\"0.4\" top=\"0.5\" bottom=\"0.5\" header=\"0.3\" footer=\"0.3\"/>");
            sheet.Append("<pageSetup paperSize=\"9\" orientation=\"").Append(m.Landscape ? "landscape" : "portrait")
                 .Append("\" fitToWidth=\"1\" fitToHeight=\"").Append(m.OnePage ? "1" : "0").Append("\"/>");
            sheet.Append("</worksheet>");

            string sheetName = SheetName(m.Title);
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    Add(zip, "[Content_Types].xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                        "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>" +
                        "</Types>");
                    Add(zip, "_rels/.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
                        "</Relationships>");
                    Add(zip, "docProps/core.xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
                        "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                        "<dc:title>" + X(m.Title) + "</dc:title><dc:creator>Phoenix Poultry ERP</dc:creator>" +
                        "<dcterms:created xsi:type=\"dcterms:W3CDTF\">" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", Inv) + "</dcterms:created></cp:coreProperties>");
                    Add(zip, "xl/workbook.xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"" + X(sheetName) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                        "<definedNames><definedName name=\"_xlnm.Print_Titles\" localSheetId=\"0\">'" + sheetName.Replace("'", "''") + "'!$1:$3</definedName></definedNames></workbook>");
                    Add(zip, "xl/_rels/workbook.xml.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                        "</Relationships>");
                    Add(zip, "xl/styles.xml", Styles());
                    Add(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
                }
                return ms.ToArray();
            }
        }

        static string Styles()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<numFmts count=\"5\"><numFmt numFmtId=\"164\" formatCode=\"#,##0\"/><numFmt numFmtId=\"165\" formatCode=\"#,##0.0\"/>" +
                "<numFmt numFmtId=\"166\" formatCode=\"#,##0.00\"/><numFmt numFmtId=\"167\" formatCode=\"#,##0.000\"/><numFmt numFmtId=\"168\" formatCode=\"dd-mm-yyyy\"/></numFmts>" +
                "<fonts count=\"4\"><font><sz val=\"10\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"10\"/><name val=\"Calibri\"/></font>" +
                "<font><b/><sz val=\"14\"/><name val=\"Calibri\"/></font><font><i/><sz val=\"9\"/><color rgb=\"FF555555\"/><name val=\"Calibri\"/></font></fonts>" +
                "<fills count=\"5\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFDDE6E0\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFEEF2EF\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFBF3DC\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
                "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border>" +
                "<border><left style=\"thin\"><color rgb=\"FF9AA19C\"/></left><right style=\"thin\"><color rgb=\"FF9AA19C\"/></right>" +
                "<top style=\"thin\"><color rgb=\"FF9AA19C\"/></top><bottom style=\"thin\"><color rgb=\"FF9AA19C\"/></bottom><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"19\">" +
                "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +                                                                  // 0
                "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +                                                   // 1 title
                "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +                                                   // 2 subtitle
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" + // 3 header
                "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"/>" +                                                 // 4 text
                "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +                       // 5
                "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +                       // 6
                "<xf numFmtId=\"166\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +                       // 7
                "<xf numFmtId=\"167\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +                       // 8
                "<xf numFmtId=\"168\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>" + // 9 date
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +                 // 10 total text
                "<xf numFmtId=\"164\" fontId=\"1\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" + // 11
                "<xf numFmtId=\"165\" fontId=\"1\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" + // 12
                "<xf numFmtId=\"166\" fontId=\"1\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" + // 13
                "<xf numFmtId=\"167\" fontId=\"1\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" + // 14
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +                 // 15 group
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +                                                   // 16 section
                "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"/>" +                                                 // 17 key
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\"/></xf>" + // 18 value
                "</cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>";
        }

        static void Add(ZipArchive zip, string name, string content)
        {
            var e = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var s = e.Open())
            {
                var b = new UTF8Encoding(false).GetBytes(content);
                s.Write(b, 0, b.Length);
            }
        }

        static string Str(string reference, string text, int style)
        {
            return "<c r=\"" + reference + "\" s=\"" + style + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" + X(text) + "</t></is></c>";
        }

        static string X(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                // drop characters that are illegal in XML 1.0
                if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue;
                sb.Append(ch);
            }
            return SecurityElement.Escape(sb.ToString());
        }

        static string Ref(int col, int row)
        {
            var name = "";
            int c = col + 1;
            while (c > 0) { int m = (c - 1) % 26; name = (char)('A' + m) + name; c = (c - 1) / 26; }
            return name + row;
        }

        static string SheetName(string t)
        {
            var sb = new StringBuilder();
            foreach (var ch in t ?? "Report") if ("[]:*?/\\".IndexOf(ch) < 0) sb.Append(ch);
            var s = sb.ToString().Trim();
            if (s.Length == 0) s = "Report";
            return s.Length > 31 ? s.Substring(0, 31) : s;
        }
    }
}
