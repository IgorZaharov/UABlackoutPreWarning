// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace UABlackoutPreWarning.Core
{
    /// <summary>Number of half-hour slots in one schedule day.</summary>
    public static class Slots
    {
        public const int PerDay = 48;
        public const int Minutes = 30;
    }

    /// <summary>One published day of the schedule grid.</summary>
    public sealed class DaySchedule
    {
        private readonly Dictionary<string, bool[]> rows = new Dictionary<string, bool[]>(StringComparer.Ordinal);
        private readonly List<string> labels = new List<string>();

        public DaySchedule(DateTime date)
        {
            Date = date.Date;
        }

        /// <summary>Calendar date of this grid (Kyiv local date).</summary>
        public DateTime Date { get; private set; }

        /// <summary>Row labels in page order, e.g. "1.1" ... "6.2".</summary>
        public IList<string> Labels
        {
            get { return labels.AsReadOnly(); }
        }

        /// <summary>Adds a row; <paramref name="off"/> has 48 entries, true = no power.</summary>
        public void AddRow(string label, bool[] off)
        {
            if (label == null) throw new ArgumentNullException("label");
            if (off == null || off.Length != Slots.PerDay) throw new ArgumentException("A row needs 48 slots.", "off");
            if (!rows.ContainsKey(label)) labels.Add(label);
            rows[label] = (bool[])off.Clone();
        }

        /// <summary>Returns the 48 off-flags for a row, or null when the row is absent.</summary>
        public bool[] GetRow(string label)
        {
            bool[] off;
            if (label != null && rows.TryGetValue(label, out off)) return (bool[])off.Clone();
            return null;
        }
    }

    /// <summary>Everything the app reads from the schedule page.</summary>
    public sealed class ParsedPage
    {
        public ParsedPage()
        {
            Days = new List<DaySchedule>();
            NoticeLines = new List<string>();
        }

        public List<DaySchedule> Days { get; private set; }

        /// <summary>"Дата оновлення інформації" value, when present.</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Lines of the announcement at the top of the page.</summary>
        public List<string> NoticeLines { get; private set; }

        /// <summary>Row labels in page order, merged across all days.</summary>
        public List<string> QueueLabels()
        {
            var result = new List<string>();
            foreach (var day in Days)
            {
                foreach (var label in day.Labels)
                {
                    if (!result.Contains(label)) result.Add(label);
                }
            }
            return result;
        }
    }

    /// <summary>A continuous run of outage slots.</summary>
    public sealed class BlackoutSeries
    {
        public BlackoutSeries(DateTime start, DateTime end)
        {
            if (end <= start) throw new ArgumentException("End must be after start.");
            Start = start;
            End = end;
        }

        /// <summary>Start, Kyiv wall-clock time.</summary>
        public DateTime Start { get; private set; }

        /// <summary>End (exclusive), Kyiv wall-clock time.</summary>
        public DateTime End { get; private set; }

        /// <summary>Identity of a series for "warn once": its start time.</summary>
        public string Key
        {
            get { return Start.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture); }
        }

        public bool Contains(DateTime moment)
        {
            return moment >= Start && moment < End;
        }

        public override string ToString()
        {
            return Start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "-" +
                   End.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>A notification that is due now.</summary>
    public sealed class Warning
    {
        public Warning(BlackoutSeries series, int minutesLeft)
        {
            Series = series;
            MinutesLeft = minutesLeft;
        }

        public BlackoutSeries Series { get; private set; }

        /// <summary>Whole minutes until the outage starts (rounded up, at least 1).</summary>
        public int MinutesLeft { get; private set; }
    }

    /// <summary>The page did not contain a recognisable schedule grid.</summary>
    public sealed class ScheduleFormatException : Exception
    {
        public ScheduleFormatException(string message) : base(message)
        {
        }
    }
}
