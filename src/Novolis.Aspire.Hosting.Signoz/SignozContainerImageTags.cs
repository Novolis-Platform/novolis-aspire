namespace Novolis.Aspire.Hosting.Signoz;

/// <summary>
/// Default container image references for the SigNoz stack.
/// </summary>
internal static class SignozContainerImageTags
{
    /// <summary>Docker registry host for all SigNoz images.</summary>
    public const string Registry = "docker.io";

    /// <summary>ZooKeeper image name.</summary>
    public const string ZookeeperImage = "signoz/zookeeper";

    /// <summary>ZooKeeper image tag.</summary>
    public const string ZookeeperTag = "3.7.1";

    /// <summary>ClickHouse server image name.</summary>
    public const string ClickHouseImage = "clickhouse/clickhouse-server";

    /// <summary>ClickHouse server image tag.</summary>
    public const string ClickHouseTag = "25.5.6";

    /// <summary>SigNoz UI image name.</summary>
    public const string SignozImage = "signoz/signoz";

    /// <summary>SigNoz UI image tag.</summary>
    public const string SignozTag = "v0.124.0";

    /// <summary>SigNoz OpenTelemetry collector image name.</summary>
    public const string OtelCollectorImage = "signoz/signoz-otel-collector";

    /// <summary>SigNoz OpenTelemetry collector image tag.</summary>
    public const string OtelCollectorTag = "v0.144.4";
}
