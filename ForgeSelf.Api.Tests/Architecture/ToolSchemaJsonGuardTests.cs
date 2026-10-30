using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.FileTools;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Architecture;

/// <summary>
/// 工具参数 Schema JSON 守卫（PC-017 · 2026-10-30 万能工具实测暴露）。
///
/// 缺陷模式：write_file 的 <c>ParametersJsonSchema</c> 中 <c>"path": {</c> 键连写两行
/// （<c>Plugins/FileTools/Services/FileToolFunctions.cs</c> 原 242-243 行），verbatim 字符串
/// 少一个闭合括号——C# 里只是普通字符串、编译全绿，但网关/ToolRegistry 在解析工具参数模板时
/// 必抛「Expected depth to be zero at the end of the JSON payload」，该工具任何调用全挂
/// （:51888 实测 3 连挂，错误恒定 LineNumber 26；run_terminal_command / read_file / list_files 同批正常）。
///
/// 不变量：<b>所有注册给 AI 工具面的 ParametersJsonSchema 必须是可解析的 JSON 对象</b>——
/// 它是网关调用前的必经契约文本，坏一处 = 该工具对全部客户端不可用且错误极具迷惑性。
///
/// 守卫等级 G2（结构性问题首次即用类级枚举）：
/// ① 源码级全量枚举——Plugins/**/*.cs 提取全部 verbatim schema 字符串逐个 Parse（2026-10-30 实测基线 74 个，命中仅 write_file 一处）；
/// ② file-tools 实例级复检——构造 4 个工具对象直接 Parse（DesignSystem 的运行期拼接 schema 源码提取不到，
///    实例级枚举扩展记 TODO，见 PC-017 悬空教训）。
/// ③ write_file 五参必填契约锁——防未来编辑丢 required。
///
/// 阳性对照：扫到 0 个 schema = 正则静默失效，必须修正则而非放行（照 PluginToolConstructorDiGuardTests 规矩）。
/// </summary>
public class ToolSchemaJsonGuardTests
{
    /// <summary>
    /// 匹配 verbatim 字符串形式的 schema 赋值/表达式（<c>ParametersJsonSchema</c> 或以 Schema 结尾的属性）。
    /// @("…" 与 "") 内部为 verbatim 转义（双引号成对），捕获组含首尾引号。
    /// </summary>
    private static readonly Regex VerbatimSchemaPattern = new(
        "(?s)(?:ParametersJsonSchema|Schema)\\s*(?:=>|=)\\s*@(\"(?:\"\"|[^\"])*\")",
        RegexOptions.Compiled);

    [Fact]
    public void 全插件工具_ParametersJsonSchema_必须是可解析JSON()
    {
        var pluginsRoot = PluginsRootOfRepository();
        var offenders = new List<string>();
        var scanned = 0;

        foreach (var file in Directory.GetFiles(pluginsRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(p => p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         .All(s => !s.StartsWith('_') && !s.Equals("obj", StringComparison.OrdinalIgnoreCase)
                                   && !s.Equals("bin", StringComparison.OrdinalIgnoreCase)
                                   && !s.Equals("node_modules", StringComparison.OrdinalIgnoreCase)))
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var text = File.ReadAllText(file);
            foreach (System.Text.RegularExpressions.Match m in VerbatimSchemaPattern.Matches(text))
            {
                scanned++;
                var literal = m.Groups[1].Value;
                var json = literal.Substring(1, literal.Length - 2).Replace("\"\"", "\"");
                var line = text.Substring(0, m.Index).Count(c => c == '\n') + 1;
                try
                {
                    using var doc = JsonDocument.Parse(json);
                }
                catch (JsonException ex)
                {
                    offenders.Add(
                        $"{Path.GetRelativePath(pluginsRoot, file)}:{line} → schema 不是合法 JSON：{ex.Message}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "工具参数 schema 是网关每次调用前必解析的契约文本（PC-017：write_file 重复键致任何参数都 INVALID_PARAMETERS），"
            + "违规明细：\n" + string.Join("\n", offenders));

        // 阳性对照：2026-10-30 实测基线 74 个 verbatim schema；扫到 0 个 = 正则失效，必须修正则而非放行。
        scanned.Should().BeGreaterThan(40,
            "Plugins 源码内应存在大量 verbatim schema 字符串；扫到 0 个 = 本守卫正则失效，必须修正则而非放行");
    }

    [Fact]
    public void FileTools四工具_实例级构造_Schema可解析()
    {
        var ctx = Mock.Of<IContext>();
        var tools = new List<IToolFunctionExtension>
        {
            new ListFilesToolFunction(ctx),
            new ReadFileToolFunction(ctx),
            new WriteFileToolFunction(ctx),
            new FolderStatsToolFunction("file-tools", null),
        };

        foreach (var tool in tools)
        {
            var act = () => JsonDocument.Parse(tool.ParametersJsonSchema);
            act.Should().NotThrow(
                $"{tool.Name} 的参数 schema 必须是可解析 JSON（PC-017：write_file 曾因重复键少闭合括号对任何调用全挂）");
        }
    }

    [Fact]
    public void WriteFileTool_Schema_锁定五参必填契约()
    {
        var tool = new WriteFileToolFunction(Mock.Of<IContext>());
        using var doc = JsonDocument.Parse(tool.ParametersJsonSchema);
        var root = doc.RootElement;

        root.TryGetProperty("required", out var required).Should().BeTrue("schema 应含 required 数组（2026-10-29 cwd 必传拍板）");
        var names = required.EnumerateArray().Select(e => e.GetString()).ToList();
        names.Should().BeEquivalentTo("cwd", "path", "content", "start_line", "end_line");

        root.TryGetProperty("properties", out var props).Should().BeTrue();
        foreach (var p in new[] { "cwd", "path", "content", "start_line", "end_line" })
        {
            props.TryGetProperty(p, out _).Should().BeTrue($"properties 缺 {p}");
        }
    }

    /// <summary>定位仓库根 Plugins 目录（先认 ForgeSelf.slnx 那层——照 PluginToolConstructorDiGuardTests 实测教训，
    /// 直接找会先撞上测试输出目录里的发布态副本）。</summary>
    private static string PluginsRootOfRepository()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
                return Path.Combine(dir.FullName, "Plugins");
            dir = dir.Parent;   // 必须推进指针，否则死循环（PC 实测挂过 11 分钟）
        }

        throw new FileNotFoundException("找不到仓库根（ForgeSelf.slnx）");
    }
}
