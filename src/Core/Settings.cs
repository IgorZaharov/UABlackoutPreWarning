// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;

namespace UABlackoutPreWarning.Core
{
    /// <summary>User settings. Stored in HKCU\Software\UABlackoutPreWarning.</summary>
    public sealed class AppSettings
    {
        public const string DefaultUrl = "https://www.ztoe.com.ua/unhooking-search.php";
        public const string DefaultQueue = "5.2";
        public const int DefaultLeadMinutes = 10;
        public const int DefaultRefreshMinutes = 30;
        public const int MinLead = 1, MaxLead = 120;
        public const int MinRefresh = 5, MaxRefresh = 240;

        public static readonly string[] DefaultQueues =
        {
            "1.1", "1.2", "2.1", "2.2", "3.1", "3.2", "4.1", "4.2", "5.1", "5.2", "6.1", "6.2"
        };

        public AppSettings()
        {
            ScheduleUrl = DefaultUrl;
            Queue = DefaultQueue;
            LeadMinutes = DefaultLeadMinutes;
            RefreshMinutes = DefaultRefreshMinutes;
            StartWithWindows = false;
            KnownQueues = string.Join(";", DefaultQueues);
            NotifiedSeries = "";
        }

        public string ScheduleUrl { get; set; }
        public string Queue { get; set; }
        public int LeadMinutes { get; set; }
        public int RefreshMinutes { get; set; }
        public bool StartWithWindows { get; set; }

        /// <summary>Row labels last seen on the page, semicolon-separated.</summary>
        public string KnownQueues { get; set; }

        /// <summary>Keys of the last announced outages (see <see cref="WarningPlanner"/>).</summary>
        public string NotifiedSeries { get; set; }

        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }

        /// <summary>Replaces missing or out-of-range values with defaults.</summary>
        public void Normalize()
        {
            if (!IsValidUrl(ScheduleUrl)) ScheduleUrl = DefaultUrl;
            ScheduleUrl = ScheduleUrl.Trim();
            if (string.IsNullOrEmpty(Queue) || !IsQueueLabel(Queue.Trim())) Queue = DefaultQueue;
            Queue = Queue.Trim();
            if (LeadMinutes < MinLead || LeadMinutes > MaxLead) LeadMinutes = DefaultLeadMinutes;
            if (RefreshMinutes < MinRefresh || RefreshMinutes > MaxRefresh) RefreshMinutes = DefaultRefreshMinutes;
            if (ParseQueues(KnownQueues).Count == 0) KnownQueues = string.Join(";", DefaultQueues);
            if (NotifiedSeries == null) NotifiedSeries = "";
        }

        /// <summary>Queue labels for the drop-down; always contains the selected queue.</summary>
        public List<string> KnownQueueList()
        {
            var list = ParseQueues(KnownQueues);
            if (!string.IsNullOrEmpty(Queue) && IsQueueLabel(Queue) && !list.Contains(Queue)) list.Add(Queue);
            return list;
        }

        private static List<string> ParseQueues(string value)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(value)) return list;
            foreach (var part in value.Split(';'))
            {
                var q = part.Trim();
                if (IsQueueLabel(q) && !list.Contains(q)) list.Add(q);
            }
            return list;
        }

        public static bool IsValidUrl(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            Uri uri;
            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri)) return false;
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        public static bool IsQueueLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split('.');
            int a, b;
            return parts.Length == 2 &&
                   int.TryParse(parts[0], out a) && int.TryParse(parts[1], out b) &&
                   a >= 1 && a <= 99 && b >= 1 && b <= 99;
        }
    }

    /// <summary>Persistence for <see cref="AppSettings"/>.</summary>
    public interface ISettingsStore
    {
        /// <summary>Loads settings; returns defaults when nothing is stored. Sets <paramref name="firstRun"/>.</summary>
        AppSettings Load(out bool firstRun);

        void Save(AppSettings settings);
    }
}
