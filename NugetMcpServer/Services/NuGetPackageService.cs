using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetMcpServer.Configuration;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Models;

namespace NuGetMcpServer.Services;

public class NuGetPackageService
{
    private const string DefaultNuGetSource = "https://api.nuget.org/v3/index.json";
    private static readonly char[] SourceSeparators = [';', ',', '\r', '\n'];
    private static readonly char[] TagSeparators = [' ', ','];

    private readonly ILogger<NuGetPackageService> _logger;
    private readonly MetaPackageDetector _metaPackageDetector;
    private readonly IMemoryCache _cache;
    private readonly NuGet.Common.ILogger _nugetLogger;
    private readonly IReadOnlyList<PackageSource> _packageSources;
    private readonly IReadOnlyList<SourceRepository> _repositories;
    private readonly Dictionary<string, SourceRepository> _repositoriesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SourceRepository> _repositoriesBySource = new(StringComparer.OrdinalIgnoreCase);

    public NuGetPackageService(
        ILogger<NuGetPackageService> logger,
        MetaPackageDetector metaPackageDetector,
        IMemoryCache cache,
        NuGetSourceOptions options)
    {
        _logger = logger;
        _metaPackageDetector = metaPackageDetector;
        _cache = cache;
        _nugetLogger = new NuGetLoggerAdapter(logger);

        _packageSources = LoadPackageSources(options ?? new NuGetSourceOptions(), logger);
        _repositories = _packageSources
            .Select(source => new SourceRepository(source, Repository.Provider.GetCoreV3()))
            .ToList();

        foreach (var repository in _repositories)
        {
            _repositoriesByName[repository.PackageSource.Name] = repository;
            _repositoriesBySource[NormalizeSource(repository.PackageSource.Source)] = repository;
        }

        if (_repositories.Count > 0)
        {
            _logger.LogInformation(
                "NuGet sources: {Sources}",
                string.Join(", ", _repositories.Select(r => $"{r.PackageSource.Name} ({r.PackageSource.Source})")));
        }
        else
        {
            _logger.LogWarning("No NuGet sources configured. Defaulting to {Source}", DefaultNuGetSource);
        }
    }

    public async Task<string> GetLatestVersion(string packageId, string? source = null)
    {
        IReadOnlyList<string> versions = await GetPackageVersions(packageId, source);
        return versions.Last();
    }

    public async Task<IReadOnlyList<string>> GetPackageVersions(string packageId, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        var repositories = ResolveRepositories(source);
        foreach (var repository in repositories)
        {
            var versions = await GetPackageVersionsFromRepositoryAsync(repository, packageId);
            if (versions.Count > 0)
            {
                return versions
                    .OrderBy(v => v)
                    .Select(v => v.ToNormalizedString())
                    .ToList();
            }
        }

        throw new InvalidOperationException(BuildNotFoundMessage(packageId, source));
    }

    public async Task<IReadOnlyList<string>> GetLatestVersions(string packageId, int count = 20, string? source = null)
    {
        IReadOnlyList<string> versions = await GetPackageVersions(packageId, source);
        return versions.TakeLast(count).ToList();
    }

