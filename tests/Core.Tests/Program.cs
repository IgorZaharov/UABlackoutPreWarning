// UABlackoutPreWarning - core logic tests.
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.
//
// A tiny self-contained test runner (no NuGet packages needed).
// Usage: Core.Tests.exe [path-to-tests/Fixtures]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UABlackoutPreWarning.Core;

namespace UABlackoutPreWarning.Tests
{
    internal static class Program
    {
        private static int failed;
        private static int passed;
        private static string fixtures;

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            fixtures = args.Length > 0 ? args[0] : FindFixtures();

            Run("Fixture: date, update time, labels", FixtureBasics);
            Run("Fixture: notice lines", FixtureNotice);
            Run("Fixture: queue 5.2 series", FixtureQueue52);
            Run("Fixture: queue 4.2 ends at midnight", FixtureQueue42);
            Run("Fixture: decoding windows-1251 bytes", FixtureDecoding);
            Run("Synthetic: raw markup without tbody, bgcolor cells", SyntheticRawMarkup);
            Run("Synthetic: two days join across midnight", MidnightJoin);
            Run("Synthetic: unknown queue gives no series", UnknownQueue);
            Run("Synthetic: page without grid is rejected", NoGrid);
            Run("Synthetic: row with wrong slot count is rejected", WrongSlotCount);
            Run("Colour detection", Colours);
            Run("Planner: warns once inside the lead window", PlannerOnce);
            Run("Planner: start-up inside window warns with minutes left", PlannerLateStart);
            Run("Planner: no warning during or after an outage", PlannerDuring);
            Run("Planner: moved start warns again, extended end does not", PlannerMoved);
            Run("Planner: persistence keeps last 20 and prunes", PlannerPersistence);
            Run("Next / current series", NextAndCurrent);
            Run("Settings: defaults and normalisation", SettingsNormalize);
            Run("Settings: queue list", SettingsQueueList);
            Run("Strings: both languages complete", StringsComplete);
            Run("Strings: formatting", StringsFormat);
            Run("Kyiv clock", KyivClockWorks);

            Console.WriteLine();
            Console.WriteLine("{0} passed, {1} failed", passed, failed);
            return failed == 0 ? 0 : 1;
        }

        // ---- fixture tests ------------------------------------------------------

        private static ParsedPage LoadFixture()
        {
            var bytes = File.ReadAllBytes(Path.Combine(fixtures, "ztoe-2026-10-28-1930.html"));
            return ScheduleParser.Parse(PageDecoder.Decode(bytes, null));
        }

        private static void FixtureBasics()
        {
            var page = LoadFixture();
            Equal(1, page.Days.Count, "days");
            Equal(new DateTime(2026, 10, 28), page.Days[0].Date, "date");
            Equal(new DateTime(2026, 10, 28, 19, 30, 0), page.UpdatedAt, "updated");
            Equal("1.1;1.2;2.1;2.2;3.1;3.2;4.1;4.2;5.1;5.2;6.1;6.2",
                string.Join(";", page.QueueLabels().ToArray()), "labels");
        }

        private static void FixtureNotice()
        {
            var page = LoadFixture();
            True(page.NoticeLines.Count >= 3, "notice has lines");
            True(page.NoticeLines[0].StartsWith("УВАГА"), "first notice line: " + page.NoticeLines[0]);
            foreach (var line in page.NoticeLines) True(!line.StartsWith("Пошук"), "search caption removed");
        }

        private static void FixtureQueue52()
        {
            var series = SeriesBuilder.Build(LoadFixture().Days, "5.2");
            Equal("2026-10-28 06:00-2026-10-28 09:00|2026-10-28 14:00-2026-10-28 16:00|2026-10-28 19:00-2026-10-28 21:00",
                Join(series), "5.2");
        }

        private static void FixtureQueue42()
        {
            var series = SeriesBuilder.Build(LoadFixture().Days, "4.2");
            Equal("2026-10-28 14:00-2026-10-28 16:30|2026-10-28 21:00-2026-10-29 00:00", Join(series), "4.2");
            Equal("24:00", Strings.EndTime(series[1]), "end shown as 24:00");
        }

        private static void FixtureDecoding()
        {
            var bytes = File.ReadAllBytes(Path.Combine(fixtures, "ztoe-2026-10-28-1930.html"));
            var a = PageDecoder.Decode(bytes, "text/html; charset=windows-1251");
            var b = PageDecoder.Decode(bytes, null);
            Equal(a, b, "header and meta charset agree");
            True(a.Contains("Графіки погодинних відключень"), "Cyrillic decoded");
        }

        // ---- synthetic pages ----------------------------------------------------

