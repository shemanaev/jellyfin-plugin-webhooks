param(
    [string[]]$Profile,
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [int]$TimeoutSeconds = 120,
    [string]$DockerPath,
    [switch]$SkipBuild,
    [switch]$KeepContainers
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repoRoot "artifacts"
$smokeRoot = Join-Path $artifactsRoot "smoke"
$profilesConfig = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "jellyfin-profiles.json") | ConvertFrom-Json
if (-not $Profile) { $Profile = @($profilesConfig.serverProfiles | ForEach-Object { $_.profile }) }
if (-not $DockerPath) {
    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if ($docker) { $DockerPath = $docker.Source }
}
if (-not $DockerPath) { throw "docker was not found. Add it to PATH or pass -DockerPath." }

function Invoke-Docker([string[]]$Arguments) {
    & $DockerPath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "docker $($Arguments -join ' ') failed with exit code $LASTEXITCODE" }
}

function Remove-SmokeContainer([string]$Name) {
    $existing = & $DockerPath ps -a --filter "name=^/$Name$" --format "{{.Names}}"
    if ($LASTEXITCODE -ne 0) { throw "docker ps failed with exit code $LASTEXITCODE" }
    if ($existing -contains $Name) { Invoke-Docker @("rm", "-f", $Name) | Out-Null }
}

function Wait-ForStartup([string]$Name) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Seconds 2
        $state = & $DockerPath inspect -f "{{.State.Status}}" $Name 2>$null
        if ($LASTEXITCODE -ne 0 -or $state -in @("exited", "dead")) { return $false }
        $logs = (& $DockerPath logs $Name 2>&1) -join "`n"
        if ($logs -match "Startup complete|Now listening on|Application started") { return $true }
    } while ((Get-Date) -lt $deadline)
    return $false
}

Invoke-Docker @("version")
if (-not $SkipBuild) {
    $buildProfiles = @($Profile | ForEach-Object {
        $server = $profilesConfig.serverProfiles | Where-Object profile -eq $_ | Select-Object -First 1
        if (-not $server) { throw "Unknown Jellyfin server profile '$_'." }
        $server.buildProfile
    } | Select-Object -Unique)
    & (Join-Path $PSScriptRoot "build-plugin.ps1") -Profile $buildProfiles -Configuration $Configuration
}

$failures = [System.Collections.Generic.List[string]]::new()
foreach ($currentProfile in $Profile) {
    $serverProfile = $profilesConfig.serverProfiles | Where-Object profile -eq $currentProfile | Select-Object -First 1
    if (-not $serverProfile) { throw "Unknown Jellyfin server profile '$currentProfile'." }
    $buildProfile = [string]$serverProfile.buildProfile
    $artifactRoot = Join-Path $artifactsRoot $buildProfile
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $artifactRoot "build.yaml")
    $version = [regex]::Match($manifest, '(?m)^version:\s*"([^"]+)"').Groups[1].Value
    if (-not $version) { throw "Could not read plugin version from $artifactRoot/build.yaml." }
    $dll = Join-Path $artifactRoot "Jellyfin.Webhooks.dll"
    if (-not (Test-Path -LiteralPath $dll)) { throw "Missing plugin artifact '$dll'." }

    $container = "webhooks-smoke-$($currentProfile.Replace('.', '-'))"
    $profileSmokeRoot = Join-Path $smokeRoot $currentProfile
    $configRoot = Join-Path $profileSmokeRoot "config"
    $pluginRoot = Join-Path $configRoot "plugins/Webhooks_$version"
    Remove-SmokeContainer $container
    if (Test-Path -LiteralPath $profileSmokeRoot) { Remove-Item -LiteralPath $profileSmokeRoot -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $pluginRoot | Out-Null
    Copy-Item -LiteralPath $dll -Destination $pluginRoot

    $image = "jellyfin/jellyfin:$currentProfile"
    Write-Host "==> Smoke testing Webhooks on Jellyfin $currentProfile ($image)"
    Invoke-Docker @("pull", $image)
    Invoke-Docker @("create", "--name", $container, $image)
    Invoke-Docker @("cp", "$configRoot/.", "${container}:/config")
    Invoke-Docker @("start", $container)

    $started = Wait-ForStartup $container
    $logs = (& $DockerPath logs $container 2>&1) -join "`n"
    $loadFailure = $logs -match "PluginLoadException|MissingMethodException|MissingFieldException|TypeLoadException|FileLoadException|Could not load file or assembly|Error loading plugin|Failed.*Webhooks|Webhooks.*Failed"
    $loaded = $logs -match "Loaded plugin:\s*Webhooks"
    if (-not $started -or $loadFailure -or -not $loaded) {
        $failures.Add($currentProfile)
        Write-Host "---- relevant Jellyfin $currentProfile logs ----"
        $logs -split "`n" | Select-String -Pattern "Webhooks|PluginLoadException|MissingMethodException|TypeLoadException|FileLoadException|Error|Failed" | Select-Object -First 120 | ForEach-Object { Write-Host $_.Line }
    } else {
        Write-Host "OK Jellyfin $currentProfile loaded Webhooks $version"
    }
    if (-not $KeepContainers) { Remove-SmokeContainer $container }
}

if ($failures.Count -gt 0) { throw "Smoke test failed for profiles: $($failures -join ', ')" }
Write-Host "All Jellyfin Docker smoke tests passed: $($Profile -join ', ')"
