namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// SigNoz OpenTelemetry collector resource. Use <see cref="SignozHostingExtensions.AddSignoz"/> to provision the full stack.
/// </summary>
/// <param name="name">Aspire resource name for the collector.</param>
public sealed class SignozContainerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal EndpointReference? UiEndpointReference { get; init; }

    internal const int OtlpGrpcPort = 4317;
    internal const int OtlpHttpPort = 4318;
    internal const int UiPort = 8080;
    internal const int HealthPort = 13133;

    internal const string OtlpGrpcEndpointName = "otlp-grpc";
    internal const string OtlpHttpEndpointName = "otlp-http";
    internal const string UiEndpointName = "http";
    internal const string HealthEndpointName = "health";

    private EndpointReference? _otlpGrpcEndpoint;
    private EndpointReference? _otlpHttpEndpoint;
    /// <summary>OTLP gRPC endpoint (port 4317).</summary>
    public EndpointReference OtlpGrpcEndpoint =>
        _otlpGrpcEndpoint ??= new EndpointReference(this, OtlpGrpcEndpointName);

    /// <summary>OTLP HTTP endpoint (port 4318).</summary>
    public EndpointReference OtlpHttpEndpoint =>
        _otlpHttpEndpoint ??= new EndpointReference(this, OtlpHttpEndpointName);

    /// <summary>SigNoz UI HTTP endpoint (port 8080) on the companion UI container.</summary>
    public EndpointReference UiEndpoint =>
        UiEndpointReference
        ?? throw new InvalidOperationException(
            "The SigNoz UI endpoint is not configured. Use AddSignoz to provision the stack.");

    /// <summary>OTLP gRPC host.</summary>
    public EndpointReferenceExpression OtlpGrpcHost =>
        OtlpGrpcEndpoint.Property(EndpointProperty.Host);

    /// <summary>OTLP gRPC port.</summary>
    public EndpointReferenceExpression OtlpGrpcPortExpression =>
        OtlpGrpcEndpoint.Property(EndpointProperty.Port);

    /// <summary>OTLP HTTP host.</summary>
    public EndpointReferenceExpression OtlpHttpHost =>
        OtlpHttpEndpoint.Property(EndpointProperty.Host);

    /// <summary>OTLP HTTP port.</summary>
    public EndpointReferenceExpression OtlpHttpPortExpression =>
        OtlpHttpEndpoint.Property(EndpointProperty.Port);

    /// <summary>SigNoz UI host.</summary>
    public EndpointReferenceExpression UiHost =>
        UiEndpoint.Property(EndpointProperty.Host);

    /// <summary>SigNoz UI port.</summary>
    public EndpointReferenceExpression UiPortExpression =>
        UiEndpoint.Property(EndpointProperty.Port);

    /// <summary>Connection string for OTLP over gRPC in the form <c>http://host:4317</c>.</summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"Endpoint=http://{OtlpGrpcHost}:{OtlpGrpcPortExpression};Protocol=grpc");

    /// <summary>OTLP gRPC URI for exporters.</summary>
    public ReferenceExpression OtlpGrpcUriExpression =>
        ReferenceExpression.Create($"http://{OtlpGrpcHost}:{OtlpGrpcPortExpression}");

    /// <summary>OTLP HTTP URI for exporters.</summary>
    public ReferenceExpression OtlpHttpUriExpression =>
        ReferenceExpression.Create($"http://{OtlpHttpHost}:{OtlpHttpPortExpression}");

    /// <summary>SigNoz UI URI.</summary>
    public ReferenceExpression UiUriExpression =>
        ReferenceExpression.Create($"http://{UiHost}:{UiPortExpression}");

    /// <inheritdoc />
    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("OtlpGrpcEndpoint", OtlpGrpcUriExpression);
        yield return new("OtlpHttpEndpoint", OtlpHttpUriExpression);
        yield return new("Ui", UiUriExpression);
        yield return new("Host", ReferenceExpression.Create($"{OtlpGrpcHost}"));
        yield return new("Port", ReferenceExpression.Create($"{OtlpGrpcPortExpression}"));
    }
}
