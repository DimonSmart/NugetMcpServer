using System.Collections.Generic;

namespace NuGetMcpServer.Services;

/// <summary>
/// Information about a NuGet package
/// </summary>
public class PackageInfo
{
    /// <summary>
    /// Package ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Current version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Package description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Download count (popularity measure)
    /// </summary>
    public long DownloadCount { get; set; }

    /// <summary>
    /// Project URL
    /// </summary>
    public string? ProjectUrl { get; set; }

    /// <summary>
    /// Package tags
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Authors
    /// </summary>
    public List<string>? Authors { get; set; }
}
