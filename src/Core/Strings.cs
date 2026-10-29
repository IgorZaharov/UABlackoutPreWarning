// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Globalization;

namespace UABlackoutPreWarning.Core
{
    /// <summary>
    /// UI text in Ukrainian and English. Kept in code (not .resx) so the
    /// application stays a single exe without satellite assemblies.
    /// </summary>
    public sealed class Strings
    {
        public string AppName { get; private set; }
        public string MenuOpen { get; private set; }
        public string MenuCheckNow { get; private set; }
        public string MenuClose { get; private set; }
        public string MenuAbout { get; private set; }
        public string AboutTitle { get; private set; }
        public string AboutDescription { get; private set; }
        public string AuthorLabel { get; private set; }
        public string ManualLabel { get; private set; }
        public string IssuesLabel { get; private set; }
        public string SettingsTitle { get; private set; }
        public string VersionFormat { get; private set; }         // {0} = version
        public string ButtonOk { get; private set; }

        public string GroupSettings { get; private set; }
        public string LabelUrl { get; private set; }
        public string LabelQueue { get; private set; }
        public string LabelLead { get; private set; }
        public string LabelRefresh { get; private set; }
        public string Minutes { get; private set; }
        public string StartWithWindows { get; private set; }
        public string ButtonCheckNow { get; private set; }
        public string ButtonSave { get; private set; }
        public string ButtonDefaults { get; private set; }
        public string DefaultsHint { get; private set; }
        public string NotSavedYet { get; private set; }
        public string ThemeToDark { get; private set; }
        public string ThemeToLight { get; private set; }

        public string GroupStatus { get; private set; }
        public string QueueBadge { get; private set; }            // {0} = queue
        public string NextBlackout { get; private set; }
        public string PowerOffNow { get; private set; }
        public string NoBlackouts { get; private set; }
        public string Tomorrow { get; private set; }
        public string InTime { get; private set; }                // {0} = duration
        public string UntilTime { get; private set; }             // {0} = time
        public string TodayLabel { get; private set; }            // {0} = date
        public string Legend { get; private set; }
        public string SiteUpdated { get; private set; }           // {0} = time
        public string Checked { get; private set; }               // {0} = time
        public string NextCheck { get; private set; }             // {0} = time
        public string NotChecked { get; private set; }
        public string Loading { get; private set; }
        public string SiteNotice { get; private set; }            // {0} = text
        public string QueueMissing { get; private set; }          // {0} = queue
        public string NoGridForToday { get; private set; }
        public string FetchFailed { get; private set; }           // {0} = error, {1} = retry time
        public string FormatFailed { get; private set; }          // {0} = detail

        public string InvalidUrl { get; private set; }
        public string Saved { get; private set; }

        public string ToastTitle { get; private set; }            // {0} = start, {1} = minutes
        public string ToastText { get; private set; }             // {0} = end, {1} = queue
        public string StaleTitle { get; private set; }
        public string StaleText { get; private set; }             // {0} = last good update or "-"
        public string TooltipNext { get; private set; }           // {0} = start-end
        public string TooltipNow { get; private set; }            // {0} = end
        public string TooltipNone { get; private set; }
        public string TooltipError { get; private set; }

        public string HoursMinutes { get; private set; }          // {0} h {1} min
        public string MinutesOnly { get; private set; }           // {0} min
        public string AlreadyRunning { get; private set; }

        public bool IsUkrainian { get; private set; }

        /// <summary>Ukrainian when Windows' display language is Ukrainian, otherwise English.</summary>
        public static Strings ForCulture(CultureInfo culture)
        {
            if (culture != null && string.Equals(culture.TwoLetterISOLanguageName, "uk", StringComparison.OrdinalIgnoreCase))
            {
                return Ukrainian();
            }
            return English();
        }

        public static Strings Current()
        {
            return ForCulture(CultureInfo.CurrentUICulture);
        }

