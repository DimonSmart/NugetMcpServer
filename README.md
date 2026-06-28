# NugetMcpServer

<!-- mcp-name: io.github.dimonsmart/NugetMcpServer -->

[![NuGet](https://img.shields.io/nuget/v/DimonSmart.NugetMcpServer?logo=nuget)](https://www.nuget.org/packages/DimonSmart.NugetMcpServer)
[![Install via Docker](https://img.shields.io/badge/Install%20via%20Docker-VS%20Code-blue?logo=docker&logoColor=white)](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22NugetMcpServer%20%28Docker%29%22%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fdimonsmart%2Fnugetmcpserver%3Alatest%22%5D%7D)

Bad NuGet API guesses are expensive.

NugetMcpServer lets Codex, Claude Code, VS Code, Claude Desktop, and other MCP clients inspect real NuGet package metadata and public APIs before generating code. It is most useful when package version details matter or when the package lives in a private feed.

Fastest path: install it with `codex mcp add` or `claude mcp add`, then ask your assistant to use the NuGet MCP server before writing package-dependent code.

## Why this exists

LLMs often know popular libraries, but they do not reliably know the exact API surface of every package version. NugetMcpServer gives the assistant a way to check the package first instead of filling gaps from memory.

NugetMcpServer connects the assistant to NuGet packages directly, including private feeds and local package folders.

## What it gives to your assistant

- Search NuGet packages and inspect package metadata.
- Read public API definitions for real package versions.
- Compare versions, inspect package files, and use nuget.org, private feeds, or local package folders.

## Quick start

These commands use the current published NuGet package version, `1.1.7`. `dnx` downloads and runs the .NET tool from NuGet, so you do not need a separate `dotnet tool install` step.

### 1. Install via Codex CLI

```bash
codex mcp add nuget -- dnx DimonSmart.NugetMcpServer@1.1.7 --yes
```

Check available MCP commands:

```bash
codex mcp --help
```

Inside the Codex TUI, run:

```text
/mcp
```

Codex CLI and the Codex IDE extension share MCP configuration, so configuring the server once should make it available in both clients.

### 2. Install via Claude Code CLI

```bash
claude mcp add --transport stdio nuget -- dnx DimonSmart.NugetMcpServer@1.1.7 --yes
```

Everything after `--` is the actual MCP server command and arguments.

Manage and verify the server:

```bash
claude mcp list
claude mcp get nuget
```

Inside Claude Code, run:

```text
/mcp
```

### 3. Install in VS Code

Open the [NuGet package page](https://www.nuget.org/packages/DimonSmart.NugetMcpServer), select the MCP Server tab, and copy the generated VS Code configuration.

For manual VS Code configuration, add this to `mcp.json`. Replace `<version>` with the package version you want to pin:

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

### 4. Install via Docker

Use Docker when the MCP client environment does not have the .NET SDK available.

Codex:

```bash
codex mcp add nuget-docker -- docker run -i --rm ghcr.io/dimonsmart/nugetmcpserver:latest
```

Claude Code:

```bash
claude mcp add --transport stdio nuget-docker -- docker run -i --rm ghcr.io/dimonsmart/nugetmcpserver:latest
```

Run the container directly:

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

### 5. Manual configuration

Codex `config.toml`:

```toml
[mcp_servers.nuget]
command = "dnx"
args = ["DimonSmart.NugetMcpServer@<version>", "--yes"]
```

Codex Docker variant:

```toml
[mcp_servers.nuget]
command = "docker"
args = ["run", "-i", "--rm", "ghcr.io/dimonsmart/nugetmcpserver:latest"]
```

Claude Desktop and generic MCP clients that use the `mcpServers` JSON shape:

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

### 6. Private/local NuGet sources

Use `NUGET_SOURCES` for one or more source URLs or local package folders. Separate sources with semicolons. Use `NUGET_CONFIG` when you want NuGet to read a specific `NuGet.Config`, for example when credentials or source names are already configured there.

Codex with `NUGET_SOURCES`:

```bash
codex mcp add nuget-private \
  --env NUGET_SOURCES="C:\NuGet\LocalFeed;https://pkgs.dev.azure.com/ORG/_packaging/Feed/nuget/v3/index.json" \
  -- dnx DimonSmart.NugetMcpServer@<version> --yes
```

Claude Code with `NUGET_SOURCES`:

```bash
claude mcp add --transport stdio \
  --env NUGET_SOURCES="C:\NuGet\LocalFeed;https://pkgs.dev.azure.com/ORG/_packaging/Feed/nuget/v3/index.json" \
  nuget-private \
  -- dnx DimonSmart.NugetMcpServer@<version> --yes
```

Codex with `NUGET_CONFIG`:

```bash
codex mcp add nuget-config \
  --env NUGET_CONFIG="/path/to/NuGet.Config" \
  -- dnx DimonSmart.NugetMcpServer@<version> --yes
```

Claude Code with `NUGET_CONFIG`:

```bash
claude mcp add --transport stdio \
  --env NUGET_CONFIG="/path/to/NuGet.Config" \
  nuget-config \
  -- dnx DimonSmart.NugetMcpServer@<version> --yes
```

Use Windows paths such as `C:\path\to\NuGet.Config` on Windows. Use Unix-style paths such as `/path/to/NuGet.Config` on Linux and macOS. Keep paths quoted when they contain spaces.

VS Code `mcp.json` with private sources:

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

Codex `config.toml` with private sources:

```toml
[mcp_servers.nuget]
command = "dnx"
args = ["DimonSmart.NugetMcpServer@<version>", "--yes"]

[mcp_servers.nuget.env]
NUGET_SOURCES = "C:\\NuGet\\LocalFeed;https://pkgs.dev.azure.com/ORG/_packaging/Feed/nuget/v3/index.json"
```

More configuration options: [Technical details](docs/technical-details.md)

## Verify it works

After the MCP client shows the server as connected, ask your assistant to use it explicitly:

```text
Use the NuGet MCP server to inspect Dapper 2.1.66 and show the public extension methods for IDbConnection.
```

```text
Before writing code, use the NuGet MCP server to inspect the real API of Microsoft.Extensions.DependencyInjection.Abstractions and use only existing public types and methods.
```

```text
Use the NuGet MCP server to compare public APIs of Some.Package 1.8.0 and 2.0.0 and summarize breaking changes.
```

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

## Troubleshooting

- `dnx` is not found: install a current .NET SDK and make sure the .NET tools path is available in your shell, or use the Docker install path.
- Docker is not running: start Docker Desktop or your Docker daemon, then retry the MCP client command.
- The MCP client shows the server but no tools: restart the client, check the client logs, and verify the server command starts successfully outside the client.
- Private feed authentication fails: check the active `NuGet.Config`, credential provider setup, feed URL, and whether the same user account can restore from that feed with normal NuGet tooling.
- Package restore or search is slow on first run: the server and NuGet client may need to download package metadata and package files before later cached reads are faster.
- Windows path quoting fails: keep paths in quotes, escape backslashes in JSON/TOML strings, and prefer `NUGET_CONFIG` when complex source paths or credentials are involved.
- Claude Code or Codex CLI command fails unexpectedly: make sure the command includes the `--` separator before `dnx` or `docker`; the MCP client options must appear before that separator.

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
