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
    /// The settings window (opened with the gear button): schedule URL, queue, lead time, refresh
    /// interval, Start with Windows. Nothing is applied until Save. Built in code (no designer files).
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private readonly Strings S;
        private readonly TrayContext app;
        private Theme T;

        private readonly TextBox txtUrl = new TextBox();
        private readonly ComboBox cmbQueue = new ComboBox();
        private readonly NumericUpDown numLead = new NumericUpDown();
        private readonly NumericUpDown numRefresh = new NumericUpDown();
        private readonly CheckBox chkAutostart = new CheckBox();
        private readonly Button btnCheck = new Button();
        private readonly Button btnSave = new Button();
        private readonly Button btnDefaults = new Button();
        private readonly Button btnTest = new Button();
        private readonly Label lblSaved = new Label();
        private readonly LinkLabel lnkAbout = new LinkLabel();
        private readonly ErrorProvider errors = new ErrorProvider();
        private readonly ToolTip tips = new ToolTip();
        private readonly Timer savedTimer = new Timer();

        // 96-DPI layout units; Windows scales them with AutoScaleMode.Dpi.
        private const int GroupWidth = MainForm.GroupWidth;
        private const int Inner = MainForm.Inner;
        private const int Col2 = 152, Col3 = 292;

        public SettingsForm(Strings strings, TrayContext context, Theme theme)
        {
            S = strings;
            app = context;
            T = theme;

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = S.SettingsTitle;
            Icon = AppInfo.LoadIcon(new Size(32, 32));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            ClientSize = new Size(GroupWidth + 24, 8 + 186 + 12);

            Controls.Add(BuildGroup(new Point(12, 8)));

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

        private GroupBox BuildGroup(Point at)
        {
            var g = MainForm.Group(S.GroupSettings, at, 186);

            g.Controls.Add(MainForm.Caption(S.LabelUrl, 12, 22));
            txtUrl.SetBounds(12, 44, Inner, 23);
            g.Controls.Add(txtUrl);

            g.Controls.Add(MainForm.Caption(S.LabelQueue, 12, 78));
            g.Controls.Add(MainForm.Caption(S.LabelLead, Col2, 78));
            g.Controls.Add(MainForm.Caption(S.LabelRefresh, Col3, 78));

            cmbQueue.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbQueue.SetBounds(12, 96, 80, 23);
            cmbQueue.DrawItem += DrawQueueItem;
            g.Controls.Add(cmbQueue);

            SetupNumber(numLead, AppSettings.MinLead, AppSettings.MaxLead, Col2, 96);
            SetupNumber(numRefresh, AppSettings.MinRefresh, AppSettings.MaxRefresh, Col3, 96);
            g.Controls.Add(numLead);
            g.Controls.Add(numRefresh);
            g.Controls.Add(MainForm.Caption(S.Minutes, Col2 + 66, 99));
            g.Controls.Add(MainForm.Caption(S.Minutes, Col3 + 66, 99));

            chkAutostart.Text = S.StartWithWindows;
            // Auto-sized so the clickable area ends where the caption ends.
            chkAutostart.AutoSize = true;
            chkAutostart.Location = new Point(12, 134);
            g.Controls.Add(chkAutostart);

            // Puts the default values back into the fields; nothing is applied until Save.
            btnDefaults.Text = S.ButtonDefaults;
            btnDefaults.SetBounds(12 + Inner - 150, 128, 150, 28);
            btnDefaults.UseVisualStyleBackColor = true;
            btnDefaults.Click += delegate { RestoreDefaults(); };
            tips.SetToolTip(btnDefaults, S.DefaultsHint);
            g.Controls.Add(btnDefaults);

            // Second row of buttons sits below the checkbox row.
            btnSave.Text = S.ButtonSave;
            btnSave.Tag = Theme.RolePrimary;
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += delegate { Save(); };
            btnCheck.Text = S.ButtonCheckNow;
            btnCheck.UseVisualStyleBackColor = true;
            btnCheck.Click += delegate { app.RefreshNow(); };
            btnTest.Text = S.ButtonTest;
            btnTest.UseVisualStyleBackColor = true;
            btnTest.Click += delegate { app.SendTestNotification(); };
            tips.SetToolTip(btnTest, S.TestHint);
            g.Controls.Add(btnSave);
            g.Controls.Add(btnCheck);
            g.Controls.Add(btnTest);

            lnkAbout.Text = S.MenuAbout;
            lnkAbout.AutoSize = true;
            lnkAbout.LinkClicked += delegate { app.ShowAbout(); };
            g.Controls.Add(lnkAbout);

            lblSaved.Tag = Theme.RoleSub;
            g.Controls.Add(lblSaved);

            // Rows below the checkbox: buttons on the right, About link and "Saved" on the left.
            int buttonsTop = 128 + 28 + 12;
            btnSave.SetBounds(12 + Inner - 110, buttonsTop, 110, 30);
            btnCheck.SetBounds(btnSave.Left - 8 - 140, buttonsTop, 140, 30);
            btnTest.SetBounds(btnCheck.Left - 8 - 160, buttonsTop, 160, 30);
            lnkAbout.Location = new Point(12, buttonsTop + 8);
            // "Saved" / "Not saved yet": right-aligned in the gap between the checkbox and Restore defaults;
            // the final position is set in PlaceSavedLabel() from the real (scaled) control positions.
            lblSaved.SetBounds(212, 130, 150, 24);
            lblSaved.TextAlign = ContentAlignment.MiddleRight;
            lblSaved.AutoEllipsis = true;
            g.Height = buttonsTop + 30 + 16;
            ClientSize = new Size(ClientSize.Width, at.Y + g.Height + 12);
            return g;
        }

        private static void SetupNumber(NumericUpDown n, int min, int max, int x, int y)
        {
            n.Minimum = min;
            n.Maximum = max;
            n.TextAlign = HorizontalAlignment.Right;
            n.SetBounds(x, y, 60, 23);
        }

        // ---- theme -------------------------------------------------------------

        /// <summary>Recolours the window in place; field values and unsaved edits stay as they are.</summary>
        public void ApplyTheme(Theme theme)
        {
            T = theme;
            SuspendLayout();
            T.Apply(this);
            cmbQueue.DrawMode = T.Dark ? DrawMode.OwnerDrawFixed : DrawMode.Normal;
            MatchQueueHeight();
            Theme.ApplyTitleBar(this, T.Dark);
            ResumeLayout(true);
            Invalidate(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this, T.Dark);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            MatchQueueHeight();   // after DPI scaling, when the final control heights are known
            PlaceSavedLabel();
        }

        /// <summary>
        /// Puts the "Saved" label in the gap between the checkbox and Restore defaults, in real pixels,
        /// so it is never under the button whatever the display scaling.
        /// </summary>
        private void PlaceSavedLabel()
        {
            int gap = Font.Height / 2;
            int left = chkAutostart.Right + gap;
            int width = Math.Max(0, btnDefaults.Left - gap - left);
            lblSaved.Bounds = new Rectangle(left, btnDefaults.Top, width, btnDefaults.Height);
            lblSaved.BringToFront();
        }

        private void ShowSavedText(string text)
        {
            PlaceSavedLabel();
            lblSaved.Text = text;
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

        // ---- data in and out ---------------------------------------------------

        public void LoadSettings(AppSettings s)
        {
            txtUrl.Text = s.ScheduleUrl;
            errors.SetError(txtUrl, "");
            SetQueueItems(s.KnownQueueList(), s.Queue);
            numLead.Value = Clamp(s.LeadMinutes, numLead);
            numRefresh.Value = Clamp(s.RefreshMinutes, numRefresh);
            chkAutostart.Checked = s.StartWithWindows;
            lblSaved.Text = "";
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
            ShowSavedText(S.NotSavedYet);
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

            ShowSavedText(S.Saved);
            savedTimer.Stop();
            savedTimer.Start();
        }

        private static decimal Clamp(int value, NumericUpDown n)
        {
            return Math.Max(n.Minimum, Math.Min(n.Maximum, value));
        }

        // ---- window behaviour --------------------------------------------------

        /// <summary>Places the window over <paramref name="anchor"/> (or the screen centre) before it is shown.</summary>
        public void PlaceOver(Form anchor)
        {
            Rectangle area;
            if (anchor != null && anchor.Visible && anchor.WindowState == FormWindowState.Normal)
            {
                var b = anchor.Bounds;
                Location = new Point(b.Left + (b.Width - Width) / 2, b.Top + Math.Max(0, (b.Height - Height) / 3));
                area = Screen.FromControl(anchor).WorkingArea;
            }
            else
            {
                area = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
            }
            // Keep it fully on the screen.
            Left = Math.Max(area.Left, Math.Min(Left, area.Right - Width));
            Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
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
}
