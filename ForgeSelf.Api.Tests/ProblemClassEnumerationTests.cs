using System.Text.RegularExpressions;
using FluentAssertions;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 问题类类级枚举守卫（G2/G3，problem-class-closure 机制首批，2026-10-17）。
/// 每个测试对应一个 PC 的不变量，能枚举就不 grep（BR4）；全部带阳性对照防空转。
/// 台账与判据真源：docs/11-troubleshooting/problem-classes/（allowlist 从此处读，单一来源）。
/// </summary>
public class ProblemClassEnumerationTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "scripts")))
            dir = dir.Parent;
        dir.Should().NotBeNull("测试需能从 bin 目录向上定位仓库根");
        return dir!.FullName;
    }

    /// <summary>读 PC front-matter 的 allowlist 相对路径（豁免单一来源，不在测试里复制清单）。</summary>
    private static List<string> PcAllowlistPaths(string pcFilePrefix)
    {
        var root = FindRepoRoot();
        var file = Directory.GetFiles(Path.Combine(root, @"docs\11-troubleshooting\problem-classes"), pcFilePrefix + "-*.md").SingleOrDefault();
        file.Should().NotBeNull($"台账中应恰好有一个 {pcFilePrefix} 档案");
        var lines = File.ReadAllLines(file!);
        var result = new List<string>();
        var inBlock = false;
        foreach (var l in lines)
        {
            if (l.StartsWith("allowlist:")) { inBlock = true; continue; }
            if (inBlock)
            {
                if (!l.StartsWith("  - ") && !string.IsNullOrWhiteSpace(l)) break;
                var m = Regex.Match(l, "path:\\s*\"([^\"]+)\"");
                if (m.Success) result.Add(m.Groups[1].Value);
            }
        }
        return result;
    }

    private static IEnumerable<string> EnumerateCsFiles(params string[] roots)
    {
        var root = FindRepoRoot();
        foreach (var r in roots)
        {
            var dir = Path.Combine(root, r);
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains(@"\bin\") || f.Contains(@"\obj\")) continue;
                yield return f;
            }
        }
    }

    /// <summary>两棵插件树（Plugins/** 与 ForgeSelf.Api/Plugins/**，只扫一棵必漏——PC-001 §2）。</summary>
    private static readonly string[] PluginTrees = { "Plugins", @"ForgeSelf.Api\Plugins" };

    // ─────────────────────────────────────────────────────────────
    // PC-001：被 ctx.Get&lt;T&gt;() 消费的 Abstractions 契约，必须有 ctx.Register/RegisterLocal 或宿主 seed 证据
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void ContractConsumedViaCtxGet_MustBeRegisteredSomewhere()
    {
        var root = FindRepoRoot();

        // Abstractions 接口全集（消费点 T 限定于此，避免误伤任意泛型 Get）
        var abstractionsIfaces = new HashSet<string>(
            EnumerateCsFiles("ForgeSelf.Abstractions")
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"public interface (\w+)").Select(m => m.Groups[1].Value)));
        abstractionsIfaces.Count.Should().BeGreaterThan(10, "ForgeSelf.Abstractions 契约接口阳性对照");

        // 消费点：Plugins 两棵树里的 ctx.Get<T>()（T ∈ Abstractions 接口）；PC-001 allowlist 文件级豁免存量真缺陷（带 TODO+到期日）
        var pc001Allow = PcAllowlistPaths("PC-001");
        var consumed = new Dictionary<string, List<string>>();
        foreach (var file in EnumerateCsFiles(PluginTrees))
        {
            var relFile = Path.GetRelativePath(root, file).Replace('\\', '/');
            foreach (System.Text.RegularExpressions.Match m in Regex.Matches(File.ReadAllText(file), @"\bctx\.Get<\s*(\w+)\s*>\s*\("))
            {
                var t = m.Groups[1].Value;
                if (!abstractionsIfaces.Contains(t)) continue;
                if (pc001Allow.Contains(relFile)) continue;
                if (!consumed.TryGetValue(t, out var list)) consumed[t] = list = new List<string>();
                list.Add(relFile);
            }
        }
        consumed.Count.Should().BeGreaterThan(5, "确有跨插件 ctx.Get 消费（阳性对照，防空转）");

        // 注册证据：全仓（插件+宿主）出现过 Register<...T...>/RegisterLocal<...T...>，或宿主 seed 清单 typeof(T)
        var registrationText = string.Concat(EnumerateCsFiles(PluginTrees.Concat(new[] { "ForgeSelf.Api", "ForgeSelf.Core" }).ToArray()).Select(File.ReadAllText));
        var unregistered = consumed
            .Where(kv => !Regex.IsMatch(registrationText, @"\.Register(?:Local)?<[^>]*\b" + Regex.Escape(kv.Key) + @"\b[^>]*>")
                         && !registrationText.Contains($"typeof({kv.Key})"))
            .Select(kv => $"{kv.Key}（消费于 {string.Join(" ; ", kv.Value.Take(3))}）消费但无任何 Register/seed 证据")
            .ToList();

        unregistered.Should().BeEmpty("PC-001 不变量违反：兄弟插件 ctx.Get 不到的契约不会凭空可用——见 docs/11-troubleshooting/problem-classes/PC-001-*.md");
    }

    // ─────────────────────────────────────────────────────────────
    // PC-002：指向 /api/ 的前端 fetch 调用方，文件级必须有 token 供给证据（authFetch/buildHeaders/Authorization）
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void FrontendBareFetchToAuthedApi_MustCarryToken()
    {
        var root = FindRepoRoot();
        var srcDirs = new List<string> { Path.Combine(root, @"ForgeSelf.Web\src") };
        foreach (var tree in PluginTrees)
            if (Directory.Exists(Path.Combine(root, tree)))
                foreach (var d in Directory.EnumerateDirectories(Path.Combine(root, tree)))
                {
                    var web = Path.Combine(d, "web", "src");
                    if (Directory.Exists(web)) srcDirs.Add(web);
                }
        srcDirs.RemoveAll(d => !Directory.Exists(d));

        var fetchRe = new Regex(@"fetch\(\s*['""`\x60]/api/");
        var tokenRe = new Regex(@"authFetch|buildHeaders|Authorization");
        var allow = PcAllowlistPaths("PC-002");
        var offenders = new List<string>();
        var matchedFiles = 0;

        foreach (var dir in srcDirs)
            foreach (var f in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                         .Where(f => f.EndsWith(".ts") || f.EndsWith(".vue"))
                         .Where(f => !f.Contains(@"\node_modules\") && !f.Contains(@"\dist\")))
            {
                var text = File.ReadAllText(f);
                if (!fetchRe.IsMatch(text)) continue;
                matchedFiles++;
                var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
                if (allow.Contains(rel)) continue;
                if (!tokenRe.IsMatch(text)) offenders.Add(rel);
            }

        matchedFiles.Should().BeGreaterThanOrEqualTo(2, "确有指向 /api/ 的前端 fetch（阳性对照）");
        allow.Count.Should().BeGreaterThan(0, "PC-002 至少登记了匿名端点豁免（阳性对照：allowlist 解析生效）");
        offenders.Should().BeEmpty("裸 fetch 需鉴权端点必 401（坑 8）——改走 authFetch/http.ts 或在 PC-002 allowlist 带理由+到期日豁免");
    }

    // ─────────────────────────────────────────────────────────────
    // PC-003：插件侧 XCode 连接名 ⊆ XCodeConfig.PluginDbs 登记
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void PluginDalConnNames_MustBeRegisteredInPluginDbs()
    {
        var root = FindRepoRoot();

        // PluginDbs key 集合（从源码单点真源解析，不复制清单）
        var xcodeText = File.ReadAllText(Path.Combine(root, @"ForgeSelf.Api\Data\XCodeConfig.cs"));
        var block = Regex.Match(xcodeText, @"PluginDbs\s*\{\s*get;\s*\}\s*=\s*new\s+Dictionary<string,\s*string>\s*\{(?<body>.*?)\n\s*\};", RegexOptions.Singleline);
        block.Success.Should().BeTrue("能在 XCodeConfig.cs 定位 PluginDbs 初始化块");
        var registered = Regex.Matches(block.Groups["body"].Value, @"\[""(\w+)""\]\s*=")
            .Select(m => m.Groups[1].Value).ToHashSet();
        registered.Count.Should().BeGreaterThanOrEqualTo(10, "PluginDbs 现有 ≥10 个登记（阳性对照）");

        // 插件侧连接名引用：ConnName = "X"（const 与 BindTable）∪ DAL.Create("X")
        var connNames = new Dictionary<string, List<string>>();
        foreach (var file in EnumerateCsFiles(PluginTrees))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            foreach (System.Text.RegularExpressions.Match m in Regex.Matches(File.ReadAllText(file), @"ConnName\s*=\s*""(\w+)""|DAL\.Create\(""(\w+)"""))
            {
                var name = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                if (name == "ForgeSelf") continue; // 宿主库连接名（HostDbs），不属插件登记面
                if (!connNames.TryGetValue(name, out var list)) connNames[name] = list = new List<string>();
                list.Add(rel);
            }
        }
        connNames.Count.Should().BeGreaterThanOrEqualTo(10, "两棵插件树确有 ≥10 个连接名引用（阳性对照）");

        var missing = connNames
            .Where(kv => !registered.Contains(kv.Key))
            .Select(kv => $"{kv.Key}（引用于 {string.Join(" ; ", kv.Value.Take(3))}）未登记 PluginDbs → 发布态落 versions/<ver>/Data/，升级即丢")
            .ToList();
        missing.Should().BeEmpty("PC-003 不变量违反——见 docs/11-troubleshooting/problem-classes/PC-003-*.md");
    }

    // ─────────────────────────────────────────────────────────────
    // PC-034：插件 DI 构造服务，构造函数注入 IServiceProvider = MS DI 自解析为插件子容器本体
    //         → 宿主/跨插件契约（IChatCompletion/ISecretEncryptionService/…）经它解析恒 null
    //         （2026-11-14 smart-terminal 助手/凭据恒 424 实证）。
    // 不变量：进插件子容器的服务（AddSingleton/AddScoped/AddTransient）构造函数只许注入 IContext；
    //         宿主契约执行期经 ctx.Get<T>()（共享表/宿主 seed）或 ctx.Get<IServiceProvider>() 回落宿主根。
    // 正道对照：StHostGateway(IContext)、ImGatewayRouter(IContext)、AgentDelegationProvider（ctx 回落宿主根）。
    // ─────────────────────────────────────────────────────────────

    /// <summary>PC-034 检测核心：DI 注册实现类型 ∧ 同文件类构造函数含 IServiceProvider 参数 → 违规。</summary>
    internal static List<string> FindPc034Violations(IEnumerable<KeyValuePair<string, string>> files)
    {
        var violations = new List<string>();
        var registeredImpls = new HashSet<string>();
        foreach (var (_, text) in files)
            foreach (System.Text.RegularExpressions.Match m in Regex.Matches(
                text, @"Add(?:Singleton|Scoped|Transient)<\s*(?:(\w+)\s*,\s*)?(\w+)\s*>"))
            {
                var impl = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value;
                if (impl.Length > 0) registeredImpls.Add(impl);
            }

        foreach (var (rel, text) in files)
            foreach (var impl in registeredImpls)
            {
                // 类声明在本文件（含 C# 12 主构造函数形态）且任一构造函数注入 IServiceProvider
                if (!Regex.IsMatch(text, $@"class\s+{impl}\b")) continue;
                var ctorHasIsp = Regex.IsMatch(text, $@"(public|internal)\s+{impl}\s*\(([^)]*)\)")
                    && Regex.Matches(text, $@"(public|internal)\s+{impl}\s*\(([^)]*)\)")
                        .Any(cm => cm.Groups[2].Value.Contains("IServiceProvider"))
                    || Regex.IsMatch(text, $@"class\s+{impl}\s*\(([^)]*)\)")
                        && Regex.Matches(text, $@"class\s+{impl}\s*\(([^)]*)\)")
                        .Any(cm => cm.Groups[1].Value.Contains("IServiceProvider"));
                if (ctorHasIsp) violations.Add($"{rel}（{impl}）");
            }

        return violations;
    }

    /// <summary>守卫咬合自证：合成样本上检出 IServiceProvider 构造、放行 IContext 构造（防检测器静默失效）。</summary>
    [Fact]
    public void Pc034_Detector_FlagsServiceProviderCtor_AndPassesIContextCtor()
    {
        var samples = new List<KeyValuePair<string, string>>
        {
            new("Plugins/X/Broken.cs", """
                public class BrokenGateway : IGateway
                {
                    public BrokenGateway(IServiceProvider services) { }
                }
                // 注册：services.AddSingleton<IGateway, BrokenGateway>();
                """),
            new("Plugins/X/Good.cs", """
                public class GoodGateway : IGateway
                {
                    public GoodGateway(IContext ctx) { }
                }
                // 注册：services.AddSingleton<IGateway, GoodGateway>();
                """),
        };

        var hits = FindPc034Violations(samples);
        hits.Should().HaveCount(1, "IServiceProvider 构造必须被检出（守卫咬合证明）");
        hits[0].Should().Contain("Broken.cs");
    }

    [Fact]
    public void Pc034_DiConstructedPluginServices_MustInjectIContextNotServiceProvider()
    {
        var root = FindRepoRoot();
        // 只扫 Plugins/ 树：本 PC 的不变量面 = 进插件子 DI 容器的服务（注册点全在插件 Apply）。
        // ForgeSelf.Api/Plugins/ 树是宿主基建（PluginManager 等，宿主 DI 生命周期，注册进宿主容器），
        // 不属插件子容器服务面——PC-001 双树扫的是 ctx.Get 消费点，此处单树是有意收窄，非遗漏。
        var files = EnumerateCsFiles("Plugins")
            .Select(f => new KeyValuePair<string, string>(
                Path.GetRelativePath(root, f).Replace('\\', '/'), File.ReadAllText(f)))
            .ToList();

        // 阳性对照：确有 DI 注册面在扫（含本次修复主角 StHostGateway 的注册点）
        files.Select(f => f.Value).Should().Contain(t =>
            t.Contains("AddSingleton<IStHostGateway, StHostGateway>()"),
            "SmartTerminalPlugin 的注册点必须在扫描面内；消失说明扫描面或注册点变更，先更新本守卫");
        files.Count(f => Regex.IsMatch(f.Value, @"Add(?:Singleton|Scoped|Transient)<")).Should()
            .BeGreaterThan(20, "插件 DI 注册面阳性对照，防空转假绿");

        var allow = PcAllowlistPaths("PC-034");
        var offenders = FindPc034Violations(files)
            .Where(v => !allow.Contains(v.Split('（')[0]))
            .ToList();

        offenders.Should().BeEmpty(
            "PC-034 不变量违反：DI 构造的插件服务注入 IServiceProvider = 拿到插件子容器，宿主/跨插件契约恒解析为 null；" +
            "改注入 IContext（见 StHostGateway / ImGatewayRouter 正道）。确需豁免 → PC-034 front-matter allowlist（理由+到期日）");
    }
}
