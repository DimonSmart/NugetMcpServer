# NugetMcpServer

<!-- mcp-name: io.github.dimonsmart/NugetMcpServer -->

[![NuGet](https://img.shields.io/nuget/v/DimonSmart.NugetMcpServer?logo=nuget)](https://www.nuget.org/packages/DimonSmart.NugetMcpServer)
[![Install via Docker](https://img.shields.io/badge/Install%20via%20Docker-VS%20Code-blue?logo=docker&logoColor=white)](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22NugetMcpServer%20%28Docker%29%22%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fdimonsmart%2Fnugetmcpserver%3Alatest%22%5D%7D)

Bad NuGet API guesses are expensive.

NugetMcpServer gives your AI assistant access to real NuGet package metadata. Instead of guessing which classes, interfaces, methods, properties, or enum values exist, the assistant can inspect the actual package version.

Certified by [MCPHub](https://mcphub.com/mcp-servers/dimonsmart/nugetmcpserver).

## Why this exists

LLMs often know popular libraries, but they do not reliably know the exact API surface of every package version. Small mistakes show up as:

- a method name from another version;
- an overload that does not exist;
- a property with the wrong type;
- a class from a different package;
- a migration suggestion based on outdated API.

NugetMcpServer connects the assistant to NuGet packages directly, including private feeds and local package folders.

## What it gives to your assistant

- Package search by name or task.
- Exact public types from a package.
- Interface, class, struct, record, and enum definitions.
- API comparison between package versions.
- Access to files inside the package.
- Support for nuget.org, private feeds, and local package folders.

## Typical use cases

### Generate code against the real API

Ask your assistant to inspect the exact package version before writing code.

> Use `Dapper` and check the real public API before suggesting the implementation.

### Check package migrations

Compare two package versions before upgrading.

> Compare `Some.Package` 1.8.0 and 2.0.0 and show breaking API changes.

### Work with private packages

Point the server to your internal feed and let the assistant inspect packages that are not visible on the public internet.

## Quick start

### Recommended: VS Code via NuGet

Open the [NuGet package page](https://www.nuget.org/packages/DimonSmart.NugetMcpServer), select the MCP Server tab, and copy the generated VS Code configuration.

For manual VS Code configuration, add this to `mcp.json` and replace `<version>` with the package version you want to use:

```json
{
  "servers": {
    "nuget": {
      "type": "stdio",
      "command": "dnx",
      "args": ["DimonSmart.NugetMcpServer@<version>", "--yes"]
    }
  }
}
```

### Docker

Use Docker when the MCP client environment does not have the .NET SDK available.

```bash
docker run -i --rm ghcr.io/dimonsmart/nugetmcpserver:latest
```

VS Code `mcp.json`:

```json
{
  "servers": {
    "nuget": {
      "type": "stdio",
      "command": "docker",
      "args": ["run", "-i", "--rm", "ghcr.io/dimonsmart/nugetmcpserver:latest"]
    }
  }
}
```

[Install in VS Code with Docker](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22NugetMcpServer%20%28Docker%29%22%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fdimonsmart%2Fnugetmcpserver%3Alatest%22%5D%7D)

### Claude Desktop

Add the server to the Claude Desktop MCP configuration and replace `<version>` with the package version you want to use:

```json
{
  "mcpServers": {
    "nuget": {
      "command": "dnx",
      "args": ["DimonSmart.NugetMcpServer@<version>", "--yes"]
    }
  }
}
```

### Other MCP clients

Use the client-specific MCP configuration format. For clients that use the common `mcpServers` shape:

```json
{
  "mcpServers": {
    "nuget": {
      "command": "dnx",
      "args": ["DimonSmart.NugetMcpServer@<version>", "--yes"]
    }
  }
}
```

## Private and local NuGet sources

Use `NUGET_SOURCES` or `NUGET_CONFIG` to point the server at local package folders or private NuGet feeds.

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

More configuration options: [Technical details](docs/technical-details.md)

## Available tools

- `search_packages`: search packages by query and keywords.
- `search_packages_fuzzy`: use AI-assisted fuzzy package search.
- `get_package_info`: get package metadata, dependencies, and package shape.
- `list_interfaces`: list public interfaces.
- `list_classes_records_structs`: list public classes, records, and structs.
- `get_interface_definition`: return a C# interface definition.
- `get_class_or_record_or_struct_definition`: return a C# class, record, or struct definition.
- `get_enum_definition`: return a C# enum definition.
- `compare_package_versions`: compare public API changes between versions.
- `list_package_files`: list files inside a package.
- `get_package_file`: read a file from a package.
- `get_current_time`: return the current server time.

Detailed parameters and development notes: [Technical details](docs/technical-details.md)

## Version

The release version is assigned by the GitHub Actions pipeline from the Git tag.

Check the installed version:

```bash
NugetMcpServer --version
```

## Publishing

NuGet releases are published from version tags on `main`:

```powershell
.\publish-next-version.ps1
```

The script creates and pushes the next `vMAJOR.MINOR.PATCH` tag. GitHub Actions builds, tests, updates the MCP manifest version, packs the NuGet MCP server package, and publishes it to NuGet.

WinGet publishing is also tag-driven after the GitHub release exists:

```powershell
.\publish-winget-version.ps1
```

By default, the WinGet script uses the latest `vMAJOR.MINOR.PATCH` release tag. Pass `-Version 1.2.3` to publish a specific release.

## License

MIT
