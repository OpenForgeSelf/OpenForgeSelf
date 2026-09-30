using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// OKLCH 色彩数学黄金值测试（AC10 的色空间部分）。
/// 期望值取 CSS Color 4 规范的 oklch 参考值，容差 1e-3：
/// 生成器与审计都建立在这套转换上，转换漂了整套设计系统不可信。
/// </summary>
public class OklchMathTests
{
    const double Eps = 1e-3;

    [Theory]
    [InlineData("#ffffff", 1.000, 0.000)]
    [InlineData("#000000", 0.000, 0.000)]
    [InlineData("#808080", 0.600, 0.000)]
    [InlineData("#ff0000", 0.628, 0.258)]
    [InlineData("#00ff00", 0.866, 0.295)]
    [InlineData("#0000ff", 0.452, 0.313)]
    public void Hex转Oklch_匹配规范参考值(string hex, double l, double c)
    {
        var o = Oklch.ParseHex(hex)!.Value;
        Assert.Equal(l, o.L, 3);
        Assert.Equal(c, o.C, 3);
    }

    [Theory]
    [InlineData("#ff0000", 29.234)]
    [InlineData("#00ff00", 142.495)]
    [InlineData("#0000ff", 264.052)]
    public void Hex转Oklch_色相匹配规范参考值(string hex, double h)
    {
        var o = Oklch.ParseHex(hex)!.Value;
        Assert.True(Math.Abs(Oklch.NormalizeHue(o.H) - h) < 0.05, $"{hex} 期望 H={h}，实际 {o.H}");
    }

    [Theory]
    [InlineData("#ffffff")]
    [InlineData("#000000")]
    [InlineData("#767676")]
    [InlineData("#7c3aed")]
    [InlineData("#0ea5e9")]
    [InlineData("#22c55e")]
    public void Hex往返_逐位相同(string hex)
    {
        var o = Oklch.ParseHex(hex)!.Value;
        Assert.Equal(hex.ToLowerInvariant(), Oklch.ToRgb8(o).ToHex());
    }

    [Fact]
    public void Oklch往返_纯灰保持彩度零()
    {
        var o = Oklch.ParseHex("#1a1a1a")!.Value;
        Assert.True(o.C < 1e-6, $"纯灰彩度应≈0，实际 {o.C}");
        Assert.Equal("#1a1a1a", Oklch.ToRgb8(o).ToHex());
    }

    [Theory]
    [InlineData("#7c3aed")]
    [InlineData("#0000ff")]
    [InlineData("#ff0000")]
    public void 解析并序列化_oklch函数形式可回读(string hex)
    {
        var o = Oklch.ParseHex(hex)!.Value;
        var css = o.ToCss();
        var back = Oklch.ParseOklch(css);
        Assert.NotNull(back);
        var b = back!.Value;
        Assert.Equal(o.L, b.L, 2);
        Assert.Equal(o.C, b.C, 2);
        Assert.True(Math.Abs(Oklch.NormalizeHue(o.H) - b.H) < 0.05);
    }

    [Fact]
    public void 百分号与小数两种明度写法等价()
    {
        var a = Oklch.ParseOklch("oklch(62% 0.19 285)")!.Value;
        var b = Oklch.ParseOklch("oklch(0.62 0.19 285)")!.Value;
        Assert.Equal(a.L, b.L, 4);
        Assert.Equal(a.C, b.C, 4);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#12g456")]
    [InlineData("not-a-color")]
    [InlineData("oklch(0.6 0.1)")]
    public void 非法输入返回null_不抛不猜(string? input)
    {
        Assert.Null(Oklch.ParseHex(input));
        Assert.Null(Oklch.ParseOklch(input));
    }

    [Fact]
    public void 三位简写与六位等价()
    {
        Assert.Equal("#ffcc00", Oklch.ToRgb8(Oklch.ParseHex("#fc0")!.Value).ToHex());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void 极值明度最大彩度为零(double l)
    {
        Assert.Equal(0, Oklch.MaxChroma(l, 285), 3);
    }

    [Fact]
    public void 最大彩度边界_域内可达域外不可达()
    {
        var max = Oklch.MaxChroma(0.62, 285);
        Assert.True(max > 0.05, $" violet 中等明度应有可用彩度，实际 {max}");
        Assert.True(Oklch.IsInGamut(new Oklch.Color(0.62, max * 0.99, 285)));
        Assert.False(Oklch.IsInGamut(new Oklch.Color(0.62, max + 0.05, 285)));
    }

    [Fact]
    public void 域外颜色只压彩度不改明度色相()
    {
        var o = new Oklch.Color(0.62, 0.45, 285); // 明显出界
        Assert.False(Oklch.IsInGamut(o));

        var fitted = Oklch.FitToGamut(o);
        Assert.True(Oklch.IsInGamut(fitted));
        Assert.Equal(o.L, fitted.L, Eps);
        Assert.Equal(o.H, Oklch.NormalizeHue(fitted.H), Eps);
        Assert.True(fitted.C < o.C);
    }

    [Fact]
    public void Oklab插值_端点复现且中点亮度单调()
    {
        var black = Oklch.ParseHex("#000000")!.Value;
        var white = Oklch.ParseHex("#ffffff")!.Value;

        Assert.Equal("#000000", Oklch.ToRgb8(Oklch.Mix(black, white, 0)).ToHex());
        Assert.Equal("#ffffff", Oklch.ToRgb8(Oklch.Mix(black, white, 1)).ToHex());

        var mid = Oklch.Mix(black, white, 0.5);
        Assert.True(mid.L > black.L && mid.L < white.L);
    }

    [Fact]
    public void 色相归一_负值与超界回绕()
    {
        Assert.Equal(350, Oklch.NormalizeHue(-10), 3);
        Assert.Equal(10, Oklch.NormalizeHue(370), 3);
        Assert.Equal(0, Oklch.NormalizeHue(360), 3);
    }

    [Theory]
    [InlineData("#ffffff", 1.0)]
    [InlineData("#000000", 0.0)]
    [InlineData("#808080", 0.2159)]
    [InlineData("#767676", 0.1811)]
    public void 相对亮度匹配参考值(string hex, double expected)
    {
        var y = Oklch.RelativeLuminance(Oklch.TryParseRgb8(hex)!.Value);
        Assert.True(Math.Abs(y - expected) < 5e-4, $"{hex} 期望 Y={expected}，实际 {y}");
    }
}
