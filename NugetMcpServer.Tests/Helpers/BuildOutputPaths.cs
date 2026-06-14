namespace NuGetMcpServer.Tests.Helpers;

internal static class BuildOutputPaths
{
    public static string FindProjectAssembly(string projectPath, string assemblyName)
    {
        var (repositoryRoot, configuration, targetFramework) = GetCurrentBuildLayout();
        var candidate = Path.Combine(repositoryRoot, projectPath, "bin", configuration, targetFramework, assemblyName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        throw new FileNotFoundException($"Could not find {assemblyName} in {projectPath}/bin/{configuration}/{targetFramework}.");
    }

    private static (string RepositoryRoot, string Configuration, string TargetFramework) GetCurrentBuildLayout()
    {
        var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var targetFramework = outputDirectory.Name;
        var configuration = outputDirectory.Parent?.Name;
        if (string.IsNullOrWhiteSpace(configuration))
        {
            throw new DirectoryNotFoundException($"Could not resolve build configuration from '{outputDirectory.FullName}'.");
        }

        var directory = outputDirectory;
        while (directory != null)
        {
            var solutionPath = Path.Combine(directory.FullName, "NugetMcpServer.sln");
            if (File.Exists(solutionPath))
            {
                return (directory.FullName, configuration, targetFramework);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find repository root from '{outputDirectory.FullName}'.");
    }
}
