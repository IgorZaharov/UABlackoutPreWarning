# CLAUDE.md - UABlackoutPreWarning

Handoff and working notes for Claude (Claude Code or claude.ai). Read this first, then `docs/DESIGN.md`.
Owner: Igor Zakharov (GitHub `IgorZaharov`). Last updated 2026-11-03, latest release **v1.5.0**.

## 1. Goal

A small Windows tray utility that warns **before a scheduled power outage** in Ukraine, so work can be saved
on a PC without a UPS. It reads the Zhytomyroblenergo (ZTOE) outage schedule
`https://www.ztoe.com.ua/unhooking-search.php`, finds the user's queue (row, e.g. `5.2`), and shows one
Windows notification per outage series, N minutes before it starts.

### Hard requirements (agreed at the start; do not break)

- **One portable exe**, no installer, no files next to the exe. Settings live in the registry.
  Agreed exception: one toast icon PNG in `%LOCALAPPDATA%\UABlackoutPreWarning\` (Igor chose this in v1.2.0).
- Runs on **clean Windows 10 and 11, x86 and x64**, nothing extra to install.
- UI language follows Windows: Ukrainian if the Windows UI is Ukrainian, otherwise English.
- Licence GNU GPL v3. User manual in **Ukrainian** (`README.md`).

## 2. Architecture decisions

| Decision | Why |
| --- | --- |
| C# **5** (no `$""`, `?.`, `nameof`, auto-property initialisers), WinForms, **.NET Framework 4.6.2**, AnyCPU | Builds with the `csc.exe` that ships inside Windows (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319`); .NET 4.6.2+ is built into Win10 1607+ and Win11. No SDK, no NuGet. |
| No third-party libraries; hand-written HTML parser (regex) | No extra DLLs. |
| UI built in code, no designer files, no .resx | Single exe, no satellite assemblies. Strings in `src/Core/Strings.cs` (uk + en). |
| Layout in 96-DPI units with `AutoScaleMode.Dpi`, **but anything positioned relative to other controls is placed from real post-scaling bounds** (`OnLoad`/`OnLayout`) | Igor's PC runs at **125 %**; fixed positions broke twice there (toolbar v1.1.1, "Saved" label v1.3.3). CI runs at 100 % only. |
| Settings in `HKCU\Software\UABlackoutPreWarning` | "No auxiliary files". |
| Autostart via `HKCU\...\CurrentVersion\Run\UABlackoutPreWarning` (quoted exe path, rewritten each start) | No admin rights; a moved exe recovers after one manual start. |
| Notifications = **WinRT toasts** via reflection (`Type.GetType("…, ContentType=WindowsRuntime")`), AppUserModelID `IgorZaharov.UABlackoutPreWarning` registered under `HKCU\Software\Classes\AppUserModelId` | Toasts stay in the Notifications panel; tray balloons do not. No Start-menu shortcut needed; no SDK metadata at build time. |
| Blackout warning uses toast **reminder scenario** + system Dismiss button | Stays on screen until dismissed (hard to miss); system dismiss needs no COM activator. |
| Tray **balloon only as fallback** when the toast fails | Showing both doubled every warning (tried in v1.3.0, reverted in v1.3.2). |
| Kyiv time via `FLE Standard Time` / `Europe/Kyiv` | Correct warnings even if the PC is in another time zone. |
| Memory trim when idle (GC + `SetProcessWorkingSetSize(-1,-1)`), 5 s after a download / notification / closed window, only with no window open | Working set ~65 MB -> ~4-9 MB in the tray (measured on Windows). |
| Main window is **closed** (rebuilt on open), not hidden | Frees its controls; part of the memory work. |
| Dark theme with documented APIs only (flat controls, owner-drawn combo, custom group box paint + `WS_CLIPCHILDREN`, DWM attribute 20/19 for the title bar, `ToolStripProfessionalRenderer`) | No undocumented uxtheme ordinals. |

### Schedule parsing (`src/Core/ScheduleParser.cs`)

