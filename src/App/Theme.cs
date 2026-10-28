// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UABlackoutPreWarning
{
    /// <summary>
    /// Light and dark colour sets, applied to a window in place (unsaved edits are kept).
    /// Light uses the normal Windows colours, so it looks exactly like a standard app.
    /// Only documented APIs are used: control colours, flat styles, owner drawing and
    /// the DWM dark title bar attribute.
    /// </summary>
    internal sealed class Theme
    {
        public bool Dark;
        public Color Window, Text, Sub, Border, Input, InputText;
        public Color Button, ButtonBorder, ButtonHover, Primary, PrimaryText;
        public Color Link, Warn, StripOn, StripOff, StripUnknown, Marker;

        /// <summary>Tag a control with one of these to pick its text colour.</summary>
        public const string RoleSub = "sub", RoleWarn = "warn", RolePrimary = "primary";

        public static readonly Theme LightTheme = CreateLight();
        public static readonly Theme DarkTheme = CreateDark();

        public static Theme For(bool dark)
        {
            return dark ? DarkTheme : LightTheme;
        }

        private static Theme CreateLight()
        {
            var t = new Theme();
            t.Dark = false;
            t.Window = SystemColors.Control;
            t.Text = SystemColors.ControlText;
            t.Sub = SystemColors.GrayText;
            t.Border = SystemColors.ControlDark;
            t.Input = SystemColors.Window;
            t.InputText = SystemColors.WindowText;
            t.Button = SystemColors.Control;
            t.ButtonBorder = SystemColors.ControlDark;
            t.ButtonHover = SystemColors.ControlLight;
            t.Primary = SystemColors.Control;
            t.PrimaryText = SystemColors.ControlText;
            t.Link = Color.FromArgb(0, 0, 255);
            t.Warn = Color.FromArgb(0xA4, 0x26, 0x2C);
            t.StripOn = Color.FromArgb(0xE3, 0xE3, 0xE3);
            t.StripOff = Color.FromArgb(0xC4, 0x2B, 0x1C);
            t.StripUnknown = Color.FromArgb(0xF0, 0xF0, 0xF0);
            t.Marker = SystemColors.ControlText;
            return t;
        }

        private static Theme CreateDark()
        {
            var t = new Theme();
            t.Dark = true;
            t.Window = Color.FromArgb(0x20, 0x20, 0x20);
            t.Text = Color.FromArgb(0xF3, 0xF3, 0xF3);
            t.Sub = Color.FromArgb(0xAB, 0xAB, 0xAB);
            t.Border = Color.FromArgb(0x45, 0x45, 0x45);
            t.Input = Color.FromArgb(0x2D, 0x2D, 0x2D);
            t.InputText = Color.FromArgb(0xF3, 0xF3, 0xF3);
            t.Button = Color.FromArgb(0x2D, 0x2D, 0x2D);
            t.ButtonBorder = Color.FromArgb(0x5A, 0x5A, 0x5A);
            t.ButtonHover = Color.FromArgb(0x3A, 0x3A, 0x3A);
            t.Primary = Color.FromArgb(0x4C, 0xC2, 0xFF);
            t.PrimaryText = Color.Black;
            t.Link = Color.FromArgb(0x99, 0xEB, 0xFF);
            t.Warn = Color.FromArgb(0xFF, 0x99, 0xA4);
            t.StripOn = Color.FromArgb(0x3A, 0x3A, 0x3A);
            t.StripOff = Color.FromArgb(0xE5, 0x48, 0x3C);
            t.StripUnknown = Color.FromArgb(0x2A, 0x2A, 0x2A);
            t.Marker = Color.White;
            return t;
        }

        /// <summary>Recolours a control and all its children.</summary>
        public void Apply(Control c)
        {
            var role = c.Tag as string;

            if (c is Form || c is PictureBox)
            {
                c.BackColor = Window;
                c.ForeColor = Text;
            }
            else if (c is ThemedGroupBox)
            {
                var g = (ThemedGroupBox)c;
                g.BackColor = Window;
                g.ForeColor = Text;
                g.Dark = Dark;
                g.BorderColor = Border;
                g.Invalidate();
            }
            else if (c is LinkLabel)
            {
                var l = (LinkLabel)c;
                l.LinkColor = Link;
                l.ActiveLinkColor = Link;
                l.VisitedLinkColor = Link;
            }
            else if (c is Label)
            {
                c.ForeColor = role == RoleSub ? Sub : (role == RoleWarn ? Warn : Text);
            }
            else if (c is TextBox || c is NumericUpDown)
            {
                c.BackColor = Input;
                c.ForeColor = InputText;
                var box = c as TextBox;
                if (box != null) box.BorderStyle = Dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                var num = c as NumericUpDown;
                if (num != null) num.BorderStyle = Dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
            }
            else if (c is ComboBox)
            {
                var cb = (ComboBox)c;
                cb.FlatStyle = Dark ? FlatStyle.Flat : FlatStyle.Standard;
                cb.BackColor = Input;
                cb.ForeColor = InputText;
            }
            else if (c is CheckBox)
            {
                var cb = (CheckBox)c;
                cb.FlatStyle = Dark ? FlatStyle.Flat : FlatStyle.Standard;
                cb.FlatAppearance.BorderColor = ButtonBorder;
                cb.FlatAppearance.CheckedBackColor = Input;
                cb.BackColor = Window;
                cb.ForeColor = Text;
            }
            else if (c is Button)
            {
                var b = (Button)c;
                bool primary = role == RolePrimary;
                if (Dark)
                {
                    b.FlatStyle = FlatStyle.Flat;
                    b.BackColor = primary ? Primary : Button;
                    b.ForeColor = primary ? PrimaryText : Text;
                    b.FlatAppearance.BorderColor = primary ? Primary : ButtonBorder;
                    b.FlatAppearance.MouseOverBackColor = primary ? ControlPaint.Light(Primary, 0.3f) : ButtonHover;
                    b.FlatAppearance.MouseDownBackColor = primary ? ControlPaint.Dark(Primary, 0.1f) : Border;
                }
                else
                {
                    b.FlatStyle = FlatStyle.Standard;
                    b.BackColor = SystemColors.Control;
                    b.ForeColor = SystemColors.ControlText;
                    b.UseVisualStyleBackColor = true;
                }
            }
            else if (c is DayStrip)
            {
                ((DayStrip)c).SetTheme(this);
            }
            else if (c is ThemeToggle)
            {
                c.BackColor = Window;
                c.Invalidate();
            }

            foreach (Control child in c.Controls) Apply(child);
        }

        // ---- dark title bar (DWM) -------------------------------------------------

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        private const int DwmUseImmersiveDarkMode = 20;          // Windows 10 20H1+ and Windows 11
        private const int DwmUseImmersiveDarkModeOld = 19;       // Windows 10 1809-1909
        private const uint SwpFrameChanged = 0x0020, SwpNoMove = 0x0002, SwpNoSize = 0x0001,
                           SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010;

        /// <summary>Asks Windows to draw the window's title bar dark or light. Ignored where unsupported.</summary>
        public static void ApplyTitleBar(Form form, bool dark)
        {
            if (!form.IsHandleCreated || Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            int value = dark ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkMode, ref value, 4) != 0)
                {
                    DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkModeOld, ref value, 4);
                }
                // Repaint the frame now rather than on the next activation.
                SetWindowPos(form.Handle, IntPtr.Zero, 0, 0, 0, 0,
                    SwpFrameChanged | SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        // ---- tray menu ------------------------------------------------------------

        public void ApplyMenu(ContextMenuStrip menu)
        {
            if (Dark)
            {
                menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors(this));
            }
            else
            {
                menu.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            }
            foreach (ToolStripItem item in menu.Items)
            {
                if (Dark) item.ForeColor = Text;
                else item.ResetForeColor();
            }
        }

        private sealed class DarkMenuColors : ProfessionalColorTable
        {
            private readonly Theme t;

            public DarkMenuColors(Theme theme)
            {
                t = theme;
                UseSystemColors = false;
            }

            public override Color ToolStripDropDownBackground { get { return t.Input; } }
            public override Color ImageMarginGradientBegin { get { return t.Input; } }
            public override Color ImageMarginGradientMiddle { get { return t.Input; } }
            public override Color ImageMarginGradientEnd { get { return t.Input; } }
            public override Color MenuBorder { get { return t.Border; } }
            public override Color MenuItemBorder { get { return t.ButtonHover; } }
            public override Color MenuItemSelected { get { return t.ButtonHover; } }
            public override Color MenuItemSelectedGradientBegin { get { return t.ButtonHover; } }
            public override Color MenuItemSelectedGradientEnd { get { return t.ButtonHover; } }
            public override Color SeparatorDark { get { return t.Border; } }
            public override Color SeparatorLight { get { return t.Border; } }
        }
    }

    /// <summary>GroupBox whose frame and title follow the theme (standard drawing in light mode).</summary>
    internal sealed class ThemedGroupBox : GroupBox
    {
        public bool Dark;
        public Color BorderColor = SystemColors.ControlDark;

        public ThemedGroupBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        /// <summary>
        /// WS_CLIPCHILDREN: a group box normally paints underneath its children, so a partial
        /// repaint of the frame could cover the top of the day strip. Clipping prevents that.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= 0x02000000;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Dark)
            {
                base.OnPaint(e);
                return;
            }

            var g = e.Graphics;
            g.Clear(BackColor);
            var textSize = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            int top = textSize.Height / 2;
            int textLeft = 8;
            using (var pen = new Pen(BorderColor))
            {
                g.DrawLine(pen, 0, top, textLeft - 2, top);
                g.DrawLine(pen, textLeft + textSize.Width + 2, top, Width - 1, top);
                g.DrawLine(pen, 0, top, 0, Height - 1);
                g.DrawLine(pen, Width - 1, top, Width - 1, Height - 1);
                g.DrawLine(pen, 0, Height - 1, Width - 1, Height - 1);
            }
            TextRenderer.DrawText(g, Text, Font, new Point(textLeft, 0), ForeColor, TextFormatFlags.NoPadding);
        }
    }

    /// <summary>
    /// Day/Night switch: a pill with a round handle. Light = handle on the right with a sun;
    /// dark = handle on the left with a moon. Click, Space or Enter toggles it.
    /// </summary>
    internal sealed class ThemeToggle : Control
    {
        private bool dark;

        public event EventHandler Toggled;

        public ThemeToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        public bool DarkMode
        {
            get { return dark; }
            set
            {
                if (dark == value) return;
                dark = value;
                Invalidate();
            }
        }

        private void Toggle()
        {
            DarkMode = !DarkMode;
            if (Toggled != null) Toggled(this, EventArgs.Empty);
        }

        protected override void OnClick(EventArgs e)
        {
            Focus();
            Toggle();
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Toggle();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Enter || base.IsInputKey(keyData);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float s = Height / 28F;                      // drawn on a 56 x 28 grid
            float w = Width - 1, h = Height - 1;
            Color track = dark ? Color.FromArgb(0x3A, 0x3A, 0x3A) : Color.FromArgb(0xE6, 0xE6, 0xE6);
            Color edge = dark ? Color.FromArgb(0x6B, 0x6B, 0x6B) : Color.FromArgb(0x8A, 0x8A, 0x8A);
            Color knob = dark ? Color.FromArgb(0xF3, 0xF3, 0xF3) : Color.White;

            using (var path = Pill(0.5F, 0.5F, w - 1, h - 1))
            using (var fill = new SolidBrush(track))
            using (var pen = new Pen(edge, 1))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            float cy = h / 2;
            float left = 14 * s, right = w - 14 * s;
            float r = 11 * s;

            if (dark)
            {
                Sun(g, right, cy, s, Color.FromArgb(0xAB, 0xAB, 0xAB), false);
                using (var b = new SolidBrush(knob)) g.FillEllipse(b, left - r, cy - r, 2 * r, 2 * r);
                Moon(g, left, cy, s, Color.FromArgb(0x20, 0x20, 0x20), knob);
            }
            else
            {
                Moon(g, left, cy, s * 0.9F, Color.FromArgb(0x6B, 0x6B, 0x6B), track);
                using (var b = new SolidBrush(knob)) g.FillEllipse(b, right - r, cy - r, 2 * r, 2 * r);
                using (var p = new Pen(edge, 1)) g.DrawEllipse(p, right - r, cy - r, 2 * r, 2 * r);
                Sun(g, right, cy, s, Color.FromArgb(0xE8, 0xA4, 0x00), true);
            }

            if (Focused && ShowFocusCues)
            {
                using (var path = Pill(1.5F, 1.5F, w - 3, h - 3))
                using (var pen = new Pen(dark ? Color.White : Color.Black, 1))
                {
                    pen.DashStyle = DashStyle.Dot;
                    g.DrawPath(pen, path);
                }
            }
        }

        private static GraphicsPath Pill(float x, float y, float w, float h)
        {
            var p = new GraphicsPath();
            float d = h;
            p.AddArc(x, y, d, d, 90, 180);
            p.AddArc(x + w - d, y, d, d, 270, 180);
            p.CloseFigure();
            return p;
        }

        private static void Sun(Graphics g, float cx, float cy, float s, Color colour, bool filled)
        {
            float core = 3.3F * s;
            using (var pen = new Pen(colour, 1.6F * s))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (filled)
                {
                    using (var b = new SolidBrush(colour)) g.FillEllipse(b, cx - core, cy - core, 2 * core, 2 * core);
                }
                else
                {
                    g.DrawEllipse(pen, cx - core, cy - core, 2 * core, 2 * core);
                }
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    float r1 = 5.4F * s, r2 = 7.4F * s;
                    g.DrawLine(pen, cx + (float)Math.Cos(a) * r1, cy + (float)Math.Sin(a) * r1,
                                    cx + (float)Math.Cos(a) * r2, cy + (float)Math.Sin(a) * r2);
                }
            }
        }

        private static void Moon(Graphics g, float cx, float cy, float s, Color colour, Color background)
        {
            float r = 6.4F * s;
            using (var b = new SolidBrush(colour)) g.FillEllipse(b, cx - r - 0.5F * s, cy - r, 2 * r, 2 * r);
            float r2 = 5.2F * s;
            float ox = cx + 2.4F * s, oy = cy - 2.4F * s;
            using (var b = new SolidBrush(background)) g.FillEllipse(b, ox - r2, oy - r2, 2 * r2, 2 * r2);
        }
    }
}