        public static Strings Ukrainian()
        {
            var s = new Strings();
            s.IsUkrainian = true;
            s.AppName = "UA Blackout Pre-Warning";
            s.MenuOpen = "Відкрити";
            s.MenuCheckNow = "Перевірити зараз";
            s.MenuClose = "Закрити програму";
            s.MenuAbout = "Про програму";
            s.AboutTitle = "Про програму";
            s.AboutDescription = "Невелика програма для системного трею Windows. Надсилає сповіщення перед плановим " +
                                 "відключенням світла в Україні, щоб ви встигли завершити роботу і зберегти дані на ПК без ДБЖ.";
            s.AuthorLabel = "Автор:";
            s.ManualLabel = "Інструкція:";
            s.IssuesLabel = "Повідомити про проблему:";
            s.SettingsTitle = "Параметри";
            s.VersionFormat = "Версія {0}";
            s.ButtonOk = "OK";
            s.GroupSettings = "Налаштування";
            s.LabelUrl = "Адреса сторінки з графіком";
            s.LabelQueue = "Черга";
            s.LabelLead = "Попереджати за";
            s.LabelRefresh = "Перевіряти кожні";
            s.Minutes = "хв";
            s.StartWithWindows = "Запускати разом з Windows";
            s.ButtonCheckNow = "Перевірити зараз";
            s.ButtonSave = "Зберегти";
            s.ButtonDefaults = "За замовчуванням";
            s.DefaultsHint = "Повернути стандартні значення всіх полів. Щоб застосувати їх, натисніть «Зберегти».";
            s.NotSavedYet = "Не збережено";
            s.ThemeToDark = "Увімкнути темну тему";
            s.ThemeToLight = "Увімкнути світлу тему";
            s.GroupStatus = "Стан";
            s.QueueBadge = "Черга {0}";
            s.NextBlackout = "Наступне відключення";
            s.PowerOffNow = "Зараз світла немає";
            s.NoBlackouts = "Відключень не заплановано";
            s.Tomorrow = "завтра";
            s.InTime = "через {0}";
            s.UntilTime = "до {0}";
            s.TodayLabel = "Сьогодні, {0}";
            s.Legend = "червоне — світла немає";
            s.SiteUpdated = "Сайт оновлено {0}";
            s.Checked = "перевірено {0}";
            s.NextCheck = "наступна перевірка {0}";
            s.NotChecked = "Ще не перевірялося";
            s.Loading = "Завантаження графіка…";
            s.SiteNotice = "Повідомлення сайту: {0}";
            s.QueueMissing = "Черги {0} немає на сторінці";
            s.NoGridForToday = "На сайті немає графіка на сьогодні";
            s.FetchFailed = "Не вдалося отримати графік ({0}). Повтор о {1}";
            s.FormatFailed = "Не вдалося розпізнати графік на сторінці ({0})";
            s.InvalidUrl = "Введіть адресу, що починається з http:// або https://";
            s.Saved = "Збережено";
            s.ToastTitle = "Відключення о {0} (через {1} хв)";
            s.ToastText = "До {0} · черга {1}. Збережіть роботу.";
            s.StaleTitle = "Графік може бути застарілим";
            s.StaleText = "Не вдається завантажити сайт. Останнє оновлення: {0}.";
            s.TooltipNext = "Наступне відключення {0}";
            s.TooltipNow = "Світла немає до {0}";
            s.TooltipNone = "Відключень не заплановано";
            s.TooltipError = "Графік недоступний";
            s.HoursMinutes = "{0} год {1} хв";
            s.MinutesOnly = "{0} хв";
            s.AlreadyRunning = "Програма вже працює";
            return s;
        }

