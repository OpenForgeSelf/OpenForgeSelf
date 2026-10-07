using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeSelf.Api.Plugins.ToolBridge.Models;
using ForgeSelf.Api.Plugins.ToolBridge.Services;

namespace ForgeSelf.Api.Tests.Plugins.ToolBridgeTests;

/// <summary>
/// AC9 / FR-4：回粘文本格式（纯函数，无 IO）。
/// 重点不是措辞，而是三件会坏的事：JSON 模式标记之间必须可直接解析、plain 模式必须**逐字节原文**、
/// 以及被拒/未识别/未解析条目**也必须在文本里**（FR-4.3——否则 AI 收到空白会原地重复同一次调用）。
/// </summary>
public class ResultFormatterTests
{
    private static ToolResult ReadOk() => new()
    {
        Tool = "read_file",
        RawName = "read_file",
        Ok = true,
        Args = new Dictionary<string, JsonNode> { ["path"] = JsonValue.Create("notes/a.txt")! },
        Result = new JsonObject { ["path"] = "notes/a.txt", ["content"] = "第一行\n第二行 中文", ["bytes"] = 24 },
        DurationMs = 3
    };

    private static ToolResult CommandRejected() => new()
    {
        Tool = "run_command",
        RawName = "exec",
        Ok = false,
        Error = "command_rejected",
        Reason = "Error: command not allowed by terminal allowlist: rm. （默认白名单拒绝模式，仅放行 dotnet/pnpm/node/git/ssh/pwsh；如需扩展请经 Agent 配置调整）",
        DurationMs = 0
    };

    private static ToolResult TruncatedRead() => new()
    {
        Tool = "read_file",
        Ok = true,
        Result = new JsonObject { ["path"] = "big.txt", ["content"] = "前段内容" },
        Truncated = true,
        OriginalBytes = 1_234_567
    };

    private static IReadOnlyList<UnknownCall> Unknown() => new List<UnknownCall>
    {
        new() { RawName = "delete_file", Reason = "工具名不在本插件清单内（不做模糊匹配，绝不就近执行）", Suggestion = "write_file" }
    };

    private static IReadOnlyList<UnparsedFragment> Unparsed() => new List<UnparsedFragment>
    {
        new() { Fragment = "接下来我打算 write_file 一下", Reason = "疑似工具名「write_file」但缺参数结构（需要 key=value 或 JSON 参数）" }
    };

    [Fact]
    public void json模式_标记之间是合法JSON且逐条可解析_AC9()
    {
        var text = ResultFormatter.BuildJson(new List<ToolResult> { ReadOk(), CommandRejected() }, Unknown(), Unparsed());

        text.Should().StartWith(PromptBuilder.ResultMarker);
        text.Should().EndWith(PromptBuilder.ResultEndMarker);

        var payload = BetweenMarkers(text);
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        root.GetProperty("tool_bridge_results").GetArrayLength().Should().Be(2);
        var first = root.GetProperty("tool_bridge_results")[0];
        first.GetProperty("tool").GetString().Should().Be("read_file");
        first.GetProperty("ok").GetBoolean().Should().BeTrue();
        first.GetProperty("args").GetProperty("path").GetString().Should().Be("notes/a.txt");
        first.GetProperty("result").GetProperty("content").GetString().Should().Be("第一行\n第二行 中文");

        var second = root.GetProperty("tool_bridge_results")[1];
        second.GetProperty("ok").GetBoolean().Should().BeFalse();
        second.GetProperty("error").GetString().Should().Be("command_rejected");
        second.GetProperty("reason").GetString()!.Should().Contain("allowlist");

        root.GetProperty("unrecognized").GetArrayLength().Should().Be(1);
        root.GetProperty("unparsed").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void json模式_中文不转义_AI可直接读()
    {
        var text = ResultFormatter.BuildJson(new List<ToolResult> { ReadOk() }, new List<UnknownCall>(), new List<UnparsedFragment>());

        text.Should().Contain("第二行 中文", "非 ASCII 转义成 \\uXXXX 会让模型读到的观察变形");
    }

    [Fact]
    public void plain模式_stdout与content原文逐字节一致_AC9()
    {
        var original = "第一行\n第二行 中文\n\t缩进行\n";
        var result = new ToolResult
        {
            Tool = "run_command",
            Ok = true,
            Result = new JsonObject
            {
                ["command"] = "git status --short",
                ["exitCode"] = 0,
                ["stdout"] = original,
                ["stderr"] = ""
            }
        };

        var text = ResultFormatter.BuildPlain(new List<ToolResult> { result }, new List<UnknownCall>(), new List<UnparsedFragment>());
        var between = BetweenRawSections(text, "stdout");

        between.Should().Be(original, "回粘必须原文，不得转义/缩进/改行");
        text.Should().Contain("exitCode: 0");
    }

    [Fact]
    public void plain模式_截断必带原长_FR42()
    {
        var text = ResultFormatter.BuildPlain(new List<ToolResult> { TruncatedRead() }, new List<UnknownCall>(), new List<UnparsedFragment>());

        text.Should().Contain("truncated: true");
        text.Should().Contain("原长 1234567 字节");
    }

    [Fact]
    public void 被拒与未识别与未解析条目_两种模式都在_FR43()
    {
        var results = new List<ToolResult> { CommandRejected() };

        var json = ResultFormatter.BuildJson(results, Unknown(), Unparsed());
        json.Should().Contain("command_rejected");
        json.Should().Contain("delete_file");
        json.Should().Contain("疑似工具名");

        var plain = ResultFormatter.BuildPlain(results, Unknown(), Unparsed());
        plain.Should().Contain("未执行 error=command_rejected");
        plain.Should().Contain("未识别的工具名 delete_file");
        plain.Should().Contain("suggestion: write_file");
        plain.Should().Contain("未解析片段");
    }

    [Fact]
    public void 本轮零调用时给出可读说明而不是空文本()
    {
        var plain = ResultFormatter.BuildPlain(new List<ToolResult>(), new List<UnknownCall>(), new List<UnparsedFragment>());

        plain.Should().Contain("本轮没有任何调用");
    }

    [Fact]
    public void Build_按mode选择两种形态()
    {
        var results = new List<ToolResult> { ReadOk() };

        ResultFormatter.Build(results, Unknown(), Unparsed(), ResultFormatter.ModeJson).Should().Contain("mode=json");
        ResultFormatter.Build(results, Unknown(), Unparsed(), ResultFormatter.ModePlain).Should().Contain("mode=plain");
    }

    private static string BetweenMarkers(string text)
    {
        var start = text.IndexOf('\n') + 1;
        var end = text.LastIndexOf(PromptBuilder.ResultEndMarker, StringComparison.Ordinal);
        return text[start..end].Trim();
    }

    private static string BetweenRawSections(string text, string key)
    {
        var open = $"  ---- {key} ----\n";
        var close = $"  ---- end {key} ----";
        var s = text.IndexOf(open, StringComparison.Ordinal);
        var e = text.IndexOf(close, StringComparison.Ordinal);
        s.Should().BeGreaterThan(-1, $"plain 模式必须为 {key} 开出原文段");
        e.Should().BeGreaterThan(s);
        // 段尾的换行是分隔符本身，不属于原文
        var body = text[(s + open.Length)..e];
        return body.EndsWith("\n") ? body[..^1] : body;
    }
}
