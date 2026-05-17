using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Aspire.Hosting.Signoz;

namespace Novolis.Aspire.Hosting.Signoz.Tests;

public sealed class SignozResourceCreationTests
{
    [Test]
    public void AddSignoz_null_builder_throws()
    {
        IDistributedApplicationBuilder builder = null!;
        var act = () => builder.AddSignoz("signoz");
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void AddSignoz_empty_name_throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var act = () => builder.AddSignoz("  ");
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public async Task AddSignoz_registers_collector_and_stack_containers()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddSignoz("telemetry");

        await using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var collector = appModel.Resources.OfType<SignozContainerResource>().SingleOrDefault();
        collector.Should().NotBeNull();
        collector!.Name.Should().Be("telemetry");

        appModel.Resources.OfType<ContainerResource>()
            .Select(resource => resource.Name)
            .Should()
            .BeEquivalentTo(
            [
                "telemetry-zookeeper-1",
                "telemetry-clickhouse",
                "telemetry-signoz",
                "telemetry-migrator",
                "telemetry",
            ]);

        collector.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotation).Should().BeTrue();
        imageAnnotation!.Image.Should().Be(SignozContainerImageTags.OtelCollectorImage);
        imageAnnotation.Tag.Should().Be(SignozContainerImageTags.OtelCollectorTag);
        imageAnnotation.Registry.Should().Be(SignozContainerImageTags.Registry);
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
        clusterXml.Should().Contain("<host>demo-zookeeper-1</host>");

        var otelConfig = await File.ReadAllTextAsync(Path.Combine(assetsPath, "otel-collector-config.yaml"));
        otelConfig.Should().Contain("${env:CLICKHOUSE_HOST}");

        var opampConfig = await File.ReadAllTextAsync(Path.Combine(assetsPath, "otel-collector-opamp-config.yaml"));
        opampConfig.Should().Contain("${env:SIGNOZ_HOST}");
    }
}
