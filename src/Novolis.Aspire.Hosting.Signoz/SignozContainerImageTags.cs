namespace Novolis.Aspire.Hosting.Signoz;

internal static class SignozContainerImageTags
{
    public const string Registry = "docker.io";

    public const string ZookeeperImage = "signoz/zookeeper";
    public const string ZookeeperTag = "3.7.1";

    public const string ClickHouseImage = "clickhouse/clickhouse-server";
    public const string ClickHouseTag = "25.5.6";

    public const string SignozImage = "signoz/signoz";
    public const string SignozTag = "v0.124.0";

    public const string OtelCollectorImage = "signoz/signoz-otel-collector";
    public const string OtelCollectorTag = "v0.144.4";
}
