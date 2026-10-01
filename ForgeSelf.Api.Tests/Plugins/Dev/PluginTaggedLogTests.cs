using ForgeSelf.Api.Plugins.Dev;
using NewLife.Log;

namespace ForgeSelf.Api.Tests.Plugins.Dev;

/// <summary>
/// PluginTaggedLog 装饰器测试：作用域命中加前缀、未命中零改动、成员透传。
/// 用内存假 ILog 断言收到的 format 与 Level 透传。
/// </summary>
public class PluginTaggedLogTests
{
    /// <summary>记录调用参数的假 ILog（实现 NewLife ILog 全部成员）。</summary>
    private sealed class FakeLog : ILog
    {
        public bool Enable { get; set; } = true;
        public LogLevel Level { get; set; } = LogLevel.Info;

        public string? LastFormat { get; private set; }
        public LogLevel? LastWriteLevel { get; private set; }

        public void Debug(string format, params object?[] args) => LastFormat = format;
        public void Info(string format, params object?[] args) => LastFormat = format;
        public void Warn(string format, params object?[] args) => LastFormat = format;
        public void Error(string format, params object?[] args) => LastFormat = format;
        public void Fatal(string format, params object?[] args) => LastFormat = format;
        public void Write(LogLevel level, string format, params object?[] args)
        {
            LastWriteLevel = level;
            LastFormat = format;
        }
    }

    [Fact]
    public void InScope_PrefixesFormatWithPluginId()
    {
        var fake = new FakeLog();
        var tagged = new PluginTaggedLog(fake);

        using (PluginLogScope.Push("sems"))
        {
            tagged.Info("初始化插件: {0}", "sems");
        }

        fake.LastFormat.Should().Be("[plugin:sems] 初始化插件: {0}");
    }

    [Fact]
    public void OutOfScope_FormatUntouched()
    {
        var fake = new FakeLog();
        var tagged = new PluginTaggedLog(fake);

        tagged.Info("宿主日志");

        fake.LastFormat.Should().Be("宿主日志");
    }

    [Fact]
    public void NestedScope_InnerWins()
    {
        var fake = new FakeLog();
        var tagged = new PluginTaggedLog(fake);

        using (PluginLogScope.Push("outer"))
        {
            using (PluginLogScope.Push("inner"))
            {
                tagged.Debug("x");
            }

            tagged.Debug("y");
        }

        fake.LastFormat.Should().Be("[plugin:outer] y");
    }

    [Fact]
    public void Level_And_Enable_PassThrough()
    {
        var fake = new FakeLog { Level = LogLevel.Info, Enable = true };
        var tagged = new PluginTaggedLog(fake);

        tagged.Level = LogLevel.Debug;
        tagged.Enable = false;

        fake.Level.Should().Be(LogLevel.Debug);
        fake.Enable.Should().BeFalse();
    }

    [Fact]
    public void Write_PassesLevelAndTaggedFormat()
    {
        var fake = new FakeLog();
        var tagged = new PluginTaggedLog(fake);

        using (PluginLogScope.Push("file-tools"))
        {
            tagged.Write(LogLevel.Warn, "文件被锁: {0}", "a.dll");
        }

        fake.LastWriteLevel.Should().Be(LogLevel.Warn);
        fake.LastFormat.Should().Be("[plugin:file-tools] 文件被锁: {0}");
    }
}
