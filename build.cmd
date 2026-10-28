@echo off
rem Builds UABlackoutPreWarning.exe with the C# compiler that ships with
rem the .NET Framework 4.x in every Windows 10/11 - no SDK or Visual Studio needed.
rem Output: out\UABlackoutPreWarning.exe (single file), then runs the unit tests.
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo .NET Framework 4.x compiler not found.
    exit /b 1
)

if not exist out mkdir out
set COMMON=/nologo /langversion:5 /optimize+ /codepage:65001 /utf8output

echo Building application...
"%CSC%" %COMMON% /target:winexe /platform:anycpu /out:out\UABlackoutPreWarning.exe ^
  /win32icon:assets\app.ico /win32manifest:src\App\app.manifest ^
  /resource:assets\app.ico,UABlackoutPreWarning.app.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  src\Core\*.cs src\App\*.cs
if errorlevel 1 exit /b 1

echo Building tests...
"%CSC%" %COMMON% /target:exe /out:out\Core.Tests.exe /r:System.dll /r:System.Core.dll ^
  src\Core\*.cs tests\Core.Tests\*.cs
if errorlevel 1 exit /b 1

echo Running tests...
out\Core.Tests.exe tests\Fixtures
if errorlevel 1 exit /b 1

echo.
echo Build OK: out\UABlackoutPreWarning.exe
