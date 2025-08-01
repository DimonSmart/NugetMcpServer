using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Models;

namespace NuGetMcpServer.Services;

public class NuGetPackageService(ILogger<NuGetPackageService> logger, HttpClient httpClient, MetaPackageDetector metaPackageDetector, IMemoryCache cache)
{

    public async Task<string> GetLatestVersion(string packageId)
    {
        IReadOnlyList<string> versions = await GetPackageVersions(packageId);
        return versions.Last();
    }

    public async Task<IReadOnlyList<string>> GetPackageVersions(string packageId)
    {
        string indexUrl = $"https://api.NuGet.org/v3-flatcontainer/{packageId.ToLower()}/index.json";
        logger.LogInformation("Fetching versions for package {PackageId} from {Url}", packageId, indexUrl);
        string json = await httpClient.GetStringAsync(indexUrl);
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement versionsArray = doc.RootElement.GetProperty("versions");
        var versions = new List<string>();

        foreach (JsonElement element in versionsArray.EnumerateArray())
        {
            string? version = element.GetString();
            if (!string.IsNullOrWhiteSpace(version))
            {
                versions.Add(version);
            }
        }

        return versions;
    }

    public async Task<IReadOnlyList<string>> GetLatestVersions(string packageId, int count = 20)
    {
        IReadOnlyList<string> versions = await GetPackageVersions(packageId);
        return versions.TakeLast(count).ToList();
    }

    public async Task<MemoryStream> DownloadPackageAsync(string packageId, string version, IProgressNotifier? progress = null)
    {
        string cacheKey = $"{packageId.ToLowerInvariant()}:{version}";

        if (cache.TryGetValue(cacheKey, out byte[]? cachedBytes))
        {
            logger.LogInformation("Using cached package {PackageId} v{Version}", packageId, version);
            progress?.ReportMessage($"Using cached package {packageId} v{version}");
            return new MemoryStream(cachedBytes, writable: false);
        }

        string url = $"https://api.NuGet.org/v3-flatcontainer/{packageId.ToLower()}/{version}/{packageId.ToLower()}.{version}.nupkg";
        logger.LogInformation("Downloading package from {Url}", url);

        progress?.ReportMessage($"Starting package download {packageId} v{version}");

        byte[] response = await httpClient.GetByteArrayAsync(url);

        cache.Set(cacheKey, response, TimeSpan.FromMinutes(5));

        progress?.ReportMessage("Package downloaded successfully");

        return new MemoryStream(response);
    }

    public (Assembly? assembly, Type[] types) LoadAssemblyFromMemoryWithTypes(byte[] assemblyData, AssemblyLoadContext? loadContext = null)
    {
        try
        {
            var assembly = loadContext == null
                ? Assembly.Load(assemblyData)
                : loadContext.LoadFromStream(new MemoryStream(assemblyData));

            try
            {
                var types = assembly.GetTypes();
                return (assembly, types);
            }
            catch (ReflectionTypeLoadException ex)
            {
                logger.LogWarning("Some types could not be loaded from assembly due to missing dependencies. Loaded {LoadedCount} out of {TotalCount} types",
                    ex.Types.Count(t => t != null), ex.Types.Length);

                var loadedTypes = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
                return (assembly, loadedTypes);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load assembly from memory. Assembly size: {Size} bytes", assemblyData.Length);
            return (null, Array.Empty<Type>());
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

        string searchUrl = $"https://azuresearch-usnc.NuGet.org/query" +
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
                PackageId = packageElement.GetProperty("id").GetString() ?? string.Empty,
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

    public PackageInfo GetPackageInfoAsync(Stream packageStream, string packageId, string version)
    {
        try
        {
            var isMetaPackage = metaPackageDetector.IsMetaPackage(packageStream, packageId);
            var dependencies = GetPackageDependencies(packageStream);

            packageStream.Position = 0;
            using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
            using var nuspecStream = reader.GetNuspec();
            var nuspecReader = new NuspecReader(nuspecStream);

            var authors = nuspecReader.GetAuthors()?.Split(',').Select(a => a.Trim()).ToList() ?? [];
            var tags = nuspecReader.GetTags()?.Split(' ', ',').Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? [];

            return new PackageInfo
            {
                PackageId = packageId,
                Version = version,
                Description = nuspecReader.GetDescription() ?? string.Empty,
                Authors = authors,
                Tags = tags,
                ProjectUrl = nuspecReader.GetProjectUrl()?.ToString() ?? string.Empty,
                LicenseUrl = nuspecReader.GetLicenseUrl()?.ToString() ?? string.Empty,
                IsMetaPackage = isMetaPackage,
                Dependencies = dependencies
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting package info for {PackageId} v{Version}", packageId, version);
            return new PackageInfo
            {
                PackageId = packageId,
                Version = version,
                Description = "Error retrieving package information",
                IsMetaPackage = false,
                Dependencies = []
            };
        }
    }
}
