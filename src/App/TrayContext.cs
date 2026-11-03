// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using UABlackoutPreWarning.Core;

namespace UABlackoutPreWarning
{
    /// <summary>What the window's status area shows.</summary>
    internal sealed class StatusView
    {
        public string QueueBadge;
        public bool QueueMissing;
        public string Caption;          // "Next blackout" / "Power is off now"
        public string Range;            // "14:00–16:00", "tomorrow 09:00–12:00" or "--:--"
        public string Countdown;        // "in 2 h 15 min" / "until 16:00" / a note
        public bool CountdownIsNote;    // the countdown is a note ("no schedule for tomorrow yet"), not a warning
        public DateTime Today;
        public bool[] TodaySlots;       // null when the site has no grid for today
        public bool[] TomorrowSlots;    // null until the site publishes tomorrow's grid (in the evening)
        public bool PowerOff;           // an outage is in progress
        public double Progress = -1;    // 0..1 for the line under the time; negative hides it
        public bool ProgressIsOutage;
        public double NowFraction;      // 0..1 position of "now" on the strip
        public string StatusLine;
        public bool StatusIsError;
        public string Notice;
    }

    /// <summary>The application: tray icon, timers, downloads and warnings.</summary>
    internal sealed class TrayContext : ApplicationContext
    {
        private static readonly int[] RetryMinutes = { 1, 2, 5 };
        private const int StaleAfterFailures = 3;

        private Strings S;
        private Theme theme = Theme.LightTheme;
        private readonly RegistrySettingsStore store = new RegistrySettingsStore();
        private readonly WarningPlanner planner = new WarningPlanner();
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip menu;
        private readonly System.Windows.Forms.Timer warnTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer retryTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer trimTimer = new System.Windows.Forms.Timer();
        private readonly Control invoker = new Control();
        private readonly StateIcons icons = new StateIcons();
        private bool iconOff, iconShown;
        private readonly TaskScheduler uiScheduler;
        private readonly RegisteredWaitHandle showWait;

        private AppSettings settings;
        private MainForm form;
        private AboutForm about;
        private SettingsForm settingsForm;
        private ParsedPage page;
        private DateTime? lastCheck;
        private DateTime? nextCheck;
        private string error;
        private bool fetching;
        private int failures;
        private bool staleNotified;
        private bool disposed;
        private List<DonationEntry> donations;      // null until donations.json has been read

        public TrayContext(EventWaitHandle showEvent)
        {
            // A hidden control gives us a UI-thread synchronisation context before Application.Run.
            invoker.CreateControl();
            GC.KeepAlive(invoker.Handle);
            if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
            {
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            }
            uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            bool firstRun;
            settings = store.Load(out firstRun);
            S = Strings.ForLanguage(settings.Language);
            planner.Load(settings.NotifiedSeries);
            theme = Theme.For(settings.DarkTheme);

            menu = new ContextMenuStrip();
            BuildMenu();

            tray = new NotifyIcon();
            tray.Icon = icons.Small(false);
            tray.Text = S.AppName;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowWindow(); };
            tray.BalloonTipClicked += delegate { ShowWindow(); };
            tray.Visible = true;

            warnTimer.Interval = 30 * 1000;
            warnTimer.Tick += delegate { Evaluate(); };
            warnTimer.Start();

            trimTimer.Interval = 5000;
            trimTimer.Tick += delegate { trimTimer.Stop(); TrimIfIdle(); };

            refreshTimer.Tick += delegate { RefreshNow(); };
            retryTimer.Tick += delegate { retryTimer.Stop(); RefreshNow(); };
            RestartRefreshTimer();

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.TimeChanged += OnTimeChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, delegate
            {
                RunOnUi(ShowWindow);
            }, null, Timeout.Infinite, false);

            TryApplyAutostart(settings.StartWithWindows);

            if (firstRun)
            {
                TrySave();
                ShowWindow();
                ShowSettings();
            }

