# NugetMcpServer technical details

This document covers configuration, tool parameters, development checks, and packaging notes.

## Source resolution

NuGet sources are resolved in this order:

1. `--source` / `--sources` command-line arguments.
2. `NUGET_SOURCES` / `NUGET_CONFIG` environment variables.
3. `NuGet:Sources` / `NuGet:ConfigPath` configuration values.
4. Default NuGet config discovery from machine, user, and solution config files.
5. Fallback to nuget.org.

Package-related tools also accept an optional `source` parameter. It can be a source name from `nuget.config`, a feed URL, or a local package folder.

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

MCP client configuration:

```json
{
  "mcpServers": {
    "nuget": {
      "command": "NugetMcpServer",
      "args": [
        "--source", "C:\\NuGet\\LocalFeed",
        "--source", "https://pkgs.dev.azure.com/ORG/_packaging/Feed/nuget/v3/index.json"
      ],
      "env": {
        "NUGET_CONFIG": "C:\\path\\to\\nuget.config"
      }
    }
  }
}
```

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

The project is packaged as a .NET tool with command name `NugetMcpServer`.

The NuGet package is marked as an MCP server package. The `.mcp/server.json` file is included under `.mcp/`, and the root `README.md` is included in the package.

Docker packaging is provided by the repository Dockerfile and published image:

```bash
docker run -i --rm ghcr.io/dimonsmart/nugetmcpserver:latest
```

## Troubleshooting

- Private feed authentication depends on the active NuGet configuration and available credential providers.
- Local folders must contain valid `.nupkg` packages.
- Package analysis reads managed assemblies only.
- Native DLLs are skipped.
