# Getting started

Add **Novolis.Aspire.Hosting.Signoz** to an Aspire AppHost to run SigNoz alongside your services.

## Prerequisites

- .NET 10 SDK and Aspire workload
- Docker or Podman

## Package reference

```bash
dotnet add package Novolis.Aspire.Hosting.Signoz
```

## AppHost example

```csharp
var builder = DistributedApplication.CreateBuilder(args);
var signoz = builder.AddSignoz("signoz", httpPort: 8080);
builder.AddProject<Projects.Api>("api").WithSignozOtlpExporter(signoz);
builder.Build().Run();
```

Open the SigNoz UI at the mapped HTTP port (default container port 8080).

## See also

- [Design](design.md)
- [Release](release.md)