- Page is `windows-1251`, server-rendered. One grid table per published day (evening pages have today and
  tomorrow). Grid date from the `colspan="48"` header; 48 half-hour cells per row after the label `N.M`.
- **Any non-white background = no power** (`#ff3333` off, `#ffffff` on; legend swatch `#ff0000`).
- Notice text = first `<h3>`, update time from "Дата оновлення інформації - HH:MM DD.MM.YYYY".

### Warning logic (`src/Core/Schedule.cs`)

- Consecutive red slots -> one series (joined across midnight). Series key = start time `yyyy-MM-ddTHH:mm`.
- Every 30 s: `start - lead <= now < start` and key not yet warned -> notify, remember key.
- Last 20 keys persisted in registry `NotifiedSeries`. **One warning per series, kept across restarts;
  changing "Warn before" afterwards does not re-warn** (by design; README troubleshooting says so).
- "Тестове сповіщення" button shows the nearest blackout and does **not** touch `NotifiedSeries`.

## 3. Code map

| Path | What |
| --- | --- |
| `src/Core/Models.cs` | `Slots`, `DaySchedule`, `ParsedPage`, `BlackoutSeries`, `Warning` |
| `src/Core/ScheduleParser.cs` | HTML -> `ParsedPage` |
| `src/Core/Schedule.cs` | `SeriesBuilder`, `WarningPlanner`, `KyivClock`, `PageDecoder` |
| `src/Core/Settings.cs` | `AppSettings` (defaults, ranges, `Normalize`, `Language`) |
| `src/Core/Strings.cs` | All UI text, uk + en, `ForLanguage`, time/duration formatting, day-strip spoken text |
| `src/Core/Donations.cs` | `DonationList.Parse` (hand-written reader for donations.json; titled https links only) |
| `src/Core/NextBlackout.cs` | `NextBlackout.Describe`: the main window's next-blackout text across today and tomorrow, "--:--" with a note when nothing is ahead |
| `src/App/Program.cs` | Single instance (mutex + `Local\UABlackoutPreWarning.Show` event), DPI aware, TLS 1.2 |
| `src/App/TrayContext.cs` | Tray icon/menu, timers (warn 30 s, refresh, retry 1/2/5 min), downloads, `Notify`, `SendTestNotification`, memory trim, window position, `ChangeLanguage`, donations download |
| `src/App/MainForm.cs` | Main window: toolbar (queue, gear, ?, dark-theme switch), status card, `DayStrip`, site notice, links |
| `src/App/SettingsForm.cs` | "Параметри"/"Preferences": sections Графік / Сповіщення / Система as cards; Save, Check now |
| `src/App/QueueHelpForm.cs` | "Як дізнатися свою чергу" dialog (no title bar) |
| `src/App/AboutForm.cs` | Version, description, links: author, manual (README), Issues; "Donations" section |
| `src/App/Visuals.cs` | `Backdrop` (Mica-like wallpaper backdrop drawn by the app), `Motion`/`Fader` (150 ms animations), `Bulb`/`StateIcons` (tray, taskbar and window icons follow the power state), `StateBadge`, `ProgressLine` |
| `src/App/Theme.cs` | Windows 11 colours, `Ui` (pixel scale `Ui.S`, fonts, glyphs), owner-drawn `Card`, `FluentButton`, `ToggleSwitch`, `FieldHost`, `FluentCombo`; title bar/Mica via DWM; tray menu |
| `src/App/Toasts.cs` | AUMID registration, toast icon PNG, toast XML, WinRT show, COM release |
| `src/App/Platform.cs` | `RegistrySettingsStore` (+ `MainWindowPosition`), `Autostart`, `Http`, `ScheduleFetcher`, `DonationFetcher` (json + favicons, memory only), `MemoryTrim`, `AppInfo` (URLs, `OpenUrl`) |
| `donations.json` | Donation links shown in About; the app reads it from `raw.githubusercontent.com/.../main/donations.json`, so edits take effect after a push, without a release |
| `tests/Core.Tests/Program.cs` | Dependency-free test runner, 40 tests; fixtures in `tests/Fixtures/` (saved ZTOE pages) |
| `tools/screenshots/` | `Shots.cs` + `shots.cmd`: runs the real app for screenshots or a memory probe. Mode `windows` = window pictures only (both themes, queue help, a language switch; uses the local donations.json), safe to run on Igor's PC |
| `tools/make_icon.py` | Generates `assets/app.ico` (crossed-out lightbulb) |
| `.github/workflows/build.yml` | Build + tests on every push; attaches `UABlackoutPreWarning.zip` to releases |
| `.github/workflows/screenshots.yml` | Manual: inputs `culture` (default `uk-UA`) and `mode` (`''` = screenshots, `memory` = memory/position probe); force-pushes results + `log.txt` to branch `screenshots` |

