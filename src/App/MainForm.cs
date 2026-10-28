// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using UABlackoutPreWarning.Core;

namespace UABlackoutPreWarning
{
    /// <summary>The single window: settings on top, status below. Built in code (no designer files).</summary>
    internal sealed class MainForm : Form
    {
        private readonly Strings S;
        private readonly TrayContext app;
        private Theme T;
        private readonly ThemeToggle themeToggle = new ThemeToggle();

        private readonly TextBox txtUrl = new TextBox();
        private readonly ComboBox cmbQueue = new ComboBox();
        private readonly NumericUpDown numLead = new NumericUpDown();
        private readonly NumericUpDown numRefresh = new NumericUpDown();
        private readonly CheckBox chkAutostart = new CheckBox();
        private readonly Button btnCheck = new Button();
        private readonly Button btnSave = new Button();
        private readonly Button btnDefaults = new Button();
        private readonly Label lblSaved = new Label();
        private readonly LinkLabel lnkAbout = new LinkLabel();
        private readonly ErrorProvider errors = new ErrorProvider();

        private readonly Label lblCaption = new Label();
        private readonly Label lblBadge = new Label();
        private readonly Label lblRange = new Label();
        private readonly Label lblCountdown = new Label();
        private readonly Label lblToday = new Label();
        private readonly Label lblLegend = new Label();
        private readonly DayStrip strip = new DayStrip();
        private readonly Label lblStatus = new Label();
        private readonly Label lblNotice = new Label();
        private readonly ToolTip tips = new ToolTip();
        private readonly Timer savedTimer = new Timer();
        private GroupBox settingsGroup;
        private GroupBox statusGroup;
        private bool hasNotice;
        private StatusView lastView;

        /// <summary>When false, the close button only hides the window to the tray.</summary>
        public bool AllowClose;

        public MainForm(Strings strings, TrayContext context, Theme theme)
        {
            S = strings;
            app = context;
            T = theme;

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = S.AppName;
            Icon = AppInfo.LoadIcon(new Size(32, 32));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            ClientSize = new Size(GroupWidth + 24, 486);

            settingsGroup = BuildSettingsGroup(new Point(12, 8));
            statusGroup = BuildStatusGroup(new Point(12, 238));
            Controls.Add(settingsGroup);
            Controls.Add(statusGroup);

            AcceptButton = btnSave;
            errors.BlinkStyle = ErrorBlinkStyle.NeverBlink;
            savedTimer.Interval = 3000;
            savedTimer.Tick += delegate
            {
                savedTimer.Stop();
                lblSaved.Text = "";
            };

            ApplyTheme(T);
            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>Recolours the window in place; field values and unsaved edits stay as they are.</summary>
        public void ApplyTheme(Theme theme)
        {
            T = theme;
            SuspendLayout();
            T.Apply(this);
            cmbQueue.DrawMode = T.Dark ? DrawMode.OwnerDrawFixed : DrawMode.Normal;
            MatchQueueHeight();
            themeToggle.DarkMode = T.Dark;
            UpdateToggleText();
            Theme.ApplyTitleBar(this, T.Dark);
            if (lastView != null) ShowStatus(lastView);   // keeps warning colours on the status labels
            ResumeLayout(true);
            Invalidate(true);
        }

        /// <summary>
        /// In dark mode the queue list is owner-drawn and its height follows ItemHeight;
        /// size it so the box is exactly as tall as the two number fields beside it.
        /// </summary>
        private void MatchQueueHeight()
        {
            if (T.Dark)
            {
                int chrome = cmbQueue.Height - cmbQueue.ItemHeight;      // border + padding of the closed box
                int item = numLead.Height - chrome;
                if (item > 0 && item != cmbQueue.ItemHeight) cmbQueue.ItemHeight = item;
            }
            cmbQueue.Top = numLead.Top + (numLead.Height - cmbQueue.Height) / 2;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            MatchQueueHeight();   // after DPI scaling, when the final control heights are known
        }

        private void UpdateToggleText()
        {
            var text = themeToggle.DarkMode ? S.ThemeToLight : S.ThemeToDark;
            tips.SetToolTip(themeToggle, text);
            themeToggle.AccessibleName = text;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this, T.Dark);
        }

