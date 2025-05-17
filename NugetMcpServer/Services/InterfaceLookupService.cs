using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using NuGetMcpServer.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Services;

[McpServerToolType]
public class InterfaceLookupService(ILogger<InterfaceLookupService> logger, HttpClient httpClient)
{
    // [McpServerTool, Description("Get the latest version of a NuGet package")]
    public async Task<string> GetLatestVersion(string packageId)
    {
        var indexUrl = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLower()}/index.json";
        logger.LogInformation("Fetching latest version for package {PackageId} from {Url}", packageId, indexUrl);

        var json = await httpClient.GetStringAsync(indexUrl);
        using var doc = JsonDocument.Parse(json);
        var versions = doc.RootElement
            .GetProperty("versions")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return versions.Last();
    }


    /// <summary>
    /// Helper method to download a NuGet package and return it as a memory stream
    /// </summary>
    /// <param name="packageId">NuGet package ID</param>
    /// <param name="version">Package version</param>
    /// <returns>MemoryStream containing the package data</returns>
    private async Task<MemoryStream> DownloadPackageAsync(string packageId, string version)
    {
        // Download .nupkg
        var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLower()}/{version}/{packageId.ToLower()}.{version}.nupkg";
        logger.LogInformation("Downloading package from {Url}", url);

        var response = await httpClient.GetByteArrayAsync(url);
        return new MemoryStream(response);
    }

    /// <summary>
    /// Helper method to load and scan assemblies for interfaces directly from memory
    /// </summary>
    /// <param name="assemblyData">Assembly bytes</param>
    /// <returns>Assembly if successfully loaded, null otherwise</returns>
    private Assembly? LoadAssemblyFromMemory(byte[] assemblyData)
    {
        try
        {
            return Assembly.Load(assemblyData);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to load assembly from memory");
            return null;
        }
    }
    /// <summary>
    /// Formats interface definition as a string
    /// </summary>
    /// <param name="interfaceType">Interface type</param>
    /// <param name="assemblyName">Assembly name</param>
    /// <returns>Formatted interface definition</returns>
    private string FormatInterfaceDefinition(Type interfaceType, string assemblyName)
    {
        var sb = new StringBuilder()
            .AppendLine($"/* C# INTERFACE FROM {assemblyName} */");

        // Format the interface declaration with generics
        sb.Append($"public interface {FormatTypeName(interfaceType)}");

        // Add generic constraints if any
        if (interfaceType.IsGenericType)
        {
            var constraints = GetGenericConstraints(interfaceType);
            if (!string.IsNullOrEmpty(constraints))
                sb.Append(constraints);
        }

        sb.AppendLine().AppendLine("{");

        // Track processed property names to avoid duplicates when looking at get/set methods
        var processedProperties = new HashSet<string>();
        var properties = GetInterfaceProperties(interfaceType);

        // Add properties
        foreach (var prop in properties)
        {
            processedProperties.Add(prop.Name);

            sb.Append($"    {FormatTypeName(prop.PropertyType)} {prop.Name} {{ ");

            if (prop.GetGetMethod() != null)
                sb.Append("get; ");

            if (prop.GetSetMethod() != null)
                sb.Append("set; ");

            sb.AppendLine("}");
        }

        // Add indexers (special properties)
        var indexers = GetInterfaceIndexers(interfaceType);
        foreach (var indexer in indexers)
        {
            processedProperties.Add(indexer.Name);

            var parameters = indexer.GetIndexParameters();
            var paramList = string.Join(", ", parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));

            sb.Append($"    {FormatTypeName(indexer.PropertyType)} this[{paramList}] {{ ");

            if (indexer.GetGetMethod() != null)
                sb.Append("get; ");

            if (indexer.GetSetMethod() != null)
                sb.Append("set; ");

            sb.AppendLine("}");
        }

        // Add methods (excluding property accessors)
        foreach (var method in interfaceType.GetMethods())
        {
            // Skip property accessor methods that we've already processed
            if (IsPropertyAccessor(method, processedProperties))
                continue;

            var parameters = string.Join(", ",
                method.GetParameters().Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));

            sb.AppendLine($"    {FormatTypeName(method.ReturnType)} {method.Name}({parameters});");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Check if a method is a property accessor (get/set) for a processed property
    /// </summary>
    private bool IsPropertyAccessor(MethodInfo method, HashSet<string> processedProperties)
    {
        // Property accessor methods start with get_ or set_ followed by property name
        if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_"))
        {
            var propertyName = method.Name.Substring(4); // Skip get_ or set_
            return processedProperties.Contains(propertyName);
        }
        return false;
    }

    /// <summary>
    /// Format a type name to be more C#-like 
    /// </summary>
    private string FormatTypeName(Type type) => type.FormatCSharpTypeName();

    /// <summary>
    /// Get all generic constraints for a generic interface
    /// </summary>
    private string GetGenericConstraints(Type interfaceType)
    {
        if (!interfaceType.IsGenericType)
            return string.Empty;

        var constraints = new StringBuilder();
        var genericArgs = interfaceType.GetGenericArguments();

        foreach (var arg in genericArgs)
        {
            var argConstraints = new List<string>();

            // Reference type constraint
            if (arg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
                argConstraints.Add("class");

            // Value type constraint
            if (arg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                argConstraints.Add("struct");

            // Constructor constraint
            if (arg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) &&
                !arg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                argConstraints.Add("new()");

            // Interface constraints
            foreach (var constraint in arg.GetGenericParameterConstraints())
            {
                if (constraint != typeof(ValueType)) // Skip ValueType for struct constraint
                    argConstraints.Add(FormatTypeName(constraint));
            }

            if (argConstraints.Count > 0)
                constraints.AppendLine($" where {arg.Name} : {string.Join(", ", argConstraints)}");
        }

        return constraints.ToString();
    }

    /// <summary>
    /// Get all properties of an interface including those from base interfaces
    /// </summary>
    private IEnumerable<PropertyInfo> GetInterfaceProperties(Type interfaceType)
    {
        var properties = interfaceType.GetProperties();

        // Skip indexers (they'll be handled separately)
        return properties.Where(p => p.GetIndexParameters().Length == 0);
    }

    /// <summary>
    /// Get all indexers of an interface including those from base interfaces
    /// </summary>
    private IEnumerable<PropertyInfo> GetInterfaceIndexers(Type interfaceType)
    {
        var properties = interfaceType.GetProperties();

        // Only return indexers (properties with parameters)
        return properties.Where(p => p.GetIndexParameters().Length > 0);
    }
    /// <summary>
    /// Lists all public interfaces from a specified NuGet package
    /// </summary>
    /// <param name="packageId">NuGet package ID</param>
    /// <param name="version">Optional package version (defaults to latest)</param>
    /// <returns>Object containing package information and list of interfaces</returns>
    [McpServerTool,
     Description(
       "Lists all public interfaces available in a specified NuGet package. " +
       "Parameters: " +
       "packageId — NuGet package ID; " +
       "version (optional) — package version (defaults to latest). " +
       "Returns package ID, version and list of interfaces."
     )]
    public Task<InterfaceListResult> ListInterfaces(
        string packageId,
        string? version = null)
    {
        return ExecuteWithLoggingAsync(
            () => ListInterfacesCore(packageId, version),
            logger,
            "Error listing interfaces");
    }

    /// <summary>
    /// Core logic for listing interfaces without try/catch
    /// </summary>
    private async Task<InterfaceListResult> ListInterfacesCore(string packageId, string? version)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await GetLatestVersion(packageId);
        }

        // Ensure we have non-null values for packageId and version
        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        logger.LogInformation("Listing interfaces from package {PackageId} version {Version}",
            packageId, version);

        var result = new InterfaceListResult
        {
            PackageId = packageId,
            Version = version,
            Interfaces = new List<InterfaceInfo>()
        };

        using var packageStream = await DownloadPackageAsync(packageId, version);
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);

        // Scan each DLL in the package
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                continue;

            ProcessArchiveEntry(entry, result);
        }

        return result;
    }    /// <summary>
         /// Process a single archive entry to extract interfaces
         /// </summary>
    private void ProcessArchiveEntry(ZipArchiveEntry entry, InterfaceListResult result)
    {
        try
        {
            // Read the DLL into memory
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);

            var assemblyData = ms.ToArray();
            var assembly = LoadAssemblyFromMemory(assemblyData);

            if (assembly == null) return;

            var assemblyName = Path.GetFileName(entry.FullName);
            var interfaces = assembly.GetTypes()
                .Where(t => t.IsInterface && t.IsPublic)
                .ToList();

            foreach (var iface in interfaces)
            {
                result.Interfaces.Add(new InterfaceInfo
                {
                    Name = iface.Name,
                    FullName = iface.FullName ?? string.Empty,
                    AssemblyName = assemblyName
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error processing archive entry {EntryName}", entry.FullName);
        }
    }

    /// <summary>
    /// Extracts and returns the C# interface definition from a specified NuGet package.
    /// </summary>
    /// <param name="packageId">
    ///   The NuGet package ID (exactly as on nuget.org).
    /// </param>
    /// <param name="interfaceName">
    ///   Interface name without namespace.
    ///   If not specified, will search for all interfaces in the assembly.
    /// </param>
    /// <param name="version">
    ///   (Optional) Version of the package. If not specified, the latest version will be used.
    /// </param>
    [McpServerTool,
     Description(
       "Extracts and returns the C# interface definition from a specified NuGet package. " +
       "Parameters: " +
       "packageId — NuGet package ID; " +
       "version (optional) — package version (defaults to latest); " +
       "interfaceName (optional) — short interface name without namespace."
     )]
    public Task<string> GetInterfaceDefinition(
        string packageId,
        string interfaceName,
        string? version = null)
    {
        return ExecuteWithLoggingAsync(
            () => GetInterfaceDefinitionCore(packageId, interfaceName, version),
            logger,
            "Error fetching interface definition");
    }

    /// <summary>
    /// Core logic for getting interface definition without try/catch
    /// </summary>
    private async Task<string> GetInterfaceDefinitionCore(
        string packageId,
        string interfaceName,
        string? version)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentNullException(nameof(interfaceName));

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await GetLatestVersion(packageId);
        }

        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        logger.LogInformation("Fetching interface {InterfaceName} from package {PackageId} version {Version}",
            interfaceName, packageId, version);

        using var packageStream = await DownloadPackageAsync(packageId, version);
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);

        // Search in each DLL in the archive
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                continue;

            var definition = await TryGetInterfaceFromEntry(entry, interfaceName);
            if (definition != null)
                return definition;
        }

        return $"Interface '{interfaceName}' not found in package {packageId}.";
    }

    /// <summary>
    /// Try to get interface definition from a specific archive entry
    /// </summary>
    private async Task<string?> TryGetInterfaceFromEntry(ZipArchiveEntry entry, string interfaceName)
    {
        try
        {
            // Read the DLL into memory
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms);

            var assemblyData = ms.ToArray();
            var assembly = LoadAssemblyFromMemory(assemblyData);
            if (assembly == null) return null;
            var iface = assembly.GetTypes()
                .FirstOrDefault(t =>
                {
                    if (!t.IsInterface) return false;

                    // Exact match
                    if (t.Name == interfaceName) return true;

                    // For generic types, compare the name part before the backtick
                    if (t.IsGenericType)
                    {
                        int backtickIndex = t.Name.IndexOf('`');
                        if (backtickIndex > 0)
                        {
                            string baseName = t.Name.Substring(0, backtickIndex);
                            return baseName == interfaceName;
                        }
                    }

                    return false;
                });

            if (iface == null)
                return null;

            return FormatInterfaceDefinition(iface, Path.GetFileName(entry.FullName));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error processing archive entry {EntryName}", entry.FullName);
            return null;
        }
    }
}
