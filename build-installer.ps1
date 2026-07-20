param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src\PushoverHotkeys\PushoverHotkeys.csproj"
$publishDirectory = Join-Path $PSScriptRoot "artifacts\publish"

# Keep the managed application in one executable for Windows application-control policies.
# Extracting native libraries is required for WPF to load its Windows components correctly.
if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $publishDirectory `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($iscc)) {
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($iscc)) {
    throw "Inno Setup не найден. Установите Inno Setup 6 и запустите этот скрипт снова."
}

& $iscc (Join-Path $PSScriptRoot "installer\PushoverHotkeys.iss")
