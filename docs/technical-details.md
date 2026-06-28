# NugetMcpServer technical details

This document covers source resolution, tool parameters, implementation constraints, development checks, and packaging notes.

## Source resolution

NuGet sources are resolved in this order:

1. `--source` / `--sources` command-line arguments.
2. `NUGET_SOURCES` / `NUGET_CONFIG` environment variables.
3. `NuGet:Sources` / `NuGet:ConfigPath` configuration values.
4. Default NuGet config discovery from machine, user, and solution config files.
5. Fallback to nuget.org.

Package-related tools also accept an optional `source` parameter. It can be a source name from `nuget.config`, a feed URL, or a local package folder.

Supported environment variables:

- `NUGET_SOURCES`: semicolon-, comma-, or newline-separated source URLs and local package folders.
- `NUGET_CONFIG`: path to a specific `NuGet.Config` file.

Supported command-line source options:

- `--source <value>` or `-s <value>`: add one source. Can be repeated.
- `--sources <value>`: add multiple semicolon-, comma-, or newline-separated sources.
- `--nuget-config <path>` or `--nugetconfig <path>`: use a specific NuGet configuration file.

## Private feeds and local folders

CLI examples:

```bash
NugetMcpServer --source "C:\NuGet\LocalFeed"
NugetMcpServer --source "C:\NuGet\LocalFeed" --source "https://pkgs.dev.azure.com/ORG/_packaging/Feed/nuget/v3/index.json"
NugetMcpServer --nuget-config "C:\path\to\nuget.config"
```

Environment variables:

```bash
set NUGET_SOURCES=C:\NuGet\LocalFeed;https://pkgs.dev.azure.com/ORG/_packaging/Feed/nuget/v3/index.json
set NUGET_CONFIG=C:\path\to\nuget.config
```

VS Code MCP configuration with `dnx`:

```json
{
  "servers": {
    "nuget": {
      "type": "stdio",
      "command": "dnx",
      "args": ["DimonSmart.NugetMcpServer@<version>", "--yes"],
      "env": {
        "NUGET_SOURCES": "C:\\NuGet\\LocalFeed;https://pkgs.dev.azure.com/ORG/_packaging/Feed/nuget/v3/index.json"
      }
    }
  }
}
```

Use either `NUGET_SOURCES` or `NUGET_CONFIG` for most setups. If both are set, explicit sources replace the source list read from configuration, while credentials and other NuGet settings still depend on the active NuGet configuration and credential providers.

For clients that use the `mcpServers` shape, keep the same `command`, `args`, and `env` values under that client-specific key.

## MCP tools

All package-related tools accept an optional `source` parameter unless noted.

### Package search

- `search_packages(query, maxResults?, source?)`: searches NuGet packages by query and comma-separated keywords.
- `search_packages_fuzzy(query, maxResults?, source?)`: uses AI-generated alternatives and word matching when direct search is not enough.

### Package information

- `get_package_info(packageId, version?, source?)`: returns package metadata, dependencies, recent versions, lib files, and meta-package information.
- `compare_package_versions(packageId, fromVersion, toVersion, typeNameFilter?, memberNameFilter?, breakingChangesOnly?, maxChangesPerCategory?, source?)`: compares public API changes between two versions.

### Type listing

- `list_interfaces(packageId, version?, source?)`: lists public interfaces.
- `list_classes_records_structs(packageId, version?, filter?, maxResults?, skip?, source?)`: lists public classes, records, and structs with optional wildcard filtering and pagination.

### Type definitions

- `get_interface_definition(packageId, interfaceName, version?, source?)`: returns the C# definition for an interface.
- `get_class_or_record_or_struct_definition(packageId, typeName, version?, source?)`: returns the C# definition for a class, record, or struct.
- `get_enum_definition(packageId, enumName, version?, source?)`: returns the C# definition for an enum.

### Package files

- `list_package_files(packageId, version?, source?)`: lists files inside a package.
- `get_package_file(packageId, filePath, version?, offset?, bytes?, source?)`: reads a package file as text or base64 for binary content. The maximum chunk size is 1 MB.

### Utilities

- `get_current_time()`: returns the current server time in ISO 8601 format.

## Metadata-only package analysis

Package DLLs are read as metadata. They are not loaded into the server runtime.

Target frameworks are selected through NuGet framework compatibility logic. The server reads public types, methods, properties, fields, events, and enum values from managed assemblies. Native DLLs are skipped.

## Caching behavior

Downloaded package bytes are cached in memory for five minutes per package ID, version, and source. NuGet's own HTTP and global package caches may also affect subsequent runs. The server does not persist its in-memory package cache after the process exits.

## Known limitations

- Package analysis reads managed assemblies only; native DLLs are skipped.
- Local folder sources must contain valid `.nupkg` packages.
- Private feed authentication is delegated to NuGet configuration and installed credential providers.
- The `source` parameter accepts one source name, feed URL, or local package folder for a tool call. Configure multiple fallback sources through server configuration.
- `get_package_file` reads a maximum chunk size of 1 MB per call.

## Developer verification

Run the regular verification suite:

```bash
dotnet restore
dotnet build
dotnet test
```

The exploratory smoke test is explicit, so it is skipped by the regular test run. The filtered command is still useful when you want category filtering:

```bash
dotnet test --filter "Category!=Exploratory&Category!=Manual"
```

## Manual smoke tests

The long NuGet package smoke test is marked as an xUnit v3 explicit test. It is skipped by the regular test run.

Run regular tests:

```bash
dotnet test
```

Run explicit smoke tests:

```bash
dotnet test --filter "Category=Exploratory" -- xUnit.Explicit=only
```

This command uses the xUnit v3 Visual Studio runner through `dotnet test`.

## Versioning

The repository default version may be `0.0.0`. This is a placeholder for local development and package metadata.

The release version is assigned by the GitHub Actions pipeline from the Git tag. Do not manually update placeholder versions during normal development.

Check the installed version:

```bash
NugetMcpServer --version
```

## Packaging

The project is packaged as a .NET tool with command name `NugetMcpServer`. The recommended distribution path for MCP clients is the NuGet MCP package launched by `dnx`:

```bash
dnx DimonSmart.NugetMcpServer@<version> --yes
```

The NuGet package is marked as an MCP server package. The `.mcp/server.json` file is included under `.mcp/`, and the root `README.md` is included in the package.

Docker packaging is provided as a fallback by the repository Dockerfile and published image:

```bash
docker run -i --rm ghcr.io/dimonsmart/nugetmcpserver:latest
```

## Release publishing

The canonical release path is a `vMAJOR.MINOR.PATCH` tag on `main`.

```powershell
.\publish-next-version.ps1
```

The release workflow validates the tag format, verifies that the tag commit is reachable from `origin/main`, restores, builds, tests, updates `.mcp/server.json`, creates the NuGet package, verifies that the package contains `.mcp/server.json` and `README.md`, and publishes the package to NuGet.

WinGet publishing is a separate tag-driven step that uses the zip asset from the GitHub release:

```powershell
.\publish-winget-version.ps1
```

By default, the WinGet script uses the latest `vMAJOR.MINOR.PATCH` release tag. Pass `-Version 1.2.3` to publish a specific release.

This creates and pushes a `wgMAJOR.MINOR.PATCH` tag at the matching `vMAJOR.MINOR.PATCH` release commit. The WinGet workflow publishes the release zip through `winget-releaser`.

## Troubleshooting

- Private feed authentication depends on the active NuGet configuration and available credential providers.
- Local folders must contain valid `.nupkg` packages.
- Package analysis reads managed assemblies only.
- Native DLLs are skipped.