        // ---- layout (96-DPI units; Windows scales them with AutoScaleMode.Dpi) --

        private const int GroupWidth = 540;
        private const int Inner = GroupWidth - 24;   // usable width inside a group
        private const int Col2 = 152, Col3 = 292;

        private GroupBox BuildSettingsGroup(Point at)
        {
            var g = Group(S.GroupSettings, at, 222);

            g.Controls.Add(Caption(S.LabelUrl, 12, 26));

            // Day/Night switch: applies and saves at once, independent of the Save button.
            themeToggle.SetBounds(12 + Inner - 56, 16, 56, 28);
            themeToggle.Toggled += delegate
            {
                UpdateToggleText();
                app.SetDarkTheme(themeToggle.DarkMode);
            };
            g.Controls.Add(themeToggle);

            txtUrl.SetBounds(12, 52, Inner, 23);
            g.Controls.Add(txtUrl);

            g.Controls.Add(Caption(S.LabelQueue, 12, 86));
            g.Controls.Add(Caption(S.LabelLead, Col2, 86));
            g.Controls.Add(Caption(S.LabelRefresh, Col3, 86));

            cmbQueue.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbQueue.SetBounds(12, 104, 80, 23);
            cmbQueue.DrawItem += DrawQueueItem;
            g.Controls.Add(cmbQueue);

            SetupNumber(numLead, AppSettings.MinLead, AppSettings.MaxLead, Col2, 104);
            SetupNumber(numRefresh, AppSettings.MinRefresh, AppSettings.MaxRefresh, Col3, 104);
            g.Controls.Add(numLead);
            g.Controls.Add(numRefresh);
            g.Controls.Add(Caption(S.Minutes, Col2 + 66, 107));
            g.Controls.Add(Caption(S.Minutes, Col3 + 66, 107));

            chkAutostart.Text = S.StartWithWindows;
            chkAutostart.SetBounds(12, 140, Inner - 170, 22);
            g.Controls.Add(chkAutostart);

            // Puts the default values back into the fields; nothing is applied until Save.
            btnDefaults.Text = S.ButtonDefaults;
            btnDefaults.SetBounds(12 + Inner - 150, 136, 150, 28);
            btnDefaults.UseVisualStyleBackColor = true;
            btnDefaults.Click += delegate { RestoreDefaults(); };
            tips.SetToolTip(btnDefaults, S.DefaultsHint);
            g.Controls.Add(btnDefaults);

            btnSave.Text = S.ButtonSave;
            btnSave.SetBounds(12 + Inner - 110, 176, 110, 30);
            btnSave.Tag = Theme.RolePrimary;
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += delegate { Save(); };
            btnCheck.Text = S.ButtonCheckNow;
            btnCheck.SetBounds(btnSave.Left - 8 - 150, 176, 150, 30);
            btnCheck.UseVisualStyleBackColor = true;
            btnCheck.Click += delegate { app.RefreshNow(); };
            g.Controls.Add(btnSave);
            g.Controls.Add(btnCheck);

            lnkAbout.Text = S.MenuAbout;
            lnkAbout.AutoSize = true;
            lnkAbout.Location = new Point(12, 184);
            lnkAbout.LinkClicked += delegate { app.ShowAbout(); };
            g.Controls.Add(lnkAbout);

            lblSaved.SetBounds(130, 184, btnCheck.Left - 140, 22);
            lblSaved.Tag = Theme.RoleSub;
            g.Controls.Add(lblSaved);
            return g;
        }

