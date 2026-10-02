using System.Globalization;
using System.Text.RegularExpressions;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>审查输入文件（code+language 等价于单文件 path="snippet.&lt;ext&gt;"）</summary>
public sealed record ReviewInput(String Path, String Content, String? Language);

/// <summary>规则建议：可直接替换为的令牌（token/cssVar/value/replace）</summary>
public sealed record ReviewSuggestion(String Token, String CssVar, String Value, String Replace);

/// <summary>单条发现</summary>
public sealed record ReviewFinding(String File, Int32 Line, Int32 Column, String Rule, String Severity,
    String Property, String Found, String Message, ReviewSuggestion? Suggestion);

/// <summary>汇总（C4：不给自拟分数）</summary>
public sealed record ReviewSummary(Int32 Files, Int32 Declarations, Int32 Tokenized, Int32 Hardcoded,
    Double? TokenCoverage, Int32 Errors, Int32 Warnings, Int32 Infos, Boolean Passed, Boolean Strict);

/// <summary>
/// C3 规则引擎（纯函数）。输入已抽取的 Declaration + TokenIndex，产出 findings 与汇总。
/// 15 条规则逐条见 03-plan §C3；strict=true 时 warning→error（info 不变）。
/// </summary>
public sealed class DesignReviewer
{
    public const Int32 MaxFiles = DesignScanner.MaxFiles;
    public const Int32 MaxBytes = DesignScanner.MaxBytes;

    static readonly String[] NamedColors =
    ["aqua", "black", "blue", "fuchsia", "gray", "green", "lime", "maroon", "navy", "olive", "orange", "purple", "red", "silver", "teal", "white", "yellow"];

    static readonly Regex VarRefRegex = new("var\\(--ds-([\\w-]+)", RegexOptions.Compiled);
    static readonly Regex HexRegex = new(@"(?<![\w-])#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})(?![\w-])", RegexOptions.Compiled);
    static readonly Regex FuncColorRegex = new(@"(?:rgb|rgba|hsl|hsla|oklch)\([^)]*\)", RegexOptions.Compiled);
    static readonly Regex LengthTokenRegex = new(@"-?\d+(?:\.\d+)?(?:px|rem|em)?\b", RegexOptions.Compiled);
    static readonly Regex DurationTokenRegex = new(@"\d+(?:\.\d+)?(?:ms|s)\b", RegexOptions.Compiled);
    static readonly Regex TailwindVarRegex = new(@"var\(--ds-[\w-]+\)", RegexOptions.Compiled);

    /// <summary>最近令牌候选（规则建议用）</summary>
    sealed record Nearest(String Path, String CssVar, String Value, String Replace, Double Distance);

    /// <summary>已定义变量候选（unknown/removed/deprecated 建议用）</summary>
    sealed record DefinedCandidate(String Path, String CssVar, String Value);

    public sealed record Outcome(String? Error, ReviewSummary? Summary, IReadOnlyList<ReviewFinding> Findings,
        IReadOnlyList<SkippedFile> Skipped, Boolean Truncated, IReadOnlyList<String> Notes);

