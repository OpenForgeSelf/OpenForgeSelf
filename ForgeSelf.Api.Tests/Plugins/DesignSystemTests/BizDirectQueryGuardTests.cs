using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// Biz 直查守卫（2026-10-04 用户口径：实体查询一律包进各实体 .Biz.cs 的高级查询方法）。
/// 插件生产码里不许再出现 <c>DesignXxx.Find/FindAll/FindCount/FindFirst/FindPage/FindValue</c>；
/// 读路径若要再改（分页、超时、失效口径），只有 Biz 那一层是单一出口。
/// .Biz.cs 自身豁免：高级查询的实现体本来就要调这些方法。
/// </summary>
public class BizDirectQueryGuardTests
{
    private readonly ITestOutputHelper _log;

    public BizDirectQueryGuardTests(ITestOutputHelper log) => _log = log;

    private static readonly string[] Entities =
    {
        "DesignAsset", "DesignAudit", "DesignComponent", "DesignComponentVariant", "DesignFontFace",
        "DesignGuideline", "DesignIcon", "DesignProject", "DesignRelease", "DesignScreen",
        "DesignShadowLayer", "DesignTheme", "DesignToken"
    };

    /// <summary>
    /// 禁的不只是裸 `FindAll`：生成器给每个实体另造的 `FindByXxx` / `FindAllByXxx` 助手函数体首行就是
    /// `if (Meta.Session.Count &lt; 1000) return Meta.Cache.Find...`，那才是真会读到过期视图的路径。
    /// 所以实体上的任何 `Find*` 静态调用都必须留在 .Biz.cs 里，外面一律走 Query*。
    /// </summary>
    private static readonly Regex BareQuery = new(
        @"\b(" + string.Join("|", Entities) + @")\.Find[A-Za-z0-9]*\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex BizQueryCall = new(@"\.Query(All|First|Count)\s*\(", RegexOptions.Compiled);

    /// <summary>生成器给每个实体都造了一个走实体缓存的 FindByCode；插件从未调用它，调了就等于把缓存路径悄悄放回来。</summary>
    private static readonly Regex CacheHelperCall = new(@"\bDesignProject\.FindByCode\s*\(", RegexOptions.Compiled);

    private static string FindRepoRoot()
    {
        // 锚点必须是「只有仓库根才有」的文件：测试输出目录下也存在 bin/.../Plugins/DesignSystem/（只放 DLL），
        // 拿目录存在性当锚点会先命中它，扫到 0 个源文件，于是"零违规"变成空转假绿。
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Plugins", "DesignSystem", "DesignSystem.csproj")))
            dir = dir.Parent;
        File.Exists(Path.Combine(dir?.FullName ?? "", "Plugins", "DesignSystem", "DesignSystem.csproj"))
            .Should().BeTrue("测试需能从 bin 目录向上定位到真正的仓库根（判据用 DesignSystem.csproj，不用目录名）");
        return dir!.FullName;
    }

    private static IEnumerable<string> ProductionSources()
    {
        var root = Path.Combine(FindRepoRoot(), "Plugins", "DesignSystem");
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".Biz.cs", StringComparison.OrdinalIgnoreCase))
            .Where(f =>
            {
                var sep = Path.DirectorySeparatorChar;
                return !f.Contains($"{sep}bin{sep}") && !f.Contains($"{sep}obj{sep}");
            })
            .ToList();
    }

    [Fact]
    public void Biz直查_生产码不得有裸实体查询()
    {
        var files = ProductionSources().ToList();
        files.Should().NotBeEmpty("插件源码目录必须被读到，否则下面的 Empty 断言是空转");

        var hits = new List<string>();
        var bizCalls = 0;
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            var rel = Path.GetRelativePath(FindRepoRoot(), file).Replace('\\', '/');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("//")) continue;
                if (BareQuery.IsMatch(line))
                    hits.Add($"{rel}:{i + 1} {line}");
                bizCalls += BizQueryCall.Matches(line).Count;
            }
        }

        // 读数常驻打印：没有这行，"0 违规"到底是因为干净还是因为没扫到，事后无法分辨
        _log.WriteLine($"扫描 {files.Count} 个生产码文件；Biz 高级查询调用 {bizCalls} 处；裸实体查询违规 {hits.Count} 处");

        bizCalls.Should().BeGreaterThan(0,
            "阳性对照：这批文件里确实有走 Biz 高级查询的调用（实测应接近 47 处），否则「零裸查询」可能只是没扫到东西");
        hits.Should().BeEmpty(
            $"实体查询必须走 .Biz.cs 的高级查询方法，发现 {hits.Count} 处裸调用：\n{string.Join("\n", hits)}");
    }

    [Fact]
    public void Biz直查_生成器的缓存助手从未被调用()
    {
        var hits = ProductionSources()
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
            .Where(x => !x.line.Trim().StartsWith("//") && CacheHelperCall.IsMatch(x.line))
            .Select(x => $"{Path.GetRelativePath(FindRepoRoot(), x.f).Replace('\\', '/')}:{x.i + 1}")
            .ToList();

        hits.Should().BeEmpty("DesignProject.FindByCode 函数体走实体缓存，一旦被调用就把「读侧走缓存」这条路悄悄放回来了");
    }

    [Theory]
    [InlineData("var rows = DesignToken.FindAll(exp);", true)]
    [InlineData("var n = DesignProject.FindCount(exp);", true)]
    [InlineData("var one = DesignGuideline.FindFirst(exp);", true)]
    [InlineData("var p = DesignRelease.FindPage(exp, page);", true)]
    [InlineData("var one = DesignGuideline.FindByProjectIdAndCode(7, \"core-tone\");", true)]
    [InlineData("var rows = DesignGuideline.FindAllByProjectId(7);", true)]
    [InlineData("public static IList<DesignToken> QueryAll(Expression exp) => FindAll(exp);", false)]
    [InlineData("var rows = DesignToken.QueryAll(exp);", false)]
    public void 反向探针_守卫必须对裸实体查询变红(string snippet, bool shouldHit)
    {
        BareQuery.IsMatch(snippet).Should().Be(shouldHit, $"扫描器对这段的形状判断错了：{snippet}");
    }
}
