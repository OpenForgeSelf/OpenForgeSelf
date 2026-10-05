using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M3 AC14：规范的写入语义 —— **只补空 / 手改受保护 / overwrite 点名 / 引用校验零行写入 / 生成链路带回条数**。
///
/// 这一层是"用户数据会不会被我的生成器悄悄改掉"的唯一防线，所以每条断言都问同一个问题：
/// **改完再读回来，库里到底是哪几个字节**。计数对得上不代表内容对（自查表 #10）。
///
/// 隔离：本类专属随机临时库目录，只创建不删除（铁律 10）。
/// </summary>
[Collection("XCode")]
public class GuidelineServiceTests : IDisposable
{
    readonly String _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly GuidelineRepository _repo = new();
    readonly GuidelineService _svc;
    readonly AuditEngine _auditEngine;

    public GuidelineServiceTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDsGuidelineSvc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);
        DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={Path.Combine(_dbDir, "DesignSystem.db")}", null, "SQLite");
        EntityFactory.InitConnection(DesignSystemTables.ConnName);

        DesignProject.Meta.Cache.Expire = 0;
        DesignTheme.Meta.Cache.Expire = 0;
        DesignToken.Meta.Cache.Expire = 0;
        DesignShadowLayer.Meta.Cache.Expire = 0;
        DesignComponent.Meta.Cache.Expire = 0;
        DesignComponentVariant.Meta.Cache.Expire = 0;
        DesignIcon.Meta.Cache.Expire = 0;
        DesignAsset.Meta.Cache.Expire = 0;
        DesignScreen.Meta.Cache.Expire = 0;
        DesignFontFace.Meta.Cache.Expire = 0;
        DesignAudit.Meta.Cache.Expire = 0;
        DesignRelease.Meta.Cache.Expire = 0;
        DesignGuideline.Meta.Cache.Expire = 0;

        _auditEngine = new AuditEngine(_tokens, _projects, _audits);
        _svc = new GuidelineService(_projects, _tokens, _repo);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    /// <summary>建一个"生成过"的项目（有令牌才有可引用的路径）</summary>
    Int64 NewProject(GenerationRequest? req = null, String? kind = null, String? description = null)
    {
        var p = _projects.Create(new ProjectInput
        {
            Code = "svc-" + Guid.NewGuid().ToString("N")[..8],
            Name = "规范服务用例",
            Kind = kind,
            Description = description,
        });
        DesignGenerator.ApplyToProject(_tokens, _projects, p.Id, req ?? new GenerationRequest { Hue = 210 }, false);
        return p.Id;
    }

    [Fact]
    public void AC14_只补空_第二次生成零新增但库里仍是全量()
    {
        var id = NewProject();

        var first = _svc.Generate(id);
        first.Created.Should().BeEquivalentTo(GuidelineGenerator.Codes,
            "首次生成的 code 集必须恰好等于 §G2 契约清单");
        first.Skipped.Should().BeEmpty();
        _repo.List(id).Should().HaveCount(14);

        var second = _svc.Generate(id);
        second.Created.Should().BeEmpty("已存在的行又被建了一遍 = 唯一索引判重失效");
        second.Skipped.Should().HaveCount(14);
        second.Total.Should().Be(14);
        _repo.Count(id).Should().Be(14, "重复生成把条数翻倍 = 界面上会出现两套同 code 规范");
    }

    [Fact]
    public void AC14_手改行受重新生成保护_内容一字不变()
    {
        var id = NewProject();
        _svc.Generate(id);

        _svc.Save(id, "buttons", new GuidelinePatch { Title = "按钮（本项目特例）", Body = "改过的正文" })
            .Source.Should().Be("manual", "写过的行不标 manual = 下次生成会把它冲掉而用户毫不知情");

        // 快照必须在手改**之后**取：要证的是"重新生成没碰这一行"，拿手改前的时间戳比会必然不等
        var before = _repo.Find(id, "buttons")!;

        var again = _svc.Generate(id, false);
        again.SkippedProtected.Should().Equal("buttons");
        again.Created.Should().BeEmpty();

        var after = _repo.Find(id, "buttons")!;
        after.Title.Should().Be("按钮（本项目特例）");
        after.Body.Should().Be("改过的正文");
        after.UpdatedAt.Should().Be(before.UpdatedAt, "保护 = 连 UpdatedAt 都不能动，否则用户看不出自己那版有没有被等过");
    }

    [Fact]
    public void AC14_overwrite_逐条点名被覆盖的code()
    {
        var id = NewProject();
        _svc.Generate(id);
        _svc.Save(id, "buttons", new GuidelinePatch { Title = "手改标题" });

        var r = _svc.Generate(id, overwrite: true);
        r.Overwritten.Should().HaveCount(14, "overwrite=true 必须覆盖全部（含手改行）");
        r.Overwritten.Should().Contain("buttons");
        r.Created.Should().BeEmpty();
        r.SkippedProtected.Should().BeEmpty("overwrite 还跳过手改行 = 响应里的 overwritten 是假的");

        _repo.Find(id, "buttons")!.Title.Should().NotBe("手改标题");
    }

    [Fact]
    public void AC14_引用不存在的令牌_逐条列出且零行写入()
    {
        var id = NewProject();

        var err = Record.Exception(() => _svc.Save(id, "layout-grid", new GuidelinePatch
        {
            Title = "栅格",
            TokenRefs = ["space.6", "space.does-not-exist", "color.nope.500"],
        }));
        err.Should().BeOfType<ArgumentException>();
        err!.Message.Should().Contain("space.does-not-exist").And.Contain("color.nope.500")
            .And.NotContain("space.6", "存在的路径不该出现在错误里：列出来会让用户去改一个没问题的引用");
        _repo.Find(id, "layout-grid").Should().BeNull("校验失败却落了行 = 半条规范比没有规范更坏");
    }

    [Fact]
    public void AC14_非法词表与超限_文案给出可用值()
    {
        var id = NewProject();

        var badCat = Record.Exception(() => _svc.Save(id, "layout-grid", new GuidelinePatch { Category = "typography" }));
        badCat!.Message.Should().Contain("typography").And.Contain("layout");

        var badStatus = Record.Exception(() => _svc.Save(id, "layout-grid", new GuidelinePatch { Status = "deleted" }));
        badStatus!.Message.Should().Contain("deleted").And.Contain("归档");

        var tooMany = Record.Exception(() => _svc.Save(id, "layout-grid", new GuidelinePatch
        {
            Rules = Enumerable.Range(1, GuidelineCategories.MaxRulesPerGuideline + 1)
                .Select(i => new GuidelineRuleInput($"rule-{i}", "MUST", "规则文本")).ToList(),
        }));
        tooMany!.Message.Should().Contain(GuidelineCategories.MaxRulesPerGuideline.ToString());

        var badCode = Record.Exception(() => _svc.Save(id, "布局 Grid", new GuidelinePatch { Title = "x" }));
        badCode!.Message.Should().Contain("布局 Grid");

        _repo.Count(id).Should().Be(0, "上面四条都是被拒写入，一条都不该进库");
    }

    [Fact]
    public void AC14_密度从产物反推_三档各归各不串档()
    {
        var compact = NewProject(new GenerationRequest { Hue = 210, Density = "compact" });
        var comfortable = NewProject(new GenerationRequest { Hue = 210, Density = "comfortable" });
        var standard = NewProject(new GenerationRequest { Hue = 210 });

        _svc.Generate(compact);
        _svc.Generate(comfortable);
        _svc.Generate(standard);

        _repo.Find(compact, "density")!.GeneratorSeed.Should().Contain("density=compact");
        _repo.Find(comfortable, "density")!.GeneratorSeed.Should().Contain("density=comfortable");
        // 串档探针（实测踩过）：反推读 space.4 时 default 的 8px 会撞上 comfortable 的基准 8px，
        // 于是最普通的默认项目被标成 comfortable —— 规范开始引用错的间距档
        _repo.Find(standard, "density")!.GeneratorSeed.Should().Contain("density=default");

        _repo.Find(compact, "density")!.Body.Should().NotBe(_repo.Find(comfortable, "density")!.Body,
            "密度反推失效（都回落 default）时两条正文会一模一样，而种子照样写着不同");
    }

    [Fact]
    public void AC14_用途与行业进种子_控制台项目与产品项目不同()
    {
        var console = NewProject(kind: "console", description: "运维监控告警平台");
        var product = NewProject(kind: "product", description: "在线协作文档");

        _svc.Generate(console);
        _svc.Generate(product);

        _repo.Find(console, "page-patterns")!.GeneratorSeed.Should().Contain("kind=console");
        _repo.Find(product, "page-patterns")!.GeneratorSeed.Should().Contain("kind=product");
        _repo.Find(console, "page-patterns")!.Body.Should().NotBe(_repo.Find(product, "page-patterns")!.Body);
    }

    [Fact]
    public void AC14_归档行仍参与判重_且默认清单读不到它()
    {
        var id = NewProject();
        _svc.Generate(id);

        _svc.Archive(id, "motion")!.Status.Should().Be("archived");
        _repo.List(id).Select(g => g.Code).Should().NotContain("motion", "默认口径必须排除归档（界面不该显示被归档的规范）");
        _repo.List(id, "all").Select(g => g.Code).Should().Contain("motion", "归档是软删：快照与导出必须还看得见它");

        // 重新生成不能把归档行再建一条同 code 的（撞唯一索引 = 整批失败）
        var r = _svc.Generate(id);
        r.Created.Should().NotContain("motion");
        _repo.List(id, "all").Count(g => g.Code == "motion").Should().Be(1);

        // 恢复：PUT 带 status=adopted
        _svc.Save(id, "motion", new GuidelinePatch { Status = "adopted" });
        _repo.List(id).Select(g => g.Code).Should().Contain("motion");
    }

    [Fact]
    public void AC14_生成链路把现存条数带回响应_第二次不是0()
    {
        var generation = new GenerationService(_tokens, _projects, _catalog, _auditEngine) { Guidelines = _svc };
        var id = _projects.Create(new ProjectInput { Code = "run-" + Guid.NewGuid().ToString("N")[..8], Name = "链路项目" }).Id;

        var first = generation.Run(id, new GenerationRequest { Hue = 210 }, false);
        first.Guidelines.Should().Be(14, "generate 响应里的 guidelines 是界面判断「规范到底有没有」的唯一依据");

        var second = generation.Run(id, new GenerationRequest { Hue = 210 }, true);
        second.Guidelines.Should().Be(14, "第二次生成零新增，回新增数会让界面上的规范凭空变成 0（自查表 #10）");
        _repo.List(id).Should().HaveCount(14);

        // 播种失败不能把"令牌已生成成功"整体判死，但必须可见
        var broken = new GenerationService(_tokens, _projects, _catalog, _auditEngine)
        {
            Guidelines = new ThrowingGuidelineService(),
        };
        var id2 = _projects.Create(new ProjectInput { Code = "run2-" + Guid.NewGuid().ToString("N")[..8], Name = "播种失败项目" }).Id;
        var res = broken.Run(id2, new GenerationRequest { Hue = 210 }, false);
        res.Result.Total.Should().BeGreaterThan(0);
        res.Result.Notes.Should().Contain(n => n.Contains("UX 规范播种失败"));
    }

    /// <summary>故意在播种处抛异常的假服务（只有"失败可见"这条断言用得上）</summary>
    sealed class ThrowingGuidelineService : GuidelineService
    {
        public ThrowingGuidelineService() : base(new DesignProjectService(), new TokenRepository(), new GuidelineRepository()) { }

        public override Int32 SeedGuidelines(Int64 projectId) => throw new InvalidOperationException("播种失败探针");
    }

    [Fact]
    public void AC14_乐观并发_旧updatedAt写入被拒()
    {
        var id = NewProject();
        var saved = _svc.Save(id, "layout-grid", new GuidelinePatch { Title = "第一版" });

        Record.Exception(() => _svc.Save(id, "layout-grid", new GuidelinePatch { Title = "第二版", ExpectUpdatedAt = saved.UpdatedAt.AddMinutes(-5) }))
            .Should().BeOfType<DesignConflictException>();
        _repo.Find(id, "layout-grid")!.Title.Should().Be("第一版", "并发比对失败却写了行 = 后写的人覆盖前人");

        _svc.Save(id, "layout-grid", new GuidelinePatch { Title = "第二版", ExpectUpdatedAt = saved.UpdatedAt })
            .Title.Should().Be("第二版");
    }
}
