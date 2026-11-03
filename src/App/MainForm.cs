// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Drawing;
using System.Windows.Forms;
using UABlackoutPreWarning.Core;

namespace UABlackoutPreWarning
{
    /// <summary>
    /// The main window: a toolbar row (queue, settings, about, dark-theme switch), the status card with
    /// today's strip, the site notice and links to the schedules and ZTOE's Facebook page.
    /// Built in code (no designer files); laid out in real pixels by <see cref="Arrange"/>.
    /// </summary>
    internal sealed class MainForm : Form
    {
        internal const int Width96 = 560;

        private readonly Strings S;
        private readonly TrayContext app;
        private Theme T;
        private readonly ToolTip tips = new ToolTip();

        private readonly Label lblBadge;
        private readonly FluentButton btnSettings, btnAbout;
        private readonly Panel toolbarDivider = new Panel();
        private readonly Label themeIcon;       // sun in the light theme, moon in the dark one
        private readonly ToggleSwitch themeSwitch = new ToggleSwitch();

        private readonly Card statusCard = new Card();
        private readonly Label lblCaption, lblRange, lblCountdown, lblLegendOff, lblLegendOn;
        private readonly TabButton tabToday = new TabButton(), tabTomorrow = new TabButton();
        private bool showTomorrow;      // which day the strip shows (the tabs); kept when the status refreshes
        private readonly Panel swatchOff = new Panel(), swatchOn = new Panel();
        private readonly DayStrip strip = new DayStrip();
        private readonly Label lblStatus;

        private readonly Card noticeCard = new Card();
        private readonly Label noticeIcon, lblNoticeTitle, lblNotice;

        private readonly Label iconSchedules, iconFacebook;
        private readonly LinkLabel lnkSchedules, lnkFacebook;

        private StatusView lastView;
        private bool hasNotice;         // Control.Visible reads false until the window is shown, so keep our own flag

        public MainForm(Strings strings, TrayContext context, Theme theme)
        {
            S = strings;
            app = context;
            T = theme;

            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            Font = Ui.Body;
            Text = S.AppName;
            Icon = AppInfo.LoadIcon(new Size(32, 32));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            // Toolbar row.
            lblBadge = Ui.Inline("", Ui.BodyStrong, null);
            btnSettings = FluentButton.Icon(Ui.GlyphSettings, "⚙", S.SettingsTitle);
            btnSettings.Click += delegate { app.ShowSettings(); };
            btnAbout = FluentButton.Icon(Ui.GlyphHelp, "?", S.MenuAbout);
            btnAbout.Click += delegate { app.ShowAbout(); };
            tips.SetToolTip(btnSettings, S.SettingsTitle);
            tips.SetToolTip(btnAbout, S.MenuAbout);
            toolbarDivider.Tag = Theme.RoleDivider;
            themeIcon = Ui.Glyph(Ui.GlyphSun, null);
            themeSwitch.Text = S.DarkTheme;                 // read by screen readers; the switch draws only its track
            themeSwitch.FitWidth();
            themeSwitch.Checked = T.Dark;
            themeSwitch.CheckedChanged += delegate { app.SetDarkTheme(themeSwitch.Checked); };
            tips.SetToolTip(themeSwitch, S.DarkTheme);
            Controls.AddRange(new Control[] { lblBadge, btnSettings, btnAbout, toolbarDivider, themeIcon, themeSwitch });

            // Status card.
            lblCaption = Ui.Label("", Ui.Body, Theme.RoleSub);
            lblRange = Ui.Inline("", Ui.Title, null);
            lblCountdown = Ui.Inline("", Ui.Body, Theme.RoleWarn);
            tabToday.Checked = true;
            tabToday.CheckedChanged += delegate
            {
                showTomorrow = !tabToday.Checked;
                ShowDay();
            };
            swatchOff.Tag = Theme.RoleStripOff;
            swatchOn.Tag = Theme.RoleStripOn;
            lblLegendOff = Ui.Inline(S.LegendOff, Ui.Caption, Theme.RoleSub);
            lblLegendOn = Ui.Inline(S.LegendOn, Ui.Caption, Theme.RoleSub);
            strip.Font = Ui.Caption;
            statusCard.Controls.AddRange(new Control[]
                { lblCaption, lblRange, lblCountdown, tabToday, tabTomorrow, swatchOff, lblLegendOff, swatchOn, lblLegendOn, strip });
            Controls.Add(statusCard);

            lblStatus = Ui.Label("", Ui.Caption, Theme.RoleSub);
            Controls.Add(lblStatus);

            // Site notice card.
            noticeIcon = Ui.Glyph(Ui.GlyphInfo, null);
            lblNoticeTitle = Ui.Label(S.NoticeTitle, Ui.BodyStrong, null);
            lblNotice = Ui.Label("", Ui.Body, null);
            noticeCard.Controls.AddRange(new Control[] { noticeIcon, lblNoticeTitle, lblNotice });
            Controls.Add(noticeCard);

            // Links.
            iconSchedules = Ui.Glyph(Ui.GlyphCalendar, null);
            lnkSchedules = Ui.Link(S.LinkSchedules, Ui.Body, AppInfo.SchedulesUrl);
            iconFacebook = Ui.Glyph(Ui.GlyphOpen, null);
            lnkFacebook = Ui.Link(S.LinkFacebook, Ui.Body, AppInfo.FacebookUrl);
            Controls.AddRange(new Control[] { iconSchedules, lnkSchedules, iconFacebook, lnkFacebook });

            ClientSize = new Size(Ui.S(Width96), Ui.S(400));
            ApplyTheme(T);
            ResumeLayout(false);
        }

