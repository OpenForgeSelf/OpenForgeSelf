using ForgeSelf.Api.Plugins.DesignSystem.Services;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>§D 预设目录契约：M1 建目录时 8 个、M3 起 13 个（§P 追加 5 个），id 唯一、Find 大小写不敏感、Request 深拷贝（改拷贝不影响目录）。</summary>
public class StylePresetsTests
{
    /// <summary>总数钉死是**有意的契约**：再加预设必须同时改这条，逼作者承认"目录变了"（推荐打分、衣柜分组都按它排）。</summary>
    [Fact]
    public void 目录_恰好13个且id唯一()
    {
        StylePresets.All.Should().HaveCount(13);
        StylePresets.All.Select(p => p.Id).Distinct(StringComparer.Ordinal).Should().HaveCount(13);
    }

    [Fact]
    public void 目录_每个预设必备字段非空()
    {
        foreach (var p in StylePresets.All)
        {
            p.Id.Should().NotBeNullOrWhiteSpace();
            p.Name.Should().NotBeNullOrWhiteSpace();
            p.Tagline.Should().NotBeNullOrWhiteSpace();
            p.Tones.Should().NotBeEmpty();
            p.Kinds.Should().NotBeEmpty();
            p.Industries.Should().NotBeEmpty();
            p.Keywords.Should().NotBeEmpty();
            p.Request.Should().NotBeNull();
            p.Request.Industry.Should().NotBeNullOrWhiteSpace();
            p.Request.Themes.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void Find_大小写不敏感()
    {
        StylePresets.Find("ADMIN-CALM")!.Id.Should().Be("admin-calm");
        StylePresets.Find("finance-trust")!.Id.Should().Be("finance-trust");
        StylePresets.Find("不存在的") .Should().BeNull();
        StylePresets.Find(null).Should().BeNull();
    }

    [Fact]
    public void Request_是深拷贝_改拷贝不影响目录()
    {
        var preset = StylePresets.Find("admin-calm")!;
        preset.Request.SeedColor = "#ff0000";
        preset.Request.Hue = 123;
        preset.Request.Density = "compact";

        var again = StylePresets.Find("admin-calm")!;
        again.Request.SeedColor.Should().BeNull();
        again.Request.Hue.Should().Be(255);
        again.Request.Density.Should().Be("default");
    }

    [Fact]
    public void 目录_已知预设关键值()
    {
        var finance = StylePresets.Find("finance-trust")!;
        finance.Request.Industry.Should().Be("finance");
        finance.Request.RadiusBase.Should().Be(4);
        finance.Request.Density.Should().Be("default");

        var education = StylePresets.Find("education-friendly")!;
        education.Request.Density.Should().Be("comfortable");
        education.Request.RadiusBase.Should().Be(12);

        var workbench = StylePresets.Find("workbench-focus")!;
        workbench.Request.Density.Should().Be("compact");
        workbench.Request.Industry.Should().Be("devtools");
    }
}
