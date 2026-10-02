using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// Agent 写开关测试（AC18）：默认允许 / Set 立即生效 / 新实例保持（模拟重启）/ 损坏只读 / 不留临时文件。
/// 纯文件操作不碰 DB，无需 [Collection("XCode")]。
/// 数据安全铁律 10：测试目录只创建、永不删除。
/// </summary>
public class AgentAccessTests
{
    static AgentAccess NewService(out String dir)
    {
        dir = Path.Combine(Path.GetTempPath(), $"ForgeSelfAccess_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return new AgentAccess(new DesignSystemPaths(dir));
    }

    [Fact]
    public void 默认允许_文件不存在时()
    {
        var svc = NewService(out _);
        var (allow, source, corrupt, updated) = svc.Get();

        allow.Should().BeTrue();
        source.Should().Be("default");
        corrupt.Should().BeFalse();
        updated.Should().BeNull();
    }

    [Fact]
    public void Set立即生效_且新实例读到同状态_模拟重启()
    {
        var svc = NewService(out var dir);
        svc.Set(false);

        var (allow, source, corrupt, _) = svc.Get();
        allow.Should().BeFalse();
        source.Should().Be("file");
        corrupt.Should().BeFalse();

        // 新实例 = 重新构造服务（模拟宿主重启后重新读文件）
        var again = new AgentAccess(new DesignSystemPaths(dir));
        again.Get().AllowWrite.Should().BeFalse();
        again.Get().Source.Should().Be("file");
    }

    [Fact]
    public void 写入不留临时文件()
    {
        var svc = NewService(out var dir);
        svc.Set(true);
        Directory.GetFiles(dir, "*.tmp").Should().BeEmpty();
        svc.Set(false);
        Directory.GetFiles(dir, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void 损坏JSON_按只读处理_failClosed()
    {
        var svc = NewService(out var dir);
        File.WriteAllText(Path.Combine(dir, "agent-access.json"), "{ not json ]");

        var (allow, source, corrupt, _) = svc.Get();
        allow.Should().BeFalse();
        source.Should().Be("file");
        corrupt.Should().BeTrue();
    }

    [Fact]
    public void 缺allowWrite字段_按只读处理()
    {
        var svc = NewService(out var dir);
        File.WriteAllText(Path.Combine(dir, "agent-access.json"), "{\"updatedAt\":\"2026-10-01T00:00:00Z\"}");

        svc.Get().AllowWrite.Should().BeFalse();
        svc.Get().Corrupt.Should().BeTrue();
    }

    [Fact]
    public void allowWrite类型不是布尔_按只读处理()
    {
        var svc = NewService(out var dir);
        File.WriteAllText(Path.Combine(dir, "agent-access.json"), "{\"allowWrite\":\"yes\"}");

        svc.Get().AllowWrite.Should().BeFalse();
        svc.Get().Corrupt.Should().BeTrue();
    }

    [Fact]
    public void 合法文件_updatedAt可回读()
    {
        var svc = NewService(out var dir);
        svc.Set(false);

        var (_, _, corrupt, updated) = svc.Get();
        corrupt.Should().BeFalse();
        updated.Should().NotBeNull();
    }
}
