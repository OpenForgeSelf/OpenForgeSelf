using FluentAssertions;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.TodoTracker;

/// <summary>
/// 项目路径归一金样表（PILOT-054 · BR-3 / AC-4）。
///
/// 判据口径：<b>同一 Key 即同一项目</b>，反例必须不同 Key —— 只测正例等于没测
/// （"/d/p 与 /d/p2 若被判成同一个项目，整个关联与统计就是错的，而正例照样全绿）。
/// 本测试工程 TFM 是 net10.0-windows（<c>ForgeSelf.Api.Tests.csproj</c>），故直接断言 Windows 行为。
/// </summary>
public class ProjectPathCanonicalizerTests
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [Theory]
    // 同一目录 D:\project 的四种写法 + 大小写差异 ⇒ 必须归一到同一串
    [InlineData("/d/project", @"D:\project")]
    [InlineData(@"D:\project", @"D:\project")]
    [InlineData("D:/project", @"D:\project")]
    [InlineData(@"D:\project\", @"D:\project")]
    [InlineData("D:/project/", @"D:\project")]
    [InlineData("/mnt/d/project", @"D:\project")]
    [InlineData(@"d:\PROJECT", @"D:\PROJECT")]
    [InlineData("\"D:\\project\"", @"D:\project")]
    [InlineData("  D:\\\\project  ", @"D:\project")]
    [InlineData(@"D:\a\.\b\..\project", @"D:\a\project")]
    // 盘符根保留尾分隔符（去掉就变成"D:"这种无效路径）
    [InlineData("/d/", @"D:\")]
    [InlineData("D:/", @"D:\")]
    // UNC 不做盘符替换，前导两个分隔符必须保住
    [InlineData(@"\\nas\work\proj", @"\\nas\work\proj")]
    // ~ 展开
    [InlineData("~/code/x", null)]
    [InlineData("/~/code/x", null)]
    public void 合法路径应归一为绝对根(string raw, string? expected)
    {
        var result = ProjectPathCanonicalizer.Normalize(raw);

        result.Ok.Should().BeTrue(because: $"{raw} 应能归一，实际原因：{result.Error}");
        if (expected != null) result.Root.Should().Be(expected);
        else result.Root.Should().StartWith(Home).And.EndWith(@"code\x");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData(@"proj\x")]              // 相对路径：不按进程 CWD 猜
    [InlineData("/etc/passwd")]          // 根但无盘符：Windows 下会被解成 C:\etc\passwd，静默指错目录
    [InlineData("relative")]
    public void 非法或缺盘符的路径应被拒绝并给出原因(string? raw)
    {
        var result = ProjectPathCanonicalizer.Normalize(raw);

        result.Ok.Should().BeFalse($"{raw} 不该被接受");
        result.Error.Should().NotBeNullOrWhiteSpace("失败必须带可读原因，界面要能自证成因");
        result.Root.Should().BeEmpty();
        result.Key.Should().BeEmpty();
    }

    [Fact]
    public void 超过列宽的路径应被拒绝()
    {
        var longPath = @"D:\" + new string('x', 600);

        var result = ProjectPathCanonicalizer.Normalize(longPath);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("500");
    }

    [Fact]
    public void 同一目录的四种写法应得到同一个Key()
    {
        var forms = new[] { "/d/project", @"D:\project", "D:/project/", "/mnt/d/project" };

        var keys = forms.Select(f => ProjectPathCanonicalizer.Normalize(f).Key).ToArray();

        keys.Should().OnlyContain(k => k == keys[0], "用户明确要求：一致的路径认为是同一个项目");
        forms.Select((f, i) => (f, k: keys[i])).Should().NotContain(x => string.IsNullOrEmpty(x.k));
    }

    [Fact]
    public void 大小写不同的同一目录应判为同一个项目()
    {
        ProjectPathCanonicalizer.SameProject(@"D:\Project", @"d:\pROJECT").Should().BeTrue();
    }

    [Theory]
    [InlineData("/d/project", "/d/project2")]
    [InlineData("/d/project", "/e/project")]
    [InlineData(@"D:\project", @"D:\project\sub")]
    [InlineData(@"D:\a\project", @"D:\b\project")]
    public void 不同目录必须判为不同项目_反例守卫(string a, string b)
    {
        ProjectPathCanonicalizer.SameProject(a, b).Should().BeFalse($"{a} 与 {b} 是两个目录，归一器不能把它们并成一条");
    }

    [Fact]
    public void 归一失败的Key不得参与同一性判定()
    {
        // 反例必须先证明前提真发生：若把"归一失败"当成 Key 相等，两个非法路径会被判成同一个项目
        ProjectPathCanonicalizer.SameProject("relative1", "relative2").Should().BeFalse();
        ProjectPathCanonicalizer.KeyOf(null).Should().BeNull();
    }

    [Fact]
    public void ResolveInside_项目内相对路径应拼成绝对路径且带Key()
    {
        var root = ProjectPathCanonicalizer.Normalize("/d/project").Root;

        var inside = ProjectPathCanonicalizer.ResolveInside(root, "docs/ai/pilot/2026-10-07-demo/01-intent.md");

        inside.Ok.Should().BeTrue(inside.Error);
        inside.Root.Should().Be($@"{root}\docs\ai\pilot\2026-10-07-demo\01-intent.md");
        inside.Key.Should().Be(inside.Root.ToUpperInvariant());
    }

    [Theory]
    [InlineData("../../Windows/system32")]
    [InlineData("docs/../../Windows")]
    public void ResolveInside_越出项目根必须被拒(string relative)
    {
        var root = ProjectPathCanonicalizer.Normalize(@"C:\Windows\Temp").Root;

        var inside = ProjectPathCanonicalizer.ResolveInside(root, relative);

        inside.Ok.Should().BeFalse($"{relative} 越出了项目根，必须拒绝而不是返回一个根外的路径");
        inside.Error.Should().Contain("越出项目根");
    }
}