Registry values (`HKCU\Software\UABlackoutPreWarning`): `ScheduleUrl`, `Queue`, `LeadMinutes` (1-120),
`RefreshMinutes` (5-240), `StartWithWindows`, `DarkTheme`, `Language` (empty = as Windows, `uk`, `en`),
`KnownQueues`, `NotifiedSeries`, `MainWindowPosition`.

UI layout rule since 1.4.0: windows use `AutoScaleMode.None` and place every control in real pixels with
`Ui.S(96-DPI value)` and measured text (`Ui.Fit`, `Ui.TextHeight`) in each form's `Arrange()`. Never test
`Control.Visible` before a window is shown (it reads false); forms keep their own flags (`hasNotice`, ...).

## 4. Build, test, release

- Windows: `build.cmd` -> `out\UABlackoutPreWarning.exe` and runs the tests.
- Tests: `src/Core/` only (it references `System`/`System.Core` only, so keep UI, registry and HTTP out of it).
  The runner has no per-test filter; it runs every `Run("name", Method)` in `Main`. Re-run without rebuilding:
  `out\Core.Tests.exe tests\Fixtures`. `tests/Fixtures/*.html` are raw cp1251 captures: never re-encode them.
- Files: UTF-8, CRLF, 4-space indent (`.editorconfig`); `.sh` uses LF.
- Linux/macOS dev build: `build.sh` (Mono `mcs -langversion:5 -sdk:4.6.2-api`). Mono previews do **not** look
  like Windows (number boxes render empty, no MDL2 icons); use the `screenshots` workflow for real pictures.
- **Version**: bump all three attributes in `src/App/AssemblyInfo.cs` (`1.x.y.0` / `1.x.y`) and push to
  `main` **before** Igor publishes the release; the tag is created from `main`'s head at publish time.
- **Releases are published by Igor** on github.com (new tag `vX.Y.Z` on `main`). CI then attaches
  `UABlackoutPreWarning.zip` (exe inside). Keep that asset name: the README links to
  `releases/latest/download/UABlackoutPreWarning.zip`.
- After Igor says "done": check that the tag points to the intended commit, both CI runs are green, the zip is
  attached, and the README "latest" link serves an exe with the new `FileVersion`.
- CI has a concurrency group per tag (publishing fires a tag-push run and a release run; they used to race).

## 5. Conventions agreed with Igor

- **Commit directly to `main` without asking** (explicit permission). Commit messages end with the
  attribution trailer the session provides (e.g. `Co-Authored-By: Claude …`).
- Plan first and ask for confirmation on big or ambiguous changes (his first instruction); small fixes he
  reports can be done straight away.
- Keep the single-exe / no-install / Win10-11 x86+x64 constraints. Ask before adding any file on disk.
- `README.md` is the **Ukrainian user manual**, simple language, app icon on top, download link first.
  Igor edits it on GitHub too and **uses `-` instead of `—`**: keep hyphens; `git pull` before editing.
- Update `README.md` and `docs/DESIGN.md` together with behaviour changes; refresh screenshots via the
  `screenshots` workflow (Ukrainian UI, dark theme in the README).
- Test UI against **125 % scaling** in your head: nothing that depends on another control's position may use
  fixed 96-DPI numbers.
