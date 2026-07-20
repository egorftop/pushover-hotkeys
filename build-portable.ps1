param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src\PushoverHotkeys\PushoverHotkeys.csproj"
$publishDirectory = Join-Path $PSScriptRoot "artifacts\portable"
$portableExe = Join-Path $PSScriptRoot "artifacts\PushoverHotkeys.exe"

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $publishDirectory `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item -LiteralPath (Join-Path $publishDirectory "PushoverHotkeys.exe") -Destination $portableExe -Force
Write-Output "Portable executable: $portableExe"
