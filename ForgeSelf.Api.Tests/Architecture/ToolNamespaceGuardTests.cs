using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Architecture;

/// <summary>
/// 工具命名空间守卫（2026-10-28，ADR-004 / pilot 2026-10-28-tool-namespace-ownership）。
/// 防复发目标：任何插件新增/改名工具都必须遵守「&lt;toolPrefix&gt;_&lt;语义名&gt;」或命中平台保留名白名单，
/// 注册表零重名。四层断言 + 规则自检（mutation 等价：证明规则函数真的会咬人）。
/// 扫描方式：源码静态扫描 Plugins/*/（r1 属性表达式 / r2 Sems 构造 / r3 实例化赋值三种 Name 形态，
/// ASCII-snake 过滤排除中文菜单名）；保留名中 Name=>常量 形态的工具（finish/list_tools）不被扫描属预期，
/// 由 ② 的保留名豁免与运行期 ExtensionPointManager 校验兜底。
/// </summary>
public class ToolNamespaceGuardTests
{
    private static readonly Regex R1 = new(@"public\s+(?:override\s+)?[Ss]tring\s+Name\s+=>\s+""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex R2 = new(@"base\(pluginId,\s+""([a-z][a-z0-9_]*)""", RegexOptions.Compiled);
    private static readonly Regex R3 = new(@"(?<![A-Za-z])Name\s*=\s*""([a-z][a-z0-9_]*)""", RegexOptions.Compiled);
    private static readonly Regex Snake = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>跨插件迁出的映射项：新名落点插件 ≠ 键里的源插件（公共工具迁 mcp-center；2026-10-28 归属修订轮文件三件套再迁 file-tools、text-tools 并入 dev-tools）。</summary>
    private static readonly Dictionary<string, string> MovedTo = new()
    {
        ["ai-agent:get_current_time"] = "mcp-center",
        ["ai-agent:calculate"] = "mcp-center",
        ["ai-agent:read_file"] = "file-tools",
        ["ai-agent:write_file"] = "file-tools",
        ["ai-agent:list_files"] = "file-tools",
    };

    [Fact]
    public void S1_有工具的插件必须声明toolPrefix且与唯一真源一致()
    {
        var repo = FindRepoRoot();
        foreach (var dir in PluginDirs(repo))
        {
            var json = File.ReadAllText(Path.Combine(dir, "plugin.json"));
            var id = ExtractJsonString(json, "Id");
            var scanned = ScanPlugin(dir);
            if (scanned.Count == 0) continue; // 无工具插件不要求 toolPrefix
            if (id == ToolNamespace.ExemptPluginId)
            {
                // mcp-center 整插件豁免前缀（用户拍板 2026-10-28 17:29）：不进前缀表、plugin.json 不声明
                json.Should().NotContain("\"toolPrefix\"", "豁免插件 mcp-center 不得再声明 toolPrefix");
                continue;
            }

            ToolNamespace.PrefixByPluginId.ContainsKey(id).Should().BeTrue(
                $"插件 {id} 有 {scanned.Count} 个工具但未在 ToolNamespace.PrefixByPluginId 声明前缀");

            json.Should().Contain("\"toolPrefix\"",
                $"插件 {id} 的 plugin.json 须声明 toolPrefix 字段（与 ToolNamespace 同源）");
            var declared = ExtractJsonString(json, "toolPrefix");
            declared.Should().Be(ToolNamespace.PrefixByPluginId[id],
                $"插件 {id} 的 plugin.json toolPrefix 与 ToolNamespace.PrefixByPluginId 不一致");
            Snake.IsMatch(declared!).Should().BeTrue($"插件 {id} 的 toolPrefix 格式须为 ^[a-z][a-z0-9_]*$");
        }
    }

    [Fact]
    public void S2_全部工具名前缀合规或属保留名()
    {
        var repo = FindRepoRoot();
        foreach (var dir in PluginDirs(repo))
        {
            var id = ExtractJsonString(File.ReadAllText(Path.Combine(dir, "plugin.json")), "Id");
            foreach (var name in ScanPlugin(dir))
            {
                var err = ToolNamespace.Validate(id, name);
                err.Should().BeNull($"插件 {id} 的工具 {name} 违反命名空间规则: {err}");
            }
        }
    }

    [Fact]
    public void S3_改名映射与源码扫描一致_旧名已退役_新名已就位()
    {
        var repo = FindRepoRoot();
        var byPlugin = PluginDirs(repo).ToDictionary(
            d => ExtractJsonString(File.ReadAllText(Path.Combine(d, "plugin.json")), "Id"),
            d => ScanPlugin(d));

        foreach (var (key, newName) in ToolNamespace.RenameMap)
        {
            var colon = key.IndexOf(':');
            var srcPlugin = key[..colon];
            var oldName = key[(colon + 1)..];

            // 旧名必须已从源插件退役（含迁出场景：源插件里也不该再有旧名）
            byPlugin[srcPlugin].Should().NotContain(oldName,
                $"映射 {key}: 旧名 {oldName} 仍留在源插件中未改名");

            // 新名必须已就位（落点 = 跨插件迁出覆盖 or 源插件自身）
            var target = MovedTo.GetValueOrDefault(key, srcPlugin);
            byPlugin[target].Should().Contain(newName,
                $"映射 {key}: 新名 {newName} 未出现在 {target} 的源码中");
        }
    }

    [Fact]
    public void S4_全注册表零重名()
    {
        var repo = FindRepoRoot();
        var all = new List<(string Plugin, string Name)>();
        foreach (var dir in PluginDirs(repo))
        {
            var id = ExtractJsonString(File.ReadAllText(Path.Combine(dir, "plugin.json")), "Id");
            all.AddRange(ScanPlugin(dir).Select(n => (id, n)));
        }

        var dups = all.GroupBy(x => x.Name).Where(g => g.Count() > 1).ToList();
        dups.Should().BeEmpty("注册表按 Name 唯一（ToolRegistry.TryAdd），发现重名: {0}",
            string.Join("; ", dups.Select(g => $"{g.Key} <- {string.Join(",", g.Select(x => x.Plugin))}")));
    }

    [Fact]
    public void S5_规则自检_规则函数真的会咬人()
    {
        // mutation 等价验证：规则函数对违规输入必须返回非空错误，对合规输入返回 null。
        ToolNamespace.Validate("dev-tools", "format_json").Should().NotBeNull("裸名（未改前）必须被拒");
        ToolNamespace.Validate("dev-tools", "dev_format_json").Should().BeNull();
        ToolNamespace.Validate("mcp-center", "read_file").Should().BeNull("保留名豁免");
        ToolNamespace.Validate("ai-agent", "finish").Should().BeNull("保留名豁免");
        ToolNamespace.Validate("ai-agent", "mcp_calculate").Should().NotBeNull("别的插件前缀算违规");
        ToolNamespace.Validate("no-such-plugin", "x_tool").Should().NotBeNull("未声明前缀的插件必须被拒");
        // mcp-center 整插件豁免（用户拍板 2026-10-28 17:29「mcp中心的工具不用前缀，是特殊例外」）
        ToolNamespace.Validate("mcp-center", "get_current_time").Should().BeNull("豁免插件裸名合规");
        ToolNamespace.Validate("mcp-center", "calculate").Should().BeNull("豁免插件裸名合规");
        ToolNamespace.ExemptPluginId.Should().Be("mcp-center");
        ToolNamespace.PrefixByPluginId.Should().NotContainKey("mcp-center", "豁免插件不得再进前缀表");
        ToolNamespace.RenameMap.Keys.Should().OnlyHaveUniqueItems();
    }

    // ────────────────────────── helpers ──────────────────────────

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
            dir = dir.Parent;
        if (dir == null) throw new InvalidOperationException("未找到仓库根（锚点 ForgeSelf.slnx）");
        return dir.FullName;
    }

