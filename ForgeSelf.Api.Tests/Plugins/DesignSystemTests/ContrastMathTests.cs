using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// WCAG 2.2 对比度黄金值测试（AC10）。
/// 判据只用 2.2 规范值；APCA 不作门禁（见 docs/07-decisions/not-taken-decisions.md）。
/// </summary>
public class ContrastMathTests
{
    [Theory]
    [InlineData("#ffffff", "#000000", 21.0)]
    [InlineData("#ffffff", "#767676", 4.54)]
    [InlineData("#ffffff", "#777777", 4.48)]
    [InlineData("#ffffff", "#959595", 3.0)]
    [InlineData("#000000", "#000000", 1.0)]
    public void 比率匹配已知色对(string a, string b, double expected)
    {
        var r = ContrastMath.Ratio(a, b);
        Assert.True(Math.Abs(r - expected) < 0.02, $"{a}/{b} 期望 {expected}，实际 {r:F3}");
    }

    [Fact]
    public void 参考紫底白字达AA_深slate底不足正文AA()
    {
        // 这正是 v1 的 selfCheck 只写散文、从不计算的那一条；现在由数学判定
        Assert.True(ContrastMath.MeetsAa(ContrastMath.Ratio("#7c3aed", "#ffffff"), ContrastMath.Usage.TextNormal),
            "Stardust 参考紫 #7c3aed 作前景配白底必须达正文 AA");

        var onDark = ContrastMath.Ratio("#7c3aed", "#0f172a");
        Assert.True(onDark is > 3.0 and < 4.5, $"紫配深 slate 应落在非文本达标区间(3~4.5)，实际 {onDark:F3}");
        Assert.True(ContrastMath.MeetsAa(onDark, ContrastMath.Usage.NonTextUi));
        Assert.False(ContrastMath.MeetsAa(onDark, ContrastMath.Usage.TextNormal));
    }

    [Fact]
    public void 不可解析颜色返回负一_区分没算与算出一()
    {
        Assert.Equal(-1, ContrastMath.Ratio("#zzzzzz", "#ffffff"));
        Assert.Equal(-1, ContrastMath.Ratio(null, "#ffffff"));
    }

    [Theory]
    [InlineData("#ffffff", "#000000")]
    [InlineData("#000000", "#ffffff")]
    [InlineData("#7c3aed", "#ffffff")]
    [InlineData("#ffffff", "#7c3aed")]
    public void 比率与前后景顺序无关(string a, string b)
    {
        Assert.Equal(ContrastMath.Ratio(a, b), ContrastMath.Ratio(b, a), 6);
    }

    [Fact]
    public void 正文AA阈值四点五_灰阶边界正确()
    {
        Assert.Equal(ContrastMath.Level.Aaa, ContrastMath.Judge(ContrastMath.Ratio("#ffffff", "#000000"), ContrastMath.Usage.TextNormal));
        Assert.Equal(ContrastMath.Level.Aa, ContrastMath.Judge(ContrastMath.Ratio("#ffffff", "#767676"), ContrastMath.Usage.TextNormal));
        Assert.Equal(ContrastMath.Level.Fail, ContrastMath.Judge(ContrastMath.Ratio("#ffffff", "#777777"), ContrastMath.Usage.TextNormal));
    }

    [Fact]
    public void 大字与非文本走三点一()
    {
        var r = ContrastMath.Ratio("#ffffff", "#949494"); // ≈3.04：够大字/非文本，不够正文
        Assert.True(ContrastMath.MeetsAa(r, ContrastMath.Usage.TextLarge));
        Assert.True(ContrastMath.MeetsAa(r, ContrastMath.Usage.NonTextUi));
        Assert.False(ContrastMath.MeetsAa(r, ContrastMath.Usage.TextNormal));
    }

    [Fact]
    public void 非文本无AAA档_判级不误升()
    {
        Assert.True(double.IsPositiveInfinity(ContrastMath.AaaThreshold(ContrastMath.Usage.NonTextUi)));
        Assert.Equal(ContrastMath.Level.Aa, ContrastMath.Judge(ContrastMath.Ratio("#ffffff", "#000000"), ContrastMath.Usage.NonTextUi));
    }

    [Theory]
    [InlineData(24.0, 400, true)]
    [InlineData(23.9, 400, false)]
    [InlineData(18.66, 700, true)]
    [InlineData(18.66, 600, false)]
    [InlineData(14.0, 900, false)]
    public void 大字判定按px与字重双条件(double size, int weight, bool large)
    {
        Assert.Equal(large, ContrastMath.IsLargeText(size, weight));
    }

    [Fact]
    public void 装饰类不门禁_等级none且视为通过()
    {
        Assert.Equal(ContrastMath.Level.None, ContrastMath.Judge(1.2, ContrastMath.Usage.Decorative));
        Assert.True(ContrastMath.MeetsAa(1.2, ContrastMath.Usage.Decorative));
    }

    [Theory]
    [InlineData(ContrastMath.Level.Aaa, "aaa")]
    [InlineData(ContrastMath.Level.Aa, "aa")]
    [InlineData(ContrastMath.Level.Fail, "fail")]
    [InlineData(ContrastMath.Level.None, "none")]
    public void 写库标签稳定(ContrastMath.Level level, string tag)
    {
        Assert.Equal(tag, ContrastMath.LevelTag(level));
    }

    [Fact]
    public void 阈值边界容差_四点五整算作达标()
    {
        Assert.True(ContrastMath.MeetsAa(4.5, ContrastMath.Usage.TextNormal));
        Assert.False(ContrastMath.MeetsAa(4.4999, ContrastMath.Usage.TextNormal));
    }
}
