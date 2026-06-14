param(
    [string] $Version,
    [string] $Remote = "origin"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    $output = & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }

    return $output
}

$repoRoot = (Invoke-Git -Arguments @("rev-parse", "--show-toplevel")).Trim()
Set-Location $repoRoot

$status = Invoke-Git -Arguments @("status", "--porcelain")
if ($status) {
    throw "Working tree is not clean. Commit or stash changes before publishing a WinGet tag."
}

Write-Host "Fetching '$Remote'..."
Invoke-Git -Arguments @("fetch", "--prune", "--tags", $Remote) | Out-Null

if ([string]::IsNullOrWhiteSpace($Version)) {
    $latest = Invoke-Git -Arguments @("tag", "--list", "v*.*.*") |
        ForEach-Object {
            $tag = $_.Trim()
            if ($tag -match '^v(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$') {
                [pscustomobject]@{
                    Tag = $tag
                    Major = [int] $Matches["major"]
                    Minor = [int] $Matches["minor"]
                    Patch = [int] $Matches["patch"]
                }
            }
        } |
        Sort-Object Major, Minor, Patch |
        Select-Object -Last 1

    if (-not $latest) {
        throw "No vMAJOR.MINOR.PATCH release tags found."
    }

    $Version = "$($latest.Major).$($latest.Minor).$($latest.Patch)"
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version '$Version' must use MAJOR.MINOR.PATCH format."
}

$releaseTag = "v$Version"
$wingetTag = "wg$Version"

$releaseRef = Invoke-Git -Arguments @("rev-parse", "--verify", "$releaseTag^{commit}")
if (-not $releaseRef) {
    throw "Release tag '$releaseTag' does not exist."
}

$existingTag = Invoke-Git -Arguments @("tag", "--list", $wingetTag)
if ($existingTag) {
    throw "Tag '$wingetTag' already exists."
}

Write-Host "Release tag: $releaseTag"
Write-Host "WinGet tag:  $wingetTag"

Invoke-Git -Arguments @("tag", "-a", $wingetTag, $releaseTag, "-m", "Publish WinGet $wingetTag") | Out-Null
Invoke-Git -Arguments @("push", $Remote, $wingetTag) | Out-Null

Write-Host "Published '$wingetTag' from '$releaseTag' and pushed it to '$Remote'."
