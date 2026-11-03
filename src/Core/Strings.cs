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
        /// <summary>Language names in the language drop-down (each in its own language).</summary>
        public const string UkrainianName = "Українська", EnglishName = "English";

        /// <summary>Title of the donations section in About; the same in both languages (Igor's choice).</summary>
        public const string DonationsTitle = "Donations";

        public string AppName { get; private set; }
        public string MenuOpen { get; private set; }
        public string MenuCheckNow { get; private set; }
        public string MenuClose { get; private set; }
        public string MenuAbout { get; private set; }
        public string AboutTitle { get; private set; }
        public string AboutDescription { get; private set; }
        public string AuthorLabel { get; private set; }
        public string ManualLabel { get; private set; }
        public string ManualLink { get; private set; }
        public string IssuesLabel { get; private set; }
        public string IssuesLink { get; private set; }
        public string VersionFormat { get; private set; }         // {0} = version
        public string ButtonClose { get; private set; }

        public string SettingsTitle { get; private set; }
        public string SectionSchedule { get; private set; }
        public string SectionNotifications { get; private set; }
        public string SectionSystem { get; private set; }
        public string LabelUrl { get; private set; }
        public string LabelQueue { get; private set; }
        public string QueueHint { get; private set; }
        public string QueueHelpLink { get; private set; }
        public string LabelLead { get; private set; }
        public string LeadHint { get; private set; }
        public string LabelRefresh { get; private set; }
        public string RefreshHint { get; private set; }
        public string Minutes { get; private set; }
        public string TestTitle { get; private set; }
        public string TestHint { get; private set; }
        public string ButtonTest { get; private set; }
        public string LabelLanguage { get; private set; }
        public string LanguageHint { get; private set; }
        public string LanguageAuto { get; private set; }
        public string StartWithWindows { get; private set; }
        public string SwitchOn { get; private set; }
        public string SwitchOff { get; private set; }
        public string DefaultsTitle { get; private set; }
        public string DefaultsHint { get; private set; }
        public string ButtonDefaults { get; private set; }
        public string ButtonCheckNow { get; private set; }
        public string ButtonSave { get; private set; }
        public string NotSavedYet { get; private set; }
        public string Saved { get; private set; }
        public string InvalidUrl { get; private set; }

        public string QueueHelpTitle { get; private set; }
        public string QueueHelpStep1Before { get; private set; }
        public string QueueHelpStep1Link { get; private set; }
        public string QueueHelpStep1After { get; private set; }
        public string QueueHelpStep2 { get; private set; }
        public string QueueHelpStep3 { get; private set; }
        public string QueueHelpStep4 { get; private set; }
        public string ButtonGotIt { get; private set; }

        public string DarkTheme { get; private set; }
        public string QueueBadge { get; private set; }            // {0} = queue
        public string NextBlackout { get; private set; }
        public string PowerOffNow { get; private set; }
        public string Tomorrow { get; private set; }
        public string InTime { get; private set; }                // {0} = duration
        public string UntilTime { get; private set; }             // {0} = time
        public string LegendOff { get; private set; }
        public string LegendOn { get; private set; }
        public string StripText { get; private set; }             // {0} = ranges
        public string StripNone { get; private set; }
        public string StripTextTomorrow { get; private set; }     // {0} = ranges
        public string StripNoneTomorrow { get; private set; }
        public string StripTomorrowNotPublished { get; private set; }
        public string TabToday { get; private set; }
        public string TabTomorrow { get; private set; }
        public string TomorrowNone { get; private set; }
        public string TomorrowNotPublished { get; private set; }
        public string SiteUpdated { get; private set; }           // {0} = time
        public string Checked { get; private set; }               // {0} = time
        public string NextCheck { get; private set; }             // {0} = time
        public string NotChecked { get; private set; }
        public string Loading { get; private set; }
        public string NoticeTitle { get; private set; }
        public string LinkSchedules { get; private set; }
        public string LinkFacebook { get; private set; }
        public string QueueMissing { get; private set; }          // {0} = queue
        public string NoGridForToday { get; private set; }
        public string FetchFailed { get; private set; }           // {0} = error, {1} = retry time
        public string FormatFailed { get; private set; }          // {0} = detail

        public string ToastTitle { get; private set; }            // {0} = start, {1} = minutes
        public string ToastText { get; private set; }             // {0} = end, {1} = queue
        public string StaleTitle { get; private set; }
        public string TestNext { get; private set; }              // {0} = range, {1} = duration
        public string TestNow { get; private set; }               // {0} = end
        public string TestNone { get; private set; }
        public string TestNoSchedule { get; private set; }
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

        /// <summary>The language chosen in Preferences (<see cref="AppSettings.Language"/>); "auto" follows Windows.</summary>
        public static Strings ForLanguage(string language)
        {
            if (language == AppSettings.LanguageUkrainian) return Ukrainian();
            if (language == AppSettings.LanguageEnglish) return English();
            return Current();
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
                                 "відключенням світла, щоб ви встигли завершити роботу і зберегти дані на ПК без ДБЖ.";
            s.AuthorLabel = "Автор";
            s.ManualLabel = "Інструкція";
            s.ManualLink = "Відкрити інструкцію";
            s.IssuesLabel = "Проблема чи побажання";
            s.IssuesLink = "Повідомити на GitHub";
            s.VersionFormat = "Версія {0}";
            s.ButtonClose = "Закрити";
            s.SettingsTitle = "Параметри";
            s.SectionSchedule = "Графік";
            s.SectionNotifications = "Сповіщення";
            s.SectionSystem = "Система";
            s.LabelUrl = "Адреса сторінки з графіком";
            s.LabelQueue = "Черга";
            s.QueueHint = "Черга і підчерга вашої адреси на сайті обленерго";
            s.QueueHelpLink = "Як дізнатися свою чергу";
            s.LabelLead = "Попереджати за";
            s.LeadHint = "Від 1 до 120 хвилин до початку відключення";
            s.LabelRefresh = "Перевіряти графік кожні";
            s.RefreshHint = "Від 5 до 240 хвилин";
            s.Minutes = "хв";
            s.TestTitle = "Перевірити сповіщення";
            s.TestHint = "Покаже найближче відключення з позначкою «Тест». Справжні попередження це не змінює.";
            s.ButtonTest = "Надіслати тестове";
            s.LabelLanguage = "Мова інтерфейсу";
            s.LanguageHint = "Спершу - як у Windows";
            s.LanguageAuto = "Як у Windows";
            s.StartWithWindows = "Запускати разом з Windows";
            s.SwitchOn = "Увімк.";
            s.SwitchOff = "Вимк.";
            s.DefaultsTitle = "Типові значення";
            s.DefaultsHint = "Повертає стандартні значення полів; застосуються після «Зберегти»";
            s.ButtonDefaults = "Відновити";
            s.ButtonCheckNow = "Перевірити зараз";
            s.ButtonSave = "Зберегти";
            s.NotSavedYet = "Не збережено";
            s.Saved = "Збережено";
            s.InvalidUrl = "Введіть адресу, що починається з http:// або https://";
            s.QueueHelpTitle = "Як дізнатися свою чергу";
            s.QueueHelpStep1Before = "Відкрийте ";
            s.QueueHelpStep1Link = "сторінку пошуку черги на сайті обленерго";
            s.QueueHelpStep1After = ".";
            s.QueueHelpStep2 = "Оберіть свій РЕМ, населений пункт і вулицю.";
            s.QueueHelpStep3 = "У таблиці знайдіть свій будинок і подивіться стовпці «Черга» і «Підчерга». " +
                               "Наприклад, черга 5 і підчерга 2 - це 5.2.";
            s.QueueHelpStep4 = "Виберіть це значення у полі «Черга» тут, у вікні «Параметри».";
            s.ButtonGotIt = "Зрозуміло";
            s.DarkTheme = "Темна тема";
            s.QueueBadge = "Черга {0}";
            s.NextBlackout = "Наступне відключення";
            s.PowerOffNow = "Зараз світла немає";
            s.Tomorrow = "завтра";
            s.InTime = "через {0}";
            s.UntilTime = "до {0}";
            s.LegendOff = "без світла";
            s.LegendOn = "є світло";
            s.StripText = "Сьогодні без світла: {0}";
            s.StripNone = "Сьогодні відключень не заплановано";
            s.StripTextTomorrow = "Завтра без світла: {0}";
            s.StripNoneTomorrow = "Завтра відключень не заплановано";
            s.StripTomorrowNotPublished = "Графіка на завтра ще немає";
            s.TabToday = "Сьогодні";
            s.TabTomorrow = "Завтра";
            s.TomorrowNone = "завтра відключень не заплановано";
            s.TomorrowNotPublished = "графіка на завтра ще немає";
            s.SiteUpdated = "Сайт оновлено {0}";
            s.Checked = "перевірено {0}";
            s.NextCheck = "наступна перевірка {0}";
            s.NotChecked = "Ще не перевірялося";
            s.Loading = "Завантаження графіка…";
            s.NoticeTitle = "Повідомлення сайту";
            s.LinkSchedules = "Графіки";
            s.LinkFacebook = "Житомиробленерго на Facebook";
            s.QueueMissing = "Черги {0} немає на сторінці";
            s.NoGridForToday = "На сайті немає графіка на сьогодні";
            s.FetchFailed = "Не вдалося отримати графік ({0}). Повтор о {1}";
            s.FormatFailed = "Не вдалося розпізнати графік на сторінці ({0})";
            s.ToastTitle = "Відключення о {0} (через {1} хв)";
            s.ToastText = "До {0} · черга {1}. Збережіть роботу.";
            s.StaleTitle = "Графік може бути застарілим";
            s.TestNext = "Тест: відключення {0} (через {1})";
            s.TestNow = "Тест: світла немає до {0}";
            s.TestNone = "Тест: відключень не заплановано";
            s.TestNoSchedule = "Тест: графік ще не завантажено";
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
            s.AboutDescription = "A small Windows tray app. It warns you before a scheduled power outage in Ukraine, " +
                                 "so you can finish and save your work on a PC without a UPS.";
            s.AuthorLabel = "Author";
            s.ManualLabel = "Manual";
            s.ManualLink = "Open the manual";
            s.IssuesLabel = "Problem or suggestion";
            s.IssuesLink = "Report on GitHub";
            s.VersionFormat = "Version {0}";
            s.ButtonClose = "Close";
            s.SettingsTitle = "Preferences";
            s.SectionSchedule = "Schedule";
            s.SectionNotifications = "Notifications";
            s.SectionSystem = "System";
            s.LabelUrl = "Schedule page URL";
            s.LabelQueue = "Queue";
            s.QueueHint = "Queue and sub-queue of your address on the oblenergo website";
            s.QueueHelpLink = "How to find your queue";
            s.LabelLead = "Warn before";
            s.LeadHint = "1 to 120 minutes before the outage starts";
            s.LabelRefresh = "Check the schedule every";
            s.RefreshHint = "5 to 240 minutes";
            s.Minutes = "min";
            s.TestTitle = "Check notifications";
            s.TestHint = "Shows the nearest blackout marked \"Test\". Real warnings are not affected.";
            s.ButtonTest = "Send test";
            s.LabelLanguage = "Interface language";
            s.LanguageHint = "At first, the same as Windows";
            s.LanguageAuto = "Same as Windows";
            s.StartWithWindows = "Start with Windows";
            s.SwitchOn = "On";
            s.SwitchOff = "Off";
            s.DefaultsTitle = "Default values";
            s.DefaultsHint = "Puts the standard values back; they apply after Save";
            s.ButtonDefaults = "Restore";
            s.ButtonCheckNow = "Check now";
            s.ButtonSave = "Save";
            s.NotSavedYet = "Not saved yet";
            s.Saved = "Saved";
            s.InvalidUrl = "Enter an address starting with http:// or https://";
            s.QueueHelpTitle = "How to find your queue";
            s.QueueHelpStep1Before = "Open the ";
            s.QueueHelpStep1Link = "queue search page on the oblenergo website";
            s.QueueHelpStep1After = ".";
            s.QueueHelpStep2 = "Choose your district office (РЕМ), town and street.";
            s.QueueHelpStep3 = "Find your building in the table and look at the «Черга» (queue) and «Підчерга» (sub-queue) " +
                               "columns. For example, queue 5 and sub-queue 2 is 5.2.";
            s.QueueHelpStep4 = "Choose that value in the Queue field here, in Preferences.";
            s.ButtonGotIt = "Got it";
            s.DarkTheme = "Dark theme";
            s.QueueBadge = "Queue {0}";
            s.NextBlackout = "Next blackout";
            s.PowerOffNow = "Power is off now";
            s.Tomorrow = "tomorrow";
            s.InTime = "in {0}";
            s.UntilTime = "until {0}";
            s.LegendOff = "power off";
            s.LegendOn = "power on";
            s.StripText = "Today without power: {0}";
            s.StripNone = "No blackouts scheduled today";
            s.StripTextTomorrow = "Tomorrow without power: {0}";
            s.StripNoneTomorrow = "No blackouts scheduled tomorrow";
            s.StripTomorrowNotPublished = "No schedule for tomorrow yet";
            s.TabToday = "Today";
            s.TabTomorrow = "Tomorrow";
            s.TomorrowNone = "no blackouts scheduled tomorrow";
            s.TomorrowNotPublished = "no schedule for tomorrow yet";
            s.SiteUpdated = "Site updated {0}";
            s.Checked = "checked {0}";
            s.NextCheck = "next check {0}";
            s.NotChecked = "Not checked yet";
            s.Loading = "Loading the schedule…";
            s.NoticeTitle = "Site notice";
            s.LinkSchedules = "Schedules";
            s.LinkFacebook = "Zhytomyroblenergo on Facebook";
            s.QueueMissing = "Queue {0} is not on the page";
            s.NoGridForToday = "The site has no schedule for today";
            s.FetchFailed = "Could not get the schedule ({0}). Retrying at {1}";
            s.FormatFailed = "Could not read the schedule on the page ({0})";
            s.ToastTitle = "Power off at {0} (in {1} min)";
            s.ToastText = "Until {0} · queue {1}. Save your work.";
            s.StaleTitle = "Schedule may be out of date";
            s.TestNext = "Test: blackout {0} (in {1})";
            s.TestNow = "Test: power off until {0}";
            s.TestNone = "Test: no blackouts scheduled";
            s.TestNoSchedule = "Test: schedule not loaded yet";
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

        /// <summary>
        /// Text for the day strip's accessible name: today's outages as ranges ("09:00–12:00, 18:00–22:00"),
        /// read from the 48 half-hour slots.
        /// </summary>
        public string StripDescription(bool[] slots)
        {
            return StripDescription(slots, false);
        }

        /// <summary>The same for today's or tomorrow's strip (the main window's two tabs).</summary>
        public string StripDescription(bool[] slots, bool tomorrow)
        {
            if (slots == null) return tomorrow ? StripTomorrowNotPublished : NoGridForToday;
            var ranges = new System.Collections.Generic.List<string>();
            int i = 0;
            while (i < slots.Length)
            {
                if (!slots[i])
                {
                    i++;
                    continue;
                }
                int start = i;
                while (i < slots.Length && slots[i]) i++;
                ranges.Add(SlotTime(start) + "–" + SlotTime(i));
            }
            if (ranges.Count == 0) return tomorrow ? StripNoneTomorrow : StripNone;
            return string.Format(tomorrow ? StripTextTomorrow : StripText, string.Join(", ", ranges.ToArray()));
        }

        private static string SlotTime(int slot)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", slot / 2, slot % 2 * 30);
        }

        public string Toast(Warning w, string queue, out string text)
        {
            text = string.Format(CultureInfo.InvariantCulture, ToastText, EndTime(w.Series), queue);
            return string.Format(CultureInfo.InvariantCulture, ToastTitle, Time(w.Series.Start), w.MinutesLeft);
        }
    }
}