    public Outcome Review(TokenIndex index, IReadOnlyList<ReviewInput> inputs, Boolean strict, Int32 maxFindings)
    {
        var notes = new List<String>();

        // C0：整次限额（先于任何扫描）
        var files = inputs.Count;
        var totalBytes = inputs.Sum(i => System.Text.Encoding.UTF8.GetByteCount(i.Content ?? ""));
        if (files > MaxFiles || totalBytes > MaxBytes)
            return new Outcome($"审查输入过大（{files} 个文件 / {Math.Ceiling(totalBytes / 1024.0)} KB），上限 {MaxFiles} 个 / {MaxBytes / 1024} KB，请分批",
                null, [], [], false, notes);

        var findings = new List<ReviewFinding>();
        var skipped = new List<SkippedFile>();
        var fileOrder = new Dictionary<String, Int32>(StringComparer.Ordinal);
        var declarations = 0;
        var tokenized = 0;
        var hardcoded = 0;

        for (var fi = 0; fi < inputs.Count; fi++)
        {
            var input = inputs[fi];
            var path = input.Path;
            fileOrder[path] = fi;

            var language = DesignScanner.InferLanguage(path, input.Language);
            var skip = DesignScanner.SkipReason(path, input.Content, language);
            if (skip != null) { skipped.Add(skip); continue; }

            var decls = new List<Declaration>();
            try
            {
                decls = DesignScanner.Scan(path, input.Content!, language!, notes);
            }
            catch (RegexMatchTimeoutException)
            {
                skipped.Add(new SkippedFile(path, "regex-timeout"));
                continue;
            }

            // 块级上下文：selector 非空且有 box-shadow/border*/background* 的块（outline-removed 守卫）
            var shields = new HashSet<(String File, String Selector)>();
            foreach (var d in decls)
                if (d.Selector != null && IsShieldProperty(d.Property))
                    shields.Add((path, d.Selector));

            foreach (var d in decls)
            {
                if (!IsC3Property(d.Property)) continue;
                declarations++;
                tokenized += CountVarRefs(d.Value);
                var added = ApplyRules(index, d, strict, shields, findings);
                hardcoded += added;
            }
        }

        // C4 汇总
        var errors = findings.Count(f => f.Severity == "error");
        var warnings = findings.Count(f => f.Severity == "warning");
        var infos = findings.Count(f => f.Severity == "info");
        var coverage = tokenized + hardcoded > 0
            ? Math.Round(tokenized / (double)(tokenized + hardcoded), 4)
            : (Double?)null;

        // 排序 + 截断
        var ordered = findings
            .OrderBy(f => fileOrder.GetValueOrDefault(f.File, 0))
            .ThenBy(f => f.Line).ThenBy(f => f.Column)
            .ThenBy(f => f.Rule, StringComparer.Ordinal)
            .ToList();
        var truncated = ordered.Count > maxFindings;
        var final = ordered.Take(maxFindings).ToList();

        var summary = new ReviewSummary(files, declarations, tokenized, hardcoded, coverage,
            errors, warnings, infos, errors == 0, strict);
        return new Outcome(null, summary, final, skipped, truncated, notes);
    }

    static Int32 CountVarRefs(String value) => VarRefRegex.Matches(value).Count;

    static Boolean IsC3Property(String p)
    {
        if (p.StartsWith("tailwind:", StringComparison.Ordinal)) return true;
        if (p.StartsWith("--", StringComparison.Ordinal)) return true;    // 自定义属性：只查 var 引用
        if (p is "color" or "background" or "background-color" or "border" or "border-color" or "outline" or "outline-color"
            or "fill" or "stroke" or "caret-color" or "accent-color" or "text-decoration-color") return true;
        if (p.StartsWith("border-", StringComparison.Ordinal) && p.EndsWith("-color", StringComparison.Ordinal)) return true;
        if (p.StartsWith("padding", StringComparison.Ordinal) || p.StartsWith("margin", StringComparison.Ordinal)
            || p is "gap" or "row-gap" or "column-gap") return true;
        if (p.StartsWith("border-radius", StringComparison.Ordinal) || p.StartsWith("border-", StringComparison.Ordinal) && p.EndsWith("-radius", StringComparison.Ordinal)) return true;
        if (p is "border-width" || p.StartsWith("border-", StringComparison.Ordinal) && p.EndsWith("-width", StringComparison.Ordinal)) return true;
        if (p is "font-size" or "font-family" or "font-weight") return true;
        if (p is "box-shadow") return true;
        if (p is "transition" or "animation" or "transition-duration" or "animation-duration") return true;
        if (p is "outline" or "outline-style") return true;
        if (p is "z-index") return true;
        return false;
    }

