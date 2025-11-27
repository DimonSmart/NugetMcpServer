# NugetMcpServer

[![Install in VS Code](https://img.shields.io/badge/Install%20in-VS%20Code-007ACC?logo=visualstudiocode&style=for-the-badge)](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22NugetMcpServer%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22DimonSmart.NugetMcpServer%22%5D%7D)
[![Install in VS Code Insiders](https://img.shields.io/badge/Install%20in-VS%20Code%20Insiders-24bfa5?logo=visualstudiocode&style=for-the-badge)](https://insiders.vscode.dev/redirect?url=vscode-insiders:mcp/install?%7B%22name%22%3A%22NugetMcpServer%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22DimonSmart.NugetMcpServer%22%5D%7D)

A powerful MCP server for getting accurate interface, class, enum, record, and struct definitions from NuGet packages. It helps reduce LLM hallucinations by giving precise information about real package APIs.
Certified by [MCPHub](https://mcphub.com/mcp-servers/dimonsmart/nugetmcpserver)

## Overview

This application is not just a demo. It is a practical MCP server that provides accurate, up-to-date information about methods and types in specific NuGet package versions. Instead of using old training data, language models can ask for real package metadata and type definitions directly from NuGet.

The server uses the Model Context Protocol (MCP). This makes it easy to connect with AI assistants and development tools. It gives real-time access to package information, helping developers and AI systems make better decisions about API usage.

You can use this server with [OllamaChat](https://github.com/DimonSmart/OllamaChat), VS Code with Copilot, and other MCP-compatible clients.

## Quick Start

1. **Click "Install in VS Code"** (button above) 
2. **In VS Code**: Open Command Palette → `MCP: List Servers` → **NugetMcpServer** → **Start**
3. **In Copilot Chat** (Agent Mode): "Use NugetMcpServer to get package info for `Newtonsoft.Json`"

### Option 1: Run with Docker (Recommended)

You can run the server using Docker. This method works on Windows, macOS, and Linux and doesn't require installing the .NET SDK.

#### Build locally
```bash
docker build -t nugetmcpserver .
docker run -i --rm nugetmcpserver
```

#### Configure in MCP Clients (e.g., Claude Desktop)
```json
{
  "mcpServers": {
    "nuget": {
      "command": "docker",
      "args": ["run", "-i", "--rm", "nugetmcpserver"]
    }
  }
}
```

### Option 2: Install via Smithery

To install NugetMcpServer for Claude Desktop automatically via [Smithery](https://smithery.ai/server/nugetmcpserver):

```bash
npx -y @smithery/cli install @dimonsmart/nugetmcpserver --client claude
```

Or directly from GitHub:

```bash
npx -y @smithery/cli install github.com/DimonSmart/NugetMcpServer --client claude
```

### Option 3: Install via WinGet (Windows)

## Manual Installation

### .NET Tool (Global)
```bash
dotnet tool install -g DimonSmart.NugetMcpServer
```
### Option 4: Install as .NET Tool
If you have the .NET SDK installed, you can install the server as a global tool:
```bash
dotnet tool install -g DimonSmart.NugetMcpServer
```
### Option 5: Build from Source

### VS Code Workspace Configuration
Create `.vscode/mcp.json` in your workspace:
```json
{
  "servers": {
    "NugetMcpServer": {
      "type": "stdio",
      "command": "dnx",
      "args": ["DimonSmart.NugetMcpServer"]
    }
  }
}
```

### WinGet Installation
```bash
winget install DimonSmart.NugetMcpServer
```

## Available Tools


### TimeTool

- `get_current_time()` - Returns the current server time in ISO 8601 format (YYYY-MM-DDThh:mm:ssZ)


### Interface Tools

- `get_interface_definition(packageId, interfaceName?, version?)` - Gets the C# interface definition from a NuGet package. Parameters: packageId (NuGet package ID), interfaceName (optional, short name without namespace), version (optional, defaults to latest)
- `list_interfaces(packageId, version?)` - Lists all public interfaces in a NuGet package. Returns package ID, version, and the list of interfaces


### Enum Tools

- `get_enum_definition(packageId, enumName, version?)` - Gets the C# enum definition from a NuGet package. Parameters: packageId (NuGet package ID), enumName (short name without namespace), version (optional, defaults to latest)

### Class Tools

- `get_class_or_record_or_struct_definition(packageId, typeName, version?)` - Gets the C# class, record or struct definition from a NuGet package. Parameters: packageId (NuGet package ID), typeName (short or full name), version (optional, defaults to latest)
- `list_classes_records_structs(packageId, version?)` - Lists all public classes, records and structs in a NuGet package. Returns package ID, version, and the list of types

### Package File Tools

- `list_package_files(packageId, version?)` - Lists all files in a NuGet package.
- `get_package_file(packageId, filePath, version?, offset?, bytes?)` - Reads a file from a NuGet package. Returns text or base64 for binary files. Maximum chunk size is 1 MB.

### Package Search Tools

- `search_packages(query, maxResults?, fuzzySearch?)` - Searches for NuGet packages by description or functionality.
  - **Standard search mode (fuzzySearch=false, default)**: Performs direct search for the full query and also searches each comma-separated keyword if provided
  - **Fuzzy search mode (fuzzySearch=true)**: Starts with the standard search and additionally tries each individual word and AI-generated package name alternatives
  - AI analyzes user's functional requirements and generates 3 most likely package names (e.g., "maze generation" → "MazeGenerator MazeBuilder MazeCreator")
  - Returns up to 50 most popular packages with details including download counts, descriptions, and project URLs
  - Results are sorted by popularity (download count) for better relevance

### Package Information Tools

- `get_package_info(packageId, version?)` - Gets comprehensive information about a NuGet package including metadata, dependencies, and meta-package status. Shows clear warnings for meta-packages and guidance on where to find actual implementations.

### Package Dependencies Tools

- **Note**: Package dependencies are now included in the `get_package_info` tool response, providing complete package information in a single call.

<details>
<summary>📄 MCP Server Response Examples</summary>

Here are examples of server responses from the MCP tools using DimonSmart.MazeGenerator as a sample package:

### GetInterfaceDefinition Example


#### User Query Example
A user might ask:

> "I want to generate mazes in my .NET application. Is there a package for that? Can you show me the interface for DimonSmart.MazeGenerator so I can see how to use it?"

The agent would use the MCP server to get the interface definition:

Request:
```json
{
  "name": "f1e_GetInterfaceDefinition",
  "parameters": {
    "packageId": "DimonSmart.MazeGenerator",
    "interfaceName": "IMazeGenerator",
    "version": ""
  }
}
```

Response (actual MCP server output):
```json
{
  "result": "namespace DimonSmart.MazeGenerator\n{\n    /// <summary>\n    /// Interface for maze generation algorithms\n    /// </summary>\n    public interface IMazeGenerator\n    {\n        /// <summary>\n        /// Creates a new maze with the specified dimensions\n        /// </summary>\n        /// <param name=\"width\">Width of the maze</param>\n        /// <param name=\"height\">Height of the maze</param>\n        /// <returns>A 2D array representing the maze</returns>\n        bool[,] Generate(int width, int height);\n        \n        /// <summary>\n        /// Gets the name of the algorithm used for maze generation\n        /// </summary>\n        string AlgorithmName { get; }\n    }\n}"
}
```


The result is the actual JSON response from the MCP server:
- The interface definition is a single string value
- All newlines are escaped as `\n`
- All double quotes are escaped as `\"`
- The content is inside the `result` property as a JSON string

This is how an agent receives the interface definition from the MCP server. The agent then parses and displays it in a readable format for the user.

### SearchPackages Example

#### User Query Example
A user might ask:

> "I need a library for working with JSON in my .NET application. Can you help me find the most popular packages for JSON handling?"

The agent would use the MCP server to search for packages:

Request:
```json
{
  "name": "SearchPackages",
  "parameters": {
    "query": "JSON serialization library",
    "maxResults": 5,
    "fuzzySearch": false
  }
}
```

#### Fuzzy Search Example

For better results with descriptive queries, enable fuzzy search:

Request:
```json
{
  "name": "SearchPackages",
  "parameters": {
    "query": "I need a library for generating mazes",
    "maxResults": 10,
    "fuzzySearch": true
  }
}
```

#### Russian Language Support Example

The AI can work with queries in Russian as well:

Request:
```json
{
  "name": "SearchPackages",
  "parameters": {
    "query": "User needs to generate a maze",
    "maxResults": 10,
    "fuzzySearch": true
  }
}
```

Response (formatted result shows combined results):
```
/* NUGET PACKAGE SEARCH RESULTS FOR: User needs to generate a maze */
/* AI-GENERATED PACKAGE NAMES: MazeGenerator MazeBuilder MazeCreator */
/* FOUND 8 PACKAGES (SHOWING TOP 8) */

## DimonSmart.MazeGenerator v1.0.0
**Downloads**: 12,543
**Description**: Advanced maze generation library with multiple algorithms
**Project URL**: https://github.com/DimonSmart/MazeGenerator

## MazeBuilder v2.1.0
**Downloads**: 8,234
**Description**: Simple maze construction toolkit
```

#### How Fuzzy Search Works

The search algorithm works as follows:
- **Standard search mode (fuzzySearch=false)**: Direct search for the whole query plus searches for comma-separated keywords
- **Fuzzy search mode (fuzzySearch=true)**:
  1. Runs the standard search
  2. Searches each individual word from the query
  3. Uses AI to suggest additional package names (e.g., "maze generation" → "MazeGenerator MazeBuilder MazeCreator")
  4. Searches for the AI-generated names
  5. Combines and deduplicates all results sorted by popularity

</details>

## Integration with Development Tools


You can use this MCP server with different development tools and AI assistants:

- **VS Code**: Integrate through MCP server configuration
- **OllamaChat**: Works with [OllamaChat](https://github.com/DimonSmart/OllamaChat) for local AI conversations
- **GitHub Copilot**: Use as an MCP server to get accurate package information
- **Other MCP Clients**: Any tool that supports the Model Context Protocol

By using this server, developers and AI systems get real-time, accurate information about NuGet package interfaces, enums, structs, and records. This reduces the chance of outdated or wrong API suggestions.

## Benefits for AI Development

### Reducing LLM Hallucinations


This MCP server helps solve a big problem in AI-assisted development: **LLM hallucinations about package APIs**. Common issues are:

- **Outdated Information**: LLMs trained on old data may suggest removed methods or interfaces
- **Non-existent APIs**: Models may generate method signatures that do not exist
- **Version Mismatches**: Suggestions may be for a different package version than the one used
- **Incomplete Information**: Missing important parameters, return types, or constraints


### How NugetMcpServer Helps

- **Real-time Data**: Gets current interface definitions directly from NuGet packages
- **Version-specific Information**: Lets you query specific package versions for accurate compatibility
- **Complete Interface Definitions**: Gives full method signatures, properties, and generic constraints
- **Accurate Type Information**: Ensures correct parameter types, return types, and namespace information


### Use Cases

- **Package Discovery**: Search for packages by functionality description and see popularity metrics
- **API Discovery**: See what interfaces are in a package before using it
- **Version Migration**: Compare interfaces between package versions when upgrading
- **Code Generation**: Generate accurate code from real package interfaces
- **Documentation**: Get up-to-date interface documentation for development
- **Technology Research**: Find the most popular packages for specific technologies or patterns

## About the MCP Protocol


Model Context Protocol (MCP) is a protocol that standardizes communication between language models and external tools. It lets models call functions, get information, and interact with external systems through one interface.

## License

Unlicense - This is free and unencumbered software released into the public domain.

## Copyright

© 2025 DimonSmart

### Meta-Package Support

All tools that analyze package content (class definitions, interface definitions, enum definitions, and listing tools) now automatically detect meta-packages and show clear warnings. Meta-packages are NuGet packages that group other packages together without containing actual implementation code. When a meta-package is detected, the tools will:

- Display a prominent warning that it's a meta-package
- List the dependencies that contain the actual implementations
- Provide guidance on which packages to analyze instead

The meta-package detection is powered by the `MetaPackageDetector` service which analyzes package structure and dependency patterns to identify when a package serves only as a grouping mechanism.

<details>
<summary>📁 Project Structure</summary>

- `Program.cs` - Main entry point that configures and runs the MCP server with version support
- `TimeTool.cs` - Utility tool for time-related functions (namespace `NugetMcpServer`)
- `Tools/GetInterfaceDefinitionTool.cs` - Extracts interface definitions from NuGet packages
- `Tools/GetClassDefinitionTool.cs` - Extracts class, record, and struct definitions from NuGet packages  
- `Tools/ListInterfacesTool.cs` - Lists all interfaces in NuGet packages
- `Tools/ListTypesTool.cs` - Lists all types (classes, records, structs) in NuGet packages
- `Tools/GetEnumDefinitionTool.cs` - Extracts enum definitions from NuGet packages
- `Tools/GetPackageInfoTool.cs` - Gets comprehensive package information and metadata
- `Tools/SearchPackagesTool.cs` - Standard package search functionality
- `Tools/SearchPackagesFuzzyTool.cs` - AI-enhanced fuzzy package search
- `Tools/PackageFileTool.cs` - Lists and reads files from NuGet packages
- `Services/NuGetPackageService.cs` - Core service for working with NuGet packages
- `Services/InterfaceFormattingService.cs` - Formats interface definitions
- `Services/ClassFormattingService.cs` - Formats class, record, and struct definitions
- `Services/EnumFormattingService.cs` - Formats enum definitions
- `Services/ArchiveProcessingService.cs` - Processes NuGet package archives
- `Services/MetaPackageDetector.cs` - Detects and handles meta-packages
- `Services/Formatters/` - Specialized result formatters for different output types
- `Common/McpToolBase.cs` - Base class for MCP tools with common functionality
- `Extensions/` - Extension methods for string formatting, exception handling, and progress notification

</details>

<details>
<summary>⚙️ Implementation Details</summary>

The server uses the .NET Generic Host and includes:

- Console logging set to trace level
- MCP server registered with STDIO transport
- Automatic discovery and registration of tools using reflection
- HttpClient service for downloading NuGet packages
- In-memory caching for improved performance
- NuGetPackageService for package operations
- Multiple formatting services for different C# constructs (interfaces, classes, enums)
- Archive processing service for efficient package analysis
- Meta-package detection and dependency resolution
- Progress notification system for long-running operations
- Version support with `--version` or `-v` command line arguments
- Comprehensive test suite covering all tools and services
- Windows-optimized single-file deployment with CET compatibility handling

Key dependencies:
- **ModelContextProtocol**: 0.3.0-preview.2 - MCP protocol implementation
- **Microsoft.Extensions.Hosting**: 9.0.7 - Application hosting framework
- **Microsoft.Extensions.Caching.Memory**: 9.0.7 - In-memory caching
- **NuGet.Packaging**: 6.14.0 - NuGet package processing
- **System.Reflection.MetadataLoadContext**: 10.0.0-preview.6 - Assembly metadata analysis
- **System.Text.Json**: 9.0.5 - JSON serialization

Project namespaces:
- `NugetMcpServer` - Main namespace (used in TimeTool.cs)
- `NuGetMcpServer.Tools` - Tool components (all analysis and search tools)
- `NuGetMcpServer.Services` - Service components (package processing, formatting, archive handling)
- `NuGetMcpServer.Common` - Shared components and base classes
- `NuGetMcpServer.Extensions` - Extension methods for strings, exceptions, and progress
- `NuGetMcpServer.Models` - Data models and result types

</details>

## Version Information

Current version: **1.0.11**

You can check the version of your installation using:
```
NugetMcpServer --version
# or
NugetMcpServer -v
```


