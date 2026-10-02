using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;
using Match = System.Text.RegularExpressions.Match;   // 与 Moq.Match 同名，显式消歧

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M2 FR10 / AC4：**模特变量契约**——模特页源码里引用的每一个 `var(--ds-*)`，
/// 必须在内置预设（M1 起 8 个、M3 起 13 个）× 明/暗的导出 CSS 定义集中**真的存在**。
///
/// 为什么必须跨层核对：模特是"样板间"，它读的变量名一旦写错（多写一个 `-strong`、少写一段路径），
/// 前端 build / vue-tsc / 组件单测全都绿，运行时只是那一处**静默失效**（声明在 computed-value
/// 阶段作废，表现是"这块突然透明/没样式"）。只有把"源码引用的名字"与"后端真正产出的名字"
/// 放在一起比对才拦得住。射程 = `showroom/mannequins/**` 的 `.vue` 与 `.css`（偏差 D4，见 03-plan）。
///
/// 隔离库只建不删（铁律 10）。夹具与 <see cref="PreviewCssTests"/> 同形。
/// </summary>
[Collection("XCode")]
public class MannequinVariableContractTests : IDisposable
{
    readonly String _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly AuditEngine _auditEngine;
    readonly ExportService _export;
    readonly PreviewCssService _preview;

    public MannequinVariableContractTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfMannequin_{Guid.NewGuid():N}");
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

        _auditEngine = new AuditEngine(_tokens, _projects, _audits);
        _export = new ExportService(_tokens, _projects, _catalog);
        _preview = new PreviewCssService(_export);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    /// <summary>源码里引用的变量：`var(--ds-x)` → name（`\s*` 容忍 `var( --ds-x )` 写法）</summary>
    static readonly Regex UsedVar = new(@"var\(\s*(--ds-[\w-]+)", RegexOptions.Compiled);
    /// <summary>导出 CSS 里定义的变量：`--ds-x:` → name（含 `:root{}` 与 reduced-motion 块内的声明）</summary>
    static readonly Regex DefinedVar = new(@"(--ds-[\w-]+)\s*:", RegexOptions.Compiled);

    static String FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("找不到含 ForgeSelf.slnx 的仓库根");
    }

    /// <summary>
    /// 从模特源码收集"引用的变量 → 首个出处 file:line"。
    /// 抽成静态方法是为了让反向探针能直接调它（守卫不空转）。
    /// </summary>
    internal static Dictionary<String, String> CollectUsed(IEnumerable<String> files)
    {
        var used = new Dictionary<String, String>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                foreach (Match m in UsedVar.Matches(lines[i]))
                    used.TryAdd(m.Groups[1].Value, $"{file}:{i + 1}");
        }
        return used;
    }

    /// <summary>差集：引用了但定义集里没有的变量（= 悬空引用），带首个出处。</summary>
    internal static List<String> MissingVars(Dictionary<String, String> used, HashSet<String> defined) =>
        used.Where(kv => !defined.Contains(kv.Key)).Select(kv => $"{kv.Key}（首见 {kv.Value}）").OrderBy(x => x).ToList();

    [Fact]
    public void AC4_模特引用的每个变量_八预设明暗导出都必须有定义()
    {
        var mannequinDir = Path.Combine(FindRepoRoot(), "Plugins", "DesignSystem", "web", "src", "showroom", "mannequins");
        Directory.Exists(mannequinDir).Should().BeTrue($"模特目录不存在：{mannequinDir}");

        var files = Directory.EnumerateFiles(mannequinDir, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".vue", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
            .ToList();
        files.Count.Should().BeGreaterThanOrEqualTo(7, "切片 A 应有 6 个模特页 + mannequin.css；扫不到 = 契约失效");

        var used = CollectUsed(files);
        used.Should().NotBeEmpty("一个变量都没扫到 = 守卫在空转");

        StylePresets.All.Count.Should().Be(13, "M3 §P 把目录扩到 13 个：模特引用的变量必须在全部 13 件衣服里都存在");
        var defined = new HashSet<String>();
        foreach (var preset in StylePresets.All)
            foreach (var theme in new[] { "light", "dark" })
                foreach (Match m in DefinedVar.Matches(_preview.Preview(preset.Request, theme).Css))
                    defined.Add(m.Groups[1].Value);

        var missing = MissingVars(used, defined);
        missing.Should().BeEmpty(
            "模特引用了后端导出里不存在的变量（运行时该声明会静默失效）：\n" + String.Join("\n", missing));
    }

    /// <summary>反向探针：造一个不存在的变量，守卫必须判缺（否则"全绿"只是没在比对）。</summary>
    [Fact]
    public void 反向探针_悬空引用必须被判缺()
    {
        var probe = new Dictionary<String, String> { ["--ds-semantic-not-a-real-role"] = "probe.vue:1" };
        MissingVars(probe, new HashSet<String>()).Should().NotBeEmpty();
        MissingVars(probe, new HashSet<String> { "--ds-semantic-not-a-real-role" }).Should().BeEmpty();
    }
}