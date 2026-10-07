namespace ForgeSelf.Api.Plugins.ToolBridge.Services;

/// <summary>工作根读数（02-spec Output）。</summary>
public sealed record WorkspaceInfo(string Root, bool Exists, string DefaultRoot, string Source, bool Dangerous);

/// <summary>
/// 沙箱工作根（PILOT-053 02-spec FR-5）：解析、越界判定、设置落盘。
/// 越界判定沿用内置 agent 的三步姿势（<c>Plugins/AIAgent/Services/ProjectWorkspaceService.cs:98-122</c>）：
/// 归一分隔符 → <c>Path.GetFullPath</c> → 必须以 <c>root + 分隔符</c> 开头，否则拒。
///
/// BC-11 危险工作根：盘符根 / 用户目录根 / 位于某个 git 仓库树内（含仓库根本身）默认**拒绝设置**，
/// 需要调用方显式 <c>confirmUnsafe=true</c> 才放行——防止 AI 生成的路径把用户真实仓库覆写掉。
/// </summary>
public sealed class SandboxRoot
{
    private static readonly char Sep = Path.DirectorySeparatorChar;

    private readonly string _settingsPath;
    private readonly object _sync = new();
    private WorkspaceInfo? _cached;

    public string PluginDataDir { get; }

    public string DefaultRoot { get; }

    public SandboxRoot(string pluginDataDir)
    {
        PluginDataDir = pluginDataDir;
        DefaultRoot = Path.Combine(pluginDataDir, "workspace");
        _settingsPath = Path.Combine(pluginDataDir, "settings.json");
    }

    /// <summary>当前工作根；不存在时按需创建（只建目录，永不删）。</summary>
    public WorkspaceInfo Current()
    {
        lock (_sync)
        {
            if (_cached == null) _cached = Load();
            EnsureDirectory(_cached.Root);
            return _cached;
        }
    }

    public bool TrySetRoot(string? root, bool confirmUnsafe, out WorkspaceInfo? info, out string? error)
    {
        info = null;
        error = null;

        if (string.IsNullOrWhiteSpace(root))
        {
            error = "root 为空：要设置的工作目录必须是绝对路径";
            return false;
        }

        var raw = root.Trim().Trim('"');
        // 必须在 GetFullPath 之前判：GetFullPath 会把相对路径按当前目录补成"看起来合法"的绝对路径，
        // 那样用户写错的相对路径会静默落到宿主进程目录下（实测缺陷）。
        if (!Path.IsPathRooted(raw))
        {
            error = $"必须是绝对路径（收到「{raw}」）——相对路径会随当前目录漂移，不能当工作根";
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(raw);
        }
        catch (Exception ex)
        {
            error = $"路径非法：{ex.Message}";
            return false;
        }

        if (!Path.IsPathRooted(full))
        {
            error = $"必须是绝对路径（收到「{full}」）";
            return false;
        }

        var trimmed = full.TrimEnd(Sep, Path.AltDirectorySeparatorChar);
        if (trimmed.Length == 0)
        {
            error = "不允许把工作根设为文件系统根";
            return false;
        }

        var dangerous = ClassifyDangerous(trimmed, out var why);
        if (dangerous && !confirmUnsafe)
        {
            error = $"危险工作根（{why}）：默认拒绝，避免 AI 生成的路径覆写真实内容。确认要用的话请带 confirmUnsafe=true。";
            return false;
        }

        if (Directory.Exists(trimmed) && File.Exists(Path.Combine(trimmed, "settings.json")) &&
            string.Equals(Path.GetFullPath(trimmed), PluginDataDir, StringComparison.OrdinalIgnoreCase))
        {
            error = "不允许把工作根设为插件自己的数据目录（会与设置文件同树）";
            return false;
        }

        var settings = ReadSettingsRaw() ?? new WorkspaceSettings();
        settings.Root = trimmed;
        settings.Dangerous = dangerous;
        settings.UpdatedAt = DateTimeOffset.Now.ToString("O");
        if (!WriteSettings(settings, out var writeError))
        {
            error = writeError;
            return false;
        }

        lock (_sync)
        {
            _cached = new WorkspaceInfo(trimmed, Directory.Exists(trimmed), DefaultRoot, "settings", dangerous);
        }

        info = _cached;
        NewLife.Log.XTrace.Log.Info("[tool-bridge] 工作根已设置为 {0}（dangerous={1}）", trimmed, dangerous);
        return true;
    }

