using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// 对话模型解析（内置执行 / Run 恢复共用）。有序链，<b>禁止硬编码具体模型</b>：
/// <list type="number">
/// <item>调用方显式指定 → 原样尊重（不改写）；</item>
/// <item>宿主 AI 默认模型（<see cref="IConfigurationService.GetAIConfig"/> 的 <c>ModelName</c>）且能在启用模型目录中匹配 → 补 provider 前缀；</item>
/// <item>模型目录（<see cref="ITurnTelemetryQuery.GetModelCatalogAsync"/>，仅启用模型）首个 → 补 provider 前缀；</item>
/// <item>全不命中 → 返回 <see langword="null"/>，由调用方给出明确失败文案（<b>禁止静默降级</b>）。</item>
/// </list>
/// </summary>
/// <remarks>
/// 历史缺陷（2026-10-25 实证）：<c>BuiltInAgentExecutionService</c> 曾硬编码 <c>default:ornith-1.0-9b</c>，
/// 该模型不在启用模型表中 → <c>AIAgentService.ResolveProvider</c> 静默回退默认 provider → 上游回空流 →
/// 029 判 Stuck，把「模型不可用」误报成「Agent 不会执行」。本解析链是这条缺陷的守卫。
/// </remarks>
public interface IChatModelResolver
{
    /// <summary>解析一次对话使用的 chatModelId（形如 <c>provider:upstreamModelId</c>）；无可用模型返回 <see langword="null"/>。</summary>
    Task<string?> ResolveAsync(string? requested, CancellationToken ct = default);
}

/// <inheritdoc />
public class ChatModelResolver : IChatModelResolver
{
    private readonly IContext? _ctx;
    private ITurnTelemetryQuery? _telemetry;
    private IConfigurationService? _config;

    public ChatModelResolver(IContext? ctx = null) => _ctx = ctx;

    /// <inheritdoc />
    public async Task<string?> ResolveAsync(string? requested, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            XTrace.Log.Info("[ChatModelResolver] 使用调用方指定模型：{0}", requested);
            return requested.Trim();
        }

        _telemetry ??= _ctx?.Get<ITurnTelemetryQuery>();
        if (_telemetry == null)
        {
            XTrace.Log.Warn("[ChatModelResolver] 模型目录契约（ITurnTelemetryQuery）不可用，无法解析默认模型");
            return null;
        }

        var catalog = await _telemetry.GetModelCatalogAsync(ct);

        // ② 宿主默认模型：仅在启用目录中能匹配时才用（不在目录 = 不能用，禁止静默套用）
        _config ??= _ctx?.Get<IConfigurationService>();
        var configured = _config?.GetAIConfig()?.ModelName;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var hit = Match(catalog, configured!);
            if (hit != null)
            {
                var id = Qualify(hit);
                XTrace.Log.Info("[ChatModelResolver] 使用宿主默认模型：{0}（配置值 {1}）", id, configured);
                return id;
            }
            XTrace.Log.Warn("[ChatModelResolver] 宿主默认模型 {0} 不在启用模型目录中，改用目录首个启用模型", configured);
        }

        // ③ 目录首个启用模型
        var first = catalog.FirstOrDefault();
        if (first != null)
        {
            var id = Qualify(first);
            XTrace.Log.Info("[ChatModelResolver] 使用模型目录首个启用模型：{0}", id);
            return id;
        }

        XTrace.Log.Warn("[ChatModelResolver] 启用模型目录为空：没有任何可用于对话的模型");
        return null;
    }

    /// <summary>按 ChatModelId / UpstreamModelId / Alias 匹配（大小写不敏感；配置值可带 <c>provider:</c> 前缀）。</summary>
    private static ModelIdentityDto? Match(IReadOnlyList<ModelIdentityDto> catalog, string configured)
    {
        var name = configured.Trim();
        var idx = name.IndexOf(':');
        if (idx > 0) name = name[(idx + 1)..];

        foreach (var m in catalog)
        {
            if (Same(m.ChatModelId, name) || Same(m.UpstreamModelId, name) || Same(m.Alias, name))
                return m;
        }
        return null;
    }

    private static bool Same(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>补 provider 前缀；ChatModelId 已带 <c>provider:</c> 前缀或 provider 名缺失时原样返回。</summary>
    private static string Qualify(ModelIdentityDto model)
    {
        var id = (model.ChatModelId ?? string.Empty).Trim();
        if (id.Length == 0 || id.Contains(':')) return id;
        return string.IsNullOrWhiteSpace(model.ProviderName) ? id : $"{model.ProviderName}:{id}";
    }
}
