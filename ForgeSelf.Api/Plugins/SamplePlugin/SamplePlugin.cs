using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.SamplePlugin;

/// <summary>
/// 示例插件：演示插件系统全部基础能力的最小可运行骨架，可作为新插件的起手模板。
/// </summary>
/// <remarks>
/// <para><b>插件契约</b>：实现 <see cref="IPlugin"/> 并在 <c>plugin.json</c> 的
/// <c>EntryType</c> 指向本类全名；宿主通过 <see cref="PluginManager"/> 隔离加载程序集后调用
/// <see cref="Apply"/>。<b>不要</b>写构造函数逻辑或实现旧的 Initialize/Start/Stop/Destroy 生命周期
/// （已废弃），一切初始化都在 <see cref="Apply"/> 内完成。</para>
///
/// <para><b>五件事可在此做</b>：</para>
/// <list type="number">
/// <item><description>取自身元数据：<c>ctx.Get&lt;PluginMetadata&gt;()?.Id</c> —— Id 的单一真源是
/// <c>plugin.json</c>，不要在代码里硬编码（本插件 Id 为 <c>sample</c>，kebab-case）。</description></item>
/// <item><description>注册 DI 服务：<c>ctx.Get&lt;IServiceCollection&gt;()?.AddScoped&lt;TImpl&gt;()</c>。
/// 注意取到的是宿主的集合，注册后对全应用可见。</description></item>
/// <item><description>注册扩展点：往 <see cref="MenuExtensions"/> / <see cref="ToolExtensions"/>
/// 里加条目，宿主在 Apply 完成后统一收集。</description></item>
/// <item><description>写插件私有数据：用 <c>ctx.EnsurePluginDataDirectory()</c> 拿
/// <c>{数据根}/Plugins/{插件Id}</c>，<b>禁止</b>直接写 <c>AppContext.BaseDirectory</c>
/// （发布目录下可能是只读安装位置，且会污染程序目录）。</description></item>
/// <item><description>注册可逆副作用：<c>ctx.Effect(() => ...)</c>，插件卸载时自动收回
/// （定时器、事件订阅、文件监听等），避免热卸载后残留。</description></item>
/// </list>
///
/// <para><b>不要做的事</b>：不要在 <c>Apply</c> 里抛异常（会把整个插件注册失败，
/// 宿主日志只显示「注册插件服务失败」）；必要时内部 try/catch 兜底。</para>
/// </remarks>
public class SamplePlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; } = new();

    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        // ① 自身元数据（Id 来自 plugin.json，不硬编码）
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "sample";
        XTrace.Log.Info("[SamplePlugin] 初始化示例插件 (Id={0})", pluginId);

        // ② 注册 DI 服务（示例：一个无状态的计算服务）
        ctx.Get<IServiceCollection>()?.AddScoped<ISampleGreetingService, SampleGreetingService>();

        // ③ 注册扩展点
        RegisterMenuExtensions(pluginId);
        RegisterToolFunctionExtensions(pluginId, ctx);

        // ⑤ 可逆副作用示例：把本次启动时间写入插件数据目录，并在卸载时清理标记文件。
        //    演示了「插件私有数据落在哪」以及「副作用如何随卸载收回」两件事。
        try
        {
            var dataDir = ctx.EnsurePluginDataDirectory();
            var marker = Path.Combine(dataDir, "last-start.txt");
            File.WriteAllText(marker, DateTime.Now.ToString("O"));
            // Effect 的委托返回 IDisposable，Dispose 即「收回副作用」；用 Disposable.Create 包一个 Action
            ctx.Effect(() => Disposable.Create(() =>
            {
                if (File.Exists(marker)) File.Delete(marker);
            }));
        }
        catch (Exception ex)
        {
            // Apply 内异常会导致插件注册失败，此处兜底不让示例插件拖垮宿主
            XTrace.Log.Warn("[SamplePlugin] 写入插件数据目录失败（不影响插件加载）: {0}", ex.Message);
        }

        XTrace.Log.Info("[SamplePlugin] 示例插件初始化完成");
    }

    private void RegisterMenuExtensions(string pluginId)
    {
        MenuExtensions.Add(new SampleMenuExtension
        {
            Id = "sample.menu.main",
            Name = "示例",
            PluginId = pluginId,
            Icon = "fa-solid fa-puzzle-piece",
            Path = "/sample",
            Order = 900
        });
    }

    private void RegisterToolFunctionExtensions(string pluginId, IContext ctx)
    {
        // AI 工具函数：注册后可在对话中被模型调用（ToolId 建议带插件前缀避免跨插件重名）
        ToolExtensions.Add(new SampleToolFunction(pluginId, ctx));
    }
}