    static Boolean IsShieldProperty(String p) =>
        p.StartsWith("box-shadow", StringComparison.Ordinal)
        || p.StartsWith("border", StringComparison.Ordinal)
        || p.StartsWith("background", StringComparison.Ordinal);

    /// <summary>对一条声明应用全部命中规则；返回 hardcoded 条数增量</summary>
    Int32 ApplyRules(TokenIndex index, Declaration d, Boolean strict,
        HashSet<(String File, String Selector)> shields, List<ReviewFinding> findings)
    {
        var hardcoded = 0;

        // var(--ds-*) 引用存在性检查：任意属性（含自定义属性、tailwind）
        CheckVarRefs(index, d, strict, findings);

        if (d.Property.StartsWith("--", StringComparison.Ordinal) || d.Property.StartsWith("tailwind:", StringComparison.Ordinal))
        {
            if (d.Property.StartsWith("tailwind:", StringComparison.Ordinal))
                hardcoded += CheckTailwind(index, d, strict, findings);
            return hardcoded;
        }

        if (IsColorProperty(d.Property)) hardcoded += CheckColors(index, d, strict, findings);

        var lengthKind = LengthKindOf(d.Property);
        if (lengthKind != null) hardcoded += CheckLengths(index, d, lengthKind, strict, findings);

        if (d.Property == "font-family") hardcoded += CheckFontFamily(index, d, strict, findings);
        if (d.Property == "font-weight") hardcoded += CheckFontWeight(index, d, strict, findings);
        if (d.Property == "box-shadow") hardcoded += CheckShadow(index, d, strict, findings);
        if (d.Property is "transition" or "animation" or "transition-duration" or "animation-duration")
            hardcoded += CheckDurations(index, d, strict, findings);
        if (d.Property is "outline" or "outline-style") CheckOutlineRemoved(index, d, strict, shields, findings);
        if (d.Property == "z-index") hardcoded += CheckZIndex(index, d, strict, findings);

        return hardcoded;
    }

    static String Sev(String baseSev, Boolean strict) => baseSev == "warning" && strict ? "error" : baseSev;

    void Add(List<ReviewFinding> findings, Declaration d, String rule, String sev, String found, String message, ReviewSuggestion? suggestion)
    {
        findings.Add(new ReviewFinding(d.File, d.Line, d.Column, rule, sev, d.Property, found, message, suggestion));
    }

    // ---- var 引用 ----

    void CheckVarRefs(TokenIndex index, Declaration d, Boolean strict, List<ReviewFinding> findings)
    {
        foreach (Match m in VarRefRegex.Matches(d.Value))
        {
            var varName = "--ds-" + m.Groups[1].Value;
            if (index.PathByCssVar.TryGetValue(varName, out var path))
            {
                var lc = index.LifecycleOf(path);
                if (lc == "removed")
                {
                    var sug = ToSuggestion(ClosestDefined(index, varName));
                    Add(findings, d, "removed-token-ref", "error", varName,
                        $"令牌 {path} 已标记 removed，不应再引用（迁移到替代令牌）",
                        sug);
                }
                else if (lc == "deprecated")
                {
                    var sug = ToSuggestion(ClosestDefined(index, varName));
                    Add(findings, d, "deprecated-token-ref", Sev("warning", strict), varName,
                        $"令牌 {path} 已标记 deprecated，建议改用替代令牌",
                        sug);
                }
            }
            else
            {
                var sug = ToSuggestion(ClosestDefined(index, varName));
                Add(findings, d, "unknown-token-ref", "error", varName,
                    $"变量 {varName} 不在本项目该主题的已定义变量集（检查拼写或先建立该令牌）",
                    sug);
            }
        }
    }

