namespace ForgeSelf.Api.Plugins.ToolBridge.Services;

/// <summary>
/// 工具面唯一真源（PILOT-053 02-spec FR-1.2）：初始提示词、解析别名、界面清单三处均由本表派生，
/// 不再出现第二份手写清单。新增工具只改本表 + ToolDispatcher 的执行分派。
/// </summary>
public static class ToolSpec
{
    /// <summary>协议世代标记：写进初始提示词，AI 回包里出现它即可判定格式世代（03-plan 扩展性四问 4）。</summary>
    public const string SpecVersion = "toolbridge-spec v1";

    public const string ReadFile = "read_file";
    public const string WriteFile = "write_file";
    public const string ListDir = "list_dir";
    public const string RunCommand = "run_command";

    /// <summary>输入文本上限（02-spec Input 表）。</summary>
    public const int MaxTextBytes = 200 * 1024;

    /// <summary>读/写文件字节上限（NFR-2）。</summary>
    public const int MaxFileBytes = 1024 * 1024;

    /// <summary>列目录条目上限，超出标 truncated（FR-3.3）。</summary>
    public const int MaxListEntries = 500;

    /// <summary>命令输出与超时上限：与内置 agent 的 TerminalCommandGuard 同值（决策 D1「不放宽」）。</summary>
    public const int MaxOutputBytes = 50 * 1024;

    public const int MaxTimeoutSeconds = 30;

    public static IReadOnlyList<ToolSpecEntry> All { get; } =
    [
        new(ReadFile,
            "读取工作目录内的 UTF-8 文本文件，返回文件内容原文。",
            """
            {
              "type": "object",
              "properties": {
                "path": { "type": "string", "description": "相对工作目录的文件路径，如 notes/todo.md" }
              },
              "required": ["path"]
            }
            """,
            ["read_file", "readfile", "read", "cat", "get_file", "open_file"],
            ["path"]),

        new(WriteFile,
            "在工作目录内写一个文本文件（默认覆盖，append=true 追加）；父目录自动创建。",
            """
            {
              "type": "object",
              "properties": {
                "path": { "type": "string", "description": "相对工作目录的文件路径" },
                "content": { "type": "string", "description": "要写入的文本内容，允许空串" },
                "append": { "type": "boolean", "description": "true=追加到文件末尾；默认 false=覆盖" }
              },
              "required": ["path", "content"]
            }
            """,
            ["write_file", "writefile", "write", "create_file", "save_file", "put_file"],
            ["path", "content"]),

        new(ListDir,
            "列出工作目录内某个子目录的条目（目录优先、按名字排序）。",
            """
            {
              "type": "object",
              "properties": {
                "path": { "type": "string", "description": "相对工作目录的目录路径；省略或空串表示工作目录根" }
              },
              "required": []
            }
            """,
            ["list_dir", "listdir", "ls", "list_files", "list_directory", "dir", "read_dir"],
            []),

        new(RunCommand,
            $"在工作目录内执行一条命令。首 token 必须在白名单（{string.Join("/", CommandGuard.DefaultAllowlist)}），" +
            "管道/分号/换行与删除格式化下载类命令一律被拒绝（拒绝时不启动任何子进程，原因原样回给你）。" +
            "非 0 退出码属正常回传，失败输出同样是有效观察数据。",
            """
            {
              "type": "object",
              "properties": {
                "command": { "type": "string", "description": "完整命令行，如 git status --short；不得含管道与链式分隔符" },
                "cwd": { "type": "string", "description": "工作目录内的相对子目录；省略=工作根" },
                "timeoutSeconds": { "type": "integer", "description": "超时秒数 1-30，默认 30；超时后进程在后台继续" }
              },
              "required": ["command"]
            }
            """,
            ["run_command", "runcmd", "run", "exec", "execute", "execute_command", "shell", "terminal", "run_terminal_command"],
            ["command"])
    ];

    /// <summary>箭头式（P4）固定映射：> exec: git status / > write: a.txt（02-spec FR-2.2 P4）。</summary>
    public static IReadOnlyDictionary<string, string> ArrowMap { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["exec"] = RunCommand,
        ["run"] = RunCommand,
        ["command"] = RunCommand,
        ["read"] = ReadFile,
        ["write"] = WriteFile,
        ["ls"] = ListDir,
        ["list"] = ListDir
    };

    public static ToolSpecEntry? Find(string canonicalName) =>
        All.FirstOrDefault(a => string.Equals(a.Name, canonicalName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 把 AI 写的名字归一成规范名。只做「大小写 + 分隔符剥离」的规范化与别名表精确比对，
    /// **不做前缀/包含/相似度匹配**（02-spec BR-1；AC5 反向探针钉住这条）。
    /// </summary>
    public static string? Normalize(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return null;
        var key = Squeeze(rawName);
        if (key.Length == 0) return null;
        foreach (var entry in All)
        {
            if (string.Equals(Squeeze(entry.Name), key, StringComparison.Ordinal)) return entry.Name;
            foreach (var alias in entry.Aliases)
            {
                if (string.Equals(Squeeze(alias), key, StringComparison.Ordinal)) return entry.Name;
            }
        }
        return null;
    }

    /// <summary>未知工具名的候选提示（编辑距离最小者；仅提示，绝不代跑）。</summary>
    public static string? Suggest(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return null;
        var key = Squeeze(rawName);
        if (key.Length == 0) return null;

        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var entry in All)
        {
            var d = Levenshtein(key, Squeeze(entry.Name));
            if (d < bestDistance)
            {
                bestDistance = d;
                best = entry.Name;
            }
        }
        return bestDistance <= 3 ? best : null;
    }

    private static string Squeeze(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }
}

/// <summary>工具面清单项（名称/说明/JSON Schema/别名/必填）。</summary>
public sealed record ToolSpecEntry(
    string Name,
    string Description,
    string ParametersSchema,
    string[] Aliases,
    string[] Required);
