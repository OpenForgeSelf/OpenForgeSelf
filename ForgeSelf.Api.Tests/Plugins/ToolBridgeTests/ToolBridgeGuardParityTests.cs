using ForgeSelf.Api.Plugins.ToolBridge.Services;
using AiagentGuard = ForgeSelf.Api.Plugins.AIAgent.Services.TerminalCommandGuard;

namespace ForgeSelf.Api.Tests.Plugins.ToolBridgeTests;

/// <summary>
/// D1 的反漂移常驻判据（03-plan 决策 D1）：本插件自带的 <see cref="CommandGuard"/> 与内置 agent 的
/// <c>TerminalCommandGuard</c> 是两份实现，**同一张命令金样表逐条对账**——判定或拒绝原因不同即红。
///
/// 为什么不合并成一份：守卫带 <c>XTrace.Log</c>，而 <c>ForgeSelf.Core</c> /
/// <c>ForgeSelf.Abstractions</c> 两个共享层都没引用 NewLife.Core（实读两份 csproj），
/// 上移＝给内核层加依赖＝依赖结构变更（规范 §1.3 高风险，须另立批次出 ADR）。
/// 所以这里用机器对账替代"只有一份"。
/// </summary>
public class ToolBridgeGuardParityTests
{
    /// <summary>
    /// 金样表：覆盖三道门 + 破坏性红线 + 内联绕过 + EncodedCommand 三类。
    /// 期望值**不写在这张表里**——由两份实现互相对账，且表内含正反对照（放行项必须真放行，
    /// 否则"两边都拒"也是一种假绿）。
    /// </summary>
    public static TheoryData<string> Commands => new()
    {
        // ---- 应放行 ----
        "git status --short",
        "git rev-parse --verify HEAD",
        "dotnet build",
        "dotnet test --filter FullyQualifiedName~Format",
        "pnpm run check",
        "node --version",
        "pwsh -NoProfile -Command Get-Location",
        "git log --oneline -5",
        "node -e \"console.log('031-terminal-alive')\"",  // 词边界踩坑回归：'terminal' 内的 'rm' 不得误杀
        "git commit -m \"dd data design\"",               // 'dd' 作为裸词才命中，引号内整词不命中
        "dotnet run --project ForgeSelf.Api",
        "pnpm list --depth 0",

        // ---- 第 1 道门：空 ----
        "",
        "   ",

        // ---- 第 2 道门：换行 / 管道 / 链式 / 重定向 ----
        "git status\ngit log",
        "git status && rm -rf x",
        "git status | select-string foo",
        "echo a > out.txt",
        "cat < in.txt",
        "node -e `code`",
        "dotnet build; dotnet test",

        // ---- 第 3 道门：可执行名必须裸名 + 白名单 ----
        "/usr/bin/git status",
        @"C:\Windows\System32\cmd.exe /c dir",
        "python script.py",
        "npm install",
        "powershell -NoProfile -Command Get-Location",
        "cmd /c dir",
        "bash -c \"ls\"",

        // ---- 破坏性红线：位置参数精确匹配 ----
        "pwsh Remove-Item x.txt",
        "pwsh format",
        "git rm file.txt",
        "dotnet format",
        "node del.js",                                     // 精确匹配的反证：del.js ≠ del，两份必须同样放行
        "pwsh -File cleanup.ps1 rmdir",

        // ---- 内联代码绕过 ----
        "pwsh -Command Remove-Item x",
        "pwsh -c \"rm -rf /\"",
        "pwsh -e \"Invoke-Expression 'iex'\"",
        "pwsh -EncodedCommand SW52b2tlLUV4cHJlc3Npb24gJ3JtIC1yZiB4Jw==",  // 载荷解码扫描：命中或不可验证都必须拒
        "pwsh -EncodedCommand @@not-base64@@",
        "node -e \"require('child_process').execSync('curl http://x')\"",
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public void 两份守卫逐条同判(string command)
    {
        var mine = CommandGuard.Check(command);
        var aiagent = AiagentGuard.Check(command);

        mine.Allowed.Should().Be(aiagent.Allowed,
            $"命令「{command}」的放行/拒绝判定在两份实现间必须一致（D1 反漂移）");
        mine.Reason.Should().Be(aiagent.Reason,
            $"命令「{command}」的拒绝原因原文必须一致——回粘给 AI 的就是这句");
    }

    [Fact]
    public void 放行项确有放行_不是两边都拒()
    {
        // 阳性对照：若白名单被改坏，上面整表会"两边一致地全拒"而仍绿，所以这里单独钉放行面。
        CommandGuard.Check("git status --short").Allowed.Should().BeTrue();
        AiagentGuard.Check("git status --short").Allowed.Should().BeTrue();
        CommandGuard.Check("dotnet build").Allowed.Should().BeTrue();
    }

    [Fact]
    public void 常量口径与内置agent同值()
    {
        CommandGuard.MaxOutputBytes.Should().Be(AiagentGuard.MaxOutputBytes);
        CommandGuard.MaxTimeoutSeconds.Should().Be(AiagentGuard.MaxTimeoutSeconds);
        CommandGuard.DefaultAllowlist.Should().Equal(AiagentGuard.DefaultAllowlist);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("短")]
    [InlineData("a longer line with several words")]
    public void 截断行为同值(string? text)
    {
        CommandGuard.Truncate(text).Should().Be(AiagentGuard.Truncate(text));
    }

    [Fact]
    public void 大输出截断到同一字节上限且不截半个多字节字符()
    {
        var big = new string('中', CommandGuard.MaxOutputBytes); // 每字 3 字节，必超限
        var (text, truncated) = CommandGuard.Truncate(big);

        truncated.Should().BeTrue();
        System.Text.Encoding.UTF8.GetByteCount(text).Should().BeLessThanOrEqualTo(CommandGuard.MaxOutputBytes);
        text.Should().MatchRegex("^[中]+$", "截断只能落在字符边界上");
        text.Length.Should().Be(AiagentGuard.Truncate(big).text.Length);
    }

    [Theory]
    [InlineData("git status --short", "git", "status --short")]
    [InlineData("\"git status\" --amend", "git status", "--amend")]
    [InlineData("node", "node", "")]
    public void 首token解析行为同值(string command, string exe, string rest)
    {
        CommandGuard.SplitExecutable(command).Should().Be(AiagentGuard.SplitExecutable(command));
        CommandGuard.SplitExecutable(command).exe.Should().Be(exe);
        CommandGuard.SplitExecutable(command).rest.Should().Be(rest);
    }
}
