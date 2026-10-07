using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeSelf.Api.Plugins.ToolBridge.Models;
using ForgeSelf.Api.Plugins.ToolBridge.Services;

namespace ForgeSelf.Api.Tests.Plugins.ToolBridgeTests;

/// <summary>
/// 测试scratch目录：**只创建、只使用、永不删除**（plugin-development 铁律 10）。
/// 落在 %TEMP% 下带类名+随机后缀的隔离目录（跑测前按 AGENTS.md §5.0 把 TEMP 指进仓库 .temp/tmp）。
/// </summary>
internal static class Scratch
{
    public static string NewDir(string tag)
    {
        var dir = Path.Combine(Path.GetTempPath(), "forgeself-toolbridge-tests", $"{tag}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>工作根放在 scratch 内层（外层含 .git 检查用例外不触碰），并返回可直接用的 SandboxRoot。</summary>
    public static SandboxRoot NewSandbox(string tag, out string dataDir)
    {
        dataDir = Path.Combine(Scratch.NewDir(tag), "data");
        var sandbox = new SandboxRoot(dataDir);
        Directory.CreateDirectory(sandbox.DefaultRoot);
        return sandbox;
    }

    public static ParsedCall Call(string tool, params (string Key, string Value)[] args)
    {
        var dict = new Dictionary<string, JsonNode>();
        foreach (var (key, value) in args)
        {
            dict[key] = JsonValue.Create(value)!;
        }
        return new ParsedCall { Tool = tool, RawName = tool, Args = dict, Via = "test" };
    }
}

/// <summary>
/// AC6 / AC7 / AC8 + BC-4~7：四个工具的真实执行与守卫。
/// 断言全部对着**真实文件系统与真实子进程**，不用替身。
/// </summary>
public class ExecutorTests
{
    // ---------- read_file ----------

    [Fact]
    public void 读文件_返回原文与相对路径()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);
        var root = sandbox.Current().Root;
        Directory.CreateDirectory(Path.Combine(root, "notes"));
        File.WriteAllText(Path.Combine(root, "notes", "a.txt"), "第一行\n第二行 中文", new System.Text.UTF8Encoding(false));

        var r = FileExecutor.Read(sandbox, "notes/a.txt");

        r.Ok.Should().BeTrue();
        var obj = r.Result!.AsObject();
        obj["path"]!.GetValue<string>().Should().Be("notes/a.txt");
        obj["content"]!.GetValue<string>().Should().Be("第一行\n第二行 中文");
        obj["bytes"]!.GetValue<long>().Should().Be(System.Text.Encoding.UTF8.GetByteCount("第一行\n第二行 中文"));
    }

    [Fact]
    public void 读不存在的文件_显式not_found_不返回空串冒充_BC5()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        var r = FileExecutor.Read(sandbox, "nope.txt");

