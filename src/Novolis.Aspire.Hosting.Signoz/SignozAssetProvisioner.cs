namespace Novolis.Aspire.Hosting.Signoz;

internal static class SignozAssetProvisioner
{
    internal const string AssetsDirectoryName = "assets";

    internal static string PrepareStackAssets(string stackName, string zookeeperHost, string clickhouseHost, string signozHost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stackName);
        ArgumentException.ThrowIfNullOrWhiteSpace(zookeeperHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(clickhouseHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(signozHost);

        var sourceRoot = ResolvePackagedAssetsRoot();
        var targetRoot = Path.Combine(Path.GetTempPath(), "novolis-signoz", stackName);
        if (Directory.Exists(targetRoot))
        {
            Directory.Delete(targetRoot, recursive: true);
        }

        CopyDirectory(sourceRoot, targetRoot, file =>
        {
            return file
                .Replace("__ZOOKEEPER_HOST__", zookeeperHost, StringComparison.Ordinal)
                .Replace("__CLICKHOUSE_HOST__", clickhouseHost, StringComparison.Ordinal)
                .Replace("__SIGNOZ_HOST__", signozHost, StringComparison.Ordinal);
        });

        return targetRoot;
    }

    private static string ResolvePackagedAssetsRoot()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, AssetsDirectoryName),
            Path.Combine(baseDirectory, "contentFiles", "any", "any", AssetsDirectoryName),
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"SigNoz asset files were not found. Expected '{AssetsDirectoryName}' under '{baseDirectory}'.");
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory, Func<string, string> transform)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(sourceDirectory, targetDirectory, StringComparison.Ordinal));
        }

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
            var targetFile = Path.Combine(targetDirectory, relativePath);
            var directoryName = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrEmpty(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            var extension = Path.GetExtension(sourceFile);
            if (string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase))
            {
                var content = File.ReadAllText(sourceFile);
                File.WriteAllText(targetFile, transform(content));
            }
            else
            {
                File.Copy(sourceFile, targetFile, overwrite: true);
            }
        }
    }
}
