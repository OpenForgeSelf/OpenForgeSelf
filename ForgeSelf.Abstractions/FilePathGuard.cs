namespace ForgeSelf.Abstractions;

/// <summary>
/// 文件工具与项目工作区共用的路径护栏：目录边界、穿越、符号链接/目录联接、大小上限。
/// 越界一律失败返回，不改写路径、不截断内容。
/// </summary>
public static class FilePathGuard
{
    /// <summary>单文件读写上限（1 MiB）。超过则拒绝，不截断、不落盘。</summary>
    public const long MaxFileBytes = 1_048_576;

    /// <summary>
    /// 把 <paramref name="candidate"/> 规范化后确认落在 <paramref name="boundaryDirectory"/> 之内（含边界自身）。
    /// </summary>
    public static bool TryContain(string boundaryDirectory, string candidate, out string fullPath, out string? error)
    {
        fullPath = string.Empty;
        try
        {
            fullPath = Path.GetFullPath(candidate);
        }
        catch (Exception ex)
        {
            error = "路径非法：" + ex.Message;
            return false;
        }

        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var rest = fullPath.Length > root.Length ? fullPath[root.Length..] : string.Empty;
        if (rest.Contains(':') || rest.Contains('\0') || rest.IndexOfAny(['*', '?', '"', '<', '>', '|']) >= 0)
        {
            error = "路径含非法字符，已拒绝：" + fullPath;
            fullPath = string.Empty;
            return false;
        }

        string boundary;
        try
        {
            boundary = Path.GetFullPath(boundaryDirectory);
        }
        catch (Exception ex)
        {
            error = "工作目录非法：" + ex.Message;
            fullPath = string.Empty;
            return false;
        }

        boundary = boundary.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var prefix = boundary + Path.DirectorySeparatorChar;
        if (!trimmed.Equals(boundary, StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            error = "路径越出工作目录边界：" + trimmed + "（cwd=" + boundary + "）";
            fullPath = string.Empty;
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// 从 <paramref name="fullPath"/> 沿已存在的祖先走到 <paramref name="boundaryDirectory"/>（含两端）。
    /// 任一节点是符号链接或目录联接即拒绝。节点不存在视为普通缺失，不跟随。
    /// </summary>
    public static bool TryRejectReparseChain(string boundaryDirectory, string fullPath, out string? error)
    {
        string boundary;
        string current;
        try
        {
            boundary = Path.GetFullPath(boundaryDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            current = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex)
        {
            error = "路径非法：" + ex.Message;
            return false;
        }

        while (true)
        {
            if (!TryInspectReparse(current, out var isReparse, out var inspectError))
            {
                error = inspectError;
                return false;
            }

            if (isReparse)
            {
                error = "拒绝符号链接或目录联接：" + current;
                return false;
            }

            if (current.Equals(boundary, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent.Equals(current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var parentTrim = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var prefix = boundary + Path.DirectorySeparatorChar;
            if (!parentTrim.Equals(boundary, StringComparison.OrdinalIgnoreCase)
                && !parentTrim.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parentTrim;
        }

        error = null;
        return true;
    }

    /// <summary>字节数超过 <see cref="MaxFileBytes"/> 时拒绝。</summary>
    public static bool TryLimitBytes(long byteCount, string what, out string? error)
    {
        if (byteCount > MaxFileBytes)
        {
            error = what + " 超过文件大小上限 " + MaxFileBytes + " 字节（实际 " + byteCount + "），已拒绝";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryInspectReparse(string path, out bool isReparse, out string? error)
    {
        isReparse = false;
        error = null;
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(trimmed);
        var name = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            return true;
        }

        if (!Directory.Exists(parent))
        {
            return true;
        }

        try
        {
            foreach (var entry in new DirectoryInfo(parent).EnumerateFileSystemInfos(name))
            {
                if (!entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                isReparse = (entry.Attributes & FileAttributes.ReparsePoint) != 0;
                return true;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = "无法确认路径是否为符号链接或目录联接：" + trimmed + "（" + ex.Message + "）";
            return false;
        }
    }
}