        private GroupBox BuildStatusGroup(Point at)
        {
            var g = Group(S.GroupStatus, at, 250);

            lblCaption.SetBounds(12, 20, 240, 22);
            lblCaption.Tag = Theme.RoleSub;
            g.Controls.Add(lblCaption);

            lblBadge.SetBounds(12 + Inner - 170, 20, 170, 22);
            lblBadge.TextAlign = ContentAlignment.TopRight;
            lblBadge.Font = new Font(Font, FontStyle.Bold);
            g.Controls.Add(lblBadge);

            lblRange.Location = new Point(10, 40);
            lblRange.AutoSize = true;
            lblRange.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            g.Controls.Add(lblRange);

            lblCountdown.AutoSize = true;
            lblCountdown.Tag = Theme.RoleWarn;
            lblCountdown.Location = new Point(200, 54);
            g.Controls.Add(lblCountdown);

            lblToday.SetBounds(12, 80, 260, 22);
            lblToday.Tag = Theme.RoleSub;
            g.Controls.Add(lblToday);

            lblLegend.AutoSize = true;                 // right-aligned to the strip in FitLayout()
            lblLegend.Location = new Point(12 + Inner - 150, 80);
            lblLegend.Tag = Theme.RoleSub;
            lblLegend.Text = S.Legend;
            g.Controls.Add(lblLegend);

            strip.SetBounds(12, 108, Inner, 50);
            g.Controls.Add(strip);
            strip.BringToFront();

            lblStatus.SetBounds(12, 150, Inner, 38);   // height and position fitted in FitLayout()
            lblStatus.UseMnemonic = false;
            lblStatus.Tag = Theme.RoleSub;
            g.Controls.Add(lblStatus);

            lblNotice.SetBounds(12, 190, Inner, 52);   // height is fitted to the text in FitLayout()
            lblNotice.Tag = Theme.RoleSub;
            lblNotice.UseMnemonic = false;
            g.Controls.Add(lblNotice);
            return g;
        }

        private static GroupBox Group(string title, Point at, int height)
        {
            var g = new ThemedGroupBox();
            g.Text = title;
            g.SetBounds(at.X, at.Y, GroupWidth, height);
            return g;
        }

        private static Label Caption(string text, int x, int y)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            return l;
        }

        private static void SetupNumber(NumericUpDown n, int min, int max, int x, int y)
        {
            n.Minimum = min;
            n.Maximum = max;
            n.TextAlign = HorizontalAlignment.Right;
            n.SetBounds(x, y, 60, 23);
        }

        // ---- data in and out ---------------------------------------------------

        public void LoadSettings(AppSettings s)
        {
            txtUrl.Text = s.ScheduleUrl;
            errors.SetError(txtUrl, "");
            SetQueueItems(s.KnownQueueList(), s.Queue);
            numLead.Value = Clamp(s.LeadMinutes, numLead);
            numRefresh.Value = Clamp(s.RefreshMinutes, numRefresh);
            chkAutostart.Checked = s.StartWithWindows;
        }

        /// <summary>New row labels arrived from the site; keeps the user's current choice.</summary>
        public void UpdateQueueList(List<string> queues, string savedQueue)
        {
            var current = cmbQueue.SelectedItem as string;
            SetQueueItems(queues, string.IsNullOrEmpty(current) ? savedQueue : current);
        }

        private void SetQueueItems(List<string> queues, string selected)
        {
            cmbQueue.BeginUpdate();
            cmbQueue.Items.Clear();
            foreach (var q in queues) cmbQueue.Items.Add(q);
            if (!string.IsNullOrEmpty(selected) && !cmbQueue.Items.Contains(selected)) cmbQueue.Items.Add(selected);
            cmbQueue.SelectedItem = selected;
            cmbQueue.EndUpdate();
        }

