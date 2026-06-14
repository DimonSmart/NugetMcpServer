param(
    [string] $Remote = "origin",
    [string] $Branch = "main",
    [string] $TagPrefix = "v",
    [string] $FirstVersion = "1.0.0"
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

function Format-Version {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Version
    )

    "$($Version.Major).$($Version.Minor).$($Version.Patch)"
}

$firstVersionMatch = [regex]::Match($FirstVersion, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$')
if (-not $firstVersionMatch.Success) {
    throw "First version '$FirstVersion' must use MAJOR.MINOR.PATCH format."
}

$repoRoot = (Invoke-Git -Arguments @("rev-parse", "--show-toplevel")).Trim()
Set-Location $repoRoot

$status = Invoke-Git -Arguments @("status", "--porcelain")
if ($status) {
    throw "Working tree is not clean. Commit or stash changes before publishing a version tag."
}

Write-Host "Fetching '$Remote'..."
Invoke-Git -Arguments @("fetch", "--prune", "--tags", $Remote) | Out-Null

Write-Host "Switching to '$Branch'..."
Invoke-Git -Arguments @("switch", $Branch) | Out-Null

Write-Host "Updating '$Branch' from '$Remote/$Branch'..."
Invoke-Git -Arguments @("pull", "--ff-only", $Remote, $Branch) | Out-Null

$escapedPrefix = [regex]::Escape($TagPrefix)
$tagPattern = "^$escapedPrefix(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$"

$latest = Invoke-Git -Arguments @("tag", "--list", "$TagPrefix*") |
    ForEach-Object {
        $tag = $_.Trim()
        if ($tag -match $tagPattern) {
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

if ($latest) {
    $next = [pscustomobject]@{
        Major = $latest.Major
        Minor = $latest.Minor
        Patch = $latest.Patch + 1
    }

    $previousTag = $latest.Tag
}
else {
    $next = [pscustomobject]@{
        Major = [int] $firstVersionMatch.Groups["major"].Value
        Minor = [int] $firstVersionMatch.Groups["minor"].Value
        Patch = [int] $firstVersionMatch.Groups["patch"].Value
    }

    $previousTag = "<none>"
}

$nextVersion = Format-Version $next
$nextTag = "$TagPrefix$nextVersion"

$existingTag = Invoke-Git -Arguments @("tag", "--list", $nextTag)
if ($existingTag) {
    throw "Tag '$nextTag' already exists."
}

Write-Host "Previous version tag: $previousTag"
Write-Host "Next version tag:     $nextTag"

Invoke-Git -Arguments @("tag", "-a", $nextTag, "-m", "Release $nextTag") | Out-Null
Invoke-Git -Arguments @("push", $Remote, $Branch, $nextTag) | Out-Null

Write-Host "Published '$nextTag' from '$Branch' and pushed it to '$Remote'."
