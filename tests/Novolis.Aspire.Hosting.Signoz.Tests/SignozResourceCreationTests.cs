using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Aspire.Hosting.Signoz;
using TUnit.Core;

namespace Novolis.Aspire.Hosting.Signoz.Tests;

public sealed class SignozResourceCreationTests
{
    [Test]
    public async Task AddSignoz_null_builder_throws()
    {
        IDistributedApplicationBuilder builder = null!;
        await Assert.That(() => builder.AddSignoz("signoz")).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task AddSignoz_empty_name_throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        await Assert.That(() => builder.AddSignoz("  ")).Throws<ArgumentException>();
    }

    [Test]
    public async Task AddSignoz_registers_collector_and_stack_containers()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddSignoz("telemetry");

        await using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var collector = appModel.Resources.OfType<SignozContainerResource>().SingleOrDefault();
        await Assert.That(collector).IsNotNull();
        await Assert.That(collector!.Name).IsEqualTo("telemetry");

        var containerNames = appModel.Resources.OfType<ContainerResource>()
            .Select(resource => resource.Name)
            .ToArray();
        await Assert.That(containerNames).IsEquivalentTo(
        [
            "telemetry-zookeeper-1",
            "telemetry-clickhouse",
            "telemetry-signoz",
            "telemetry-migrator",
            "telemetry",
        ]);

        await Assert.That(collector.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotation)).IsTrue();
        await Assert.That(imageAnnotation!.Image).IsEqualTo(SignozContainerImageTags.OtelCollectorImage);
        await Assert.That(imageAnnotation.Tag).IsEqualTo(SignozContainerImageTags.OtelCollectorTag);
        await Assert.That(imageAnnotation.Registry).IsEqualTo(SignozContainerImageTags.Registry);
    }

    [Test]
    public async Task PrepareStackAssets_substitutes_host_placeholders()
    {
        var assetsPath = SignozAssetProvisioner.PrepareStackAssets(
            "demo",
            zookeeperHost: "demo-zookeeper-1",
            clickhouseHost: "demo-clickhouse",
            signozHost: "demo-signoz");

        var clusterXml = await File.ReadAllTextAsync(Path.Combine(assetsPath, "clickhouse", "config.d", "cluster.xml"));
        await Assert.That(clusterXml).Contains("<host>demo-zookeeper-1</host>");

        var otelConfig = await File.ReadAllTextAsync(Path.Combine(assetsPath, "otel-collector-config.yaml"));
        await Assert.That(otelConfig).Contains("${env:CLICKHOUSE_HOST}");

        var opampConfig = await File.ReadAllTextAsync(Path.Combine(assetsPath, "otel-collector-opamp-config.yaml"));
        await Assert.That(opampConfig).Contains("${env:SIGNOZ_HOST}");
    }
}
