using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Architecture;

/// <summary>
/// 架构守卫（PILOT-079 验收返工 T13 · PC-005 二次复发治理）：插件侧<b>禁止在 ctx 本体上裸 CreateScope()</b>。
///
/// 缺陷模式（:51888 实例 2026-11-04 20:54:53 实抓）：插件工具/服务构造函数存下 Apply 传入的
/// <c>IContext</c>（自有服务表，无 IServiceScopeFactory），请求期直接 <c>_serviceProvider.CreateScope()</c>
/// → 抛「No service for type 'IServiceScopeFactory' has been registered.」（plan_workflow 实证，
/// 与 2026-10-17 list_todos 同族——2026-10-17 治理以三次实抓名单逐案修，未穷举全仓，本批 20 处漏网）。
///
/// 统一入口：<c>ForgeSelf.Abstractions.PluginContextDiExtensions.CreateHostScope/RequireHostProvider</c>
/// （请求期从 Context 取宿主 seed 的根 provider；直传真容器原样可用；未就绪抛指名错误）。
/// 本守卫把「接收者为 ctx 形态字段（_serviceProvider/serviceProvider/provider?）的裸 CreateScope」变成 CI 判据；
/// 真容器接收者（pluginProvider/_rootProvider/app.Services 等）不在禁令内。宿主 ForgeSelf.Api 的
/// ToolRegistry 由宿主 DI 构造注入真容器，不在扫描目录。
/// 照既有守卫规矩：先剥注释再匹配；扫描面与已治理锚点双向阳性对照（防路径漂移假绿）。
/// </summary>
public class PluginRawCreateScopeGuardTests
{
    /// <summary>裸 CreateScope 且接收者为 ctx 形态字段的违规模式（全仓实证：这些名字在插件侧恒为 ctx 本体）。</summary>
    private static readonly Regex RawCreateScopePattern = new(
        @"(_serviceProvider|serviceProvider|provider\?)\s*\.\s*CreateScope\s*\(",
        RegexOptions.Compiled);

    /// <summary>已治理锚点：守卫必须能在扫描面里找到这些统一入口用点（缺失=扫描路径漂移，门禁失效）。</summary>
    private static readonly (string File, string Token)[] TreatedAnchors =
    {
        ("Plugins/AIAgent/AIAgentPlugin.cs", "_serviceProvider.CreateHostScope()"),
        ("ForgeSelf.Abstractions/ToolFunctionUsageReportingExtensions.cs", "serviceProvider.CreateHostScope()"),
        ("Plugins/Sems/ToolExtensions.cs", "_services.CreateHostScope()"),
    };

    private static string RepoRootOf()
    {
        // T13 守卫加固：锚 ForgeSelf.slnx 逐级上溯（-o 自定义输出目录如 .temp/tmp/d3bin 时固定 4 级会落到 repo 外）
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir, "ForgeSelf.slnx"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    [Fact]
    public void 插件侧禁止在ctx本体上裸CreateScope_统一走CreateHostScope()
    {
        var repoRoot = RepoRootOf();
        var scanRoots = new[] { Path.Combine(repoRoot, "Plugins"), Path.Combine(repoRoot, "ForgeSelf.Abstractions") };
        var offenders = new List<string>();
        var scannedFiles = 0;
        foreach (var root in scanRoots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }
                scannedFiles++;
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;   // 注释行不算（文档本身会提到该名字）
                    if (RawCreateScopePattern.IsMatch(lines[i]))
                    {
                        offenders.Add($"{Path.GetRelativePath(repoRoot, file)}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }
        }
        scannedFiles.Should().BeGreaterThan(400, "扫描面漂移（路径/目录变更）会让本守卫静默失效，先修扫描根");
        offenders.Should().BeEmpty(
            "插件侧禁止在 ctx 本体（_serviceProvider/serviceProvider 字段）上裸 CreateScope——"
            + "统一走 ForgeSelf.Abstractions.PluginContextDiExtensions.CreateHostScope()；命中清单：\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void 已治理锚点_必须仍存在于扫描面_防守卫静默失效()
    {
        var repoRoot = RepoRootOf();
        foreach (var (file, token) in TreatedAnchors)
        {
            var full = Path.Combine(repoRoot, file);
            File.Exists(full).Should().BeTrue($"已治理锚点文件缺失：{file}（重构时须同步更新本守卫锚点）");
            File.ReadAllText(full).Should().Contain(token, $"锚点 {file} 须保留统一入口用点 {token}");
        }
    }
}
