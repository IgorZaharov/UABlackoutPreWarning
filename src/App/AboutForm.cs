// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using UABlackoutPreWarning.Core;

namespace UABlackoutPreWarning
{
    /// <summary>
    /// About window: name, version, description, links to the author, the manual and Issues, and the
    /// "Donations" section from donations.json (read from GitHub at start-up; hidden until it arrives).
    /// </summary>
    internal sealed class AboutForm : Form
    {
        private const int Width96 = 480;

        private readonly Strings S;
        private Theme theme;

        private readonly PictureBox picture = new PictureBox();
        private readonly Label lblName, lblVersion, lblDescription;
        private readonly Card linksCard = new Card();
        private readonly List<Control[]> linkRows = new List<Control[]>();
        private readonly List<Panel> linkDividers = new List<Panel>();
        private readonly Label lblDonations;
        private readonly Card donationsCard = new Card();
        private readonly List<DonationRow> donationRows = new List<DonationRow>();
        private readonly FluentButton btnClose = new FluentButton(ButtonKind.Standard);
        private bool hasDonations;      // Control.Visible reads false until the window is shown, so keep our own flag

        private sealed class DonationRow
        {
            public PictureBox Icon;
            public LinkLabel Title;
            public Label Host;
            public Panel Divider;
            public DonationEntry Entry;
        }

        public AboutForm(Strings strings, Theme t, List<DonationEntry> donations)
        {
            S = strings;
            theme = t;
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            Font = Ui.Body;
            Text = S.AboutTitle;
            Icon = AppInfo.LoadIcon(new Size(32, 32));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;

            picture.SizeMode = PictureBoxSizeMode.Zoom;
            using (var icon = AppInfo.LoadIcon(new Size(64, 64))) picture.Image = icon.ToBitmap();
            picture.AccessibleRole = AccessibleRole.Graphic;
            picture.AccessibleName = "";
            lblName = Ui.Inline(S.AppName, Ui.Subtitle, null);
            lblVersion = Ui.Inline(string.Format(S.VersionFormat, AppInfo.Version), Ui.Caption, Theme.RoleSub);
            lblDescription = Ui.Label(S.AboutDescription, Ui.Body, null);
            Controls.AddRange(new Control[] { picture, lblName, lblVersion, lblDescription, linksCard });

            AddLinkRow(S.AuthorLabel, AppInfo.AuthorText, AppInfo.AuthorUrl);
            AddLinkRow(S.ManualLabel, S.ManualLink, AppInfo.ManualUrl);
            AddLinkRow(S.IssuesLabel, S.IssuesLink, AppInfo.IssuesUrl);

            lblDonations = Ui.Label(Strings.DonationsTitle, Ui.BodyStrong, null);
            Controls.Add(lblDonations);
            Controls.Add(donationsCard);

            btnClose.Text = S.ButtonClose;
            btnClose.FitWidth(96);
            btnClose.Click += delegate { Close(); };
            Controls.Add(btnClose);
            AcceptButton = btnClose;
            CancelButton = btnClose;

            ClientSize = new Size(Ui.S(Width96), Ui.S(400));
            ShowDonations(donations);
            ResumeLayout(false);
        }

        private void AddLinkRow(string caption, string text, string url)
        {
            var label = Ui.Label(caption, Ui.Body, Theme.RoleSub);
            var link = Ui.Link(text, Ui.Body, url);
            if (linkRows.Count > 0)
            {
                var divider = new Panel();
                divider.Tag = Theme.RoleDivider;
                linkDividers.Add(divider);
                linksCard.Controls.Add(divider);
            }
            linksCard.Controls.Add(label);
            linksCard.Controls.Add(link);
            linkRows.Add(new Control[] { label, link });
        }

        /// <summary>Fills the "Donations" section (hidden when the list is empty or not loaded yet).</summary>
        public void ShowDonations(List<DonationEntry> donations)
        {
            SuspendLayout();
            foreach (var row in donationRows)
            {
                if (row.Icon.Image != null && row.Entry.Icon == null) row.Icon.Image.Dispose();   // our letter tile
                row.Icon.Image = null;
            }
            var old = new List<Control>();
            foreach (Control c in donationsCard.Controls) old.Add(c);
            donationsCard.Controls.Clear();
            foreach (var c in old) c.Dispose();
            donationRows.Clear();
            if (donations != null)
            {
                foreach (var entry in donations)
                {
                    var row = new DonationRow();
                    row.Entry = entry;
                    row.Icon = new PictureBox();
                    row.Icon.SizeMode = PictureBoxSizeMode.Zoom;
                    row.Icon.AccessibleRole = AccessibleRole.Graphic;
                    row.Icon.AccessibleName = "";
                    row.Title = Ui.Link(entry.Link.Title, Ui.Body, entry.Link.Url);
                    row.Title.AccessibleDescription = entry.Link.Host;
                    row.Host = Ui.Inline(entry.Link.Host, Ui.Caption, Theme.RoleSub);
                    if (donationRows.Count > 0)
                    {
                        row.Divider = new Panel();
                        row.Divider.Tag = Theme.RoleDivider;
                        donationsCard.Controls.Add(row.Divider);
                    }
                    donationsCard.Controls.Add(row.Icon);
                    donationsCard.Controls.Add(row.Title);
                    donationsCard.Controls.Add(row.Host);
                    donationRows.Add(row);
                }
            }
            hasDonations = donationRows.Count > 0;
            lblDonations.Visible = hasDonations;
            donationsCard.Visible = hasDonations;
            ApplyTheme(theme);
            Arrange();
            ResumeLayout(false);
        }

