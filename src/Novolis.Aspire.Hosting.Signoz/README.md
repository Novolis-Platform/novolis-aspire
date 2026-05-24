# Novolis.Aspire.Hosting.Signoz

Aspire hosting integration that provisions a local SigNoz observability stack (ClickHouse, ZooKeeper, UI, OTLP collector) for development.

## Install

```bash
dotnet add package Novolis.Aspire.Hosting.Signoz
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download), Aspire AppHost, container runtime (Docker/Podman).

## Quick start

```csharp
var builder = DistributedApplication.CreateBuilder(args);
var signoz = builder.AddSignoz("signoz");
builder.AddProject<Projects.MyApi>("api")
    .WithSignozOtlpExporter(signoz);
builder.Build().Run();
```

## Related packages

| Package | When to use |
|---------|-------------|
| *(Aspire SDK)* | Required AppHost and `Microsoft.Extensions.ServiceDiscovery` wiring |

## More documentation

- [Getting started](https://github.com/Novolis-Platform/novolis-aspire/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-aspire/blob/main/docs/design.md)

## Support

Targets local/dev stacks; container images and ports follow upstream SigNoz compose layouts.
