using System.Text.Json;

namespace ForgeSelf.Api.Plugins.FileTools;

/// <summary>文件三件套的路径/行号校验原语（FR-5：read/write 必填绝对路径；不再隐式接项目根）。</summary>
internal static class FileToolPathOps
{
    /// <summary>
    /// path 必须为绝对路径；合法时返回 true 并给出规范化绝对路径（out absPath），
    /// 否则返回 false + 中文引导错误（out error）。
    /// </summary>
    public static bool EnsureAbsolute(string? path, out string? absPath, out string? error)
    {
        absPath = null;
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "缺少必填参数 path：文件绝对路径（如 D:\\src\\my-proj\\src\\main.py）";
            return false;
        }
        if (!Path.IsPathRooted(path))
        {
            error = "path 必须为绝对路径（收到相对路径：" + path + "）；不再隐式接项目根，请传完整绝对路径";
            return false;
        }
        absPath = Path.GetFullPath(path);
        return true;
    }

    /// <summary>
    /// cwd 必填校验（2026-10-29 用户拍板：文件工具必传 cwd，彻底移除「未选择项目目录/未设置
    /// FORGESELF_DEFAULT_PROJECT_ROOT」隐式回退——工作区锚点一律由调用方显式给参数）。
    /// 必须为绝对路径且目录已存在；合法时返回 true + 规范化路径（out absCwd），否则 false + 中文必传引导。
    /// </summary>
    public static bool EnsureCwd(string? cwd, out string? absCwd, out string? error)
    {
        absCwd = null;
        error = null;
        if (string.IsNullOrWhiteSpace(cwd))
        {
            error = "缺少必填参数 cwd：工作目录绝对路径（如 D:\\src\\my-proj\\src）。"
                + "文件工具不再回退当前项目根/FORGESELF_DEFAULT_PROJECT_ROOT，cwd 必传";
            return false;
        }
        if (!Path.IsPathRooted(cwd))
        {
            error = "cwd 必须为绝对路径（收到相对路径：" + cwd + "）";
            return false;
        }
        absCwd = Path.GetFullPath(cwd);
        if (!Directory.Exists(absCwd))
        {
            error = "cwd 目录不存在：" + absCwd;
            absCwd = null;
            return false;
        }
        return true;
    }

    /// <summary>读路径存在性校验（read_file 用）。</summary>
    public static bool EnsureFileExists(string absPath, out string? error)
    {
        error = null;
        if (File.Exists(absPath))
        {
            return true;
        }
        error = "文件不存在：" + absPath;
        return false;
    }
}

/// <summary>read_file / write_file 共用的行处理原语（最小路径：统一按 \n 切分与回写，CRLF/CR 归一）。语义自 AIAgent ProjectFileToolFunctions.cs 原样迁入（2026-10-28）。</summary>
internal static class FileToolLineOps
{
    /// <summary>把文本按行切分（CRLF/CR 归一为 LF）。</summary>
    public static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    /// <summary>从参数 JSON 根节点读取必填的 start_line（正整数，1 基）；数字或数字字符串均可。缺失/非法时返回 false 并给出中文错误。</summary>
    public static bool TryReadStartLine(JsonElement root, out int startLine, out string? error)
    {
        startLine = 0;
        error = null;
        if (!root.TryGetProperty("start_line", out var el))
        {
            error = "缺少必填参数 start_line：起始行号（整数，从 1 开始），read_file 表示从该行读到文末，write_file 表示从该行起直接覆盖";
            return false;
        }
        var ok = el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt32(out startLine),
            JsonValueKind.String => int.TryParse(el.GetString(), out startLine),
            _ => false,
        };
        if (!ok || startLine < 1)
        {
            error = "start_line 必须为从 1 开始的正整数，收到：" + el.GetRawText();
            startLine = 0;
            return false;
        }
        return true;
    }

    /// <summary>
    /// 从参数 JSON 根节点读取 end_line（含，正整数且 ≥ startLine）。
    /// required=false（read_file）：缺省返回 null=到文末；required=true（write_file，先读后写行号必知）：缺失即报错。
    /// 非法时返回 false + 中文错误。
    /// </summary>
    public static bool TryReadEndLine(JsonElement root, int startLine, bool required, out int? endLine, out string? error)
    {
        endLine = null;
        error = null;
        if (!root.TryGetProperty("end_line", out var el))
        {
            if (required)
            {
                error = "缺少必填参数 end_line：结束行号（整数，含该行，从 1 开始）；write_file 必须先 read_file 读文件拿到行号，再用 start_line..end_line 指定要替换的区间";
                return false;
            }
            return true;
        }
        var ok = el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt32(out var v) && (endLine = v) == v,
            JsonValueKind.String => int.TryParse(el.GetString(), out var v) && (endLine = v) == v,
            _ => false,
        };
        if (!ok || endLine < 1)
        {
            error = "end_line 必须为从 1 开始的正整数，收到：" + el.GetRawText();
            endLine = null;
            return false;
        }
        if (endLine < startLine)
        {
            error = "end_line=" + endLine + " 不得小于 start_line=" + startLine;
            endLine = null;
            return false;
        }
        return true;
    }
}
