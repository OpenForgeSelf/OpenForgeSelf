using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using ForgeSelf.Api.Plugins.SmartTerminal.Entities;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Data;

/// <summary>
/// 智能终端插件建表入口。连接名 <c>SmartTerminal</c> 须已在宿主
/// <c>XCodeConfig.PluginDbs["SmartTerminal"]="smart-terminal"</c> 登记。
/// 插件不得自行 <c>DAL.AddConnStr</c>。
/// </summary>
public static class SmartTerminalTables
{
    public const string ConnName = "SmartTerminal";

    public static readonly Type[] EntityTypes =
    [
        typeof(StProfile),
        typeof(StSnippet),
        typeof(StSecretRef),
        typeof(StAudit)
    ];

    /// <summary>确保库与表就绪（幂等）。失败返回 false，调用方只告警，不让插件加载失败。</summary>
    public static bool EnsureCreated()
    {
        try
        {
            EntityFactory.InitConnection(ConnName);
            var dal = DAL.Create(ConnName);
            var version = dal.Db.ServerVersion;
            XTrace.Log.Info("[smart-terminal] 数据库就绪 (ConnName={0} ServerVersion={1})", ConnName, version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[smart-terminal] 建表失败: {0}", ex.Message);
            return false;
        }
    }
}
