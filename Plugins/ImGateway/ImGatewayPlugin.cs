using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.ImGateway.Channels;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using ForgeSelf.Api.Plugins.ImGateway.Data;
using ForgeSelf.Api.Plugins.ImGateway.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ImGateway;

/// <summary>
/// IM 多渠道网关插件入口。
/// 设计核心：本插件只做「协议适配 + 路由 + 会话管理」，AI 能力经 IChatCompletion 契约（由 AIAgent 提供）获取，
/// 平台协议关在 IImChannel 适配器里。新增飞书/钉钉等平台 = 新增一个 IImChannel 实现，内核零改动。
/// </summary>
public class ImGatewayPlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        XTrace.Log.Info("[ImGateway] 初始化 IM 网关插件（v2.0.0 企微长连接）");

        // v2.0.0：数据落库（配置/会话/去重三张 XCode 实体表），插件必须自行建表（宿主扫不到插件实体）
        ImGatewayTables.EnsureCreated();

        var services = ctx.Get<IServiceCollection>();

        // 共享基础设施：配置/会话存储走 SQLite（Secret 经宿主 ISecretEncryptionService 加密入库）。
        // 加密服务运行期惰性解析：Apply 阶段宿主接缝（ProvideHostServices）尚未 seed，Save/Load 时已就绪。
        var configStore = new DbConfigStore(() => ctx.Get<ISecretEncryptionService>());
        var sessionStore = new DbSessionStore();

        // v1.1.0 旧文件配置迁移：config.json 存在且 DB 尚无配置 → 读旧文件 → 加密入库 → 原文件改名留档（不删除）
        MigrateLegacyFileConfig(configStore);

        services?.AddSingleton<IConfigStore>(configStore);
        services?.AddSingleton(sessionStore);

        // 通道适配器：v2.0.0 起仅企微「智能机器人」长连接形态（wss 主动外连），回调形态已整体移除
        services?.AddSingleton<IImChannel>(new WeComChannel(configStore));

        // 网关路由核心（单例，持有通道字典 + 会话存储 + IChatCompletion 解析器）
        services?.AddSingleton<ImGatewayRouter>();

        // 长连接生命周期管理器 + 宿主钩子（企微长连接在此拉起/停止；回调型通道不受影响）
        services?.AddSingleton<ImGatewayConnectionManager>();
        services?.AddSingleton<IHostedService, ImGatewayConnectionHostedService>();

        // 扫码授权服务（方案 A：CLI 仅作扫码取凭据工具，扫码成功后自动解密回填 BotId+Secret）
        services?.AddSingleton<WeComScanAuthService>();

        // 侧边栏入口
        MenuExtensions.Add(new ImGatewayMenuExtension
        {
            Id = "imgateway.menu.main",
            Name = "IM 网关",
            PluginId = ctx.Get<PluginMetadata>()?.Id ?? "im-gateway",
            Icon = "fa-solid fa-comments",
            Path = "/im-gateway",
            Order = 40
        });

        XTrace.Log.Info("[ImGateway] IM 网关插件初始化完成（已注册 1 个长连接通道适配器 + 网关路由）");
    }

    /// <summary>
    /// v1.1.0 → v2.0.0 配置迁移：旧 config.json 明文文件 → 加密入库 → 改名留档（绝不删除）。
    /// 触发条件：config.json 存在，且 DB 中尚无配置（避免重复迁移覆盖新配置）。
    /// </summary>
    private static void MigrateLegacyFileConfig(IConfigStore dbStore)
    {
        try
        {
            var dataDir = FileConfigStore.ResolvePluginDataDir();
            var filePath = Path.Combine(dataDir, "config.json");
            if (!File.Exists(filePath)) return;

            var dbCfg = dbStore.Load();
            if (!string.IsNullOrEmpty(dbCfg.WeCom.BotId) || dbCfg.WeCom.Enabled)
            {
                // DB 已有配置：旧文件仅是历史残留，不覆盖；若尚未留档则补改名
                ArchiveLegacyFile(filePath);
                return;
            }

            var legacy = new FileConfigStore().Load();
            var hasLegacy = !string.IsNullOrEmpty(legacy.WeCom?.BotId) || !string.IsNullOrEmpty(legacy.WeCom?.Secret) || (legacy.WeCom?.Enabled ?? false);
            if (hasLegacy)
            {
                dbStore.Save(legacy);
                XTrace.Log.Info("[ImGateway] 旧 config.json 配置已加密迁移入库（BotId={0}）", legacy.WeCom.BotId);
            }
            ArchiveLegacyFile(filePath);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ImGateway] 旧配置迁移失败（保留原文件待手工处理）: {0}", ex.Message);
        }
    }

    /// <summary>把旧 config.json 改名为 config.json.imported-&lt;ts&gt; 留档（可逆，非删除）。</summary>
    private static void ArchiveLegacyFile(string filePath)
    {
        try
        {
            var archive = $"{filePath}.imported-{DateTime.Now:yyyyMMddHHmmss}";
            if (File.Exists(archive)) archive = $"{filePath}.imported-{DateTime.Now:yyyyMMddHHmmssfff}";
            File.Move(filePath, archive);
            XTrace.Log.Info("[ImGateway] 旧配置已留档: {0}", Path.GetFileName(archive));
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ImGateway] 旧配置留档改名失败（忽略，不影响运行）: {0}", ex.Message);
        }
    }
}

public class ImGatewayMenuExtension : IMenuExtension
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? ParentId { get; set; }
    public IReadOnlyList<IMenuExtension>? Children { get; set; }
}
