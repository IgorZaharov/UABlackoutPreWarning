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
    /// The Preferences window (opened with the gear button), laid out like Windows 11 Settings: sections of
    /// cards, each with a title, a short hint and its control on the right. Nothing is applied until Save.
    /// Built in code (no designer files); laid out in real pixels by <see cref="Arrange"/>.
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private const int Width96 = 560;
        private static readonly string[] LanguageValues =
            { AppSettings.LanguageAuto, AppSettings.LanguageUkrainian, AppSettings.LanguageEnglish };

        private readonly Strings S;
        private readonly TrayContext app;
        private Theme T;

        private readonly TextBox txtUrl = new TextBox();
        private readonly FieldHost urlField;
        private readonly Label lblUrlTitle, lblUrlError;
        private readonly Card urlCard = new Card();
        private readonly FluentCombo cmbQueue = new FluentCombo(Ui.S(96));
        private readonly LinkLabel lnkQueueHelp;
        private readonly NumericUpDown numLead = new NumericUpDown();
        private readonly NumericUpDown numRefresh = new NumericUpDown();
        private readonly FluentButton btnTest = new FluentButton(ButtonKind.Standard);
        private readonly FluentCombo cmbLanguage = new FluentCombo(Ui.S(180));
        private readonly ToggleSwitch chkAutostart = new ToggleSwitch();
        private readonly FluentButton btnDefaults = new FluentButton(ButtonKind.Standard);
        private readonly LinkLabel lnkAbout;
        private readonly StatusLabel lblSaved = new StatusLabel();
        private readonly FluentButton btnCheck = new FluentButton(ButtonKind.Standard);
        private readonly FluentButton btnSave = new FluentButton(ButtonKind.Accent);
        private readonly Timer savedTimer = new Timer();
        private bool urlErrorShown;     // Control.Visible reads false until the window is shown, so keep our own flag

        /// <summary>The window's blocks top-down: a section title (Label) or a row (Row).</summary>
        private readonly List<object> blocks = new List<object>();

        private sealed class Row
        {
            public Card Card;
            public Label Title, Hint;
            public LinkLabel Link;
            public Control Control;
        }

        public SettingsForm(Strings strings, TrayContext context, Theme theme)
        {
            S = strings;
            app = context;
            T = theme;

            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            Font = Ui.Body;
            Text = S.SettingsTitle;
            Icon = AppInfo.LoadIcon(new Size(32, 32));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;

            // Schedule.
            Section(S.SectionSchedule);
            lblUrlTitle = Ui.Label(S.LabelUrl, Ui.Body, null);
            txtUrl.AccessibleName = S.LabelUrl;
            urlField = new FieldHost(txtUrl, Ui.S(100));
            lblUrlError = Ui.Label("", Ui.Caption, Theme.RoleWarn);   // shown after a failed Save (SetUrlError)
            lblUrlError.Visible = false;
            urlCard.Controls.AddRange(new Control[] { lblUrlTitle, urlField, lblUrlError });
            Controls.Add(urlCard);
            blocks.Add(urlCard);

            cmbQueue.AccessibleName = S.LabelQueue;
            lnkQueueHelp = Ui.Link(S.QueueHelpLink, Ui.Caption, null);
            lnkQueueHelp.LinkClicked += delegate { ShowQueueHelp(); };
            AddRow(S.LabelQueue, S.QueueHint, cmbQueue, lnkQueueHelp);

            // Notifications.
            Section(S.SectionNotifications);
            AddRow(S.LabelLead, S.LeadHint, MinutesBox(numLead, AppSettings.MinLead, AppSettings.MaxLead, S.LabelLead), null);
            AddRow(S.LabelRefresh, S.RefreshHint, MinutesBox(numRefresh, AppSettings.MinRefresh, AppSettings.MaxRefresh, S.LabelRefresh), null);
            btnTest.Text = S.ButtonTest;
            btnTest.FitWidth(96);
            btnTest.Click += delegate { app.SendTestNotification(); };
            AddRow(S.TestTitle, S.TestHint, btnTest, null);

            // System.
            Section(S.SectionSystem);
            cmbLanguage.AccessibleName = S.LabelLanguage;
            cmbLanguage.Items.AddRange(new object[] { S.LanguageAuto, Strings.UkrainianName, Strings.EnglishName });
            AddRow(S.LabelLanguage, S.LanguageHint, cmbLanguage, null);
            chkAutostart.Text = S.StartWithWindows;
            chkAutostart.OnText = S.SwitchOn;
            chkAutostart.OffText = S.SwitchOff;
            chkAutostart.FitWidth();
            var autostartRow = AddRow(S.StartWithWindows, null, chkAutostart, null);
            autostartRow.Title.Click += delegate { chkAutostart.Checked = !chkAutostart.Checked; };
            btnDefaults.Text = S.ButtonDefaults;
            btnDefaults.FitWidth(96);
            btnDefaults.Click += delegate { RestoreDefaults(); };
            AddRow(S.DefaultsTitle, S.DefaultsHint, btnDefaults, null);

            // Footer: About link and "Saved" on the left, Check now and Save on the right.
            lnkAbout = Ui.Link(S.MenuAbout, Ui.Body, null);
            lnkAbout.LinkClicked += delegate { app.ShowAbout(); };
            lblSaved.Font = Ui.Body;
            lblSaved.Tag = Theme.RoleSub;
            lblSaved.UseMnemonic = false;
            lblSaved.TextAlign = ContentAlignment.MiddleRight;
            lblSaved.AutoEllipsis = true;
            btnCheck.Text = S.ButtonCheckNow;
            btnCheck.FitWidth(96);
            btnCheck.Click += delegate { app.RefreshNow(); };
            btnSave.Text = S.ButtonSave;
            btnSave.FitWidth(96);
            btnSave.Click += delegate { Save(); };
            Controls.AddRange(new Control[] { lnkAbout, lblSaved, btnCheck, btnSave });
            AcceptButton = btnSave;

            savedTimer.Interval = 3000;
            savedTimer.Tick += delegate
            {
                savedTimer.Stop();
                lblSaved.Text = "";
            };

            ClientSize = new Size(Ui.S(Width96), Ui.S(600));
            ApplyTheme(T);
            Arrange();
            ResumeLayout(false);
        }

        private void Section(string title)
        {
            var l = Ui.Label(title, Ui.BodyStrong, null);
            Controls.Add(l);
            blocks.Add(l);
        }

        private Row AddRow(string title, string hint, Control control, LinkLabel link)
        {
            var row = new Row();
            row.Card = new Card();
            row.Title = Ui.Label(title, Ui.Body, null);
            row.Card.Controls.Add(row.Title);
            if (hint != null)
            {
                row.Hint = Ui.Label(hint, Ui.Caption, Theme.RoleSub);
                row.Card.Controls.Add(row.Hint);
            }
            if (link != null)
            {
                row.Link = link;
                row.Card.Controls.Add(link);
            }
            row.Control = control;
            row.Card.Controls.Add(control);
            Controls.Add(row.Card);
            blocks.Add(row);
            return row;
        }

        /// <summary>A number field (no spin buttons; arrow keys and the wheel change it) followed by "хв".</summary>
        private Panel MinutesBox(NumericUpDown n, int min, int max, string name)
        {
            n.Minimum = min;
            n.Maximum = max;
            n.TextAlign = HorizontalAlignment.Right;
            n.AccessibleName = name;
            n.AccessibleDescription = S.Minutes;
            var field = new FieldHost(n, Ui.S(72));
            var unit = Ui.Inline(S.Minutes, Ui.Body, Theme.RoleSub);
            var box = new Panel();
            box.Controls.Add(field);
            box.Controls.Add(unit);
            unit.Location = new Point(field.Right + Ui.S(8), (field.Height - unit.Height) / 2);
            box.Size = new Size(field.Width + Ui.S(8) + Math.Max(unit.Width, Ui.S(24)), field.Height);
            return box;
        }

        /// <summary>Places the sections and cards top-down in real pixels and fits the window height.</summary>
        private void Arrange()
        {
            SuspendLayout();
            int margin = Ui.S(16), width = Ui.S(Width96) - 2 * margin, pad = Ui.S(16), inner = width - 2 * pad;
            int y = Ui.S(4);
            object previous = null;
            foreach (var block in blocks)
            {
                var title = block as Label;
                if (title != null)
                {
                    y = Ui.Fit(title, margin + Ui.S(2), y + (previous == null ? Ui.S(4) : Ui.S(16)), width - Ui.S(4)) + Ui.S(6);
                }
                else if (block == urlCard)
                {
                    int cy = Ui.Fit(lblUrlTitle, pad, Ui.S(12), inner);
                    urlField.SetBounds(pad, cy + Ui.S(8), inner, urlField.Height);
                    cy = urlField.Bottom;
                    if (urlErrorShown) cy = Ui.Fit(lblUrlError, pad, cy + Ui.S(6), inner);
                    urlCard.SetBounds(margin, y, width, cy + Ui.S(12));
                    y = urlCard.Bottom + Ui.S(4);
                }
                else
                {
                    var row = (Row)block;
                    int textWidth = inner - row.Control.Width - Ui.S(16);
                    var titleHeight = Ui.TextHeight(row.Title.Text, row.Title.Font, textWidth);
                    int textHeight = titleHeight;
                    if (row.Hint != null) textHeight += Ui.TextHeight(row.Hint.Text, row.Hint.Font, textWidth);
                    if (row.Link != null) textHeight += Ui.S(2) + row.Link.Height;
                    int content = Math.Max(textHeight, row.Control.Height);
                    int height = Math.Max(Ui.S(60), content + Ui.S(24));
                    int ty = (height - textHeight) / 2;
                    ty = Ui.Fit(row.Title, pad, ty, textWidth);
                    if (row.Hint != null) ty = Ui.Fit(row.Hint, pad, ty, textWidth);
                    if (row.Link != null) row.Link.Location = new Point(pad, ty + Ui.S(2));
                    row.Control.Location = new Point(pad + inner - row.Control.Width, (height - row.Control.Height) / 2);
                    row.Card.SetBounds(margin, y, width, height);
                    y = row.Card.Bottom + Ui.S(4);
                }
                previous = block;
            }

            // Footer.
            int top = y + Ui.S(16);
            btnSave.Location = new Point(margin + width - btnSave.Width, top);
            btnCheck.Location = new Point(btnSave.Left - Ui.S(8) - btnCheck.Width, top);
            lnkAbout.Location = new Point(margin + Ui.S(2), top + (btnSave.Height - lnkAbout.Height) / 2);
            int savedLeft = lnkAbout.Right + Ui.S(16);
            lblSaved.SetBounds(savedLeft, top, Math.Max(0, btnCheck.Left - Ui.S(12) - savedLeft), btnSave.Height);
            ClientSize = new Size(Ui.S(Width96), btnSave.Bottom + margin);
            ResumeLayout(false);
        }

        // ---- theme -------------------------------------------------------------

        /// <summary>Recolours the window in place; field values and unsaved edits stay as they are.</summary>
        public void ApplyTheme(Theme theme)
        {
            T = theme;
            SuspendLayout();
            T.Apply(this);
            T.ApplyFrame(this);
            ResumeLayout(false);
            Invalidate(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            T.ApplyFrame(this);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Arrange();      // the drop-downs know their final height only once their handles exist
        }

        private void ShowQueueHelp()
        {
            using (var help = new QueueHelpForm(S, T))
            {
                help.ShowDialog(this);
            }
        }

        // ---- data in and out ---------------------------------------------------

        public void LoadSettings(AppSettings s)
        {
            txtUrl.Text = s.ScheduleUrl;
            SetUrlError(null);
            SetQueueItems(s.KnownQueueList(), s.Queue);
            numLead.Value = Clamp(s.LeadMinutes, numLead);
            numRefresh.Value = Clamp(s.RefreshMinutes, numRefresh);
            cmbLanguage.SelectedIndex = Math.Max(0, Array.IndexOf(LanguageValues, s.Language));
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
            SetUrlError(null);
            if (!cmbQueue.Items.Contains(d.Queue)) cmbQueue.Items.Add(d.Queue);
            cmbQueue.SelectedItem = d.Queue;
            numLead.Value = Clamp(d.LeadMinutes, numLead);
            numRefresh.Value = Clamp(d.RefreshMinutes, numRefresh);
            cmbLanguage.SelectedIndex = 0;
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
                SetUrlError(S.InvalidUrl);
                txtUrl.Focus();
                return;
            }
            SetUrlError(null);

            var s = new AppSettings();
            s.ScheduleUrl = url;
            s.Queue = (cmbQueue.SelectedItem as string) ?? AppSettings.DefaultQueue;
            s.LeadMinutes = (int)numLead.Value;
            s.RefreshMinutes = (int)numRefresh.Value;
            s.Language = LanguageValues[Math.Max(0, cmbLanguage.SelectedIndex)];
            s.StartWithWindows = chkAutostart.Checked;
            app.ApplySettings(s);
            ShowSaved();
        }

        /// <summary>"Saved" for 3 seconds, announced to screen readers.</summary>
        public void ShowSaved()
        {
            ShowSavedText(S.Saved);
            savedTimer.Stop();
            savedTimer.Start();
        }

        /// <summary>After a rebuild (language change): keyboard focus back on Save, where the user left it.</summary>
        public void FocusSave()
        {
            Activate();
            btnSave.Focus();
        }

        private void ShowSavedText(string text)
        {
            lblSaved.Text = text;
            lblSaved.Announce();
        }

        /// <summary>
        /// Shows <paramref name="text"/> under the address field (null hides it) and makes it the field's
        /// accessible description, so it is read out when focus returns to the field.
        /// </summary>
        private void SetUrlError(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            lblUrlError.Text = show ? text : "";
            txtUrl.AccessibleDescription = show ? text : null;
            if (urlErrorShown == show) return;
            urlErrorShown = show;
            lblUrlError.Visible = show;
            Arrange();
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
            if (disposing) savedTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>A label whose text changes are announced by screen readers (a status message).</summary>
    internal sealed class StatusLabel : Label
    {
        public StatusLabel()
        {
            AccessibleRole = AccessibleRole.StatusBar;
        }

        public void Announce()
        {
            if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        }
    }
}
