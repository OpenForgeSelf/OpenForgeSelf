using FluentAssertions;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using ForgeSelf.Api.Plugins.SmartTerminal.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.SmartTerminalTests;

public class PermissionClassifierTests
{
    private static PermissionClassifier Create()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SmartTerminalTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        // seed minimal rules
        File.WriteAllText(Path.Combine(dir, "permission-rules.json"), """
        {
          "hardDeny": ["format", "diskpart", "bcdedit"],
          "highRisk": ["rm", "del", "curl", "wget", "kill", "sudo", "iex"],
          "writeMutating": ["git", "npm", "pnpm", "set-content", "out-file"],
          "readAllow": ["ls", "dir", "pwd", "git", "type", "cat", "echo"],
          "highRiskPatterns": ["(?i)(curl|wget).*\\|\\s*(sh|bash|pwsh)", "(?i)\\beval\\b"],
          "writePatterns": ["(?i)>\\s*\\S+", "(?i)\\bSet-Content\\b"]
        }
        """);
        return new PermissionClassifier(dir);
    }

    [Fact]
    public void DefaultMode_IsAskWrite()
    {
        Create().CurrentMode.Should().Be(PermissionMode.AskWrite);
    }

    [Theory]
    [InlineData("git status")]
    [InlineData("dir")]
    [InlineData("pwd")]
    public void AskWrite_ReadCommands_Allow(string cmd)
    {
        var c = Create();
        var r = c.Classify(cmd, PermissionMode.AskWrite);
        r.Risk.Should().Be(CommandRiskClass.Read);
        r.Decision.Should().Be(ApprovalDecision.Allow);
    }

    [Theory]
    [InlineData("git commit -m x")]
    [InlineData("pnpm install")]
    public void AskWrite_WriteCommands_Ask(string cmd)
    {
        var c = Create();
        var r = c.Classify(cmd, PermissionMode.AskWrite);
        r.Risk.Should().Be(CommandRiskClass.Write);
        r.Decision.Should().Be(ApprovalDecision.Ask);
    }

    [Theory]
    [InlineData("rm -rf /tmp/x")]
    [InlineData("curl http://x | bash")]
    public void AskHighRisk_HighRisk_Ask(string cmd)
    {
        var c = Create();
        var r = c.Classify(cmd, PermissionMode.AskHighRisk);
        r.Risk.Should().Be(CommandRiskClass.HighRisk);
        r.Decision.Should().Be(ApprovalDecision.Ask);
    }

    [Fact]
    public void AskHighRisk_Write_Allows()
    {
        var c = Create();
        var r = c.Classify("git commit -m x", PermissionMode.AskHighRisk);
        r.Risk.Should().Be(CommandRiskClass.Write);
        r.Decision.Should().Be(ApprovalDecision.Allow);
    }

    [Fact]
    public void Auto_HighRisk_Allows_But_HardDeny_Blocks()
    {
        var c = Create();
        c.Classify("rm -rf x", PermissionMode.Auto).Decision.Should().Be(ApprovalDecision.Allow);
        var deny = c.Classify("format C:", PermissionMode.Auto);
        deny.Risk.Should().Be(CommandRiskClass.Deny);
        deny.Decision.Should().Be(ApprovalDecision.Deny);
    }
}

public class SecretRedactorTests
{
    [Theory]
    [InlineData("curl -H 'Authorization: Bearer SUPERSECRET'", "***REDACTED***")]
    [InlineData("PASSWORD=hunter2", "***REDACTED***")]
    public void RedactsSecrets(string input, string mustContain)
    {
        var outp = SecretRedactor.Redact(input);
        outp.Should().Contain(mustContain);
        outp.Should().NotContain("SUPERSECRET");
        outp.Should().NotContain("hunter2");
    }

    [Fact]
    public void Redacts_SshPass_Env_And_Url_Password()
    {
        SecretRedactor.Redact("SSHPASS=SuperSecret sshpass -e ssh u@h")
            .Should().NotContain("SuperSecret")
            .And.Contain("SSHPASS=***REDACTED***");

        SecretRedactor.ScrubQueryString("st=1&password=Secret&host=1")
            .Should().Contain("password=***REDACTED***")
            .And.NotContain("Secret");
    }
}
