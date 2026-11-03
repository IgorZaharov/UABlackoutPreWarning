// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;
using UABlackoutPreWarning.Core;

namespace UABlackoutPreWarning
{
    /// <summary>Settings in HKCU\Software\UABlackoutPreWarning (no files beside the exe).</summary>
    internal sealed class RegistrySettingsStore : ISettingsStore
    {
        internal const string KeyPath = @"Software\UABlackoutPreWarning";

        public AppSettings Load(out bool firstRun)
        {
            var s = new AppSettings();
            using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
            {
                firstRun = key == null;
                if (key != null)
                {
                    s.ScheduleUrl = GetString(key, "ScheduleUrl", s.ScheduleUrl);
                    s.Queue = GetString(key, "Queue", s.Queue);
                    s.LeadMinutes = GetInt(key, "LeadMinutes", s.LeadMinutes);
                    s.RefreshMinutes = GetInt(key, "RefreshMinutes", s.RefreshMinutes);
                    s.StartWithWindows = GetInt(key, "StartWithWindows", 0) != 0;
                    s.DarkTheme = GetInt(key, "DarkTheme", 0) != 0;
                    s.Language = GetString(key, "Language", s.Language);
                    s.KnownQueues = GetString(key, "KnownQueues", s.KnownQueues);
                    s.NotifiedSeries = GetString(key, "NotifiedSeries", s.NotifiedSeries);
                }
            }
            s.Normalize();
            return s;
        }

        public void Save(AppSettings s)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                key.SetValue("ScheduleUrl", s.ScheduleUrl ?? "", RegistryValueKind.String);
                key.SetValue("Queue", s.Queue ?? "", RegistryValueKind.String);
                key.SetValue("LeadMinutes", s.LeadMinutes, RegistryValueKind.DWord);
                key.SetValue("RefreshMinutes", s.RefreshMinutes, RegistryValueKind.DWord);
                key.SetValue("StartWithWindows", s.StartWithWindows ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("DarkTheme", s.DarkTheme ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("Language", s.Language ?? "", RegistryValueKind.String);
                key.SetValue("KnownQueues", s.KnownQueues ?? "", RegistryValueKind.String);
                key.SetValue("NotifiedSeries", s.NotifiedSeries ?? "", RegistryValueKind.String);
            }
        }

        private const string WindowPositionValue = "MainWindowPosition";

