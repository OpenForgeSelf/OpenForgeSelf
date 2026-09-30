using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// 别名图与分层校验测试（AC4 / design G4、G6、G17）——纯逻辑，不碰库。
/// 覆盖：正常链解析、主题覆盖合并、自指环、互指环、超长链、悬空别名、逆向引用、无值无别名。
/// </summary>
public class TokenGraphTests
{
    static TokenNode Prim(String path, String value) => new(path, TokenTiers.Primitive, TokenTypes.Color, value, null);
    static TokenNode Sem(String path, String alias) => new(path, TokenTiers.Semantic, TokenTypes.Color, null, alias);
    static TokenNode Comp(String path, String alias) => new(path, TokenTiers.Component, TokenTypes.Color, null, alias);

    [Fact]
    public void 三层链解析到末端字面值()
    {
        var g = new TokenGraph(
        [
            Prim("color.brand.500", "#7c3aed"),
            Sem("semantic.brand", "color.brand.500"),
            Comp("component.button.background", "semantic.brand"),
        ]);

        var r = g.Resolve("component.button.background");
        Assert.True(r.IsOk);
        Assert.Equal("#7c3aed", r.Value);
        Assert.Equal("color.brand.500", r.SourcePath);
        Assert.Equal(["component.button.background", "semantic.brand", "color.brand.500"], r.Chain);
    }

    [Fact]
    public void 主题覆盖层顶掉共享层同路径()
    {
        var shared = new[] { Prim("color.brand.500", "#7c3aed"), Sem("semantic.brand", "color.brand.500") };
        var dark = new[] { new TokenNode("color.brand.500", TokenTiers.Primitive, TokenTypes.Color, "#a78bfa", null) };

        var g = new TokenGraph(shared, dark, "dark");
        Assert.Equal("#a78bfa", g.Resolve("semantic.brand").Value);

        // 未覆盖的路径必须回落到共享层
        var lightOnly = new TokenNode("semantic.surface", TokenTiers.Semantic, TokenTypes.Color, "#ffffff", null);
        var g2 = new TokenGraph([.. shared, lightOnly], dark, "dark");
        Assert.Equal("#ffffff", g2.Resolve("semantic.surface").Value);
    }

    [Fact]
    public void 自指别名判环()
    {
        var g = new TokenGraph([new TokenNode("a", TokenTiers.Semantic, TokenTypes.Color, null, "a")]);
        var r = g.Resolve("a");
        Assert.Equal(ResolveStatus.Cycle, r.Status);
        Assert.Contains("a", r.Chain);
    }

    [Fact]
    public void 互指别名判环且链里能看出参与项()
    {
        var g = new TokenGraph(
        [
            new TokenNode("a", TokenTiers.Semantic, TokenTypes.Color, null, "b"),
            new TokenNode("b", TokenTiers.Semantic, TokenTypes.Color, null, "a"),
        ]);

        Assert.Equal(ResolveStatus.Cycle, g.Resolve("a").Status);
        Assert.Equal(ResolveStatus.Cycle, g.Resolve("b").Status);

        var diags = g.Validate();
        Assert.Equal(2, diags.Count);
        Assert.All(diags, d => Assert.Contains("成环", d.Message));
    }

    [Fact]
    public void 别名链超过深度上限报错而非继续深挖()
    {
        var nodes = new List<TokenNode>();
        for (var i = 0; i < DesignSystemConstants.MaxAliasDepth + 3; i++)
            nodes.Add(new TokenNode($"t{i}", TokenTiers.Semantic, TokenTypes.Color, null, $"t{i + 1}"));
        nodes.Add(Prim($"t{DesignSystemConstants.MaxAliasDepth + 3}", "#000000"));

        var r = new TokenGraph(nodes).Resolve("t0");
        Assert.Equal(ResolveStatus.TooDeep, r.Status);
    }

    [Fact]
    public void 悬空别名不猜测不回退()
    {
        var g = new TokenGraph([Sem("semantic.brand", "color.does-not-exist")]);
        var r = g.Resolve("semantic.brand");
        Assert.Equal(ResolveStatus.Missing, r.Status);
        Assert.Equal("", r.Value);
        Assert.Contains("不存在", g.Validate().Single().Message);
    }

    [Fact]
    public void 逆向引用被拒_semantic不可指向component()
    {
        var g = new TokenGraph(
        [
            Comp("component.button.background", "#111111"),
            Sem("semantic.brand", "component.button.background"),
        ]);

        Assert.Equal(ResolveStatus.LayerViolation, g.Resolve("semantic.brand").Status);
    }

    [Fact]
    public void 同层别名允许()
    {
        var g = new TokenGraph(
        [
            Prim("color.brand.500", "#7c3aed"),
            Sem("semantic.brand", "color.brand.500"),
            new TokenNode("semantic.brand-strong", TokenTiers.Semantic, TokenTypes.Color, null, "semantic.brand"),
        ]);

        Assert.Equal(ResolveStatus.Ok, g.Resolve("semantic.brand-strong").Status);
        Assert.Equal("#7c3aed", g.Resolve("semantic.brand-strong").Value);
    }

    [Fact]
    public void 无值无别名判空()
    {
        var g = new TokenGraph([new TokenNode("x", TokenTiers.Semantic, TokenTypes.Color, null, null)]);
        Assert.Equal(ResolveStatus.Empty, g.Resolve("x").Status);
    }

    [Fact]
    public void 校验只报坏项_健康图无诊断()
    {
        var good = new TokenGraph([Prim("color.brand.500", "#7c3aed"), Sem("semantic.brand", "color.brand.500")]);
        Assert.False(good.HasBlockingErrors(out var none));
        Assert.Empty(none);

        var mixed = new TokenGraph(
        [
            Prim("color.brand.500", "#7c3aed"),
            Sem("semantic.brand", "color.brand.500"),
            Sem("semantic.broken", "color.nope"),
        ]);
        Assert.True(mixed.HasBlockingErrors(out var diags));
        Assert.Single(diags);
        Assert.Equal("semantic.broken", diags[0].Path);
    }

    [Fact]
    public void 有效色解析接受hex与oklch两种值()
    {
        var g = new TokenGraph(
        [
            new TokenNode("color.brand.500", TokenTiers.Primitive, TokenTypes.Color, "oklch(62% 0.19 285)", null),
            new TokenNode("color.accent.500", TokenTiers.Primitive, TokenTypes.Color, "#0ea5e9", null),
        ]);

        var a = g.ResolveColor("color.brand.500");
        var b = g.ResolveColor("color.accent.500");
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(0.62, a!.Value.L, 2);
        Assert.Equal("#0ea5e9", Oklch.ToRgb8(b!.Value).ToHex());
    }

    [Fact]
    public void All合并去重_主题覆盖不产生重复路径()
    {
        var shared = new[] { Prim("color.brand.500", "#7c3aed"), Prim("color.gray.500", "#6b7280") };
        var themed = new[] { Prim("color.brand.500", "#a78bfa") };

        var paths = new TokenGraph(shared, themed, "dark").All().Select(n => n.Path).ToList();
        Assert.Equal(2, paths.Count);
        Assert.DoesNotContain(paths, p => paths.Count(x => x == p) > 1);
    }
}
