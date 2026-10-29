using System.Collections.Concurrent;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Sems;
using ForgeSelf.Api.Plugins.Sems.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.Sems;

/// <summary>
/// sems 规格（B1 最小切片）测试：版本只追加不覆盖、并发不丢版本、非法 specId 拒绝、
/// 项目不存在、孤儿规格不列出、外部标识关联，以及 sems_*_spec 工具往返。
/// 数据目录落在仓库 .temp/tmp 下（只创建不删除），不依赖本机 %TEMP% 是否可写。
/// </summary>
public class SpecServiceTests
{
    private readonly string _dataDir;
    private readonly Mock<IProjectRegistry> _registry = new();
    private readonly SpecService _svc;
    private string _root = @"D:\proj\demo";

    public SpecServiceTests()
    {
        _dataDir = Path.Combine(RepoTemp(), $"SemsSpec_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDir);

        var dls = new Mock<IDataLocationService>();
        dls.Setup(d => d.GetPluginDataDirectory(It.IsAny<string>()))
           .Returns<string>(id => Path.Combine(_dataDir, "plugins", id));

        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<IDataLocationService>()).Returns(dls.Object);
        ctx.Setup(c => c.Get<PluginMetadata>()).Returns(new PluginMetadata { Id = "sems" });
        ctx.Setup(c => c.Get<IProjectRegistry>()).Returns(_registry.Object);
        _registry.Setup(r => r.Get(1)).Returns(() => new ProjectInfo { Id = 1, Root = _root });

        _svc = new SpecService(ctx.Object);
    }