    private static List<string> PluginDirs(string repo)
    {
        var root = Path.Combine(repo, "Plugins");
        return Directory.GetDirectories(root)
            .Where(d => File.Exists(Path.Combine(d, "plugin.json")))
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>扫描单个插件目录的 .cs（跳过 obj/），提取候选工具名（ASCII-snake 才收）。</summary>
    private static SortedSet<string> ScanPlugin(string dir)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Replace('\\', '/').Contains("/obj/")) continue;
            string code;
            try { code = File.ReadAllText(file); }
            catch (IOException) { continue; }

            // 注释剥离（2026-10-28 同类修复）：XCode 生成模板注释里含 entity.Name = "abc" 示例，
            // 会被 R3 当成工具名误判（CostScope 无工具插件被 S1 误红即是实证）。扫描器只看真实代码。
            code = StripComments(code);

            // 行级过滤（2026-10-28 同类修复）：三类 Name = "..." 形态不是宿主注册表工具——
            // ① mcp-center 外部 MCP 工具目录种子（行含 ServerId =，语义是外部服务器工具名，不进 ToolRegistry）；
            // ② 脚本参数定义（行含 ScriptParameterType.，语义是脚本入参名）；
            // ③ ASP.NET 参数绑定特性（行含 (Name = "，如 [FromQuery(Name = "ref")]，语义是查询串参数名）。
            foreach (var line in code.Split('\n'))
            {
                if (line.Contains("ServerId =") || line.Contains("ScriptParameterType.") || line.Contains("(Name = \"")) continue;

                foreach (System.Text.RegularExpressions.Match m in R1.Matches(line))
                    if (Snake.IsMatch(m.Groups[1].Value)) names.Add(m.Groups[1].Value);
                foreach (System.Text.RegularExpressions.Match m in R2.Matches(line))
                    names.Add(m.Groups[1].Value);
                foreach (System.Text.RegularExpressions.Match m in R3.Matches(line))
                    names.Add(m.Groups[1].Value);
            }
        }
        return names;
    }

    /// <summary>
    /// 剥离 C# 行注释（// 与 ///）与块注释（/* */），带字符串字面量感知（"..." 内的 // 不视为注释）。
    /// 简易实现：不处理逐字字符串 @"" 与原始字符串 """ 中的转义/双引号形态——工具类源码不使用，够用即可。
    /// </summary>
    private static string StripComments(string code)
    {
        var sb = new System.Text.StringBuilder(code.Length);
        var inStr = false; var inLine = false; var inBlock = false;
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            var n = i + 1 < code.Length ? code[i + 1] : '\0';
            if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
            if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
            if (inStr)
            {
                sb.Append(c);
                if (c == '\\' && n == '"') { sb.Append(n); i++; }
                else if (c == '"') inStr = false;
                continue;
            }
            if (c == '"') { inStr = true; sb.Append(c); continue; }
            if (c == '/' && n == '/') { inLine = true; i++; continue; }
            if (c == '/' && n == '*') { inBlock = true; i++; continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string ExtractJsonString(string json, string field)
    {
        var m = Regex.Match(json, $"\"{field}\"\\s*:\\s*\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : string.Empty;
    }
}
