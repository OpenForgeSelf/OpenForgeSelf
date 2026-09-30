using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// 内置图标库 forge（用户拍板：插件必须自带一套**零许可证负担**且好看的图标）。
/// 这里钉的是"能不能当真用"：数量、绘制规范一致性、许可字段、幂等首植、跨项目可见。
/// </summary>
[Collection("XCode")]
public class BuiltinIconTests : IDisposable
{
    readonly string _dbDir;
    readonly CatalogRepository _catalog = new();

    public BuiltinIconTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDsIcon_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={Path.Combine(_dbDir, "DesignSystem.db")}", null, "SQLite");
        EntityFactory.InitConnection(DesignSystemTables.ConnName);
        DesignIcon.Meta.Cache.Expire = 0;
    }

    public void Dispose() => GC.SuppressFinalize(this);

    [Fact]
    public void 内置集合_数量够日常界面用()
    {
        BuiltinIcons.All.Count.Should().BeGreaterThanOrEqualTo(24, "导航/CRUD/状态/图表这几类动作都要有得选，否则界面必然回退到文字或 emoji");
        BuiltinIcons.All.Select(i => i.Code).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void 绘制规范全套一致_24网格_1点5描边_不填充()
    {
        foreach (var icon in BuiltinIcons.All)
        {
            icon.Code.Should().MatchRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", "代码即令牌名，必须 kebab-case 稳定标识");
            icon.Name.Should().NotBeNullOrWhiteSpace("界面要显示中文名");
            icon.Tags.Should().NotBeNullOrWhiteSpace("图标靠关键词检索，没标签等于找不到");

            icon.SvgBody.Should().StartWith("<g ")
                .And.Contain("stroke-width=\"1.5\"")
                .And.Contain("fill=\"none\"")
                .And.Contain("stroke=\"currentColor\"")
                .And.EndWith("</g>", "一套图标只能有一种笔宽与取色方式，混了就谈不上设计系统");
            icon.SvgBody.Should().MatchRegex("<(path|circle|rect|ellipse|line|polyline) ", "svgBody 必须是真实图形，不是空白占位");
        }
    }

    [Fact]
    public void 每个图标都写清用途_避免界面乱配()
    {
        // 一套图标只有图形没有约定，很快就会被用成"随便挑一个顺眼的"
        BuiltinIcons.All.Should().OnlyContain(i => i.Usage.Length > 0, "每个图标都要说明什么时候用它");
        BuiltinIcons.All.Where(i => i.Code is "close" or "alert-triangle" or "info")
            .Should().HaveCount(3, "状态类（关闭/警告/说明）必须成组存在，界面才不会拿 play 当删除用");
    }

    [Fact]
    public void 首植落库_collection与许可与网格都对()
    {
        var written = BuiltinIcons.Seed(_catalog);
        written.Should().Be(BuiltinIcons.All.Count);

        var rows = DesignIcon.FindAll(DesignIcon._.ProjectId == DesignSystemConstants.BuiltinProjectId);
        rows.Should().HaveCount(BuiltinIcons.All.Count);
        rows.Should().OnlyContain(r => r.Collection == DesignSystemConstants.BuiltinIconCollection);
        rows.Should().OnlyContain(r => r.License == "Owned");
        rows.Should().OnlyContain(r => r.GridPx == 24 && Math.Abs(r.StrokeWidth - 1.5) < 0.001);
        rows.Should().OnlyContain(r => r.ViewBox == "0 0 24 24");
    }

    [Fact]
    public void 首植幂等_第二次不再写()
    {
        BuiltinIcons.Seed(_catalog).Should().BeGreaterThan(0);

        BuiltinIcons.SeedIfMissing(_catalog).Should().Be(0, "每次启动都可能调用，不能反复重写全套");
        DesignIcon.FindAll(DesignIcon._.ProjectId == DesignSystemConstants.BuiltinProjectId)
            .Should().HaveCount(BuiltinIcons.All.Count, "重复调用不得长出新行");
    }

    [Fact]
    public void 任意项目都能读到内置集_且可按集合与关键词过滤()
    {
        BuiltinIcons.Seed(_catalog);
        var projects = new DesignProjectService();
        var p = projects.Create(new ProjectInput { Code = $"ic-{Guid.NewGuid():N}"[..22], Name = "图标可见性" });

        var list = _catalog.ListIcons(p.Id, null, null);
        list.Should().HaveCountGreaterThanOrEqualTo(BuiltinIcons.All.Count, "项目视图必须连带内置库，否则用户以为没图标");

        _catalog.ListIcons(p.Id, DesignSystemConstants.BuiltinIconCollection, null)
            .Should().OnlyContain(i => i.Collection == DesignSystemConstants.BuiltinIconCollection);

        var search = _catalog.ListIcons(p.Id, null, "搜索");
        search.Should().ContainSingle(i => i.Code == "search");
    }

    [Fact]
    public void 内置库只读_项目不得写入内置集合()
    {
        BuiltinIcons.Seed(_catalog);

        var act = () => _catalog.SaveIcon(DesignSystemConstants.BuiltinProjectId,
            new IconInput { Code = "hack", SvgBody = "<g></g>" });
        act.Should().Throw<DesignConflictException>().WithMessage("*只读*");
    }
}
