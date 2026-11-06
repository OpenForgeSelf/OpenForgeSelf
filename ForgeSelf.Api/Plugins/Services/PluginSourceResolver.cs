using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins;

namespace ForgeSelf.Api.Plugins.Services;

/// <summary>GitHub Release 与 NuGet 都进同一个安装器。默认只暂存；<paramref name="activate"/> 为真才切 current。</summary>
public sealed class PluginSourceResolver
{
    private readonly PluginInstallerService _installer;
    private readonly PluginVersionService _versions;
    private readonly PluginManager _manager;
    private readonly PluginGitHubProvider _github;
    private readonly PluginNuGetProvider _nuget;

    public PluginSourceResolver(
        PluginInstallerService installer,
        PluginVersionService versions,
        PluginManager manager,
        PluginGitHubProvider github,
        PluginNuGetProvider nuget)
    {
        _installer = installer;
        _versions = versions;
        _manager = manager;
        _github = github;
        _nuget = nuget;
    }

    public async Task<PluginMetadata> InstallAsync(string kind, string locator, bool activate, CancellationToken cancellationToken)
    {
        var normalized = (kind ?? "").Trim().ToLowerInvariant();
        var payload = normalized switch
        {
            "github" => await _github.DownloadLatestAsync(locator, cancellationToken),
            "nuget" => await _nuget.DownloadLatestAsync(locator, cancellationToken),
            _ => throw new InvalidDataException("kind 只接受 github 或 nuget")
        };

        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + Path.GetExtension(payload.FileName));
        await File.WriteAllBytesAsync(temp, payload.Bytes, cancellationToken);
        try
        {
            var staged = _installer.StageFromPackage(temp)
                ?? throw new InvalidOperationException("暂存失败");
            var alreadyLoaded = _manager.GetPluginMetadata(staged.Id) != null;
            if (activate)
            {
                if (alreadyLoaded)
                {
                    if (!_versions.Activate(staged.Id, staged.Version))
                        throw new InvalidOperationException("激活失败，已尝试回滚");
                }
                else if (PluginVersionLayout.ReadCurrentVersion(staged.PluginDirectory) == null)
                {
                    PluginVersionLayout.WriteCurrentVersion(staged.PluginDirectory, staged.Version);
                }

                WriteConsent(staged.PluginDirectory, normalized, locator, staged.Version);
            }

            return staged;
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    private static void WriteConsent(string pluginDir, string kind, string locator, string version)
    {
        PluginVersionLayout.EnsureLayout(pluginDir);
        var path = Path.Combine(PluginVersionLayout.ForgeDirectory(pluginDir), "activation-consent.json");
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            at = DateTimeOffset.Now,
            kind,
            locator,
            version
        });
        File.WriteAllText(path, json);
    }
}
