using System.Reflection;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Sems;
using ForgeSelf.Api.Plugins.Sems.Controllers;
using ForgeSelf.Api.Plugins.Sems.Services;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.Sems;

/// <summary>
/// B1-AC2 测试：规格版本带结构化验收标准，逐条登记用例并查看覆盖；
/// 旧版本文件（无 Criteria 字段）仍可读；用例登记按版本固定；工具与控制器往返。
/// 「已关联」只表示有人登记了用例标识，不代表用例存在或通过。
/// </summary>
public class SpecCoverageTests
{
    private readonly string _dataDir;
    private readonly SpecService _svc;

    public SpecCoverageTests()
    {
        _dataDir = Path.Combine(RepoTemp(), $"SemsCov_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDir);

        var dls = new Mock<IDataLocationService>();
        dls.Setup(d => d.GetPluginDataDirectory(It.IsAny<string>()))
           .Returns<string>(id => Path.Combine(_dataDir, "plugins", id));

        var registry = new Mock<IProjectRegistry>();
        registry.Setup(r => r.Get(1)).Returns(() => new ProjectInfo { Id = 1, Root = @"D:\proj\demo" });

        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<IDataLocationService>()).Returns(dls.Object);
        ctx.Setup(c => c.Get<PluginMetadata>()).Returns(new PluginMetadata { Id = "sems" });
        ctx.Setup(c => c.Get<IProjectRegistry>()).Returns(registry.Object);

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

    private static List<SpecCriterion> Crit(params string[] ids) =>
        ids.Select(i => new SpecCriterion(i, $"标准 {i}")).ToList();

    private string NewSpec(params string[] ids) =>
        _svc.Create(1, "T", "C", null, null, Crit(ids)).Data!.SpecId;

    [Fact]
    public void Coverage_Shows_Each_Criterion_Linked_Or_Not()
    {
        var id = NewSpec("AC1", "AC2", "AC3");
        Assert.True(_svc.LinkCase(1, id, 1, "AC1", "A.cs::T1").Success);
        Assert.True(_svc.LinkCase(1, id, 1, "AC2", "A.cs::T2").Success);

        var cov = _svc.GetCoverage(1, id, null).Data!;
        Assert.Equal(3, cov.Total);
        Assert.Equal(2, cov.Linked);
        Assert.True(cov.Items.Single(i => i.Id == "AC1").Linked);
        Assert.True(cov.Items.Single(i => i.Id == "AC2").Linked);
        Assert.False(cov.Items.Single(i => i.Id == "AC3").Linked);
        Assert.Equal(new[] { "A.cs::T1" }, cov.Items.Single(i => i.Id == "AC1").Cases);
    }

    [Fact]
    public void Criteria_Survive_Disk_Roundtrip()
    {
        var id = NewSpec("AC1", "AC2");
        // Get 每次从文件读，能读回说明序列化正确
        var got = _svc.Get(1, id, 1).Data!;
        Assert.Equal(new[] { "AC1", "AC2" }, got.Criteria!.Select(c => c.Id));
        Assert.Equal("标准 AC1", got.Criteria![0].Text);
    }

    [Fact]
    public void Spec_Without_Criteria_Gives_Zero_Total_Not_Error()
    {
        var id = _svc.Create(1, "T", "C", null, null).Data!.SpecId;
        var cov = _svc.GetCoverage(1, id, null);
        Assert.True(cov.Success);
        Assert.Equal(0, cov.Data!.Total);
        Assert.Equal(0, cov.Data.Linked);
    }

    [Fact]
    public void Old_Version_File_Without_Criteria_Field_Is_Still_Readable()
    {
        var dir = Path.Combine(_dataDir, "plugins", "sems", "specs", "1", "0123456789ab");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "v1.json"),
            """{"SpecId":"0123456789ab","Version":1,"ProjectId":1,"ProjectRoot":"x","Title":"old","Content":"c","Author":null,"ChangeNote":null,"CreatedAt":"2026-01-01T00:00:00Z"}""");

