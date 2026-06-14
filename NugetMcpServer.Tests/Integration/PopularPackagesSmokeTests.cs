using System.Text;
using NuGetMcpServer.Services;
using NuGetMcpServer.Tests.Helpers;
using NuGetMcpServer.Tools;
using Xunit;
using static NuGetMcpServer.Extensions.ProgressNotifier;

namespace NuGetMcpServer.Tests.Integration;

public class PopularNuGetPackagesMetadataSmokeTests : TestBase
{
    private static readonly PackageSmokeExpectation[] PopularPackages =
    [
        new("Newtonsoft.Json", 95, ["JsonConvert", "JsonSerializer", "JsonSerializerSettings", "JObject", "JArray", "DefaultContractResolver"]),
        new("Serilog", 35, ["Log", "LoggerConfiguration", "Logger", "LogEvent", "MessageTemplateTextFormatter"]),
        new("AWSSDK.Core", 600, ["AmazonServiceClient", "AmazonWebServiceRequest", "AmazonWebServiceResponse", "ClientConfig", "RegionEndpoint", "BasicAWSCredentials"]),
        new("Polly", 45, ["Policy", "PolicyBuilder", "Context", "ResiliencePipeline", "ResiliencePipelineBuilder"]),
        new("Google.Protobuf", 90, ["ByteString", "CodedInputStream", "CodedOutputStream", "MessageParser", "JsonFormatter", "JsonParser"]),
        new("StackExchange.Redis", 55, ["ConnectionMultiplexer", "ConfigurationOptions", "SocketManager", "LuaScript", "LoadedLuaScript"]),
        new("Castle.Core", 120, ["ProxyGenerator", "ProxyGenerationOptions", "StandardInterceptor", "DefaultProxyBuilder", "ComponentActivatorException"]),
        new("Grpc.Core.Api", 60, ["CallInvoker", "ChannelBase", "ClientBase", "Metadata", "ServerCallContext", "Marshaller<T>"]),
        new("Swashbuckle.AspNetCore.SwaggerGen", 55, ["SwaggerGenOptions", "SwaggerGenerator", "SchemaGenerator", "OperationFilterContext", "SchemaFilterContext"]),
        new("OpenTelemetry.Api", 65, ["Tracer", "TelemetrySpan", "Baggage", "CompositeTextMapPropagator", "PropagationContext"]),
        new("CsvHelper", 160, ["CsvReader", "CsvWriter", "CsvParser", "CsvConfiguration", "ClassMap", "CsvHelperException"]),
        new("Grpc.Net.Client", 15, ["GrpcChannel", "GrpcChannelOptions", "GrpcChannelCredentials", "GrpcChannelLogger"]),
        new("OpenTelemetry", 80, ["TracerProvider", "TracerProviderBuilder", "MeterProvider", "BatchExportActivityProcessor", "BaseExporter<T>"]),
        new("Humanizer.Core", 90, ["Configurator", "Vocabulary", "DefaultFormatter", "InflectorExtensions", "DateHumanizeExtensions"]),
        new("Npgsql", 130, ["NpgsqlConnection", "NpgsqlCommand", "NpgsqlDataSource", "NpgsqlDataReader", "NpgsqlParameter", "NpgsqlConnectionStringBuilder"]),
        new("BouncyCastle.Cryptography", 1200, ["BigInteger", "X509Certificate", "RsaKeyPairGenerator", "SecureRandom", "PemReader", "Pkcs10CertificationRequest"]),
        new("Moq", 35, ["Mock", "Mock<T>", "MockRepository", "Times", "It", "MockException"]),
        new("FluentValidation", 95, ["AbstractValidator<T>", "InlineValidator<T>", "ValidationResult", "ValidationFailure", "ValidationContext<T>"]),
        new("AutoMapper", 85, ["MapperConfiguration", "Mapper", "Profile", "ResolutionContext", "TypeMap"]),
        new("Dapper", 10, ["SqlMapper", "DynamicParameters", "DbString", "SqlMapper.GridReader"]),
        new("Google.Apis.Auth", 90, ["GoogleCredential", "UserCredential", "ServiceAccountCredential", "ComputeCredential", "JsonCredentialParameters"]),
        new("FluentAssertions", 450, ["AssertionScope", "ObjectAssertions", "StringAssertions", "GenericCollectionAssertions<T>", "EquivalencyAssertionOptions<T>"]),
        new("YamlDotNet", 250, ["Serializer", "Deserializer", "SerializerBuilder", "DeserializerBuilder", "YamlException"]),
        new("Npgsql.EntityFrameworkCore.PostgreSQL", 180, ["NpgsqlDbContextOptionsBuilder", "NpgsqlRetryingExecutionStrategy", "NpgsqlMigrationSqlGenerator", "NpgsqlSqlGenerationHelper"]),
        new("Google.Apis", 45, ["BaseClientService", "ClientServiceRequest<T>", "GoogleApiException", "RequestError", "MediaDownloader"]),
        new("Google.Apis.Core", 70, ["ConfigurableHttpClient", "ConfigurableMessageHandler", "BackOffHandler", "ExponentialBackOff", "GZipEnabled"]),
        new("SharpZipLib", 170, ["ZipFile", "ZipInputStream", "ZipOutputStream", "TarInputStream", "GZipInputStream", "BZip2InputStream"]),
        new("NUnit", 200, ["Assert", "TestContext", "TestAttribute", "SetUpAttribute", "TearDownAttribute", "Is", "Has", "Does"]),
        new("DnsClient", 45, ["LookupClient", "LookupClientOptions", "DnsQueryResponse", "DnsQuestion", "DnsResourceRecord"]),
        new("NLog", 230, ["LogManager", "Logger", "LogFactory", "LogEventInfo", "LoggingConfiguration", "Target"]),
        new("DocumentFormat.OpenXml", 3000, ["OpenXmlElement", "OpenXmlPart", "WordprocessingDocument", "SpreadsheetDocument", "PresentationDocument", "OpenXmlReader"]),
        new("Autofac", 120, ["ContainerBuilder", "Module", "Container", "ContainerBuilderExtensions", "ComponentRegistryBuilder"]),
        new("RabbitMQ.Client", 65, ["ConnectionFactory", "AmqpTcpEndpoint", "PublicationAddress", "BasicProperties", "ShutdownEventArgs"]),
        new("MediatR", 8, ["Mediator", "MediatRServiceConfiguration", "NotificationHandlerExecutor"]),
        new("RestSharp", 80, ["RestClient", "RestRequest", "RestResponse", "RestClientOptions", "Parameter", "FileParameter"]),
        new("Hangfire.Core", 230, ["BackgroundJob", "BackgroundJobClient", "BackgroundJobServer", "RecurringJob", "JobStorage", "GlobalConfiguration"]),
        new("Renci.SshNet", 110, ["SshClient", "ScpClient", "SftpClient", "ConnectionInfo", "PasswordAuthenticationMethod", "PrivateKeyFile"], "SSH.NET / Renci.SshNet"),
        new("NJsonSchema", 220, ["JsonSchema", "JsonSchemaProperty", "JsonSchemaGenerator", "JsonSchemaResolver", "ValidationError"], MinimumPublicClassesOverride: 60),
        new("MessagePack", 120, ["MessagePackSerializer", "MessagePackSerializerOptions", "MessagePackSecurity", "TypelessFormatter", "MessagePackObjectAttribute"]),
        new("System.Reactive", 130, ["Observable", "Observer", "Subject<T>", "BehaviorSubject<T>", "ReplaySubject<T>", "CompositeDisposable"]),
        new("AngleSharp", 230, ["BrowsingContext", "Configuration", "HtmlParser", "CssParser", "AngleSharpException"]),
        new("JetBrains.Annotations", 70, ["NotNullAttribute", "CanBeNullAttribute", "UsedImplicitlyAttribute", "PublicAPIAttribute", "InstantHandleAttribute"]),
        new("SixLabors.Fonts", 100, ["FontCollection", "FontFamily", "Font", "TextOptions", "FontRectangle", "SystemFonts"]),
        new("IdentityModel", 90, ["DiscoveryDocumentRequest", "TokenClient", "TokenRequest", "TokenResponse", "ClientCredentialsTokenRequest"]),
        new("MongoDB.Bson", 260, ["BsonDocument", "BsonArray", "BsonValue", "BsonString", "BsonInt32", "BsonBinaryData"]),
        new("SixLabors.ImageSharp", 420, ["Image", "Image<TPixel>", "Configuration", "DecoderOptions", "ImageMetadata", "ImageFrame"]),
        new("Google.Api.Gax", 130, ["GaxPreconditions", "RetrySettings", "Expiration", "PollingSettings", "PathTemplate", "ResourceName"], MinimumPublicClassesOverride: 35),
        new("System.Linq.Async", 20, ["AsyncEnumerable", "AsyncQueryable", "AsyncGrouping<TKey,TElement>", "AsyncEnumerableEx"], MinimumPublicClassesOverride: 3),
        new("MongoDB.Driver", 330, ["MongoClient", "MongoClientSettings", "MongoDatabaseSettings", "MongoCollectionSettings", "Builders<T>", "FilterDefinition<T>"]),
        new("NodaTime", 120, ["Instant", "LocalDate", "LocalDateTime", "ZonedDateTime", "DateTimeZone", "Period", "Duration"])
    ];