        r.Ok.Should().BeFalse();
        r.Error.Should().Be("not_found");
        r.Result.Should().BeNull("缺文件时绝不能给出 content:\"\" 让 AI 误读成空文件");
    }

    [Fact]
    public void 读目录_is_a_directory并建议改用list_dir()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);
        Directory.CreateDirectory(Path.Combine(sandbox.Current().Root, "sub"));

        var r = FileExecutor.Read(sandbox, "sub");

        r.Ok.Should().BeFalse();
        r.Error.Should().Be("is_a_directory");
        r.Reason.Should().Contain("list_dir");
    }

    [Fact]
    public void 超大文件_截断并带原长()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);
        var big = new string('x', ToolSpec.MaxFileBytes + 4096);
        File.WriteAllText(Path.Combine(sandbox.Current().Root, "big.txt"), big);

        var r = FileExecutor.Read(sandbox, "big.txt");

        r.Ok.Should().BeTrue();
        r.Truncated.Should().BeTrue();
        r.OriginalBytes.Should().Be(big.Length); // ASCII ⇒ 字节数==字符数
        r.Result!.AsObject()["truncatedNote"]!.GetValue<string>().Should().Contain("已截断");
    }

    [Fact]
    public void 含NUL的文件_标binarySuspect不猜编码_BC4()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);
        File.WriteAllBytes(Path.Combine(sandbox.Current().Root, "bin.dat"), new byte[] { 1, 2, 0, 3, 4 });

        var r = FileExecutor.Read(sandbox, "bin.dat");

        r.Ok.Should().BeTrue();
        r.Result!.AsObject()["binarySuspect"].Should().NotBeNull();
        r.Result!.AsObject()["binarySuspect"]!.GetValue<bool>().Should().BeTrue();
    }

    // ---------- write_file ----------

    [Fact]
    public void 写文件_建父目录_覆盖并回报字节()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        var first = FileExecutor.Write(sandbox, "out/deep/a.md", "AAA", false);
        var second = FileExecutor.Write(sandbox, "out/deep/a.md", "BB", false);

        first.Ok.Should().BeTrue();
        first.Result!.AsObject()["bytesWritten"]!.GetValue<int>().Should().Be(3);
        File.Exists(Path.Combine(sandbox.Current().Root, "out", "deep", "a.md")).Should().BeTrue();
        second.Result!.AsObject()["totalBytes"]!.GetValue<long>().Should().Be(2);
        File.ReadAllText(Path.Combine(sandbox.Current().Root, "out", "deep", "a.md")).Should().Be("BB");
    }

    [Fact]
    public void 写文件_append为真时追加()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);
        FileExecutor.Write(sandbox, "log.txt", "一\n", false);

        var appended = FileExecutor.Write(sandbox, "log.txt", "二\n", true);

        appended.Ok.Should().BeTrue();
        appended.Result!.AsObject()["append"]!.GetValue<bool>().Should().BeTrue();
        File.ReadAllText(Path.Combine(sandbox.Current().Root, "log.txt")).Should().Be("一\n二\n");
    }

    [Fact]
    public void 写空串是合法的空文件()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        var r = FileExecutor.Write(sandbox, "empty.txt", "", false);

        r.Ok.Should().BeTrue();
        new FileInfo(Path.Combine(sandbox.Current().Root, "empty.txt")).Length.Should().Be(0);
    }

    [Fact]
    public void 写内容超限_content_too_large且不落盘()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);
        var huge = new string('y', ToolSpec.MaxFileBytes + 1);

        var r = FileExecutor.Write(sandbox, "too-big.txt", huge, false);

        r.Ok.Should().BeFalse();
        r.Error.Should().Be("content_too_large");
        File.Exists(Path.Combine(sandbox.Current().Root, "too-big.txt")).Should().BeFalse();
    }

    // ---------- list_dir ----------

    [Fact]
    public void 列目录_目录优先_名字序_给出相对路径()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);
        var root = sandbox.Current().Root;
        Directory.CreateDirectory(Path.Combine(root, "zeta"));
        File.WriteAllText(Path.Combine(root, "beta.txt"), "b");
        File.WriteAllText(Path.Combine(root, "alpha.txt"), "aa");

        var r = FileExecutor.ListDir(sandbox, "");

        r.Ok.Should().BeTrue();
        var entries = r.Result!.AsObject()["entries"]!.AsArray();
        entries.Should().HaveCount(3);
        entries[0]["name"]!.GetValue<string>().Should().Be("zeta");
        entries[1]["name"]!.GetValue<string>().Should().Be("alpha.txt");
        entries[2]["name"]!.GetValue<string>().Should().Be("beta.txt");
        entries[0]["relativePath"]!.GetValue<string>().Should().Be("zeta");
        entries[1]["size"]!.GetValue<long>().Should().Be(2);
    }

    [Fact]
    public void 列不存在的目录_not_found()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        var r = FileExecutor.ListDir(sandbox, "ghost");

        r.Ok.Should().BeFalse();
        r.Error.Should().Be("not_found");
    }

    // ---------- 越界（AC6 三形态）----------

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("sub/../../outside.txt")]
    public void 相对路径越界_一律拒绝(string path)
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        var r = FileExecutor.Write(sandbox, path, "x", false);

        r.Ok.Should().BeFalse();
        r.Error.Should().Be("outside_workspace");
        r.Reason.Should().NotBeNullOrWhiteSpace();
        File.Exists(Path.Combine(Path.GetDirectoryName(sandbox.Current().Root)!, "outside.txt")).Should().BeFalse();
    }

    [Theory]
    [InlineData(@"D:\Windows\win.ini")]
    [InlineData("/etc/passwd")]
    public void 绝对路径_一律拒绝(string path)
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        var r = FileExecutor.Read(sandbox, path);

        r.Ok.Should().BeFalse();
        r.Error.Should().Be("outside_workspace");
        r.Reason.Should().Contain("绝对路径");
    }

    // ---------- run_command（AC7 / AC8）----------

    [Fact]
    public async Task 白名单内命令_真起进程_回exitCode与stdout原文()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        var r = await CommandExecutor.RunAsync(sandbox, "git --version", null, null);

        r.Ok.Should().BeTrue();
        var obj = r.Result!.AsObject();
        obj["exitCode"]!.GetValue<int>().Should().Be(0);
        obj["stdout"]!.GetValue<string>().Should().Contain("git version");
        r.Truncated.Should().BeFalse();
    }

    [Fact]
    public async Task 非零退出码不是错误_ok为真并带exitCode_AC8()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        var r = await CommandExecutor.RunAsync(sandbox, "git rev-parse --verify e2e-definitely-missing-ref", null, null);

        r.Ok.Should().BeTrue("失败输出同样是有效观察数据，AI 需要看到它");
        var obj = r.Result!.AsObject();
        obj["exitCode"]!.GetValue<int>().Should().NotBe(0);
        obj["stderr"]!.GetValue<string>().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task 白名单外命令被拒_并且未启动任何子进程_AC7()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);
        var root = sandbox.Current().Root;
        // 反向证明的前提：真放一个探针文件进去。若守卫失效、rm 真跑了，文件就没了。
        var probe = Path.Combine(root, "probe-must-survive.txt");
        File.WriteAllText(probe, "still here");

        var rejected = await CommandExecutor.RunAsync(sandbox, "rm -f probe-must-survive.txt", null, null);

        rejected.Ok.Should().BeFalse();
        rejected.Error.Should().Be("command_rejected");
        rejected.Reason.Should().Contain("allowlist");
        File.Exists(probe).Should().BeTrue("被拒路径必须零子进程——探针文件还在即为证据");
        File.ReadAllText(probe).Should().Be("still here");

        // 阳性对照：同一沙箱里白名单命令确实起了进程，上面的"文件还在"不是假绿。
        var control = await CommandExecutor.RunAsync(sandbox, "git --version", null, null);
        control.Ok.Should().BeTrue();
    }

    [Fact]
    public async Task 内联破坏命令被拒_原因原文回给AI_AC7()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        var r = await CommandExecutor.RunAsync(sandbox, "pwsh -Command Remove-Item x.txt", null, null);

        r.Ok.Should().BeFalse();
        r.Error.Should().Be("command_rejected");
        r.Reason.Should().Contain("destructive command not allowed");
    }

    [Fact]
    public async Task cwd越界_拒绝且不进进程()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        var r = await CommandExecutor.RunAsync(sandbox, "git --version", "../outside-dir", null);

        r.Ok.Should().BeFalse();
        r.Error.Should().Be("outside_workspace");
    }

    [Fact]
    public async Task 超时_不Kill进程_exitCode为负一并标timedOut_BC7()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        // pwsh 的 Start-Sleep 不在破坏词表内 ⇒ 过守卫；给 1s 超时而命令要睡 30s。
        var r = await CommandExecutor.RunAsync(sandbox, "pwsh -Command Start-Sleep 30", null, 1);

        r.Ok.Should().BeFalse();
        r.Error.Should().Be("timeout");
        var obj = r.Result!.AsObject();
        obj["exitCode"]!.GetValue<int>().Should().Be(-1);
        obj["timedOut"]!.GetValue<bool>().Should().BeTrue();
        r.Reason.Should().Contain("continues in background");
    }

    [Fact]
    public async Task 输出超过50KB_标truncated并截到上限_NFR2()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        // 用 node 打印 60000 个字符（内联内容不含破坏词 ⇒ 过守卫），稳定超 50KB 上限。
        var r = await CommandExecutor.RunAsync(sandbox, "node -e \"console.log('x'.repeat(60000))\"", null, 20);

        r.Ok.Should().BeTrue();
        r.Truncated.Should().BeTrue();
        var obj = r.Result!.AsObject();
        obj["stdoutTruncated"]!.GetValue<bool>().Should().BeTrue();
        System.Text.Encoding.UTF8.GetByteCount(obj["stdout"]!.GetValue<string>())
            .Should().BeLessThanOrEqualTo(CommandGuard.MaxOutputBytes);
    }

    // ---------- ToolDispatcher ----------

    [Fact]
    public async Task 缺必填参数_报missing_argument并把schema回给AI()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);

        var r = await ToolDispatcher.DispatchAsync(Scratch.Call("read_file"), sandbox);

        r.Ok.Should().BeFalse();
        r.Error.Should().Be("missing_argument: path");
        r.Reason.Should().Contain("path");
    }

    [Fact]
    public async Task 清单外工具名_报unknown_tool并列出可用名_绝不执行()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);
        var call = new ParsedCall { Tool = "delete_everything", RawName = "delete_everything", Via = "test" };

        var r = await ToolDispatcher.DispatchAsync(call, sandbox);

        r.Ok.Should().BeFalse();
        r.Error.Should().Be("unknown_tool");
        r.Reason.Should().Contain("read_file");
        Directory.GetFiles(sandbox.Current().Root).Should().BeEmpty("未知工具不得产生任何文件系统动作");
    }

    [Fact]
    public async Task 别名进来的调用_执行时按规范名分派()
    {
        var sandbox = Scratch.NewSandbox(nameof(ExecutorTests), out _);
        File.WriteAllText(Path.Combine(sandbox.Current().Root, "a.txt"), "A");

        var r = await ToolDispatcher.DispatchAsync(
            new ParsedCall { Tool = "read_file", RawName = "Read_File", Args = new Dictionary<string, JsonNode> { ["path"] = JsonValue.Create("a.txt")! } },
            sandbox);

        r.Ok.Should().BeTrue();
        r.Tool.Should().Be("read_file");
    }
}