/// <summary>示例服务：演示插件内 DI 服务的注册与消费。</summary>
public interface ISampleGreetingService
{
    string Greet(string name);
}

public class SampleGreetingService : ISampleGreetingService
{
    public string Greet(string name) => string.IsNullOrWhiteSpace(name) ? "你好！" : $"你好，{name}！";
}

/// <summary>示例菜单扩展：把插件页面挂到宿主侧边栏。</summary>
public class SampleMenuExtension : IMenuExtension
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string PluginId { get; init; } = string.Empty;

    public string Icon { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public int Order { get; init; }

    public string? ParentId { get; init; }

    /// <summary>子菜单；顶级菜单留空即可。接口要求只读列表，避免宿主侧被意外改写。</summary>
    public IReadOnlyList<IMenuExtension>? Children { get; init; }
}

/// <summary>
/// 示例 AI 工具函数：向模型暴露一个无副作用的 greet 能力。
/// </summary>
/// <remarks>
/// 真实插件请从宿主提供的工具基类继承（如 <c>ToolFunctionBase</c>）；此处直接实现接口，
/// 是为了让示例不依赖任何宿主内部类型，保持「可直接复制改名即用」。
/// </remarks>
public class SampleToolFunction : IToolFunctionExtension
{
    private readonly string _pluginId;
    private readonly IContext _ctx;

    public SampleToolFunction(string pluginId, IContext ctx)
    {
        _pluginId = pluginId;
        _ctx = ctx;
    }

    /// <summary>扩展点 Id：加插件前缀避免跨插件重名（历史教训：全局工具注册表按 Id 唯一）。</summary>
    public string Id => $"{_pluginId}.greet";

    public string Name => "示例问候";

    public string Description => "向指定对象问好，用于演示 AI 工具函数的注册与调用。";

    public string PluginId => _pluginId;

    /// <summary>JSON Schema 形式参数声明，供模型生成调用参数（接口要求返回普通字符串）。</summary>
    public string ParametersJsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "name": { "type": "string", "description": "要问候的对象" }
          },
          "required": ["name"]
        }
        """;

    /// <summary>
    /// 执行工具函数。
    /// </summary>
    /// <param name="parameters">模型生成的参数 JSON 字符串。</param>
    /// <returns>执行结果字符串（会作为工具调用结果回传给模型）。</returns>
    public Task<string> ExecuteAsync(string parameters)
    {
        var name = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(parameters);
            if (doc.RootElement.TryGetProperty("name", out var n)) name = n.GetString() ?? string.Empty;
        }
        catch (JsonException)
        {
            // 模型传入非法 JSON 时降级为按原文处理，不抛异常打断对话
            name = parameters?.Trim('"') ?? string.Empty;
        }

        // 从插件自己的 Fiber 上下文解析私有服务（而非根 provider），保证作用域正确
        var greeting = _ctx.Get<IServiceProvider>()?.GetService(typeof(ISampleGreetingService))
            as ISampleGreetingService ?? new SampleGreetingService();

        return Task.FromResult(greeting.Greet(name));
    }
}
