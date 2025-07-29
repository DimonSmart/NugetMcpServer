using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace NuGetMcpServer.Services;

/// <summary>
/// AssemblyLoadContext that loads assemblies from an in-memory dictionary.
/// </summary>
internal sealed class InMemoryAssemblyLoadContext : AssemblyLoadContext
{
    private readonly IDictionary<string, byte[]> _assemblies;

    public InMemoryAssemblyLoadContext(IDictionary<string, byte[]> assemblies)
        : base(isCollectible: true)
    {
        _assemblies = assemblies;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (name != null && _assemblies.TryGetValue(name, out var bytes))
        {
            using var ms = new MemoryStream(bytes);
            return LoadFromStream(ms);
        }

        return null;
    }
}
