$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\DeejFeedback\DeejFeedback.csproj"
$output = Join-Path $root "release\DeejFeedback"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host ".NET 8 SDK required: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    Read-Host "Install the SDK, then run again. Press Enter to exit"
    exit 1
}

dotnet restore $project
if ($LASTEXITCODE -ne 0) { throw "Dependency restore failed." }
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $output
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

Write-Host "`nBuild complete: $output\DeejFeedback.exe" -ForegroundColor Green
Write-Host "Self-contained EXE: no separate .NET runtime needed on the target PC."
Read-Host "Press Enter to exit"