    private readonly NuGetPackageService _packageService;

    public PopularNuGetPackagesMetadataSmokeTests(ITestOutputHelper output) : base(output)
    {
        _packageService = CreateNuGetPackageService();
    }

    [Fact(Explicit = true)]
    [Trait("Category", "Exploratory")]
    [Trait("Category", "Manual")]
    public async Task PopularNuGetPackages_MetadataParser_IsProductionSmokeStable()
    {
        var archiveService = CreateArchiveProcessingService();
        var listTypesTool = new ListTypesTool(new TestLogger<ListTypesTool>(TestOutput), _packageService, archiveService);
        var failures = new List<string>();
        var rows = new List<PackageSmokeRow>();
        var parsedOk = 0;
        var metaPackages = 0;
        var packagesWithoutApi = 0;
        var totalPublicApiFiles = 0;
        var totalPublicTypes = 0;
        var totalPublicMethods = 0;

        foreach (var expectation in PopularPackages)
        {
            try
            {
                var version = await _packageService.GetLatestVersion(expectation.PackageId);
                var loaded = await archiveService.LoadPackageMetadataAsync(expectation.PackageId, version, VoidProgressNotifier);
                var allTypes = loaded.Api.Assemblies.SelectMany(static assembly => assembly.Types).ToList();
                var publicClasses = allTypes.Where(IsPublicClass).ToList();
                var interfaces = allTypes.Where(static type => type.Kind == ApiTypeKind.Interface).ToList();
                var structs = allTypes.Where(static type => type.Kind is ApiTypeKind.Struct or ApiTypeKind.RecordStruct).ToList();
                var enums = allTypes.Where(static type => type.Kind == ApiTypeKind.Enum).ToList();
                var delegates = allTypes.Where(static type => type.Kind == ApiTypeKind.Delegate).ToList();
                var methodCount = allTypes.Sum(static type => type.Methods.Count);
                var propertyCount = allTypes.Sum(static type => type.Properties.Count);
                var eventCount = allTypes.Sum(static type => type.Events.Count);
                var hasApi = loaded.Api.Assemblies.Count > 0 && allTypes.Count > 0;
                var publicApiFileCount = loaded.Api.Assemblies
                    .Where(static assembly => assembly.Types.Count > 0)
                    .Select(static assembly => assembly.PackagePath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var selectedTfms = string.Join(", ", loaded.Api.Assemblies.Select(static assembly => assembly.TargetFramework).Distinct());
                var matchingExamples = expectation.ExamplePublicClasses
                    .Where(sample => publicClasses.Any(type => IsMatchingSample(type, sample)))
                    .ToArray();
                var packageFailures = new List<string>();

                if (loaded.PackageInfo.IsMetaPackage)
                {
                    metaPackages++;
                }
                else if (!hasApi)
                {
                    packagesWithoutApi++;
                    packageFailures.Add("package has no analyzable public API");
                }

                if (publicClasses.Count < expectation.MinimumPublicClasses)
                {
                    packageFailures.Add(
                        $"expected at least {expectation.MinimumPublicClasses} public classes for approximate target {expectation.ExpectedApproxPublicClasses}, got {publicClasses.Count}");
                }

                if (hasApi)
                {
                    var typeResult = await listTypesTool.list_classes_records_structs(expectation.PackageId, version, maxResults: 1);
                    var classRecordStructCount = publicClasses.Count + structs.Count;

                    if (typeResult.TotalCount != classRecordStructCount)
                    {
                        packageFailures.Add(
                            $"list_classes_records_structs returned total {typeResult.TotalCount}, expected {classRecordStructCount} class/record/struct types");
                    }
                }

                foreach (var packageFailure in packageFailures)
                {
                    failures.Add($"{expectation.PackageId}: {packageFailure}");
                }

                totalPublicTypes += allTypes.Count;
                totalPublicMethods += methodCount;
                totalPublicApiFiles += publicApiFileCount;

                if (packageFailures.Count == 0)
                {
                    parsedOk++;
                }

                rows.Add(new PackageSmokeRow(
                    expectation.DisplayName,
                    version,
                    selectedTfms,
                    publicApiFileCount,
                    publicClasses.Count,
                    expectation.ExpectedApproxPublicClasses,
                    expectation.MinimumPublicClasses,
                    allTypes.Count,
                    interfaces.Count,
                    structs.Count,
                    enums.Count,
                    delegates.Count,
                    methodCount,
                    propertyCount,
                    eventCount,
                    matchingExamples.Length,
                    expectation.ExamplePublicClasses.Length,
                    packageFailures.Count == 0 ? "OK" : string.Join("; ", packageFailures)));
            }
            catch (Exception ex)
            {
                failures.Add($"{expectation.PackageId}: {ex.GetType().Name}: {ex.Message}");
                rows.Add(PackageSmokeRow.Failed(expectation.DisplayName, ex));
            }
        }

        WriteSummaryTable(rows);

        var summary = $"""
Total packages: {PopularPackages.Length}
Parsed OK: {parsedOk}
Failed: {failures.Count}
Meta-packages: {metaPackages}
Packages without analyzable API: {packagesWithoutApi}
Total public API files: {totalPublicApiFiles}
Total public types: {totalPublicTypes}
Total public methods: {totalPublicMethods}
""";

        TestOutput.WriteLine(summary);
        Console.WriteLine(summary);
        TestContext.Current.SendDiagnosticMessage(summary);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static bool IsPublicClass(ApiTypeModel type)
    {
        return type.Kind is ApiTypeKind.Class
            or ApiTypeKind.StaticClass
            or ApiTypeKind.AbstractClass
            or ApiTypeKind.SealedClass
            or ApiTypeKind.RecordClass;
    }

    private static bool IsMatchingSample(ApiTypeModel type, string sample)
    {
        var normalizedSample = NormalizeTypeName(sample);
        return NormalizeTypeName(type.Name).Equals(normalizedSample, StringComparison.OrdinalIgnoreCase)
            || NormalizeTypeName(type.FullName).EndsWith("." + normalizedSample, StringComparison.OrdinalIgnoreCase)
            || NormalizeTypeName(type.FullName).EndsWith("+" + normalizedSample, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTypeName(string typeName)
    {
        var genericMarkerIndex = typeName.IndexOf('<', StringComparison.Ordinal);
        if (genericMarkerIndex >= 0)
        {
            typeName = typeName[..genericMarkerIndex];
        }

        var arityMarkerIndex = typeName.IndexOf('`', StringComparison.Ordinal);
        if (arityMarkerIndex >= 0)
        {
            typeName = typeName[..arityMarkerIndex];
        }

        return typeName.Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private void WriteSummaryTable(IEnumerable<PackageSmokeRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("| Package | Version | TFM | Public API files | Public classes | Expected | Min | Public types | Interfaces | Structs | Enums | Delegates | Methods | Properties | Events | Examples | Parsing errors |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |");

        foreach (var row in rows)
        {
            builder.AppendLine(
                $"| {EscapeCell(row.Package)} | {EscapeCell(row.Version)} | {EscapeCell(row.TargetFrameworks)} | {row.PublicApiFiles} | {row.PublicClasses} | ~{row.ExpectedApproxPublicClasses} | {row.MinimumPublicClasses} | {row.PublicTypes} | {row.Interfaces} | {row.Structs} | {row.Enums} | {row.Delegates} | {row.Methods} | {row.Properties} | {row.Events} | {row.FoundExamples}/{row.ExpectedExamples} | {EscapeCell(row.ParseError)} |");
        }

        var table = builder.ToString();
        TestOutput.WriteLine(table);
        Console.WriteLine(table);
        TestContext.Current.SendDiagnosticMessage(table);
    }

    private static string EscapeCell(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Replace("|", "\\|", StringComparison.Ordinal).Replace(Environment.NewLine, " ", StringComparison.Ordinal);
    }

    public sealed record PackageSmokeExpectation(
        string PackageId,
        int ExpectedApproxPublicClasses,
        string[] ExamplePublicClasses,
        string? DisplayNameOverride = null,
        int? MinimumPublicClassesOverride = null)
    {
        public string DisplayName => DisplayNameOverride ?? PackageId;
        public int MinimumPublicClasses => MinimumPublicClassesOverride ?? Math.Max(1, ExpectedApproxPublicClasses / 3);
    }

    public sealed record PackageSmokeRow(
        string Package,
        string Version,
        string TargetFrameworks,
        int PublicApiFiles,
        int PublicClasses,
        int ExpectedApproxPublicClasses,
        int MinimumPublicClasses,
        int PublicTypes,
        int Interfaces,
        int Structs,
        int Enums,
        int Delegates,
        int Methods,
        int Properties,
        int Events,
        int FoundExamples,
        int ExpectedExamples,
        string ParseError)
    {
        public static PackageSmokeRow Failed(string package, Exception exception)
        {
            return new PackageSmokeRow(
                package,
                "-",
                "-",
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
