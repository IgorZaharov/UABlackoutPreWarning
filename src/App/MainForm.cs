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
        private static readonly Color OffRed = Color.FromArgb(0xC4, 0x2B, 0x1C);
        private static readonly Color DarkRed = Color.FromArgb(0xA4, 0x26, 0x2C);

        private readonly Strings S;
        private readonly TrayContext app;

        private readonly TextBox txtUrl = new TextBox();
        private readonly ComboBox cmbQueue = new ComboBox();
        private readonly NumericUpDown numLead = new NumericUpDown();
        private readonly NumericUpDown numRefresh = new NumericUpDown();
        private readonly CheckBox chkAutostart = new CheckBox();
        private readonly Button btnCheck = new Button();
        private readonly Button btnSave = new Button();
        private readonly Label lblSaved = new Label();
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

        /// <summary>When false, the close button only hides the window to the tray.</summary>
        public bool AllowClose;

        public MainForm(Strings strings, TrayContext context)
        {
            S = strings;
            app = context;

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
            statusGroup = BuildStatusGroup(new Point(12, 226));
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

            ResumeLayout(false);
            PerformLayout();
        }

        // ---- layout (96-DPI units; Windows scales them with AutoScaleMode.Dpi) --

        private const int GroupWidth = 540;
        private const int Inner = GroupWidth - 24;   // usable width inside a group
        private const int Col2 = 182, Col3 = 352;

        private GroupBox BuildSettingsGroup(Point at)
        {
            var g = Group(S.GroupSettings, at, 210);

            g.Controls.Add(Caption(S.LabelUrl, 12, 22));
            txtUrl.SetBounds(12, 40, Inner, 23);
            g.Controls.Add(txtUrl);

            g.Controls.Add(Caption(S.LabelQueue, 12, 74));
            g.Controls.Add(Caption(S.LabelLead, Col2, 74));
            g.Controls.Add(Caption(S.LabelRefresh, Col3, 74));

            cmbQueue.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbQueue.SetBounds(12, 92, 80, 23);
            g.Controls.Add(cmbQueue);

            SetupNumber(numLead, AppSettings.MinLead, AppSettings.MaxLead, Col2, 92);
            SetupNumber(numRefresh, AppSettings.MinRefresh, AppSettings.MaxRefresh, Col3, 92);
            g.Controls.Add(numLead);
            g.Controls.Add(numRefresh);
            g.Controls.Add(Caption(S.Minutes, Col2 + 66, 95));
            g.Controls.Add(Caption(S.Minutes, Col3 + 66, 95));

            chkAutostart.Text = S.StartWithWindows;
            chkAutostart.SetBounds(12, 128, Inner, 22);
            g.Controls.Add(chkAutostart);

            btnSave.Text = S.ButtonSave;
            btnSave.SetBounds(12 + Inner - 110, 164, 110, 30);
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += delegate { Save(); };
            btnCheck.Text = S.ButtonCheckNow;
            btnCheck.SetBounds(btnSave.Left - 8 - 150, 164, 150, 30);
            btnCheck.UseVisualStyleBackColor = true;
            btnCheck.Click += delegate { app.RefreshNow(); };
            g.Controls.Add(btnSave);
            g.Controls.Add(btnCheck);

            lblSaved.SetBounds(12, 168, btnCheck.Left - 20, 22);
            lblSaved.ForeColor = SystemColors.GrayText;
            g.Controls.Add(lblSaved);
            return g;
        }

        private GroupBox BuildStatusGroup(Point at)
        {
            var g = Group(S.GroupStatus, at, 250);

            lblCaption.SetBounds(12, 20, 240, 22);
            lblCaption.ForeColor = SystemColors.GrayText;
            g.Controls.Add(lblCaption);

            lblBadge.AutoSize = true;
            lblBadge.Location = new Point(12 + Inner - 100, 20);
            lblBadge.Font = new Font(Font, FontStyle.Bold);
            g.Controls.Add(lblBadge);

            lblRange.Location = new Point(10, 40);
            lblRange.AutoSize = true;
            lblRange.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            g.Controls.Add(lblRange);

            lblCountdown.AutoSize = true;
            lblCountdown.ForeColor = DarkRed;
            lblCountdown.Location = new Point(200, 54);
            g.Controls.Add(lblCountdown);

            lblToday.SetBounds(12, 82, 220, 22);
            lblToday.ForeColor = SystemColors.GrayText;
            g.Controls.Add(lblToday);

            lblLegend.AutoSize = true;
            lblLegend.Location = new Point(12 + Inner - 150, 82);
            lblLegend.ForeColor = SystemColors.GrayText;
            lblLegend.Text = S.Legend;
            g.Controls.Add(lblLegend);

            strip.SetBounds(12, 106, Inner, 40);
            g.Controls.Add(strip);

            lblStatus.UseMnemonic = false;
            lblStatus.Location = new Point(12, 152);
            lblStatus.ForeColor = SystemColors.GrayText;
            g.Controls.Add(lblStatus);

            lblNotice.UseMnemonic = false;
            lblNotice.Location = new Point(12, 176);
            lblNotice.ForeColor = SystemColors.GrayText;
            g.Controls.Add(lblNotice);
            return g;
        }

        private static GroupBox Group(string title, Point at, int height)
        {
            var g = new GroupBox();
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
            lblCaption.Text = v.Caption;
            lblBadge.Text = v.QueueBadge;
            lblBadge.ForeColor = v.QueueMissing ? OffRed : SystemColors.ControlText;
            lblRange.Text = v.Range;
            lblCountdown.Text = v.Countdown;
            lblCountdown.Left = lblRange.Right + lblRange.Margin.Right;
            lblCountdown.Top = lblRange.Bottom - lblCountdown.Height - lblRange.Height / 8;
            lblToday.Text = v.TodayLabel;
            strip.SetData(v.TodaySlots, v.NowFraction);
            lblStatus.Text = v.StatusLine;
            lblStatus.ForeColor = v.StatusIsError ? OffRed : SystemColors.GrayText;
            lblNotice.Text = v.Notice ?? "";
            hasNotice = !string.IsNullOrEmpty(v.Notice);
            lblNotice.Visible = hasNotice;
            Relayout();
        }

        /// <summary>Sizes a label to its word-wrapped text at the given width.</summary>
        private static void FitText(Label label, int width)
        {
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;
            var size = TextRenderer.MeasureText(label.Text.Length == 0 ? " " : label.Text, label.Font,
                new Size(width, int.MaxValue), flags);
            label.Size = new Size(width, size.Height + label.Font.Height / 4);
        }

        /// <summary>
        /// Right-aligns the badge and legend, wraps the status and site-notice text to the
        /// strip width, and grows the status group and the window so nothing is cut off.
        /// Works in scaled pixels, so it is correct at any DPI.
        /// </summary>
        private void Relayout()
        {
            SuspendLayout();
            int right = strip.Right;
            int gap = Font.Height / 2;

            lblBadge.Left = right - lblBadge.Width;
            lblLegend.Left = right - lblLegend.Width;

            FitText(lblStatus, strip.Width);
            FitText(lblNotice, strip.Width);
            lblStatus.Top = strip.Bottom + gap / 2;
            lblNotice.Top = lblStatus.Bottom + gap;

            int contentBottom = hasNotice ? lblNotice.Bottom : lblStatus.Bottom;
            statusGroup.Height = contentBottom + gap + gap / 2;
            ClientSize = new Size(ClientSize.Width, statusGroup.Bottom + settingsGroup.Top + gap / 2);
            ResumeLayout(true);
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
        private static readonly Color Off = Color.FromArgb(0xC4, 0x2B, 0x1C);
        private static readonly Color On = Color.FromArgb(0xE3, 0xE3, 0xE3);
        private static readonly Color Unknown = Color.FromArgb(0xF0, 0xF0, 0xF0);

        private bool[] slots;
        private double now = -1;

        public DayStrip()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.Selectable, false);
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
            float cell = width / Slots.PerDay;

            for (int i = 0; i < Slots.PerDay; i++)
            {
                var colour = slots == null ? Unknown : (slots[i] ? Off : On);
                using (var brush = new SolidBrush(colour))
                {
                    g.FillRectangle(brush, i * cell, barTop, Math.Max(1F, cell - 1F), barHeight);
                }
            }

            if (now >= 0 && now <= 1)
            {
                float x = (float)(now * width);
                using (var pen = new Pen(ForeColor, 2 * scale))
                {
                    g.DrawLine(pen, x, 0, x, barTop + barHeight + 2 * scale);
                }
            }

            var hours = new[] { "00", "06", "12", "18", "24" };
            int textTop = (int)(barTop + barHeight + 3 * scale);
            for (int i = 0; i < hours.Length; i++)
            {
                int x = (int)(i * width / 4);
                var size = TextRenderer.MeasureText(g, hours[i], Font);
                int left = i == 0 ? 0 : (i == hours.Length - 1 ? x - size.Width : x - size.Width / 2);
                TextRenderer.DrawText(g, hours[i], Font, new Point(left, textTop), SystemColors.GrayText);
            }
        }
    }
}
