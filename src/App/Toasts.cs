// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Security;
using Microsoft.Win32;

namespace UABlackoutPreWarning
{
    /// <summary>
    /// Windows 10/11 toast notifications that also stay in the Notifications panel (tray balloons do not).
    /// A portable exe has no Start-menu shortcut, so the app's AppUserModelID is registered per user under
    /// HKCU\Software\Classes\AppUserModelId (no admin rights). The WinRT classes are reached by
    /// reflection, so no Windows SDK metadata is needed at build time. Any failure returns false and the
    /// caller falls back to a tray balloon.
    /// </summary>
    internal static class Toasts
    {
        public const string AppId = "IgorZaharov.UABlackoutPreWarning";
        public const string AppIdKey = @"Software\Classes\AppUserModelId\" + AppId;

        private const string WinRT = ", ContentType=WindowsRuntime";
        private static object notifier;
        private static Type xmlType, toastType, notifierType;

        /// <summary>The last failure, for diagnostics.</summary>
        public static string LastError { get; private set; }

        /// <summary>
        /// Toasts take their header icon only from an image file, so the app icon is written once to
        /// %LOCALAPPDATA%\UABlackoutPreWarning\toast-icon.png (re-created if it is deleted).
        /// </summary>
        public static string IconPath
        {
            get
            {
                return Path.Combine(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UABlackoutPreWarning"),
                    "toast-icon.png");
            }
        }

        /// <summary>
        /// Shows a toast. <paramref name="sticky"/> uses the reminder scenario: the toast stays on screen
        /// until the user dismisses it. Returns false if toasts are not available.
        /// </summary>
        public static bool Show(string title, string text, bool sticky)
        {
            try
            {
                EnsureReady();

                var xml = new System.Text.StringBuilder();
                xml.Append(sticky ? "<toast scenario=\"reminder\">" : "<toast>");
                xml.Append("<visual><binding template=\"ToastGeneric\">");
                xml.Append("<text>").Append(SecurityElement.Escape(title)).Append("</text>");
                xml.Append("<text>").Append(SecurityElement.Escape(text)).Append("</text>");
                xml.Append("</binding></visual>");
                if (sticky)
                {
                    // A reminder needs a button; the system "dismiss" action (text localised by Windows)
                    // works without any COM activator.
                    xml.Append("<actions><action activationType=\"system\" arguments=\"dismiss\" content=\"\"/></actions>");
                }
                xml.Append("</toast>");

                object doc = null, toast = null;
                try
                {
                    doc = Activator.CreateInstance(xmlType);
                    xmlType.GetMethod("LoadXml", new[] { typeof(string) }).Invoke(doc, new object[] { xml.ToString() });
                    toast = Activator.CreateInstance(toastType, doc);
                    notifierType.GetMethod("Show").Invoke(notifier, new[] { toast });
                }
                finally
                {
                    // Windows keeps its own reference to a shown toast; release ours now instead of at the next GC.
                    Release(toast);
                    Release(doc);
                }
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                LastError = inner.GetType().Name + ": " + inner.Message;
                notifier = null;      // try again from scratch next time
                return false;
            }
        }

        private static void Release(object o)
        {
            try
            {
                if (o != null && System.Runtime.InteropServices.Marshal.IsComObject(o))
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(o);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void EnsureReady()
        {
            if (notifier != null) return;
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || Environment.OSVersion.Version.Major < 10)
            {
                throw new PlatformNotSupportedException("Windows 10 or later is required for toasts");
            }

            Register();

            var manager = Type.GetType("Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications" + WinRT, true);
            toastType = Type.GetType("Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications" + WinRT, true);
            notifierType = Type.GetType("Windows.UI.Notifications.ToastNotifier, Windows.UI.Notifications" + WinRT, true);
            xmlType = Type.GetType("Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument" + WinRT, true);
            notifier = manager.GetMethod("CreateToastNotifier", new[] { typeof(string) }).Invoke(null, new object[] { AppId });
        }

        /// <summary>Registers the app's name and icon for its toasts.</summary>
        private static void Register()
        {
            string icon = WriteIcon();
            using (var key = Registry.CurrentUser.CreateSubKey(AppIdKey))
            {
                key.SetValue("DisplayName", "UA Blackout Pre-Warning", RegistryValueKind.String);
                if (icon != null) key.SetValue("IconUri", icon, RegistryValueKind.String);
                else key.DeleteValue("IconUri", false);
            }
        }

        /// <summary>The icon PNG path, written from the embedded icon if missing; null if it cannot be written.</summary>
        private static string WriteIcon()
        {
            var path = IconPath;
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var icon = AppInfo.LoadIcon(new Size(64, 64)))
                using (var bitmap = icon.ToBitmap())
                {
                    bitmap.Save(path, ImageFormat.Png);
                }
                return path;
            }
            catch (Exception)
            {
                return null;     // toasts still work, just without an icon
            }
        }
    }
}
