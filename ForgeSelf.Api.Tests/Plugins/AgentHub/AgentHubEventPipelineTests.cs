using System.Text.Json.Nodes;
using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>
/// EventPipeline 归一化测试：各 agent 的私有输出 → 统一事件。
///
/// 覆盖要点：
/// - JSONL 按 profile 映射表正确分派事件类型；
/// - 非 JSON 行**降级为 Text 而非丢弃**（CLI 混日志是常态）；
/// - 类型词表映射（各家叫法不一，必须收敛）；
/// - 超长文本截断并置 Truncated（不假装完整）；
/// - 参数模板展开：**prompt 里的 shell 元字符不得被当作命令**（安全铁证）。
/// </summary>
public class AgentHubEventPipelineTests
{
    /// <summary>构造一个带 codex 风格映射表的 profile</summary>
    private static AgentProfile CodexLikeProfile() => new()
    {
        Id = "test-agent",
        OutputFormat = "JsonLines",
        OutputMapping = new Dictionary<String, String>
        {
            ["type"] = "$.type",
            ["text"] = "$.item.text",
            ["tool"] = "$.item.name",
            ["sessionId"] = "$.session_id"
        }
    };

    [Fact]
    public void ParseLines_JsonLines_按映射表分派文本与工具事件()
    {
        var pipeline = new EventPipeline(CodexLikeProfile());
        var lines = new[]
        {
            """{"type":"text","item":{"text":"开始分析代码"}}""",
            """{"type":"tool_call","item":{"name":"read_file"}}"""
        };

        var events = pipeline.ParseLines(lines).ToList();

        events.Should().HaveCount(2);
        events[0].Type.Should().Be("Text");
        events[0].Text.Should().Be("开始分析代码");
        events[1].Type.Should().Be("ToolCall");
        events[1].Tool.Should().Be("read_file");
    }

    [Fact]
    public void ParseLines_非Json行_降级为Text而非丢弃()
    {
        var pipeline = new EventPipeline(CodexLikeProfile());
        var lines = new[]
        {
            "warning: 这是一行普通日志（不是 JSON）",
            """{"type":"text","item":{"text":"正常内容"}}"""
        };

        var events = pipeline.ParseLines(lines).ToList();

        // 关键：混入的日志行**必须保留内容**，不能静默吞掉
        events.Should().HaveCount(2);
        events[0].Type.Should().Be("Text");
        events[0].Text.Should().Contain("这是一行普通日志");
        events[1].Text.Should().Be("正常内容");
    }

    [Fact]
    public void ParseLines_Text格式_整行原样输出()
    {
        var pipeline = new EventPipeline(new AgentProfile { Id = "plain", OutputFormat = "Text" });

        var events = pipeline.ParseLines(["第一行", "第二行"]).ToList();

        events.Should().HaveCount(2);
        events.Should().OnlyContain(e => e.Type == "Text");
        events[0].Text.Should().Be("第一行");
    }

    [Fact]
    public void ParseLines_空行_跳过不产生事件()
    {
        var pipeline = new EventPipeline(new AgentProfile { OutputFormat = "Text" });

        var events = pipeline.ParseLines(["", "   ", "有内容"]).ToList();

        events.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("text", "Text")]
    [InlineData("assistant_message", "Text")]
    [InlineData("reasoning", "Thought")]
    [InlineData("tool_use", "ToolCall")]
    [InlineData("tool_result", "ToolResult")]
    [InlineData("write_file", "FileChange")]
    [InlineData("permission_request", "PermissionRequest")]
    [InlineData("error", "Error")]
    [InlineData("turn_complete", "Exit")]
    [InlineData("session_start", "Meta")]
    [InlineData("某个没见过的类型", "Meta")]
    [InlineData(null, "Text")]
    public void NormalizeType_各家叫法收敛到统一词表(string? raw, string expected)
    {
        EventPipeline.NormalizeType(raw).Should().Be(expected);
    }

    [Fact]
    public void 超长文本_截断并置Truncated_不假装完整()
    {
        var pipeline = new EventPipeline(new AgentProfile { OutputFormat = "Text" });
        var huge = new string('x', EventPipeline.MaxEventTextLength + 5000);

        var evt = pipeline.ParseLines([huge]).Single();

        evt.Text!.Length.Should().Be(EventPipeline.MaxEventTextLength);
        evt.Truncated.Should().BeTrue("前端需要显式提示内容被截断");
    }

