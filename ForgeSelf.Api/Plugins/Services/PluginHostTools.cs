using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins;

namespace ForgeSelf.Api.Plugins.Services;

public sealed class PluginHostToolCatalog
{
    private readonly IDataLocationService _data;
    private readonly PluginInstallerService _installer;
    private readonly PluginVersionService _versions;
    private readonly PluginJobStore _jobs;
    private readonly PluginBuildService _build;
    private readonly PluginSourceResolver _sources;

    public PluginHostToolCatalog(
        IDataLocationService data,
        PluginInstallerService installer,
        PluginVersionService versions,
        PluginJobStore jobs,
        PluginBuildService build,
        PluginSourceResolver sources)
    {
        _data = data;
        _installer = installer;
        _versions = versions;
        _jobs = jobs;
        _build = build;
        _sources = sources;
    }

    public void Register(IToolRegistry registry)
    {
        registry.RegisterTool(Tool("plugin.scaffold", "在数据根 plugins/{id}/src 生成独立仓库骨架", ScaffoldAsync));
        registry.RegisterTool(Tool("plugin.source.clone", "把远程仓库克隆到该插件 src/", CloneAsync));
        registry.RegisterTool(Tool("plugin.source.sync", "在该插件 src/ 执行 git pull --ff-only", SyncAsync));
        registry.RegisterTool(Tool("plugin.build", "只在该插件 src/ 构建", (p, ct) => VerbAsync(p, "build", ct)));
        registry.RegisterTool(Tool("plugin.test", "只在该插件 src/ 测试", (p, ct) => VerbAsync(p, "test", ct)));
        registry.RegisterTool(Tool("plugin.package", "只在该插件 src/ 打包", (p, ct) => VerbAsync(p, "package", ct)));
        registry.RegisterTool(Tool("plugin.stage", "把本地 ZIP 或 DLL 暂存到 versions/，不激活", StageAsync));
        registry.RegisterTool(Tool("plugin.activate", "按指定发行串激活，失败回滚并写 job", ActivateAsync));
        registry.RegisterTool(Tool("plugin.rollback", "按指定发行串回滚", ActivateAsync));
        registry.RegisterTool(Tool("plugin.verify", "对隔离实例打一个需鉴权的只读请求", VerifyAsync));
        registry.RegisterTool(Tool("plugin.publish.github", "无授权或无 GITHUB_TOKEN 时失败，不打印密钥", (p, ct) => PublishAsync(p, "GITHUB_TOKEN", ct)));
        registry.RegisterTool(Tool("plugin.publish.nuget", "无授权或无 NUGET_API_KEY 时失败，不打印密钥", (p, ct) => PublishAsync(p, "NUGET_API_KEY", ct)));
    }

