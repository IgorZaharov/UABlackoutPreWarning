// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

namespace UABlackoutPreWarning.Core
{
    /// <summary>The "next blackout" lines of the main window.</summary>
    public sealed class NextBlackoutView
    {
        public string Caption;          // "Наступне відключення" / "Зараз світла немає"
        public string Range;            // "завтра 09:00–12:00", "19:00–22:00" or "--:--"
        public string Countdown;        // "через 10 год 30 хв", "до 22:00" or a note
        public bool CountdownIsNote;    // true: a plain note ("графіка на завтра ще немає"), not a warning

        /// <summary>True while an outage is in progress (the tray and window icons show the crossed-out bulb).</summary>
        public bool PowerOff;

        /// <summary>
        /// 0..1 for the bar under the time: the elapsed share of the current outage, or of the time from the
        /// previous outage's end (or midnight) to the next one's start. Negative when nothing is ahead.
        /// </summary>
        public double Progress = -1;

        /// <summary>True when <see cref="Progress"/> measures an outage in progress.</summary>
        public bool ProgressIsOutage;
    }

    /// <summary>
    /// Works out what the main window says about the next blackout, across every published day: in the
    /// evening the site adds tomorrow's grid, so the countdown runs into tomorrow. With nothing ahead the
    /// time is "--:--" and the note says whether tomorrow's grid is missing or has no outages.
    /// </summary>
    public static class NextBlackout
    {
        public const string UnknownTime = "--:--";

        public static NextBlackoutView Describe(Strings s, ParsedPage page, string queue, System.DateTime now)
        {
            var series = SeriesBuilder.Build(page.Days, queue);
            var current = SeriesBuilder.Current(series, now);
            var next = SeriesBuilder.Next(series, now);
            var v = new NextBlackoutView();
            if (current != null)
            {
                v.Caption = s.PowerOffNow;
                v.Range = Strings.Range(current);
                v.Countdown = string.Format(s.UntilTime, Strings.EndTime(current));
                v.PowerOff = true;
                v.Progress = Share(current.Start, current.End, now);
                v.ProgressIsOutage = true;
            }
            else if (next != null)
            {
                v.Caption = s.NextBlackout;
                v.Range = s.RangeFrom(next, now);
                v.Countdown = string.Format(s.InTime, s.Duration(next.Start - now));
                var from = now.Date;
                foreach (var item in series)
                {
                    if (item.End <= now && item.End > from) from = item.End;
                }
                v.Progress = Share(from, next.Start, now);
            }
            else
            {
                var tomorrow = page.FindDay(now.Date.AddDays(1));
                v.Caption = s.NextBlackout;
                v.Range = UnknownTime;
                v.Countdown = tomorrow != null && tomorrow.GetRow(queue) != null ? s.TomorrowNone : s.TomorrowNotPublished;
                v.CountdownIsNote = true;
            }
            return v;
        }

        private static double Share(System.DateTime from, System.DateTime to, System.DateTime now)
        {
            double total = (to - from).TotalMinutes;
            if (total <= 0) return 1;
            return System.Math.Max(0, System.Math.Min(1, (now - from).TotalMinutes / total));
        }
    }
}
