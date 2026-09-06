@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Install the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
dotnet publish ".\src\DeejFeedback\DeejFeedback.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ".\release\DeejFeedback"
if errorlevel 1 (
  echo Build failed. See the errors above.
  pause
  exit /b 1
)
echo Build complete: release\DeejFeedback\DeejFeedback.exe
pause
