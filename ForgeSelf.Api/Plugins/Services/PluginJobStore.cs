using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins;

namespace ForgeSelf.Api.Plugins.Services;

/// <summary>把插件任务写到 <c>{插件目录}/.forge/jobs/{jobId}.json</c>。不写入密钥。</summary>
public sealed class PluginJobStore
{
    public string Write(string pluginDir, string action, bool ok, string? version, string? message)
    {
        var dir = PluginVersionLayout.JobsDirectory(pluginDir);
        Directory.CreateDirectory(dir);
        var id = Guid.NewGuid().ToString("N");
        var json = JsonSerializer.Serialize(new
        {
            jobId = id,
            action,
            ok,
            version,
            message,
            at = DateTimeOffset.Now
        }, ForgeJson.Default);
        File.WriteAllText(Path.Combine(dir, id + ".json"), json);
        return id;
    }
}
