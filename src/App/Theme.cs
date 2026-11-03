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
    /// Windows 11 light and dark colour sets (Fluent neutrals and the default accent), applied to a
    /// window in place so unsaved edits are kept. Only documented APIs: control colours, owner drawing
    /// and DWM window attributes.
    /// </summary>
    internal sealed class Theme
    {
        public bool Dark, HighContrast;
        public Color Window, Card, CardStroke, Text, Sub, Warn, Accent, OnAccent;
        public Color Control, ControlStroke, FieldBottom, StripOn, StripOff, StripUnknown;

        /// <summary>Tag a Label (text colour) or Panel (fill) with one of these roles.</summary>
        public const string RoleSub = "sub", RoleWarn = "warn", RoleStripOff = "strip-off", RoleStripOn = "strip-on",
                            RoleSurface = "surface", RoleDivider = "divider";

        public static readonly Theme LightTheme = CreateLight();
        public static readonly Theme DarkTheme = CreateDark();

        /// <summary>The light or dark set; with a Windows High Contrast theme on, the user's system colours.</summary>
        public static Theme For(bool dark)
        {
            if (SystemInformation.HighContrast) return CreateHighContrast();
            return dark ? DarkTheme : LightTheme;
        }

        /// <summary>
        /// Windows High Contrast: every colour comes from the user's contrast theme, every edge is drawn in
        /// the text colour, and the accent is the system highlight.
        /// </summary>
        private static Theme CreateHighContrast()
        {
            var t = new Theme();
            t.HighContrast = true;
            t.Window = SystemColors.Window;
            t.Dark = t.Window.GetBrightness() < 0.5F;
            t.Card = SystemColors.Window;
            t.CardStroke = SystemColors.WindowText;
            t.Text = SystemColors.WindowText;
            t.Sub = SystemColors.WindowText;
            t.Warn = SystemColors.WindowText;
            t.Accent = SystemColors.Highlight;
            t.OnAccent = SystemColors.HighlightText;
            t.Control = SystemColors.Window;
            t.ControlStroke = SystemColors.WindowText;
            t.FieldBottom = SystemColors.WindowText;
            t.StripOn = SystemColors.Window;
            t.StripOff = SystemColors.Highlight;
            t.StripUnknown = SystemColors.Window;
            return t;
        }

        private static Theme CreateLight()
        {
            var t = new Theme();
            t.Dark = false;
            t.Window = Rgb(0xF3F3F3);
            t.Card = Rgb(0xFBFBFB);
            t.CardStroke = Rgb(0xE5E5E5);
            t.Text = Rgb(0x1B1B1B);
            t.Sub = Rgb(0x616161);              // 5.6:1 on Window, 6.0:1 on Card
            t.Warn = Rgb(0xC42B1C);
            t.Accent = Rgb(0x005FB8);           // Windows 11 default accent for light mode
            t.OnAccent = Color.White;
            t.Control = Rgb(0xFFFFFF);
            t.ControlStroke = Rgb(0xE0E0E0);
            t.FieldBottom = Rgb(0x8A8A8A);
            t.StripOn = Rgb(0xE3E3E3);
            t.StripOff = Rgb(0xC42B1C);         // 4.4:1 against StripOn
            t.StripUnknown = Rgb(0xECECEC);
            return t;
        }

        private static Theme CreateDark()
        {
            var t = new Theme();
            t.Dark = true;
            t.Window = Rgb(0x202020);
            t.Card = Rgb(0x2B2B2B);
            t.CardStroke = Rgb(0x363636);
            t.Text = Rgb(0xFFFFFF);
            t.Sub = Rgb(0xC5C5C5);
            t.Warn = Rgb(0xFF99A4);
            t.Accent = Rgb(0x60CDFF);           // Windows 11 default accent for dark mode
            t.OnAccent = Color.Black;
            t.Control = Rgb(0x2D2D2D);
            t.ControlStroke = Rgb(0x3A3A3A);
            t.FieldBottom = Rgb(0x9A9A9A);
            t.StripOn = Rgb(0x3A3A3A);
            t.StripOff = Rgb(0xF25F55);         // 3.5:1 against StripOn
            t.StripUnknown = Rgb(0x333333);
            return t;
        }

        private static Color Rgb(int rgb)
        {
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        /// <summary>Mixes <paramref name="amount"/> (0..1) of <paramref name="b"/> into <paramref name="a"/>.</summary>
        public static Color Blend(Color a, Color b, double amount)
        {
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * amount),
                (int)Math.Round(a.G + (b.G - a.G) * amount),
                (int)Math.Round(a.B + (b.B - a.B) * amount));
        }

        /// <summary>Recolours a control and all its children; containers take their parent's background.</summary>
        public void Apply(Control c)
        {
            var role = c.Tag as string;
            var parentBack = c.Parent != null ? c.Parent.BackColor : Window;
            // Text and pictures placed straight on a window with the Mica-like backdrop let it show through.
            bool onGlass = c.Parent != null && Backdrop.IsGlass(c.Parent, this);
            var labelBack = onGlass ? Color.Transparent : parentBack;

            if (c is Form)
            {
                c.BackColor = Window;
                c.ForeColor = Text;
            }
            else if (c is Card)
            {
                var card = (Card)c;
                card.BackColor = Card;
                card.BorderColor = CardStroke;
                card.Theme = this;
                card.Invalidate();
            }
            else if (c is IThemed)
            {
                c.BackColor = parentBack;
                ((IThemed)c).SetTheme(this);
            }
            else if (c is LinkLabel)
            {
                var l = (LinkLabel)c;
                l.BackColor = labelBack;
                l.LinkColor = Accent;
                l.ActiveLinkColor = Accent;
                l.VisitedLinkColor = Accent;
                l.ForeColor = Text;
            }
            else if (c is Label)
            {
                c.BackColor = labelBack;
                c.ForeColor = role == RoleSub ? Sub : (role == RoleWarn ? Warn : Text);
            }
            else if (c is TextBox || c is NumericUpDown)
            {
                c.BackColor = Control;
                c.ForeColor = Text;
            }
            else if (c is DayStrip)
            {
                c.BackColor = parentBack;
                ((DayStrip)c).SetTheme(this);
            }
            else if (role == RoleStripOff) c.BackColor = StripOff;
            else if (role == RoleStripOn) c.BackColor = StripOn;
            else if (role == RoleSurface) c.BackColor = Card;
            else if (role == RoleDivider) c.BackColor = CardStroke;
            else
            {
                c.BackColor = c is PictureBox || c.GetType() == typeof(Panel) ? labelBack : parentBack;
                c.ForeColor = Text;
            }

            foreach (Control child in c.Controls) Apply(child);
        }

        // ---- window frame (DWM) -----------------------------------------------------

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        private const int DwmUseImmersiveDarkMode = 20;          // Windows 10 20H1+ and Windows 11
        private const int DwmUseImmersiveDarkModeOld = 19;       // Windows 10 1809-1909
        private const int DwmWindowCornerPreference = 33;        // Windows 11
        private const int DwmCaptionColor = 35;                  // Windows 11
        private const int DwmSystemBackdropType = 38;            // Windows 11 22H2 (build 22621)
        private const int CornerRound = 2, BackdropMica = 2;
        private const uint SwpFrameChanged = 0x0020, SwpNoMove = 0x0002, SwpNoSize = 0x0001,
                           SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010;

        /// <summary>
        /// The title bar and frame follow the theme: dark or light, rounded corners on Windows 11, and the
        /// title bar either shows Mica (22H2+) or takes the window's own colour, so it blends with the
        /// content. Each attribute is ignored where Windows does not support it.
        /// </summary>
        public void ApplyFrame(Form form)
        {
            if (!form.IsHandleCreated || Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            int build = Environment.OSVersion.Version.Build;
            try
            {
                int dark = Dark ? 1 : 0;
                if (DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkMode, ref dark, 4) != 0)
                {
                    DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkModeOld, ref dark, 4);
                }
                if (build >= 22000)
                {
                    int corner = CornerRound;
                    DwmSetWindowAttribute(form.Handle, DwmWindowCornerPreference, ref corner, 4);
                    int mica = BackdropMica;
                    if (build < 22621 || DwmSetWindowAttribute(form.Handle, DwmSystemBackdropType, ref mica, 4) != 0)
                    {
                        int colour = Window.R | (Window.G << 8) | (Window.B << 16);      // COLORREF
                        DwmSetWindowAttribute(form.Handle, DwmCaptionColor, ref colour, 4);
                    }
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
            menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors(this));
            menu.Font = Ui.Body;
            foreach (ToolStripItem item in menu.Items)
            {
                item.ForeColor = Text;
                item.Padding = new Padding(Ui.S(4), Ui.S(4), Ui.S(4), Ui.S(4));
            }
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            private readonly Theme t;

            public MenuColors(Theme theme)
            {
                t = theme;
                UseSystemColors = false;
            }

            private Color Menu { get { return t.Dark ? t.Card : Color.FromArgb(0xF9, 0xF9, 0xF9); } }
            private Color Hover { get { return Blend(Menu, t.Text, 0.07); } }

            public override Color ToolStripDropDownBackground { get { return Menu; } }
            public override Color ImageMarginGradientBegin { get { return Menu; } }
            public override Color ImageMarginGradientMiddle { get { return Menu; } }
            public override Color ImageMarginGradientEnd { get { return Menu; } }
            public override Color MenuBorder { get { return t.Dark ? t.CardStroke : Color.FromArgb(0xD9, 0xD9, 0xD9); } }
            public override Color MenuItemBorder { get { return Hover; } }
            public override Color MenuItemSelected { get { return Hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return Hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return Hover; } }
            public override Color SeparatorDark { get { return t.CardStroke; } }
            public override Color SeparatorLight { get { return Menu; } }
        }
    }

    /// <summary>A control that draws itself from the theme.</summary>
    internal interface IThemed
    {
        void SetTheme(Theme theme);
    }

    /// <summary>
    /// Pixel sizes and fonts. The app is system-DPI aware, so one scale factor (system DPI / 96) holds for
    /// the whole run; layouts are written in 96-DPI units and converted with <see cref="S"/> - no WinForms
    /// auto-scaling, which moved controls to the wrong place at 125 % in the past.
    /// </summary>
    internal static class Ui
    {
        public static readonly float Scale = ReadScale();
        public static readonly Font Body = Pick(10.5F, false, "Segoe UI Variable Text", "Segoe UI");
        public static readonly Font BodyStrong = Pick(10.5F, true, "Segoe UI Variable Text", "Segoe UI");
        public static readonly Font Caption = Pick(9F, false, "Segoe UI Variable Small", "Segoe UI");
        public static readonly Font Subtitle = Pick(15F, true, "Segoe UI Variable Display", "Segoe UI");
        public static readonly Font Title = Pick(21F, true, "Segoe UI Variable Display", "Segoe UI");

        /// <summary>Segoe Fluent Icons (Windows 11) or Segoe MDL2 Assets (Windows 10); null if neither exists.</summary>
        public static readonly Font Icons = PickIcons(12F);

        public const string GlyphSettings = "", GlyphHelp = "", GlyphMoon = "", GlyphSun = "", GlyphInfo = "",
                            GlyphCalendar = "", GlyphOpen = "", GlyphChevron = "", GlyphCheck = "";

        public static int S(int value)
        {
            return (int)Math.Round(value * Scale);
        }

        private static float ReadScale()
        {
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero)) return Math.Max(1F, g.DpiX / 96F);
            }
            catch (Exception)
            {
                return 1F;
            }
        }

        /// <summary>
        /// The first installed family; semibold where asked. Segoe UI Variable's semibold is a named
        /// instance ("… Semibold"); Segoe UI has "Segoe UI Semibold"; otherwise bold.
        /// </summary>
        private static Font Pick(float size, bool semibold, params string[] families)
        {
            foreach (var family in families)
            {
                var f = Exists(semibold ? family + " Semibold" : family, size, FontStyle.Regular);
                if (f != null) return f;
            }
            return new Font(SystemFonts.MessageBoxFont.FontFamily, size, semibold ? FontStyle.Bold : FontStyle.Regular);
        }

        private static Font PickIcons(float size)
        {
            return Exists("Segoe Fluent Icons", size, FontStyle.Regular) ?? Exists("Segoe MDL2 Assets", size, FontStyle.Regular);
        }

        private static Font Exists(string family, float size, FontStyle style)
        {
            var f = new Font(family, size, style);
            if (string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase)) return f;
            f.Dispose();
            return null;
        }

        /// <summary>Height of <paramref name="text"/> wrapped to <paramref name="width"/> pixels.</summary>
        public static int TextHeight(string text, Font font, int width)
        {
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
            return TextRenderer.MeasureText(string.IsNullOrEmpty(text) ? " " : text, font, new Size(width, int.MaxValue), flags).Height;
        }

        public static int TextWidth(string text, Font font)
        {
            return TextRenderer.MeasureText(string.IsNullOrEmpty(text) ? " " : text, font, Size.Empty, TextFormatFlags.NoPadding).Width;
        }

        /// <summary>A label that wraps its text; size it with <see cref="Fit"/>.</summary>
        public static Label Label(string text, Font font, string role)
        {
            var l = new Label();
            l.AutoSize = false;
            l.UseMnemonic = false;
            l.Font = font;
            l.Text = text;
            l.Tag = role;
            return l;
        }

        /// <summary>Places a wrapping label at (x, y) with the given width; returns its bottom.</summary>
        public static int Fit(Label l, int x, int y, int width)
        {
            l.SetBounds(x, y, width, TextHeight(l.Text, l.Font, width));
            return l.Bottom;
        }

        /// <summary>A label sized to its one-line text.</summary>
        public static Label Inline(string text, Font font, string role)
        {
            var l = Label(text, font, role);
            l.AutoSize = true;
            l.Padding = Padding.Empty;
            l.Margin = Padding.Empty;
            return l;
        }

        /// <summary>A Segoe icon glyph as a decorative label (hidden from screen readers by its empty name).</summary>
        public static Label Glyph(string glyph, string role)
        {
            var l = Inline(Icons != null ? glyph : "", Icons ?? Caption, role);
            l.AccessibleRole = AccessibleRole.Graphic;
            l.AccessibleName = "";
            return l;
        }

        public static LinkLabel Link(string text, Font font, string url)
        {
            var l = new LinkLabel();
            l.AutoSize = true;
            l.UseMnemonic = false;
            l.Font = font;
            l.Text = text;
            l.Padding = Padding.Empty;
            l.Margin = Padding.Empty;
            l.LinkBehavior = LinkBehavior.HoverUnderline;
            if (url != null) l.LinkClicked += delegate { AppInfo.OpenUrl(l.FindForm(), url); };
            return l;
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(2 * radius, Math.Min(r.Width, r.Height));
            if (d <= 0)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>
        /// Fills a control's background: the backdrop when it sits directly on a glass window, otherwise its
        /// parent's colour. Returns the colour to blend hover states with.
        /// </summary>
        public static Color PaintBack(Graphics g, Control c, Theme t)
        {
            if (c.Parent != null && Backdrop.IsGlass(c.Parent, t))
            {
                Backdrop.Paint(g, c, t);
                return t.Window;
            }
            var back = c.Parent != null ? c.Parent.BackColor : t.Window;
            g.Clear(back);
            return back;
        }

        /// <summary>The keyboard focus visual: a 2 px rounded outline in the text colour.</summary>
        public static void FocusRing(Graphics g, Rectangle bounds, Color colour)
        {
            using (var path = Round(new RectangleF(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2), S(5)))
            using (var pen = new Pen(colour, S(2)))
            {
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>
    /// A rounded surface (8 px corners): the Windows 11 card. Depth comes from a faint edge that is a little
    /// darker at the bottom, as a soft shadow would make it; in High Contrast a plain 1 px border.
    /// </summary>
    internal sealed class Card : Panel
    {
        public Color BorderColor = SystemColors.ControlDark;
        public Theme Theme = Theme.LightTheme;

        public Card()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            var outside = Ui.PaintBack(g, this, Theme);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float radius = Ui.S(8);
            var shape = new RectangleF(0.5F, 0.5F, Width - 1, Height - 1);
            if (Theme.HighContrast)
            {
                using (var path = Ui.Round(shape, radius))
                using (var fill = new SolidBrush(BackColor))
                using (var pen = new Pen(BorderColor))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }
                return;
            }
            // Shadow edge: the whole outline, then the card shifted 1 px up so only the bottom edge stays darker.
            var shadow = Theme.Dark ? Color.FromArgb(0x14, 0x14, 0x14) : Theme.Blend(outside, Color.Black, 0.12);
            using (var path = Ui.Round(shape, radius))
            using (var b = new SolidBrush(shadow))
            {
                g.FillPath(b, path);
            }
            var edge = Theme.Dark ? Theme.Blend(BackColor, Color.White, 0.06) : Theme.Blend(BackColor, Color.Black, 0.06);
            using (var path = Ui.Round(new RectangleF(0.5F, 0.5F, Width - 1, Height - 2), radius))
            using (var b = new SolidBrush(edge))
            {
                g.FillPath(b, path);
            }
            using (var path = Ui.Round(new RectangleF(1.5F, 1.5F, Width - 3, Height - 4), radius - 1))
            using (var fill = new SolidBrush(BackColor))
            {
                g.FillPath(fill, path);
            }
        }
    }

    /// <summary>Button styles of Windows 11: standard, accent (the one primary action) and subtle (icon only).</summary>
    internal enum ButtonKind
    {
        Standard,
        Accent,
        Subtle
    }

    /// <summary>A Windows 11 style button, drawn by hand in both themes; keyboard and screen readers as a normal Button.</summary>
    internal sealed class FluentButton : Button, IThemed, IAnimated
    {
        private Theme theme = Theme.LightTheme;
        private bool hover, pressed;
        private readonly Fader hoverFade = new Fader(), pressFade = new Fader();

        public ButtonKind Kind;

        public FluentButton(ButtonKind kind)
        {
            Kind = kind;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            UseMnemonic = false;
            Font = Ui.Body;
            Height = Ui.S(32);
        }

        /// <summary>A borderless icon button (Segoe glyph) with the given accessible name and tooltip text.</summary>
        public static FluentButton Icon(string glyph, string fallback, string name)
        {
            var b = new FluentButton(ButtonKind.Subtle);
            b.Font = Ui.Icons ?? Ui.Body;
            b.Text = Ui.Icons != null ? glyph : fallback;
            b.AccessibleName = name;
            b.Size = new Size(Ui.S(36), Ui.S(36));
            return b;
        }

        /// <summary>Width for the text plus 12 px padding each side, at least <paramref name="min96"/>.</summary>
        public void FitWidth(int min96)
        {
            Width = Math.Max(Ui.S(min96), Ui.TextWidth(Text, Font) + Ui.S(24));
        }

        public void SetTheme(Theme t)
        {
            theme = t;
            Invalidate();
        }

        private void Fade()
        {
            bool a = hoverFade.To(hover ? 1 : 0), b = pressFade.To(pressed ? 1 : 0);
            if (a || b) Motion.Start(this);
            Invalidate();
        }

        public bool Step()
        {
            bool a = hoverFade.Step(), b = pressFade.Step();
            return a || b;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Fade(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Fade(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Fade(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Fade(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var back = Ui.PaintBack(g, this, theme);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            double h = hoverFade.Value, p = pressFade.Value;

            Color rest, fill, text, stroke;
            bool fillRest;
            if (Kind == ButtonKind.Accent)
            {
                rest = theme.Accent;
                fill = Theme.Blend(Theme.Blend(rest, back, 0.1 * h), back, 0.1 * p);
                text = theme.OnAccent;
                stroke = theme.HighContrast ? theme.Text : fill;
                fillRest = true;
            }
            else if (Kind == ButtonKind.Subtle)
            {
                fill = Theme.Blend(back, theme.Text, 0.07 * h - 0.03 * p);
                text = theme.Text;
                stroke = theme.HighContrast ? theme.Text : fill;     // High Contrast: every button shows its edge
                fillRest = h > 0 || p > 0 || theme.HighContrast;     // at rest a subtle button is only its glyph
            }
            else
            {
                fill = Theme.Blend(Theme.Blend(theme.Control, theme.Text, 0.03 * h), back, 0.5 * p);
                text = p > 0.5 ? theme.Sub : theme.Text;
                stroke = theme.ControlStroke;
                fillRest = true;
            }

            if (fillRest)
            {
                using (var path = Ui.Round(new RectangleF(0.5F, 0.5F, Width - 1, Height - 1), Ui.S(4)))
                using (var brush = new SolidBrush(fill))
                using (var pen = new Pen(stroke))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) Ui.FocusRing(g, ClientRectangle, theme.Text);
        }
    }

    /// <summary>
    /// Windows 11 toggle switch. A CheckBox underneath, so Space toggles it and screen readers announce a
    /// check box with its <see cref="Control.Text"/> and state; only the state words and the track are drawn.
    /// </summary>
    internal sealed class ToggleSwitch : CheckBox, IThemed, IAnimated
    {
        private Theme theme = Theme.LightTheme;
        private bool hover;
        private readonly Fader slide = new Fader();     // 0 = off (knob left) .. 1 = on (knob right)
        private bool slideReady;

        public bool Step()
        {
            return slide.Step();
        }

        /// <summary>Drawn left of the track ("Увімк." / "Вимк."); empty strings draw the track only.</summary>
        public string OnText = "", OffText = "";

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            UseMnemonic = false;
            Font = Ui.Body;
        }

        /// <summary>Width for the longer state word plus the 40 x 20 track.</summary>
        public void FitWidth()
        {
            int words = Math.Max(Ui.TextWidth(OnText, Font), Ui.TextWidth(OffText, Font));
            if (string.IsNullOrEmpty(OnText) && string.IsNullOrEmpty(OffText)) words = 0;
            Size = new Size((words > 0 ? words + Ui.S(12) : 0) + Ui.S(46), Ui.S(28));
        }

        public void SetTheme(Theme t)
        {
            theme = t;
            Invalidate();
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            // Slide only when the user flips it on screen; a value set before showing jumps straight there.
            if (slideReady && Visible && slide.To(Checked ? 1 : 0)) Motion.Start(this);
            else slide.Value = Checked ? 1 : 0;
            Invalidate();
            base.OnCheckedChanged(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            slide.Value = Checked ? 1 : 0;
            slide.To(Checked ? 1 : 0);
            slideReady = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var back = Ui.PaintBack(g, this, theme);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = Ui.S(40), h = Ui.S(20);
            var track = new RectangleF(Width - w - Ui.S(3) + 0.5F, (Height - h) / 2F + 0.5F, w - 1, h - 1);
            var words = Checked ? OnText : OffText;
            if (!string.IsNullOrEmpty(words))
            {
                var area = new Rectangle(0, 0, (int)track.Left - Ui.S(12), Height);
                TextRenderer.DrawText(g, words, Font, area, theme.Text,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }

            float knob = Ui.S(hover ? 14 : 12);
            double on = slide.Value;                       // 0..1 while the knob slides
            using (var path = Ui.Round(track, track.Height / 2))
            {
                var off = hover ? Theme.Blend(back, theme.Text, 0.05) : back;
                var accent = hover ? Theme.Blend(theme.Accent, back, 0.1) : theme.Accent;
                using (var b = new SolidBrush(Theme.Blend(off, accent, on))) g.FillPath(b, path);
                if (on < 1 || theme.HighContrast)
                {
                    using (var p = new Pen(Theme.Blend(theme.Sub, accent, on))) g.DrawPath(p, path);
                }
            }
            float left = track.Left + track.Height / 2, right = track.Right - track.Height / 2;
            float cx = left + (right - left) * (float)on;
            float cy = track.Top + track.Height / 2;
            using (var b = new SolidBrush(Theme.Blend(theme.Sub, theme.OnAccent, on)))
            {
                g.FillEllipse(b, cx - knob / 2, cy - knob / 2, knob, knob);
            }
            if (Focused && ShowFocusCues)
            {
                var ring = Rectangle.Round(track);
                ring.Inflate(Ui.S(3), Ui.S(3));
                using (var path = Ui.Round(ring, ring.Height / 2F))
                using (var pen = new Pen(theme.Text, Ui.S(2)))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }

    /// <summary>
    /// A Windows 11 selector-bar tab: plain text, the selected one in the text colour with a short accent
    /// underline, the others in the secondary colour. A RadioButton underneath, so the arrow keys move between
    /// the tabs of one container and screen readers announce which one is selected.
    /// </summary>
    internal sealed class TabButton : RadioButton, IThemed
    {
        private Theme theme = Theme.LightTheme;
        private bool hover;

        public TabButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            UseMnemonic = false;
            Font = Ui.Body;
            Height = Ui.S(32);
        }

        /// <summary>Width for the text plus 8 px padding each side.</summary>
        public void FitWidth()
        {
            Width = Ui.TextWidth(Text, Ui.BodyStrong) + Ui.S(16);
        }

        public void SetTheme(Theme t)
        {
            theme = t;
            Invalidate();
        }

        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var back = Parent != null ? Parent.BackColor : theme.Window;
            g.Clear(hover && !Checked ? Theme.Blend(back, theme.Text, 0.05) : back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            TextRenderer.DrawText(g, Text, Checked ? Ui.BodyStrong : Font, new Rectangle(0, 0, Width, Height - Ui.S(4)),
                Checked || hover ? theme.Text : theme.Sub,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            if (Checked)
            {
                float w = Ui.S(16), h = Ui.S(3);
                using (var path = Ui.Round(new RectangleF((Width - w) / 2, Height - h - 1, w, h), h / 2))
                using (var b = new SolidBrush(theme.Accent))
                {
                    g.FillPath(b, path);
                }
            }
            if (Focused && ShowFocusCues) Ui.FocusRing(g, ClientRectangle, theme.Text);
        }
    }

    /// <summary>
    /// The Windows 11 text field frame around a borderless TextBox or NumericUpDown: rounded, 1 px stroke,
    /// a stronger bottom edge that becomes a 2 px accent line while the field has focus.
    /// </summary>
    internal sealed class FieldHost : Panel, IThemed
    {
        private Theme theme = Theme.LightTheme;
        public readonly Control Field;

        public FieldHost(Control field, int width)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Field = field;
            field.Font = Ui.Body;
            var box = field as TextBox;
            if (box != null) box.BorderStyle = BorderStyle.None;
            var num = field as NumericUpDown;
            if (num != null) num.BorderStyle = BorderStyle.None;
            Size = new Size(width, Ui.S(32));
            Controls.Add(field);
            field.GotFocus += delegate { Invalidate(); };
            field.LostFocus += delegate { Invalidate(); };
            Click += delegate { field.Focus(); };
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int pad = Ui.S(10);
            int width = Width - 2 * pad;
            var num = Field as NumericUpDown;
            if (num != null && num.Controls.Count > 0)
            {
                // No spin buttons in a Windows 11 number box: widen the control by their width and clip them
                // away (hiding them leaves an unpainted block); arrow keys and the mouse wheel still work.
                var spin = num.Controls[0];
                spin.AccessibleRole = AccessibleRole.None;      // clipped away, so not offered to screen readers
                spin.AccessibleName = "";
                int buttons = spin.Width;
                Field.SetBounds(pad, (Height - Field.Height) / 2, width + buttons, Field.Height);
                var old = Field.Region;
                Field.Region = new Region(new Rectangle(0, 0, width, Field.Height));
                if (old != null) old.Dispose();
                return;
            }
            Field.SetBounds(pad, (Height - Field.Height) / 2, width, Field.Height);
        }

        public void SetTheme(Theme t)
        {
            theme = t;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : theme.Window);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5F, 0.5F, Width - 1, Height - 1);
            using (var path = Ui.Round(r, Ui.S(4)))
            using (var fill = new SolidBrush(theme.Control))
            using (var pen = new Pen(theme.ControlStroke))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
            g.SmoothingMode = SmoothingMode.None;
            bool focused = Field.ContainsFocus;
            int line = focused ? Ui.S(2) : 1;
            using (var b = new SolidBrush(focused ? theme.Accent : theme.FieldBottom))
            {
                g.FillRectangle(b, Ui.S(3), Height - line, Width - 2 * Ui.S(3), line);
            }
        }
    }

    /// <summary>
    /// A drop-down list drawn like a Windows 11 ComboBox in both themes. The native control keeps keyboard,
    /// mouse and screen-reader behaviour; its closed box is painted over after Windows draws it.
    /// </summary>
    internal sealed class FluentCombo : ComboBox, IThemed
    {
        private Theme theme = Theme.LightTheme;
        private const int WmPaint = 0x000F;

        public FluentCombo(int width)
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            Font = Ui.Body;
            ItemHeight = Ui.S(26);
            Width = width;
        }

        public void SetTheme(Theme t)
        {
            theme = t;
            BackColor = t.Control;
            ForeColor = t.Text;
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnSelectedIndexChanged(EventArgs e) { Invalidate(); base.OnSelectedIndexChanged(e); }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            // The current item in the open list: accent fill (well over 3:1 against the other rows).
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(selected ? theme.Accent : theme.Control))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }
            var r = e.Bounds;
            r.X += Ui.S(10);
            r.Width -= Ui.S(10);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, r, selected ? theme.OnAccent : theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WmPaint || !IsHandleCreated) return;
            using (var g = Graphics.FromHwnd(Handle))
            {
                var back = Parent != null ? Parent.BackColor : theme.Window;
                g.Clear(back);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = Ui.Round(new RectangleF(0.5F, 0.5F, Width - 1, Height - 1), Ui.S(4)))
                using (var fill = new SolidBrush(theme.Control))
                using (var pen = new Pen(theme.ControlStroke))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }
                int chevron = Ui.S(28);
                var text = new Rectangle(Ui.S(10), 0, Width - Ui.S(10) - chevron, Height);
                TextRenderer.DrawText(g, SelectedIndex >= 0 ? GetItemText(SelectedItem) : "", Font, text, theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                var arrow = new Rectangle(Width - chevron, 0, chevron - Ui.S(4), Height);
                if (Ui.Icons != null)
                {
                    using (var small = new Font(Ui.Icons.FontFamily, 7.5F))
                    {
                        TextRenderer.DrawText(g, Ui.GlyphChevron, small, arrow, theme.Sub,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    }
                }
                if (Focused && ShowFocusCues) Ui.FocusRing(g, ClientRectangle, theme.Text);
            }
        }
    }
}
