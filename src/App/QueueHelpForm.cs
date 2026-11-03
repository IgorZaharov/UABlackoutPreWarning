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
    /// "How to find your queue": four steps (the same as the Android app's help), shown from the link on the
    /// Queue row in Preferences. A Windows 11 content dialog: no title bar, the steps on a surface, the button
    /// in a footer. Enter, Escape or the button closes it.
    /// </summary>
    internal sealed class QueueHelpForm : Form
    {
        private const int Width96 = 448;

        private readonly Theme theme;

        public QueueHelpForm(Strings S, Theme t)
        {
            theme = t;
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            Font = Ui.Body;
            Text = "";
            AccessibleName = S.QueueHelpTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;                 // with no text: no title bar, like a Windows 11 dialog
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;

            int pad = Ui.S(24), width = Ui.S(Width96), inner = width - 2 * pad;

            var content = new Panel();
            content.Tag = Theme.RoleSurface;
            var title = Ui.Label(S.QueueHelpTitle, Ui.Subtitle, null);
            title.AccessibleRole = AccessibleRole.StaticText;
            content.Controls.Add(title);
            int y = Ui.Fit(title, pad, pad, inner) + Ui.S(12);

            var step1 = new LinkLabel();
            step1.AutoSize = false;
            step1.UseMnemonic = false;
            step1.Font = Ui.Body;
            step1.LinkBehavior = LinkBehavior.HoverUnderline;
            step1.Text = S.QueueHelpStep1Before + S.QueueHelpStep1Link + S.QueueHelpStep1After;
            step1.LinkArea = new LinkArea(S.QueueHelpStep1Before.Length, S.QueueHelpStep1Link.Length);
            step1.LinkClicked += delegate { AppInfo.OpenUrl(this, AppInfo.SchedulesUrl); };
            var steps = new Label[] { step1, Ui.Label(S.QueueHelpStep2, Ui.Body, null),
                                      Ui.Label(S.QueueHelpStep3, Ui.Body, null), Ui.Label(S.QueueHelpStep4, Ui.Body, null) };
            int numberWidth = Ui.S(22);
            for (int i = 0; i < steps.Length; i++)
            {
                var number = Ui.Inline((i + 1) + ".", Ui.Body, null);
                number.Location = new Point(pad, y);
                content.Controls.Add(number);
                content.Controls.Add(steps[i]);
                y = Ui.Fit(steps[i], pad + numberWidth, y, inner - numberWidth) + Ui.S(8);
            }
            content.SetBounds(0, 0, width, y + Ui.S(16));

            var divider = new Panel();
            divider.Tag = Theme.RoleDivider;
            divider.SetBounds(0, content.Bottom, width, 1);

            var ok = new FluentButton(ButtonKind.Accent);
            ok.Text = S.ButtonGotIt;
            ok.Width = (inner - Ui.S(8)) / 2;
            ok.Location = new Point(pad + inner - ok.Width, divider.Bottom + pad);
            ok.DialogResult = DialogResult.OK;
            AcceptButton = ok;
            CancelButton = ok;

            Controls.AddRange(new Control[] { content, divider, ok });
            ClientSize = new Size(width, ok.Bottom + pad);
            theme.Apply(this);
            ResumeLayout(false);
            ActiveControl = ok;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            theme.ApplyFrame(this);
        }
    }
}
