// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UABlackoutPreWarning.Core
{
    /// <summary>Turns the grid of one queue into continuous outage periods.</summary>
    public static class SeriesBuilder
    {
        /// <summary>
        /// Builds blackout series for <paramref name="queue"/> across all published days.
        /// Consecutive red slots merge; a series ending at 24:00 joins one starting at
        /// 00:00 on the next published day.
        /// </summary>
        public static List<BlackoutSeries> Build(IEnumerable<DaySchedule> days, string queue)
        {
            var spans = new List<KeyValuePair<DateTime, DateTime>>();
            foreach (var day in days)
            {
                var off = day.GetRow(queue);
                if (off == null) continue;
                int i = 0;
                while (i < off.Length)
                {
                    if (!off[i])
                    {
                        i++;
                        continue;
                    }
                    int start = i;
                    while (i < off.Length && off[i]) i++;
                    spans.Add(new KeyValuePair<DateTime, DateTime>(
                        day.Date.AddMinutes(start * Slots.Minutes),
                        day.Date.AddMinutes(i * Slots.Minutes)));
                }
            }

            spans.Sort(delegate(KeyValuePair<DateTime, DateTime> a, KeyValuePair<DateTime, DateTime> b)
            {
                return a.Key.CompareTo(b.Key);
            });

            var result = new List<BlackoutSeries>();
            DateTime? curStart = null;
            DateTime curEnd = DateTime.MinValue;
            foreach (var s in spans)
            {
                if (curStart != null && s.Key <= curEnd)
                {
                    if (s.Value > curEnd) curEnd = s.Value;
                    continue;
                }
                if (curStart != null) result.Add(new BlackoutSeries(curStart.Value, curEnd));
                curStart = s.Key;
                curEnd = s.Value;
            }
            if (curStart != null) result.Add(new BlackoutSeries(curStart.Value, curEnd));
            return result;
        }

        /// <summary>The series in progress at <paramref name="now"/>, or null.</summary>
        public static BlackoutSeries Current(IEnumerable<BlackoutSeries> series, DateTime now)
        {
            foreach (var s in series)
            {
                if (s.Contains(now)) return s;
            }
            return null;
        }

        /// <summary>The first series that starts after <paramref name="now"/>, or null.</summary>
        public static BlackoutSeries Next(IEnumerable<BlackoutSeries> series, DateTime now)
        {
            BlackoutSeries best = null;
            foreach (var s in series)
            {
                if (s.Start > now && (best == null || s.Start < best.Start)) best = s;
            }
            return best;
        }
    }

    /// <summary>
    /// Decides which warnings are due and remembers which were already shown,
    /// so each outage is announced once even across restarts.
    /// </summary>
    public sealed class WarningPlanner
    {
        public const int RememberCount = 20;

        private readonly List<string> notified = new List<string>();

        /// <summary>Keys of series already announced, oldest first.</summary>
        public IList<string> Notified
        {
            get { return notified.AsReadOnly(); }
        }

        /// <summary>Returns warnings due at <paramref name="now"/> and marks them as shown.</summary>
        public List<Warning> Due(IEnumerable<BlackoutSeries> series, DateTime now, int leadMinutes)
        {
            var due = new List<Warning>();
            foreach (var s in series)
            {
                if (now >= s.Start) continue;                       // started or over: no warning
                if (now < s.Start.AddMinutes(-leadMinutes)) continue; // too early
                if (notified.Contains(s.Key)) continue;               // already announced
                var minutes = (int)Math.Ceiling((s.Start - now).TotalMinutes - 1e-9);
                if (minutes < 1) minutes = 1;
                due.Add(new Warning(s, minutes));
                Remember(s.Key);
            }
            return due;
        }

        /// <summary>Forgets keys of series that started more than a day before <paramref name="now"/>.</summary>
        public void Prune(DateTime now)
        {
            notified.RemoveAll(delegate(string key)
            {
                DateTime start;
                if (!DateTime.TryParseExact(key, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out start))
                {
                    return true;
                }
                return start < now.AddHours(-24);
            });
        }

        /// <summary>Semicolon-separated keys, for the registry.</summary>
        public string Serialize()
        {
            return string.Join(";", notified.ToArray());
        }

        /// <summary>Restores keys written by <see cref="Serialize"/>; ignores junk.</summary>
        public void Load(string value)
        {
            notified.Clear();
            if (string.IsNullOrEmpty(value)) return;
            foreach (var part in value.Split(';'))
            {
                var key = part.Trim();
                DateTime ignored;
                if (key.Length > 0 && DateTime.TryParseExact(key, "yyyy-MM-dd'T'HH:mm",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out ignored))
                {
                    Remember(key);
                }
            }
        }

        private void Remember(string key)
        {
            notified.Remove(key);
            notified.Add(key);
            while (notified.Count > RememberCount) notified.RemoveAt(0);
        }
    }

    /// <summary>Kyiv wall-clock time, whatever the PC's own time zone is.</summary>
    public static class KyivClock
    {
        private static readonly TimeZoneInfo Zone = FindZone();

        public static DateTime Now
        {
            get { return FromUtc(DateTime.UtcNow); }
        }

        public static DateTime FromUtc(DateTime utc)
        {
            if (Zone == null) return utc.ToLocalTime();
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
        }

        private static TimeZoneInfo FindZone()
        {
            foreach (var id in new[] { "FLE Standard Time", "Europe/Kyiv", "Europe/Kiev" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }
            return null;
        }
    }

    /// <summary>Decodes the downloaded page bytes (the site uses windows-1251).</summary>
    public static class PageDecoder
    {
        public static string Decode(byte[] bytes, string contentType)
        {
            var encoding = FromContentType(contentType) ?? FromMeta(bytes) ?? GetEncoding("windows-1251");
            return encoding.GetString(bytes);
        }

        private static Encoding FromContentType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType)) return null;
            var idx = contentType.IndexOf("charset=", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var name = contentType.Substring(idx + 8).Trim().Trim('"', '\'', ';', ' ');
            var semi = name.IndexOf(';');
            if (semi >= 0) name = name.Substring(0, semi);
            return GetEncoding(name);
        }

        private static Encoding FromMeta(byte[] bytes)
        {
            var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 4096));
            var idx = head.IndexOf("charset=", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var sb = new StringBuilder();
            for (int i = idx + 8; i < head.Length; i++)
            {
                var ch = head[i];
                if (ch == '"' || ch == '\'')
                {
                    if (sb.Length == 0) continue;
                    break;
                }
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_') sb.Append(ch);
                else break;
            }
            return GetEncoding(sb.ToString());
        }

        private static Encoding GetEncoding(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            try
            {
                return Encoding.GetEncoding(name);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
