// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace UABlackoutPreWarning
{
    /// <summary>A window that paints the Mica-like <see cref="Backdrop"/> behind its content.</summary>
    internal interface IGlassWindow
    {
    }

    /// <summary>
    /// Mica drawn by the app. Windows' own Mica needs a window painted with per-pixel alpha, which WinForms text
    /// and native controls cannot do, so the effect is reproduced the way Mica works: the desktop wallpaper,
    /// heavily blurred and tinted with the theme colour, sampled at the window's place on the screen. Only
    /// documented APIs (SPI_GETDESKWALLPAPER). Works on Windows 10 and every Windows 11; solid colour when
    /// "Transparency effects" are off, in High Contrast, without a wallpaper picture, and while the window is
    /// inactive (as Mica). The tint keeps secondary text at 4.5:1 or more over any wallpaper.
    /// </summary>
    internal static class Backdrop
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SystemParametersInfoW")]
        private static extern bool SystemParametersInfo(int action, int size, StringBuilder buffer, int winIni);

        private const int SpiGetDeskWallpaper = 0x0073;
        private const int SampleWidth = 64;                  // the wallpaper is kept this small: stretching it blurs it
        private const int LightTint = 235, DarkTint = 217;   // alpha of the theme colour over the wallpaper

        private static Bitmap sample;
        private static Size sampleScreen;
        private static bool loaded;
        private static bool? transparency;

        /// <summary>Forget the wallpaper and settings (wallpaper, display or personalisation changed).</summary>
        public static void Reset()
        {
            if (sample != null) sample.Dispose();
            sample = null;
            loaded = false;
            transparency = null;
        }

        public static bool Enabled(Theme t)
        {
            return !t.HighContrast && Environment.OSVersion.Platform == PlatformID.Win32NT && TransparencyOn();
        }

        /// <summary>True when controls placed directly on <paramref name="parent"/> sit on the backdrop.</summary>
        public static bool IsGlass(Control parent, Theme t)
        {
            return parent is IGlassWindow && Enabled(t);
        }

        /// <summary>Paints the backdrop behind <paramref name="c"/> (the window itself or a control on it).</summary>
        public static void Paint(Graphics g, Control c, Theme t)
        {
            var area = c.ClientRectangle;
            var form = c as Form ?? c.FindForm();
            var image = Enabled(t) && form != null && Form.ActiveForm == form ? Sample(Screen.FromControl(form).Bounds) : null;
            if (image == null)
            {
                using (var solid = new SolidBrush(t.Window)) g.FillRectangle(solid, area);
                return;
            }
            var screen = Screen.FromControl(form).Bounds;
            var origin = c.PointToScreen(Point.Empty);
            float sx = image.Width / (float)screen.Width, sy = image.Height / (float)screen.Height;
            var state = g.Save();
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            using (var attributes = new ImageAttributes())
            {
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(image, area, (origin.X - screen.X) * sx, (origin.Y - screen.Y) * sy,
                    area.Width * sx, area.Height * sy, GraphicsUnit.Pixel, attributes);
            }
            g.Restore(state);
            using (var tint = new SolidBrush(Color.FromArgb(t.Dark ? DarkTint : LightTint, t.Window))) g.FillRectangle(tint, area);
        }

        /// <summary>The wallpaper cropped like "Fill" to the screen's shape and shrunk to 64 px wide; null if none.</summary>
        private static Bitmap Sample(Rectangle screen)
        {
            if (loaded && sampleScreen == screen.Size) return sample;
            Reset();
            loaded = true;
            sampleScreen = screen.Size;
            try
            {
                var path = new StringBuilder(260);
                if (!SystemParametersInfo(SpiGetDeskWallpaper, path.Capacity, path, 0)) return null;
                if (path.Length == 0 || !File.Exists(path.ToString())) return null;
                using (var stream = new MemoryStream(File.ReadAllBytes(path.ToString())))
                using (var picture = Image.FromStream(stream, false, false))
                {
                    float screenAspect = screen.Width / (float)screen.Height;
                    float w = picture.Width, h = picture.Height;
                    RectangleF crop = w / h > screenAspect
                        ? new RectangleF((w - h * screenAspect) / 2, 0, h * screenAspect, h)
                        : new RectangleF(0, (h - w / screenAspect) / 2, w, w / screenAspect);
                    int height = Math.Max(1, (int)Math.Round(SampleWidth / screenAspect));
                    sample = new Bitmap(SampleWidth, height, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(sample))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(picture, new Rectangle(0, 0, SampleWidth, height), crop, GraphicsUnit.Pixel);
                    }
                }
            }
            catch (Exception)
            {
                if (sample != null) sample.Dispose();
                sample = null;
            }
            return sample;
        }

        /// <summary>Windows' "Transparency effects" switch (Settings > Personalisation > Colours); on when unknown.</summary>
        private static bool TransparencyOn()
        {
            if (transparency.HasValue) return transparency.Value;
            bool on = true;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key == null ? null : key.GetValue("EnableTransparency");
                    if (value is int) on = (int)value != 0;
                }
            }
            catch (Exception)
            {
            }
            transparency = on;
            return on;
        }
    }

    /// <summary>A control with running animations; <see cref="Step"/> returns false when they have finished.</summary>
    internal interface IAnimated
    {
        bool Step();
    }

    /// <summary>
    /// One 15 ms timer for all running animations; it runs only while something moves. Animations are skipped
    /// when Windows' "Animation effects" are off (SPI_GETCLIENTAREAANIMATION).
    /// </summary>
    internal static class Motion
    {
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
        private static extern bool SystemParametersInfo(int action, int size, ref int value, int winIni);

        private const int SpiGetClientAreaAnimation = 0x1042;
        private static readonly List<Control> running = new List<Control>();
        private static Timer timer;

        public static bool Enabled
        {
            get
            {
                try
                {
                    int on = 1;
                    return !SystemParametersInfo(SpiGetClientAreaAnimation, 0, ref on, 0) || on != 0;
                }
                catch (EntryPointNotFoundException)
                {
                    return true;
                }
            }
        }

        public static void Start(Control c)
        {
            if (!running.Contains(c)) running.Add(c);
            if (timer == null)
            {
                timer = new Timer();
                timer.Interval = 15;
                timer.Tick += delegate { Tick(); };
            }
            timer.Start();
        }

        private static void Tick()
        {
            foreach (var c in running.ToArray())
            {
                bool more = !c.IsDisposed && ((IAnimated)c).Step();
                if (!c.IsDisposed) c.Invalidate();
                if (!more) running.Remove(c);
            }
            if (running.Count == 0) timer.Stop();
        }
    }

    /// <summary>A value that eases to its target in 150 ms (instantly when animations are off).</summary>
    internal sealed class Fader
    {
        private const float Duration = 150F;
        private float from, target;
        private int started;

        public float Value;

        /// <summary>Sets a new target; true when an animation has to run.</summary>
        public bool To(float value)
        {
            if (value == target && Value == target) return false;
            from = Value;
            target = value;
            started = Environment.TickCount;
            if (!Motion.Enabled)
            {
                Value = target;
                return false;
            }
            return true;
        }

        /// <summary>Advances the value; true while it is still moving.</summary>
        public bool Step()
        {
            float k = Math.Min(1F, (Environment.TickCount - started) / Duration);
            float eased = 1 - (1 - k) * (1 - k);
            Value = from + (target - from) * eased;
            return k < 1;
        }
    }

    /// <summary>
    /// The lightbulb of the app's icon, drawn at any size: lit (yellow) while there is power, grey and crossed
    /// out in red during an outage. Used for the tray, taskbar and window icons and the state badge.
    /// </summary>
    internal static class Bulb
    {
        private static readonly Color Lit = Color.FromArgb(0xF5, 0xC5, 0x18), LitEdge = Color.FromArgb(0xB8, 0x8F, 0x00);
        private static readonly Color Dark = Color.FromArgb(0xB8, 0xB8, 0xB8), DarkEdge = Color.FromArgb(0x7A, 0x7A, 0x7A);
        private static readonly Color Base = Color.FromArgb(0x8A, 0x8A, 0x8A), Slash = Color.FromArgb(0xD1, 0x34, 0x38);

        /// <summary>Full-colour bulb in <paramref name="r"/>.</summary>
        public static void Draw(Graphics g, RectangleF r, bool off)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = r.Width, h = r.Height;
            var glass = new RectangleF(r.X + 0.19F * w, r.Y + 0.09F * h, 0.62F * w, 0.62F * h);
            using (var b = new SolidBrush(off ? Dark : Lit)) g.FillEllipse(b, glass);
            using (var p = new Pen(off ? DarkEdge : LitEdge, Math.Max(1F, w / 24))) g.DrawEllipse(p, glass);
            using (var path = Ui.Round(new RectangleF(r.X + 0.34F * w, r.Y + 0.70F * h, 0.32F * w, 0.20F * h), 0.05F * w))
            using (var b = new SolidBrush(Base))
            {
                g.FillPath(b, path);
            }
            if (off) DrawSlash(g, r, Slash, 0.11F);
        }

        /// <summary>Single-colour outline bulb (for the badge beside the status caption).</summary>
        public static void DrawOutline(Graphics g, RectangleF r, bool off, Color ink)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = r.Width, h = r.Height, stroke = Math.Max(1.2F, w / 10);
            using (var p = new Pen(ink, stroke))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawEllipse(p, r.X + 0.2F * w, r.Y + 0.08F * h, 0.6F * w, 0.6F * h);
                g.DrawLine(p, r.X + 0.38F * w, r.Y + 0.80F * h, r.X + 0.62F * w, r.Y + 0.80F * h);
                g.DrawLine(p, r.X + 0.42F * w, r.Y + 0.94F * h, r.X + 0.58F * w, r.Y + 0.94F * h);
            }
            if (off) DrawSlash(g, r, ink, 0.1F);
        }

        private static void DrawSlash(Graphics g, RectangleF r, Color colour, float thickness)
        {
            using (var p = new Pen(colour, Math.Max(1.5F, r.Width * thickness)))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, r.X + 0.14F * r.Width, r.Bottom - 0.10F * r.Height, r.Right - 0.14F * r.Width, r.Y + 0.08F * r.Height);
            }
        }
    }

    /// <summary>The four icons that follow the power state: small (tray) and large (window, taskbar), lit and crossed.</summary>
    internal sealed class StateIcons : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        private readonly Icon smallOn, smallOff, largeOn, largeOff;

        public StateIcons()
        {
            smallOn = Make(SystemInformation.SmallIconSize.Width, false);
            smallOff = Make(SystemInformation.SmallIconSize.Width, true);
            largeOn = Make(SystemInformation.IconSize.Width, false);
            largeOff = Make(SystemInformation.IconSize.Width, true);
        }

        public Icon Small(bool off) { return off ? smallOff : smallOn; }

        public Icon Large(bool off) { return off ? largeOff : largeOn; }

        private static Icon Make(int size, bool off)
        {
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    Bulb.Draw(g, new RectangleF(0, 0, size, size), off);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        public void Dispose()
        {
            foreach (var icon in new[] { smallOn, smallOff, largeOn, largeOff })
            {
                var handle = icon.Handle;
                icon.Dispose();
                DestroyIcon(handle);    // Icon.FromHandle does not own the handle
            }
        }
    }

    /// <summary>Round badge with the outline bulb before the status caption (crossed out while the power is off).</summary>
    internal sealed class StateBadge : Control, IThemed
    {
        private Theme theme = Theme.LightTheme;
        private bool off;

        public StateBadge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.Graphic;
            Size = new Size(Ui.S(24), Ui.S(24));
        }

        public bool PowerOff
        {
            get { return off; }
            set
            {
                off = value;
                Invalidate();
            }
        }

        public void SetTheme(Theme t)
        {
            theme = t;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var back = Parent != null ? Parent.BackColor : theme.Card;
            g.Clear(back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var ink = off ? theme.Warn : theme.Accent;
            using (var b = new SolidBrush(theme.HighContrast ? back : Theme.Blend(back, ink, 0.14)))
            {
                g.FillEllipse(b, 0, 0, Width - 1, Height - 1);
            }
            float pad = Width * 0.22F;
            Bulb.DrawOutline(g, new RectangleF(pad, pad, Width - 2 * pad, Height - 2 * pad), off, ink);
        }
    }

    /// <summary>The thin progress line under the time; announced as a progress bar with its percentage.</summary>
    internal sealed class ProgressLine : Control, IThemed
    {
        private Theme theme = Theme.LightTheme;
        private double value = -1;
        private bool outage;

        public ProgressLine()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.ProgressBar;
            Height = Ui.S(4);
        }

        /// <summary>0..1, or negative to hide the bar; <paramref name="isOutage"/> paints it in the warning colour.</summary>
        public void SetValue(double share, bool isOutage)
        {
            value = share;
            outage = isOutage;
            AccessibleDescription = share < 0 ? "" : ((int)Math.Round(share * 100)) + " %";
            Invalidate();
        }

        public void SetTheme(Theme t)
        {
            theme = t;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var back = Parent != null ? Parent.BackColor : theme.Card;
            g.Clear(back);
            if (value < 0) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float r = Height / 2F;
            using (var path = Ui.Round(new RectangleF(0, 0, Width, Height), r))
            using (var b = new SolidBrush(theme.HighContrast ? back : Theme.Blend(back, theme.Text, 0.09)))
            {
                g.FillPath(b, path);
                if (theme.HighContrast) using (var p = new Pen(theme.Text)) g.DrawPath(p, path);
            }
            float fill = (float)(Width * Math.Max(0, Math.Min(1, value)));
            if (fill < 1) return;
            using (var path = Ui.Round(new RectangleF(0, 0, Math.Max(fill, Height), Height), r))
            using (var b = new SolidBrush(outage ? theme.Warn : theme.Accent))
            {
                g.FillPath(b, path);
            }
        }
    }
}