        public void ShowStatus(StatusView v)
        {
            lastView = v;
            lblCaption.Text = v.Caption;
            lblBadge.Text = v.QueueBadge;
            lblBadge.ForeColor = v.QueueMissing ? T.Warn : T.Text;
            lblRange.Text = v.Range;
            lblCountdown.Text = v.Countdown;
            lblCountdown.Left = lblRange.Right + lblRange.Margin.Right;
            lblCountdown.Top = lblRange.Bottom - lblCountdown.Height - lblRange.Height / 8;
            lblToday.Text = v.TodayLabel;
            strip.SetData(v.TodaySlots, v.NowFraction);
            lblStatus.Text = v.StatusLine;
            lblStatus.ForeColor = v.StatusIsError ? T.Warn : T.Sub;
            lblNotice.Text = v.Notice ?? "";
            hasNotice = !string.IsNullOrEmpty(v.Notice);
            lblNotice.Visible = hasNotice;
            FitLayout();
            tips.SetToolTip(lblNotice, v.Notice ?? "");
        }

        /// <summary>
        /// Right-aligns the legend with the strip and grows the site-notice label, the status
        /// group and the window so the whole notice is visible. Uses scaled pixels (any DPI).
        /// </summary>
        private void FitLayout()
        {
            SuspendLayout();
            lblLegend.Left = strip.Right - lblLegend.Width;

            int gap = Font.Height / 2;

            // Status line and site notice are sized to their wrapped text and stacked
            // below the strip, so neither can cover the other.
            FitHeight(lblStatus, strip.Width, 0);
            lblStatus.Top = strip.Bottom + gap / 2;
            FitHeight(lblNotice, strip.Width, gap);
            lblNotice.Top = lblStatus.Bottom + gap;
            lblNotice.BringToFront();

            int bottom = hasNotice ? lblNotice.Bottom : lblStatus.Bottom;
            statusGroup.Height = bottom + 2 * gap;
            ClientSize = new Size(ClientSize.Width, statusGroup.Bottom + settingsGroup.Top + gap);
            ResumeLayout(true);
        }

        /// <summary>Sets a label's size to its word-wrapped text plus <paramref name="extra"/> pixels.</summary>
        private static void FitHeight(Label label, int width, int extra)
        {
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
            var needed = TextRenderer.MeasureText(label.Text.Length == 0 ? " " : label.Text, label.Font,
                new Size(width, int.MaxValue), flags);
            label.Size = new Size(width, needed.Height + extra);
        }

        /// <summary>Queue list items in dark mode (the closed box and list follow the theme).</summary>
        private void DrawQueueItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            var back = selected ? T.ButtonHover : T.Input;
            using (var b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, cmbQueue.Items[e.Index].ToString(), e.Font, e.Bounds, T.InputText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private void RestoreDefaults()
        {
            var d = new AppSettings();
            txtUrl.Text = d.ScheduleUrl;
            errors.SetError(txtUrl, "");
            if (!cmbQueue.Items.Contains(d.Queue)) cmbQueue.Items.Add(d.Queue);
            cmbQueue.SelectedItem = d.Queue;
            numLead.Value = Clamp(d.LeadMinutes, numLead);
            numRefresh.Value = Clamp(d.RefreshMinutes, numRefresh);
            chkAutostart.Checked = d.StartWithWindows;

            savedTimer.Stop();
            lblSaved.Text = S.NotSavedYet;
            btnSave.Focus();
        }

        private void Save()
        {
            var url = txtUrl.Text.Trim();
            if (!AppSettings.IsValidUrl(url))
            {
                errors.SetError(txtUrl, S.InvalidUrl);
                txtUrl.Focus();
                return;
            }
            errors.SetError(txtUrl, "");

            var s = new AppSettings();
            s.ScheduleUrl = url;
            s.Queue = (cmbQueue.SelectedItem as string) ?? AppSettings.DefaultQueue;
            s.LeadMinutes = (int)numLead.Value;
            s.RefreshMinutes = (int)numRefresh.Value;
            s.StartWithWindows = chkAutostart.Checked;
            app.ApplySettings(s);

            lblSaved.Text = S.Saved;
            savedTimer.Stop();
            savedTimer.Start();
        }

        private static decimal Clamp(int value, NumericUpDown n)
        {
            return Math.Max(n.Minimum, Math.Min(n.Maximum, value));
        }

        // ---- window behaviour --------------------------------------------------

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Hide();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                savedTimer.Dispose();
                tips.Dispose();
                errors.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Today's 48 half-hour slots: red = no power, with a marker at the current time.</summary>
    internal sealed class DayStrip : Control
    {
        private Color Off = Color.FromArgb(0xC4, 0x2B, 0x1C);
        private Color On = Color.FromArgb(0xE3, 0xE3, 0xE3);
        private Color Unknown = Color.FromArgb(0xF0, 0xF0, 0xF0);
        private Color Marker = SystemColors.ControlText;
        private Color TickColour = SystemColors.GrayText;

        private bool[] slots;
        private double now = -1;

        public DayStrip()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.Selectable, false);
        }

