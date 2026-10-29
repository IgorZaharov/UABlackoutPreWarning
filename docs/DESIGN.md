# UABlackoutPreWarning — Design (as built, v1.3.1)

UABlackoutPreWarning is a portable Windows tray utility. It reads the Zhytomyroblenergo (ZTOE) outage
schedule and warns the user a set number of minutes before each scheduled blackout, so work can be
saved on a PC without a UPS. User documentation (Ukrainian) is in [README.md](../README.md).

- **Deliverable:** one `UABlackoutPreWarning.exe` (~200 KB), no installer, no files beside it. At run time it
  writes only the registry and one toast icon (`%LOCALAPPDATA%\UABlackoutPreWarning\toast-icon.png`).
- **Runs on:** clean Windows 10 (1607+) and Windows 11, x86 and x64. Needs only the .NET Framework 4.6.2+
  that ships with Windows.
- **Licence:** GNU GPL v3.
- **Out of scope:** other oblenergos, emergency (unscheduled) outages, mobile or web versions.

## Source page

The schedule is read from `https://www.ztoe.com.ua/unhooking-search.php`: server-rendered HTML in
`windows-1251`, no login and no JavaScript. Verified against a saved copy from 28.10.2026, 19:30 update
(`tests/Fixtures/ztoe-2026-10-28-1930.html`).

| Aspect | Finding |
| --- | --- |
| Banner | Free text in the first `<h3>` ("УВАГА! ВАЖЛИВА ІНФОРМАЦІЯ!" and the current restriction notice) |
| Update time | `<h4>Дата оновлення інформації - 19:30 28.10.2026</h4>` |
| Grid table | One `<table … table-layout:fixed …>` per published day |
| Grid date | Header cell with `colspan="48"` holding `<b>28.10.2026</b>` |
| Columns | "Черга", "Підчерга", a spacer, then 48 half-hour headers `00:00-00:30` … `23:30-24:00` |
| Rows | 12 rows labelled `1.1` … `6.2` (a `<b>` inside a link to `?pidcherga_id=1…12`) |
| Slot cells | 48 per row; `background: #ff3333` = no power, `#ffffff` = power on |
| Legend | Its swatch is `#ff0000`, so the rule is "any non-white background = no power" |

Fixture check: queue 5.2 parses to 06:00–09:00, 14:00–16:00 and 19:00–21:00.

In the evening the site publishes two grids, today and tomorrow (`tests/Fixtures/ztoe-2026-10-28-2100.html`).
Each grid's date comes from its header cell; the day strip shows the grid dated today, warnings cover both
days, and a next outage on another day is labelled ("завтра 09:00–12:00").

## Requirements

| ID | Requirement |
| --- | --- |
| F1 | Tray icon. Right-click menu: Open, Check now, About, Close Application. Double-click opens the main window. |
| F2 | Main window: status, plus gear (settings), "?" (about) and the Day/Night switch. Separate settings window: schedule URL, queue drop-down, warning lead time, refresh interval, Start with Windows, Restore defaults, Check now, Save. |
| F3–F4 | Download and parse on start, then every refresh interval. |
| F5 | Consecutive red slots form one blackout series. |
| F6 | One notification per series, X minutes before it starts. |
| F7 | Closing the window hides it; only Close Application exits. |
| F8 | All settings saved in the registry (HKCU) on Save and restored at start-up. |
| F9 | Queue drop-down lists the row labels found on the page. |
| F10 | Start with Windows via the per-user Run key. |
| F11 | Standard Windows notification sound. Each warning shows the tray balloon and a toast; the toast uses the reminder scenario (stays on screen until dismissed) and is kept in the Notifications panel. |
| F15 | "Test notification" in the settings window: the same notification for the nearest blackout in the saved queue, marked "Тест:", not remembered (NotifiedSeries unchanged). |
| F13 | Day/Night switch (sun/moon) in the main window: light or dark theme for all windows and the tray menu, applied and saved at once without Save. |
| F14 | About window: description, version, links to the author, the manual (README) and GitHub Issues. |
| F12 | UI language follows Windows: Ukrainian, otherwise English. |

## Architecture

**C# 5, WinForms, .NET Framework 4.6.2, AnyCPU, no third-party libraries.** One AnyCPU exe runs as a
32-bit process on x86 Windows and as 64-bit on x64.

| Choice | Reason |
| --- | --- |
| .NET Framework 4.6.2 target | Built into Windows 10 1607+ (LTSC 2016/2019 included) and all Windows 11 |
| C# 5 language level | The compiler shipped inside Windows (`v4.0.30319\csc.exe`) can build it; no SDK needed |
| WinRT toasts via a registry AppUserModelID | Toasts stay in the Notifications panel (tray balloons do not). A portable exe has no Start-menu shortcut, so the ID is registered under `HKCU\Software\Classes\AppUserModelId\IgorZaharov.UABlackoutPreWarning` (DisplayName, IconUri). The WinRT types are loaded by reflection (`ContentType=WindowsRuntime`), so no SDK metadata is needed to build. Falls back to `NotifyIcon.ShowBalloonTip` if toasts fail |
| Hand-written HTML parser | No HtmlAgilityPack, so no extra DLL |
| Registry settings | Keeps the "no auxiliary files" rule |
| In-code string tables | No `.resx` satellite assemblies (they would be extra DLLs) |
| Memory trim when idle | 5 s after a download, notification or closed window (and only with no window open): full GC and `SetProcessWorkingSetSize(-1, -1)`. Working set goes from ~65 MB to ~4–9 MB in the tray; windows are closed (not hidden) and rebuilt on open |
| Kyiv time zone | Schedule times are converted through `FLE Standard Time`, so a PC in another zone still warns correctly |