        /// <summary>Where the main window was last closed ("x,y" in screen pixels), or null.</summary>
        public Point? LoadWindowPosition()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    var text = key == null ? null : key.GetValue(WindowPositionValue) as string;
                    if (string.IsNullOrEmpty(text)) return null;
                    var parts = text.Split(',');
                    int x, y;
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out x) &&
                        int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out y))
                    {
                        return new Point(x, y);
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public void SaveWindowPosition(Point p)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    key.SetValue(WindowPositionValue,
                        p.X.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                        p.Y.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
                }
            }
            catch (Exception)
            {
            }
        }

        private static string GetString(RegistryKey key, string name, string fallback)
        {
            var v = key.GetValue(name) as string;
            return v ?? fallback;
        }

        private static int GetInt(RegistryKey key, string name, int fallback)
        {
            var v = key.GetValue(name);
            if (v is int) return (int)v;
            int parsed;
            if (v is string && int.TryParse((string)v, out parsed)) return parsed;
            return fallback;
        }
    }

    /// <summary>Start with Windows: the per-user Run key, no admin rights needed.</summary>
    internal static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "UABlackoutPreWarning";

        /// <summary>Adds (with the current exe path) or removes the startup entry.</summary>
        public static void Apply(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
                else key.DeleteValue(ValueName, false);
            }
        }
    }

    /// <summary>HTTP GET with the app's user agent, a timeout, no cache and a size limit.</summary>
    internal static class Http
    {
        private const int TimeoutMs = 20000;

        public static byte[] Get(string url, string accept, int maxBytes, out string contentType)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = TimeoutMs;
            request.ReadWriteTimeout = TimeoutMs;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) UABlackoutPreWarning/" + AppInfo.Version;
            request.Accept = accept;
            request.Headers[HttpRequestHeader.AcceptLanguage] = "uk-UA,uk;q=0.9,en;q=0.5";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            if (request.Proxy != null) request.Proxy.Credentials = CredentialCache.DefaultNetworkCredentials;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var buffer = new MemoryStream())
            {
                // .NET Framework follows https -> http redirects; never read an https request's answer over http.
                if (request.RequestUri.Scheme == Uri.UriSchemeHttps && response.ResponseUri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new WebException("Redirected from https to " + response.ResponseUri.Scheme + ".");
                }
                var chunk = new byte[64 * 1024];
                int read;
                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    buffer.Write(chunk, 0, read);
                    if (buffer.Length > maxBytes) throw new InvalidDataException("The response is too large.");
                }
                contentType = response.ContentType;
                return buffer.ToArray();
            }
        }
    }

    /// <summary>Downloads and parses the schedule page.</summary>
    internal static class ScheduleFetcher
    {
        private const int MaxBytes = 8 * 1024 * 1024;

        public static ParsedPage Fetch(string url)
        {
            string contentType;
            var bytes = Http.Get(url, "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8", MaxBytes, out contentType);
            return ScheduleParser.Parse(PageDecoder.Decode(bytes, contentType));
        }

        /// <summary>Short, human-readable reason for a failure.</summary>
        public static string Describe(Exception ex)
        {
            var agg = ex as AggregateException;
            if (agg != null && agg.InnerExceptions.Count > 0) ex = agg.InnerExceptions[0];
            var web = ex as WebException;
            string text;
            if (web != null)
            {
                var resp = web.Response as HttpWebResponse;
                text = resp != null ? "HTTP " + (int)resp.StatusCode : web.Status.ToString();
            }
            else
            {
                text = ex.Message;
            }
            if (text.Length > 120) text = text.Substring(0, 117) + "...";
            return text;
        }
    }

    /// <summary>
    /// Gives memory back to Windows when the app is idle in the tray. .NET keeps freed memory and Windows
    /// keeps the pages of loaded components in the process until asked, so Task Manager would otherwise
    /// show the peak (for example after the first notification) until the app is restarted.
    /// </summary>
    internal static class MemoryTrim
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimum, IntPtr maximum);

        public static void Run()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            try
            {
                // (-1, -1) = remove as many pages as possible from the working set; they come back on use.
                SetProcessWorkingSetSize(GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
            }
            catch (EntryPointNotFoundException)
            {
            }
        }
    }

    /// <summary>A donations.json entry with its site's favicon (null when it could not be read).</summary>
    internal sealed class DonationEntry
    {
        public DonationLink Link;
        public Image Icon;
    }

    /// <summary>
    /// Reads donations.json from the repository on GitHub (once per start) and each link's favicon. Icons are
    /// kept in memory only, so nothing is written to disk.
    /// </summary>
    internal static class DonationFetcher
    {
        private const int MaxJsonBytes = 64 * 1024, MaxPageBytes = 1024 * 1024, MaxIconBytes = 512 * 1024;
        private const int MaxHeadChars = 64 * 1024, MaxIconPixels = 512;
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        private static readonly Regex IconLink = new Regex(
            "<link\\b[^>]*\\brel\\s*=\\s*[\"']?[^\"'>]*\\bicon\\b[^>]*>", RegexOptions.IgnoreCase, RegexTimeout);
        private static readonly Regex Href = new Regex(
            "\\bhref\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.IgnoreCase, RegexTimeout);

        public static List<DonationEntry> Load(int iconSize)
        {
            string contentType;
            var bytes = Http.Get(AppInfo.DonationsUrl, "application/json,text/plain;q=0.9,*/*;q=0.5", MaxJsonBytes, out contentType);
            return FromJson(Encoding.UTF8.GetString(bytes), iconSize);
        }

        /// <summary>The entries of a donations.json text, each with its favicon (downloaded now).</summary>
        public static List<DonationEntry> FromJson(string json, int iconSize)
        {
            var result = new List<DonationEntry>();
            foreach (var link in DonationList.Parse(json))
            {
                var entry = new DonationEntry();
                entry.Link = link;
                entry.Icon = Favicon(link.Url, iconSize);
                result.Add(entry);
            }
            return result;
        }

        /// <summary>The site's /favicon.ico, else the first icon its page declares; null when neither is usable.</summary>
        private static Image Favicon(string pageUrl, int size)
        {
            var page = new Uri(pageUrl);
            var icon = Picture(new Uri(page, "/favicon.ico"), size);
            if (icon != null) return icon;
            try
            {
                string contentType;
                var html = PageDecoder.Decode(Http.Get(pageUrl, "text/html,*/*;q=0.5", MaxPageBytes, out contentType), contentType);
                // Icons are declared in <head>: search only there (and at most 64 KB), with a regex time limit.
                int end = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                if (end < 0 || end > MaxHeadChars) end = Math.Min(html.Length, MaxHeadChars);
                html = html.Substring(0, end);
                foreach (Match tag in IconLink.Matches(html))
                {
                    var href = Href.Match(tag.Value);
                    if (!href.Success) continue;
                    var value = WebUtility.HtmlDecode(href.Groups[1].Value + href.Groups[2].Value + href.Groups[3].Value).Trim();
                    Uri target;
                    if (!Uri.TryCreate(page, value, out target) || target.Scheme != Uri.UriSchemeHttps) continue;
                    icon = Picture(target, size);
                    if (icon != null) return icon;
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        /// <summary>
        /// An .ico, .png, .gif or .jpg (checked by its first bytes, so no metafiles or other GDI+ formats) as a
        /// size x size bitmap; null when missing, unreadable, under 16 px or over 512 px.
        /// </summary>
        private static Image Picture(Uri url, int size)
        {
            try
            {
                string contentType;
                var bytes = Http.Get(url.AbsoluteUri, "image/*,*/*;q=0.5", MaxIconBytes, out contentType);
                bool ico = Starts(bytes, 0x00, 0x00, 0x01, 0x00);
                bool known = ico || Starts(bytes, 0x89, 0x50, 0x4E, 0x47) || Starts(bytes, 0x47, 0x49, 0x46, 0x38) ||
                             Starts(bytes, 0xFF, 0xD8, 0xFF);
                if (!known) return null;
                if (ico)
                {
                    try
                    {
                        using (var stream = new MemoryStream(bytes))
                        using (var icon = new Icon(stream, size, size))
                        using (var bitmap = icon.ToBitmap())
                        {
                            if (icon.Width >= 16) return Scaled(bitmap, size);
                        }
                    }
                    catch (Exception)
                    {
                        // PNG-compressed icon frames: GDI+ below can still read the file.
                    }
                }
                using (var stream = new MemoryStream(bytes))
                using (var image = Image.FromStream(stream, false, false))
                {
                    if (image.Width < 16 || image.Height < 16) return null;    // e.g. a 1 x 1 placeholder
                    if (image.Width > MaxIconPixels || image.Height > MaxIconPixels) return null;
                    return Scaled(image, size);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Starts(byte[] bytes, params byte[] magic)
        {
            if (bytes.Length < magic.Length) return false;
            for (int i = 0; i < magic.Length; i++)
            {
                if (bytes[i] != magic[i]) return false;
            }
            return true;
        }

        /// <summary>A new size x size copy of <paramref name="source"/> (the caller keeps and disposes the source).</summary>
        private static Image Scaled(Image source, int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(source, 0, 0, size, size);
            }
            return bmp;
        }
    }

    internal static class AppInfo
    {
        public const string AuthorUrl = "https://github.com/IgorZaharov";
        public const string AuthorText = "github.com/IgorZaharov";
        public const string ManualUrl = "https://github.com/IgorZaharov/UABlackoutPreWarning/blob/main/README.md";
        public const string IssuesUrl = "https://github.com/IgorZaharov/UABlackoutPreWarning/issues";
        public const string SchedulesUrl = "https://www.ztoe.com.ua/unhooking-search.php";
        public const string FacebookUrl = "https://www.facebook.com/ztoe.com.ua";

        /// <summary>donations.json on the main branch; read at every start.</summary>
        public const string DonationsUrl = "https://raw.githubusercontent.com/IgorZaharov/UABlackoutPreWarning/main/donations.json";

        /// <summary>Opens a web page in the default browser; a failure is shown over <paramref name="owner"/>.</summary>
        public static void OpenUrl(IWin32Window owner, string url)
        {
            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, ex.Message, Strings.Current().AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public static string Version
        {
            get
            {
                var attr = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    typeof(AppInfo).Assembly, typeof(AssemblyInformationalVersionAttribute));
                return attr != null ? attr.InformationalVersion : "1.0";
            }
        }

        /// <summary>The embedded multi-size application icon at the requested size.</summary>
        public static Icon LoadIcon(Size size)
        {
            using (var stream = typeof(AppInfo).Assembly.GetManifestResourceStream("UABlackoutPreWarning.app.ico"))
            {
                if (stream == null) return (Icon)SystemIcons.Application.Clone();
                return new Icon(stream, size);
            }
        }
    }
}
