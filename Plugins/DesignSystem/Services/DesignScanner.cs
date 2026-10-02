using System.Text.RegularExpressions;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>一条待审声明：property 已小写化、value 为原文（规则层再细分值）</summary>
public sealed record Declaration(String File, Int32 Line, Int32 Column, String Property, String Value, String? Selector);

/// <summary>被跳过的文件（C0：empty / binary / unknown-language / regex-timeout）</summary>
public sealed record SkippedFile(String File, String Reason);

/// <summary>
/// C1 抽取器：把文件内容变成 <see cref="Declaration"/> 序列（纯函数，不碰库）。
/// 各语言抽取规则见 03-plan §C1：CSS/SCSS/LESS 状态机逐字符跳注释/字符串/url()；
/// Vue/HTML 提取 `&lt;style&gt;` 块 + `style="…"` 内联 + Tailwind 任意值；TSX/JSX 提取标签模板 +
/// style/sx 对象字面量（仅 `key: 字面量` 两种形态，宁漏勿误）+ Tailwind 任意值。
/// </summary>
public static class DesignScanner
{
    public const Int32 MaxFiles = 200;
    public const Int32 MaxBytes = 204_800;

    static readonly String[] KnownExts =
    [".css", ".scss", ".less", ".vue", ".html", ".htm", ".tsx", ".jsx", ".ts", ".js", ".mjs"];

    static readonly Regex TailwindRegex = new(
        @"(?<![\w-])(?:[a-z0-9-]+:)*(bg|text|border|ring|fill|stroke|p[xytrbl]?|m[xytrbl]?|gap|rounded(?:-[a-z]+)?|shadow)-\[([^\]\s]+)\](?![\w-])",
        RegexOptions.Compiled);

    static readonly Regex StyleAttrRegex = new("style\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);
    static readonly Regex StyleBlockRegex = new("<style[^>]*>([\\s\\S]*?)</style>", RegexOptions.Compiled);

    /// <summary>推断语言：扩展名 → 语言；未知返回 null（→ unknown-language 跳过）</summary>
    public static String? InferLanguage(String path, String? explicitLanguage)
    {
        if (!explicitLanguage.IsNullOrEmpty()) return explicitLanguage.ToLowerInvariant();
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".sass" || ext == ".styl") return null;
        return KnownExts.Contains(ext) ? ext.TrimStart('.') : null;
    }

    /// <summary>扫描一个文件；返回 Declaration 列表（Notes 记录覆盖边界说明）</summary>
    public static List<Declaration> Scan(String file, String content, String language, List<String> notes)
    {
        var decls = new List<Declaration>();
        switch (language)
        {
            case "css":
            case "scss":
            case "less":
                ScanCss(decls, content, 1, 1, file, language);
                break;
            case "vue":
            case "html":
                ScanMarkup(decls, content, file, language, notes);
                break;
            case "tsx":
            case "jsx":
            case "ts":
            case "js":
                ScanTs(decls, content, file, notes);
                break;
            default:
                return decls;
        }

        // Tailwind 任意值：对 vue/html/tsx/jsx/ts/js 全文匹配
        if (language is "vue" or "html" or "tsx" or "jsx" or "ts" or "js")
        {
            foreach (Match m in TailwindRegex.Matches(content))
            {
                var cls = m.Groups[1].Value;
                var inner = m.Groups[2].Value.Trim();
                var (line, col) = PositionOf(content, m.Index);
                decls.Add(new Declaration(file, line, col, "tailwind:" + cls, inner, null));
            }
        }
        return decls;
    }

    // ---- CSS 状态机 ----

