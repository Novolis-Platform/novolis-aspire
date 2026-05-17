# novolis-aspire

Aspire hosting integrations for the Novolis platform.

## Packages

| Package | Description |
| --- | --- |
| `Novolis.Aspire.Hosting.Signoz` | Local [SigNoz](https://signoz.io/) stack (ZooKeeper, ClickHouse, UI, OTLP collector) for AppHosts |

## SigNoz

Add the package to your AppHost:

```xml
<PackageReference Include="Novolis.Aspire.Hosting.Signoz" Version="0.1.0-preview.2" />
```

Provision the stack and export telemetry from a project:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var signoz = builder.AddSignoz("signoz");

var api = builder.AddProject<Projects.Api>("api")
    .WithSignozOtlpExporter(signoz);

builder.Build().Run();
```

`WithSignozOtlpExporter` sets `SIGNOZ_OTEL_EXPORTER_OTLP_ENDPOINT` and `SIGNOZ_OTEL_EXPORTER_OTLP_PROTOCOL` only. It does **not** override `OTEL_EXPORTER_OTLP_ENDPOINT`, so the Aspire dashboard keeps receiving telemetry when apps dual-export (dashboard + SigNoz).

`AddSignoz` starts the containers defined in the upstream SigNoz Docker deployment (pinned image tags). The collector exposes OTLP gRPC (`4317`) and HTTP (`4318`); the UI listens on port `8080`.

Pass `SignozHostingOptions` for `ContainerLifetime.Persistent` and fixed Podman names via `WithContainerName` (same pattern as Garnet/Raven in AppHosts).

Open the dashboard at the `signoz-signoz` HTTP endpoint after `aspire start`.