        /// <summary>Recolours the window in place.</summary>
        public void ApplyTheme(Theme theme)
        {
            T = theme;
            SuspendLayout();
            T.Apply(this);
            themeSwitch.Checked = T.Dark;
            if (Ui.Icons != null) themeIcon.Text = T.Dark ? Ui.GlyphMoon : Ui.GlyphSun;
            T.ApplyFrame(this);
            if (lastView != null) ShowStatus(lastView);   // keeps the warning colours of the status labels
            ResumeLayout(false);
            Invalidate(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            T.ApplyFrame(this);
        }

        // ---- status ------------------------------------------------------------

        public void ShowStatus(StatusView v)
        {
            lastView = v;
            lblBadge.Text = v.QueueBadge;
            lblBadge.ForeColor = v.QueueMissing ? T.Warn : T.Text;
            lblCaption.Text = v.Caption;
            lblRange.Text = v.Range;
            lblCountdown.Text = v.Countdown;
            lblCountdown.ForeColor = v.CountdownIsNote ? T.Sub : T.Warn;
            tabToday.Text = S.TabToday + ", " + v.Today.ToString("dd.MM", System.Globalization.CultureInfo.InvariantCulture);
            tabTomorrow.Text = S.TabTomorrow + ", " + v.Today.AddDays(1).ToString("dd.MM", System.Globalization.CultureInfo.InvariantCulture);
            tabToday.FitWidth();
            tabTomorrow.FitWidth();
            ShowDay();
            lblStatus.Text = v.StatusLine;
            lblStatus.ForeColor = v.StatusIsError ? T.Warn : T.Sub;
            lblNotice.Text = v.Notice ?? "";
            hasNotice = !string.IsNullOrEmpty(v.Notice);
            noticeCard.Visible = hasNotice;
            tips.SetToolTip(lblNotice, v.Notice ?? "");
            Arrange();
        }

        /// <summary>The strip shows the day of the selected tab; "now" is marked on today only.</summary>
        private void ShowDay()
        {
            if (lastView == null) return;
            var slots = showTomorrow ? lastView.TomorrowSlots : lastView.TodaySlots;
            strip.SetData(slots, showTomorrow ? -1 : lastView.NowFraction);
            strip.Message = slots != null ? null : (showTomorrow ? S.StripTomorrowNotPublished : S.NoGridForToday);
            strip.AccessibleName = S.StripDescription(slots, showTomorrow);
        }

        /// <summary>Places every control top-down in real pixels and fits the window height to the content.</summary>
        private void Arrange()
        {
            SuspendLayout();
            int margin = Ui.S(16), width = Ui.S(Width96) - 2 * margin;

            // Toolbar: queue on the left; gear, "?", divider, sun or moon, and switch on the right.
            int top = Ui.S(8), row = Ui.S(36);
            themeSwitch.Location = new Point(margin + width - themeSwitch.Width + Ui.S(3), top + (row - themeSwitch.Height) / 2);
            themeIcon.Location = new Point(themeSwitch.Left - themeIcon.Width, top + (row - themeIcon.Height) / 2);
            toolbarDivider.SetBounds(themeIcon.Left - Ui.S(12), top + Ui.S(8), 1, row - Ui.S(16));
            btnAbout.Location = new Point(toolbarDivider.Left - Ui.S(8) - btnAbout.Width, top);
            btnSettings.Location = new Point(btnAbout.Left - Ui.S(4) - btnSettings.Width, top);
            lblBadge.Location = new Point(margin, top + (row - lblBadge.Height) / 2);

            // Status card.
            int pad = Ui.S(16), inner = width - 2 * pad;
            int y = Ui.Fit(lblCaption, pad, pad, inner);
            lblRange.Location = new Point(pad - Ui.S(1), y);
            int gap = Ui.S(12);
            if (lblRange.Right + gap + lblCountdown.Width <= pad + inner)
            {
                // Same baseline as the large time: both bottoms minus their descent (about a fifth of the height).
                lblCountdown.Location = new Point(lblRange.Right + gap,
                    lblRange.Bottom - lblRange.Height / 5 - (lblCountdown.Height - lblCountdown.Height / 5));
                y = lblRange.Bottom;
            }
            else
            {
                lblCountdown.Location = new Point(pad, lblRange.Bottom);
                y = lblCountdown.Bottom;
            }
            // Day tabs (text aligned with the card's content edge) and the legend on the right.
            y += Ui.S(8);
            tabToday.Location = new Point(pad - Ui.S(8), y);
            tabTomorrow.Location = new Point(tabToday.Right + Ui.S(4), y);
            int sw = Ui.S(10), legendTop = y + (tabToday.Height - lblLegendOn.Height) / 2;
            lblLegendOn.Location = new Point(pad + inner - lblLegendOn.Width, legendTop);
            swatchOn.SetBounds(lblLegendOn.Left - Ui.S(6) - sw, legendTop + (lblLegendOn.Height - sw) / 2, sw, sw);
            lblLegendOff.Location = new Point(swatchOn.Left - Ui.S(12) - lblLegendOff.Width, legendTop);
            swatchOff.SetBounds(lblLegendOff.Left - Ui.S(6) - sw, swatchOn.Top, sw, sw);
            y = tabToday.Bottom + Ui.S(8);
            strip.SetBounds(pad, y, inner, Ui.S(46));
            statusCard.SetBounds(margin, top + row + Ui.S(8), width, strip.Bottom + pad);

            y = Ui.Fit(lblStatus, margin + Ui.S(2), statusCard.Bottom + Ui.S(8), width - Ui.S(4));

            // Site notice.
            if (hasNotice)
            {
                int left = pad + noticeIcon.Width + Ui.S(12);
                noticeIcon.Location = new Point(pad, Ui.S(14));
                int ny = Ui.Fit(lblNoticeTitle, left, Ui.S(12), width - left - pad);
                ny = Ui.Fit(lblNotice, left, ny + Ui.S(2), width - left - pad);
                noticeCard.SetBounds(margin, y + Ui.S(8), width, ny + Ui.S(12));
                y = noticeCard.Bottom;
            }

            // Links on one line when they fit, otherwise one per line.
            y += Ui.S(12);
            int x = margin + Ui.S(2);
            PlaceLink(iconSchedules, lnkSchedules, x, y);
            x = lnkSchedules.Right + Ui.S(24);
            if (x + iconFacebook.Width + Ui.S(6) + lnkFacebook.Width > margin + width)
            {
                x = margin + Ui.S(2);
                y = lnkSchedules.Bottom + Ui.S(4);
            }
            PlaceLink(iconFacebook, lnkFacebook, x, y);

            ClientSize = new Size(Ui.S(Width96), lnkFacebook.Bottom + margin);
            ResumeLayout(false);
        }

        private static void PlaceLink(Label icon, LinkLabel link, int x, int y)
        {
            icon.Location = new Point(x, y + (link.Height - icon.Height) / 2);
            link.Location = new Point(icon.Right + Ui.S(6), y);
        }

        // ---- window behaviour --------------------------------------------------

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();    // the app keeps running in the tray; the window is rebuilt when opened again
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

    /// <summary>Today's 48 half-hour slots: red = no power, with a marker at the current time.</summary>
    internal sealed class DayStrip : Control
    {
        private Color Off = Color.FromArgb(0xC4, 0x2B, 0x1C);
        private Color On = Color.FromArgb(0xE3, 0xE3, 0xE3);
        private Color Unknown = Color.FromArgb(0xEC, 0xEC, 0xEC);
        private Color Marker = SystemColors.ControlText;
        private Color TickColour = SystemColors.GrayText;

        private bool[] slots;
        private double now = -1;

        /// <summary>Drawn over the empty strip when the day has no grid ("Графіка на завтра ще немає").</summary>
        public string Message
        {
            get { return message; }
            set
            {
                message = value;
                Invalidate();
            }
        }

        private string message;

        public DayStrip()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.Graphic;
        }

        public void SetTheme(Theme t)
        {
            Off = t.StripOff;
            On = t.StripOn;
            Unknown = t.StripUnknown;
            Marker = t.Text;
            TickColour = t.Sub;
            ForeColor = t.Sub;
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
            float barTop = Ui.S(3);
            float barHeight = Ui.S(20);
            float width = ClientSize.Width;

            // 24 hour blocks with a gap between hours; the two half-hours of an hour sit together.
            float hourGap = Ui.S(3);
            float halfGap = Math.Max(1, Ui.S(1));
            float hourWidth = (width - 23 * hourGap) / 24;
            float halfWidth = (hourWidth - halfGap) / 2;

            if (slots == null && !string.IsNullOrEmpty(message))
            {
                // No grid for this day: one plain bar carrying the message instead of 48 empty blocks.
                using (var brush = new SolidBrush(Unknown)) g.FillRectangle(brush, 0, barTop, width, barHeight);
                TextRenderer.DrawText(g, message, Font, new Rectangle(0, (int)barTop, (int)width, (int)barHeight), ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }
            for (int h = 0; h < 24 && !(slots == null && !string.IsNullOrEmpty(message)); h++)
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
                using (var pen = new Pen(Marker, Ui.S(2)))
                {
                    g.DrawLine(pen, x, 0, x, barTop + barHeight + Ui.S(3));
                }
            }

            // Tick and label every 2 hours, centred on the gap between hour blocks.
            float tickTop = barTop + barHeight + Ui.S(2);
            int textTop = (int)(tickTop + Ui.S(5));
            using (var tick = new Pen(TickColour, 1))
            {
                for (int t = 0; t <= 24; t += 2)
                {
                    float x = t == 0 ? 0 : (t == 24 ? width - 1 : HourLeft(t, hourWidth, hourGap) - hourGap / 2);
                    g.DrawLine(tick, x, tickTop, x, tickTop + Ui.S(4));

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
