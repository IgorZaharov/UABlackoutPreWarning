@echo off
rem Builds the README screenshot helper (application sources + Shots.cs) and runs it.
rem Usage: tools\screenshots\shots.cmd [culture] [output folder]   e.g. uk-UA out\screenshots
rem Needs an interactive desktop. Changes HKCU\Software\UABlackoutPreWarning (theme, settings).
setlocal
cd /d "%~dp0\..\.."
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist out mkdir out
"%CSC%" /nologo /langversion:5 /codepage:65001 /utf8output /target:exe /platform:anycpu ^
  /out:out\Shots.exe /win32manifest:src\App\app.manifest ^
  /resource:assets\app.ico,UABlackoutPreWarning.app.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  src\Core\*.cs src\App\AboutForm.cs src\App\AssemblyInfo.cs src\App\MainForm.cs src\App\Platform.cs ^
  src\App\SettingsForm.cs src\App\Theme.cs src\App\Toasts.cs src\App\TrayContext.cs tools\screenshots\Shots.cs
if errorlevel 1 exit /b 1
set "CULTURE=%~1"
if "%CULTURE%"=="" set "CULTURE=uk-UA"
set "OUTDIR=%~2"
if "%OUTDIR%"=="" set "OUTDIR=out\screenshots"
out\Shots.exe %CULTURE% "%OUTDIR%" %3
