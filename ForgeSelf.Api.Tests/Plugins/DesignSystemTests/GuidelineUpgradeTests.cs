using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M3 AC12（升级面）：新表**真的建得出来、建了不脏数据、索引真的落到物理库上**。
///
/// 三条各自对应一个会在生产上炸的形态：
/// ① 漏登记 <c>EntityTypes</c> / Model 与库里不一致 → 启动后第一个规范查询 <c>no such table</c>；
/// ② 建表过程动了既有表的数据 → 存量项目被升级污染（AC12「旧行不变」）；
/// ③ <c>(ProjectId,Code)</c> 唯一索引没落到物理库 → 同一项目能写进两条同 code 规范，
///    上层所有按 code 读写的逻辑（保存/归档/生成判重）全部失去依据。
///
/// **本类证明不到的一条，写清楚**（05-evidence Known Limitations 同步登记）：
/// 「进程冷启动时库里只有 12 张表」这个形态无法在共享测试进程里稳定复现——XCode 的**按需**建表只在实体 Meta
/// 首次初始化时决策，而同进程里前面的 DesignSystem 测试类已经把 13 个实体的 Meta 预热到别的库文件上
/// （实测：换新库文件、连 <c>DAL.Tables</c> 都置空后写入仍直接 <c>no such table: DesignProject</c>，跑序变了就红）。
/// 该形态在"本类是进程里第一个 XCode 测试类"的条件下已单独 Verified 一次：EnsureCreated 补建出新表、
/// 旧令牌行逐行不变、规范为空；常态跑序下由「重复初始化不动旧行」+「唯一索引真实落到库里」两条承担同等风险。
/// 表是否真的建到物理库，不靠表清单文字比对，靠往这张表写数据（第三条用例的 Generate 落 14 条）。
///
/// 隔离：本类专属随机临时库目录，只创建不删除（铁律 10）。
/// </summary>
[Collection("XCode")]
public class GuidelineUpgradeTests : IDisposable
{
    readonly String _dbFile;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly GuidelineRepository _guidelines = new();

    public GuidelineUpgradeTests()
    {
        var dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDsUpgrade_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dbDir);
        _dbFile = Path.Combine(dbDir, "DesignSystem.db");
        DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={_dbFile}", null, "SQLite");

        // 缓存关掉：本类各自一份临时库，Meta.Cache 是执行上下文级的，留着会把上一个类的行读成幽灵
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

        // 生产升级路径就是这一句（插件 OnLoad 调用）
        DesignSystemTables.EnsureCreated().Should().BeTrue();
    }

    public void Dispose() => GC.SuppressFinalize(this);

    /// <summary>DAL 侧的表清单（EnsureCreated 之后）</summary>
    static ICollection<String> TableNames() => DAL.Create(DesignSystemTables.ConnName).TableNames ?? [];

    [Fact]
    public void AC12_13张表全部由建表路径产出到物理库()
    {
        var names = TableNames();
        var missing = DesignSystemTables.EntityTypes
            .Select(t => t.Name)
            .Where(n => !names.Contains(n))
            .ToList();

        missing.Should().BeEmpty("EntityTypes 登记了但库里没有这张表 = 建表路径没覆盖到它");
        DesignSystemTables.EntityTypes.Should().HaveCount(13);
    }

    [Fact]
    public void AC12_唯一索引真实落到库里()
    {
        TableNames().Should().Contain("DesignGuideline", "新表没建出来，下面两条断言都是空的");

        var projectId = _projects.Create(new ProjectInput { Code = "ix-" + Guid.NewGuid().ToString("N")[..8], Name = "索引项目" }).Id;
        var code = "layout-" + Guid.NewGuid().ToString("N")[..6];
        SaveRow(projectId, code);

        // 同形状的第三行换个 code 必须写得进 —— 否则下面那条"被索引挡住"其实是别的原因（假绿）
        var other = Record.Exception(() => SaveRow(projectId, code + "-b"));
        other.Should().BeNull($"换 code 都写不进去就不是唯一索引问题：{other?.Message}");

        // 名字对不算证据，行为必须真被索引挡住：同 (ProjectId,Code) 第二条写不进去
        var again = Record.Exception(() => SaveRow(projectId, code));
        again.Should().NotBeNull("唯一索引没生效 = 按 code 读写规范的所有上层逻辑失去唯一性依据");
    }

    void SaveRow(Int64 projectId, String code) =>
        new DesignGuideline { ProjectId = projectId, Code = code, Title = "索引探针", Category = "layout", SortOrder = 0 }.Save();

    [Fact]
    public void AC12_重复初始化不动旧行也不回填规范()
    {
        var project = _projects.Create(new ProjectInput { Code = "up-" + Guid.NewGuid().ToString("N")[..8], Name = "旧库项目" });
        DesignGenerator.ApplyToProject(_tokens, _projects, project.Id,
            new GenerationRequest { Hue = 210, Themes = ["light", "dark"] }, false);

        var tokensBefore = DesignToken.FindAll(DesignToken._.ProjectId == project.Id)
            .OrderBy(t => t.Id).Select(Snapshot).ToList();
        tokensBefore.Count.Should().BeGreaterThan(120, "库里没写进数据，升级判据就是空的");

        // 再走一次启动初始化（= 用户升级后第二次、第 N 次启动）：必须逐行不动
        DesignSystemTables.EnsureCreated().Should().BeTrue();

        var tokensAfter = DesignToken.FindAll(DesignToken._.ProjectId == project.Id)
            .OrderBy(t => t.Id).Select(Snapshot).ToList();
        tokensAfter.Should().Equal(tokensBefore, "建表过程改动了既有表的数据行 = 升级会污染存量项目");

        // 存量项目不回填（D4）：初始化之后读规范仍是空的，只有显式生成才写
        _guidelines.List(project.Id).Should().BeEmpty("启动初始化写入了规范 = 违背「只补空、由用户显式生成」");

        // 显式生成必须能落 14 条（表建出来了但写不进去 = 列/索引与 Model 不一致）
        var r = new GuidelineService(_projects, _tokens, _guidelines).Generate(project.Id);
        r.Created.Should().HaveCount(GuidelineGenerator.Codes.Length);
        _guidelines.List(project.Id).Should().HaveCount(GuidelineGenerator.Codes.Length);
    }

    static String Snapshot(DesignToken t) => $"{t.Id}|{t.Path}|{t.Tier}|{t.Value}|{t.AliasPath}|{t.UpdatedAt:O}";
}
