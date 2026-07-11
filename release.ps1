[CmdletBinding(SupportsShouldProcess)]
param([switch]$Push)

$ErrorActionPreference = "Stop"
$repoPath = $PSScriptRoot
$status = & git -C $repoPath status --porcelain
if ($LASTEXITCODE -ne 0) { throw "Unable to read git status." }
if ($status) { throw "Working tree is not clean." }

[xml]$project = Get-Content -LiteralPath (Join-Path $repoPath "Jellyfin.Webhooks/Jellyfin.Webhooks.csproj") -Raw
$version = @($project.Project.PropertyGroup.AssemblyVersion | Where-Object { $_ })[0]
if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Invalid AssemblyVersion: $version" }
$tag = "v$($version -replace '\.0$', '')"
$legacyTag = "v$version"

foreach ($existingTag in @($tag, $legacyTag) | Select-Object -Unique) {
    & git -C $repoPath rev-parse --verify --quiet "refs/tags/$existingTag" *> $null
    if ($LASTEXITCODE -eq 0) { throw "Version $version is already released as $existingTag." }
}
if (-not (& git -C $repoPath branch --show-current)) { throw "HEAD is detached." }

if ($PSCmdlet.ShouldProcess($repoPath, "Create annotated tag $tag")) {
    & git -C $repoPath tag -a $tag -m "Release $tag"
    if ($LASTEXITCODE -ne 0) { throw "Unable to create tag $tag." }
}
if ($Push -and $PSCmdlet.ShouldProcess("origin", "Push tag $tag")) {
    & git -C $repoPath push origin "refs/tags/$tag"
    if ($LASTEXITCODE -ne 0) { throw "Unable to push tag $tag." }
}