    static void ScanCss(List<Declaration> decls, String content, Int32 startLine, Int32 startCol, String file, String language)
    {
        var i = 0;
        var line = startLine;
        var col = startCol;
        var depth = 0;
        var selector = (String?)null;
        var sb = new System.Text.StringBuilder();
        var sbStart = (Line: 0, Col: 0);        // 当前累积段首个非空白字符位置（声明 prop 起始）
        var fontFace = false;
        var skipAtRule = false;     // @media 等前导文本不产出声明，花括号内按规则块解析
        var inLineComment = language != "css"; // scss/less 支持 //
        var inUrl = false;
        var urlDepth = 0;
        var inString = (Char?)null;

        void FlushSegment()
        {
            sb.Clear();
            sbStart = (0, 0);
        }

        // 声明结束：把 sb 解析为 prop: value（仅在块内、非 fontFace、非变量定义）
        void EmitDeclaration()
        {
            var text = sb.ToString().Trim();
            var (sLine, sCol) = sbStart;
            FlushSegment();
            if (depth < 1 || fontFace || text.Length == 0 || selector == null) return;
            // 跳过 at-rule 自身的声明（@media 等前导段不是声明）
            if (skipAtRule && depth == 1) { skipAtRule = false; return; }
            var colon = text.IndexOf(':');
            if (colon <= 0) return;
            var prop = text[..colon].Trim();
            var value = text[(colon + 1)..].Trim().TrimEnd(';').Trim();
            if (prop.Length == 0 || value.Length == 0) return;
            // scss $x: / less @x: 定义跳过；自定义属性 --x: 是定义
            if (prop.StartsWith('$') || prop.StartsWith('@')) return;
            decls.Add(new Declaration(file, sLine, sCol,
                prop.StartsWith("--", StringComparison.Ordinal) ? prop : prop.ToLowerInvariant(), value, selector));
        }
        while (i < content.Length)
        {
            var c = content[i];
            var next = i + 1 < content.Length ? content[i + 1] : '\0';

            if (inString is { } q)
            {
                sb.Append(c);
                if (c == q && content[i - 1] != '\\') inString = null;
                i++; line = c == '\n' ? line + 1 : line; col = c == '\n' ? 1 : col + 1;
                continue;
            }
            if (inUrl)
            {
                if (c == '(') urlDepth++;
                else if (c == ')')
                {
                    urlDepth--;
                    if (urlDepth <= 0) inUrl = false;
                }
                i++; line = c == '\n' ? line + 1 : line; col = c == '\n' ? 1 : col + 1;
                continue;
            }

            if (c == '/' && next == '*')
            {
                i += 2; col += 2;
                while (i < content.Length && !(content[i] == '*' && i + 1 < content.Length && content[i + 1] == '/'))
                {
                    if (content[i] == '\n') { line++; col = 1; } else col++;
                    i++;
                }
                if (i < content.Length) { i += 2; col += 2; }
                continue;
            }
            if (inLineComment && c == '/' && next == '/')
            {
                while (i < content.Length && content[i] != '\n')
                {
                    i++; col++;
                }
                continue;
            }
            if (c == '\'' || c == '"') { inString = c; sb.Append(c); i++; col++; continue; }
            if (c == 'u' && next == 'r' && i + 2 < content.Length && content[i + 2] == 'l' && i + 3 < content.Length && content[i + 3] == '(')
            {
                inUrl = true; urlDepth = 1;
                i += 4; col += 4;
                continue;
            }

            if (c == '{')
            {
                var selText = sb.ToString().Trim();
                if (depth == 0)
                {
                    if (selText.StartsWith("@font-face", StringComparison.OrdinalIgnoreCase))
                    {
                        fontFace = true;
                        selector = null;
                    }
                    else
                    {
                        fontFace = false;
                        selector = selText;
                    }
                    skipAtRule = selText.StartsWith('@') && !selText.StartsWith("@keyframes", StringComparison.OrdinalIgnoreCase)
                        && !fontFace;
                    // @keyframes 名不报（C2），其花括号内是 keyframe 块：selector 记为 null 不判
                    if (selText.StartsWith("@keyframes", StringComparison.OrdinalIgnoreCase))
                        selector = null;
                }
                depth++;
                FlushSegment();
                i++; col++;
                continue;
            }
            if (c == '}')
            {
                EmitDeclaration();
                depth = Math.Max(0, depth - 1);
                if (depth == 0) { selector = null; fontFace = false; skipAtRule = false; }
                FlushSegment();
                i++; col++;
                continue;
            }
            if (c == ';' && depth >= 1)
            {
                EmitDeclaration();
                i++; col++;
                continue;
            }

            if (sbStart.Line == 0 && !Char.IsWhiteSpace(c)) sbStart = (line, col);
            sb.Append(c);
            i++; line = c == '\n' ? line + 1 : line; col = c == '\n' ? 1 : col + 1;
        }
        // 文件尾无分号的最后一条声明
        if (sb.Length > 0 && depth >= 1) EmitDeclaration();
    }

    // ---- Vue / HTML ----

    static void ScanMarkup(List<Declaration> decls, String content, String file, String language, List<String> notes)
    {
        // <style> 块（含 scoped / lang=scss|less）
        foreach (Match m in StyleBlockRegex.Matches(content))
        {
            var tag = content[..m.Index];
            var blockLine = 1 + CountLines(tag);
            var lang = m.Value.StartsWith("<style", StringComparison.OrdinalIgnoreCase)
                ? (Regex.Match(m.Value, "lang\\s*=\\s*[\"']?([a-zA-Z]+)").Groups[1].Value.ToLowerInvariant() switch
                {
                    "scss" => "scss",
                    "less" => "less",
                    _ => "css",
                }) : "css";
            var body = m.Groups[1].Value;
            ScanCss(decls, body, blockLine, 1, file, lang);
        }

        // style="…" 内联
        foreach (Match m in StyleAttrRegex.Matches(content))
        {
            var (line, col) = PositionOf(content, m.Groups[1].Index);
            ScanInlineStyle(decls, m.Groups[1].Value, line, col, file);
        }

        // :style="{…}" 对象（vue/html 也常见）
        ScanJsStyleObject(decls, content, file);

        if (content.Contains("style=\"", StringComparison.Ordinal) && language == "html" && notes.Count == 0)
            notes.Add("HTML 内联 style 与 Tailwind 任意值已抽取；动态样式（JS 计算）不识别");
    }

