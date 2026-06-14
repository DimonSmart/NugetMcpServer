# NugetMcpServer

[![Install via Docker](https://img.shields.io/badge/Install%20via%20Docker-VS%20Code-blue?logo=docker&logoColor=white)](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22NugetMcpServer%20%28Docker%29%22%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fdimonsmart%2Fnugetmcpserver%3Alatest%22%5D%7D)
[![Install via .NET Tool](https://img.shields.io/badge/Install%20via%20.NET-VS%20Code-512BD4?logo=dotnet&logoColor=white)](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22NugetMcpServer%20%28Local%29%22%2C%22command%22%3A%22NugetMcpServer%22%2C%22args%22%3A%5B%5D%7D)

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

### Docker

```bash
docker run -i --rm ghcr.io/dimonsmart/nugetmcpserver:latest
```

### .NET tool

```bash
dotnet tool install -g DimonSmart.NugetMcpServer
```

### VS Code

[Install in VS Code with Docker](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22NugetMcpServer%20%28Docker%29%22%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fdimonsmart%2Fnugetmcpserver%3Alatest%22%5D%7D)

[Install in VS Code with the .NET tool](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22NugetMcpServer%20%28Local%29%22%2C%22command%22%3A%22NugetMcpServer%22%2C%22args%22%3A%5B%5D%7D)

### Claude Desktop

```bash
npx -y @smithery/cli install @dimonsmart/nugetmcpserver --client claude
```

### Manual MCP configuration

```json
{
  "mcpServers": {
    "nuget": {
      "command": "docker",
      "args": ["run", "-i", "--rm", "ghcr.io/dimonsmart/nugetmcpserver:latest"]
    }
  }
}
```

## Private and local NuGet sources

Use `--source` to point the server at a local package folder or a private NuGet feed.

```bash
NugetMcpServer --source "C:\NuGet\LocalFeed" --source "https://pkgs.dev.azure.com/ORG/_packaging/Feed/nuget/v3/index.json"
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

## License

MIT
