// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using UABlackoutPreWarning.Core;

namespace UABlackoutPreWarning
{
    internal static class Program
    {
        private const string MutexName = @"Local\UABlackoutPreWarning.Instance";
        internal const string ShowEventName = @"Local\UABlackoutPreWarning.Show";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    SignalRunningInstance();
                    return;
                }

                TryEnableDpiAwareness();
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += OnThreadException;

                using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
                using (var context = new TrayContext(showEvent))
                {
                    Application.Run(context);
                }

                GC.KeepAlive(mutex);
            }
        }

        /// <summary>A second launch asks the running copy to show its window, then exits.</summary>
        private static void SignalRunningInstance()
        {
            EventWaitHandle handle;
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out handle))
            {
                using (handle) handle.Set();
            }
        }

        private static bool errorShown;

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            if (errorShown) return;
            errorShown = true;
            MessageBox.Show(e.Exception.Message, Strings.Current().AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            errorShown = false;
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private static void TryEnableDpiAwareness()
        {
            // The manifest already declares DPI awareness in release builds; this covers other builds.
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT) SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch (DllNotFoundException)
            {
            }
        }
    }
}
