// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace UABlackoutPreWarning.Core
{
    /// <summary>
    /// Reads the outage grid from the ZTOE page (unhooking-search.php).
    /// The markup is matched loosely so that small layout changes and
    /// browser-normalised copies (extra tbody, different quoting) still parse.
    /// </summary>
    public static class ScheduleParser
    {
        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant;

        private static readonly Regex TableRx = new Regex(@"<table\b[^>]*>(.*?)</table\s*>", Opts);
        private static readonly Regex RowRx = new Regex(@"<tr\b[^>]*>(.*?)</tr\s*>", Opts);
        private static readonly Regex CellRx = new Regex(@"<t[dh]\b([^>]*)>(.*?)</t[dh]\s*>", Opts);
        private static readonly Regex TagRx = new Regex(@"<[^>]*>", Opts);
        private static readonly Regex BrRx = new Regex(@"<br\s*/?>", Opts);
        private static readonly Regex H3Rx = new Regex(@"<h3\b[^>]*>(.*?)</h3\s*>", Opts);
        private static readonly Regex SlotHeaderRx = new Regex(@"^(\d{1,2}):(\d{2})\s*-\s*(\d{1,2}):(\d{2})$", Opts);
        private static readonly Regex LabelRx = new Regex(@"^\d{1,2}\.\d{1,2}$", Opts);
        private static readonly Regex DateRx = new Regex(@"(\d{2})\.(\d{2})\.(\d{4})", Opts);
        private static readonly Regex UpdatedRx = new Regex(
            @"Дата\s+оновлення\s+інформації\s*[-–—:]?\s*(\d{1,2}):(\d{2})\s+(\d{2})\.(\d{2})\.(\d{4})", Opts);
        private static readonly Regex BackgroundRx = new Regex(
            @"background(?:-color)?\s*:\s*([^;""']+)", Opts);
        private static readonly Regex BgcolorRx = new Regex(
            @"\bbgcolor\s*=\s*[""']?([#\w]+)", Opts);
        private static readonly Regex ColspanRx = new Regex(@"\bcolspan\s*=\s*[""']?(\d+)", Opts);

        /// <summary>Parses the page; throws <see cref="ScheduleFormatException"/> when no grid is found.</summary>
        public static ParsedPage Parse(string html)
        {
            if (html == null) throw new ArgumentNullException("html");

            var page = new ParsedPage();
            ReadNotice(html, page);
            ReadUpdated(html, page);

            foreach (Match table in TableRx.Matches(html))
            {
                var day = TryReadGrid(table.Groups[1].Value);
                if (day != null) page.Days.Add(day);
            }

            if (page.Days.Count == 0)
            {
                throw new ScheduleFormatException("The outage schedule table was not found on the page.");
            }

            page.Days.Sort(delegate(DaySchedule a, DaySchedule b) { return a.Date.CompareTo(b.Date); });
            return page;
        }

        /// <summary>True when a CSS colour value means "white" (power on).</summary>
        public static bool IsWhite(string colour)
        {
            if (colour == null) return true;
            var c = colour.Trim().ToLowerInvariant().Replace(" ", "");
            if (c.Length == 0) return true;
            if (c.EndsWith("!important")) c = c.Substring(0, c.Length - "!important".Length);
            return c == "white" || c == "#fff" || c == "#ffffff" || c == "#ffffffff" ||
                   c == "rgb(255,255,255)" || c == "transparent" || c == "none";
        }

        private static DaySchedule TryReadGrid(string tableHtml)
        {
            // Quick filter: a grid table has the 48 half-hour headers.
            if (tableHtml.IndexOf("00:00", StringComparison.Ordinal) < 0 ||
                tableHtml.IndexOf("23:30", StringComparison.Ordinal) < 0)
            {
                return null;
            }

            DateTime? date = null;
            int headerSlots = 0;
            DaySchedule day = null;
            var pending = new List<KeyValuePair<string, bool[]>>();

            foreach (Match row in RowRx.Matches(tableHtml))
            {
                var cells = CellRx.Matches(row.Groups[1].Value);

                // Header: the date cell spans the 48 slot columns.
                if (date == null)
                {
                    foreach (Match cell in cells)
                    {
                        var span = ColspanRx.Match(cell.Groups[1].Value);
                        if (span.Success && span.Groups[1].Value == "48")
                        {
                            var d = DateRx.Match(Text(cell.Groups[2].Value));
                            if (d.Success) date = ToDate(d);
                        }
                    }
                }

                // Header: the time-slot labels.
                int slotHeaders = 0;
                foreach (Match cell in cells)
                {
                    if (SlotHeaderRx.IsMatch(Text(cell.Groups[2].Value))) slotHeaders++;
                }
                if (slotHeaders > 0)
                {
                    headerSlots = Math.Max(headerSlots, slotHeaders);
                    continue;
                }

                // Data row: a cell whose text is a label such as "5.2", then 48 coloured cells.
                int labelIndex = -1;
                string label = null;
                for (int i = 0; i < cells.Count; i++)
                {
                    var text = Text(cells[i].Groups[2].Value);
                    if (LabelRx.IsMatch(text))
                    {
                        labelIndex = i;
                        label = text;
                        break;
                    }
                }
                if (label == null) continue;

                var off = new List<bool>(Slots.PerDay);
                for (int i = labelIndex + 1; i < cells.Count; i++)
                {
                    string colour;
                    if (!TryGetBackground(cells[i].Groups[1].Value, out colour)) continue; // spacer cell
                    off.Add(!IsWhite(colour));
                }
                if (off.Count != Slots.PerDay)
                {
                    throw new ScheduleFormatException(string.Format(CultureInfo.InvariantCulture,
                        "Row {0} has {1} time slots instead of {2}.", label, off.Count, Slots.PerDay));
                }
                pending.Add(new KeyValuePair<string, bool[]>(label, off.ToArray()));
            }

            if (pending.Count == 0) return null;
            if (headerSlots != 0 && headerSlots != Slots.PerDay)
            {
                throw new ScheduleFormatException(string.Format(CultureInfo.InvariantCulture,
                    "The grid has {0} time columns instead of {1}.", headerSlots, Slots.PerDay));
            }
            if (date == null)
            {
                throw new ScheduleFormatException("The schedule table has no date.");
            }

            day = new DaySchedule(date.Value);
            foreach (var p in pending) day.AddRow(p.Key, p.Value);
            return day;
        }

        private static bool TryGetBackground(string attributes, out string colour)
        {
            var m = BackgroundRx.Match(attributes);
            if (m.Success)
            {
                colour = m.Groups[1].Value.Trim();
                return true;
            }
            m = BgcolorRx.Match(attributes);
            if (m.Success)
            {
                colour = m.Groups[1].Value.Trim();
                return true;
            }
            colour = null;
            return false;
        }

        private static void ReadNotice(string html, ParsedPage page)
        {
            var m = H3Rx.Match(html);
            if (!m.Success) return;
            var first = Text(m.Groups[1].Value);
            if (first.StartsWith("Графіки", StringComparison.OrdinalIgnoreCase)) return; // no banner today

            foreach (var part in BrRx.Split(m.Groups[1].Value))
            {
                var line = Text(part);
                if (line.Length == 0) continue;
                if (line.StartsWith("Пошук черги", StringComparison.OrdinalIgnoreCase)) continue;
                page.NoticeLines.Add(line);
            }
        }

        private static void ReadUpdated(string html, ParsedPage page)
        {
            var m = UpdatedRx.Match(html);
            if (!m.Success) return;
            try
            {
                page.UpdatedAt = new DateTime(
                    int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), 0);
            }
            catch (ArgumentOutOfRangeException)
            {
                page.UpdatedAt = null;
            }
        }

        private static DateTime? ToDate(Match d)
        {
            try
            {
                return new DateTime(
                    int.Parse(d.Groups[3].Value, CultureInfo.InvariantCulture),
                    int.Parse(d.Groups[2].Value, CultureInfo.InvariantCulture),
                    int.Parse(d.Groups[1].Value, CultureInfo.InvariantCulture));
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>Tag-free, entity-decoded, whitespace-collapsed text.</summary>
        private static string Text(string html)
        {
            var s = TagRx.Replace(html, " ");
            s = WebUtility.HtmlDecode(s);
            var sb = new StringBuilder(s.Length);
            bool space = false;
            foreach (var ch in s)
            {
                if (char.IsWhiteSpace(ch) || ch == ' ')
                {
                    space = true;
                    continue;
                }
                if (space && sb.Length > 0) sb.Append(' ');
                space = false;
                sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