            RefreshNow();
            Evaluate();
            LoadDonations();
        }

        /// <summary>Tray menu items in the current language and theme.</summary>
        private void BuildMenu()
        {
            var old = new ToolStripItem[menu.Items.Count];
            menu.Items.CopyTo(old, 0);
            menu.Items.Clear();
            foreach (var item in old) item.Dispose();
            var open = new ToolStripMenuItem(S.MenuOpen, null, delegate { ShowWindow(); });
            menu.Items.Add(open);
            menu.Items.Add(new ToolStripMenuItem(S.MenuCheckNow, null, delegate { RefreshNow(); }));
            menu.Items.Add(new ToolStripMenuItem(S.MenuAbout, null, delegate { ShowAbout(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(S.MenuClose, null, delegate { ExitApplication(); }));
            theme.ApplyMenu(menu);
            open.Font = Ui.BodyStrong;
        }

        // ---- donations ---------------------------------------------------------

        /// <summary>Reads donations.json from GitHub (and the sites' favicons) in the background, once per start.</summary>
        private void LoadDonations()
        {
            if (!LoadDonationsAtStart) return;
            int iconSize = Ui.S(16);
            Task.Factory.StartNew(delegate { return DonationFetcher.Load(iconSize); },
                    CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(delegate(Task<List<DonationEntry>> t)
                {
                    if (!t.IsFaulted) UseDonations(t.Result);      // offline or no file: About shows no Donations section
                }, CancellationToken.None, TaskContinuationOptions.None, uiScheduler);
        }

        /// <summary>Keeps the donation links for the About window (also used by the screenshot tool).</summary>
        internal void UseDonations(List<DonationEntry> list)
        {
            if (disposed)
            {
                DisposeIcons(list);
                return;
            }
            var previous = donations;
            donations = list;
            if (about != null && !about.IsDisposed) about.ShowDonations(donations);   // stops showing the old icons
            DisposeIcons(previous);
            RequestTrim();
        }

        /// <summary>The screenshot tool turns this off and supplies the working copy's donations.json instead.</summary>
        internal static bool LoadDonationsAtStart = true;

        private static void DisposeIcons(List<DonationEntry> list)
        {
            if (list == null) return;
            foreach (var d in list) if (d.Icon != null) d.Icon.Dispose();
        }

        // ---- window ------------------------------------------------------------

        public void ShowWindow()
        {
            if (disposed) return;
            if (form == null || form.IsDisposed)
            {
                form = new MainForm(S, this, theme);
                form.FormClosed += delegate { RequestTrim(); };
                RestoreWindowPosition(form);
                var closing = form;
                closing.FormClosing += delegate
                {
                    var bounds = closing.WindowState == FormWindowState.Normal ? closing.Bounds : closing.RestoreBounds;
                    store.SaveWindowPosition(bounds.Location);
                };
            }
            form.SetPowerIcon(icons.Large(iconOff));
            form.ShowStatus(BuildStatus(KyivClock.Now));
            if (!form.Visible) form.Show();
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.Activate();
        }

        /// <summary>
        /// Opens the main window where it was last closed, if that place is still on a screen
        /// (a monitor may have been disconnected); otherwise it opens centred.
        /// </summary>
        private void RestoreWindowPosition(Form f)
        {
            var saved = store.LoadWindowPosition();
            if (!saved.HasValue) return;
            // The title bar must be reachable: its top-left 120 x 30 px has to be inside a screen's working area.
            var grip = new Rectangle(saved.Value.X + 20, saved.Value.Y, 120, 30);
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.Contains(grip))
                {
                    f.StartPosition = FormStartPosition.Manual;
                    f.Location = saved.Value;
                    return;
                }
            }
        }

        /// <summary>
        /// Shows the settings window (one at a time). An already open window keeps its unsaved edits;
        /// a new one is filled from the saved settings and placed over the main window.
        /// </summary>
        public void ShowSettings()
        {
            if (disposed) return;
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(S, this, theme);
                settingsForm.FormClosed += delegate { RequestTrim(); };
                settingsForm.LoadSettings(settings);
            }
            if (!settingsForm.Visible)
            {
                var anchor = form != null && !form.IsDisposed && form.Visible ? form : null;
                settingsForm.Owner = anchor;            // stays above the main window
                settingsForm.PlaceOver(anchor);
                settingsForm.Show();
            }
            if (settingsForm.WindowState == FormWindowState.Minimized) settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
        }

        /// <summary>Shows the About window (one at a time).</summary>
        public void ShowAbout()
        {
            if (disposed) return;
            if (about == null || about.IsDisposed)
            {
                about = new AboutForm(S, theme, donations);
                about.FormClosed += delegate { RequestTrim(); };
            }
            if (!about.Visible) about.Show();
            about.Activate();
        }

        /// <summary>Day/Night switch: applies at once and saves only this setting.</summary>
        public void SetDarkTheme(bool dark)
        {
            if (settings.DarkTheme == dark) return;
            settings.DarkTheme = dark;
            TrySave();
            ApplyThemeEverywhere();
        }

        /// <summary>Windows colour settings changed (e.g. High Contrast turned on or off): recolour everything.</summary>
        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.Color || e.Category == UserPreferenceCategory.Accessibility ||
                e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Desktop)
            {
                RunOnUi(delegate
                {
                    // Wallpaper, transparency or contrast may have changed: rebuild the backdrop and recolour.
                    Backdrop.Reset();
                    ApplyThemeEverywhere();
                });
            }
        }

        private void ApplyThemeEverywhere()
        {
            if (disposed) return;
            theme = Theme.For(settings.DarkTheme);
            theme.ApplyMenu(menu);
            if (form != null && !form.IsDisposed) form.ApplyTheme(theme);
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.ApplyTheme(theme);
            if (about != null && !about.IsDisposed) about.ApplyTheme(theme);
        }

        /// <summary>Called by the settings window's Save button.</summary>
        public void ApplySettings(AppSettings updated)
        {
            updated.Normalize();
            bool urlChanged = !string.Equals(updated.ScheduleUrl, settings.ScheduleUrl, StringComparison.Ordinal);
            bool refreshChanged = updated.RefreshMinutes != settings.RefreshMinutes;
            bool autostartChanged = updated.StartWithWindows != settings.StartWithWindows;
            bool languageChanged = updated.Language != settings.Language;

            updated.KnownQueues = settings.KnownQueues;
            updated.DarkTheme = settings.DarkTheme;     // the Day/Night switch saves itself
            updated.NotifiedSeries = planner.Serialize();
            settings = updated;
            TrySave();

            if (autostartChanged) TryApplyAutostart(settings.StartWithWindows);

            if (urlChanged)
            {
                page = null;
                error = null;
                RefreshNow();
            }
            else if (refreshChanged)
            {
                RestartRefreshTimer();
            }
            Evaluate();
            if (languageChanged) RunOnUi(ChangeLanguage);   // after the Save click handler has returned
        }

        /// <summary>
        /// Switches the UI language: the tray menu is rebuilt and the open windows are closed and opened again
        /// in the new language at the same place (the settings are saved, so nothing is lost).
        /// </summary>
        private void ChangeLanguage()
        {
            if (disposed) return;
            S = Strings.ForLanguage(settings.Language);
            BuildMenu();

            bool mainOpen = IsOpen(form), settingsOpen = IsOpen(settingsForm), aboutOpen = IsOpen(about);
            var settingsAt = settingsOpen ? settingsForm.Location : Point.Empty;
            var aboutAt = aboutOpen ? about.Location : Point.Empty;
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.Close();
            if (about != null && !about.IsDisposed) about.Close();
            if (form != null && !form.IsDisposed) form.Close();

            // Reopen in this order so Preferences, where Save was pressed, ends up in front with focus on Save.
            if (mainOpen) ShowWindow();
            if (aboutOpen)
            {
                ShowAbout();
                about.Location = aboutAt;
            }
            if (settingsOpen)
            {
                ShowSettings();
                settingsForm.Location = settingsAt;
                settingsForm.ShowSaved();
                settingsForm.FocusSave();
            }
            UpdateViews(KyivClock.Now, null);
        }

        public void RefreshNow()
        {
            if (disposed || fetching) return;
            fetching = true;
            retryTimer.Stop();
            var url = settings.ScheduleUrl;
            UpdateViews(KyivClock.Now, null);

            Task.Factory.StartNew(delegate { return ScheduleFetcher.Fetch(url); },
                    CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(delegate(Task<ParsedPage> t) { OnFetched(t, url); },
                    CancellationToken.None, TaskContinuationOptions.None, uiScheduler);
        }

        public void ExitApplication()
        {
            ExitThread();
        }

        // ---- downloads ---------------------------------------------------------

        private void OnFetched(Task<ParsedPage> task, string url)
        {
            fetching = false;
            if (disposed) return;
            if (!string.Equals(url, settings.ScheduleUrl, StringComparison.Ordinal))
            {
                RefreshNow(); // the address changed while downloading
                return;
            }

            var now = KyivClock.Now;
            lastCheck = now;

            if (task.IsFaulted)
            {
                var ex = task.Exception.GetBaseException();
                failures++;
                error = ex is ScheduleFormatException
                    ? string.Format(S.FormatFailed, ScheduleFetcher.Describe(ex))
                    : ScheduleFetcher.Describe(ex);

                int delay = Math.Min(RetryMinutes[Math.Min(failures, RetryMinutes.Length) - 1], settings.RefreshMinutes);
                retryTimer.Interval = delay * 60 * 1000;
                retryTimer.Start();
                nextCheck = now.AddMinutes(delay);

                if (failures >= StaleAfterFailures && !staleNotified)
                {
                    staleNotified = true;
                    var last = page != null && page.UpdatedAt.HasValue
                        ? Strings.Time(page.UpdatedAt.Value) + " " + Strings.Date(page.UpdatedAt.Value)
                        : "-";
                    Notify(S.StaleTitle, string.Format(S.StaleText, last), ToolTipIcon.Info, false);
                }
            }
            else
            {
                page = task.Result;
                error = null;
                failures = 0;
                staleNotified = false;
                RememberQueues(page.QueueLabels());
                RestartRefreshTimer();
            }

            Evaluate();
            RequestTrim();
        }

        private void RestartRefreshTimer()
        {
            refreshTimer.Stop();
            refreshTimer.Interval = settings.RefreshMinutes * 60 * 1000;
            refreshTimer.Start();
            nextCheck = KyivClock.Now.AddMinutes(settings.RefreshMinutes);
        }

        private void RememberQueues(List<string> labels)
        {
            if (labels.Count == 0) return;
            var joined = string.Join(";", labels.ToArray());
            if (joined == settings.KnownQueues) return;
            settings.KnownQueues = joined;
            TrySave();
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                settingsForm.UpdateQueueList(settings.KnownQueueList(), settings.Queue);
            }
        }

        // ---- warnings ----------------------------------------------------------

        private void Evaluate()
        {
            if (disposed) return;
            var now = KyivClock.Now;
            var series = CurrentSeries();

            planner.Prune(now);
            var due = planner.Due(series, now, settings.LeadMinutes);
            if (due.Count > 0)
            {
                string text;
                var title = S.Toast(due[0], settings.Queue, out text);
                Notify(title, text, ToolTipIcon.Warning, true);
                settings.NotifiedSeries = planner.Serialize();
                TrySave();
            }

            UpdateViews(now, series);
        }

        /// <summary>
        /// Windows toast (kept in the Notifications panel; a blackout warning stays on screen until dismissed).
        /// The tray balloon is shown only if the toast cannot be shown.
        /// </summary>
        private void Notify(string title, string text, ToolTipIcon icon, bool sticky)
        {
            if (!Toasts.Show(title, text, sticky)) tray.ShowBalloonTip(15000, title, text, icon);
            RequestTrim();
        }

        // ---- memory ------------------------------------------------------------

        /// <summary>Returns memory to Windows a few seconds after a download, a notification or a closed window.</summary>
        private void RequestTrim()
        {
            if (disposed) return;
            trimTimer.Stop();
            trimTimer.Start();
        }

        /// <summary>Only while nothing is on screen; closing the last window asks again.</summary>
        private void TrimIfIdle()
        {
            if (disposed || IsOpen(form) || IsOpen(settingsForm) || IsOpen(about)) return;
            MemoryTrim.Run();
        }

        private static bool IsOpen(Form f)
        {
            return f != null && !f.IsDisposed && f.Visible;
        }

        /// <summary>
        /// Settings window's "Test notification": the warning for the nearest blackout in the saved queue.
        /// Nothing is remembered, so real warnings (NotifiedSeries) are not affected.
        /// </summary>
        public void SendTestNotification()
        {
            if (disposed) return;
            var now = KyivClock.Now;
            var series = CurrentSeries();
            var current = SeriesBuilder.Current(series, now);
            var next = SeriesBuilder.Next(series, now);

            string title, text;
            if (page == null)
            {
                title = S.TestNoSchedule;
                text = error ?? S.Loading;
            }
            else if (next != null)
            {
                title = string.Format(S.TestNext, S.RangeFrom(next, now), S.Duration(next.Start - now));
                text = string.Format(CultureInfo.InvariantCulture, S.ToastText, Strings.EndTime(next), settings.Queue);
            }
            else if (current != null)
            {
                title = string.Format(S.TestNow, Strings.EndTime(current));
                text = string.Format(CultureInfo.InvariantCulture, S.ToastText, Strings.EndTime(current), settings.Queue);
            }
            else
            {
                title = S.TestNone;
                text = string.Format(S.QueueBadge, settings.Queue);
            }
            Notify(title, text, ToolTipIcon.Warning, true);
        }

        private List<BlackoutSeries> CurrentSeries()
        {
            if (page == null) return new List<BlackoutSeries>();
            return SeriesBuilder.Build(page.Days, settings.Queue);
        }

        private void UpdateViews(DateTime now, List<BlackoutSeries> series)
        {
            if (series == null) series = CurrentSeries();
            tray.Text = Tooltip(now, series);

            // Tray, taskbar and window icon: lit bulb with power, crossed-out bulb during an outage.
            bool off = SeriesBuilder.Current(series, now) != null;
            if (!iconShown || off != iconOff)
            {
                tray.Icon = icons.Small(off);
                iconOff = off;
                iconShown = true;
            }
            if (form != null && !form.IsDisposed)
            {
                form.SetPowerIcon(icons.Large(off));
                if (form.Visible) form.ShowStatus(BuildStatus(now, series));
            }
        }

        private string Tooltip(DateTime now, List<BlackoutSeries> series)
        {
            string text;
            var current = SeriesBuilder.Current(series, now);
            var next = SeriesBuilder.Next(series, now);
            if (page == null && error != null) text = S.TooltipError;
            else if (current != null) text = string.Format(S.TooltipNow, Strings.EndTime(current));
            else if (next != null) text = string.Format(S.TooltipNext, S.RangeFrom(next, now));
            else if (page == null) text = S.AppName;
            else text = S.TooltipNone;
            text = S.AppName + "\n" + text;
            return text.Length > 63 ? text.Substring(0, 63) : text; // NotifyIcon limit
        }

        private StatusView BuildStatus(DateTime now)
        {
            return BuildStatus(now, CurrentSeries());
        }

        private StatusView BuildStatus(DateTime now, List<BlackoutSeries> series)
        {
            var v = new StatusView();
            v.QueueBadge = string.Format(S.QueueBadge, settings.Queue);
            v.Today = now.Date;
            v.NowFraction = now.TimeOfDay.TotalMinutes / (24 * 60.0);

            if (page == null)
            {
                v.Caption = S.NextBlackout;
                v.Range = fetching || error == null ? S.Loading : "—";
                v.Countdown = "";
            }
            else
            {
                v.QueueMissing = !page.QueueLabels().Contains(settings.Queue);
                var next = NextBlackout.Describe(S, page, settings.Queue, now);
                v.Caption = next.Caption;
                v.Range = next.Range;
                v.Countdown = next.Countdown;
                v.CountdownIsNote = next.CountdownIsNote;
                v.PowerOff = next.PowerOff;
                v.Progress = next.Progress;
                v.ProgressIsOutage = next.ProgressIsOutage;

                var today = page.FindDay(now);
                var tomorrow = page.FindDay(now.AddDays(1));
                if (today != null) v.TodaySlots = today.GetRow(settings.Queue);
                if (tomorrow != null) v.TomorrowSlots = tomorrow.GetRow(settings.Queue);

                if (page.NoticeLines.Count > 0)
                {
                    v.Notice = string.Join(" · ", page.NoticeLines.ToArray());
                }
            }

            var parts = new List<string>();
            if (error != null)
            {
                v.StatusIsError = true;
                parts.Add(string.Format(S.FetchFailed, error, nextCheck.HasValue ? Strings.Time(nextCheck.Value) : "—"));
            }
            else
            {
                if (v.QueueMissing) parts.Add(string.Format(S.QueueMissing, settings.Queue));
                else if (page != null && v.TodaySlots == null) parts.Add(S.NoGridForToday);
                if (page != null && page.UpdatedAt.HasValue)
                {
                    var u = page.UpdatedAt.Value;
                    parts.Add(string.Format(S.SiteUpdated, u.Date == now.Date ? Strings.Time(u) : Strings.Time(u) + " " + Strings.Date(u)));
                }
                if (lastCheck.HasValue) parts.Add(string.Format(S.Checked, Strings.Time(lastCheck.Value)));
                else parts.Add(S.NotChecked);
                if (nextCheck.HasValue) parts.Add(string.Format(S.NextCheck, Strings.Time(nextCheck.Value)));
            }
            var line = string.Join(" · ", parts.ToArray());
            v.StatusLine = line.Length > 0 ? char.ToUpper(line[0], CultureInfo.CurrentCulture) + line.Substring(1) : line;
            return v;
        }

        // ---- system events and helpers -----------------------------------------

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume) return;
            RunOnUi(delegate
            {
                RefreshNow();
                Evaluate();
            });
        }

        private void OnTimeChanged(object sender, EventArgs e)
        {
            RunOnUi(Evaluate);
        }

        private void RunOnUi(MethodInvoker action)
        {
            if (disposed || invoker.IsDisposed) return;
            try
            {
                invoker.BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void TrySave()
        {
            try
            {
                store.Save(settings);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        private void TryApplyAutostart(bool enabled)
        {
            try
            {
                Autostart.Apply(enabled);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, S.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void ExitThreadCore()
        {
            TrySave();
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.Close();
            if (about != null && !about.IsDisposed) about.Close();
            if (form != null && !form.IsDisposed)
            {
                form.Close();
            }
            tray.Visible = false;
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                SystemEvents.TimeChanged -= OnTimeChanged;
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                showWait.Unregister(null);
                warnTimer.Dispose();
                refreshTimer.Dispose();
                retryTimer.Dispose();
                trimTimer.Dispose();
                tray.Visible = false;
                tray.Dispose();
                menu.Dispose();
                if (form != null) form.Dispose();
                icons.Dispose();
                if (about != null) about.Dispose();
                DisposeIcons(donations);
                if (settingsForm != null) settingsForm.Dispose();
                invoker.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
