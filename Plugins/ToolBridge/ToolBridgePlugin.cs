using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.ToolBridge.Services;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ToolBridge;

/// <summary>
/// 工具桥插件（PILOT-053）：把外部网页 AI 的工具调用文本接到本机原子能力上，跑一轮人工 agent 回合。
///
/// 决策 D3（03-plan）：本插件**不向宿主 ToolRegistry 注册工具扩展点**——
/// <c>read_file</c> / <c>write_file</c> / <c>list_files</c> 已由 AIAgent 注册，重名并存会让内置 agent
/// 出现两份同名工具；且进内置 agent 视野需改 <c>AIAgentService.ToolScopePluginIds</c> 白名单，
/// 该处注释实证「~77 个工具会撑爆本地小模型 prompt → 400」。要接进内置 agent 须另立批次出 ADR。
/// </summary>
public class ToolBridgePlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; } = new();

    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        try
        {
            var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "tool-bridge";

            // 插件私有数据（工作根默认落点 + 台账 + settings.json）一律走数据目录，不放发布目录。
            var dataDir = ctx.EnsurePluginDataDirectory();
            var sandbox = new SandboxRoot(dataDir);
            var ledger = new TurnLedger(dataDir);

            var services = ctx.Get<IServiceCollection>();
            services?.AddSingleton(sandbox);
            services?.AddSingleton(ledger);

            // 双注册同一实例（同 AIAgentPlugin.cs:26-35 记录的坑）：控制器走宿主 DI，
            // 插件内运行时走 ctx.Get；若两处各建一份实例，工作根会"设在一个实例、读在另一个实例"。
            ctx.Register(sandbox);
            ctx.Register(ledger);

            XTrace.Log.Info("[tool-bridge] 插件已初始化（Id={0}，数据目录={1}，默认工作根={2}）",
                pluginId, dataDir, sandbox.DefaultRoot);
        }
        catch (Exception ex)
        {
            // Apply 内抛异常会导致整个插件注册失败且只留一句「注册插件服务失败」（SamplePlugin 权威注释），
            // 因此这里兜住：初始化失败宁可降级，也不拖垮插件加载。
            XTrace.Log.Error("[tool-bridge] 初始化失败（插件不可用）：{0}", ex.Message);
        }
    }
}