        public void SetTheme(Theme t)
        {
            Off = t.StripOff;
            On = t.StripOn;
            Unknown = t.StripUnknown;
            Marker = t.Marker;
            TickColour = t.Sub;
            BackColor = t.Window;
            ForeColor = t.Text;
            Invalidate();
        }

        public void SetData(bool[] offSlots, double nowFraction)
        {
            slots = offSlots;
            now = nowFraction;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            float scale = g.DpiY / 96F;
            float barTop = 2 * scale;
            float barHeight = 20 * scale;
            float width = ClientSize.Width;

            // 24 hour blocks with a gap between hours; the two half-hours of an hour sit together.
            float hourGap = 3 * scale;
            float halfGap = 1 * scale;
            float hourWidth = (width - 23 * hourGap) / 24;
            float halfWidth = (hourWidth - halfGap) / 2;

            for (int h = 0; h < 24; h++)
            {
                float x = HourLeft(h, hourWidth, hourGap);
                for (int k = 0; k < 2; k++)
                {
                    int i = 2 * h + k;
                    var colour = slots == null ? Unknown : (slots[i] ? Off : On);
                    using (var brush = new SolidBrush(colour))
                    {
                        g.FillRectangle(brush, x + k * (halfWidth + halfGap), barTop, halfWidth, barHeight);
                    }
                }
            }

            if (now >= 0 && now <= 1)
            {
                double hours = now * 24;
                int h = Math.Min(23, (int)Math.Floor(hours));
                float x = HourLeft(h, hourWidth, hourGap) + (float)(hours - h) * hourWidth;
                using (var pen = new Pen(Marker, 2 * scale))
                {
                    g.DrawLine(pen, x, 0, x, barTop + barHeight + 2 * scale);
                }
            }

            // Tick and label every 2 hours, centred on the gap between hour blocks.
            float tickTop = barTop + barHeight + 2 * scale;
            int textTop = (int)(tickTop + 5 * scale);
            using (var tick = new Pen(TickColour, 1))
            {
                for (int t = 0; t <= 24; t += 2)
                {
                    float x = t == 0 ? 0 : (t == 24 ? width - 1 : HourLeft(t, hourWidth, hourGap) - hourGap / 2);
                    g.DrawLine(tick, x, tickTop, x, tickTop + 4 * scale);

                    var text = t.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
                    var size = TextRenderer.MeasureText(g, text, Font, Size.Empty, TextFormatFlags.NoPadding);
                    int left = t == 0 ? 0 : (t == 24 ? (int)width - size.Width : (int)(x - size.Width / 2F));
                    TextRenderer.DrawText(g, text, Font, new Point(left, textTop), ForeColor, TextFormatFlags.NoPadding);
                }
            }
        }

        private static float HourLeft(int hour, float hourWidth, float hourGap)
        {
            return hour * (hourWidth + hourGap);
        }
    }
}
