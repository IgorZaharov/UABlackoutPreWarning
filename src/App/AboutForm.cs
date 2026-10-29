// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using UABlackoutPreWarning.Core;

namespace UABlackoutPreWarning
{
    /// <summary>Small About window: name, version, description, links to the author, the manual and Issues.</summary>
    internal sealed class AboutForm : Form
    {
        private const int Width96 = 480;

        private readonly Label lblDescription = new Label();
        private readonly Label lblAuthor = new Label();
        private readonly LinkLabel lnkAuthor = new LinkLabel();
        private readonly Label lblManual = new Label();
        private readonly LinkLabel lnkManual = new LinkLabel();
        private readonly Label lblIssues = new Label();
        private readonly LinkLabel lnkIssues = new LinkLabel();
        private readonly Button btnOk = new Button();

        private Theme theme;

        public AboutForm(Strings S, Theme t)
        {
            theme = t;
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = S.AboutTitle;
            Icon = AppInfo.LoadIcon(new Size(32, 32));
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Width96, 220);

            var picture = new PictureBox();
            picture.SetBounds(20, 18, 48, 48);
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            using (var icon = AppInfo.LoadIcon(new Size(64, 64))) picture.Image = icon.ToBitmap();
            Controls.Add(picture);

            var name = new Label();
            name.Text = S.AppName;
            name.AutoSize = true;
            name.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
            name.Location = new Point(82, 20);
            Controls.Add(name);

            var version = new Label();
            version.Text = string.Format(S.VersionFormat, AppInfo.Version);
            version.AutoSize = true;
            version.Tag = Theme.RoleSub;
            version.Location = new Point(84, 46);
            Controls.Add(version);

            lblDescription.Text = S.AboutDescription;
            lblDescription.UseMnemonic = false;
            lblDescription.SetBounds(20, 82, Width96 - 40, 60);
            Controls.Add(lblDescription);

            lblAuthor.Text = S.AuthorLabel;

            AddLink(lblAuthor, lnkAuthor, AppInfo.AuthorText, AppInfo.AuthorUrl);
            lblManual.Text = S.ManualLabel;
            AddLink(lblManual, lnkManual, AppInfo.ManualText, AppInfo.ManualUrl);
            lblIssues.Text = S.IssuesLabel;
            AddLink(lblIssues, lnkIssues, AppInfo.IssuesText, AppInfo.IssuesUrl);

            btnOk.Text = S.ButtonOk;
            btnOk.SetBounds(Width96 - 20 - 96, 176, 96, 30);
            btnOk.UseVisualStyleBackColor = true;
            btnOk.Click += delegate { Close(); };
            Controls.Add(btnOk);
            AcceptButton = btnOk;
            CancelButton = btnOk;
            theme.Apply(this);

            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>Fits the description to its text and places the author line and OK button below it.</summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            int gap = Font.Height / 2;

            // Size from the window's real inner width (already DPI-scaled), so the text always wraps inside it.
            int margin = lblDescription.Left;
            lblDescription.Width = ClientSize.Width - 2 * margin;
            btnOk.Left = ClientSize.Width - margin - btnOk.Width;

            var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
            var needed = TextRenderer.MeasureText(lblDescription.Text, lblDescription.Font,
                new Size(lblDescription.Width, int.MaxValue), flags);
            lblDescription.Height = needed.Height;

            // Author, manual and issues rows; the links share one column after the widest caption.
            var captions = new[] { lblAuthor, lblManual, lblIssues };
            var links = new[] { lnkAuthor, lnkManual, lnkIssues };
            int linkLeft = 0;
            foreach (var c in captions) linkLeft = Math.Max(linkLeft, c.Right + gap / 2);
            int top = lblDescription.Bottom + gap;
            for (int i = 0; i < captions.Length; i++)
            {
                captions[i].Top = top;
                links[i].Top = top;
                links[i].Left = linkLeft;
                top = captions[i].Bottom + (i == 0 ? gap : gap / 2);
            }

            // OK sits at the right of the last two rows when they leave room, otherwise below them.
            int linksRight = Math.Max(lnkManual.Right, lnkIssues.Right);
            if (linksRight + 2 * gap <= btnOk.Left) btnOk.Top = lnkIssues.Bottom - btnOk.Height;
            else btnOk.Top = lnkIssues.Bottom + gap * 2;
            if (btnOk.Top < lnkManual.Top) btnOk.Top = lnkIssues.Bottom + gap * 2;
            ClientSize = new Size(ClientSize.Width, Math.Max(btnOk.Bottom, lnkIssues.Bottom) + gap * 2);
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            theme.Apply(this);
            Theme.ApplyTitleBar(this, theme.Dark);
            Invalidate(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this, theme.Dark);
        }

        private void AddLink(Label caption, LinkLabel link, string text, string url)
        {
            caption.AutoSize = true;
            caption.Location = new Point(20, 150);
            Controls.Add(caption);

            link.Text = text;
            link.AutoSize = true;
            link.UseMnemonic = false;
            link.Location = new Point(80, 150);
            link.LinkClicked += delegate { OpenPage(url); };
            Controls.Add(link);
        }

        private void OpenPage(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
