using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Extensions.Logging;

using NuGet.Packaging;

using NuGetMcpServer.Extensions;

namespace NuGetMcpServer.Services;

public class NuGetPackageService(ILogger<NuGetPackageService> logger, HttpClient httpClient)
{

    public async Task<string> GetLatestVersion(string packageId)
    {
        string indexUrl = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLower()}/index.json";
        logger.LogInformation("Fetching latest version for package {PackageId} from {Url}", packageId, indexUrl);
        string json = await httpClient.GetStringAsync(indexUrl);
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement versionsArray = doc.RootElement.GetProperty("versions");
        List<string> versions = new List<string>();

        foreach (JsonElement element in versionsArray.EnumerateArray())
        {
            string? version = element.GetString();
            if (!string.IsNullOrWhiteSpace(version))
            {
                versions.Add(version);
            }
        }

        return versions.Last();
    }

    public async Task<MemoryStream> DownloadPackageAsync(string packageId, string version, IProgressNotifier? progress = null)
    {
        string url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLower()}/{version}/{packageId.ToLower()}.{version}.nupkg";
        logger.LogInformation("Downloading package from {Url}", url);

        progress?.ReportMessage($"Starting package download {packageId} v{version}");

        byte[] response = await httpClient.GetByteArrayAsync(url);

        progress?.ReportMessage("Package downloaded successfully");

        return new MemoryStream(response);
    }

    // Loads an assembly from a byte array
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

    public Task<bool> IsMetaPackageAsync(Stream packageStream)
    {
        try
        {
            packageStream.Position = 0;

            using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
            var files = reader.GetFiles();
            var libFiles = files.Where(f => f.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) &&
                                       !f.EndsWith("/_._", StringComparison.OrdinalIgnoreCase) &&
                                       !f.EndsWith("\\_._", StringComparison.OrdinalIgnoreCase)).ToList();

            using var nuspecStream = reader.GetNuspec();
            var nuspecReader = new NuspecReader(nuspecStream);
            var dependencyGroups = nuspecReader.GetDependencyGroups();
            var hasDependencies = dependencyGroups.Any(group => group.Packages.Any());
            var description = nuspecReader.GetDescription() ?? string.Empty;

            // Method 1: No lib files but has dependencies (classic meta-package)
            if (!libFiles.Any() && hasDependencies)
            {
                logger.LogDebug("Package determined as meta-package: no lib files but has dependencies");
                return Task.FromResult(true);
            }

            // Method 3: High dependency to content ratio (many dependencies, few lib files)
            if (hasDependencies && dependencyGroups.SelectMany(g => g.Packages).Count() >= 2 && libFiles.Count <= 3)
            {
                logger.LogDebug("Package determined as meta-package: high dependency to content ratio ({DependencyCount} deps, {LibFileCount} lib files)",
                    dependencyGroups.SelectMany(g => g.Packages).Count(), libFiles.Count);
                return Task.FromResult(true);
            }

            logger.LogDebug("Package analysis: hasLib={HasLib}, hasDependencies={HasDependencies}, isMetaPackage=false",
                libFiles.Any(), hasDependencies);

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error checking if package is meta-package, defaulting to false");
            return Task.FromResult(false);
        }
    }

    public List<PackageDependency> GetPackageDependencies(Stream packageStream)
    {
        try
        {
            packageStream.Position = 0;
            using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
            using var nuspecStream = reader.GetNuspec();
            var nuspecReader = new NuspecReader(nuspecStream);
            var dependencyGroups = nuspecReader.GetDependencyGroups();

            var dependencies = dependencyGroups
                .SelectMany(group => group.Packages.Select(package => new PackageDependency
                {
                    Id = package.Id,
                    Version = package.VersionRange?.ToString() ?? "latest"
                }))
                .DistinctBy(d => d.Id)
                .ToList();

            return dependencies;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error extracting package dependencies using NuGet API, falling back to manual parsing");
            return [];
        }
    }

    public async Task<IReadOnlyCollection<PackageInfo>> SearchPackagesAsync(string query, int take = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        string searchUrl = $"https://azuresearch-usnc.nuget.org/query" +
                       $"?q={Uri.EscapeDataString(query)}" +
                       $"&take={take}" +
                       $"&sortBy=popularity-desc";

        logger.LogInformation("Searching packages with query '{Query}' from {Url}", query, searchUrl);

        var json = await httpClient.GetStringAsync(searchUrl);
        using JsonDocument doc = JsonDocument.Parse(json);
        List<PackageInfo> packages = [];
        JsonElement dataArray = doc.RootElement.GetProperty("data");

        foreach (JsonElement packageElement in dataArray.EnumerateArray())
        {
            PackageInfo packageInfo = new()
            {
                Id = packageElement.GetProperty("id").GetString() ?? string.Empty,
                Version = packageElement.GetProperty("version").GetString() ?? string.Empty,
                Description = packageElement.TryGetProperty("description", out JsonElement desc) ? desc.GetString() : null,
                DownloadCount = packageElement.TryGetProperty("totalDownloads", out JsonElement downloads) ? downloads.GetInt64() : 0,
                ProjectUrl = packageElement.TryGetProperty("projectUrl", out JsonElement projectUrl) ? projectUrl.GetString() : null
            };

            // Extract tags
            if (packageElement.TryGetProperty("tags", out JsonElement tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            {
                packageInfo.Tags = tagsElement.EnumerateArray()
                    .Where(t => t.ValueKind == JsonValueKind.String)
                    .Select(t => t.GetString()!)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();
            }

            // Extract authors
            if (packageElement.TryGetProperty("authors", out JsonElement authorsElement) && authorsElement.ValueKind == JsonValueKind.Array)
            {
                packageInfo.Authors = authorsElement.EnumerateArray()
                    .Where(a => a.ValueKind == JsonValueKind.String)
                    .Select(a => a.GetString()!)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .ToList();
            }
            packages.Add(packageInfo);
        }

        return packages.OrderByDescending(p => p.DownloadCount).ToList();
    }

    public string GetPackageDescription(Stream packageStream)
    {
        try
        {
            packageStream.Position = 0;
            using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
            using var nuspecStream = reader.GetNuspec();
            var nuspecReader = new NuspecReader(nuspecStream);
            return nuspecReader.GetDescription() ?? string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error extracting package description");
            return string.Empty;
        }
    }
}
