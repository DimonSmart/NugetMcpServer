using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace NuGetMcpServer.Services;

/// <summary>
/// Service for interacting with NuGet packages
/// </summary>
public class NuGetPackageService(ILogger<NuGetPackageService> logger, HttpClient httpClient)
{

    /// <summary>
    /// Gets the latest version of a NuGet package
    /// </summary>
    /// <param name="packageId">NuGet package ID</param>
    /// <returns>Latest version string</returns>
    public async Task<string> GetLatestVersion(string packageId)
    {
        var indexUrl = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLower()}/index.json";
        logger.LogInformation("Fetching latest version for package {PackageId} from {Url}", packageId, indexUrl); var json = await httpClient.GetStringAsync(indexUrl);
        using var doc = JsonDocument.Parse(json);

        var versionsArray = doc.RootElement.GetProperty("versions");
        var versions = new List<string>();

        foreach (var element in versionsArray.EnumerateArray())
        {
            var version = element.GetString();
            if (!string.IsNullOrWhiteSpace(version))
            {
                versions.Add(version);
            }
        }

        return versions.Last();
    }

    /// <summary>
    /// Downloads a NuGet package as a memory stream
    /// </summary>
    /// <param name="packageId">Package ID</param>
    /// <param name="version">Package version</param>
    /// <returns>MemoryStream containing the package</returns>
    public async Task<MemoryStream> DownloadPackageAsync(string packageId, string version)
    {
        var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLower()}/{version}/{packageId.ToLower()}.{version}.nupkg";
        logger.LogInformation("Downloading package from {Url}", url);

        var response = await httpClient.GetByteArrayAsync(url);
        return new MemoryStream(response);
    }

    /// <summary>
    /// Loads an assembly from a byte array
    /// </summary>
    /// <param name="assemblyData">Assembly binary data</param>
    /// <returns>Loaded assembly or null if loading failed</returns>
    public Assembly? LoadAssemblyFromMemory(byte[] assemblyData)
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
}