### Components

| File | Component | Responsibility | Unit-tested |
| --- | --- | --- | --- |
| `src/Core/ScheduleParser.cs` | `ScheduleParser` | Grid(s), dates, update time, banner, row labels, red-cell detection | Yes |
| `src/Core/Schedule.cs` | `SeriesBuilder` | Red slots → series, midnight join across days, current/next series | Yes |
| `src/Core/Schedule.cs` | `WarningPlanner` | Which warnings are due; remembers the last 20 warned series | Yes |
| `src/Core/Schedule.cs` | `KyivClock`, `PageDecoder` | Kyiv wall-clock time; charset detection (header, meta, cp1251) | Yes |
| `src/Core/Settings.cs` | `AppSettings` | Defaults, ranges, normalisation, queue list | Yes |
| `src/Core/Strings.cs` | `Strings` | Ukrainian and English text, time/duration formatting | Yes |
| `src/App/TrayContext.cs` | `TrayContext` | Tray icon, menu, timers, downloads, warnings, tooltip, sleep/resume | No |
| `src/App/MainForm.cs` | `MainForm`, `DayStrip`, `Glyphs` | Main window: toolbar (Segoe MDL2 gear and "?" icons), status area, day strip (24 hour blocks, labels every 2 h) | No |
| `src/App/SettingsForm.cs` | `SettingsForm` | Settings window: fields, validation, Restore defaults, Save; opens over the main window, one at a time | No |
| `src/App/Theme.cs` | `Theme`, `ThemedGroupBox`, `ThemeToggle` | Light/dark colour sets applied in place, dark group frames, the switch, dark title bar (DWM), dark tray menu | No |
| `src/App/AboutForm.cs` | `AboutForm` | Name, version, description, links to the author, the manual and Issues | No |
| `src/App/Toasts.cs` | `Toasts` | AppUserModelID registration, toast icon file, toast XML, show via WinRT | No |
| `src/App/Platform.cs` | `RegistrySettingsStore`, `Autostart`, `ScheduleFetcher` | Registry, Run key, HTTP download | No |
| `src/App/Program.cs` | `Program` | Single instance, DPI awareness, TLS 1.2, start-up | No |

Two timers drive the app: the refresh timer downloads and parses the page; a separate 30-second timer
evaluates warnings against the last good schedule. A failed download never stops a warning that is
already known.

## Notification logic

1. Build series for the selected queue from every published day; a series ending at 24:00 joins one
   starting at 00:00 on the next published day.
2. Key each series by its start, e.g. `2026-10-28T14:00`.
3. Every 30 s: a series with `start − lead ≤ now < start` whose key is not yet remembered → notification,
   remember the key (last 20 keys are also saved to the registry).
4. Keys older than 24 h are pruned.

| Situation | Behaviour |
| --- | --- |
| App starts 4 min before a series | Warns at once ("через 4 хв") |
| App starts during a series | No warning; status shows "Зараз світла немає" until its end |
| Start time moves (14:00 → 15:00) | New key → new warning |
| Only the end time changes | Same key → no second warning |
| Download or parse fails | Keeps the last good schedule; retries after 1, 2, 5 min; one "may be out of date" notice after 3 failures |
| PC resumes from sleep | Immediate refresh and evaluation |

## Settings (registry)

`HKCU\Software\UABlackoutPreWarning`

| Value | Type | Default | Holds |
| --- | --- | --- | --- |
| `ScheduleUrl` | REG_SZ | `https://www.ztoe.com.ua/unhooking-search.php` | Schedule page |
| `Queue` | REG_SZ | `5.2` | Selected row |
| `LeadMinutes` | REG_DWORD | 10 | 1–120 |
| `RefreshMinutes` | REG_DWORD | 30 | 5–240 |
| `StartWithWindows` | REG_DWORD | 0 | Checkbox |
| `KnownQueues` | REG_SZ | `1.1;…;6.2` | Row labels last seen on the page |
| `NotifiedSeries` | REG_SZ | empty | Last 20 warned series |
| `DarkTheme` | REG_DWORD | 0 | Day/Night switch; saved the moment it is clicked |

Start with Windows writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\UABlackoutPreWarning`
(the exe path in quotes) and rewrites it at every start, so a moved exe recovers after one manual launch.

## Build and test

| Command | Where | What |
| --- | --- | --- |
| `build.cmd` | Any Windows 10/11 | Builds with the built-in .NET Framework `csc.exe`, embeds icon and manifest, runs the tests |
| `build.sh` | Linux/macOS with Mono | Development build (no manifest; DPI awareness is also set in code) and tests |
| GitHub Actions | `windows-latest` | `build.cmd` on every push; a `v*` tag or a published release gets `UABlackoutPreWarning.zip` attached |

`tests/Core.Tests` is a dependency-free test runner (23 tests) covering the fixture, synthetic markup
(no `<tbody>`, `bgcolor`, multiple days, malformed rows), planner rules, settings and strings.

Manual test matrix before a release: Windows 10 22H2 x86 and x64, Windows 10 LTSC 2019 x64, Windows 11
x64 at 100 % and 150 % scaling. Check: start from Downloads (SmartScreen), one toast with sound, no repeat
after restart, stale notice with the network unplugged, Start with Windows on and off.

## Risks

| Risk | Mitigation |
| --- | --- |
| ZTOE changes the table markup | Loose matching; clear status message instead of a crash; fixture makes fixes quick |
| Banner overrides the grid | Banner text is shown in the status area; the app does not interpret it |
| Site slow or down | Last good schedule kept; retries; stale notice |
| Unsigned exe | SmartScreen prompt on first run (README explains); code signing optional |
| Do Not Disturb / Focus Assist | Toast may be hidden; README explains |