    [Fact]
    public void MapToEvent_退出码_从映射路径解析()
    {
        var profile = new AgentProfile
        {
            OutputFormat = "Json",
            OutputMapping = new Dictionary<String, String>
            {
                ["type"] = "$.type",
                ["text"] = "$.message",
                ["exitCode"] = "$.code"
            }
        };
        var pipeline = new EventPipeline(profile);
        var node = JsonNode.Parse("""{"type":"exit","message":"完成","code":0}""")!;

        var evt = pipeline.MapToEvent(node);

        evt.Type.Should().Be("Exit");
        evt.Text.Should().Be("完成");
        evt.ExitCode.Should().Be(0);
    }

    [Fact]
    public void MapToEvent_无映射表_整体保留为Meta不丢信息()
    {
        var pipeline = new EventPipeline(new AgentProfile { OutputFormat = "Json" });
        var node = JsonNode.Parse("""{"unknown_field":"值","nested":{"a":1}}""")!;

        var evt = pipeline.MapToEvent(node);

        evt.Type.Should().Be("Meta");
        evt.Payload.Should().NotBeNull("原样保留上游片段，便于复盘");
    }

    [Fact]
    public void ReadPath_支持数组下标与嵌套路径()
    {
        var node = JsonNode.Parse("""{"items":[{"text":"第一个"},{"text":"第二个"}]}""")!;

        EventPipeline.ReadPath(node, "$.items[1].text").Should().Be("第二个");
        EventPipeline.ReadPath(node, "$.items[0].text").Should().Be("第一个");
        EventPipeline.ReadPath(node, "$.items[5].text").Should().BeNull("越界返回 null，不抛异常");
        EventPipeline.ReadPath(node, "$.不存在").Should().BeNull();
    }

    // ===== 安全铁证：prompt 里的 shell 元字符不得被当作命令 =====

    [Fact]
    public void BuildArgs_含分号与管道符的prompt_作为单个参数项不拆分()
    {
        var req = new AgentRunRequest
        {
            Prompt = "分析代码; rm -rf / && echo hacked | tee /tmp/x",
            Cwd = "/tmp/work",
            PermissionMode = "read-only"
        };

        var args = CliTransport.BuildArgs("exec \"{prompt}\" --json", req, req.Prompt);

        // 关键：整个 prompt 必须落在**一个** argv 项里
        // （参数经 ArgumentList 数组传给进程，操作系统不会做 shell 解析）
        // 模板 exec "{prompt}" --json → 3 项：exec / "<prompt>" / --json
        args.Should().HaveCount(3);
        args[1].Should().Be("\"分析代码; rm -rf / && echo hacked | tee /tmp/x\"",
            "prompt 内的分号与竖线不得把参数切碎，引号原样保留交由运行库处理");

        // 直接钉住"没被切碎"：prompt 里的 | 不得产生额外参数项
        args.Should().NotContain("tee /tmp/x", "竖线后的内容必须仍在同一个参数项内");
    }

    [Fact]
    public void BuildArgs_多参数模板_按换行或竖线切分为独立argv项()
    {
        var req = new AgentRunRequest { Cwd = @"D:\proj", PermissionMode = "read-only" };

        var args = CliTransport.BuildArgs("exec\n--json\n-C\n{cwd}", req, null);

        args.Should().BeEquivalentTo(["exec", "--json", "-C", @"D:\proj"]);
    }

    [Fact]
    public void BuildArgs_未知占位符_原样保留不静默替换为空()
    {
        var req = new AgentRunRequest { Cwd = "/w" };

        var args = CliTransport.BuildArgs("--model {model} --weird {unknownThing}", req, null);

        // {model} 无值 → 替换为空串；未知占位符必须保留，避免命令走样
        args.Should().Contain("{unknownThing}");
    }

    [Fact]
    public void BuildArgs_Stdin注入模式_模板不含prompt()
    {
        var req = new AgentRunRequest { Prompt = "任务内容", Cwd = "/w" };

        var args = CliTransport.BuildArgs("exec --json", req, null);

        args.Should().BeEquivalentTo(["exec", "--json"]);
        args.Should().NotContain(a => a.Contains("任务内容"), "prompt 走 stdin，不进命令行");
    }
}
