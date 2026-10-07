using FluentAssertions;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.TodoTracker;

/// <summary>
/// 下发载荷组装（PILOT-054 · FR-1.1 / Output-3 / AC-7）。
/// 纯函数直接锁形状：提示词里必须出现「项目根、taskKey、验收判据、验证命令、回报契约」，
/// 缺必填项时必须点名缺哪一项 —— 这两件事错一个字段，接手的 agent 就只能回来问人。
/// </summary>
public class DispatchPayloadBuilderTests
{
    private static DispatchPayloadBuilder.TaskPayload Full(int stage = TodoStage.Ready) => new(
        Id: 7,
        TaskKey: "3f2b9c1e00004a1f9b7c2d8e5f6a1b0c",
        Title: "给 Todo 插件加执行记录",
        Objective: "执行记录可在界面与 REST 两侧读写",
        Content: "## 背景\n把做了什么写进台账",
        Acceptance: "- [ ] AC-1 记录可写\n- [ ] AC-2 记录可读",
        Verification: "dotnet test --filter ~TodoTracker\npnpm run check",
        AllowedScope: "Plugins/TodoTracker/**\nForgeSelf.Web/src/views/**",
        ForbiddenScope: "宿主实体结构",
        Priority: 1,
        Stage: stage,
        ProjectRoot: @"D:\project",
        ProjectPathRaw: "/d/project",
        ArtifactRef: @"docs\ai\pilot\2026-10-07-todo-agent-dispatch",
        Assignee: "qoder",
        PermissionMode: "read-only");

    private static DispatchPayloadBuilder.TaskPayload Lacking(params string[] fields)
    {
        var t = Full();
        foreach (var f in fields)
        {
            switch (f)
            {
                case "objective": t = t with { Objective = "  " }; break;
                case "content": t = t with { Content = "" }; break;
                case "acceptance": t = t with { Acceptance = "" }; break;
                case "verification": t = t with { Verification = "" }; break;
            }
        }
        return t;
    }

    [Fact]
    public void 必填齐备时可下发且无缺口()
    {
        var task = Full();

        DispatchPayloadBuilder.Missing(task).Should().BeEmpty();
        DispatchPayloadBuilder.CanDispatch(task).Should().BeTrue();
    }

    [Theory]
    [InlineData("objective")]
    [InlineData("content")]
    [InlineData("acceptance")]
    [InlineData("verification")]
    public void 缺任一项必填都应点名(string field)
    {
        var task = Lacking(field);

        DispatchPayloadBuilder.Missing(task).Should().Equal(field);
        DispatchPayloadBuilder.CanDispatch(task).Should().BeFalse();
    }

    [Fact]
    public void 提示词应带齐agent开工需要的六块内容()
    {
        var markdown = DispatchPayloadBuilder.BuildMarkdown(Full(), "http://localhost:7102");

        markdown.Should().Contain("3f2b9c1e00004a1f9b7c2d8e5f6a1b0c", "agent 回报要用 taskKey 引用任务");
        markdown.Should().Contain(@"D:\project", "工作目录必须写归一后的根，而不是用户随手写的 /d/project");
        markdown.Should().Contain("/d/project", "同时回显用户自己的写法，便于确认没理解错");
        markdown.Should().Contain("## Objective");
        markdown.Should().Contain("## 允许改动范围");
        markdown.Should().Contain("## 禁止改动范围");
        markdown.Should().Contain("## 验收判据");
        markdown.Should().Contain("## 验证命令");
        markdown.Should().Contain("## 完成后必须回报", "没有回报契约，agent 做完就没人知道");
        markdown.Should().Contain("P1");
        markdown.Should().Contain("docs\\ai\\pilot\\2026-10-07-todo-agent-dispatch", "来源工件要可反查");
    }

    [Fact]
    public void 回报契约应指向按外部键的端点且token只留占位符()
    {
        var markdown = DispatchPayloadBuilder.BuildMarkdown(Full(), "http://localhost:7102/");

        markdown.Should().Contain("http://localhost:7102/api/todos/by-key/3f2b9c1e00004a1f9b7c2d8e5f6a1b0c/records");
        markdown.Should().Contain("/stage");
        markdown.Should().Contain("Bearer <token>", "密钥绝不进正文（BR-7）");
        markdown.Should().NotContain("forge_api_token=");
        markdown.Should().NotContain("```bash\n```", "契约段不能是空代码块");
    }

