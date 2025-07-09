using System;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

using NuGet.Packaging;

namespace NuGetMcpServer.Services;

/// <summary>
/// Service for detecting whether a NuGet package is a meta-package
/// </summary>
public class MetaPackageDetector(ILogger<MetaPackageDetector> logger)
{
    public bool IsMetaPackage(Stream packageStream, string? packageId = null)
    {
        try
        {
            packageStream.Position = 0;
            using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
            using var nuspecStream = reader.GetNuspec();
            var nuspecReader = new NuspecReader(nuspecStream);

            return IsMetaPackageCore(nuspecReader, reader, packageId ?? nuspecReader.GetId());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error determining meta-package status for package {PackageId}", packageId);
            return false;
        }
    }

    private bool IsMetaPackageCore(NuspecReader nuspecReader, PackageArchiveReader reader, string packageId)
    {
        // Method 1: Check for explicit packageType = "Dependency" (primary method)
        var packageTypes = nuspecReader.GetPackageTypes();
        if (packageTypes.Any(pt => string.Equals(pt.Name, "Dependency", StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogDebug("Package {PackageId} determined as meta-package: explicit packageType=Dependency", packageId);
            return true;
        }

        // Method 2: Fallback for backward compatibility - no lib files but has dependencies
        var files = reader.GetFiles();
        var libFiles = files.Where(f => f.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) &&
                                   !f.EndsWith("/_._", StringComparison.OrdinalIgnoreCase) &&
                                   !f.EndsWith("\\_._", StringComparison.OrdinalIgnoreCase)).ToList();

        var dependencyGroups = nuspecReader.GetDependencyGroups();
        var hasDependencies = dependencyGroups.Any(group => group.Packages.Any());

        if (!libFiles.Any() && hasDependencies)
        {
            logger.LogDebug("Package {PackageId} determined as meta-package: no lib files but has dependencies (fallback)", packageId);
            return true;
        }

        return false;
    }
}
