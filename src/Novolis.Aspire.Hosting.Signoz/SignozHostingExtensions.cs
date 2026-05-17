using Aspire.Hosting.ApplicationModel;
using Novolis.Aspire.Hosting.Signoz;

namespace Aspire.Hosting;

/// <summary>Extension methods for provisioning SigNoz in an Aspire distributed application.</summary>
public static class SignozHostingExtensions
{
    private const string ClickHouseDsnEnvVar = "SIGNOZ_OTEL_COLLECTOR_CLICKHOUSE_DSN";
    private const string ClickHouseClusterEnvVar = "SIGNOZ_OTEL_COLLECTOR_CLICKHOUSE_CLUSTER";
    private const string ClickHouseReplicationEnvVar = "SIGNOZ_OTEL_COLLECTOR_CLICKHOUSE_REPLICATION";
    private const string ClickHouseTimeoutEnvVar = "SIGNOZ_OTEL_COLLECTOR_TIMEOUT";
    private const string ClickHouseHostEnvVar = "CLICKHOUSE_HOST";
    private const string SignozHostEnvVar = "SIGNOZ_HOST";
    private const string LowCardinalityEnvVar = "LOW_CARDINAL_EXCEPTION_GROUPING";

    /// <summary>
    /// Adds a SigNoz observability stack (ZooKeeper, ClickHouse, SigNoz UI, schema migrator, and OTLP collector).
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">Aspire resource name for the OTLP collector and connection string.</param>
    /// <param name="httpPort">Optional host port for the SigNoz UI (container port 8080).</param>
    /// <param name="otlpGrpcPort">Optional host port for OTLP gRPC ingestion (container port 4317).</param>
    /// <param name="otlpHttpPort">Optional host port for OTLP HTTP ingestion (container port 4318).</param>
    public static IResourceBuilder<SignozContainerResource> AddSignoz(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? httpPort = null,
        int? otlpGrpcPort = null,
        int? otlpHttpPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var zookeeperName = $"{name}-zookeeper-1";
        var clickhouseName = $"{name}-clickhouse";
        var signozUiName = $"{name}-signoz";
        var migratorName = $"{name}-migrator";

        var assetsPath = SignozAssetProvisioner.PrepareStackAssets(
            name,
            zookeeperHost: zookeeperName,
            clickhouseHost: clickhouseName,
            signozHost: signozUiName);

        var clickhouseAssets = Path.Combine(assetsPath, "clickhouse");
        var otelConfigPath = Path.Combine(assetsPath, "otel-collector-config.yaml");
        var opampConfigPath = Path.Combine(assetsPath, "otel-collector-opamp-config.yaml");

        var zookeeper = builder.AddContainer(zookeeperName, SignozContainerImageTags.ZookeeperImage, SignozContainerImageTags.ZookeeperTag)
            .WithImageRegistry(SignozContainerImageTags.Registry)
            .WithEnvironment("ZOO_SERVER_ID", "1")
            .WithEnvironment("ALLOW_ANONYMOUS_LOGIN", "yes")
            .WithEnvironment("ZOO_AUTOPURGE_INTERVAL", "1")
            .WithEnvironment("ZOO_ENABLE_PROMETHEUS_METRICS", "yes")
            .WithEnvironment("ZOO_PROMETHEUS_METRICS_PORT_NUMBER", "9141")
            .WithVolume($"{name}-zookeeper-data", "/bitnami/zookeeper")
            .WithHttpEndpoint(targetPort: 8080, name: "http")
            .WithHttpHealthCheck("/commands/ruok", endpointName: "http", statusCode: 200);

        var clickhouse = builder.AddContainer(clickhouseName, SignozContainerImageTags.ClickHouseImage, SignozContainerImageTags.ClickHouseTag)
            .WithImageRegistry(SignozContainerImageTags.Registry)
            .WithEnvironment("CLICKHOUSE_SKIP_USER_SETUP", "1")
            .WithBindMount(Path.Combine(clickhouseAssets, "config.xml"), "/etc/clickhouse-server/config.xml")
            .WithBindMount(Path.Combine(clickhouseAssets, "users.xml"), "/etc/clickhouse-server/users.xml")
            .WithBindMount(Path.Combine(clickhouseAssets, "custom-function.xml"), "/etc/clickhouse-server/custom-function.xml")
            .WithBindMount(Path.Combine(clickhouseAssets, "config.d", "cluster.xml"), "/etc/clickhouse-server/config.d/cluster.xml")
            .WithBindMount(Path.Combine(clickhouseAssets, "user_scripts"), "/var/lib/clickhouse/user_scripts")
            .WithVolume($"{name}-clickhouse-data", "/var/lib/clickhouse")
            .WithHttpEndpoint(targetPort: 8123, name: "http")
            .WithHttpHealthCheck("/ping", endpointName: "http", statusCode: 200)
            .WaitFor(zookeeper);

        var signozUi = builder.AddContainer(signozUiName, SignozContainerImageTags.SignozImage, SignozContainerImageTags.SignozTag)
            .WithImageRegistry(SignozContainerImageTags.Registry)
            .WithEnvironment("SIGNOZ_ALERTMANAGER_PROVIDER", "signoz")
            .WithEnvironment("SIGNOZ_TELEMETRYSTORE_CLICKHOUSE_DSN", $"tcp://{clickhouseName}:9000")
            .WithEnvironment("SIGNOZ_SQLSTORE_SQLITE_PATH", "/var/lib/signoz/signoz.db")
            .WithEnvironment("SIGNOZ_TOKENIZER_JWT_SECRET", "secret")
            .WithVolume($"{name}-sqlite-data", "/var/lib/signoz")
            .WithHttpEndpoint(targetPort: SignozContainerResource.UiPort, port: httpPort, name: SignozContainerResource.UiEndpointName)
            .WithHttpHealthCheck("/api/v1/health", endpointName: SignozContainerResource.UiEndpointName, statusCode: 200)
            .WaitFor(clickhouse);

        var migrator = builder.AddContainer(migratorName, SignozContainerImageTags.OtelCollectorImage, SignozContainerImageTags.OtelCollectorTag)
            .WithImageRegistry(SignozContainerImageTags.Registry)
            .WithEnvironment(ClickHouseDsnEnvVar, $"tcp://{clickhouseName}:9000")
            .WithEnvironment(ClickHouseClusterEnvVar, "cluster")
            .WithEnvironment(ClickHouseReplicationEnvVar, "true")
            .WithEnvironment(ClickHouseTimeoutEnvVar, "10m")
            .WithEntrypoint("/bin/sh")
            .WithArgs(
                "-c",
                "/signoz-otel-collector migrate bootstrap && /signoz-otel-collector migrate sync up && /signoz-otel-collector migrate async up")
            .WaitFor(clickhouse);

        var collector = new SignozContainerResource(name)
        {
            UiEndpointReference = new EndpointReference(signozUi.Resource, SignozContainerResource.UiEndpointName),
        };

        var collectorBuilder = builder.AddResource(collector)
            .WithImage(SignozContainerImageTags.OtelCollectorImage, SignozContainerImageTags.OtelCollectorTag)
            .WithImageRegistry(SignozContainerImageTags.Registry)
            .WithEndpoint(
                targetPort: SignozContainerResource.OtlpGrpcPort,
                port: otlpGrpcPort,
                name: SignozContainerResource.OtlpGrpcEndpointName,
                scheme: "http")
            .WithEndpoint(
                targetPort: SignozContainerResource.OtlpHttpPort,
                port: otlpHttpPort,
                name: SignozContainerResource.OtlpHttpEndpointName,
                scheme: "http")
            .WithEndpoint(
                targetPort: SignozContainerResource.HealthPort,
                name: SignozContainerResource.HealthEndpointName,
                scheme: "http")
            .WithBindMount(otelConfigPath, "/etc/otel-collector-config.yaml")
            .WithBindMount(opampConfigPath, "/etc/manager-config.yaml")
            .WithEnvironment(ClickHouseHostEnvVar, clickhouseName)
            .WithEnvironment(SignozHostEnvVar, signozUiName)
            .WithEnvironment(ClickHouseDsnEnvVar, $"tcp://{clickhouseName}:9000")
            .WithEnvironment(ClickHouseClusterEnvVar, "cluster")
            .WithEnvironment(ClickHouseReplicationEnvVar, "true")
            .WithEnvironment(ClickHouseTimeoutEnvVar, "10m")
            .WithEnvironment(LowCardinalityEnvVar, "false")
            .WithEnvironment("OTEL_RESOURCE_ATTRIBUTES", "host.name=signoz-host,os.type=linux")
            .WithEntrypoint("/bin/sh")
            .WithArgs(
                "-c",
                "/signoz-otel-collector migrate sync check && /signoz-otel-collector --config=/etc/otel-collector-config.yaml --manager-config=/etc/manager-config.yaml --copy-path=/var/tmp/collector-config.yaml")
            .WithHttpHealthCheck("/", endpointName: SignozContainerResource.HealthEndpointName, statusCode: 200)
            .WaitForCompletion(migrator)
            .WaitFor(clickhouse)
            .WaitFor(signozUi);

        return collectorBuilder;
    }

    /// <summary>
    /// Routes OpenTelemetry exporters on a project to the SigNoz OTLP gRPC endpoint.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithSignozOtlpExporter(
        this IResourceBuilder<ProjectResource> builder,
        IResourceBuilder<SignozContainerResource> signoz)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(signoz);

        return builder
            .WithReference(signoz)
            .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", signoz.Resource.OtlpGrpcUriExpression)
            .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc");
    }

}
