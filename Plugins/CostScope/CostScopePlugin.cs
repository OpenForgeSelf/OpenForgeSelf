using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.CostScope;

/// <summary>
/// 成本观测插件（CostScope）：LLM 用量成本可视化。
/// 数据全部落在插件自有库（连接名 <c>CostScope</c>，库文件 <c>CostScope.db</c>），与宿主库严格隔离（铁律12）。
/// 本文件为原子任务 A2（插件骨架）：仅完成插件注册、插件数据目录准备与最小 DI 占位；
/// 成本计算 / 单价目录 CRUD / 预算 / 日汇总 / 遥测契约实现分别在 A3–A7、A11 落地。
/// </summary>
/// <remarks>
/// 连接名 <c>CostScope</c> 已在宿主 <c>XCodeConfig.PluginDbs</c> 注册（ConnName→插件Id=cost-scope），
/// 宿主据此自动建连接串与建表；实体由 <c>Data/Model.xml</c> 经 xcode 生成，禁手改（铁律9）。
/// </remarks>
public class CostScopePlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; } = new();

    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        // ① 自身元数据（Id 来自 plugin.json，不硬编码）
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "cost-scope";
        XTrace.Log.Info("[CostScopePlugin] 初始化成本观测插件 (Id={0})", pluginId);

        // ② 确保插件私有数据目录（库文件 {连接名}.db 落于此）
        try
        {
            var dataDir = ctx.EnsurePluginDataDirectory();
            XTrace.Log.Info("[CostScopePlugin] 插件数据目录就绪: {0}", dataDir);
        }
        catch (Exception ex)
        {
            // Apply 内异常会导致插件注册失败，此处兜底，不让成本插件拖垮宿主
            XTrace.Log.Warn("[CostScopePlugin] 准备插件数据目录失败（不影响插件加载）: {0}", ex.Message);
        }

        // ③ 服务注册（A11）：成本/预算目录与 trace 投影是**无状态或仅持有插件库句柄**的服务，
        //    可安全单例；**聚合器（CostAggregationService / TelemetryProjectionService）故意不进 DI**——
        //    它们依赖「本次请求的模型目录快照」，按请求新建才能避免目录变更后读到陈旧归属（见 CostController.LoadAsync）。
        var services = ctx.Get<IServiceCollection>();
        if (services is not null)
        {
            services.AddSingleton<CostScopeDbNaming>();
            services.AddSingleton<ForgeSelf.Api.Plugins.CostScope.Services.PriceCatalogService>();
            services.AddSingleton<ForgeSelf.Api.Plugins.CostScope.Services.BudgetService>();
            services.AddSingleton<ForgeSelf.Api.Plugins.CostScope.Services.TraceProjectionService>();
        }

        XTrace.Log.Info("[CostScopePlugin] 成本观测插件初始化完成");
    }
}

/// <summary>
/// CostScope 连接名常量：插件库统一连接名，必须与 <c>Data/Model.xml</c> 的 ConnName 及
/// 宿主 <c>XCodeConfig.PluginDbs</c> 的键一致（单一真源，避免拼写漂移）。
/// </summary>
public sealed class CostScopeDbNaming
{
    /// <summary>XCode 连接名（= 库文件名前缀，库为 CostScope.db）。</summary>
    public const string ConnName = "CostScope";

    /// <summary>插件 Id（kebab-case，与 plugin.json 一致）。</summary>
    public const string PluginId = "cost-scope";
}