    /// <summary>编辑距离 ≤2 或同前缀的最近已定义变量（用于 unknown/removed/deprecated 建议）</summary>
    DefinedCandidate? ClosestDefined(TokenIndex index, String varName)    {
        DefinedCandidate? best = null;
        var bestDist = 0;
        foreach (var (cssVar, path) in index.PathByCssVar)
        {
            if (index.LifecycleOf(path) == "removed") continue;
            var dist = Levenshtein(varName, cssVar);
            if (dist <= 2)
            {
                if (best == null || dist < bestDist)
                {
                    best = new DefinedCandidate(path, cssVar, SnapValue(index, path));
                    bestDist = dist;
                }
            }
        }
        if (best == null)
        {
            // 同前缀：取共同前缀最长者
            var prefixBest = (Prefix: 0, CssVar: "", Path: "", Value: "");
            foreach (var (cssVar, path) in index.PathByCssVar)
            {
                if (index.LifecycleOf(path) == "removed") continue;
                var p = CommonPrefix(varName, cssVar);
                if (p > prefixBest.Prefix) prefixBest = (p, cssVar, path, SnapValue(index, path));
            }
            if (prefixBest.Prefix >= 3)
                best = new DefinedCandidate(prefixBest.Path, prefixBest.CssVar, prefixBest.Value);
        }
        return best != null ? new DefinedCandidate(best.Path, best.CssVar, best.Value) : null;
    }

    static ReviewSuggestion? ToSuggestion(DefinedCandidate? c) =>
        c == null ? null : new ReviewSuggestion(c.Path, c.CssVar, c.Value, "var(" + c.CssVar + ")");

    static String SnapValue(TokenIndex index, String path)
    {
        var t = index.Tokens.FirstOrDefault(x => x.Path == path);
        return t?.ColorHex ?? t?.Value ?? "";
    }

