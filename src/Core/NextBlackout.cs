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
            }
            else if (next != null)
            {
                v.Caption = s.NextBlackout;
                v.Range = s.RangeFrom(next, now);
                v.Countdown = string.Format(s.InTime, s.Duration(next.Start - now));
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
    }
}
