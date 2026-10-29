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
    /// <summary>
    /// The main window: a toolbar row (settings, about, Day/Night switch) and the status area.
    /// Built in code (no designer files).
    /// </summary>
    internal sealed class MainForm : Form
    {
        private readonly Strings S;
        private readonly TrayContext app;
        private Theme T;
        private readonly ThemeToggle themeToggle = new ThemeToggle();
        private readonly Button btnSettings = new Button();
        private readonly Button btnAbout = new Button();

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
        private GroupBox statusGroup;
        private bool hasNotice;
        private StatusView lastView;

        /// <summary>When false, the close button only hides the window to the tray.</summary>
        public bool AllowClose;

        // ---- layout (96-DPI units; Windows scales them with AutoScaleMode.Dpi) --

        internal const int GroupWidth = 540;
        internal const int Inner = GroupWidth - 24;   // usable width inside a group
        private const int Margin96 = 12;
        private const int ToolbarTop = 8, ToolButton = 36;

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
            ClientSize = new Size(GroupWidth + 2 * Margin96, 330);

            BuildToolbar();
            statusGroup = BuildStatusGroup(new Point(Margin96, ToolbarTop + ToolButton + 4));
            Controls.Add(statusGroup);

            ApplyTheme(T);
            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>Recolours the window in place.</summary>
        public void ApplyTheme(Theme theme)
        {
            T = theme;
            SuspendLayout();
            T.Apply(this);
            themeToggle.DarkMode = T.Dark;
            UpdateToggleText();
            Theme.ApplyTitleBar(this, T.Dark);
            if (lastView != null) ShowStatus(lastView);   // keeps warning colours on the status labels
            ResumeLayout(true);
            Invalidate(true);
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

        /// <summary>Right-aligned row: gear (settings), "?" (about), Day/Night switch.</summary>
        private void BuildToolbar()
        {
            int right = Margin96 + GroupWidth;

            // Day/Night switch: applies and saves at once.
            themeToggle.SetBounds(right - 56, ToolbarTop + (ToolButton - 28) / 2, 56, 28);
            themeToggle.Toggled += delegate
            {
                UpdateToggleText();
                app.SetDarkTheme(themeToggle.DarkMode);
            };
            Controls.Add(themeToggle);

            SetupToolButton(btnAbout, Glyphs.Help, "?", S.MenuAbout, themeToggle.Left - 12 - ToolButton);
            btnAbout.Click += delegate { app.ShowAbout(); };
            SetupToolButton(btnSettings, Glyphs.Settings, "\u2699", S.GroupSettings, btnAbout.Left - 8 - ToolButton);
            btnSettings.Click += delegate { app.ShowSettings(); };
            Controls.Add(btnSettings);
            Controls.Add(btnAbout);
        }

        private void SetupToolButton(Button b, string glyph, string fallback, string name, int x)
        {
            b.SetBounds(x, ToolbarTop, ToolButton, ToolButton);
            var icons = Glyphs.Font(14F);
            if (icons != null)
            {
                b.Font = icons;
                b.Text = glyph;
            }
            else
            {
                b.Font = new Font("Segoe UI Symbol", 14F);
                b.Text = fallback;
            }
            b.Padding = Padding.Empty;
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.UseVisualStyleBackColor = true;
            b.UseMnemonic = false;
            b.AccessibleName = name;
            tips.SetToolTip(b, name);
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

        internal static GroupBox Group(string title, Point at, int height)
        {
            var g = new ThemedGroupBox();
            g.Text = title;
            g.SetBounds(at.X, at.Y, GroupWidth, height);
            return g;
        }

        internal static Label Caption(string text, int x, int y)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            return l;
        }

        // ---- status ------------------------------------------------------------

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
            ClientSize = new Size(ClientSize.Width, statusGroup.Bottom + btnSettings.Top + gap);
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
            if (disposing) tips.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Toolbar icons from the Segoe MDL2 Assets font that ships with Windows 10 and 11.</summary>
    internal static class Glyphs
    {
        public const string Settings = "\uE713";   // gear
        public const string Help = "\uE897";       // question mark

        private const string Family = "Segoe MDL2 Assets";

        /// <summary>The icon font, or null when it is not installed (the caller then uses plain text).</summary>
        public static Font Font(float size)
        {
            var f = new Font(Family, size);
            if (string.Equals(f.Name, Family, StringComparison.OrdinalIgnoreCase)) return f;
            f.Dispose();
            return null;
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
