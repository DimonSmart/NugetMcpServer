using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;

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
        logger.LogInformation("Fetching latest version for package {PackageId} from {Url}", packageId, indexUrl);
        var json = await httpClient.GetStringAsync(indexUrl);
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
    /// <param name="progress">Progress notification for long-running operations</param>
    /// <returns>MemoryStream containing the package</returns>
    public async Task<MemoryStream> DownloadPackageAsync(string packageId, string version, IProgress<ProgressNotificationValue>? progress = null)
    {
        var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLower()}/{version}/{packageId.ToLower()}.{version}.nupkg";
        logger.LogInformation("Downloading package from {Url}", url);

        progress?.Report(new ProgressNotificationValue() { Progress = 25, Total = 100, Message = $"Starting package download {packageId} v{version}" });

        var response = await httpClient.GetByteArrayAsync(url);

        progress?.Report(new ProgressNotificationValue() { Progress = 100, Total = 100, Message = "Package downloaded successfully" });

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

    /// <summary>
    /// Searches for NuGet packages by query
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="take">Maximum number of results to return</param>
    /// <param name="progress">Progress notification for long-running operations</param>
    /// <returns>List of matching packages</returns>
    public async Task<IReadOnlyCollection<PackageInfo>> SearchPackagesAsync(string query, int take = 20, IProgress<ProgressNotificationValue>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return []; progress?.Report(new ProgressNotificationValue() { Progress = 10, Total = 100, Message = "Preparing search request" });

        var searchUrl = $"https://azuresearch-usnc.nuget.org/query" +
                       $"?q={Uri.EscapeDataString(query)}" +
                       $"&take={take}" +
                       $"&sortBy=popularity-desc";

        logger.LogInformation("Searching packages with query '{Query}' from {Url}", query, searchUrl);

        progress?.Report(new ProgressNotificationValue() { Progress = 30, Total = 100, Message = "Sending request to NuGet API" });

        var json = await httpClient.GetStringAsync(searchUrl);

        progress?.Report(new ProgressNotificationValue() { Progress = 60, Total = 100, Message = "Processing search results" });

        using var doc = JsonDocument.Parse(json);

        var packages = new List<PackageInfo>();
        var dataArray = doc.RootElement.GetProperty("data");

        var totalElements = dataArray.GetArrayLength();
        var processedElements = 0;

        foreach (var packageElement in dataArray.EnumerateArray())
        {
            var packageInfo = new PackageInfo
            {
                Id = packageElement.GetProperty("id").GetString() ?? string.Empty,
                Version = packageElement.GetProperty("version").GetString() ?? string.Empty,
                Description = packageElement.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                DownloadCount = packageElement.TryGetProperty("totalDownloads", out var downloads) ? downloads.GetInt64() : 0,
                ProjectUrl = packageElement.TryGetProperty("projectUrl", out var projectUrl) ? projectUrl.GetString() : null
            };

            // Extract tags
            if (packageElement.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            {
                packageInfo.Tags = tagsElement.EnumerateArray()
                    .Where(t => t.ValueKind == JsonValueKind.String)
                    .Select(t => t.GetString()!)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();
            }

            // Extract authors
            if (packageElement.TryGetProperty("authors", out var authorsElement) && authorsElement.ValueKind == JsonValueKind.Array)
            {
                packageInfo.Authors = authorsElement.EnumerateArray()
                    .Where(a => a.ValueKind == JsonValueKind.String)
                    .Select(a => a.GetString()!)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .ToList();
            }

            packages.Add(packageInfo); processedElements++;

            // Report progress every 10% of packages processed
            if (processedElements % Math.Max(1, totalElements / 10) == 0 || processedElements == totalElements)
            {
                var processingProgress = 60 + (processedElements * 30.0 / totalElements);
                progress?.Report(new ProgressNotificationValue() { Progress = (float)processingProgress, Total = 100, Message = $"Processing package {processedElements} of {totalElements}: {packageInfo.Id}" });
            }
        }

        progress?.Report(new ProgressNotificationValue() { Progress = 100, Total = 100, Message = $"Search completed - Found {packages.Count} packages" });

        return packages.OrderByDescending(p => p.DownloadCount).ToList();
    }
}