        private static string MakeGrid(DateTime date, Dictionary<string, int[]> offSlots, bool useBgcolor)
        {
            var sb = new StringBuilder();
            sb.Append("<table border=0 style='table-layout:fixed'>");
            sb.Append("<tr><td rowspan=2>Черга</td><td rowspan=2>Підчерга</td><td style='width:3pt'>&nbsp;</td>");
            sb.AppendFormat("<td colspan=48><b>{0}</b></td></tr>\n", date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            sb.Append("<tr><td style='width:3pt'>&nbsp;</td>");
            for (int i = 0; i < 48; i++)
            {
                sb.AppendFormat("<td>{0:00}:{1:00}-{2:00}:{3:00}</td>", i / 2, (i % 2) * 30, (i + 1) / 2, ((i + 1) % 2) * 30);
            }
            sb.Append("</tr>\n");
            foreach (var kv in offSlots)
            {
                sb.AppendFormat("<tr><td style='background:white'><a href='x?pidcherga_id=1'><b>{0}</b></a></td><td style='width:3pt'>&nbsp;</td>", kv.Key);
                for (int i = 0; i < 48; i++)
                {
                    bool off = Array.IndexOf(kv.Value, i) >= 0;
                    if (useBgcolor) sb.AppendFormat("<td bgcolor={0}><a>&nbsp;</a></td>", off ? "#FF3333" : "#FFFFFF");
                    else sb.AppendFormat("<td style='border:1pt solid black; background: {0};'><a>&nbsp;</a></td>", off ? "#ff3333" : "#ffffff");
                }
                sb.Append("</tr>\n");
            }
            sb.Append("</table>");
            return sb.ToString();
        }

        private static string Wrap(params string[] grids)
        {
            return "<html><body><h3>Графіки погодинних відключень</h3><h4>Дата оновлення інформації - 07:05 29.10.2026</h4>" +
                   string.Join("<br>", grids) + "</body></html>";
        }

        private static Dictionary<string, int[]> Rows(string label, params int[] off)
        {
            var d = new Dictionary<string, int[]>();
            d[label] = off;
            return d;
        }

        private static void SyntheticRawMarkup()
        {
            var page = ScheduleParser.Parse(Wrap(MakeGrid(new DateTime(2026, 10, 29), Rows("5.2", 0, 1, 47), true)));
            Equal(new DateTime(2026, 10, 29, 7, 5, 0), page.UpdatedAt, "updated");
            Equal(0, page.NoticeLines.Count, "no banner");
            Equal("2026-10-29 00:00-2026-10-29 01:00|2026-10-29 23:30-2026-10-30 00:00",
                Join(SeriesBuilder.Build(page.Days, "5.2")), "series");
        }

        private static void MidnightJoin()
        {
            var html = Wrap(
                MakeGrid(new DateTime(2026, 10, 30), Rows("5.2", 46, 47), false),
                MakeGrid(new DateTime(2026, 10, 29), Rows("5.2", 10), false),
                MakeGrid(new DateTime(2026, 10, 31), Rows("5.2", 0, 1, 2), false));
            var page = ScheduleParser.Parse(html);
            Equal(3, page.Days.Count, "days");
            Equal(new DateTime(2026, 10, 29), page.Days[0].Date, "sorted by date");
            Equal("2026-10-29 05:00-2026-10-29 05:30|2026-10-30 23:00-2026-10-31 01:30",
                Join(SeriesBuilder.Build(page.Days, "5.2")), "joined");
        }

        private static void UnknownQueue()
        {
            var page = LoadFixture();
            Equal(0, SeriesBuilder.Build(page.Days, "7.1").Count, "no series");
        }

        private static void NoGrid()
        {
            Throws(delegate { ScheduleParser.Parse("<html><body><table><tr><td>1</td></tr></table></body></html>"); });
        }

        private static void WrongSlotCount()
        {
            var grid = MakeGrid(new DateTime(2026, 10, 29), Rows("5.2", 3), false);
            var idx = grid.LastIndexOf("<td style='border", StringComparison.Ordinal);
            var broken = grid.Remove(idx, grid.IndexOf("</td>", idx, StringComparison.Ordinal) + 5 - idx);
            Throws(delegate { ScheduleParser.Parse(Wrap(broken)); });
        }

        private static void Colours()
        {
            True(ScheduleParser.IsWhite("#ffffff"), "#ffffff");
            True(ScheduleParser.IsWhite(" White "), "White");
            True(ScheduleParser.IsWhite("#FFF"), "#FFF");
            True(ScheduleParser.IsWhite("rgb(255, 255, 255)"), "rgb");
            True(!ScheduleParser.IsWhite("#ff3333"), "#ff3333 is off");
            True(!ScheduleParser.IsWhite("#ff0000"), "#ff0000 is off");
            True(!ScheduleParser.IsWhite("red"), "red is off");
        }

        // ---- planner ------------------------------------------------------------

        private static List<BlackoutSeries> Today(params string[] ranges)
        {
            var list = new List<BlackoutSeries>();
            foreach (var r in ranges)
            {
                var p = r.Split('-');
                list.Add(new BlackoutSeries(T(p[0]), T(p[1])));
            }
            return list;
        }

        private static DateTime T(string hhmm)
        {
            if (hhmm == "24:00") return new DateTime(2026, 10, 29);
            return DateTime.ParseExact("2026-10-28 " + hhmm, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private static void PlannerOnce()
        {
            var s = Today("14:00-16:00");
            var p = new WarningPlanner();
            Equal(0, p.Due(s, T("13:49"), 10).Count, "too early");
            var due = p.Due(s, T("13:50"), 10);
            Equal(1, due.Count, "due at 13:50");
            Equal(10, due[0].MinutesLeft, "10 min left");
            Equal(0, p.Due(s, T("13:51"), 10).Count, "only once");
            Equal(0, p.Due(s, T("13:59"), 10).Count, "still once");
        }

        private static void PlannerLateStart()
        {
            var due = new WarningPlanner().Due(Today("14:00-16:00"), T("13:56").AddSeconds(30), 10);
            Equal(1, due.Count, "warns");
            Equal(4, due[0].MinutesLeft, "rounded up");
        }

        private static void PlannerDuring()
        {
            var s = Today("14:00-16:00");
            Equal(0, new WarningPlanner().Due(s, T("14:00"), 10).Count, "at start");
            Equal(0, new WarningPlanner().Due(s, T("15:00"), 10).Count, "during");
            Equal(0, new WarningPlanner().Due(s, T("16:30"), 10).Count, "after");
        }

        private static void PlannerMoved()
        {
            var p = new WarningPlanner();
            Equal(1, p.Due(Today("14:00-16:00"), T("13:55"), 10).Count, "first");
            Equal(0, p.Due(Today("14:00-17:00"), T("13:56"), 10).Count, "end extended: same key");
            Equal(1, p.Due(Today("15:00-17:00"), T("14:55"), 10).Count, "start moved: new warning");
        }

        private static void PlannerPersistence()
        {
            var p = new WarningPlanner();
            var day = new DateTime(2026, 10, 1);
            for (int i = 0; i < 25; i++)
            {
                var start = day.AddHours(i);
                p.Due(new[] { new BlackoutSeries(start, start.AddMinutes(30)) }, start.AddMinutes(-5), 10);
            }
            Equal(WarningPlanner.RememberCount, p.Notified.Count, "capped at 20");
            var copy = new WarningPlanner();
            copy.Load(p.Serialize() + ";garbage;;");
            Equal(p.Serialize(), copy.Serialize(), "round trip");
            copy.Prune(day.AddDays(3));
            Equal(0, copy.Notified.Count, "pruned after a day");
            Equal(0, copy.Due(new[] { new BlackoutSeries(day.AddHours(24), day.AddHours(25)) }, day.AddHours(23), 10).Count,
                "after prune nothing due early");
        }

        private static void NextAndCurrent()
        {
            var s = Today("06:00-09:00", "14:00-16:00", "19:00-21:00");
            Equal("2026-10-28 14:00-2026-10-28 16:00", SeriesBuilder.Next(s, T("11:45")).ToString(), "next");
            True(SeriesBuilder.Current(s, T("11:45")) == null, "none current");
            Equal("2026-10-28 19:00-2026-10-28 21:00", SeriesBuilder.Current(s, T("20:00")).ToString(), "current");
            True(SeriesBuilder.Next(s, T("21:00")) == null, "nothing after last");
        }

        // ---- settings and strings ----------------------------------------------

        private static void SettingsNormalize()
        {
            var s = new AppSettings();
            Equal("https://www.ztoe.com.ua/unhooking-search.php", s.ScheduleUrl, "default url");
            Equal("5.2", s.Queue, "default queue");
            Equal(10, s.LeadMinutes, "default lead");
            Equal(30, s.RefreshMinutes, "default refresh");
            True(!s.StartWithWindows, "autostart off");

            s.ScheduleUrl = "ftp://x";
            s.Queue = "abc";
            s.LeadMinutes = 0;
            s.RefreshMinutes = 1;
            s.KnownQueues = ";;";
            s.Normalize();
            Equal(AppSettings.DefaultUrl, s.ScheduleUrl, "url reset");
            Equal("5.2", s.Queue, "queue reset");
            Equal(10, s.LeadMinutes, "lead reset");
            Equal(30, s.RefreshMinutes, "refresh reset");
            Equal(12, s.KnownQueueList().Count, "queues reset");

            s.LeadMinutes = 120;
            s.RefreshMinutes = 5;
            s.Normalize();
            Equal(120, s.LeadMinutes, "max lead kept");
            Equal(5, s.RefreshMinutes, "min refresh kept");
        }

        private static void SettingsQueueList()
        {
            var s = new AppSettings();
            s.KnownQueues = "1.1;1.2";
            s.Queue = "5.2";
            Equal("1.1;1.2;5.2", string.Join(";", s.KnownQueueList().ToArray()), "selected queue kept in list");
            True(AppSettings.IsQueueLabel("6.2"), "6.2 valid");
            True(!AppSettings.IsQueueLabel("6"), "6 invalid");
            True(!AppSettings.IsQueueLabel("a.b"), "a.b invalid");
        }

        private static void StringsComplete()
        {
            foreach (var s in new[] { Strings.Ukrainian(), Strings.English() })
            {
                foreach (var prop in typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.PropertyType != typeof(string)) continue;
                    var value = (string)prop.GetValue(s, null);
                    True(!string.IsNullOrEmpty(value), prop.Name + " set (" + (s.IsUkrainian ? "uk" : "en") + ")");
                }
            }
            True(Strings.ForCulture(new CultureInfo("uk-UA")).IsUkrainian, "uk-UA picks Ukrainian");
            True(!Strings.ForCulture(new CultureInfo("en-US")).IsUkrainian, "en-US picks English");
            True(!Strings.ForCulture(new CultureInfo("ru-RU")).IsUkrainian, "others pick English");
        }

        private static void StringsFormat()
        {
            var uk = Strings.Ukrainian();
            Equal("2 год 15 хв", uk.Duration(TimeSpan.FromMinutes(135)), "uk duration");
            Equal("9 хв", uk.Duration(TimeSpan.FromMinutes(8.2)), "uk short duration rounds up");
            Equal("2 h 15 min", Strings.English().Duration(TimeSpan.FromMinutes(135)), "en duration");
            string text;
            var title = uk.Toast(new Warning(new BlackoutSeries(T("14:00"), T("16:00")), 10), "5.2", out text);
            Equal("Відключення о 14:00 (через 10 хв)", title, "toast title");
            Equal("До 16:00 · черга 5.2. Збережіть роботу.", text, "toast text");
            Equal("14:00–16:00", Strings.Range(new BlackoutSeries(T("14:00"), T("16:00"))), "range");
            Equal("23:00–01:30 (31.10)", Strings.Range(new BlackoutSeries(new DateTime(2026, 10, 30, 23, 0, 0),
                new DateTime(2026, 10, 31, 1, 30, 0))), "range over midnight");
        }

        private static void KyivClockWorks()
        {
            // 28.10.2026 is after the October DST change: Kyiv = UTC+2.
            Equal(new DateTime(2026, 10, 28, 14, 0, 0), KyivClock.FromUtc(new DateTime(2026, 10, 28, 12, 0, 0)), "winter time");
            Equal(new DateTime(2026, 7, 1, 15, 0, 0), KyivClock.FromUtc(new DateTime(2026, 7, 1, 12, 0, 0)), "summer time");
        }

        // ---- mini framework -----------------------------------------------------

        private static void Run(string name, Action test)
        {
            int before = failed;
            try
            {
                test();
            }
            catch (TestFailed)
            {
                // already counted
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine("  ! exception: " + ex.GetType().Name + ": " + ex.Message);
            }
            if (failed == before)
            {
                passed++;
                Console.WriteLine("PASS  " + name);
            }
            else
            {
                Console.WriteLine("FAIL  " + name);
            }
        }

        private static void True(bool condition, string what)
        {
            if (!condition)
            {
                failed++;
                Console.WriteLine("  ! expected true: " + what);
                throw new TestFailed();
            }
        }

        private static void Equal(object expected, object actual, string what)
        {
            if (!Equals(expected, actual))
            {
                failed++;
                Console.WriteLine("  ! " + what + ": expected <" + expected + "> but was <" + actual + ">");
                throw new TestFailed();
            }
        }

        private static void Throws(Action action)
        {
            try
            {
                action();
            }
            catch (ScheduleFormatException)
            {
                return;
            }
            failed++;
            Console.WriteLine("  ! expected ScheduleFormatException");
            throw new TestFailed();
        }

        private static string Join(IEnumerable<BlackoutSeries> series)
        {
            var parts = new List<string>();
            foreach (var s in series) parts.Add(s.ToString());
            return string.Join("|", parts.ToArray());
        }

        private static string FindFixtures()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6 && dir != null; i++)
            {
                var candidate = Path.Combine(Path.Combine(dir, "tests"), "Fixtures");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            return Path.Combine("tests", "Fixtures");
        }

        private sealed class TestFailed : Exception
        {
        }
    }
}
