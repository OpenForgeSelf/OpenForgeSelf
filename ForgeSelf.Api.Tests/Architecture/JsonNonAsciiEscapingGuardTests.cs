using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using ForgeSelf.Abstractions;
using Match = System.Text.RegularExpressions.Match;
using Xunit;

namespace ForgeSelf.Api.Tests.Architecture;

/// <summary>
/// PC-027 守卫：全仓 JSON 写出路径中文保持原文，禁止转义成 \uXXXX。
/// 根因（2026-11-07 用户输入6）：System.Text.Json 默认 Encoder 转义一切非 ASCII。
/// 项目约定所有 JSON 写出路径显式使用 ForgeJson（Abstractions 共享选项）。
/// G2 源码枚举拦截：① 单参 JsonSerializer.Serialize*；② 缺 Encoder 的
/// new JsonSerializerOptions/JsonWriterOptions 初始化器（含 target-typed new()）；③ 无参 .ToJsonString()。
/// 已知盲区（靠 sweep 期 grep 复核）：插值串孔内、结构体属性逐个赋值、WriteAsJsonAsync 单参重载。
/// </summary>
public class JsonNonAsciiEscapingGuardTests
{
    [Fact]
    public void 生产代码_JSON写出路径_必须中文保持原文_禁止默认转义()
    {
        var root = RepoRoot();
        var files = ProductionCsFiles(root);

        files.Count.Should().BeGreaterThan(150,
            $"扫描面异常：仅 {files.Count} 个 *.cs ⇒ 仓库根定位或枚举失效（前提自查）");

        var offenders = new List<string>();
        int serializeCount = 0;

        foreach (var file in files)
        {
            var code = Strip(File.ReadAllText(file));
            var rel = Path.GetRelativePath(root, file);
            serializeCount += CountMarker(code, "JsonSerializer.Serialize");
            ScanSingleArgSerialize(code, rel, offenders);
            ScanNoArgToJsonString(code, rel, offenders);
            ScanOptionsInitializers(code, rel, offenders);
        }

        serializeCount.Should().BeGreaterThan(50,
            $"阳性对照失败：全仓 Serialize 调用仅 {serializeCount} ⇒ 扫描正则/剥离失效（照 ToolSchemaJsonGuardTests 规矩）");

        offenders.Should().BeEmpty(
            $"PC-027 违例 {offenders.Count} 处（JSON 写出未用 ForgeJson，中文会转义成 \\uXXXX）：\n" +
            string.Join("\n", offenders.Take(50)));
    }

    // ---------------- G1：ForgeJson 行为单测 ----------------

    [Fact]
    public void ForgeJson_Default_中文保持原文不产生转义()
    {
        var json = JsonSerializer.Serialize(new { 目录 = "目录上溯", note = "中文<测试>&+" }, ForgeJson.Default);

        json.Should().Contain("目录上溯");
        json.Should().NotContain("\\u76EE\\u5F55");
        json.Should().NotContain("\\u");
    }

    [Fact]
    public void ForgeJson_Default_不改命名策略_字段名保持原样()
    {
        var json = JsonSerializer.Serialize(new { ToolName = "demo", Count = 1, 参数 = 2 }, ForgeJson.Default);

        json.Should().Contain("\"ToolName\"");
        json.Should().Contain("\"参数\"");
        ForgeJson.Default.PropertyNamingPolicy.Should().BeNull();
    }

    [Fact]
    public void ForgeJson_Indented_带缩进且中文保持原文()
    {
        var json = JsonSerializer.Serialize(new { 目录 = "目录上溯" }, ForgeJson.Indented);

        json.Should().Contain("\n");
        json.Should().Contain("目录上溯");
        json.Should().NotContain("\\u");
        ForgeJson.Indented.WriteIndented.Should().BeTrue();
        ForgeJson.Default.WriteIndented.Should().BeFalse();
    }

    [Fact]
    public void 对照组_无选项默认行为_仍会转义非ASCII_守卫环境自证()
    {
        var json = JsonSerializer.Serialize(new { msg = "目录上溯" });

        json.Should().Contain("\\u76EE\\u5F55");
    }

