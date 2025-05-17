$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo(".\publish\NugetMcpServer.exe")
$version = "$($versionInfo.FileMajorPart).$($versionInfo.FileMinorPart).$($versionInfo.FileBuildPart)"

wingetcreate update DimonSmart.NugetMcpServer --urls "https://github.com/DimonSmart/NugetMcpServer/releases/download/v$version/nuget-mcp-server-win-x64-$version.zip" -v $version -s 
