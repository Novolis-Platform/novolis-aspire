# Design

## Package

**Novolis.Aspire.Hosting.Signoz** extends `IDistributedApplicationBuilder` with `AddSignoz` and wires OTLP export env vars via `WithSignozOtlpExporter`.

## Stack topology

```
ZooKeeper → ClickHouse → SigNoz UI
                      ↘ schema migrator → OTLP collector
```

Container images and tags are centralized in `SignozContainerImageTags`. Config assets are copied from packaged `assets/` and token-replaced for host names.

## OTLP routing

`WithSignozOtlpExporter` sets `SIGNOZ_OTEL_EXPORTER_OTLP_*` variables so applications can dual-export without overriding Aspire dashboard `OTEL_EXPORTER_OTLP_ENDPOINT`.

## Options

`SignozHostingOptions` controls container lifetime and optional fixed container names for persistent Podman dev environments.