        public static Strings English()
        {
            var s = new Strings();
            s.IsUkrainian = false;
            s.AppName = "UA Blackout Pre-Warning";
            s.MenuOpen = "Open";
            s.MenuCheckNow = "Check now";
            s.MenuClose = "Close Application";
            s.MenuAbout = "About";
            s.AboutTitle = "About";
            s.AboutDescription = "Small tool to run in Windows TaskTray and send notification balloon before planned " +
                                 "blackout in UA to finish and save data on PC without UPS.";
            s.AuthorLabel = "Author:";
            s.ManualLabel = "Manual:";
            s.IssuesLabel = "Report issues:";
            s.SettingsTitle = "Preferences";
            s.VersionFormat = "Version {0}";
            s.ButtonOk = "OK";
            s.GroupSettings = "Settings";
            s.LabelUrl = "Schedule page URL";
            s.LabelQueue = "Queue";
            s.LabelLead = "Warn before";
            s.LabelRefresh = "Check every";
            s.Minutes = "min";
            s.StartWithWindows = "Start with Windows";
            s.ButtonCheckNow = "Check now";
            s.ButtonSave = "Save";
            s.ButtonDefaults = "Restore defaults";
            s.DefaultsHint = "Put the standard values back into all fields. Press Save to apply them.";
            s.NotSavedYet = "Not saved yet";
            s.ThemeToDark = "Switch to dark theme";
            s.ThemeToLight = "Switch to light theme";
            s.GroupStatus = "Status";
            s.QueueBadge = "Queue {0}";
            s.NextBlackout = "Next blackout";
            s.PowerOffNow = "Power is off now";
            s.NoBlackouts = "No blackouts scheduled";
            s.Tomorrow = "tomorrow";
            s.InTime = "in {0}";
            s.UntilTime = "until {0}";
            s.TodayLabel = "Today, {0}";
            s.Legend = "red = power off";
            s.SiteUpdated = "Site updated {0}";
            s.Checked = "checked {0}";
            s.NextCheck = "next check {0}";
            s.NotChecked = "Not checked yet";
            s.Loading = "Loading the schedule…";
            s.SiteNotice = "Site notice: {0}";
            s.QueueMissing = "Queue {0} is not on the page";
            s.NoGridForToday = "The site has no schedule for today";
            s.FetchFailed = "Could not get the schedule ({0}). Retrying at {1}";
            s.FormatFailed = "Could not read the schedule on the page ({0})";
            s.InvalidUrl = "Enter an address starting with http:// or https://";
            s.Saved = "Saved";
            s.ToastTitle = "Power off at {0} (in {1} min)";
            s.ToastText = "Until {0} · queue {1}. Save your work.";
            s.StaleTitle = "Schedule may be out of date";
            s.StaleText = "The site cannot be reached. Last update: {0}.";
            s.TooltipNext = "Next blackout {0}";
            s.TooltipNow = "Power off until {0}";
            s.TooltipNone = "No blackouts scheduled";
            s.TooltipError = "Schedule unavailable";
            s.HoursMinutes = "{0} h {1} min";
            s.MinutesOnly = "{0} min";
            s.AlreadyRunning = "The application is already running";
            return s;
        }

        // ---- formatting helpers -------------------------------------------------

        public static string Time(DateTime t)
        {
            return t.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>End time of a series; midnight shows as 24:00, a later day adds its date.</summary>
        public static string EndTime(BlackoutSeries s)
        {
            if (s.End.TimeOfDay == TimeSpan.Zero && s.End.Date == s.Start.Date.AddDays(1)) return "24:00";
            if (s.End.Date != s.Start.Date)
            {
                return s.End.ToString("HH:mm (dd.MM)", CultureInfo.InvariantCulture);
            }
            return Time(s.End);
        }

        public static string Range(BlackoutSeries s)
        {
            return Time(s.Start) + "–" + EndTime(s);
        }

        /// <summary>
        /// Range as seen from <paramref name="now"/>: plain for today, "завтра 09:00–12:00"
        /// for tomorrow, "30.10 09:00–12:00" for a later day (the site can publish two days).
        /// </summary>
        public string RangeFrom(BlackoutSeries s, DateTime now)
        {
            if (s.Start.Date == now.Date) return Range(s);
            if (s.Start.Date == now.Date.AddDays(1)) return Tomorrow + " " + Range(s);
            return s.Start.ToString("dd.MM", CultureInfo.InvariantCulture) + " " + Range(s);
        }

        public static string Date(DateTime d)
        {
            return d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }

        public string Duration(TimeSpan span)
        {
            var total = (int)Math.Ceiling(span.TotalMinutes - 1e-9);
            if (total < 1) total = 1;
            if (total < 60) return string.Format(CultureInfo.InvariantCulture, MinutesOnly, total);
            return string.Format(CultureInfo.InvariantCulture, HoursMinutes, total / 60, total % 60);
        }

        public string Toast(Warning w, string queue, out string text)
        {
            text = string.Format(CultureInfo.InvariantCulture, ToastText, EndTime(w.Series), queue);
            return string.Format(CultureInfo.InvariantCulture, ToastTitle, Time(w.Series.Start), w.MinutesLeft);
        }
    }
}
