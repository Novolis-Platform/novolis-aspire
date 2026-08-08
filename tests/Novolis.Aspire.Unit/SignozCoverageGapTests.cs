using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Aspire.Hosting.Signoz;
using TUnit.Core;

namespace Novolis.Aspire.Hosting.Signoz.Tests;

/// <summary>
/// Asset-root mutations must not run alongside other Signoz tests that read AppContext assets/.
/// </summary>
[NotInParallel("SignozAssets")]
public sealed class SignozCoverageGapTests
{
    [Test]
    public async Task AddSignoz_with_fixed_container_names_builds()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSignoz(
            "named-stack",
            options: new SignozHostingOptions
            {
                ZookeeperContainerName = "zk-fixed",
                ClickHouseContainerName = "ch-fixed",
                SignozUiContainerName = "ui-fixed",
                MigratorContainerName = "mig-fixed",
                CollectorContainerName = "col-fixed",
            });

        await using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var containers = appModel.Resources.OfType<ContainerResource>().ToArray();
        await Assert.That(containers.Length).IsEqualTo(5);
        await Assert.That(appModel.Resources.OfType<SignozContainerResource>().Single().Name)
            .IsEqualTo("named-stack");
    }

    [Test]
    public async Task PrepareStackAssets_missing_assets_throws()
    {
        EnsureAssetsPresent();
        var assetsRoot = Path.Combine(AppContext.BaseDirectory, SignozAssetProvisioner.AssetsDirectoryName);
        var contentFiles = Path.Combine(AppContext.BaseDirectory, "contentFiles");
        var assetsBackup = assetsRoot + ".bak-" + Guid.NewGuid().ToString("N");
        var contentBackup = contentFiles + ".bak-" + Guid.NewGuid().ToString("N");
        var movedAssets = false;
        var movedContent = false;
        try
        {
            Directory.Move(assetsRoot, assetsBackup);
            movedAssets = true;

            if (Directory.Exists(contentFiles))
            {
                Directory.Move(contentFiles, contentBackup);
                movedContent = true;
            }

            await Assert.That(() => SignozAssetProvisioner.PrepareStackAssets("missing", "z", "c", "s"))
                .Throws<DirectoryNotFoundException>();
        }
        finally
        {
            if (movedAssets && Directory.Exists(assetsBackup) && !Directory.Exists(assetsRoot))
                Directory.Move(assetsBackup, assetsRoot);
            if (movedContent && Directory.Exists(contentBackup) && !Directory.Exists(contentFiles))
                Directory.Move(contentBackup, contentFiles);
        }
    }

    [Test]
    public async Task PrepareStackAssets_copies_binary_and_reuses_target()
    {
        EnsureAssetsPresent();
        var first = SignozAssetProvisioner.PrepareStackAssets(
            "bin-demo",
            zookeeperHost: "z",
            clickhouseHost: "c",
            signozHost: "s");
        var copied = Path.Combine(first, "clickhouse", "user_scripts", "marker.bin");
        await Assert.That(File.Exists(copied)).IsTrue();
        await Assert.That(await File.ReadAllBytesAsync(copied)).IsEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        var second = SignozAssetProvisioner.PrepareStackAssets(
            "bin-demo",
            zookeeperHost: "z2",
            clickhouseHost: "c2",
            signozHost: "s2");
        await Assert.That(second).IsEqualTo(first);
        await Assert.That(File.Exists(copied)).IsTrue();
    }

    [Test]
    public async Task SignozContainerResource_UiEndpoint_without_reference_throws()
    {
        var orphan = new SignozContainerResource("orphan");
        await Assert.That(() => _ = orphan.UiEndpoint).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task SignozContainerResource_connection_string_and_properties_resolve()
    {
        var builder = DistributedApplication.CreateBuilder();
        var signoz = builder.AddSignoz("props-stack");
        await using var app = builder.Build();

        var resource = signoz.Resource;
        _ = resource.OtlpGrpcEndpoint;
        _ = resource.OtlpHttpEndpoint;
        _ = resource.UiEndpoint;
        _ = resource.ConnectionStringExpression;
        _ = resource.OtlpGrpcUriExpression;
        _ = resource.OtlpHttpUriExpression;
        _ = resource.UiUriExpression;

        var props = ((IResourceWithConnectionString)resource).GetConnectionProperties().ToArray();
        await Assert.That(props.Select(p => p.Key)).IsEquivalentTo(
        [
            "OtlpGrpcEndpoint",
            "OtlpHttpEndpoint",
            "Ui",
            "Host",
            "Port",
        ]);
    }

    [Test]
    public async Task PrepareStackAssets_falls_back_to_contentFiles_layout()
    {
        EnsureAssetsPresent();
        var assetsRoot = Path.Combine(AppContext.BaseDirectory, SignozAssetProvisioner.AssetsDirectoryName);
        var assetsBackup = assetsRoot + ".bak-" + Guid.NewGuid().ToString("N");
        var contentAssets = Path.Combine(AppContext.BaseDirectory, "contentFiles", "any", "any", SignozAssetProvisioner.AssetsDirectoryName);
        var moved = false;
        try
        {
            Directory.Move(assetsRoot, assetsBackup);
            moved = true;

            Directory.CreateDirectory(contentAssets);
            await File.WriteAllTextAsync(Path.Combine(contentAssets, "probe.yaml"), "host: __SIGNOZ_HOST__");

            var prepared = SignozAssetProvisioner.PrepareStackAssets("cf", "z", "c", "signoz-host");
            var probe = await File.ReadAllTextAsync(Path.Combine(prepared, "probe.yaml"));
            await Assert.That(probe).Contains("signoz-host");
        }
        finally
        {
            var contentRoot = Path.Combine(AppContext.BaseDirectory, "contentFiles");
            if (Directory.Exists(contentRoot))
                Directory.Delete(contentRoot, recursive: true);
            if (moved && Directory.Exists(assetsBackup) && !Directory.Exists(assetsRoot))
                Directory.Move(assetsBackup, assetsRoot);
        }
    }

    [Test]
    public async Task PrepareStackAssets_null_or_whitespace_args_throw()
    {
        await Assert.That(() => SignozAssetProvisioner.PrepareStackAssets(" ", "z", "c", "s"))
            .Throws<ArgumentException>();
        await Assert.That(() => SignozAssetProvisioner.PrepareStackAssets("n", " ", "c", "s"))
            .Throws<ArgumentException>();
        await Assert.That(() => SignozAssetProvisioner.PrepareStackAssets("n", "z", "", "s"))
            .Throws<ArgumentException>();
        await Assert.That(() => SignozAssetProvisioner.PrepareStackAssets("n", "z", "c", "\t"))
            .Throws<ArgumentException>();
    }

    private static void EnsureAssetsPresent()
    {
        var assetsRoot = Path.Combine(AppContext.BaseDirectory, SignozAssetProvisioner.AssetsDirectoryName);
        if (Directory.Exists(assetsRoot))
            return;

        var bak = Directory.GetDirectories(AppContext.BaseDirectory, "assets.bak-*")
            .OrderByDescending(d => d)
            .FirstOrDefault();
        if (bak is not null)
            Directory.Move(bak, assetsRoot);
    }
}
