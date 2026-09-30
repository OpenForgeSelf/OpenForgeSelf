using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ForgeSelf.Api.Data;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

using System.Text.Json;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// XCodeConfig 防漏回归：DbFiles 是唯一真源，AddXCode 与 InitializeXCodeDatabase 共用，
/// 新增库只需在 DbFiles 加一行，连接串注册与初始化不会漏。
/// </summary>
public class XCodeConfigTests
{
    [Fact]
    public void DbFiles_是唯一真源_所有条目均为Db文件()
    {
        XCodeConfig.DbFiles.Should().NotBeEmpty();
        foreach (var (name, file) in XCodeConfig.DbFiles)
        {
            name.Should().NotBeNullOrWhiteSpace();
            file.Should().EndWith(".db", "每个连接名必须映射到具体库文件");
        }
    }

    [Fact]
    public void AddXCode_每个DbFiles条目都注册到绝对数据根_无相对路径泄漏()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "ofs_xcodetest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new ConfigurationBuilder().Build();
            var services = new ServiceCollection();

            services.AddXCode(config, dataDir);

            // 注册的连接名集合必须与 DbFiles 完全一致（不漏不滥）
            DAL.ConnStrs.Keys.Should().Contain(XCodeConfig.DbFiles.Keys);
            foreach (var (name, file) in XCodeConfig.DbFiles)
            {
                var expected = "Data Source=" + Path.Combine(dataDir, file);
                // 派生串在绝对路径后还带 Busy Timeout（SQLite 撞写锁时排队而不是立即抛错），
                // 所以这里断言「以绝对数据根路径开头」而不是逐字相等 —— 路径这一层守卫不能松。
                DAL.ConnStrs[name].Should().StartWith(expected,
                    "连接串必须由数据根派生绝对路径，避免落到程序目录");
                DAL.ConnStrs[name].Should().Contain("Busy Timeout=",
                    "SQLite 并发写锁需靠 Busy Timeout 排队，缺了它界面并发操作会以 500 冒给用户");
            }
        }
        finally
        {
            // 数据安全铁律：测试自建数据目录只创建、不自动删除（删除由人手动/构建清理负责）
        }
    }

    [Fact]
    public void DbFiles_宿主库在根_插件库在Plugins插件Id子目录()
    {
        // 宿主库：数据根/ForgeSelf.db（用户诉求：宿主库必须在用户数据目录）
        XCodeConfig.DbFiles["ForgeSelf"].Should().Be("ForgeSelf.db");

        // 插件库：Plugins/{插件Id}/{连接名}.db（库文件名=连接名，与 XCode 一致：连接名即数据库名）
        foreach (var (connName, pluginId) in XCodeConfig.PluginDbs)
        {
            XCodeConfig.DbFiles[connName].Should().Be(
                Path.Combine(XCodeConfig.PluginDataRootName, pluginId, connName + ".db"),
                "插件库必须落在数据根/Plugins/{插件Id}/，且文件名即连接名");
        }
    }

    [Fact]
    public void AddXCode_相对路径连接串被忽略_仍派生到数据根()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "ofs_xcodetest_" + Guid.NewGuid().ToString("N"));
        try
        {
            // 历史遗留写法：appsettings 里写 Data\ForgeSelf.db，会被 NewLife 解析到程序目录
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ForgeSelf"] = "Data Source=Data\\ForgeSelf.db"
                })
                .Build();
            var services = new ServiceCollection();

            services.AddXCode(config, dataDir);

            // 同上一条用例：派生串带 Busy Timeout，故按前缀断言；"相对路径必须被忽略"这一层用否定断言钉住
            DAL.ConnStrs["ForgeSelf"].Should().StartWith("Data Source=" + Path.Combine(dataDir, "ForgeSelf.db"),
                "相对路径连接串必须被忽略，否则数据库会落到程序目录");
            DAL.ConnStrs["ForgeSelf"].Should().NotContain("Data\\ForgeSelf.db");
        }
        finally
        {
            // 数据安全铁律：测试自建数据目录只创建、不自动删除（删除由人手动/构建清理负责）
        }
    }

    [Fact]
    public void AddXCode_自定义绝对路径连接串被采纳()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "ofs_xcodetest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var custom = Path.Combine(dataDir, "custom", "my.db");
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ForgeSelf"] = "Data Source=" + custom
                })
                .Build();
            var services = new ServiceCollection();

            services.AddXCode(config, dataDir);

            DAL.ConnStrs["ForgeSelf"].Should().Be("Data Source=" + custom);
        }
        finally
        {
            // 数据安全铁律：测试自建数据目录只创建、不自动删除（删除由人手动/构建清理负责）
        }
    }

    /// <summary>
    /// 插件 Id 命名规范回归：kebab-case（全小写 + 短横线），无历史 <c>.plugin</c> 后缀。
    /// 插件 Id 同时是数据目录名与前端路由名，点号在两处都易混淆、全小写连排可读性差。
    /// 校验对象是仓库里真实的 plugin.json，防止有人手改清单又退回旧风格。
    /// </summary>
    [Theory]
    [InlineData("ai-agent")]
    [InlineData("dev-tools")]
    [InlineData("file-tools")]
    [InlineData("memory-system")]
    [InlineData("quick-links")]
    [InlineData("sample")]
    [InlineData("scheduler")]
    [InlineData("script-runner")]
    [InlineData("system-monitor")]
    [InlineData("text-tools")]
    [InlineData("todo-tracker")]
    [InlineData("workflow-engine")]
    public void 插件清单Id_符合kebabCase规范(string expectedId)
    {
        AllPluginIds().Should().Contain(expectedId,
            "仓库中应存在 Id 为「{0}」的插件清单（改名需同步本用例的期望值）", expectedId);
    }

    [Fact]
    public void 全部插件清单Id_均符合规范且唯一()
    {
        var ids = AllPluginIds();

        ids.Should().NotBeEmpty("应能扫描到仓库内的插件清单");
        ids.Should().OnlyHaveUniqueItems("插件 Id 必须全局唯一");
        foreach (var id in ids)
        {
            id.Should().MatchRegex("^[a-z0-9]+(-[a-z0-9]+)*$",
                "插件 Id 必须 kebab-case（{0} 不符合）", id);
        }
    }

    [Fact]
    public void 有独立库的插件_其Id已在PluginDbs登记()
    {
        // PluginDbs 的 value 是插件 Id，直接决定库落在哪个目录；
        // 若与 plugin.json 的 Id 不一致，插件库会落到与插件数据目录不同的位置（两份目录、数据割裂）。
        var knownIds = AllPluginIds().ToHashSet();

        foreach (var (connName, pluginId) in XCodeConfig.PluginDbs)
        {
            knownIds.Should().Contain(pluginId,
                "PluginDbs 里 {0} 的插件 Id「{1}」必须在某个 plugin.json 中存在", connName, pluginId);
        }
    }

    private static string ReadManifestId(string manifestPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        // plugin.json 由后端 camelCase 序列化、手工维护为 PascalCase，两种写法都兼容
        if (doc.RootElement.TryGetProperty("Id", out var pascal)) return pascal.GetString()!;
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    /// <summary>仓库内全部插件清单的 Id（按目录名排序，保证断言输出稳定）。
    /// 排除下划线开头的工具目录（如 _backups 发布备份、_published 暂存区）：
    /// 它们是发布流程生成的版本副本而非插件源码，纳入扫描会造成 Id 假性重复。</summary>
    private static List<string> AllPluginIds() =>
        Directory.GetFiles(PluginsRootOfRepository(), "plugin.json", SearchOption.AllDirectories)
            .Where(p => p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .All(s => !s.StartsWith('_')))
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(ReadManifestId)
            .ToList();

    /// <summary>定位仓库根 Plugins 目录（测试运行在 bin 下，需回溯）。
    /// 同时比对仓库根 ForgeSelf.Api 目录，避免测试项目自身的 Tests\Plugins 测试辅助目录误命中。</summary>
    private static string PluginsRootOfRepository()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Plugins");
            if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(dir.FullName, "ForgeSelf.Api")))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("未能定位仓库根 Plugins 目录");
    }

    [Fact]
    public void AddXCode_插件库父目录已创建()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "ofs_xcodetest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new ConfigurationBuilder().Build();
            var services = new ServiceCollection();

            services.AddXCode(config, dataDir);

            // SQLite 不会自动建父目录，缺失时报 Error 14「unable to open database file」
            foreach (var (connName, _) in XCodeConfig.PluginDbs)
            {
                Directory.Exists(Path.GetDirectoryName(Path.Combine(dataDir, XCodeConfig.DbFiles[connName]))!)
                    .Should().BeTrue("插件库所在目录必须在注册连接串时就创建好");
            }
        }
        finally
        {
            // 数据安全铁律：测试自建数据目录只创建、不自动删除（删除由人手动/构建清理负责）
        }
    }
}
