using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Architecture;

/// <summary>
/// 架构守卫（PILOT-062 · 2026-10-17「全部都做」批）：<b>插件工具/服务构造函数禁止解析服务</b>。
///
/// 缺陷模式（TodoTracker / sems / AgentHub 三次实证，复现批
/// <c>docs/ai/pilot/2026-10-17-gateway-list-todos-scope-repro/</c>）：插件 <c>Apply</c> 在
/// <c>builder.Build()</c> 之前执行，宿主根 <c>IServiceProvider</c> 要到 <c>ProvideHostServices</c>
/// 才 seed 进根 Context——构造期解析恒 miss 并把未就绪的插件 Context 存进字段，
/// 请求期 <c>CreateScope()</c> 抛出离真因极远的「No service for type 'IServiceScopeFactory'…」。
///
/// 正确范式：构造只存原始上下文，执行期调 <c>ForgeSelf.Abstractions.PluginContextDiExtensions</c>
/// 的 <c>RequireHostProvider/CreateHostScope</c> 即时解析（对 seed 时序免疫、未就绪报明确错）。
/// 本守卫把该范式变成 CI 判据：命中「含 IServiceProvider/IContext 参数的构造函数体内解析服务」即红。
///
/// 照既有守卫规矩（plugin-development §七B）：先剥注释再匹配、上溯循环推进指针、变异探针实证翻红。
/// </summary>
public class PluginToolConstructorDiGuardTests
{
    /// <summary>构造/主构造函数体内视为「解析服务」的调用 token（出现即违规）。</summary>
    private static readonly string[] ForbiddenResolutionTokens =
    {
        ".GetService(", ".GetRequiredService(", ".GetRequiredService<", ".GetService<",
        ".CreateScope(", ".CreateHostScope(", "Resolve(", "Resolve<", "ResolveHost<",
    };

    /// <summary>
    /// 匹配签名含 <c>IServiceProvider</c>/<c>IContext</c> 的构造函数（块体或表达式体）。
    /// 构造名 = 类名（首字母大写标识符），后随参数表，再跟 <c>{...}</c> 或 <c>=&gt;...;</c>。
    /// </summary>
    private static readonly Regex ConstructorPattern = new(
        @"(?:public|protected|internal)\s+[A-Za-z_][\w.]*\s*\((?:[^)]*)?(?:IServiceProvider|IContext)(?:[^)]*)?\)\s*(?:=>[^;{]*;|\{[^{}]*\})",
        RegexOptions.Compiled);

    [Fact]
    public void 插件构造函数_禁止解析服务_只许存原始上下文()
    {
        var pluginsRoot = PluginsRootOfRepository();
        var offenders = new List<string>();
        var scannedConstructors = 0;

        foreach (var file in Directory.GetFiles(pluginsRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(p => p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         .All(s => !s.StartsWith('_') && !s.Equals("obj", StringComparison.OrdinalIgnoreCase)
                                   && !s.Equals("bin", StringComparison.OrdinalIgnoreCase)))
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var code = StripComments(File.ReadAllText(file));
            foreach (System.Text.RegularExpressions.Match m in ConstructorPattern.Matches(code))
            {
                scannedConstructors++;
                var hit = ForbiddenResolutionTokens.FirstOrDefault(t => m.Value.Contains(t));
                if (hit != null)
                {
                    var line = code.Substring(0, m.Index).Count(c => c == '\n') + 1;
                    offenders.Add(
                        $"{Path.GetRelativePath(pluginsRoot, file)}:{line} → 构造期出现「{hit}」。" +
                        "整改：构造只存原始上下文，执行期经 ForgeSelf.Abstractions.PluginContextDiExtensions" +
                        ".RequireHostProvider/CreateHostScope 即时解析（见 2026-10-17-plugin-di-helper-todo-fix）。");
                }
            }
        }

        offenders.Should().BeEmpty("插件工具/服务构造期解析宿主 DI 是已三次实证的缺陷模式（seed 晚于 Apply），违规明细：\n"
            + string.Join("\n", offenders));

        // 阳性对照：守卫确实扫到了含 provider/context 参数的构造函数（防正则静默失效假绿）
        scannedConstructors.Should().BeGreaterThan(0,
            "Plugins 源码内应存在 IServiceProvider/IContext 参数的构造函数（TodoTracker 8 个工具即在内）；" +
            "扫到 0 个 = 本守卫正则失效，必须修正则而非放行");
    }

    /// <summary>去掉行注释与块注释，避免「为了说明为什么不能出现 X 而写出 X 的名字」误判（§七B 两次踩坑教训）。</summary>
    private static string StripComments(string source) =>
        Regex.Replace(
            Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline),
            @"//.*$", "", RegexOptions.Multiline);

    /// <summary>定位仓库根 Plugins 目录（先认 ForgeSelf.slnx 那层，再拼 Plugins——照 TodoAgentDelegationTests 的实测教训，
    /// 直接找 Plugins 会先撞上测试输出目录里的发布态副本）。</summary>
    private static string PluginsRootOfRepository()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
                return Path.Combine(dir.FullName, "Plugins");
            dir = dir.Parent;   // 必须推进指针，否则死循环（实测挂过 11 分钟）
        }

        throw new FileNotFoundException("找不到仓库根（ForgeSelf.slnx）");
    }
}
