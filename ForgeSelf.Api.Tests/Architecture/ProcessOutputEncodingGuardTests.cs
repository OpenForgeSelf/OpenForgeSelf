using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Architecture;

/// <summary>
/// 架构守卫（PILOT-073 · PC-011）：<b>子进程输出禁止固定码页解码</b>。
///
/// 缺陷模式（run_terminal_command 中文 U+FFFD 实证，<c>docs/ai/pilot/2026-10-22-terminal-output-encoding/</c>）：
/// 任何"固定码页解码"必坏一侧——<c>StandardOutputEncoding = Encoding.UTF8</c> 毁掉子进程按系统
/// ANSI(GBK) 写出的中文；不设置（.NET 默认跟随宿主码页）或 <c>BeginOutputReadLine</c> 事件流
/// （同走默认解码）毁掉 dotnet/pnpm/node 的协议性 UTF-8。
///
/// 正确范式：读 <c>StandardOutput.BaseStream</c> 原始字节，交全仓唯一解码真源
/// <c>ForgeSelf.Core.ProcessOutputText</c>（整段）/ <c>ProcessOutputLineBuffer</c>（流式逐行）裁决。
/// 本守卫把该范式变成 CI 判据：生产源码命中禁用 token 且不在 allowlist 即红。
///
/// 照既有守卫规矩（plugin-development §七B / PluginToolConstructorDiGuardTests）：
/// 先剥注释再匹配（站点迁移注释里大量出现 token 名，不剥必假红）、阳性对照防正则失效、变异探针实证可红。
/// </summary>
public class ProcessOutputEncodingGuardTests
{
    /// <summary>
    /// 禁用 token → 违规语义。
    /// ① 固定码页赋值：psi 上显式设 Standard(Output|Error)Encoding；
    /// ② 事件流默认解码：Begin(Output|Error)ReadLine（OutputDataReceived 回调按宿主码页解码，PC-011 变体B）。
    /// </summary>
    private static readonly string[] ForbiddenTokens =
    {
        "StandardOutputEncoding =", "StandardErrorEncoding =",
        "BeginOutputReadLine(", "BeginErrorReadLine(",
    };

    /// <summary>
    /// allowlist（相对仓库根，含豁免理由——每条都必须带"为什么合法"，防止后来者无理由加白）：
    /// <list type="bullet">
    /// <item><description>StdioMcpTransport.cs：协议流，MCP 规范强制 JSON-RPC over stdio 用 UTF-8，固定 UTF-8 是正确姿势；</description></item>
    /// <item><description>ScriptExecutor.cs：事件流形态（BeginOutputReadLine），迁移挂账 TODO「PC-011」expires 2026-12-31，收口后必须移出本表。</description></item>
    /// </list>
    /// 03-plan 曾列 RunnerService.cs（不消费文本），实测其不命中任何禁用 token，无需占位豁免。
    /// </summary>
    private static readonly string[] AllowlistFiles =
    {
        @"Plugins\McpCenter\Services\McpClient\StdioMcpTransport.cs",
        @"Plugins\ScriptRunner\Services\ScriptExecutor.cs",
    };

    /// <summary>生产源码根（测试工程豁免：测试起桩进程属测试基建，不构成 shipped 缺陷面）。</summary>
    private static readonly string[] ProductionDirNames =
    {
        "ForgeSelf.Api", "ForgeSelf.Core", "ForgeSelf.Abstractions", "ForgeSelf.Bootstrapper", "Plugins",
    };

    [Fact]
    public void 生产源码_子进程输出_禁止固定码页解码()
    {
        var repoRoot = RepositoryRoot();
        var allowlist = new HashSet<string>(
            AllowlistFiles.Select(f => Path.GetFullPath(Path.Combine(repoRoot, f))), StringComparer.OrdinalIgnoreCase);

        var offenders = new List<string>();
        var allowlistedHits = 0;
        var files = ProductionDirNames
            .SelectMany(d => Directory.GetFiles(Path.Combine(repoRoot, d), "*.cs", SearchOption.AllDirectories))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .OrderBy(p => p, StringComparer.Ordinal);

        foreach (var file in files)
        {
            var code = StripComments(File.ReadAllText(file));
            foreach (var token in ForbiddenTokens)
            {
                var idx = code.IndexOf(token, StringComparison.Ordinal);
                while (idx >= 0)
                {
                    var line = code.Substring(0, idx).Count(c => c == '\n') + 1;
                    var rel = Path.GetRelativePath(repoRoot, file);
                    if (allowlist.Contains(Path.GetFullPath(file)))
                        allowlistedHits++;
                    else
                        offenders.Add($"{rel}:{line} → 出现「{token}」。" +
                            "整改：读 BaseStream 原始字节，交 ForgeSelf.Core.ProcessOutputText.Decode（整段）" +
                            "或 ProcessOutputLineBuffer（流式逐行）裁决（PC-011，见 docs/11-troubleshooting/problem-classes/）。");
                    idx = code.IndexOf(token, idx + token.Length, StringComparison.Ordinal);
                }
            }
        }

        offenders.Should().BeEmpty(
            "固定码页解码子进程输出是 PC-011 已实证的缺陷类（GBK 中文烧成 U+FFFD / 协议 UTF-8 被宿主码页毁掉），违规明细：\n"
            + string.Join("\n", offenders));

        // 阳性对照：allowlist 内确实扫到命中（防 token 表/扫描根失效导致"永远绿"假守卫）
        allowlistedHits.Should().BeGreaterThan(0,
            "StdioMcpTransport 必有 StandardOutputEncoding = 命中（协议 UTF-8 豁免）；扫到 0 = 本守卫扫描失效，必须修扫描而非放行");
    }

    /// <summary>去掉行注释与块注释，避免「站点迁移说明注释里写出被禁 token 的名字」误判。</summary>
    private static string StripComments(string source) =>
        Regex.Replace(
            Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline),
            @"//.*$", "", RegexOptions.Multiline);

    /// <summary>定位仓库根（认 ForgeSelf.slnx 那层；照 PluginToolConstructorDiGuardTests 实测教训，
    /// 直接找子目录会先撞上测试输出目录里的发布态副本）。</summary>
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
                return dir.FullName;
            dir = dir.Parent; // 必须推进指针，否则死循环（历史实测挂 11 分钟）
        }

        throw new FileNotFoundException("找不到仓库根（ForgeSelf.slnx）");
    }
}
