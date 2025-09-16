# Sync version from Directory.Build.props to server.json
param(
    [string]$BuildPropsPath = ".\Directory.Build.props",
    [string]$ServerJsonPath = ".\NugetMcpServer\.mcp\server.json"
)

function Get-VersionFromBuildProps {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        throw "Directory.Build.props file not found at: $Path"
    }
    
    Write-Host "Loading XML from: $Path" -ForegroundColor Gray
    [xml]$buildProps = Get-Content $Path
    
    Write-Host "XML loaded successfully" -ForegroundColor Gray
    
    # Try to find VersionPrefix in any PropertyGroup
    $versionPrefix = $null
    foreach ($propertyGroup in $buildProps.Project.PropertyGroup) {
        if ($propertyGroup.VersionPrefix) {
            $versionPrefix = $propertyGroup.VersionPrefix
            break
        }
    }
    
    if (-not $versionPrefix) {
        throw "VersionPrefix not found in Directory.Build.props"
    }
    
    Write-Host "Found VersionPrefix: '$versionPrefix'" -ForegroundColor Gray
    # Trim any whitespace from version
    return $versionPrefix.Trim()
}

function Update-ServerJsonVersion {
    param(
        [string]$ServerJsonPath,
        [string]$NewVersion
    )
    
    if (-not (Test-Path $ServerJsonPath)) {
        throw "server.json file not found at: $ServerJsonPath"
    }
    
    $serverJson = Get-Content $ServerJsonPath -Raw | ConvertFrom-Json
    $currentVersion = $serverJson.version_detail.version
    
    if ($currentVersion -eq $NewVersion) {
        Write-Host "Version is already up to date: $NewVersion" -ForegroundColor Green
        return $false
    }
    
    $serverJson.version_detail.version = $NewVersion
    
    # Save with clean formatting
    $cleanJson = $serverJson | ConvertTo-Json -Depth 10
    $cleanJson | Out-File -FilePath $ServerJsonPath -Encoding UTF8
    
    Write-Host "Updated server.json version from $currentVersion to $NewVersion" -ForegroundColor Yellow
    return $true
}

try {
    Write-Host "Reading version from: $BuildPropsPath" -ForegroundColor Cyan
    $version = Get-VersionFromBuildProps -Path $BuildPropsPath
    Write-Host "Found version in Directory.Build.props: '$version'" -ForegroundColor Cyan
    
    Write-Host "Updating server.json at: $ServerJsonPath" -ForegroundColor Cyan
    $updated = Update-ServerJsonVersion -ServerJsonPath $ServerJsonPath -NewVersion $version
    
    if ($updated) {
        Write-Host "Version synchronization completed successfully!" -ForegroundColor Green
    }
}
catch {
    Write-Host "Error details: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    Write-Error "Error during version synchronization: $_"
    exit 1
}