- Avoid creating extra branches: in the old cloud session remote branches could not be deleted and Igor had to
  clean them up. Run probes from `main`.
- Verify on real Windows where possible (screenshots workflow, or Igor's PC in Claude Code), not just Mono.
- Do not route around network/policy blocks; ask Igor instead.

## 6. History (what is finished)

| Version | Change |
| --- | --- |
| 1.0.0 | Tray app, parser, warnings, settings window, autostart, registry, Ukrainian README, GPL |
| 1.0.1 | Dark-mode fixes (strip edge, queue box height) |
| 1.0.2 | "Start with Windows" click area ends at its caption |
| 1.1.0 | Settings moved to their own window; main window = status + gear / ? / Day-Night; About links to manual and Issues |
| 1.1.1 | Toolbar placed after DPI scaling (was broken at 125 %) |
| 1.2.0 | WinRT toasts kept in Notifications, reminder scenario, toast icon in LocalAppData |
| 1.3.0 | "Тестове сповіщення" button; (toast + balloon both - reverted in 1.3.2) |
| 1.3.1 | Memory trim when idle; toast COM objects released; main window closed instead of hidden |
| 1.3.2 | Only the toast; balloon only as fallback |
| 1.3.3 | "Saved" label placed from real positions (was hidden behind Restore defaults); CI concurrency fix |
| 1.3.4 | Main window reopens where it was last closed (`MainWindowPosition`, centred if off-screen) |
| 1.3.5 | Accessibility fixes from a UI review: Queue/minute fields named for screen readers; invalid URL error shown as text under the field (was an ErrorProvider icon); dark day-strip red `#F25F55` (3.5:1 against grey) |
| 1.4.0 | Issue #1 Windows 11 redesign (approved mockup) + UI language switch, queue help dialog, main-window links (Графіки, Facebook), Donations from donations.json with favicons, Сьогодні / Завтра tabs and "--:--" when no next blackout is known (`Core/NextBlackout.cs`). Checked with `shots.cmd ... windows` on Igor's PC (uk-UA and en-US, both themes, 100 %); not yet at 125 %/150 % or on Windows 10 |
| 1.5.0 | "Premium" polish from the updated mockup: Mica-like backdrop drawn from the wallpaper (system Mica needs per-pixel alpha, which WinForms cannot paint; works on 21H2 and Win10), 8 px cards with a shadow edge, 150 ms hover/press and switch animations, bulb badge and progress line in the status card, rounded strip blocks with the current hour raised and a hover tooltip; tray/taskbar/window icon = lit bulb, crossed-out during an outage. `shots.cmd` captures show solid backgrounds (Windows does not give Shots.exe the foreground, and the backdrop shows only on the active window); Mica checked with a probe on Igor's PC |

## 7. Open items

1. **Issue #1 - Windows 11 redesign**: built in 1.4.0 from the approved mockup
   (https://claude.ai/artifact/PgYuaq5LWNUiCPP4316ogB), together with the four features Igor asked for to
   match the Android app (language switch, queue help, main-window links, Donations). Decisions: the system
   title bar is kept but takes the window colour (Mica on 22H2+); no borderless window. "Donations" is the
   section title in both languages (Igor's wording). Favicons are memory only: ask before caching on disk.
   savelife.in.ua answers 403 to automated requests, so its icon is a letter tile; do not try to get around it.
   Still to check: 125 % and 150 % scaling, Windows 10, Narrator on the new controls; then close issue #1.
2. Optional, never requested: code signing (Azure Trusted Signing) to stop SmartScreen/Chrome download
   warnings for the unsigned exe.
3. Optional: clicking a toast does not open the app window (would need a COM activator / protocol).
4. Optional: the GitHub repo description still says "notification balloon"; Igor's setting to change.

## 8. Next steps

1. Igor tries 1.5.0 on his PC (125 %): Mica on all windows, both themes, icon switching during an outage,
   strip tooltip, animations; Narrator on the progress line.
2. v1.5.0 is published (tag pushed by Claude on Igor's request; CI created the release). Close issue #1
   after step 1.
