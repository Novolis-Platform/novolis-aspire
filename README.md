# novolis-aspire

Aspire hosting integrations for the Novolis platform.

## Packages

| Package | Description |
| --- | --- |
| `Novolis.Aspire.Hosting.Signoz` | Local [SigNoz](https://signoz.io/) stack (ZooKeeper, ClickHouse, UI, OTLP collector) for AppHosts |

## SigNoz

Add the package to your AppHost:

```xml
<PackageReference Include="Novolis.Aspire.Hosting.Signoz" Version="0.1.0-preview.4" />
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

Open the SigNoz UI at the `signoz-signoz` HTTP endpoint after `aspire run` (container port `8080`).

### Troubleshooting

**Migrator fails with ClickHouse code 159** (`distributed_ddl_task_timeout`, host `clickhouse` **Inactive**):

- Use **preview.4+**, which substitutes `__CLICKHOUSE_HOST__` in `cluster.xml` (Aspire DNS name `{name}-clickhouse`, not Docker Compose’s `clickhouse`).
- After a failed bootstrap, clear stale ZooKeeper DDL nodes, then restart the migrator:

```bash
podman exec <zookeeper-container> /opt/bitnami/zookeeper/bin/zkCli.sh -server localhost:2181 deleteall /clickhouse/task_queue/ddl/query-0000000000
# repeat for other query-* entries listed under /clickhouse/task_queue/ddl
```

**Collector stays Waiting:** the OTLP collector `WaitForCompletion(migrator)`. First bootstrap can take several minutes; check `aspire logs signoz-migrator`.

**No traces in SigNoz but dashboard works:** wire **dual OTLP** in app code — `WithSignozOtlpExporter` only sets `SIGNOZ_OTEL_EXPORTER_OTLP_*`; apps need named `AddOtlpExporter("signoz")` when that env is set (do not replace `OTEL_EXPORTER_OTLP_ENDPOINT`).