        var g = _svc.Get(1, "0123456789ab", null);
        Assert.True(g.Success);
        Assert.Null(g.Data!.Criteria);
        Assert.Equal(0, _svc.GetCoverage(1, "0123456789ab", null).Data!.Total);
    }

    [Fact]
    public void LinkCase_Rejects_Unknown_Criterion_Blank_Input_And_Unknown_Version()
    {
        var id = NewSpec("AC1");
        Assert.Equal(SemsOpStatus.Invalid, _svc.LinkCase(1, id, 1, "AC9", "A.cs::T").Status);
        Assert.Equal(SemsOpStatus.Invalid, _svc.LinkCase(1, id, 1, "AC1", " ").Status);
        Assert.Equal(SemsOpStatus.Invalid, _svc.LinkCase(1, id, 1, "", "A.cs::T").Status);
        Assert.Equal(SemsOpStatus.NotFound, _svc.LinkCase(1, id, 7, "AC1", "A.cs::T").Status);
        Assert.Equal(SemsOpStatus.Invalid, _svc.LinkCase(1, "ZZZ", 1, "AC1", "A.cs::T").Status);
    }

    [Fact]
    public void LinkCase_On_Spec_Without_Criteria_Is_Rejected()
    {
        var id = _svc.Create(1, "T", "C", null, null).Data!.SpecId;
        Assert.Equal(SemsOpStatus.Invalid, _svc.LinkCase(1, id, 1, "AC1", "A.cs::T").Status);
    }

    [Fact]
    public void CriterionId_Is_Case_Insensitive_And_Stored_Canonical()
    {
        var id = NewSpec("AC1");
        var r = _svc.LinkCase(1, id, 1, "ac1", "A.cs::T");
        Assert.True(r.Success);
        Assert.Equal("AC1", r.Data!.CriterionId);
        Assert.Equal(1, _svc.GetCoverage(1, id, null).Data!.Linked);
    }

    [Fact]
    public void Duplicate_Case_Registration_Is_Deduplicated_On_Read()
    {
        var id = NewSpec("AC1");
        Assert.True(_svc.LinkCase(1, id, 1, "AC1", "A.cs::T").Success);
        Assert.True(_svc.LinkCase(1, id, 1, "AC1", "A.cs::T").Success);
        Assert.True(_svc.LinkCase(1, id, 1, "AC1", "A.cs::T2").Success);
        Assert.Equal(new[] { "A.cs::T", "A.cs::T2" }, _svc.GetCoverage(1, id, null).Data!.Items.Single().Cases);
    }

    [Fact]
    public void Bad_Criteria_Input_Is_Rejected()
    {
        Assert.Equal(SemsOpStatus.Invalid,
            _svc.Create(1, "T", "C", null, null, new[] { new SpecCriterion("AC1", "a"), new SpecCriterion("ac1", "b") }).Status);
        Assert.Equal(SemsOpStatus.Invalid,
            _svc.Create(1, "T", "C", null, null, new[] { new SpecCriterion("AC1", " ") }).Status);
        Assert.Equal(SemsOpStatus.Invalid,
            _svc.Create(1, "T", "C", null, null, new[] { new SpecCriterion("", "a") }).Status);
    }

    [Fact]
    public void New_Version_Inherits_Criteria_But_Cases_Stay_Pinned_To_Old_Version()
    {
        var id = NewSpec("AC1", "AC2");
        _svc.LinkCase(1, id, 1, "AC1", "A.cs::T1");

        var v2 = _svc.AddVersion(1, id, "v2", null, null).Data!;
        Assert.Equal(2, v2.Criteria!.Count);

        // 最新版（v2）未关联；v1 仍显示它生成时登记的用例
        Assert.Equal(0, _svc.GetCoverage(1, id, null).Data!.Linked);
        Assert.Equal(1, _svc.GetCoverage(1, id, 1).Data!.Linked);
    }

    [Fact]
    public void New_Version_Can_Replace_Criteria_And_Old_Ones_Become_Invalid_Targets()
    {
        var id = NewSpec("AC1", "AC2");
        _svc.AddVersion(1, id, "v2", null, null, Crit("AC1"));

        Assert.Equal(SemsOpStatus.Invalid, _svc.LinkCase(1, id, 2, "AC2", "A.cs::T").Status);
        Assert.True(_svc.LinkCase(1, id, 1, "AC2", "A.cs::T").Success);   // 旧版本仍可登记其自己的标准
    }

    [Fact]
    public void Corrupt_Case_Line_Is_Skipped()
    {
        var id = NewSpec("AC1");
        _svc.LinkCase(1, id, 1, "AC1", "good1");
        var path = Path.Combine(_dataDir, "plugins", "sems", "specs", "1", id, "cases.jsonl");
        File.AppendAllText(path, "{not json\n");
        _svc.LinkCase(1, id, 1, "AC1", "good2");

        Assert.Equal(new[] { "good1", "good2" }, _svc.GetCoverage(1, id, null).Data!.Items.Single().Cases);
    }

    [Fact]
    public void Cases_Do_Not_Pollute_External_Links()
    {
        var id = NewSpec("AC1");
        _svc.LinkCase(1, id, 1, "AC1", "A.cs::T");
        _svc.Link(1, id, 1, "commit", "abc");
        Assert.Single(_svc.ListLinks(1, id).Data!);
        Assert.Single(_svc.FindByRef(1, "commit", "abc").Data!);
    }

    [Fact]
    public async Task Tools_Roundtrip_Create_With_Criteria_Link_Case_Then_Coverage()
    {
        var sc = new ServiceCollection();
        sc.AddSingleton<ISpecService>(_svc);
        var sp = sc.BuildServiceProvider();
        var create = new SemsCreateSpecToolFunction("sems", sp);
        var link = new SemsLinkSpecCaseToolFunction("sems", sp);
        var cov = new SemsGetSpecCoverageToolFunction("sems", sp);

        using var r1 = JsonDocument.Parse(await create.ExecuteAsync(
            """{"projectId":1,"title":"T","content":"C","criteria":[{"id":"AC1","text":"a"},{"id":"AC2","text":"b"}]}"""));
        Assert.True(r1.RootElement.GetProperty("success").GetBoolean());
        var data1 = r1.RootElement.GetProperty("data");
        Assert.Equal(2, data1.GetProperty("criteria").GetArrayLength());
        var specId = data1.GetProperty("specId").GetString()!;

        using var r2 = JsonDocument.Parse(await link.ExecuteAsync(
            $$"""{"projectId":1,"specId":"{{specId}}","version":1,"criterionId":"AC1","case":"X.cs::Y"}"""));
        Assert.True(r2.RootElement.GetProperty("success").GetBoolean());

        using var r3 = JsonDocument.Parse(await cov.ExecuteAsync($$"""{"projectId":1,"specId":"{{specId}}"}"""));
        var d3 = r3.RootElement.GetProperty("data");
        Assert.Equal(2, d3.GetProperty("total").GetInt32());
        Assert.Equal(1, d3.GetProperty("linked").GetInt32());
        Assert.Equal("X.cs::Y", d3.GetProperty("items")[0].GetProperty("cases")[0].GetString());

        using var r4 = JsonDocument.Parse(await link.ExecuteAsync(
            $$"""{"projectId":1,"specId":"{{specId}}","version":1,"criterionId":"AC9","case":"X.cs::Y"}"""));
        Assert.False(r4.RootElement.GetProperty("success").GetBoolean());

        using var r5 = JsonDocument.Parse(await link.ExecuteAsync("{}"));
        Assert.False(r5.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void New_Case_Tools_Have_Valid_Schemas_And_Unique_Ids()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var tools = new List<IToolFunctionExtension>
        {
            new SemsLinkSpecCaseToolFunction("sems", sp),
            new SemsGetSpecCoverageToolFunction("sems", sp)
        };
        Assert.Equal(2, tools.Select(t => t.Id).Distinct().Count());
        Assert.All(tools, t =>
        {
            Assert.StartsWith("sems_", t.Name);
            using var doc = JsonDocument.Parse(t.ParametersJsonSchema);
            Assert.Equal("object", doc.RootElement.GetProperty("type").GetString());
        });
    }

    [Fact]
    public void Controller_Case_And_Coverage_Endpoints_Return_Expected_Envelopes()
    {
        var ctl = new SpecsController(_svc);
        var created = ctl.Create(1, new SpecsController.CreateSpecRequest("T", "C", null, null,
            new List<SpecsController.CriterionDto> { new("AC1", "a"), new("AC2", "b") }));
        Assert.IsType<OkObjectResult>(created.Result);
        var id = _svc.List(1).Data!.Single().SpecId;

        Assert.IsType<OkObjectResult>(ctl.LinkCase(1, id, new SpecsController.LinkCaseRequest(1, "AC1", "A.cs::T")).Result);

        var cov = Assert.IsType<OkObjectResult>(ctl.Coverage(1, id, null).Result);
        Assert.NotNull(cov.Value);

        var bad = Assert.IsAssignableFrom<ObjectResult>(
            ctl.LinkCase(1, id, new SpecsController.LinkCaseRequest(1, "AC9", "A.cs::T")).Result);
        Assert.Equal(400, bad.StatusCode);

        var missing = Assert.IsAssignableFrom<ObjectResult>(ctl.Coverage(1, "0123456789ab", null).Result);
        Assert.Equal(404, missing.StatusCode);

        var badProject = Assert.IsAssignableFrom<ObjectResult>(ctl.Coverage(99, id, null).Result);
        Assert.Equal(404, badProject.StatusCode);
    }

    [Fact]
    public void SpecsController_Is_Class_Authorized_And_Has_No_Anonymous_Endpoint()
    {
        var attr = typeof(SpecsController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();
        Assert.Equal("ApiKeyPolicy", attr.Policy);
        Assert.DoesNotContain(typeof(SpecsController).GetMethods(),
            m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any());
    }
}