    // ---------------- 扫描实现 ----------------

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
            dir = dir.Parent;
        dir.Should().NotBeNull("应能向上找到 ForgeSelf.slnx（仓库根锚点）");
        return dir!.FullName;
    }

    private static List<string> ProductionCsFiles(string root)
    {
        var roots = new[]
        {
            "ForgeSelf.Api", "ForgeSelf.Core", "ForgeSelf.Abstractions",
            "ForgeSelf.Bootstrapper", "Plugins",
        };
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Tests", "TestResults", "obj", "bin", "node_modules", "web", "dist", "e2e",
            "docs", "openwiki", ".git", ".temp", ".agents", "forgeself-design",
            "ForgeSelf.Web", "build", "publish", "screenshots",
        };

        var files = new List<string>();
        foreach (var r in roots)
        {
            var dir = Path.Combine(root, r);
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, f);
                var segments = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (segments.Any(s => excluded.Contains(s))) continue;
                files.Add(f);
            }
        }
        return files;
    }

    private static int CountMarker(string code, string marker)
    {
        int n = 0;
        int i = 0;
        while ((i = code.IndexOf(marker, i, StringComparison.Ordinal)) >= 0)
        {
            n++;
            i += marker.Length;
        }
        return n;
    }

    private static void ScanSingleArgSerialize(string code, string rel, List<string> offenders)
    {
        const string marker = "JsonSerializer.Serialize";
        int i = 0;
        while ((i = code.IndexOf(marker, i, StringComparison.Ordinal)) >= 0)
        {
            int pos = i + marker.Length;
            while (pos < code.Length && (char.IsWhiteSpace(code[pos]) || code[pos] == '<' || code[pos] == '>'))
                pos++;

            if (pos < code.Length && code[pos] == '(')
            {
                int close = FindClose(code, pos, '(', ')');
                if (close > 0)
                {
                    var args = code.Substring(pos + 1, close - pos - 1);
                    if (!HasTopLevelComma(args))
                        offenders.Add($"{rel}:{LineOf(code, i)} 单参 JsonSerializer.Serialize(...) ⇒ 补 ForgeJson options（PC-027）");
                }
            }
            i = pos;
        }
    }

    private static void ScanNoArgToJsonString(string code, string rel, List<string> offenders)
    {
        foreach (Match m in Regex.Matches(code, @"\.ToJsonString\s*\(\s*\)"))
            offenders.Add($"{rel}:{LineOf(code, m.Index)} 无参 .ToJsonString() ⇒ 改 .ToJsonString(ForgeJson.Default)（PC-027）");
    }

    private static void ScanOptionsInitializers(string code, string rel, List<string> offenders)
    {
        foreach (Match m in Regex.Matches(code, @"new\s+(?:[\w.]+\.)?Json(Serializer|Writer)Options\b"))
            CheckOptionsInitializer(code, rel, offenders, m, "new JsonSerializerOptions/JsonWriterOptions 初始化器缺 Encoder");

        foreach (Match m in Regex.Matches(code, @"Json(Serializer|Writer)Options\s+[\w]+\s*(?:\{[^}]*\}\s*)?=\s*new\s*\(\s*\)"))
            CheckOptionsInitializer(code, rel, offenders, m, "target-typed new() options 初始化器缺 Encoder");
    }

    private static void CheckOptionsInitializer(string code, string rel, List<string> offenders, Match m, string kind)
    {
        int i = m.Index + m.Length;
        while (i < code.Length && char.IsWhiteSpace(code[i])) i++;

        if (i < code.Length && code[i] == '(')
        {
            int close = FindClose(code, i, '(', ')');
            if (close < 0) return;
            i = close + 1;
            while (i < code.Length && char.IsWhiteSpace(code[i])) i++;
        }

        if (i >= code.Length || code[i] != '{') return;

        int end = FindClose(code, i, '{', '}');
        if (end < 0) return;

        var block = code.Substring(i, end - i + 1);
        if (!block.Contains("Encoder"))
            offenders.Add($"{rel}:{LineOf(code, m.Index)} {kind} ⇒ 补 UnsafeRelaxedJsonEscaping 或改用 ForgeJson（PC-027）");
    }

    private static int FindClose(string s, int open, char openCh, char closeCh)
    {
        int depth = 0;
        for (int i = open; i < s.Length; i++)
        {
            if (s[i] == openCh) depth++;
            else if (s[i] == closeCh)
            {
                depth--;
                if (depth == 0) return i;
            }
            else if (s[i] == ';' && depth > 0) return -1;
        }
        return -1;
    }

    private static bool HasTopLevelComma(string s)
    {
        int p = 0, b = 0, k = 0;
        foreach (var c in s)
        {
            if (c == '(') p++;
            else if (c == ')') p--;
            else if (c == '{') b++;
            else if (c == '}') b--;
            else if (c == '[') k++;
            else if (c == ']') k--;
            else if (c == ',' && p == 0 && b == 0 && k == 0) return true;
        }
        return false;
    }

    private static int LineOf(string s, int index) => s.Substring(0, index).Count(c => c == '\n') + 1;

    /// <summary>剥离注释与字符串字面量（保留换行对齐行号；索引在剥离文本上有效）。</summary>
    private static string Strip(string text)
    {
        var sb = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i < text.Length)
                {
                    if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/') { i += 2; break; }
                    if (text[i] == '\n') sb.Append('\n');
                    i++;
                }
                continue;
            }

            if (c == '"')
            {
                i++;
                while (i < text.Length)
                {
                    if (text[i] == '\\') { i += 2; continue; }
                    if (text[i] == '"') { i++; break; }
                    if (text[i] == '\n') sb.Append('\n');
                    i++;
                }
                continue;
            }

            if (c == '\'')
            {
                i++;
                while (i < text.Length)
                {
                    if (text[i] == '\\') { i += 2; continue; }
                    if (text[i] == '\'') { i++; break; }
                    if (text[i] == '\n') sb.Append('\n');
                    i++;
                }
                continue;
            }

            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }
}
