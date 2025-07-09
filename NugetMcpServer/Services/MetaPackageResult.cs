namespace NuGetMcpServer.Services;

public class MetaPackageResult : PackageResultBase
{
    public MetaPackageResult()
    {
        IsMetaPackage = true;
    }
}