    [Fact]
    public void 未给基址时回报地址应写成占位符而不是伪造地址()
    {
        var markdown = DispatchPayloadBuilder.BuildMarkdown(Full(), null);

        markdown.Should().Contain("<宿主地址>/api/todos/by-key/");
        markdown.Should().NotContain("localhost:7102", "服务层不该猜宿主端口；地址由控制器按真实请求给出");
    }

    [Fact]
    public void 验收判据与验证命令应逐条成形()
    {
        var markdown = DispatchPayloadBuilder.BuildMarkdown(Full(), null);

        markdown.Should().Contain("- [ ] AC-1 记录可写");
        markdown.Should().Contain("- [ ] AC-2 记录可读");
        markdown.Should().Contain("```bash\ndotnet test --filter ~TodoTracker\npnpm run check\n```");
    }

    [Fact]
    public void 载荷JSON应使用委派契约的键名且不丢工作目录()
    {
        var json = DispatchPayloadBuilder.BuildAgentHubJson(Full(), agentId: 3);

        json.Should().Contain("\"prompt\":");
        json.Should().Contain("\"agentId\":3");
        json.Should().Contain("\"cwd\":\"D:\\\\project\"");
        json.Should().Contain("\"permissionMode\":\"read-only\"");
        json.Should().Contain("\"createdBy\":\"todo-tracker\"");
    }

    [Fact]
    public void 未关联项目时给的是软提醒而不是拦截()
    {
        var task = Full() with { ProjectRoot = "", ProjectPathRaw = "" };

        DispatchPayloadBuilder.Missing(task).Should().BeEmpty("项目不是下发的必填项");
        DispatchPayloadBuilder.Warnings(task).Should().ContainSingle(w => w.Contains("未关联项目"));
    }

    [Theory]
    [InlineData("A README.md", "A", "README.md")]
    [InlineData("M:src/a.cs", "M", "src/a.cs")]
    [InlineData("  \"docs/x.md\"  ", "", "docs/x.md")]
    public void 文件列表的三种常见写法都应归一(string line, string expectedChange, string expectedPath)
    {
        var storage = FileChangeList.ToStorage(null, line);

        var parsed = FileChangeList.FromStorage(storage).Should().ContainSingle().Subject;
        parsed.Change.Should().Be(expectedChange);
        parsed.Path.Should().Be(expectedPath);
    }

    [Fact]
    public void 同一路径重复出现只保留最后一次变更()
    {
        var storage = FileChangeList.ToStorage(
            [new ForgeSelf.Api.Plugins.TodoTracker.Models.ChangedFileDto { Path = "a.cs", Change = "M" },
             new ForgeSelf.Api.Plugins.TodoTracker.Models.ChangedFileDto { Path = "a.cs", Change = "D" }],
            null);

        var items = FileChangeList.FromStorage(storage);
        items.Should().ContainSingle();
        items[0].Change.Should().Be("D");
    }

    [Fact]
    public void 存量不是合法JSON时不得丢信息()
    {
        // 阳性对照：合规 JSON 解析出条目；坏 JSON（历史脏数据）解析不出条目但原文要交出去，界面仍可显示
        FileChangeList.FromStorage("[{\"path\":\"a.cs\",\"change\":\"M\"}]").Should().ContainSingle();

        var broken = "{\"broken\":";
        FileChangeList.FromStorage(broken).Should().BeEmpty();
        FileChangeList.ToRawText(broken).Should().Be(broken);
        FileChangeList.ToRawText("[{\"path\":\"a.cs\",\"change\":\"M\"}]").Should().BeEmpty("能解析成条目时不需要兜底原文");
    }

    [Fact]
    public void 空输入归一后是空串而不是空数组字样()
    {
        FileChangeList.ToStorage(null, null).Should().BeEmpty();
        FileChangeList.ToStorage([], "   ").Should().BeEmpty();
        FileChangeList.FromStorage(string.Empty).Should().BeEmpty();
    }
}