    private static string RepoTemp()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
                return Path.Combine(dir.FullName, ".temp", "tmp");
            dir = dir.Parent;
        }

        throw new FileNotFoundException("找不到仓库根（ForgeSelf.slnx）");
    }

    [Fact]
    public void Create_Then_AddVersion_Yields_V1_V2_And_Keeps_V1()
    {
        var c = _svc.Create(1, "登录规格", "v1 正文", "alice", null);
        Assert.True(c.Success);
        Assert.Equal(1, c.Data!.Version);

        var a = _svc.AddVersion(1, c.Data.SpecId, "v2 正文", "bob", "补充边界");
        Assert.True(a.Success);
        Assert.Equal(2, a.Data!.Version);
        Assert.Equal("登录规格", a.Data.Title);

        Assert.Equal("v1 正文", _svc.Get(1, c.Data.SpecId, 1).Data!.Content);
        Assert.Equal("v2 正文", _svc.Get(1, c.Data.SpecId, null).Data!.Content);
    }

    [Fact]
    public void Concurrent_AddVersion_Never_Loses_Or_Duplicates_A_Version()
    {
        var id = _svc.Create(1, "并发", "v1", null, null).Data!.SpecId;
        var versions = new ConcurrentBag<int>();
        Parallel.For(0, 20, i => versions.Add(_svc.AddVersion(1, id, $"c{i}", null, null).Data!.Version));
        Assert.Equal(Enumerable.Range(2, 20), versions.OrderBy(v => v));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("")]
    [InlineData("ZZZ")]
    public void Invalid_SpecId_Is_Rejected(string bad)
    {
        Assert.Equal(SemsOpStatus.Invalid, _svc.Get(1, bad, null).Status);
        Assert.Equal(SemsOpStatus.Invalid, _svc.AddVersion(1, bad, "x", null, null).Status);
    }

    [Fact]
    public void Unknown_Project_Returns_NotFound()
    {
        Assert.Equal(SemsOpStatus.NotFound, _svc.Create(99, "t", "c", null, null).Status);
    }

    [Fact]
    public void Blank_Title_Or_Content_Returns_Invalid()
    {
        Assert.Equal(SemsOpStatus.Invalid, _svc.Create(1, " ", "c", null, null).Status);
        Assert.Equal(SemsOpStatus.Invalid, _svc.Create(1, "t", "", null, null).Status);
    }

    [Fact]
    public void Orphan_Spec_With_Changed_Root_Is_Not_Listed()
    {
        _svc.Create(1, "旧项目规格", "x", null, null);
        Assert.Single(_svc.List(1).Data!);

        _root = @"D:\proj\other";   // 同一 id 被别的目录复用
        Assert.Empty(_svc.List(1).Data!);
    }

    [Fact]
    public void Link_Is_Appended_And_Bad_Input_Rejected()
    {
        var id = _svc.Create(1, "t", "c", null, null).Data!.SpecId;
        Assert.True(_svc.Link(1, id, 1, "commit", "abc123").Success);
        Assert.Equal(SemsOpStatus.Invalid, _svc.Link(1, id, 1, "", "abc").Status);
        Assert.Equal(SemsOpStatus.NotFound, _svc.Link(1, id, 7, "commit", "abc").Status);
        Assert.Single(File.ReadAllLines(Path.Combine(_dataDir, "plugins", "sems", "specs", "1", id, "links.jsonl")));
    }

    [Fact]
    public async Task Tool_Roundtrip_Create_Then_List_And_Missing_Args_Fail_Gracefully()
    {
        var sc = new ServiceCollection();
        sc.AddSingleton<ISpecService>(_svc);
        var sp = sc.BuildServiceProvider();

        var create = new SemsCreateSpecToolFunction("sems", sp);
        var list = new SemsListSpecsToolFunction("sems", sp);

        using var r1 = JsonDocument.Parse(await create.ExecuteAsync("""{"projectId":1,"title":"T","content":"C"}"""));
        Assert.True(r1.RootElement.GetProperty("success").GetBoolean());

        using var r2 = JsonDocument.Parse(await list.ExecuteAsync("""{"projectId":1}"""));
        Assert.Equal(1, r2.RootElement.GetProperty("data").GetProperty("total").GetInt32());

        using var r3 = JsonDocument.Parse(await create.ExecuteAsync("{}"));
        Assert.False(r3.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void New_Tools_Have_Unique_Ids_And_Valid_Schemas()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var tools = new List<IToolFunctionExtension>
        {
            new SemsCreateSpecToolFunction("sems", sp),
            new SemsAddSpecVersionToolFunction("sems", sp),
            new SemsGetSpecToolFunction("sems", sp),
            new SemsListSpecsToolFunction("sems", sp),
            new SemsLinkSpecToolFunction("sems", sp)
        };

        Assert.Equal(5, tools.Select(t => t.Id).Distinct().Count());
        Assert.All(tools, t => Assert.StartsWith("sems_", t.Name));
        Assert.All(tools, t =>
        {
            using var doc = JsonDocument.Parse(t.ParametersJsonSchema);
            Assert.Equal("object", doc.RootElement.GetProperty("type").GetString());
        });
    }

    [Fact]
    public void Links_Are_Listed_For_Spec_And_Found_By_Ref_Both_Directions()
    {
        var a = _svc.Create(1, "A", "a", null, null).Data!.SpecId;
        var b = _svc.Create(1, "B", "b", null, null).Data!.SpecId;
        Assert.True(_svc.Link(1, a, 1, "commit", "abc123").Success);
        Assert.True(_svc.Link(1, a, 1, "pr", "42").Success);
        Assert.True(_svc.Link(1, b, 1, "commit", "abc123").Success);

        var forA = _svc.ListLinks(1, a).Data!;
        Assert.Equal(2, forA.Count);
        Assert.Contains(forA, l => l.Kind == "commit" && l.Ref == "abc123");
        Assert.Contains(forA, l => l.Kind == "pr" && l.Ref == "42");

        // 多对多：同一变更关联两份规格，反查两份都在，且都带版本号
        var hits = _svc.FindByRef(1, "commit", "abc123").Data!;
        Assert.Equal(2, hits.Count);
        Assert.Equal(new[] { a, b }.OrderBy(x => x), hits.Select(h => h.SpecId).OrderBy(x => x));
        Assert.All(hits, h => Assert.Equal(1, h.Version));
        Assert.Empty(_svc.FindByRef(1, "commit", "nope").Data!);
    }

    [Fact]
    public void Link_Stays_Pinned_To_Its_Version_After_New_Version()
    {
        var id = _svc.Create(1, "t", "v1", null, null).Data!.SpecId;
        _svc.Link(1, id, 1, "commit", "aaa");
        _svc.AddVersion(1, id, "v2", null, null);
        _svc.Link(1, id, 2, "commit", "bbb");

        Assert.Equal(1, _svc.FindByRef(1, "commit", "aaa").Data!.Single().Version);
        Assert.Equal(2, _svc.FindByRef(1, "commit", "bbb").Data!.Single().Version);
        Assert.Equal("v1", _svc.Get(1, id, 1).Data!.Content);
    }

    [Fact]
    public void ListLinks_And_FindByRef_Validate_Input()
    {
        Assert.Equal(SemsOpStatus.Invalid, _svc.ListLinks(1, "ZZZ").Status);
        Assert.Equal(SemsOpStatus.NotFound, _svc.ListLinks(1, "0123456789ab").Status);
        Assert.Equal(SemsOpStatus.Invalid, _svc.FindByRef(1, "", "x").Status);
        Assert.Equal(SemsOpStatus.NotFound, _svc.FindByRef(99, "commit", "x").Status);
    }

    [Fact]
    public void Corrupt_Link_Line_Is_Skipped()
    {
        var id = _svc.Create(1, "t", "c", null, null).Data!.SpecId;
        _svc.Link(1, id, 1, "commit", "good1");
        var path = Path.Combine(_dataDir, "plugins", "sems", "specs", "1", id, "links.jsonl");
        File.AppendAllText(path, "{not json\n");
        _svc.Link(1, id, 1, "commit", "good2");

        var links = _svc.ListLinks(1, id).Data!;
        Assert.Equal(new[] { "good1", "good2" }, links.Select(l => l.Ref));
    }
}
