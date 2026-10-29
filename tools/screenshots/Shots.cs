// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.
//
// Screenshot helper for the README (not part of the application).
// Compiled together with the application sources by tools\screenshots\shots.cmd. It runs the app
// with the UI language given on the command line (default uk-UA), opens the main, settings and
// About windows in the dark and light themes and saves each window as a PNG in the output folder.
// Used by .github/workflows/screenshots.yml on a GitHub-hosted Windows runner.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace UABlackoutPreWarning
{
    internal static class Shots
    {
        private static string outDir;
        private static TrayContext app;

        [STAThread]
        private static int Main(string[] args)
        {
            var culture = new CultureInfo(args.Length > 0 ? args[0] : "uk-UA");
            outDir = Path.GetFullPath(args.Length > 1 ? args[1] : "screenshots");
            Directory.CreateDirectory(outDir);
            var log = new StreamWriter(Path.Combine(outDir, "log.txt"), false, System.Text.Encoding.UTF8);
            log.AutoFlush = true;
            Console.SetOut(log);
            Console.SetError(log);

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            SetProcessDPIAware();
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            app = new TrayContext(new EventWaitHandle(false, EventResetMode.AutoReset));

            // (seconds to wait before the step, step)
            var steps = new List<KeyValuePair<int, MethodInvoker>>();
            steps.Add(Step(1, delegate { CloseOthers(); app.SetDarkTheme(true); app.ShowWindow(); }));
            steps.Add(Step(25, delegate { Save(Find<MainForm>(), "main-window-dark"); }));     // time for the download
            steps.Add(Step(1, delegate { app.ShowSettings(); }));
            steps.Add(Step(3, delegate { Save(Find<SettingsForm>(), "settings-window-dark"); }));
            steps.Add(Step(1, delegate { CloseOthers(); app.ShowAbout(); }));
            steps.Add(Step(3, delegate { Save(Find<AboutForm>(), "about-window-dark"); }));
            steps.Add(Step(1, delegate { CloseOthers(); app.SetDarkTheme(false); app.ShowWindow(); }));
            steps.Add(Step(3, delegate { Save(Find<MainForm>(), "main-window-light"); }));
            steps.Add(Step(1, delegate { app.ShowSettings(); }));
            steps.Add(Step(3, delegate { Save(Find<SettingsForm>(), "settings-window-light"); }));
            steps.Add(Step(1, delegate { CloseOthers(); app.ShowAbout(); }));
            steps.Add(Step(3, delegate { Save(Find<AboutForm>(), "about-window-light"); }));
            // Blackout warning as a toast: on screen, then in the Notifications panel (Win+N).
            steps.Add(Step(1, delegate { CloseOthers(); Find<MainForm>().Hide(); }));
            steps.Add(Step(1, delegate
            {
                bool ok = Toasts.Show("Відключення о 12:00 (через 10 хв)", "До 15:00 · черга 5.2. Збережіть роботу.", true);
                Console.WriteLine("Toast shown: {0} {1}", ok, Toasts.LastError);
            }));
            steps.Add(Step(4, delegate { SaveScreen("toast-on-screen"); }));
            steps.Add(Step(1, delegate { PressWinN(); }));
            steps.Add(Step(3, delegate { SaveScreen("notification-center"); }));
            steps.Add(Step(1, delegate { app.ExitApplication(); }));

            int index = 0;
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = steps[0].Key * 1000;
            timer.Tick += delegate
            {
                timer.Stop();
                try
                {
                    steps[index].Value();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Step {0} failed: {1}", index, ex);
                }
                index++;
                if (index < steps.Count)
                {
                    timer.Interval = steps[index].Key * 1000;
                    timer.Start();
                }
            };
            timer.Start();

            Application.Run(app);
            return 0;
        }

        private static KeyValuePair<int, MethodInvoker> Step(int seconds, MethodInvoker action)
        {
            return new KeyValuePair<int, MethodInvoker>(seconds, action);
        }

        private static T Find<T>() where T : Form
        {
            foreach (Form f in Application.OpenForms)
            {
                var t = f as T;
                if (t != null && t.Visible) return t;
            }
            throw new InvalidOperationException(typeof(T).Name + " is not open");
        }

        /// <summary>Closes the settings and About windows; the main window stays.</summary>
        private static void CloseOthers()
        {
            var open = new List<Form>();
            foreach (Form f in Application.OpenForms) open.Add(f);
            foreach (var f in open)
            {
                if (f is SettingsForm || f is AboutForm) f.Close();
            }
        }

        /// <summary>Saves the window as it appears on screen, title bar included.</summary>
        private static void Save(Form form, string name)
        {
            form.TopMost = true;
            form.Activate();
            if (form is SettingsForm)
            {
                // The focused address box would show its whole text highlighted; not useful in a picture.
                ClearSelection(form);
                form.ActiveControl = null;
            }
            form.Refresh();
            Application.DoEvents();
            Thread.Sleep(500);
            Application.DoEvents();

            var r = FrameBounds(form);
            using (var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(r.Location, Point.Empty, r.Size);
                }
                var path = Path.Combine(outDir, name + ".png");
                bmp.Save(path, ImageFormat.Png);
                Console.WriteLine("Saved {0} ({1}x{2})", path, r.Width, r.Height);
            }
            form.TopMost = false;
        }

        private static void SaveScreen(string name)
        {
            var r = Screen.PrimaryScreen.Bounds;
            using (var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
                var path = Path.Combine(outDir, name + ".png");
                bmp.Save(path, ImageFormat.Png);
                Console.WriteLine("Saved {0} ({1}x{2})", path, r.Width, r.Height);
            }
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

        private static void PressWinN()
        {
            const byte Win = 0x5B, N = 0x4E;
            const uint Up = 0x0002;
            keybd_event(Win, 0, 0, UIntPtr.Zero);
            keybd_event(N, 0, 0, UIntPtr.Zero);
            keybd_event(N, 0, Up, UIntPtr.Zero);
            keybd_event(Win, 0, Up, UIntPtr.Zero);
        }

        private static void ClearSelection(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                var box = c as TextBox;
                if (box != null) box.Select(0, 0);
                ClearSelection(c);
            }
        }

        // ---- native ---------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private const int DwmExtendedFrameBounds = 9;

        /// <summary>The visible window frame (without the invisible resize borders of Windows 10/11).</summary>
        private static Rectangle FrameBounds(Form form)
        {
            RECT r;
            try
            {
                if (DwmGetWindowAttribute(form.Handle, DwmExtendedFrameBounds, out r, Marshal.SizeOf(typeof(RECT))) == 0)
                {
                    return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                }
            }
            catch (DllNotFoundException)
            {
            }
            return form.Bounds;
        }
    }
}
