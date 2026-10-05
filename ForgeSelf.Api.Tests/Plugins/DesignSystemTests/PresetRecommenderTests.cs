using ForgeSelf.Api.Plugins.DesignSystem.Services;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>§D 推荐器打分：industry+40 / kind+30 / keyword+10 封顶40 / tone+10 封顶30 / density+15；同分声明序；全0声明序。</summary>
public class PresetRecommenderTests
{
    [Fact]
    public void 行业匹配_加40()
    {
        var hits = PresetRecommender.Recommend(null, null, "finance", null, null, null, 8);
        var top = hits[0];
        top.Id.Should().Be("finance-trust");
        top.Score.Should().BeGreaterThanOrEqualTo(40);
        top.Reasons.Should().Contain(r => r.Contains("行业匹配", StringComparison.Ordinal));
    }

    [Fact]
    public void brief关键词推断行业_命中finance()
    {
        var hits = PresetRecommender.Recommend("做一套支付账单系统", null, null, null, null, null, 8);
        hits[0].Id.Should().Be("finance-trust");
    }

    [Fact]
    public void kind匹配_加30()
    {
        var hits = PresetRecommender.Recommend(null, "marketing", null, null, null, null, 8);
        // marketing 命中 media-bold / commerce-vivid / education-friendly / mobile-fresh 四家，取分数最高（关键词无加分时同 30，按声明序）
        hits[0].Score.Should().BeGreaterThanOrEqualTo(30);
    }

    [Fact]
    public void keyword_加10封顶40()
    {
        var hits = PresetRecommender.Recommend("一个后台管理系统 中台 报表 运营 管理", null, null, null, null, null, 8);
        var admin = hits.First(h => h.Id == "admin-calm");
        admin.Score.Should().BeGreaterThanOrEqualTo(40);   // 4 个关键词 ×10 封顶 40
        admin.Reasons.Count(r => r.StartsWith("关键词命中", StringComparison.Ordinal)).Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void tone_加10封顶30()
    {
        var hits = PresetRecommender.Recommend(null, null, null, ["calm", "professional", "reliable"], null, null, 8);
        hits[0].Id.Should().Be("admin-calm");
        hits[0].Score.Should().Be(30);                     // 3×10 封顶 30，无其他线索
    }

    [Fact]
    public void density_加15()
    {
        var hits = PresetRecommender.Recommend(null, null, null, null, "compact", null, 8);
        hits[0].Id.Should().Be("workbench-focus");         // compact 命中 workbench-focus + admin-calm + finance-trust 等，声明序首个=workbench-focus
        hits[0].Score.Should().Be(15);
    }

    [Fact]
    public void 全0线索_按声明序且reasons说明()
    {
        var hits = PresetRecommender.Recommend(null, null, null, null, null, null, 3);
        hits.Select(h => h.Id).Should().Equal(["admin-calm", "workbench-focus", "finance-trust"]);
        hits[0].Score.Should().Be(0);
        hits[0].Reasons.Should().Contain(r => r.Contains("无匹配线索", StringComparison.Ordinal));
    }

    [Fact]
    public void limit_钳制到1到目录数()
    {
        // M3 偏差登记：M1 时这里钳到 8（目录就是 8 个）。目录扩到 13 后上限必须跟着走，否则新 5 个永远推不出来
        PresetRecommender.Recommend(null, null, null, null, null, null, 99).Should().HaveCount(13);
        PresetRecommender.Recommend(null, null, null, null, null, null, 0).Should().HaveCount(1);
    }

    [Fact]
    public void brandColor_覆盖seedColor并置空hue()
    {
        var hits = PresetRecommender.Recommend(null, null, "finance", null, null, "#123456", 3);
        hits[0].Request.SeedColor.Should().Be("#123456");
        hits[0].Request.Hue.Should().BeNull();
    }

    [Fact]
    public void 未命中时_不丢预设默认值()
    {
        var hits = PresetRecommender.Recommend(null, null, null, null, null, null, 1);
        var r = hits[0].Request;
        r.Hue.Should().NotBeNull();
        r.Density.Should().NotBeNullOrWhiteSpace();
        r.Themes.Should().NotBeEmpty();
    }
}
