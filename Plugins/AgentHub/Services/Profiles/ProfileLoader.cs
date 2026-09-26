using System.Text.Json;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Profiles;

/// <summary>
/// 声明式 profile 定义（L1 扩展点，见 design §14）。
/// 新增一个标准 CLI agent = 丢一个 json 到 Profiles 目录，不需要改代码。
/// </summary>
public class AgentProfile
{
    /// <summary>厂商标识（必须唯一，与 AgentDefinition.Vendor 对应）</summary>
    public String Id { get; set; } = string.Empty;

    /// <summary>厂商展示名</summary>
    public String DisplayName { get; set; } = string.Empty;

    /// <summary>类型（Coding|Generic）</summary>
    public String Kind { get; set; } = "Coding";

    /// <summary>可执行文件名（PATH 中查找，如 codex）</summary>
    public String Executable { get; set; } = string.Empty;

    /// <summary>参数模板（含占位符 {prompt} {cwd} {model} {permission}）</summary>
    public String ArgsTemplate { get; set; } = string.Empty;

    /// <summary>提示词注入方式（Arg|Stdin）</summary>
    public String PromptInjection { get; set; } = "Arg";

    /// <summary>输出格式（Text|JsonLines|Json）</summary>
    public String OutputFormat { get; set; } = "Text";

    /// <summary>输出字段映射（L2 扩展点：事件类型/文本/工具名的 JSONPath）</summary>
    public Dictionary<String, String>? OutputMapping { get; set; }

    /// <summary>会话续接参数模板（如 --session {sessionId}）</summary>
    public String? SessionFlagTemplate { get; set; }

    /// <summary>权限参数模板（如 --sandbox {permission}）</summary>
    public String? PermissionFlagTemplate { get; set; }

    /// <summary>探测参数（如 --version）</summary>
    public String? ProbeArgs { get; set; }

    /// <summary>探测断言：校验关键 flag / 最低版本（防 profile 漂移，见 design §14 防脆弱三件套）</summary>
    public List<ProfileProbeAssertion>? Probe { get; set; }

    /// <summary>能力矩阵（Facet F1-F6；每格要么 true 要么 false，不得省略）</summary>
    public Dictionary<String, Boolean>? Capabilities { get; set; }

    /// <summary>profile 版本（针对哪个版本的 agent CLI 验证过）</summary>
    public String ProfileVersion { get; set; } = string.Empty;

    /// <summary>备注</summary>
    public String? Notes { get; set; }
}

/// <summary>profile 探测断言。</summary>
public class ProfileProbeAssertion
{
    /// <summary>校验类型（version_min|help_contains）</summary>
    public String Kind { get; set; } = string.Empty;

    /// <summary>期望值（最低版本号 / 应包含的文本）</summary>
    public String Expect { get; set; } = string.Empty;
}

/// <summary>
/// profile 加载器：内置 profile（随程序集发布）+ 数据根覆盖版本（用户可改，热加载）。
/// 加载顺序：数据根 Profiles 目录覆盖同名内置 profile —— 用户改配置不需要重新发布插件。
/// </summary>
public class ProfileLoader
{
    /// <summary>内置 profile 所在子目录（随程序集输出）</summary>
    private const String BuiltinDirName = "Data/Profiles";

    /// <summary>用户覆盖 profile 所在子目录（数据根下，可编辑）</summary>
    private const String UserDirName = "Profiles";

    private readonly Dictionary<String, AgentProfile> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Object _lock = new();

    /// <summary>
    /// 内置 profile 搜索目录（按优先级）：
    /// 1) <c>{程序集目录}/Data/Profiles</c> —— 宿主把插件目录内容铺平的情形；
    /// 2) <c>{程序集目录}/../Data/Profiles</c> —— 插件 DLL 位于 <c>Plugins/{Id}/</c> 子目录的情形（生产真实布局，
    ///    也是测试宿主的布局：DLL 在 <c>Plugins/AgentHub/</c>，profile 在其下 <c>Data/Profiles/</c>）。
    /// 只取实际存在的目录，避免无谓 IO。
    /// </summary>
    private static IEnumerable<String> BuiltinDirs
    {
        get
        {
            var baseDir = AppContext.BaseDirectory;
            var primary = Path.Combine(baseDir, BuiltinDirName);
            if (Directory.Exists(primary)) yield return primary;

            // 插件私有子目录布局：Plugins/{Id}/Data/Profiles
            var parent = Path.GetDirectoryName(baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!parent.IsNullOrEmpty())
            {
                var sibling = Path.Combine(parent, BuiltinDirName);
                if (!sibling.EqualIgnoreCase(primary) && Directory.Exists(sibling)) yield return sibling;
            }
        }
    }

    /// <summary>已加载的 profile 数量</summary>
    public Int32 Count
    {
        get { lock (_lock) return _cache.Count; }
    }

    /// <summary>
    /// 加载全部 profile：先读内置，再用数据根的同名文件覆盖。
    /// </summary>
    /// <param name="userProfileDir">用户 profile 目录（数据根下 Profiles/），可为 null</param>
    /// <returns>加载到的 profile 列表</returns>
    public IReadOnlyList<AgentProfile> LoadAll(String? userProfileDir = null)
    {
        lock (_lock)
        {
            _cache.Clear();

            foreach (var dir in BuiltinDirs) LoadFromDirectory(dir, isBuiltin: true);

            if (!userProfileDir.IsNullOrEmpty())
            {
                LoadFromDirectory(userProfileDir, isBuiltin: false);
            }

            return _cache.Values.ToList();
        }
    }

    /// <summary>按厂商标识取 profile；不存在返回 null</summary>
    /// <param name="id">厂商标识</param>
    /// <returns>profile 或 null</returns>
    public AgentProfile? Get(String id)
    {
        if (id.IsNullOrEmpty()) return null;

        lock (_lock)
        {
            return _cache.TryGetValue(id, out var profile) ? profile : null;
        }
    }

    /// <summary>取全部已加载 profile（只读快照）</summary>
    /// <returns>profile 列表</returns>
    public IReadOnlyList<AgentProfile> All()
    {
        lock (_lock) return _cache.Values.ToList();
    }

    /// <summary>扫描目录并加载 *.json；同名时后加载的覆盖先加载的（用户目录覆盖内置目录）</summary>
    /// <param name="dir">目录</param>
    /// <param name="isBuiltin">是否内置目录（仅用于日志区分）</param>
    private void LoadFromDirectory(String dir, Boolean isBuiltin)
    {
        if (dir.IsNullOrEmpty() || !Directory.Exists(dir)) return;

        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<AgentProfile>(json, JsonOptions);
                if (profile == null || profile.Id.IsNullOrEmpty())
                {
                    XTrace.Log.Warn("[AgentHub] profile {0} 缺少 Id，已跳过", Path.GetFileName(file));
                    continue;
                }

                _cache[profile.Id] = profile;
                XTrace.Log.Debug("[AgentHub] 已加载 {0} profile: {1}（{2}）",
                    isBuiltin ? "内置" : "用户", profile.Id, Path.GetFileName(file));
            }
            catch (Exception ex)
            {
                // 单个 profile 坏掉不影响其余：显式告警而非静默
                XTrace.Log.Warn("[AgentHub] 加载 profile {0} 失败: {1}", Path.GetFileName(file), ex.Message);
            }
        }
    }

    /// <summary>JSON 反序列化选项（camelCase，容忍注释/尾随逗号便于用户手改）</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
