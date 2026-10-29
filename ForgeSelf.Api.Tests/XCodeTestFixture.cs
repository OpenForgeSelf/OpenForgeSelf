using XCode.DataAccessLayer;
using XCode;
using System.Reflection;
using NewLife.Log;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// XCode 测试数据库辅助类，为测试提供临时 SQLite 数据库
/// </summary>
/// <remarks>
/// 2026-10-29 实测（PILOT-051）：改为<b>进程级单次绑定</b>。
/// 原设计每类一个临时目录 + ctor 重跑 <c>DAL.AddConnStr</c> 换库，实测在
/// XCode 12.0.2026.701 下存在跨类换库竞态：AddConnStr 换串后仍有旧连接/旧会话
/// 被交替检出（DAL.Reset/DbBase.ReleaseSession 均收不干净），同一测试类的写操作
/// 随机落入新旧两个库（AgentRun 落旧库、AgentStepRun 交替、部分行整体消失），
/// GoalPursuitTests 之后的 OrchestratorBehaviorSnapshotTests 因此假红。
/// 现改为：进程内第一个夹具 ctor 绑定随机临时目录并注册连接串；后续 ctor
/// <b>复用同一目录</b>，仅 EnsureTablesCreated + ClearAllData（同库清空 ≈ 换新库语义，
/// 且全程不动连接串，彻底消除重绑竞态）。各类实体缓存清零仍由测试类 ctor 自理（既有模式）。
/// </remarks>
public class XCodeTestFixture
{
    private static string? _boundDir;

    private readonly string _dbDir;
    private static readonly string[] _connNames = new[]
    {
        "ForgeSelf",
        "MemorySystem",
        "QuickLinks",
        "Scheduler",
        "ScriptRunner",
        "WorkflowEngine",
        "AIAgent",
        "TodoTracker",
        "ProxyCapture",
        "ImGateway",
        "FileTools"
    };

    public XCodeTestFixture()
    {
        if (_boundDir == null)
        {
            _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_dbDir);
            _boundDir = _dbDir;

            XTrace.Log.Level = LogLevel.Error;

            // 预载插件程序集（2026-10-29 实证）：全量首绑可能早于插件实体程序集加载，
            // EntityFactory.Init 的 GetAllSubclasses 只扫【已加载】程序集 → 静默漏建表
            //（test-full-2/3/4 连续三轮大面积 no such table 的根因）。显式 Load 后首绑
            // 即可为全部连接名建齐所有实体表；个别名不存在/无实体时忽略。
            foreach (var name in new[]
            {
                "ForgeSelf.Api", "AIAgent", "TodoTracker", "QuickLinks", "Scheduler",
                "ScriptRunner", "WorkflowEngine", "ImGateway", "MemorySystem", "FileTools",
                "DesignSystem", "CostScope", "Sems", "AgentHub", "McpCenter", "SamplePlugin",
                "DevTools", "SystemMonitor", "ToolBridge",
            })
            {
                try { Assembly.Load(new AssemblyName(name)); }
                catch { /* 可选程序集缺失不影响其余连接名 */ }
            }

            foreach (var connName in _connNames)
            {
                var dbPath = Path.Combine(_dbDir, $"{connName}.db");
                var connStr = $"Data Source={dbPath}";
                DAL.AddConnStr(connName, connStr, null, "SQLite");
            }

            EnsureTablesCreated();

            // 2026-10-29（PILOT-051，最终形态）：保持 Migration=On（默认）。
            // 实测结论：吞行/吞表的真正来源是「测试 ctor 反复 AddConnStr 换库」→ DAL.Reset() 换
            // Database 实例（_hasCheck 归零 → 每类重跑 CheckTables → SQLite「备份-重建-回填」
            // 与并发写竞态）+ 旧 ThreadLocal/AsyncLocal 会话残留。进程级单次绑定 + 全部测试类
            // 禁止手搓连接串后，CheckTables 全进程只跑一次、schema 不再变化，SetTables 对已存在
            // 同构表是空操作，重建竞态随之消失。**不要**在此把 Database 置 Migration=Off：
            // 实测会连 CheckTableWhenFirstUse 实体（WorkflowDefinition/ScriptExecution/AIChatMessage 等）
            // 的惰性建表一起堵死，全量出现大面积 no such table（test-full-2/3 实证）。
        }
        else
        {
            // 复用已绑定目录：清空业务表数据，等价于「每类一个新库」的旧语义；
            // 连接串不变 ⇒ 无重绑、无重建，清库是纯 DELETE（见类注释）
            _dbDir = _boundDir;

            ClearAllData();
        }
    }

    /// <summary>进程绑定的测试库目录（首个 ctor 创建，后续复用）。测试类需要直接读库文件时取用。</summary>
    public string DbDir => _dbDir;

    /// <summary>
    /// 注册连接串后，为每个连接名主动建表（反向工程）。
    /// 用 <see cref="EntityFactory.InitConnection"/> 触发该连接名下所有实体的建表，
    /// 避免测试运行期出现 "no such table" 错误。
    /// 依据 DeepWiki（NewLifeX/NewLife.XCode）：InitConnection 会获取该连接的 DAL 实例，
    /// 在 Migration 开启时调用 dal.SetTables(...) 创建/更新所有关联实体表。
    /// </summary>
    public static void EnsureTablesCreated()
    {
        foreach (var connName in _connNames)
        {
            try
            {
                // Migration 保持 On（默认）：本方法在首个夹具 ctor 与每测试 Before() 各跑一遍，
                // 既补建「迟到程序集」实体（全量首绑时 AIAgent/DesignSystem 等程序集可能尚未加载），
                // 也覆盖 CheckTableWhenFirstUse 实体的表。schema 未变时 SetTables 是空操作；
                // 进程内无 AddConnStr 换库 ⇒ 无 Reset ⇒ 无重复 CheckTables ⇒ 无重建竞态。
                EntityFactory.InitConnection(connName);
            }
            catch
            {
                // 个别连接名可能暂无关联实体，忽略（诊断期曾输出异常原文，全量三轮零命中）
            }
        }
    }

    /// <summary>
    /// 清空全部测试连接名下所有业务表数据（每测试方法前调用，保证用例间零串扰）。
    /// 只作用于 <see cref="_connNames"/> 指向的测试临时库（构造函数注册的随机目录），
    /// 与任何真实数据目录无交集；跳过 sqlite_/sys_ 等系统表。
    /// </summary>
    public static void ClearAllData()
    {
        foreach (var connName in _connNames)
        {
            try
            {
                var dal = DAL.Create(connName);
                foreach (var table in dal.Tables)
                {
                    var name = table.Name;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (name.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.StartsWith("sys_", StringComparison.OrdinalIgnoreCase)) continue;
                    dal.Execute($"DELETE FROM {name}");
                }
            }
            catch
            {
                // 个别连接暂无可清空的表（或表已不存在）时忽略，不阻断测试
            }
        }
    }
}
