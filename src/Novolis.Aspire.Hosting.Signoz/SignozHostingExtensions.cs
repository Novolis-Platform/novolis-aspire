using Aspire.Hosting;
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

    /// <summary>Secondary OTLP endpoint for SigNoz; does not replace Aspire dashboard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>.</summary>
    public const string SignozOtelExporterOtlpEndpointVar = "SIGNOZ_OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>OTLP protocol for <see cref="SignozOtelExporterOtlpEndpointVar"/> (e.g. <c>grpc</c>).</summary>
    public const string SignozOtelExporterOtlpProtocolVar = "SIGNOZ_OTEL_EXPORTER_OTLP_PROTOCOL";

    /// <summary>
    /// Adds a SigNoz observability stack (ZooKeeper, ClickHouse, SigNoz UI, schema migrator, and OTLP collector).
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">Aspire resource name for the OTLP collector and connection string.</param>
    /// <param name="httpPort">Optional host port for the SigNoz UI (container port 8080).</param>
    /// <param name="otlpGrpcPort">Optional host port for OTLP gRPC ingestion (container port 4317).</param>
    /// <param name="otlpHttpPort">Optional host port for OTLP HTTP ingestion (container port 4318).</param>
    /// <param name="options">Optional Podman lifetime and fixed container names (see Garnet/Raven-style persistent dev stacks).</param>
    public static IResourceBuilder<SignozContainerResource> AddSignoz(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? httpPort = null,
        int? otlpGrpcPort = null,
        int? otlpHttpPort = null,
        SignozHostingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        options ??= new SignozHostingOptions();

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

        var zookeeper = ConfigureContainer(
                builder.AddContainer(zookeeperName, SignozContainerImageTags.ZookeeperImage, SignozContainerImageTags.ZookeeperTag),
                options.Lifetime,
                options.ZookeeperContainerName)
            .WithImageRegistry(SignozContainerImageTags.Registry)
            .WithEnvironment("ZOO_SERVER_ID", "1")
            .WithEnvironment("ALLOW_ANONYMOUS_LOGIN", "yes")
            .WithEnvironment("ZOO_AUTOPURGE_INTERVAL", "1")
            .WithEnvironment("ZOO_ENABLE_PROMETHEUS_METRICS", "yes")
            .WithEnvironment("ZOO_PROMETHEUS_METRICS_PORT_NUMBER", "9141")
            .WithVolume($"{name}-zookeeper-data", "/bitnami/zookeeper")
            .WithHttpEndpoint(targetPort: 8080, name: "http")
            .WithHttpHealthCheck("/commands/ruok", endpointName: "http", statusCode: 200);

        var clickhouse = ConfigureContainer(
                builder.AddContainer(clickhouseName, SignozContainerImageTags.ClickHouseImage, SignozContainerImageTags.ClickHouseTag),
                options.Lifetime,
                options.ClickHouseContainerName)
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

        var signozUi = ConfigureContainer(
                builder.AddContainer(signozUiName, SignozContainerImageTags.SignozImage, SignozContainerImageTags.SignozTag),
                options.Lifetime,
                options.SignozUiContainerName)
            .WithImageRegistry(SignozContainerImageTags.Registry)
            .WithEnvironment("SIGNOZ_ALERTMANAGER_PROVIDER", "signoz")
            .WithEnvironment("SIGNOZ_TELEMETRYSTORE_CLICKHOUSE_DSN", $"tcp://{clickhouseName}:9000")
            .WithEnvironment("SIGNOZ_SQLSTORE_SQLITE_PATH", "/var/lib/signoz/signoz.db")
            .WithEnvironment("SIGNOZ_TOKENIZER_JWT_SECRET", "secret")
            .WithVolume($"{name}-sqlite-data", "/var/lib/signoz")
            .WithHttpEndpoint(targetPort: SignozContainerResource.UiPort, port: httpPort, name: SignozContainerResource.UiEndpointName)
            .WithHttpHealthCheck("/api/v1/health", endpointName: SignozContainerResource.UiEndpointName, statusCode: 200)
            .WaitFor(clickhouse);

        var migrator = ConfigureContainer(
                builder.AddContainer(migratorName, SignozContainerImageTags.OtelCollectorImage, SignozContainerImageTags.OtelCollectorTag),
                options.MigratorLifetime,
                options.MigratorContainerName)
            .WithImageRegistry(SignozContainerImageTags.Registry)
            .WithEnvironment(ClickHouseDsnEnvVar, $"tcp://{clickhouseName}:9000")
            .WithEnvironment(ClickHouseClusterEnvVar, "cluster")
            .WithEnvironment(ClickHouseReplicationEnvVar, "false")
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

        var collectorBuilder = ConfigureContainer(
                builder.AddResource(collector),
                options.Lifetime,
                options.CollectorContainerName)
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
            .WithEnvironment(ClickHouseReplicationEnvVar, "false")
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
    /// Adds SigNoz OTLP env vars for a secondary exporter. Does not set <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>
    /// so Aspire dashboard OTLP remains the default sink; apps should also export using
    /// <c>SIGNOZ_OTEL_EXPORTER_OTLP_ENDPOINT</c> / <c>SIGNOZ_OTEL_EXPORTER_OTLP_PROTOCOL</c>.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithSignozOtlpExporter(
        this IResourceBuilder<ProjectResource> builder,
        IResourceBuilder<SignozContainerResource> signoz)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(signoz);

        return builder
            .WithReference(signoz)
            .WithEnvironment(SignozOtelExporterOtlpEndpointVar, signoz.Resource.OtlpGrpcUriExpression)
            .WithEnvironment(SignozOtelExporterOtlpProtocolVar, "grpc");
    }

    private static IResourceBuilder<T> ConfigureContainer<T>(
        IResourceBuilder<T> container,
        ContainerLifetime lifetime,
        string? containerName)
        where T : ContainerResource
    {
        container = container.WithLifetime(lifetime);
        if (!string.IsNullOrWhiteSpace(containerName))
            container = container.WithContainerName(containerName);

        return container;
    }

}