        /// <summary>Places everything top-down in real pixels and fits the window height.</summary>
        private void Arrange()
        {
            int margin = Ui.S(24), width = Ui.S(Width96) - 2 * margin, gap = Ui.S(16);

            picture.SetBounds(margin, Ui.S(16), Ui.S(48), Ui.S(48));
            int textLeft = picture.Right + Ui.S(16);
            int nameTop = picture.Top + (picture.Height - lblName.Height - lblVersion.Height) / 2;
            lblName.Location = new Point(textLeft - Ui.S(1), nameTop);
            lblVersion.Location = new Point(textLeft, lblName.Bottom);
            int y = Ui.Fit(lblDescription, margin, picture.Bottom + gap, width);

            // Caption column as wide as the widest caption.
            int pad = Ui.S(16), captionWidth = 0;
            foreach (var row in linkRows) captionWidth = Math.Max(captionWidth, Ui.TextWidth(row[0].Text, row[0].Font));
            captionWidth += Ui.S(16);
            int ry = 0, rowHeight = Ui.S(40);
            for (int i = 0; i < linkRows.Count; i++)
            {
                if (i > 0) linkDividers[i - 1].SetBounds(0, ry, width, 1);
                var caption = (Label)linkRows[i][0];
                var link = linkRows[i][1];
                caption.SetBounds(pad, ry + (rowHeight - caption.Font.Height) / 2, captionWidth, caption.Font.Height + Ui.S(2));
                link.Location = new Point(pad + captionWidth, ry + (rowHeight - link.Height) / 2);
                ry += rowHeight;
            }
            linksCard.SetBounds(margin, y + gap, width, ry);
            y = linksCard.Bottom;

            if (hasDonations)
            {
                y = Ui.Fit(lblDonations, margin, y + gap, width);
                int dy = 0, drow = Ui.S(52), icon = Ui.S(16);
                foreach (var row in donationRows)
                {
                    if (row.Divider != null) row.Divider.SetBounds(0, dy, width, 1);
                    row.Icon.SetBounds(pad, dy + (drow - icon) / 2, icon, icon);
                    int textHeight = row.Title.Height + row.Host.Height;
                    row.Title.Location = new Point(row.Icon.Right + Ui.S(12), dy + (drow - textHeight) / 2);
                    row.Host.Location = new Point(row.Title.Left + Ui.S(1), row.Title.Bottom);
                    dy += drow;
                }
                donationsCard.SetBounds(margin, y + Ui.S(8), width, dy);
                y = donationsCard.Bottom;
            }

            btnClose.Location = new Point(margin + width - btnClose.Width, y + Ui.S(20));
            ClientSize = new Size(Ui.S(Width96), btnClose.Bottom + margin);
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            theme.Apply(this);
            foreach (var row in donationRows)
            {
                if (row.Entry.Icon != null)
                {
                    row.Icon.Image = row.Entry.Icon;
                }
                else
                {
                    var old = row.Icon.Image;
                    row.Icon.Image = LetterTile(row.Entry.Link.Host, Ui.S(16));
                    if (old != null) old.Dispose();
                }
            }
            theme.ApplyFrame(this);
            Invalidate(true);
        }

        /// <summary>Stand-in when a site's favicon cannot be read: the first letter of its host on a rounded tile.</summary>
        private Image LetterTile(string host, int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var path = Ui.Round(new RectangleF(0, 0, size - 1, size - 1), Ui.S(3)))
                using (var b = new SolidBrush(theme.ControlStroke))
                {
                    g.FillPath(b, path);
                }
                var letter = string.IsNullOrEmpty(host) ? "?" : host.Substring(0, 1).ToUpperInvariant();
                using (var f = new Font(Ui.Caption.FontFamily, 7F, FontStyle.Bold))
                {
                    TextRenderer.DrawText(g, letter, f, new Rectangle(0, 0, size, size), theme.Text, theme.ControlStroke,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }
            return bmp;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            theme.ApplyFrame(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var row in donationRows)
                {
                    if (row.Entry.Icon == null && row.Icon.Image != null) row.Icon.Image.Dispose();
                    row.Icon.Image = null;      // favicons belong to the app, not to this window
                }
                if (picture.Image != null) picture.Image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