    public async Task<MemoryStream> DownloadPackageAsync(
        string packageId,
        string version,
        IProgressNotifier? progress = null,
        string? source = null)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentNullException(nameof(version));
        }

        var repositories = ResolveRepositories(source);
        var parsedVersion = NuGetVersion.Parse(version);

        foreach (var repository in repositories)
        {
            var cacheKey = BuildCacheKey(packageId, version, repository.PackageSource.Source);
            if (_cache.TryGetValue(cacheKey, out byte[]? cachedBytes) && cachedBytes != null)
            {
                _logger.LogInformation(
                    "Using cached package {PackageId} v{Version} from {Source}",
                    packageId, version, repository.PackageSource.Name);
                progress?.ReportMessage($"Using cached package {packageId} v{version}");
                return new MemoryStream(cachedBytes, writable: false);
            }

            var findPackage = await repository.GetResourceAsync<FindPackageByIdResource>();
            if (findPackage == null)
            {
                continue;
            }

            using var cacheContext = new SourceCacheContext();
            using var stream = new MemoryStream();

            _logger.LogInformation(
                "Downloading package {PackageId} v{Version} from {Source}",
                packageId, version, repository.PackageSource.Name);

            progress?.ReportMessage($"Downloading package {packageId} v{version} from {repository.PackageSource.Name}");

            bool success = await findPackage.CopyNupkgToStreamAsync(
                packageId,
                parsedVersion,
                stream,
                cacheContext,
                _nugetLogger,
                CancellationToken.None);

            if (!success)
            {
                continue;
            }

            byte[] response = stream.ToArray();
            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(5));

            progress?.ReportMessage("Package downloaded successfully");
            return new MemoryStream(response, writable: false);
        }

        throw new InvalidOperationException(BuildNotFoundMessage(packageId, source));
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
            _logger.LogDebug(ex, "Error extracting package dependencies using NuGet API, falling back to manual parsing");
            return [];
        }
    }

    public async Task<IReadOnlyCollection<PackageInfo>> SearchPackagesAsync(string query, int take = 20, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var repositories = ResolveRepositories(source);
        var results = new List<PackageInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var repository in repositories)
        {
            try
            {
                var searchResource = await repository.GetResourceAsync<PackageSearchResource>();
                if (searchResource == null)
                {
                    _logger.LogDebug("Search is not available for source {Source}", repository.PackageSource.Name);
                    continue;
                }

                var filter = new SearchFilter(includePrerelease: true)
                {
                    IncludeDelisted = false
                };

                _logger.LogInformation(
                    "Searching packages with query '{Query}' from {Source}",
                    query, repository.PackageSource.Name);

                var packages = await searchResource.SearchAsync(
                    query,
                    filter,
                    0,
                    take,
                    _nugetLogger,
                    CancellationToken.None);

                foreach (var package in packages)
                {
                    var info = MapPackageInfo(package);
                    if (seen.Add(info.PackageId))
                    {
                        results.Add(info);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search packages in source {Source}", repository.PackageSource.Name);
            }
        }

        if (take > 0 && results.Count > take)
        {
            return results.Take(take).ToList();
        }

        return results;
    }

    public PackageInfo GetPackageInfoAsync(Stream packageStream, string packageId, string version)
    {
        try
        {
            var isMetaPackage = _metaPackageDetector.IsMetaPackage(packageStream, packageId);
            var dependencies = GetPackageDependencies(packageStream);

            packageStream.Position = 0;
            using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
            using var nuspecStream = reader.GetNuspec();
            var nuspecReader = new NuspecReader(nuspecStream);

            var authors = nuspecReader.GetAuthors()?.Split(',').Select(a => a.Trim()).ToList() ?? [];
            var tags = nuspecReader.GetTags()?.Split(TagSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToList() ?? [];

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
            _logger.LogError(ex, "Error getting package info for {PackageId} v{Version}", packageId, version);
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

    public async Task<IReadOnlyList<string>> ListPackageFilesAsync(
        string packageId,
        string version,
        IProgressNotifier? progress = null,
        string? source = null)
    {
        using var packageStream = await DownloadPackageAsync(packageId, version, progress, source);
        using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
        var files = reader.GetFiles().OrderBy(f => f).ToList();
        return files;
    }

    public async Task<FileContentResult> GetPackageFileAsync(
        string packageId,
        string version,
        string filePath,
        long offset = 0,
        int? bytes = null,
        IProgressNotifier? progress = null,
        string? source = null)
    {
        using var packageStream = await DownloadPackageAsync(packageId, version, progress, source);
        using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);

        if (!reader.GetFiles().Contains(filePath))
            throw new FileNotFoundException($"File {filePath} not found in package {packageId} v{version}");

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be greater than or equal to 0.");
        }

        if (bytes.HasValue && bytes.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), "Bytes must be greater than 0.");
        }

        using var fileStream = reader.GetStream(filePath);
        if (fileStream.CanSeek)
        {
            if (offset > fileStream.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), $"Offset {offset} is greater than file size {fileStream.Length}.");
            }

            fileStream.Seek(offset, SeekOrigin.Begin);
        }
        else if (offset > 0)
        {
            await SkipBytesAsync(fileStream, offset);
        }

        var maxBytes = Math.Min(bytes ?? 1_000_000, 1_000_000);
        var buffer = new byte[maxBytes];
        var read = await fileStream.ReadAsync(buffer.AsMemory(0, maxBytes));

        var isBinary = IsBinary(buffer, read);
        var content = isBinary
            ? Convert.ToBase64String(buffer, 0, read)
            : Encoding.UTF8.GetString(buffer, 0, read);

        return new FileContentResult
        {
            PackageId = packageId,
            Version = version,
            FilePath = filePath,
            Content = content,
            IsBinary = isBinary
        };
    }

    private async Task<IReadOnlyList<NuGetVersion>> GetPackageVersionsFromRepositoryAsync(SourceRepository repository, string packageId)
    {
        var findPackage = await repository.GetResourceAsync<FindPackageByIdResource>();
        if (findPackage == null)
        {
            return [];
        }

        using var cacheContext = new SourceCacheContext();
        var versions = await findPackage.GetAllVersionsAsync(
            packageId,
            cacheContext,
            _nugetLogger,
            CancellationToken.None);

        return versions?.ToList() ?? [];
    }

    private IReadOnlyList<SourceRepository> ResolveRepositories(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return _repositories;
        }

        return [ResolveRepository(source)];
    }

    private SourceRepository ResolveRepository(string source)
    {
        if (_repositoriesByName.TryGetValue(source, out var repository))
        {
            return repository;
        }

        var normalized = NormalizeSource(source);
        if (_repositoriesBySource.TryGetValue(normalized, out repository))
        {
            return repository;
        }

        string available = _repositories.Count == 0
            ? "none"
            : string.Join(", ", _repositories.Select(r => r.PackageSource.Name));

        throw new ArgumentException($"Unknown NuGet source '{source}'. Available: {available}", nameof(source));
    }

    private static IReadOnlyList<PackageSource> LoadPackageSources(NuGetSourceOptions options, ILogger logger)
    {
        var configuredSources = LoadSourcesFromConfig(options.ConfigPath, logger);
        var explicitSources = options.Sources
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Select(source => source.Trim())
            .ToList();

        if (explicitSources.Count > 0)
        {
            return DeduplicateSources(ResolveExplicitSources(explicitSources, configuredSources));
        }

        if (configuredSources.Count > 0)
        {
            return DeduplicateSources(configuredSources);
        }

        return [new PackageSource(DefaultNuGetSource, "nuget.org")];
    }

    private static List<PackageSource> LoadSourcesFromConfig(string? configPath, ILogger logger)
    {
        try
        {
            ISettings settings;

            if (!string.IsNullOrWhiteSpace(configPath))
            {
                string normalized = NormalizeConfigPath(configPath);
                if (Directory.Exists(normalized))
                {
                    settings = Settings.LoadDefaultSettings(normalized);
                }
                else if (File.Exists(normalized))
                {
                    settings = Settings.LoadSpecificSettings(Path.GetDirectoryName(normalized)!, Path.GetFileName(normalized));
                }
                else
                {
                    logger.LogWarning("NuGet config not found at {ConfigPath}", normalized);
                    return [];
                }
            }
            else
            {
                settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory());
            }

            var provider = new PackageSourceProvider(settings);
            return provider.LoadPackageSources()
                .Where(source => source.IsEnabled)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load NuGet sources from config");
            return [];
        }
    }

    private static List<PackageSource> ResolveExplicitSources(
        IEnumerable<string> explicitSources,
        List<PackageSource> configuredSources)
    {
        var byName = configuredSources.ToDictionary(source => source.Name, StringComparer.OrdinalIgnoreCase);
        var bySource = configuredSources.ToDictionary(
            source => NormalizeSource(source.Source),
            StringComparer.OrdinalIgnoreCase);

        var resolved = new List<PackageSource>();

        foreach (var source in explicitSources)
        {
            if (byName.TryGetValue(source, out var match) ||
                bySource.TryGetValue(NormalizeSource(source), out match))
            {
                resolved.Add(match);
            }
            else
            {
                resolved.Add(new PackageSource(source, source));
            }
        }

        return resolved;
    }

    private static List<PackageSource> DeduplicateSources(IEnumerable<PackageSource> sources)
    {
        var result = new List<PackageSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            var normalized = NormalizeSource(source.Source);
            if (seen.Add(normalized))
            {
                result.Add(source);
            }
        }

        return result;
    }

    private static string NormalizeSource(string source)
    {
        var trimmed = source.Trim();
        if (LooksLikePath(trimmed))
        {
            try
            {
                return Path.GetFullPath(trimmed)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return uri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.Unescaped).TrimEnd('/');
        }

        return trimmed.TrimEnd('/');
    }

    private static bool LooksLikePath(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (Path.IsPathRooted(source) ||
            source.StartsWith("./", StringComparison.Ordinal) ||
            source.StartsWith(".\\", StringComparison.Ordinal) ||
            source.StartsWith("../", StringComparison.Ordinal) ||
            source.StartsWith("..\\", StringComparison.Ordinal) ||
            source.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return true;
        }

        if (Directory.Exists(source) || File.Exists(source))
        {
            return true;
        }

        return Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.IsFile;
    }

    private static string BuildCacheKey(string packageId, string version, string source)
    {
        return $"{NormalizeSource(source)}|{packageId.ToLowerInvariant()}:{version}";
    }

    private static string BuildNotFoundMessage(string packageId, string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return $"Package {packageId} was not found in configured sources.";
        }

        return $"Package {packageId} was not found in source '{source}'.";
    }

    private static string NormalizeConfigPath(string configPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(configPath.Trim());
        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), expanded));
    }

    private static List<string>? SplitTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return null;
        }

        return tags.Split(TagSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string>? SplitAuthors(string? authors)
    {
        if (string.IsNullOrWhiteSpace(authors))
        {
            return null;
        }

        return authors.Split(SourceSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(author => author.Trim())
            .Where(author => !string.IsNullOrWhiteSpace(author))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static PackageInfo MapPackageInfo(IPackageSearchMetadata metadata)
    {
        return new PackageInfo
        {
            PackageId = metadata.Identity.Id,
            Version = metadata.Identity.Version?.ToNormalizedString() ?? string.Empty,
            Description = metadata.Description ?? metadata.Summary ?? string.Empty,
            DownloadCount = metadata.DownloadCount ?? 0,
            ProjectUrl = metadata.ProjectUrl?.ToString(),
            Tags = SplitTags(metadata.Tags),
            Authors = SplitAuthors(metadata.Authors)
        };
    }

    private static bool IsBinary(byte[] data, int length)
    {
        try
        {
            var decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            decoder.GetString(data, 0, length);
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static async Task SkipBytesAsync(Stream stream, long offset)
    {
        var buffer = new byte[8192];
        long remaining = offset;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)));
            if (read == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), $"Offset {offset} is greater than file size.");
            }

            remaining -= read;
        }
    }
}
