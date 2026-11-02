using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.BrowserUse;

/// <summary>
/// 本机浏览器插件。工具只注册在本插件，不进入 AIAgent/MafLoop。无界面，不做团队共享。
/// </summary>
public class BrowserUsePlugin : IPlugin
{
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "browser-use";
        XTrace.Log.Info("[BrowserUse] 初始化浏览器插件 (Id={0})", pluginId);

        string dataDir;
        try
        {
            dataDir = ctx.EnsurePluginDataDirectory();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[BrowserUse] 插件数据目录不可用，工具调用将失败: {0}", ex.Message);
            dataDir = "";
        }

        if (string.IsNullOrEmpty(dataDir))
        {
            XTrace.Log.Error("[BrowserUse] 未取得数据目录，跳过工具注册");
            return;
        }

        try
        {
            BrowserUseConfig.Load(dataDir);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[BrowserUse] 写入默认 config.json 失败: {0}", ex.Message);
        }

        var session = new BrowserSession(dataDir);
        ctx.Effect(() => Disposable.Create(() => session.Shutdown()));

        RegisterToolFunctionExtensions(pluginId, session);
        XTrace.Log.Info("[BrowserUse] 浏览器插件初始化完成，工具 {0} 个", ToolExtensions.Count);
    }

    private void RegisterToolFunctionExtensions(string pluginId, BrowserSession session)
    {
        XTrace.Log.Debug("[BrowserUse] 注册 AI 工具函数扩展点");
        ToolExtensions.Add(new BrowserOpenTool(pluginId, session));
        ToolExtensions.Add(new BrowserNavigateTool(pluginId, session));
        ToolExtensions.Add(new BrowserSnapshotTool(pluginId, session));
        ToolExtensions.Add(new BrowserClickTool(pluginId, session));
        ToolExtensions.Add(new BrowserFillTool(pluginId, session));
        ToolExtensions.Add(new BrowserExtractTool(pluginId, session));
        ToolExtensions.Add(new BrowserScreenshotTool(pluginId, session));
        ToolExtensions.Add(new BrowserDownloadTool(pluginId, session));
        XTrace.Log.Debug("[BrowserUse] AI 工具函数扩展点注册完成，共 {0} 个", ToolExtensions.Count);
    }
}
