using ForgeSelf.Api.Plugins.ToolBridge.Services;

namespace ForgeSelf.Api.Tests.Plugins.ToolBridgeTests;

/// <summary>
/// AC3 / AC4 / AC5 + BC-1/2/3/12：四档格式解析、零副作用、不猜原则。
/// 解析器签名里没有 SandboxRoot / 执行器参数 ⇒ "解析不执行"是**编译期**就成立的；
/// 下面的用例再加文件系统层面的可观测证据（前后目录快照一致）。
/// </summary>
public class CallParserTests : IDisposable
{
    private readonly string _probeDir;

    public CallParserTests()
    {
        // 只创建、只使用、不删除（plugin-development 铁律 10）。
        _probeDir = Path.Combine(Path.GetTempPath(), "forgeself-toolbridge-tests", $"CallParser_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_probeDir);
        File.WriteAllText(Path.Combine(_probeDir, "existing.txt"), "keep me");
    }

    public void Dispose()
    {
        // 故意不删目录：测试夹具不做任何删除动作。
    }

    private void AssertNoFileSystemSideEffects(Action act)
    {
        var before = Directory.GetFileSystemEntries(_probeDir, "*", SearchOption.AllDirectories).OrderBy(p => p).ToArray();
        var contentBefore = File.ReadAllText(Path.Combine(_probeDir, "existing.txt"));

        act();

        var after = Directory.GetFileSystemEntries(_probeDir, "*", SearchOption.AllDirectories).OrderBy(p => p).ToArray();
        after.Should().Equal(before, "解析不得创建/删除任何文件");
        File.ReadAllText(Path.Combine(_probeDir, "existing.txt")).Should().Be(contentBefore);
    }

    // ---------- P1 围栏 JSON ----------

    [Fact]
    public void 围栏单条对象_带args_识别为一条()
    {
        var text = """
            我来写这个文件：
            ```json
            { "tool": "write_file", "args": { "path": "notes/plan.md", "content": "# 计划" } }
            ```
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().ContainSingle();
        r.Calls[0].Tool.Should().Be("write_file");
        r.Calls[0].GetArgString("path").Should().Be("notes/plan.md");
        r.Calls[0].GetArgString("content").Should().Be("# 计划");
        r.Calls[0].Via.Should().Be("json");
    }

    [Fact]
    public void 围栏单条对象_引子正文进未解析并带原因()
    {
        var text = """
            好的，我先创建文件，然后列出目录。
            ```json
            { "name": "write_file", "arguments": { "path": "a.txt", "content": "A" } }
            ```
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().ContainSingle();
        r.Unparsed.Should().NotBeEmpty();
        r.Unparsed[0].Reason.Should().Contain("未找到结构化调用字段");
        r.Unparsed[0].Fragment.Should().Contain("好的");
    }

    [Fact]
    public void OpenAI风格_tool_calls_与对象型arguments_识别()
    {
        var text = """
            ```json
            {
              "tool_calls": [
                { "id": "1", "type": "function", "function": { "name": "read_file", "arguments": { "path": "a.txt" } } },
                { "id": "2", "type": "function", "function": { "name": "list_dir", "arguments": { } } }
              ]
            }
            ```
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().HaveCount(2);
        r.Calls[0].Tool.Should().Be("read_file");
        r.Calls[0].Via.Should().Be("openai");
        r.Calls[1].Tool.Should().Be("list_dir");
    }

    /// <summary>
    /// U-1 真实样例（2026-10-06 用户从网页 AI 粘回的原文，逐字）。此前所有用例都是我自己写的形状，
    /// 缺"真 AI 会连着散文一起输出 + 单条调用 + 参数值为空串"这一组合：
    /// 前有两行中文说明、后有"请把执行结果贴回来…"追问，且 <c>arguments.path</c> 是空字符串。
    /// </summary>
    [Fact]
    public void 真实AI回复_散文夹一个json围栏_单条list_dir空路径_识别()
    {
        var text = """
            我已读到 toolbridge-spec v1，清楚了调用格式与限制。为了先了解工作目录里有什么，我先做一次只读列目录：

            ```json
            {
              "tool_calls": [
                { "id": "1", "type": "function", "function": { "name": "list_dir", "arguments": { "path": "" } } }
              ]
            }
            ```

            请把执行结果贴回来。同时告诉我你这次想完成的具体任务（要读/写哪些文件、要跑什么命令），我再据此发出后续调用。
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().ContainSingle("真实样例必须被识别为一条调用，否则用户粘回去点解析就是空结果");
        r.Calls[0].Tool.Should().Be("list_dir");
        r.Calls[0].Via.Should().Be("openai");
        r.Calls[0].GetArgString("path").Should().BeEmpty("空串是合法取值（工作根本身），不得当成缺参");
        r.Unknown.Should().BeEmpty();
        // 散文不该被算成"已解析"，但要作为未解析片段留痕（不猜原则）
        r.Unparsed.Should().NotBeEmpty();
    }

    /// <summary>
    /// 裸 JSON（**没有 ``` 围栏**）——网页聊天把代码块渲染出来后再复制，常常只带内容不带围栏。
    /// 2026-10-06 用户实测缺口：同一段文字带围栏能解析、去掉围栏界面就报"没认出工具调用"，
    /// 因为解析器原先只在围栏内试 JSON（CallParser.ParseFence），正文区只走标签式与 key=value 行。
    /// </summary>
    [Fact]
    public void 裸JSON无围栏_散文包裹_也识别()
    {
        var text = """
            我已读到 toolbridge-spec v1，清楚了调用格式与限制。为了先了解工作目录里有什么，我先做一次只读列目录：

            {
              "tool_calls": [
                { "id": "1", "type": "function", "function": { "name": "list_dir", "arguments": { "path": "" } } }
              ]
            }

            请把执行结果贴回来。
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().ContainSingle("复制自网页聊天的裸 JSON（无围栏）是最常见输入形态，不能落进未解析");
        r.Calls[0].Tool.Should().Be("list_dir");
        r.Calls[0].Via.Should().Be("openai");
    }

    /// <summary>整段就是一个裸 JSON 数组（没有散文、没有围栏）。</summary>
    [Fact]
    public void 裸JSON数组无围栏_识别多条()
    {
        var text = """
            [
              { "tool": "read_file", "args": { "path": "a.txt" } },
              { "name": "run_command", "arguments": { "command": "git status" } }
            ]
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().HaveCount(2);
        r.Calls[0].Tool.Should().Be("read_file");
        r.Calls[1].Tool.Should().Be("run_command");
    }

    /// <summary>散文里出现"像 JSON 但不是调用"的花括号内容，不得被硬掰成调用（不猜原则）。</summary>
    [Fact]
    public void 裸花括号不是调用结构_仍归未解析()
    {
        var text = """
            我在文档里举了个例子 { "foo": 1 }，另外还写了 { 缺引号的东西 }。
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().BeEmpty();
        r.Unknown.Should().BeEmpty();
    }

    /// <summary>
    /// 反向护栏：把「初始指令」里的**工具目录**整段裸粘回来，不得凭空造出 4 条调用
    /// （目录项也带 name 键，所以裸 JSON 识别必须排除 description/aliases/parametersSchema 这类特征键）。
    /// </summary>
    [Fact]
    public void 裸JSON工具目录_不当成调用_不猜原则()
    {
        var catalog = PromptBuilder.ToolsAsJson().ToJsonString();
        var text = "可用工具清单如下：\n" + catalog + "\n请按格式回复调用。";

        var r = CallParser.Parse(text);

        r.Calls.Should().BeEmpty("目录条目描述的是工具本身，不是某次调用");
        r.Unknown.Should().BeEmpty();
    }

    [Fact]
    public void OpenAI风格_arguments是字符串_二次解析成功_BC3()
    {
        var text = """
            ```json
            { "tool_calls": [ { "function": { "name": "write_file", "arguments": "{\"path\":\"b.txt\",\"content\":\"两行\\n第二行\"}" } } ] }
            ```
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().ContainSingle();
        r.Calls[0].Tool.Should().Be("write_file");
        r.Calls[0].GetArgString("path").Should().Be("b.txt");
        r.Calls[0].GetArgString("content").Should().Contain("\n");
    }

    [Fact]
    public void arguments字符串不是合法JSON_归未知并回原文_BC3()
    {
        var text = """
            ```json
            { "tool_calls": [ { "function": { "name": "write_file", "arguments": "path=b.txt" } } ] }
            ```
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().BeEmpty();
        r.Unknown.Should().ContainSingle();
        r.Unknown[0].Reason.Should().Contain("不是合法 JSON 对象");
    }

    [Fact]
    public void 顶层数组多条_逐条识别()
    {
        var text = """
            ```json
            [
              { "tool": "read_file", "args": { "path": "a.txt" } },
              { "action": "run_command", "input": { "command": "git status --short" } }
            ]
            ```
            """;

        var r = CallParser.Parse(text);

        r.Calls.Select(c => c.Tool).Should().Equal("read_file", "run_command");
        r.Calls[1].GetArgString("command").Should().Be("git status --short");
    }

    [Fact]
    public void JSON缺工具名键_进未解析并说明缺哪个键()
    {
        var text = """
            ```json
            { "path": "a.txt", "content": "x" }
            ```
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().BeEmpty();
        r.Unparsed.Should().ContainSingle();
        r.Unparsed[0].Reason.Should().Contain("缺工具名键");
    }

    // ---------- P2 标签式（样例用拼接构造，避免源码里出现成对尖括号标签） ----------

    private static string Tag(string name, params (string Key, string Value)[] args)
    {
        var lt = "<";
        var gt = ">";
        var slash = "/";
        var inner = string.Concat(args.Select(a =>
            $"{lt}parameter name=\"{a.Key}\"{gt}{a.Value}{lt}{slash}parameter{gt}"));
        return $"{lt}tool name=\"{name}\"{gt}{inner}{lt}{slash}tool{gt}";
    }

    [Fact]
    public void 标签式逐个参数_识别为一条()
    {
        var text = $"我先读文件。\n{Tag("read_file", ("path", "notes/a.txt"))}\n然后再决定。";

        var r = CallParser.Parse(text);

        r.Calls.Should().ContainSingle();
        r.Calls[0].Tool.Should().Be("read_file");
        r.Calls[0].GetArgString("path").Should().Be("notes/a.txt");
        r.Calls[0].Via.Should().Be("tag");
    }

    [Fact]
    public void 标签式_体内是JSON对象_也识别()
    {
        var lt = "<";
        var gt = ">";
        var slash = "/";
        var text = $"{lt}invoke name=\"write_file\"{gt}{{\"path\":\"c.txt\",\"content\":\"C\"}}{lt}{slash}invoke{gt}";

        var r = CallParser.Parse(text);

        r.Calls.Should().ContainSingle();
        r.Calls[0].Tool.Should().Be("write_file");
        r.Calls[0].GetArgString("content").Should().Be("C");
    }

    [Fact]
    public void 标签未闭合_原样可见并给原因()
    {
        var lt = "<";
        var text = $"先看这里：{lt}tool name=\"read_file\" 然后我就断了。";

        var r = CallParser.Parse(text);

        r.Calls.Should().BeEmpty();
        r.Unparsed.Should().ContainSingle();
        r.Unparsed[0].Reason.Should().Contain("未闭合");
    }

    // ---------- P3 行式 key=value ----------

    [Fact]
    public void 行式_引号值含空格_识别()
    {
        var text = """
            read_file path=notes/a.txt
            write_file path="out b.txt" content="hello world 两 词"
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().HaveCount(2);
        r.Calls[0].Tool.Should().Be("read_file");
        r.Calls[1].GetArgString("path").Should().Be("out b.txt");
        r.Calls[1].GetArgString("content").Should().Be("hello world 两 词");
        r.Calls[1].Via.Should().Be("kv");
    }

    [Fact]
    public void 行式_大小写与连字符差异_归一()
    {
        var text = """
            Read_File path=a.txt
            WRITE-FILE path=b.txt content=B
            """;

        var r = CallParser.Parse(text);

        r.Calls.Select(c => c.Tool).Should().Equal("read_file", "write_file");
    }

    [Fact]
    public void 行式_引号不成对_判不可解析不误吞()
    {
        var text = """
            write_file path="broken content="x
            read_file path=a.txt
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().ContainSingle("引号不成对的那行必须被拒，而不是猜一个参数值");
        r.Calls[0].Tool.Should().Be("read_file");
        r.Unparsed.Should().NotBeEmpty();
    }

    // ---------- P4 箭头式 ----------

    [Fact]
    public void 箭头式_单动作行_绑首个必填参数()
    {
        var text = """
            > exec: dotnet --version
            > read: notes/a.txt
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().HaveCount(2);
        r.Calls[0].Tool.Should().Be("run_command");
        r.Calls[0].GetArgString("command").Should().Be("dotnet --version");
        r.Calls[0].Via.Should().Be("arrow");
        r.Calls[1].Tool.Should().Be("read_file");
        r.Calls[1].GetArgString("path").Should().Be("notes/a.txt");
    }

    // ---------- BC-12 混档 + 不猜原则 ----------

    [Fact]
    public void 同一段里JSON与行式混排_各自独立成条_BC12()
    {
        var text = """
            ```json
            { "tool": "read_file", "args": { "path": "a.txt" } }
            ```
            run_command command="git status"
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().HaveCount(2);
        r.Calls.Select(c => c.Via).Should().Equal("json", "kv");
    }

    [Fact]
    public void 纯自然语言_零调用_带原因_BC1()
    {
        var text = "我认为应该先看看目录结构，然后再写一个测试文件，最后运行构建验证。";

        var r = CallParser.Parse(text);

        r.Calls.Should().BeEmpty();
        r.Unknown.Should().BeEmpty();
        r.Unparsed.Should().ContainSingle();
        r.Unparsed[0].Reason.Should().Contain("未找到结构化调用字段");
    }

    [Fact]
    public void 散文里出现裸工具名_提示缺参数结构_但不生成调用()
    {
        var text = "接下来我打算 write_file 一下，把结果存起来。";

        var r = CallParser.Parse(text);

        r.Calls.Should().BeEmpty("光有工具名没有参数结构，绝不推断出一条调用");
        r.Unparsed.Should().ContainSingle();
        r.Unparsed[0].Reason.Should().Contain("疑似工具名").And.Contain("缺参数结构");
    }

    [Fact]
    public void 未知工具名_归unknown_绝不就近执行_AC5()
    {
        var text = """
            ```json
            { "tool": "delete_file", "args": { "path": "a.txt" } }
            ```
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().BeEmpty("清单外的名字绝不能被执行，也不能挑一个'最像的'替 AI 跑了");
        r.Unknown.Should().ContainSingle();
        r.Unknown[0].RawName.Should().Be("delete_file");
        r.Unknown[0].Reason.Should().Contain("不做模糊匹配");
    }

    [Fact]
    public void 拼错的工具名_给候选提示但仍然不执行()
    {
        // 候选提示只是给人看的线索（编辑距离 ≤3 才给），执行面必须保持空。
        var text = """
            ```json
            { "tool": "read_flie", "args": { "path": "a.txt" } }
            ```
            """;

        var r = CallParser.Parse(text);

        r.Calls.Should().BeEmpty();
        r.Unknown.Should().ContainSingle();
        r.Unknown[0].Suggestion.Should().Be("read_file");
    }

    [Fact]
    public void 空输入_不抛异常_给出引导()
    {
        var r = CallParser.Parse("   ");

        r.Calls.Should().BeEmpty();
        r.Unparsed.Should().ContainSingle();
        r.Unparsed[0].Reason.Should().Contain("输入为空");
    }

    [Fact]
    public void 解析全程零文件系统副作用_AC3()
    {
        var mixed = $$"""
            说明文字，含裸词 read_file 但没有参数。
            ```json
            { "tool_calls": [ { "function": { "name": "list_dir", "arguments": { "path": "." } } } ] }
            ```
            run_command command="git --version"
            """;

        AssertNoFileSystemSideEffects(() =>
        {
            var r = CallParser.Parse(mixed);
            r.Calls.Should().HaveCount(2);
        });
    }

    [Fact]
    public void 解析结果统计与三段条数一致()
    {
        var text = """
            ```json
            { "tool": "read_file", "args": { "path": "a.txt" } }
            { "tool": "no_such_tool", "args": {} }
            ```
            """;

        var r = CallParser.Parse(text);

        r.Stats.Recognized.Should().Be(r.Calls.Count);
        r.Stats.Unknown.Should().Be(r.Unknown.Count);
        r.Stats.Unparsed.Should().Be(r.Unparsed.Count);
    }
}