    static void ScanInlineStyle(List<Declaration> decls, String style, Int32 line, Int32 col, String file)
    {
        foreach (var part in style.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = part.IndexOf(':');
            if (colon <= 0) continue;
            var prop = part[..colon].Trim().ToLowerInvariant();
            var value = part[(colon + 1)..].Trim();
            if (prop.Length == 0 || value.Length == 0) continue;
            decls.Add(new Declaration(file, line, col, prop, value, null));
        }
    }

    // ---- TSX / JSX / TS / JS ----

    static void ScanTs(List<Declaration> decls, String content, String file, List<String> notes)
    {
        // 标签模板：css`…` / styled.x`…` / styled(X)`…` / createGlobalStyle`…` / keyframes`…`
        foreach (Match m in TaggedTemplateRegex.Matches(content))
        {
            var body = m.Groups[1].Value;
            var (line, col) = PositionOf(content, m.Index);
            // ${…} 插值 → 占位 __expr__；值含占位的声明跳过
            var normalized = ExprRegex.Replace(body, "__expr__");
            var sub = new List<Declaration>();
            // styled/css 模板内容是裸声明（无花括号），包一层块以便状态机识别；'{' 与被包装内容同起行
            ScanCss(sub, "{" + normalized + "\n}", line, 1, file, "css");
            foreach (var d in sub)
            {
                if (d.Value.Contains("__expr__", StringComparison.Ordinal)) continue;
                decls.Add(d);
            }
        }

        // JSX style={{…}} / sx={{…}}：括号配对取对象体，仅识别 key: '字面量' / key: 数字
        ScanJsStyleObject(decls, content, file);

        // 覆盖边界说明
        if (notes.Count == 0 && (content.Contains("styled", StringComparison.Ordinal) || content.Contains("css`", StringComparison.Ordinal)))
            notes.Add("TSX/JSX 仅识别 css 标签模板与 style/sx 对象字面量；动态样式与展开运算符不识别（宁漏勿误）");
    }

    static readonly Regex TaggedTemplateRegex = new(
        @"(?:\bcss|styled(?:\.\w+|\s*\(\s*\w+\s*\))|createGlobalStyle|keyframes)\s*`([^`]*)`",
        RegexOptions.Compiled);
    static readonly Regex ExprRegex = new(@"\$\{[^}]*\}", RegexOptions.Compiled);

    /// <summary>style={{…}} / sx={{…}}：括号配对取对象体；仅 `key: '字面量'` / `key: 数字` 两种形态</summary>
    static void ScanJsStyleObject(List<Declaration> decls, String content, String file)
    {
        var re = new Regex(@"(?:style|sx)\s*=\s*\{\s*\{\s*([\s\S]*?)\s*\}\s*\}", RegexOptions.Compiled);
        foreach (Match m in re.Matches(content))
        {
            var body = m.Groups[1].Value;
            var (line, col) = PositionOf(content, m.Index);
            foreach (Match kv in JsObjectPairRegex.Matches(body))
            {
                var key = kv.Groups[1].Value.Trim().Trim('"', '\'', '\u0060');
                var value = kv.Groups[2].Success ? kv.Groups[2].Value
                    : kv.Groups[3].Success ? kv.Groups[3].Value : kv.Groups[4].Value;
                var prop = ToKebab(key);
                if (prop.Length == 0) continue;
                // 数字属性仅特定集视为 px（驼峰转 kebab 后按关键词判定，覆盖四角/复合写法）
                if (kv.Groups[4].Success
                    && (prop.Contains("padding") || prop.Contains("margin") || prop.Contains("gap")
                        || prop.Contains("radius") || prop.Contains("border-width") || prop.Contains("font-size"))
                    && Double.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var num))
                {
                    value = num.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "px";
                }
                decls.Add(new Declaration(file, line, col, prop, value, null));
            }
        }
    }

    static readonly Regex JsObjectPairRegex = new(
        @"['""`]?([A-Za-z][A-Za-z0-9-]*)['""`]?\s*:\s*(?:'([^']*)'|""([^""]*)""|(-?\d+(?:\.\d+)?))");

    static String ToKebab(String key)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in key)
        {
            if (Char.IsUpper(c)) sb.Append('-').Append(Char.ToLowerInvariant(c));
            else sb.Append(c);
        }
        return sb.ToString().ToLowerInvariant();
    }

    // ---- 工具 ----

    public static (Int32 Line, Int32 Col) PositionOf(String content, Int32 index)
    {
        var line = 1;
        var col = 1;
        for (var i = 0; i < index && i < content.Length; i++)
        {
            if (content[i] == '\n') { line++; col = 1; }
            else col++;
        }
        return (line, col);
    }

    static Int32 CountLines(String s)
    {
        var n = 0;
        foreach (var c in s) if (c == '\n') n++;
        return n;
    }

    /// <summary>文件级跳过判定（C0）：empty / binary；未知语言由调用方先判</summary>
    public static SkippedFile? SkipReason(String file, String? content, String? language)
    {
        if (content == null || content.Trim().Length == 0) return new SkippedFile(file, "empty");
        if (content.Contains('\0')) return new SkippedFile(file, "binary");
        if (language == null) return new SkippedFile(file, "unknown-language");
        return null;
    }
}
