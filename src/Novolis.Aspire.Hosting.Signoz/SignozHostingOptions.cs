using Aspire.Hosting.ApplicationModel;

namespace Novolis.Aspire.Hosting.Signoz;

/// <summary>Podman/container hosting options for <c>AddSignoz</c>.</summary>
public sealed class SignozHostingOptions
{
    /// <summary>Lifetime for ZooKeeper, ClickHouse, SigNoz UI, and the OTLP collector.</summary>
    public ContainerLifetime Lifetime { get; init; } = ContainerLifetime.Session;

    /// <summary>Lifetime for the schema migrator (typically session-scoped, one-shot).</summary>
    public ContainerLifetime MigratorLifetime { get; init; } = ContainerLifetime.Session;

    /// <summary>Fixed Podman container name for ZooKeeper (optional).</summary>
    public string? ZookeeperContainerName { get; init; }

    /// <summary>Fixed Podman container name for ClickHouse (optional).</summary>
    public string? ClickHouseContainerName { get; init; }

    /// <summary>Fixed Podman container name for the SigNoz UI (optional).</summary>
    public string? SignozUiContainerName { get; init; }

    /// <summary>Fixed Podman container name for the schema migrator (optional).</summary>
    public string? MigratorContainerName { get; init; }

    /// <summary>Fixed Podman container name for the OTLP collector (optional).</summary>
    public string? CollectorContainerName { get; init; }
}
