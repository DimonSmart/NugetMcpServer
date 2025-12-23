# NugetMcpServer

[![Install via Docker](https://img.shields.io/badge/Install%20via%20Docker-VS%20Code-blue?logo=docker&logoColor=white)](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22NugetMcpServer%20%28Docker%29%22%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fdimonsmart%2Fnugetmcpserver%3Alatest%22%5D%7D)
[![Install via .NET Tool](https://img.shields.io/badge/Install%20via%20.NET-VS%20Code-512BD4?logo=dotnet&logoColor=white)](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22NugetMcpServer%20%28Local%29%22%2C%22command%22%3A%22NugetMcpServer%22%2C%22args%22%3A%5B%5D%7D)

NugetMcpServer is an MCP server that helps you find and inspect NuGet packages. It gives you accurate information about interfaces, classes, enums, and other types directly from the packages. This helps AI assistants provide better code suggestions and avoid making up non-existent APIs.

Certified by [MCPHub](https://mcphub.com/mcp-servers/dimonsmart/nugetmcpserver).

## Overview

This server connects your AI assistant (like Claude or Copilot) to the real NuGet ecosystem. Instead of guessing, the AI can look up the exact methods and types available in a specific version of a package.

You can use it to:
*   Find the right package for your task.
*   See the exact interface definitions.
*   Check for breaking changes between versions.
*   Get correct code examples based on real metadata.

It works with any client that supports the Model Context Protocol (MCP), such as VS Code (with Copilot), Claude Desktop, or [OllamaChat](https://github.com/DimonSmart/OllamaChat).

## Supported Clients

- **VS Code**: Integrate through MCP server configuration
- **OllamaChat**: My experimental C# AI playground built on Semantic Kernel. It features RAG, image analysis support, multi-agent chat (e.g., philosophers debating), and MCP server support with automatic function selection via vector indexes. Check it out at [OllamaChat](https://github.com/DimonSmart/OllamaChat).
- **GitHub Copilot**: Use as an MCP server to get accurate package information
- **Other MCP Clients**: Any tool that supports the Model Context Protocol

## Quick Start

### Option 1: Docker (Recommended)
**Prerequisite**: [Docker](https://www.docker.com/) installed and running.

[**Click to Install in VS Code (Docker)**](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22NugetMcpServer%20%28Docker%29%22%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fdimonsmart%2Fnugetmcpserver%3Alatest%22%5D%7D)

### Option 2: .NET Tool (Native)
**Prerequisite**: .NET 9.0 SDK installed.

1. Install the tool globally:
   ```bash
   dotnet tool install -g DimonSmart.NugetMcpServer
   ```
2. [**Click to Install in VS Code (Local)**](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22NugetMcpServer%20%28Local%29%22%2C%22command%22%3A%22NugetMcpServer%22%2C%22args%22%3A%5B%5D%7D)

### Option 3: Claude Desktop

Run this command to install automatically via Smithery:

```bash
npx -y @smithery/cli install @dimonsmart/nugetmcpserver --client claude
```

### Option 4: Manual Configuration

If you prefer to configure manually:

#### Docker Configuration
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

#### .NET Tool Configuration
```json
{
  "mcpServers": {
    "nuget": {
      "command": "NugetMcpServer",
      "args": []
    }
  }
}
```

## Installation Options

### Option 1: Run with Docker (Recommended)

This works on Windows, macOS, and Linux. You do not need the .NET SDK.

```bash
docker run -i --rm ghcr.io/dimonsmart/nugetmcpserver:latest
```

### Option 2: Install via Smithery

To install for Claude Desktop automatically:

```bash
npx -y @smithery/cli install @dimonsmart/nugetmcpserver --client claude
```

### Option 3: Install via WinGet (Windows)

You can install using the Windows Package Manager:

```powershell
winget install DimonSmart.NugetMcpServer
```

### Option 4: Install as .NET Tool

If you have the .NET SDK installed:

```bash
dotnet tool install -g DimonSmart.NugetMcpServer
```

## Available Tools

### Package Search

*   `search_packages(query, maxResults?)`
    *   Searches for packages using keywords.
    *   Good for finding a specific package if you know the name or a keyword.
*   `search_packages_fuzzy(query, maxResults?)`
    *   Uses AI to guess package names based on your description.
    *   Good if you don't know the exact package name (e.g., "library to generate mazes").

### Package Information

*   `get_package_info(packageId, version?)`
    *   Gets details about a package, including its dependencies and whether it is a meta-package.
*   `compare_package_versions(packageId, fromVersion, toVersion, ...)`
    *   Compares two versions of a package.
    *   Shows breaking changes, new methods, and removed APIs.
    *   You can filter by type name or member name.

### Type Definitions

*   `get_interface_definition(packageId, interfaceName, version?)`
    *   Gets the C# code for an interface.
*   `get_class_or_record_or_struct_definition(packageId, typeName, version?)`
    *   Gets the C# code for a class, record, or struct.
*   `get_enum_definition(packageId, enumName, version?)`
    *   Gets the C# code for an enum.

### Listing Types

*   `list_interfaces(packageId, version?)`
    *   Lists all public interfaces in a package.
*   `list_classes_records_structs(packageId, version?)`
    *   Lists all public classes, records, and structs in a package.

### File Access

*   `list_package_files(packageId, version?)`
    *   Lists all files inside the package.
*   `get_package_file(packageId, filePath, ...)`
    *   Reads the content of a file from the package.

### Utilities

*   `get_current_time()`
    *   Returns the current server time.

## Examples

<details>
<summary>📄 See Example Responses</summary>

### Get Interface Definition

**Request:**
```json
{
  "name": "get_interface_definition",
  "parameters": {
    "packageId": "DimonSmart.MazeGenerator",
    "interfaceName": "IMazeGenerator"
  }
}
```

**Response:**
```json
{
  "result": "namespace DimonSmart.MazeGenerator\n{\n    public interface IMazeGenerator\n    {\n        bool[,] Generate(int width, int height);\n        string AlgorithmName { get; }\n    }\n}"
}
```

### Search Packages

**Request:**
```json
{
  "name": "search_packages",
  "parameters": {
    "query": "json"
  }
}
```

**Response:**
```text
## Newtonsoft.Json v13.0.3
**Downloads**: 4,500,000,000
**Description**: Json.NET is a popular high-performance JSON framework for .NET
```

</details>

## Project Structure

<details>
<summary>📁 View File Structure</summary>

*   `Program.cs`: Main entry point.
*   `Tools/`: Contains the logic for each MCP tool.
*   `Services/`: Handles NuGet downloads, formatting, and analysis.
*   `Common/`: Shared code and base classes.

</details>

## Version Information

Current version: **1.0.18**

Check your version:
```bash
NugetMcpServer --version
```

## Copyright

© 2025 DimonSmart

## License

Unlicense - This is free and unencumbered software released into the public domain.


