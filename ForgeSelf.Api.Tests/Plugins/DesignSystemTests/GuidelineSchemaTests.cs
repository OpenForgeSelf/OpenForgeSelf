using System.Reflection;
using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Controllers;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M3 AC12（静态面）：`DesignGuideline` 是本次唯一的结构变更，且这张表**真的挂在建表路径上**。
///
/// 这里只跑不碰库的断言；"旧库升级"那条要独立执行上下文（不能被前置的 InitConnection 抢跑建表），
/// 见 <see cref="GuidelineUpgradeTests"/>。
///
/// 每条断言对应的真实事故：
/// - 漏登记 <c>EntityTypes</c> → 生产库上没有这张表，测试库却有（建表铁律 12）；
/// - 手改 xcode 生成物 → 下次生成被覆写，索引/列定义与 Model.xml 分叉；
/// - 分类词表在 Biz 里另写一份 → 第二份真相（自查表 #22）；
/// - 提供 DELETE → 违反"归档即软删、永不物理删除"（铁律 10）；
/// - 类级鉴权特性丢失 → 无 token 也能读写规范（铁律 17）。
/// </summary>
public class GuidelineSchemaTests
{
    [Fact]
    public void AC12_新表已登记进建表路径()
    {
        DesignSystemTables.EntityTypes.Should().Contain(typeof(DesignGuideline),
            "没登记进 EntityTypes 就等于生产库里不会有这张表（建表路径的唯一真源是它）");
        DesignSystemTables.EntityTypes.Should().HaveCount(13, "M3 只允许新增这一张表");
    }

    [Fact]
    public void AC12_Model仅新增一张表_既有12表定义仍在()
    {
        var xml = File.ReadAllText(FindRepoFile("Plugins", "DesignSystem", "Data", "Model.xml"));
        var tables = Regex.Matches(xml, "<Table Name=\"([A-Za-z]+)\"")
            .Select(m => m.Groups[1].Value).ToList();

        tables.Should().Contain("DesignGuideline");
        tables.Should().HaveCount(13);
        // M1/M2 的 12 张表一张都不能少（少一张 = 结构变更面超出 AC12 允许的范围）
        String[] old12 = ["DesignProject", "DesignTheme", "DesignToken", "DesignShadowLayer", "DesignComponent",
            "DesignComponentVariant", "DesignIcon", "DesignAsset", "DesignScreen", "DesignFontFace", "DesignAudit", "DesignRelease"];
        foreach (var table in old12)
            tables.Should().Contain(table, "既有表定义被删/改名 = 违反「本次唯一结构变更是新增 DesignGuideline」");
    }

    [Fact]
    public void AC12_生成物未被手改且索引来自Model()
    {
        var src = File.ReadAllText(FindRepoFile("Plugins", "DesignSystem", "Data", "Entities", "DesignGuideline.cs"));
        src.Should().Contain("XCode", "生成物头部应带生成来源标记");
        src.Should().Contain("BindTable", "实体必须绑定到 DesignSystem 连接");

        // 索引逐条对 Model.xml：唯一索引 (ProjectId,Code) 掉了 = 同一项目能建两条同 code 规范；
        // 手写的 BindIndex 会在下次 xcode 生成时被覆写，所以这里必须证明它来自 Model 而不是凭空出现
        var xml = File.ReadAllText(FindRepoFile("Plugins", "DesignSystem", "Data", "Model.xml"));
        var block = Regex.Match(xml, "<Table Name=\"DesignGuideline\".*?</Table>", RegexOptions.Singleline);
        block.Success.Should().BeTrue("Model.xml 里没有 DesignGuideline 表定义 = 实体是手写的");
        var indexes = Regex.Matches(block.Value, "<Index Columns=\"([^\"]+)\"( Unique=\"True\")?");
        indexes.Should().HaveCount(3, "唯一索引 + 两条查询索引，少一条就是 Model 与生成物分叉");
        foreach (System.Text.RegularExpressions.Match m in indexes)
        {
            var cols = m.Groups[1].Value;
            var flag = m.Groups[2].Success ? "true" : "false";
            src.Should().Contain($"""{flag}, "{cols}")""", $"生成物索引 [{flag},\"{cols}\"] 与 Model.xml 不匹配（手改生成物会在下次生成时被覆写）");
        }

        // Biz 里写的词表校验必须读 GuidelineCategories，而不是在 Biz 里另列一份分类
        var biz = File.ReadAllText(FindRepoFile("Plugins", "DesignSystem", "Data", "Entities", "DesignGuideline.Biz.cs"));
        biz.Should().Contain("GuidelineCategories", "Biz 校验没走词表单点 = 第二份真相");
    }

    [Fact]
    public void AC12_控制器没有删除路由()
    {
        var methods = typeof(DesignSystemController).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var delete = methods.Where(m => m.GetCustomAttributes().Any(a => a is HttpDeleteAttribute)).ToList();

        // 全控制器（不只规范那五个）都必须没有 DELETE：铁律 10「不提供任何删除」
        delete.Should().BeEmpty("存在 DELETE 路由 = 提供物理删除能力，违反数据安全铁律 10");
    }

    [Fact]
    public void AC12_类级鉴权未削弱()
    {
        typeof(DesignSystemController).GetCustomAttribute<AuthorizeAttribute>()
            .Should().NotBeNull("管理类控制器必须类级 ApiKeyPolicy（铁律 17）");
    }

    static String FindRepoFile(params String[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) dir = dir.Parent;
        return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
    }
}