    /// <summary>
    /// 把相对路径解析为工作根内的绝对路径；越界/绝对路径一律拒并给原因（AC6）。
    /// </summary>
    public bool TryResolve(string? relative, out string full, out string? error)
    {
        var root = Current().Root.TrimEnd(Sep);
        full = root;
        error = null;

        var normalized = (relative ?? string.Empty).Replace('\\', Sep).Replace('/', Sep).Trim();
        if (normalized.Length == 0) return true;

        if (Path.IsPathRooted(normalized))
        {
            error = $"path/cwd 必须是工作根内的相对路径，不接受绝对路径（收到「{relative}」）";
            return false;
        }

        if (normalized.Split(Sep).Contains("..", StringComparer.Ordinal))
        {
            error = $"path/cwd 不得含 .. 段（收到「{relative}」）";
            return false;
        }

        string combined;
        try
        {
            combined = Path.GetFullPath(Path.Combine(root, normalized));
        }
        catch (Exception ex)
        {
            error = $"路径非法：{ex.Message}";
            return false;
        }

        var prefix = root + Sep;
        if (!string.Equals(combined, root, StringComparison.OrdinalIgnoreCase) &&
            !combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            error = $"越出工作根：目标「{combined}」不在「{root}」内";
            return false;
        }

        full = combined;
        return true;
    }

    /// <summary>
    /// 把绝对路径回写成相对工作根的展示形式。
    /// 工作根自身回空串（不是绝对路径）——回粘文本与台账里都不得出现本机绝对路径（FR-1.3 同一口径）。
    /// </summary>
    public string ToRelative(string full)
    {
        var root = Current().Root.TrimEnd(Sep);
        if (string.Equals(full.TrimEnd(Sep), root, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return full.StartsWith(root + Sep, StringComparison.OrdinalIgnoreCase)
            ? full[(root.Length + 1)..].Replace(Sep, '/')
            : full;
    }

    private WorkspaceInfo Load()
    {
        var settings = ReadSettingsRaw();
        if (settings != null && !string.IsNullOrWhiteSpace(settings.Root))
        {
            var root = settings.Root;
            EnsureDirectory(root);
            return new WorkspaceInfo(root, Directory.Exists(root), DefaultRoot, "settings",
                settings.Dangerous || ClassifyDangerous(root, out _));
        }

        EnsureDirectory(DefaultRoot);
        return new WorkspaceInfo(DefaultRoot, true, DefaultRoot, "default", false);
    }

    private static bool ClassifyDangerous(string full, out string why)
    {
        why = string.Empty;
        var rootPath = Path.GetPathRoot(full)?.TrimEnd(Sep);
        if (string.Equals(full.TrimEnd(Sep), rootPath, StringComparison.OrdinalIgnoreCase) && rootPath?.Length > 0)
        {
            why = "盘符/文件系统根";
            return true;
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile) &&
            string.Equals(full.TrimEnd(Sep), profile.TrimEnd(Sep), StringComparison.OrdinalIgnoreCase))
        {
            why = "用户目录根本身";
            return true;
        }

        // 仓库树内（含仓库根）：向上走一层层找 .git
        var dir = new DirectoryInfo(full);
        for (var d = dir; d != null; d = d.Parent)
        {
            if (Directory.Exists(Path.Combine(d.FullName, ".git")) || File.Exists(Path.Combine(d.FullName, ".git")))
            {
                why = $"位于 Git 仓库树内（{d.FullName}）";
                return true;
            }
        }

        return false;
    }

    private WorkspaceSettings? ReadSettingsRaw()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return null;
            var json = File.ReadAllText(_settingsPath, System.Text.Encoding.UTF8);
            return JsonSerializer.Deserialize<WorkspaceSettings>(json, SettingsOptions);
        }
        catch (Exception ex)
        {
            // 设置读不到不炸插件：回落默认根，但必须留痕（错误处理表）。
            NewLife.Log.XTrace.Log.Warn("[tool-bridge] settings.json 读取失败，回落默认工作根：{0}", ex.Message);
            return null;
        }
    }

    private bool WriteSettings(WorkspaceSettings settings, out string? error)
    {
        error = null;
        var temp = _settingsPath + ".tmp";
        try
        {
            Directory.CreateDirectory(PluginDataDir);
            // 临时文件 + 改名：避免半截 JSON（02-spec BR-6）。
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, SettingsOptions), NoBomUtf8);
            File.Move(temp, _settingsPath, true);
            return true;
        }
        catch (Exception ex)
        {
            error = $"工作根设置落盘失败：{ex.Message}";
            return false;
        }
    }

    private static void EnsureDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
        catch (Exception ex)
        {
            NewLife.Log.XTrace.Log.Warn("[tool-bridge] 工作根目录创建失败 {0}: {1}", path, ex.Message);
        }
    }

    private static readonly JsonSerializerOptions SettingsOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    internal static readonly System.Text.Encoding NoBomUtf8 = new System.Text.UTF8Encoding(false);

    private sealed class WorkspaceSettings
    {
        public string Root { get; set; } = string.Empty;
        public bool Dangerous { get; set; }
        public string UpdatedAt { get; set; } = string.Empty;
    }
}
