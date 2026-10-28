#!/usr/bin/env bash
# Development build on Linux/macOS with Mono (mcs). The release exe is built on
# Windows by build.cmd (GitHub Actions), which also embeds the application manifest.
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p out

FLAGS="-langversion:5 -sdk:4.6.2-api -optimize+"

mcs $FLAGS -target:winexe -platform:anycpu -out:out/UABlackoutPreWarning.exe \
  -win32icon:assets/app.ico -resource:assets/app.ico,UABlackoutPreWarning.app.ico \
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll \
  src/Core/*.cs src/App/*.cs

mcs $FLAGS -target:exe -out:out/Core.Tests.exe -r:System.dll -r:System.Core.dll \
  src/Core/*.cs tests/Core.Tests/*.cs

mono out/Core.Tests.exe tests/Fixtures
