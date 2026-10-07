namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 项目路径归一器（PILOT-054 · BR-3 的唯一实现点）。
///
/// 决策背景：宿主 <c>HostProjectRegistry.Register</c> 只做 <c>Path.GetFullPath</c> +
/// <c>Project.FindByRoot</c> 精确匹配，在 Windows 上 Git-Bash 风格 <c>/d/project</c> 会被解析成
/// <c>C:\d\project</c> → <c>Directory.Exists</c> 为假 → 直接「目录不存在」，根本登记不上。
/// 用户拍板「插件内部支持多种格式」，故归一落在本插件，宿主注册表与 Project 表结构零改动。
///
/// 同一性判据：<see cref="Result.Key"/> 相等即同一项目（Windows 路径大小写不敏感 ⇒ Key = 归一根大写）。
/// 与宿主比对时必须对宿主返回的 <c>ProjectInfo.Root</c> <b>再跑一次</b> Normalize（宿主可能存的是别的写法），
/// 见 <see cref="KeyOf"/>。
/// </summary>
public static class ProjectPathCanonicalizer
{
    /// <summary>归一结果：Ok=是否成功，Root=归一后的路径，Key=同一性判据（Root 大写），Error=失败原因原文。</summary>
    public readonly record struct Result(bool Ok, string Root, string Key, string? Error)
    {
        /// <summary>成功。</summary>
        public static Result Success(string root) => new(true, root, root.ToUpperInvariant(), null);

        /// <summary>失败（原因必须可自证成因，便于用户改写法）。</summary>
        public static Result Failed(string error) => new(false, string.Empty, string.Empty, error);
    }

    /// <summary>最长可存列宽（Model.xml 的 ProjectRoot/ProjectPathRaw Length=500）。</summary>
    public const int MaxLength = 500;

    private static readonly char Sep = Path.DirectorySeparatorChar;

