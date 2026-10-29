using Xunit;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// PC-014 G2 类级守卫（2026-10-29，PILOT-051）：XCode 测试连接串唯一入口铁律。
///
/// 不变量：测试进程内 XCode 连接串只允许在 <see cref="XCodeTestFixture"/> 首绑处注册一次；
/// 任何测试类 ctor 手搓 <c>DAL.AddConnStr</c> 换库都会触发 <c>DAL.Reset()</c> 换 Database
/// （Migration 回 On → CheckTables 重跑 → SQLite「备份-重建-回填」与并发写竞态 → 吞行/吞表）
/// 并残留旧线程会话——全量联跑时把其他类的写入引去别处（实证：2026-10-29 中档全量 74 红归因）。
///
/// 本守卫机械封堵：除夹具本体与豁免清单（存量同型残留，专项批待迁移，expires 2026-12-31）外，
/// 任何测试源码出现 <c>AddConnStr</c> 即红灯。
/// </summary>
public class XCodeTestFixtureIronRuleTests
{
    /// <summary>存量豁免：非 AIAgent 连接名的既有手搓点（同型缺陷、受害连接不同），专项批迁移后逐条移除。</summary>
    private static readonly HashSet<string> Allowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "Integration\\ChatControllerIntegrationTests.cs",
        "Integration\\ChatControllerInvariantTests.cs",
        "Integration\\ChatProjectionRaceTests.cs",
        "Plugins\\AgentHub\\AgentHubDelegationRuntimeTests.cs",
        "Plugins\\CostScopeTests\\BudgetServiceTests.cs",
        "Plugins\\CostScopeTests\\CostAggregationServiceTests.cs",
        "Plugins\\CostScopeTests\\CostControllerTests.cs",
        "Plugins\\CostScopeTests\\PriceCatalogServiceTests.cs",
        "Plugins\\CostScopeTests\\TelemetryProjectionServiceTests.cs",
        "Plugins\\DesignSystemTests\\BuiltinIconTests.cs",
        "Plugins\\DesignSystemTests\\DesignAgentToolTests.cs",
        "Plugins\\DesignSystemTests\\DesignBriefTests.cs",
        "Plugins\\DesignSystemTests\\DesignReviewerTests.cs",
        "Plugins\\DesignSystemTests\\DesignSystemStoreTests.cs",
        "Plugins\\DesignSystemTests\\ExportProjectionTests.cs",
        "Plugins\\DesignSystemTests\\GenerateShapeTests.cs",
        "Plugins\\DesignSystemTests\\GenerationAuditTests.cs",
        "Plugins\\DesignSystemTests\\GuidelineExportTests.cs",
        "Plugins\\DesignSystemTests\\GuidelineReleaseTests.cs",
        "Plugins\\DesignSystemTests\\GuidelineRestTests.cs",
        "Plugins\\DesignSystemTests\\GuidelineServiceTests.cs",
        "Plugins\\DesignSystemTests\\GuidelineToolTests.cs",
        "Plugins\\DesignSystemTests\\GuidelineUpgradeTests.cs",
        "Plugins\\DesignSystemTests\\ImportTests.cs",
        "Plugins\\DesignSystemTests\\MannequinVariableContractTests.cs",
        "Plugins\\DesignSystemTests\\PreviewCssTests.cs",
        "Plugins\\DesignSystemTests\\QuickCreateServiceTests.cs",
        "Plugins\\DesignSystemTests\\ReleaseSnapshotTests.cs",
        "Plugins\\DesignSystemTests\\StyleAxisAuditTests.cs",
        "Plugins\\Sems\\ProjectServiceTests.cs",
        "Plugins\\Sems\\RunnerServiceTests.cs",
        "Plugins\\Sems\\SemsControllerTests.cs",
        "Plugins\\Sems\\SemsToolExtensionTests.cs",
        "Services\\HostProjectRegistryTests.cs",
        "Services\\PersistentInboxTests.cs",
        "Services\\PersistentSessionStoreAdversarialTests.cs",
        "Services\\PersistentSessionStoreTests.cs",
        "Services\\SessionProjectionServiceTests.cs",
    };

    [Fact]
    public void TestSources_MustNotRebindConnStr_OutsideFixtureOrAllowlist()
    {
        var testsRoot = AppContext.BaseDirectory;
        // 从 bin/Debug/netX/ 向上找到仓库测试项目根（XCodeTestFixture.cs 所在目录）
        var dir = new DirectoryInfo(testsRoot);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "XCodeTestFixture.cs")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(dir!.FullName, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(dir.FullName, file);
            if (rel.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || rel.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || rel.Contains($"{Path.DirectorySeparatorChar}.temp{Path.DirectorySeparatorChar}"))
                continue;
            if (string.Equals(rel, "XCodeTestFixture.cs", StringComparison.OrdinalIgnoreCase))
                continue; // 唯一合法入口
            if (string.Equals(rel, "XCodeTestFixtureIronRuleTests.cs", StringComparison.OrdinalIgnoreCase))
                continue; // 本守卫自身（注释与断言文本含模式字面量）
            if (Allowlist.Contains(rel))
                continue; // 存量豁免（专项批迁移）
            var text = File.ReadAllText(file);
            if (text.Contains("DAL.AddConnStr(") || text.Contains("DAL.Create(connName).Db") && text.Contains("ConnStr ="))
                offenders.Add(rel);
        }

        Assert.True(offenders.Count == 0,
            "PC-014 违例：以下测试文件手搓连接串/换库（唯一入口 = XCodeTestFixture 首绑）：" +
            string.Join("; ", offenders));
    }
}
