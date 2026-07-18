using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Backend.Plugins.Abstractions;

namespace OpenForgeSelf.Backend.Tests.Plugins;

public class PermissionCheckerTests
{
    private readonly DefaultPermissionChecker _checker;

    public PermissionCheckerTests()
    {
        _checker = new DefaultPermissionChecker();
    }

    [Fact]
    public void HasPermission_NoPermissionsGranted_ReturnsFalse()
    {
        var result = _checker.HasPermission("test.plugin", PluginPermission.FileSystem);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasPermission_PermissionGranted_ReturnsTrue()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.FileSystem);

        var result = _checker.HasPermission("test.plugin", PluginPermission.FileSystem);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_DifferentPermission_ReturnsFalse()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.FileSystem);

        var result = _checker.HasPermission("test.plugin", PluginPermission.Network);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasPermission_MultiplePermissionsGranted_SingleCheck_ReturnsTrue()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.FileSystem | PluginPermission.Network);

        _checker.HasPermission("test.plugin", PluginPermission.FileSystem).Should().BeTrue();
        _checker.HasPermission("test.plugin", PluginPermission.Network).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_CombinedPermissionCheck_HasAll_ReturnsTrue()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.FileSystem | PluginPermission.Network | PluginPermission.Database);

        var result = _checker.HasPermission("test.plugin", PluginPermission.FileSystem | PluginPermission.Network);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_CombinedPermissionCheck_MissingOne_ReturnsFalse()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.FileSystem);

        var result = _checker.HasPermission("test.plugin", PluginPermission.FileSystem | PluginPermission.Network);

        result.Should().BeFalse();
    }

    [Fact]
    public void GrantPermission_SinglePermission_GrantsPermission()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.FileSystem);

        var perms = _checker.GetPermissions("test.plugin");
        perms.Should().Be(PluginPermission.FileSystem);
    }

    [Fact]
    public void GrantPermission_MultipleTimes_CombinesPermissions()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.FileSystem);
        _checker.GrantPermission("test.plugin", PluginPermission.Network);

        var perms = _checker.GetPermissions("test.plugin");
        perms.Should().Be(PluginPermission.FileSystem | PluginPermission.Network);
    }

    [Fact]
    public void GrantPermission_DuplicatePermission_DoesNotThrow()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.FileSystem);

        var action = () => _checker.GrantPermission("test.plugin", PluginPermission.FileSystem);

        action.Should().NotThrow();
        _checker.GetPermissions("test.plugin").Should().Be(PluginPermission.FileSystem);
    }

    [Fact]
    public void GrantPermission_MultiplePlugins_IndependentPermissions()
    {
        _checker.GrantPermission("plugin.a", PluginPermission.FileSystem);
        _checker.GrantPermission("plugin.b", PluginPermission.Network);

        _checker.GetPermissions("plugin.a").Should().Be(PluginPermission.FileSystem);
        _checker.GetPermissions("plugin.b").Should().Be(PluginPermission.Network);
    }

    [Fact]
    public void RevokePermission_ExistingPermission_RevokesPermission()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.FileSystem);

        _checker.RevokePermission("test.plugin", PluginPermission.FileSystem);

        _checker.HasPermission("test.plugin", PluginPermission.FileSystem).Should().BeFalse();
        _checker.GetPermissions("test.plugin").Should().Be(PluginPermission.None);
    }

    [Fact]
    public void RevokePermission_OneOfMultiplePermissions_OnlyRevokesSpecified()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.FileSystem | PluginPermission.Network | PluginPermission.Database);

        _checker.RevokePermission("test.plugin", PluginPermission.Network);

        _checker.HasPermission("test.plugin", PluginPermission.FileSystem).Should().BeTrue();
        _checker.HasPermission("test.plugin", PluginPermission.Network).Should().BeFalse();
        _checker.HasPermission("test.plugin", PluginPermission.Database).Should().BeTrue();
    }

    [Fact]
    public void RevokePermission_NonExistingPermission_DoesNotThrow()
    {
        var action = () => _checker.RevokePermission("test.plugin", PluginPermission.FileSystem);

        action.Should().NotThrow();
    }

    [Fact]
    public void RevokePermission_NonExistingPlugin_DoesNotThrow()
    {
        var action = () => _checker.RevokePermission("nonexistent", PluginPermission.FileSystem);

        action.Should().NotThrow();
    }

    [Fact]
    public void GetPermissions_NoPermissionsGranted_ReturnsNone()
    {
        var perms = _checker.GetPermissions("test.plugin");

        perms.Should().Be(PluginPermission.None);
    }

    [Fact]
    public void GetPermissions_SinglePermission_ReturnsPermission()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.FileSystem);

        var perms = _checker.GetPermissions("test.plugin");

        perms.Should().Be(PluginPermission.FileSystem);
    }

    [Fact]
    public void GetPermissions_AllPermissions_ReturnsAll()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.All);

        var perms = _checker.GetPermissions("test.plugin");

        perms.Should().Be(PluginPermission.All);
    }

    [Fact]
    public void HasPermission_NonePermission_NoPermissions_ReturnsTrue()
    {
        var result = _checker.HasPermission("test.plugin", PluginPermission.None);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_NonePermission_WithPermissions_ReturnsTrue()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.FileSystem);

        var result = _checker.HasPermission("test.plugin", PluginPermission.None);

        result.Should().BeTrue();
    }

    [Fact]
    public void GrantPermission_AllPermission_GrantsAll()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.All);

        _checker.HasPermission("test.plugin", PluginPermission.FileSystem).Should().BeTrue();
        _checker.HasPermission("test.plugin", PluginPermission.Network).Should().BeTrue();
        _checker.HasPermission("test.plugin", PluginPermission.Database).Should().BeTrue();
        _checker.HasPermission("test.plugin", PluginPermission.Configuration).Should().BeTrue();
        _checker.HasPermission("test.plugin", PluginPermission.ExtensionPoint).Should().BeTrue();
        _checker.HasPermission("test.plugin", PluginPermission.AIService).Should().BeTrue();
        _checker.HasPermission("test.plugin", PluginPermission.PluginManagement).Should().BeTrue();
    }

    [Fact]
    public void RevokePermission_AllPermissionWithAllGranted_RevokesAll()
    {
        _checker.GrantPermission("test.plugin", PluginPermission.All);

        _checker.RevokePermission("test.plugin", PluginPermission.All);

        _checker.GetPermissions("test.plugin").Should().Be(PluginPermission.None);
    }

    [Fact]
    public void PermissionEnum_HasFlagsAttribute()
    {
        var enumType = typeof(PluginPermission);
        var hasFlags = enumType.IsDefined(typeof(FlagsAttribute), false);

        hasFlags.Should().BeTrue();
    }
}