    /// <summary>
    /// 归一一条项目路径。支持：<c>D:\proj</c>、<c>D:/proj</c>、<c>D:\proj\</c>、
    /// Git-Bash <c>/d/proj</c>、WSL <c>/mnt/d/proj</c>、<c>~/code/proj</c>、UNC <c>\\nas\share\proj</c>，
    /// 并折叠 <c>.</c> / <c>..</c> / 重复分隔符。
    /// </summary>
    public static Result Normalize(string? raw)
    {
        var text = Clean(raw);
        if (text.Length == 0) return Result.Failed("项目路径不能为空");

        // 规则 2：~ 展开
        text = ExpandHome(text);

        // 规则 3/4：MSYS /d/x 与 WSL /mnt/d/x → 盘符式（仅 Windows；POSIX 下这些就是普通路径）
        if (OperatingSystem.IsWindows()) text = TranslateUnixDriveForm(text);

        // 规则 11：必须在 GetFullPath <b>之前</b>判"是不是绝对路径"。
        // GetFullPath("relative") 会拼上进程当前目录、GetFullPath("/etc/passwd") 会拼上当前盘符，
        // 先折叠再判就永远判不出"相对"了 —— 实测这正是不该被接受的路径被放行的原因。
        if (OperatingSystem.IsWindows() && !IsAbsoluteWindowsForm(text))
            return Result.Failed($"无法确定盘符，请给出绝对路径（如 D:\\project 或 /d/project）：{raw}");

        // 规则 5/6：分隔符统一 + GetFullPath 折叠 . 与 ..
        if (OperatingSystem.IsWindows()) text = text.Replace('/', Sep);

        string full;
        try
        {
            full = Path.GetFullPath(text);
        }
        catch (Exception ex)
        {
            return Result.Failed($"路径非法：{ex.Message}");
        }

        // 规则 7：去 \\?\ 扩展前缀
        if (full.StartsWith(@"\\?\", StringComparison.Ordinal)) full = full[4..];

        // 规则 11：Windows 下必须落到带盘符的绝对路径。
        // "/etc/passwd" 这类"根但无盘符"在 .NET 里会被解析成「当前盘\x」，静默指错目录比报错更糟 ⇒ 直接拒。
        // 兜底：折叠后仍必须落在带盘符的位置上（UNC 例外，它本来就没有盘符）。
        if (OperatingSystem.IsWindows() && !HasDriveLetter(full) && !full.StartsWith(@"\\", StringComparison.Ordinal))
            return Result.Failed($"无法确定盘符，请给出绝对路径（如 D:\\project 或 /d/project）：{raw}");

        // 规则 7/8：去尾分隔符（盘符根与 UNC 根保留）+ 盘符大写
        var root = TrimTrailing(full);
        if (root.Length == 0) return Result.Failed($"路径非法：{raw}");

        // 规则 13：长度上限（与列宽一致，越界会在写库时被实体校验挡下，这里提前给出人话）
        if (root.Length > MaxLength)
            return Result.Failed($"路径长度不能超过 {MaxLength} 字符（当前 {root.Length}）");

        return Result.Success(root);
    }

    /// <summary>取某条路径的同一性 Key；归一失败返回 null（调用方按「无法判定」处理，不得当成 Key 相等）。</summary>
    public static string? KeyOf(string? path)
    {
        var result = Normalize(path);
        return result.Ok ? result.Key : null;
    }

    /// <summary>两条路径是否指向同一个项目（各自归一后比 Key；任一归一失败即 false）。</summary>
    public static bool SameProject(string? a, string? b)
    {
        var ra = Normalize(a);
        var rb = Normalize(b);
        return ra.Ok && rb.Ok && string.Equals(ra.Key, rb.Key, StringComparison.Ordinal);
    }

    /// <summary>
    /// 拼接「项目根 + 项目内相对路径」并守住不越出项目根。
    /// 成功时 <see cref="Result.Root"/> = 归一后的绝对路径（注意这里不是项目根而是目标路径），
    /// <see cref="Result.Key"/> = 其大写形式。失败（相对路径含 .. 越界、绝对路径不在根内）给原因原文。
    /// </summary>
    public static Result ResolveInside(string projectRoot, string relative)
    {
        var root = Normalize(projectRoot);
        if (!root.Ok) return Result.Failed(root.Error!);

        var rel = (relative ?? string.Empty).Trim().Trim('"', '\'');
        if (rel.Length == 0) return Result.Failed("路径不能为空");
        if (OperatingSystem.IsWindows()) rel = rel.Replace('/', Sep);

        string combined;
        try
        {
            combined = Path.GetFullPath(Path.Combine(root.Root, rel));
        }
        catch (Exception ex)
        {
            return Result.Failed($"路径非法：{ex.Message}");
        }

        var prefix = root.Root.EndsWith(Sep) ? root.Root : root.Root + Sep;
        if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return Result.Failed($"越出项目根：{relative}（允许范围 {prefix}）");

        return new Result(true, combined, combined.ToUpperInvariant(), null);
    }

    /// <summary>规则 1：去首尾空白与包裹引号/反引号，并折叠连续分隔符（UNC 的前导两字符单独保住）。</summary>
    private static string Clean(string? raw)
    {
        var text = (raw ?? string.Empty).Trim().Trim('"', '\'', '`').Trim();
        if (text.Length == 0) return string.Empty;

        // UNC（\\server\share）折叠分隔符时会把前导两个分隔符压成一个，先摘出来再补回
        var isUnc = text.StartsWith(@"\\", StringComparison.Ordinal) || text.StartsWith("//", StringComparison.Ordinal);
        if (isUnc) text = text.TrimStart('\\', '/');

        text = CollapseRepeats(text, '/');
        text = CollapseRepeats(text, '\\');

        if (isUnc) text = @"\\" + text;
        return text;
    }

    private static string CollapseRepeats(string text, char ch)
    {
        var pair = new string(ch, 2);
        while (text.Contains(pair, StringComparison.Ordinal))
            text = text.Replace(pair, ch.ToString(), StringComparison.Ordinal);
        return text;
    }

    /// <summary>规则 2：前导 ~ 展开为用户目录。</summary>
    private static string ExpandHome(string text)
    {
        if (!(text == "~" || text.StartsWith("~/", StringComparison.Ordinal) ||
              text.StartsWith("~\\", StringComparison.Ordinal) || text.StartsWith("/~/", StringComparison.Ordinal)))
            return text;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) return text;

        var rest = text.TrimStart('/');
        rest = rest.Length > 1 ? rest[1..].TrimStart('/', '\\') : string.Empty;
        return rest.Length == 0 ? home : Path.Combine(home, rest.Replace('/', Sep));
    }

    /// <summary>
    /// 规则 3/4：把 Unix 风格盘符写法翻成 Windows 盘符。
    /// <c>/d/proj</c> → <c>D:\proj</c>；<c>/d</c> → <c>D:\</c>；<c>/mnt/d/proj</c> → <c>D:\proj</c>。
    /// 只认「单字母」段，<c>/etc/passwd</c> 一类不在这层被改写（留给规则 11 判成"无盘符"并拒绝）。
    /// </summary>
    private static string TranslateUnixDriveForm(string text)
    {
        if (text.Length == 0 || text[0] != '/') return text;

        var body = text[1..];
        if (body.StartsWith("mnt/", StringComparison.OrdinalIgnoreCase) && body.Length >= 5 && IsLetter(body[4]) &&
            (body.Length == 5 || body[5] == '/'))
            body = body[4..]; // 变成 "d/proj"

        if (body.Length < 1 || !IsLetter(body[0])) return text;
        if (!(body.Length == 1 || body[1] == '/')) return text;

        var tail = body.Length == 1 ? string.Empty : body[2..];
        var drive = char.ToUpperInvariant(body[0]);
        return tail.Length == 0 ? $"{drive}:{Sep}" : $"{drive}:{Sep}{tail}";
    }

    private static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

    /// <summary>
    /// Windows 下的绝对形式：带盘符（<c>D:\</c> 或 <c>D:/</c>）或 UNC（<c>\\server\</c> / <c>//server/</c>）。
    /// 单写 <c>D:x</c>（盘符相对）不算 —— 它依赖该盘的当前目录，指到哪没人知道。
    /// </summary>
    private static bool IsAbsoluteWindowsForm(string text) =>
        (text.Length >= 3 && text[1] == ':' && IsLetter(text[0]) && (text[2] == '\\' || text[2] == '/'))
        || (text.Length > 2 && (text.StartsWith(@"\\", StringComparison.Ordinal) || text.StartsWith("//", StringComparison.Ordinal)));

    private static bool HasDriveLetter(string path) =>
        path.Length >= 2 && path[1] == ':' && IsLetter(path[0]);

    /// <summary>规则 7/8：去尾分隔符（盘符根如 D:\ 保留），盘符大写。</summary>
    private static string TrimTrailing(string path)
    {
        var text = path;

        // 盘符根 / UNC 根：保留必要的尾分隔符
        var isDriveRoot = OperatingSystem.IsWindows() && text.Length == 3 && text[1] == ':' && text[2] == Sep;
        if (!isDriveRoot)
        {
            while (text.Length > 3 && (text.EndsWith(Sep) || text.EndsWith('/')))
                text = text[..^1];
            // 单字符根（POSIX "/"）不动
            if (text.Length == 0) text = Sep.ToString();
        }

        if (OperatingSystem.IsWindows() && text.Length >= 2 && text[1] == ':' && IsLetter(text[0]))
            text = char.ToUpperInvariant(text[0]) + text[1..];

        return text;
    }
}
