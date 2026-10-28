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
    /// <summary>Small About window: name, version, description and a link to the author.</summary>
    internal sealed class AboutForm : Form
    {
        private const int Width96 = 480;

        private readonly Label lblDescription = new Label();
        private readonly Label lblAuthor = new Label();
        private readonly LinkLabel lnkAuthor = new LinkLabel();
        private readonly Button btnOk = new Button();

        public AboutForm(Strings S)
        {
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
            version.ForeColor = SystemColors.GrayText;
            version.Location = new Point(84, 46);
            Controls.Add(version);

            lblDescription.Text = S.AboutDescription;
            lblDescription.UseMnemonic = false;
            lblDescription.SetBounds(20, 82, Width96 - 40, 60);
            Controls.Add(lblDescription);

            lblAuthor.Text = S.AuthorLabel;
            lblAuthor.AutoSize = true;
            lblAuthor.Location = new Point(20, 150);
            Controls.Add(lblAuthor);

            lnkAuthor.Text = AppInfo.AuthorText;
            lnkAuthor.AutoSize = true;
            lnkAuthor.Location = new Point(80, 150);
            lnkAuthor.LinkClicked += delegate { OpenAuthorPage(); };
            Controls.Add(lnkAuthor);

            btnOk.Text = S.ButtonOk;
            btnOk.SetBounds(Width96 - 20 - 96, 176, 96, 30);
            btnOk.UseVisualStyleBackColor = true;
            btnOk.Click += delegate { Close(); };
            Controls.Add(btnOk);
            AcceptButton = btnOk;
            CancelButton = btnOk;

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

            lblAuthor.Top = lblDescription.Bottom + gap;
            lnkAuthor.Top = lblAuthor.Top;
            lnkAuthor.Left = lblAuthor.Right + gap / 2;
            btnOk.Top = lblAuthor.Bottom + gap * 2;
            ClientSize = new Size(ClientSize.Width, btnOk.Bottom + gap * 2);
        }

        private void OpenAuthorPage()
        {
            try
            {
                Process.Start(AppInfo.AuthorUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
