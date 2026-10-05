using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Controllers;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Microsoft.AspNetCore.Mvc;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// Generate 响应形状回归（AC22）：控制器 Generate 改调 GenerationService 后，
/// 响应匿名对象的键序列必须与重构前基线一字不差 —— 前端与工具都按这份形状消费。
/// 基线（重构前 2026-10-01 实测取键）：seed, industry, hue, tokens, components, variants, fonts, screens,
/// assets, themes, notes, skippedProtected, conflicts, audit。
///
/// 隔离：每类一份随机临时目录库（只创建、永不删除 —— plugin-development 铁律 10）。
/// </summary>
[Collection("XCode")]
public class GenerateShapeTests : IDisposable
{
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    /// <summary>M3 基线增量：末尾追加 `guidelines`（AC14「生成后规范到底有没有」在响应里可见），其余键顺序一字未动</summary>
    static readonly String[] ShapeBaseline =
    [
        "seed", "industry", "hue", "tokens", "components", "variants",
        "fonts", "screens", "assets", "themes", "notes", "skippedProtected", "conflicts", "audit",
        "guidelines",
    ];

    readonly String _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly AuditEngine _auditEngine;
    readonly ExportService _export;
    readonly ReleaseService _releases;
    readonly GenerationService _generation;
    readonly GuidelineService _guidelines;

    public GenerateShapeTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfShape_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={Path.Combine(_dbDir, "DesignSystem.db")}", null, "SQLite");
        EntityFactory.InitConnection(DesignSystemTables.ConnName);

        // 缓存是进程级 AsyncLocal 一份，跨测试会互相看见幽灵行；这里禁用缓存，正确性判定全走直查
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
        _export = new ExportService(_tokens, _projects, _catalog);
        _releases = new ReleaseService(_tokens, _projects, _audits, _auditEngine, new DesignSystemPaths(_dbDir), _catalog);
        _generation = new GenerationService(_tokens, _projects, _catalog, _auditEngine);
        // 与插件 DI 一致：生成链路挂上规范服务，generate 响应的 guidelines 才是真数而不是 null
        _guidelines = new GuidelineService(_projects, _tokens, new GuidelineRepository());
        _generation.Guidelines = _guidelines;
    }

    public void Dispose()
    {
        // 铁律 10：绝不删除测试库目录，交给构建产物清理
        GC.SuppressFinalize(this);
    }

    DesignSystemController NewController() => new(_projects, _tokens, _catalog, _audits, _auditEngine, _export, _releases,
        _generation, new AgentAccess(new DesignSystemPaths(_dbDir)),
        new DesignReviewService(_export, _tokens, _projects),
        new QuickCreateService(_projects, _generation), null!, new PreviewCssService(_export), _guidelines);

    Int64 NewProject(String tag)
    {
        var p = _projects.Create(new ProjectInput { Code = $"g-{tag}-{Guid.NewGuid():N}"[..24], Name = $"形状 {tag}" });
        return p.Id;
    }

    [Fact]
    public void Generate_响应键集与基线一字不差()
    {
        var id = NewProject("shape");
        var res = NewController().Generate(id, new GenerationRequest(), false);

        var json = JsonSerializer.Serialize(((JsonResult)res).Value, JsonOpts);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.GetProperty("data");
        var keys = root.EnumerateObject().Select(p => p.Name).ToList();

        keys.Should().Equal(ShapeBaseline);
    }

    [Fact]
    public void Generate_核心字段有真实数量_非空声明()
    {
        var id = NewProject("count");
        var res = NewController().Generate(id, new GenerationRequest(), false);

        var json = JsonSerializer.Serialize(((JsonResult)res).Value, JsonOpts);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.GetProperty("data");

        root.GetProperty("tokens").GetInt32().Should().BeGreaterThan(100);
        root.GetProperty("components").GetInt32().Should().BeGreaterThanOrEqualTo(10);
        root.GetProperty("variants").GetInt32().Should().BeGreaterThan(0);
        root.GetProperty("fonts").GetInt32().Should().BeGreaterThan(0);
        root.GetProperty("themes").EnumerateArray().Should().NotBeEmpty();
        root.GetProperty("seed").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
