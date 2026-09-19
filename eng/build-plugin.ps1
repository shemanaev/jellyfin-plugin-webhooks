param(
    [string[]]$Profile,
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Jellyfin.Webhooks/Jellyfin.Webhooks.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$profilesConfig = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "jellyfin-profiles.json") | ConvertFrom-Json
if (-not $Profile) { $Profile = @($profilesConfig.buildProfiles | ForEach-Object { $_.profile }) }
$Profile = @($Profile | ForEach-Object {
    $requested = $_
    $server = $profilesConfig.serverProfiles | Where-Object profile -eq $requested | Select-Object -First 1
    if ($server) { return [string]$server.buildProfile }
    $build = $profilesConfig.buildProfiles | Where-Object profile -eq $requested | Select-Object -First 1
    if ($build) { return [string]$build.profile }
    throw "Unknown Jellyfin profile '$requested'."
} | Select-Object -Unique)

foreach ($currentProfile in $Profile) {
    $output = Join-Path $artifactsRoot $currentProfile
    $safeRoot = [System.IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $safeOutput = [System.IO.Path]::GetFullPath($output)
    if (-not $safeOutput.StartsWith($safeRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean artifact path outside '$safeRoot': '$safeOutput'"
    }
    if (Test-Path -LiteralPath $safeOutput) { Remove-Item -LiteralPath $safeOutput -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $safeOutput | Out-Null
    & dotnet build $project -c $Configuration -p:JellyfinProfile=$currentProfile -p:OutputPath="$safeOutput/"
    if ($LASTEXITCODE -ne 0) { throw "Build failed for Jellyfin $currentProfile." }
    & (Join-Path $PSScriptRoot "write-build-yaml.ps1") -Profile $currentProfile -OutputPath (Join-Path $safeOutput "build.yaml")
}