    private async Task<string> ScaffoldAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var id = Required(root, "pluginId");
        var name = Optional(root, "name") ?? id;
        var pluginDir = _data.GetPluginDataDirectory(id);
        PluginVersionLayout.EnsureLayout(pluginDir);
        var src = PluginVersionLayout.SourceDirectory(pluginDir);
        PluginSrcScaffold.Write(src, id, name);
        var job = _jobs.Write(pluginDir, "scaffold", true, null, src);
        return Ok(job, "骨架已写入 " + src);
    }

    private async Task<string> CloneAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var id = Required(root, "pluginId");
        var url = Required(root, "url");
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return Fail(null, "只接受 https 克隆地址");
        var pluginDir = _data.GetPluginDataDirectory(id);
        PluginVersionLayout.EnsureLayout(pluginDir);
        var src = PluginVersionLayout.SourceDirectory(pluginDir);
        if (Directory.Exists(src) && Directory.EnumerateFileSystemEntries(src).Any())
        {
            var blocked = _jobs.Write(pluginDir, "clone", false, null, "src 非空");
            return Fail(blocked, "src 非空，拒绝覆盖");
        }
        if (Directory.Exists(src))
            Directory.Delete(src, true);
        var result = await RunAsync("git", $"clone -- \"{url}\" \"{src}\"", cancellationToken);
        var job = _jobs.Write(pluginDir, "clone", result.ExitCode == 0, null, result.ExitCode == 0 ? "已克隆" : "克隆失败");
        return result.ExitCode == 0 ? Ok(job, "已克隆") : Fail(job, "克隆失败");
    }

    private async Task<string> SyncAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var id = Required(root, "pluginId");
        var pluginDir = _data.GetPluginDataDirectory(id);
        var src = PluginVersionLayout.SourceDirectory(pluginDir);
        var result = await RunAsync("git", $"-C \"{src}\" pull --ff-only", cancellationToken);
        var job = _jobs.Write(pluginDir, "sync", result.ExitCode == 0, null, result.ExitCode == 0 ? "已同步" : "同步失败");
        return result.ExitCode == 0 ? Ok(job, "已同步") : Fail(job, "同步失败");
    }

    private async Task<string> VerbAsync(JsonElement root, string verb, CancellationToken cancellationToken)
    {
        var id = Required(root, "pluginId");
        var (ok, job, message) = await _build.RunAsync(id, verb, cancellationToken);
        return ok ? Ok(job, message) : Fail(job, message);
    }

    private Task<string> StageAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var path = Required(root, "packagePath");
        var meta = _installer.StageFromPackage(path);
        if (meta == null)
            return Task.FromResult(Fail(null, "暂存失败"));
        var job = _jobs.Write(meta.PluginDirectory, "stage", true, meta.Version, "已暂存");
        return Task.FromResult(Ok(job, "已暂存 " + meta.Version));
    }

    private Task<string> ActivateAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var id = Required(root, "pluginId");
        var version = Required(root, "version");
        var ok = _versions.Activate(id, version);
        return Task.FromResult(ok ? Ok(null, "已激活 " + version) : Fail(null, "激活失败，指针已回滚"));
    }

    private async Task<string> VerifyAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var id = Required(root, "pluginId");
        var baseUrl = Optional(root, "baseUrl");
        var path = Optional(root, "path") ?? "/api/plugin";
        var token = Optional(root, "token");
        var pluginDir = _data.GetPluginDataDirectory(id);
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
        {
            var job = _jobs.Write(pluginDir, "verify", false, null, "需要 baseUrl 与 token，未默认指向任何运行中的宿主");
            return Fail(job, "需要 baseUrl 与 token");
        }

        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, baseUrl.TrimEnd('/') + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            var job = _jobs.Write(pluginDir, "verify", false, null, ex.Message);
            return Fail(job, ex.Message);
        }

        var ok = (int)response.StatusCode == 200;
        var message = "HTTP " + (int)response.StatusCode;
        var jobId = _jobs.Write(pluginDir, "verify", ok, null, message);
        return ok ? Ok(jobId, message) : Fail(jobId, message);
    }

    private Task<string> PublishAsync(JsonElement root, string envName, CancellationToken cancellationToken)
    {
        var id = Optional(root, "pluginId") ?? "unknown";
        var pluginDir = _data.GetPluginDataDirectory(id);
        PluginVersionLayout.EnsureLayout(pluginDir);
        var confirm = root.TryGetProperty("confirm", out var c) && c.ValueKind == JsonValueKind.True;
        var secret = Environment.GetEnvironmentVariable(envName);
        if (!confirm || string.IsNullOrWhiteSpace(secret))
        {
            var why = !confirm ? "缺少用户当次授权 confirm=true" : "缺少环境变量 " + envName;
            var job = _jobs.Write(pluginDir, "publish", false, null, why);
            return Task.FromResult(Fail(job, why + "。未读取密钥文件，未上传。"));
        }

        var jobReady = _jobs.Write(pluginDir, "publish", false, null, "宿主不代发。请用插件仓库 Actions。");
        return Task.FromResult(Fail(jobReady, "已具备授权与密钥，但宿主不代为 push、打 tag 或上传。"));
    }

    private static PluginHostTool Tool(string name, string description, Func<JsonElement, CancellationToken, Task<string>> execute) =>
        new(name, description, execute);

    private static string Required(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("缺少参数 " + name);
        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("参数为空 " + name);
        return text;
    }

    private static string? Optional(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Ok(string? jobId, string message) =>
        JsonSerializer.Serialize(new { ok = true, jobId, message });

    private static string Fail(string? jobId, string message) =>
        JsonSerializer.Serialize(new { ok = false, jobId, message });

    private static async Task<(int ExitCode, string Output)> RunAsync(string file, string args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 " + file);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, "");
    }
}

public sealed class PluginHostTool : IToolFunctionExtension
{
    private readonly Func<JsonElement, CancellationToken, Task<string>> _execute;

    public PluginHostTool(string name, string description, Func<JsonElement, CancellationToken, Task<string>> execute)
    {
        Name = name;
        Id = name;
        Description = description;
        _execute = execute;
    }

    public string Id { get; }
    public string Name { get; }
    public string PluginId => "host";
    public string Description { get; }
    public string ParametersJsonSchema => """
        {"type":"object","properties":{"pluginId":{"type":"string"},"name":{"type":"string"},"url":{"type":"string"},"packagePath":{"type":"string"},"version":{"type":"string"},"baseUrl":{"type":"string"},"path":{"type":"string"},"token":{"type":"string"},"confirm":{"type":"boolean"}},"additionalProperties":true}
        """;

    public Task<string> ExecuteAsync(string parameters) => ExecuteAsync(parameters, CancellationToken.None);

    public async Task<string> ExecuteAsync(string parameters, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            return await _execute(doc.RootElement, cancellationToken);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { ok = false, jobId = (string?)null, message = ex.Message });
        }
    }
}
