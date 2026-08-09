<!-- novolis-marketing:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-brand-transparent.svg" width="360" alt="Novolis"/>
  </a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/banners/novolis-aspire.svg" width="100%" alt="novolis-aspire"/>
</p>

<p align="center">
  <strong>Aspire hosting extensions</strong><br/>
  Aspire hosting helpers (e.g. Signoz) for Novolis distributed apps.
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-aspire/"><img src="https://img.shields.io/badge/docs-portfolio-0a7ea3" alt="docs"/></a>
  <a href="https://github.com/Novolis-Platform/novolis-aspire/actions"><img src="https://img.shields.io/github/actions/workflow/status/Novolis-Platform/novolis-aspire/merge.yml?branch=main&label=merge&logo=github" alt="merge"/></a>
  <a href="https://github.com/orgs/Novolis-Platform/packages?repo_name=novolis-aspire"><img src="https://img.shields.io/badge/packages-GitHub%20Packages-0a7ea3?logo=nuget" alt="packages"/></a>
  <a href="https://github.com/Novolis-Platform"><img src="https://img.shields.io/badge/org-Novolis--Platform-111827" alt="org"/></a>
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-aspire/">Docs</a>
  ·
  <a href="https://nuget.pkg.github.com/Novolis-Platform/index.json"><code>https://nuget.pkg.github.com/Novolis-Platform/index.json</code></a>
  ·
  <a href="https://github.com/Novolis-Platform/.github/blob/main/profile/README.md">Org landing</a>
  ·
  <a href="https://github.com/Novolis-Platform/novolis-governance">Governance</a>
</p>

---
<!-- novolis-marketing:end -->
<!-- novolis-package-index:start -->
> **GitHub Packages shows this repository README on every package page** (upstream limitation).
> Open the **package README** for install and quick start — embedded in each .nupkg and linked below.

## Published packages

| Package | Install | Package README |
|---------|---------|----------------|
| `Novolis.Aspire.Hosting.Signoz` | `dotnet add package Novolis.Aspire.Hosting.Signoz` | [README](https://github.com/Novolis-Platform/novolis-aspire/blob/main/src/Novolis.Aspire.Hosting.Signoz/README.md) |

For NuGet.org and Visual Studio, the **embedded** README.md inside each package is authoritative.

<!-- novolis-package-index:end -->
# novolis-aspire

Aspire hosting integrations for the Novolis platform.

## Packages

| Package | Description |
| --- | --- |
| [Novolis.Aspire.Hosting.Signoz](src/Novolis.Aspire.Hosting.Signoz/README.md) | Local [SigNoz](https://signoz.io/) stack (ZooKeeper, ClickHouse, UI, OTLP collector) for AppHosts |

Restore from **GitHub Packages** (`2026.1.*`) and **nuget.org**. Use **`Novolis.Platform.slnx`** for local ProjectReference iteration on Aspire-related platform libs.

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

