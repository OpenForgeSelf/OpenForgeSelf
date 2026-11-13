using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.Terminal;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.SmartTerminalTests;

public class TerminalOpenParserTests
{
    [Fact]
    public void ProtocolUrl_becomes_page_query_without_password()
    {
        var parsed = TerminalOpenParser.TryParse(
        [
            "forgeself://terminal?type=ssh&host=10.0.0.1&port=22&user=ops&password=SECRET&step=uname"
        ]);

        parsed.Should().NotBeNull();
        parsed!.Password.Should().Be("SECRET");
        parsed.SecretStripped.Should().BeFalse();
        parsed.Query.Should().Contain("type=ssh");
        parsed.Query.Should().Contain("host=10.0.0.1");
        parsed.Query.Should().Contain("user=ops");
        parsed.Query.Should().Contain("step=uname");
        parsed.Query.Should().Contain("st=1");
        parsed.Query.Should().NotContain("SECRET");
        parsed.Query.Should().NotContain("password");
    }

    [Fact]
    public void OpenTerminalFlags_become_the_same_query()
    {
        var parsed = TerminalOpenParser.TryParse(
        [
            "--console",
            "--open-terminal",
            "--type", "ssh",
            "--host", "bastion.example",
            "--port", "2222",
            "--user", "root",
            "--step", "whoami"
        ]);

        parsed.Should().NotBeNull();
        parsed!.SecretStripped.Should().BeFalse();
        parsed.Query.Should().Contain("type=ssh");
        parsed.Query.Should().Contain("host=bastion.example");
        parsed.Query.Should().Contain("port=2222");
        parsed.Query.Should().Contain("user=root");
        parsed.Query.Should().Contain("step=whoami");
    }

    [Fact]
    public void Install_is_not_a_terminal_open()
    {
        TerminalOpenParser.TryParse(["install"]).Should().BeNull();
    }

    [Fact]
    public void Strip_keeps_console_and_drops_the_url()
    {
        var left = TerminalOpenParser.Strip(
        [
            "--console",
            "forgeself://terminal?type=local&shell=pwsh"
        ]);
        left.Should().Equal("--console");
    }

    [Fact]
    public void Registration_prefers_the_root_launcher()
    {
        var process = @"D:\apps\ForgeSelf\versions\2.4.22.1\ForgeSelf.exe";
        var launcher = @"D:\apps\ForgeSelf\ForgeSelf.exe";
        var chosen = ForgeselfProtocol.ResolveRegistrationExe(process, path => path == launcher);
        chosen.Should().Be(launcher);
        ForgeselfProtocol.CommandValue(chosen).Should().Be($"\"{launcher}\" \"%1\"");
    }

    [Fact]
    public void Inbox_drains_once()
    {
        TerminalOpenInbox.Clear();
        var notice = TerminalOpenInbox.Enqueue("st=1&type=local", null);
        var first = TerminalOpenInbox.Drain();
        var second = TerminalOpenInbox.Drain();
        first.Should().ContainSingle(n => n.Id == notice.Id && n.Query == "st=1&type=local");
        second.Should().BeEmpty();
    }

    [Fact]
    public void Tool_file_uses_host_scope()
    {
        var path = FindRepoFile(Path.Combine("Plugins", "SmartTerminal", "Services", "SmartTerminalToolFunctions.cs"));
        var text = File.ReadAllText(path);
        text.Should().Contain("CreateHostScope(");
        text.Should().NotContain(".CreateScope(");
        text.Should().Contain("TerminalOpenInbox.Enqueue");
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate) && File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
                return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relative);
    }
}
