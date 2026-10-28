// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
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
                key.SetValue("KnownQueues", s.KnownQueues ?? "", RegistryValueKind.String);
                key.SetValue("NotifiedSeries", s.NotifiedSeries ?? "", RegistryValueKind.String);
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

    /// <summary>Downloads and parses the schedule page.</summary>
    internal static class ScheduleFetcher
    {
        private const int TimeoutMs = 20000;
        private const int MaxBytes = 8 * 1024 * 1024;

        public static ParsedPage Fetch(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = TimeoutMs;
            request.ReadWriteTimeout = TimeoutMs;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) UABlackoutPreWarning/" + AppInfo.Version;
            request.Accept = "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8";
            request.Headers[HttpRequestHeader.AcceptLanguage] = "uk-UA,uk;q=0.9,en;q=0.5";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            if (request.Proxy != null) request.Proxy.Credentials = CredentialCache.DefaultNetworkCredentials;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[64 * 1024];
                int read;
                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    buffer.Write(chunk, 0, read);
                    if (buffer.Length > MaxBytes) throw new InvalidDataException("The page is too large.");
                }
                var html = PageDecoder.Decode(buffer.ToArray(), response.ContentType);
                return ScheduleParser.Parse(html);
            }
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

    internal static class AppInfo
    {
        public const string AuthorUrl = "https://github.com/IgorZaharov";
        public const string AuthorText = "github.com/IgorZaharov";

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
