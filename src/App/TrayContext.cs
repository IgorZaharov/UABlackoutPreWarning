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
        public string Range;            // "14:00–16:00" or "No blackouts scheduled"
        public string Countdown;        // "in 2 h 15 min" / "until 16:00"
        public string TodayLabel;
        public bool[] TodaySlots;       // null when the site has no grid for today
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

        private readonly Strings S = Strings.Current();
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
            planner.Load(settings.NotifiedSeries);
            theme = Theme.For(settings.DarkTheme);

            menu = new ContextMenuStrip();
            var open = new ToolStripMenuItem(S.MenuOpen, null, delegate { ShowWindow(); });
            open.Font = new Font(open.Font, FontStyle.Bold);
            menu.Items.Add(open);
            menu.Items.Add(new ToolStripMenuItem(S.MenuCheckNow, null, delegate { RefreshNow(); }));
            menu.Items.Add(new ToolStripMenuItem(S.MenuAbout, null, delegate { ShowAbout(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(S.MenuClose, null, delegate { ExitApplication(); }));

            theme.ApplyMenu(menu);

            tray = new NotifyIcon();
            tray.Icon = AppInfo.LoadIcon(SystemInformation.SmallIconSize);
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
        }

        // ---- window ------------------------------------------------------------

        public void ShowWindow()
        {
            if (disposed) return;
            if (form == null || form.IsDisposed)
            {
                form = new MainForm(S, this, theme);
                form.FormClosed += delegate { RequestTrim(); };
            }
            form.ShowStatus(BuildStatus(KyivClock.Now));
            if (!form.Visible) form.Show();
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.Activate();
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
                about = new AboutForm(S, theme);
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
            theme = Theme.For(dark);
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
        /// Shows both a Windows toast (kept in the Notifications panel; a blackout warning stays on screen
        /// until dismissed) and the tray balloon. Toasts are skipped where Windows does not support them.
        /// </summary>
        private void Notify(string title, string text, ToolTipIcon icon, bool sticky)
        {
            Toasts.Show(title, text, sticky);
            tray.ShowBalloonTip(15000, title, text, icon);
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
            if (form != null && !form.IsDisposed && form.Visible) form.ShowStatus(BuildStatus(now, series));
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
            v.TodayLabel = string.Format(S.TodayLabel, Strings.Date(now));
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
                var current = SeriesBuilder.Current(series, now);
                var next = SeriesBuilder.Next(series, now);
                if (current != null)
                {
                    v.Caption = S.PowerOffNow;
                    v.Range = Strings.Range(current);
                    v.Countdown = string.Format(S.UntilTime, Strings.EndTime(current));
                }
                else if (next != null)
                {
                    v.Caption = S.NextBlackout;
                    v.Range = S.RangeFrom(next, now);
                    v.Countdown = string.Format(S.InTime, S.Duration(next.Start - now));
                }
                else
                {
                    v.Caption = S.NextBlackout;
                    v.Range = S.NoBlackouts;
                    v.Countdown = "";
                }

                foreach (var day in page.Days)
                {
                    if (day.Date == now.Date) v.TodaySlots = day.GetRow(settings.Queue);
                }

                if (page.NoticeLines.Count > 0)
                {
                    v.Notice = string.Format(S.SiteNotice, string.Join(" · ", page.NoticeLines.ToArray()));
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
                showWait.Unregister(null);
                warnTimer.Dispose();
                refreshTimer.Dispose();
                retryTimer.Dispose();
                trimTimer.Dispose();
                tray.Visible = false;
                tray.Dispose();
                menu.Dispose();
                if (form != null) form.Dispose();
                if (about != null) about.Dispose();
                if (settingsForm != null) settingsForm.Dispose();
                invoker.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