    static Int32 Levenshtein(String a, String b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        var prev = new Int32[b.Length + 1];
        var cur = new Int32[b.Length + 1];
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

    static Int32 CommonPrefix(String a, String b)
    {
        var n = 0;
        while (n < a.Length && n < b.Length && a[n] == b[n]) n++;
        return n;
    }

    // ---- 颜色 ----

    static Boolean IsColorProperty(String p) => p switch
    {
        "color" or "background" or "background-color" or "border" or "border-color" or "outline" or "outline-color"
            or "fill" or "stroke" or "caret-color" or "accent-color" or "text-decoration-color" => true,
        _ => p.StartsWith("border-", StringComparison.Ordinal) && p.EndsWith("-color", StringComparison.Ordinal),
    };

    Int32 CheckColors(TokenIndex index, Declaration d, Boolean strict, List<ReviewFinding> findings)
    {
        var hits = 0;
        foreach (var raw in ExtractColorLiterals(d.Value))
        {
            if (!NearestTokenFinder.TryParseColor(raw, out var color)) continue;
            var best = ClosestColor(index, color);
            if (best == null) continue;
            if (best.Distance <= 0.05)
                Add(findings, d, "hardcoded-color", Sev("warning", strict), raw,
                    $"颜色 {raw} 已有对应令牌（距离 {best.Distance:0.####} ≤ 0.05），应改用 var(--ds-…)",
                    new ReviewSuggestion(best.Path, best.CssVar, best.Value, best.Replace));
            else
            {
                var sug = best.Distance <= 0.25
                    ? new ReviewSuggestion(best.Path, best.CssVar, best.Value, best.Replace) : null;
                Add(findings, d, "off-palette-color", Sev("warning", strict), raw,
                    $"颜色 {raw} 偏离色板（最近 {best.Distance:0.####} > 0.05）",
                    sug);
            }
            hits++;
        }
        return hits;
    }

    static Nearest? ClosestColor(TokenIndex index, Oklch.Color color)
    {
        var lab = Oklch.OklchToOklab(color);
        Nearest? best = null;
        foreach (var t in index.ColorCandidates())
        {
            if (!(t.ColorHex.IsNullOrEmpty() ? NearestTokenFinder.TryParseColor(t.Value, out var c) : NearestTokenFinder.TryParseColor(t.ColorHex, out c))) continue;
            var tlab = Oklch.OklchToOklab(c);
            var dist = Math.Round(Math.Sqrt(Sq(lab.L - tlab.L) + Sq(lab.A - tlab.A) + Sq(lab.B - tlab.B)), 4);
            if (best == null || dist < best.Distance)
                best = new Nearest(t.Path, ExportService.CssVarName(t.Path), t.ColorHex ?? t.Value, "var(" + ExportService.CssVarName(t.Path) + ")", dist);
        }
        return best;
    }

    /// <summary>提取值里的颜色字面量：先移除 url()/var()/函数包装，再匹配 hex/函数/命名色</summary>
    internal static List<String> ExtractColorLiterals(String value)
    {
        var cleaned = Regex.Replace(value, @"url\([^)]*\)", " ");
        cleaned = Regex.Replace(cleaned, @"var\([^)]*\)", " ");
        var list = new List<String>();
        foreach (Match m in FuncColorRegex.Matches(cleaned)) list.Add(m.Value);
        foreach (Match m in HexRegex.Matches(cleaned)) list.Add(m.Value);
        foreach (var name in NamedColors)
        {
            var re = new Regex(@$"(?<![\w-]){name}(?![\w-])", RegexOptions.IgnoreCase);
            foreach (Match m in re.Matches(cleaned)) list.Add(m.Value.ToLowerInvariant());
        }
        // alpha=0 的颜色跳过（C2）
        return list.Where(c => !IsZeroAlpha(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    static Boolean IsZeroAlpha(String c)
    {
        var m = Regex.Match(c, @"(?:rgba?|hsla?)\([^)]*,\s*0\s*\)$");
        return m.Success;
    }

    // ---- 长度 ----

    static String? LengthKindOf(String p)
    {
        if (p.StartsWith("padding", StringComparison.Ordinal) || p.StartsWith("margin", StringComparison.Ordinal)
            || p is "gap" or "row-gap" or "column-gap") return "space";
        if (p.StartsWith("border-radius", StringComparison.Ordinal)
            || p.StartsWith("border-", StringComparison.Ordinal) && p.EndsWith("-radius", StringComparison.Ordinal)) return "radius";
        if (p is "border" or "border-width" || p.StartsWith("border-", StringComparison.Ordinal) && p.EndsWith("-width", StringComparison.Ordinal)) return "border";
        if (p == "font-size") return "size";
        return null;
    }

    Int32 CheckLengths(TokenIndex index, Declaration d, String kind, Boolean strict, List<ReviewFinding> findings)
    {
        var hits = 0;
        foreach (var (raw, px) in ExtractLengths(d.Value, kind))
        {
            var exact = ExactLengthMatch(index, kind, px);
            if (exact != null)
            {
                Add(findings, d, "hardcoded-length", Sev("warning", strict), raw,
                    $"{raw} 已有对应令牌（{exact.Path}），应改用 var(--ds-…)",
                    ToSuggestion(exact));
            }
            else if (px >= 3)
            {
                var nearest = NearestLength(index, kind, px);
                var sug = nearest != null && Math.Abs(nearest.Distance) <= Math.Max(2, px * 0.25)
                    ? new ReviewSuggestion(nearest.Path, nearest.CssVar, nearest.Value, nearest.Replace) : null;
                Add(findings, d, "off-scale-length", Sev("warning", strict), raw,
                    $"{raw} 不在这类档位（最近 {nearest?.Path ?? "无"}，Δ {nearest?.Distance ?? 0:0.##}px）", sug);
            }
            hits++;
        }
        return hits;
    }

    /// <summary>C2 过滤 + 提取长度 token（百分比/auto/var/calc 整值/负值/0 排除）</summary>
    internal static List<(String Raw, Double Px)> ExtractLengths(String value, String kind)
    {
        if (value.Contains("calc(", StringComparison.Ordinal) || value.Contains("clamp(", StringComparison.Ordinal)
            || value.Contains("min(", StringComparison.Ordinal) || value.Contains("max(", StringComparison.Ordinal)) return [];
        var cleaned = Regex.Replace(value, @"url\([^)]*\)", " ");
        cleaned = Regex.Replace(cleaned, @"var\([^)]*\)", " ");   // var() 引用位不算字面量（C2 回退值不报）
        var list = new List<(String, Double)>();
        foreach (Match m in LengthTokenRegex.Matches(cleaned))
        {
            var raw = m.Value;
            if (m.Index + m.Length < cleaned.Length && cleaned[m.Index + m.Length] == '%') continue;  // 百分比整值跳过
            if (raw == "auto" || raw == "inherit" || raw == "initial" || raw == "unset" || raw == "revert" || raw == "none") continue;
            if (!NearestTokenFinder.TryParseLength(raw, out var px)) continue;
            if (px < 0) continue;
            if (px == 0) continue;                                  // 0 跳过
            if (kind == "space" && px is 1 or 2) continue;          // 1px/2px 仅间距类跳过
            if (kind == "border" && px == 1) continue;              // 1px 边框宽跳过
            list.Add((raw, px));
        }
        return list;
    }

    DefinedCandidate? ExactLengthMatch(TokenIndex index, String kind, Double px)
    {
        foreach (var t in index.LengthCandidates(kind))
        {
            if (!NearestTokenFinder.TryParseLength(t.Value, out var tv)) continue;
            if (Math.Abs(tv - px) <= 0.01)
                return new DefinedCandidate(t.Path, ExportService.CssVarName(t.Path), t.Value);
        }
        return null;
    }

    Nearest? NearestLength(TokenIndex index, String kind, Double px)
    {
        Nearest? best = null;
        foreach (var t in index.LengthCandidates(kind))
        {
            if (!NearestTokenFinder.TryParseLength(t.Value, out var tv)) continue;
            var dist = Math.Abs(tv - px);
            if (best == null || dist < best.Distance)
                best = new Nearest(t.Path, ExportService.CssVarName(t.Path), t.Value, "var(" + ExportService.CssVarName(t.Path) + ")", dist);
        }
        return best;
    }

    // ---- 字体 ----

    static readonly String[] GenericFamilies = ["serif", "sans-serif", "monospace", "system-ui", "cursive", "fantasy"];

    Int32 CheckFontFamily(TokenIndex index, Declaration d, Boolean strict, List<ReviewFinding> findings)
    {
        var v = d.Value.Trim();
        if (v.Contains("var(", StringComparison.Ordinal)) return 0;
        var first = v.Split(',')[0].Trim().Trim('"', '\'').ToLowerInvariant();
        if (GenericFamilies.Contains(first) || first.StartsWith("ui-", StringComparison.Ordinal)) return 0;

        var mono = v.ToLowerInvariant().Contains("mono", StringComparison.Ordinal);
        var target = mono ? "font.mono" : "font.sans";
        var tok = index.Tokens.FirstOrDefault(t => t.Path == target);
        ReviewSuggestion? sug = null;
        if (tok != null)
            sug = new ReviewSuggestion(tok.Path, ExportService.CssVarName(tok.Path), tok.Value, "var(" + ExportService.CssVarName(tok.Path) + ")");
        Add(findings, d, "hardcoded-font-family", Sev("warning", strict), first,
            $"字族 {first} 硬编码，应使用设计系统字族", sug);
        return 1;
    }

    Int32 CheckFontWeight(TokenIndex index, Declaration d, Boolean strict, List<ReviewFinding> findings)
    {
        var v = d.Value.Trim().ToLowerInvariant();
        if (!NearestTokenFinder.TryParseWeight(v, out var w)) return 0;
        foreach (var t in index.FontWeightCandidates())
        {
            if (NearestTokenFinder.TryParseWeight(t.Value, out var tw) && Math.Abs(tw - w) <= 0.5)
            {
                Add(findings, d, "hardcoded-font-weight", "info", v,
                    $"字重 {v} 已有对应令牌，应改用 var(--ds-…)",
                    new ReviewSuggestion(t.Path, ExportService.CssVarName(t.Path), t.Value, "var(" + ExportService.CssVarName(t.Path) + ")"));
                return 1;
            }
        }
        return 0;
    }

    // ---- 阴影 ----

    Int32 CheckShadow(TokenIndex index, Declaration d, Boolean strict, List<ReviewFinding> findings)
    {
        var v = d.Value.Trim();
        if (v == "none" || v.Contains("var(", StringComparison.Ordinal)) return 0;
        var nearest = NearestTokenFinder.Find(index, v, "box-shadow", 1);
        ReviewSuggestion? sug = null;
        if (nearest.Matches.Count > 0 && nearest.Matches[0].Exact)
            sug = new ReviewSuggestion(nearest.Matches[0].Path, nearest.Matches[0].CssVar, nearest.Matches[0].Value, nearest.Matches[0].Replace);
        Add(findings, d, "hardcoded-shadow", Sev("warning", strict), v,
            "阴影硬编码，应使用 shadow.elevation-* 令牌", sug);
        return 1;
    }

    // ---- 时长 ----

    Int32 CheckDurations(TokenIndex index, Declaration d, Boolean strict, List<ReviewFinding> findings)
    {
        var first = FirstDuration(d.Value);
        if (first == null || first.Value.Ms <= 0) return 0;   // 0 不检；找不到时间值不检
        var ms = first.Value.Ms;
        var exact = ExactDuration(index, ms);
        if (exact != null)
        {
            Add(findings, d, "hardcoded-duration", Sev("warning", strict), first.Value.Raw,
                $"时长 {first.Value.Raw} 已有对应令牌，应改用 var(--ds-…)",
                ToSuggestion(exact));
            return 1;
        }
        if (ms >= 50)
        {
            var nearest = NearestDuration(index, ms);
            var sug = nearest != null && Math.Abs(nearest.Distance) <= 100
                ? new ReviewSuggestion(nearest.Path, nearest.CssVar, nearest.Value, nearest.Replace) : null;
            Add(findings, d, "off-scale-duration", Sev("warning", strict), first.Value.Raw,
                $"时长 {first.Value.Raw} 不在这类档位（最近 {nearest?.Path ?? "无"}，Δ {nearest?.Distance ?? 0:0}ms）", sug);
            return 1;
        }
        return 0;
    }

    static (String Raw, Double Ms)? FirstDuration(String value)
    {
        foreach (Match m in DurationTokenRegex.Matches(value))
        {
            if (NearestTokenFinder.TryParseDuration(m.Value, out var ms)) return (m.Value, ms);
        }
        return null;
    }

    DefinedCandidate? ExactDuration(TokenIndex index, Double ms)
    {
        foreach (var t in index.DurationCandidates())
        {
            if (NearestTokenFinder.TryParseDuration(t.Value, out var tv) && Math.Abs(tv - ms) <= 0.5)
                return new DefinedCandidate(t.Path, ExportService.CssVarName(t.Path), t.Value);
        }
        return null;
    }

    Nearest? NearestDuration(TokenIndex index, Double ms)
    {
        Nearest? best = null;
        foreach (var t in index.DurationCandidates())
        {
            if (!NearestTokenFinder.TryParseDuration(t.Value, out var tv)) continue;
            var dist = Math.Abs(tv - ms);
            if (best == null || dist < best.Distance)
                best = new Nearest(t.Path, ExportService.CssVarName(t.Path), t.Value, "var(" + ExportService.CssVarName(t.Path) + ")", dist);
        }
        return best;
    }

    // ---- outline ----

    void CheckOutlineRemoved(TokenIndex index, Declaration d, Boolean strict,
        HashSet<(String File, String Selector)> shields, List<ReviewFinding> findings)
    {
        var v = d.Value.Trim().ToLowerInvariant();
        var isRemoved = d.Property == "outline-style"
            ? v == "none"
            : v == "none" || v == "0";
        if (!isRemoved) return;
        if (d.Selector != null && d.Selector.Contains(":not(:focus-visible)", StringComparison.Ordinal)) return;
        // 同块守卫：块里有 box-shadow/border*/background* 即视为有可见焦点替代
        if (d.Selector != null && shields.Contains((d.File, d.Selector))) return;
        ReviewSuggestion? sug = null;
        if (index.DefinedCssVars.Contains("--ds-component-focus-outline-width"))
        {
            var tok = index.Tokens.FirstOrDefault(t => ExportService.CssVarName(t.Path) == "--ds-component-focus-outline-width");
            if (tok != null)
                sug = new ReviewSuggestion(tok.Path, ExportService.CssVarName(tok.Path), tok.Value, "var(--ds-component-focus-outline-width)");
        }
        Add(findings, d, "outline-removed", Sev("warning", strict), v,
            "移除 outline 必须同时提供可见的焦点替代（box-shadow/border/background），否则键盘用户看不到焦点", sug);
    }

    // ---- z-index ----

    Int32 CheckZIndex(TokenIndex index, Declaration d, Boolean strict, List<ReviewFinding> findings)
    {
        var v = d.Value.Trim();
        if (!Int32.TryParse(v, out var zi) || zi < 10) return 0;
        foreach (var t in index.Tokens.Where(t => t.Path.StartsWith("z-index.", StringComparison.Ordinal)))
        {
            if (Int32.TryParse(t.Value?.Trim(), out var tv) && tv == zi)
            {
                Add(findings, d, "hardcoded-z-index", "info", v,
                    $"z-index {zi} 已有对应令牌，应改用 var(--ds-…)",
                    new ReviewSuggestion(t.Path, ExportService.CssVarName(t.Path), t.Value!, "var(" + ExportService.CssVarName(t.Path) + ")"));
                return 1;
            }
        }
        return 0;
    }

    // ---- Tailwind 任意值 ----

    Int32 CheckTailwind(TokenIndex index, Declaration d, Boolean strict, List<ReviewFinding> findings)
    {
        var cls = d.Property["tailwind:".Length..];
        var inner = d.Value.Trim();
        if (TailwindVarRegex.IsMatch(inner)) return 0;                  // var 引用：只查存在性（已在 CheckVarRefs）
        if (NearestTokenFinder.TryParseColor(inner, out var color))
        {
            var best = ClosestColor(index, color);
            ReviewSuggestion? sug = best != null
                ? new ReviewSuggestion(best.Path, best.CssVar, best.Value, $"{cls}-[var({best.CssVar})]") : null;
            Add(findings, d, "tailwind-arbitrary-value", Sev("warning", strict), inner,
                $"任意值 {cls}-[{inner}] 应改用设计系统令牌", sug);
            return 1;
        }
        if (NearestTokenFinder.TryParseLength(inner, out var px))
        {
            var nearest = NearestLength(index, "space", px) ?? NearestLength(index, "size", px);
            ReviewSuggestion? sug = nearest != null
                ? new ReviewSuggestion(nearest.Path, nearest.CssVar, nearest.Value, $"{cls}-[var({nearest.CssVar})]") : null;
            Add(findings, d, "tailwind-arbitrary-value", Sev("warning", strict), inner,
                $"任意值 {cls}-[{inner}] 应改用设计系统令牌", sug);
            return 1;
        }
        return 0;   // calc()/theme()/百分比等忽略
    }

    static Double Sq(Double x) => x * x;
}
