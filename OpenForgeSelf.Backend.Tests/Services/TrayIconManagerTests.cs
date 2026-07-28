using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using H.NotifyIcon.Core;
using OpenForgeSelf.Backend.Services;

namespace OpenForgeSelf.Backend.Tests.Services;

/// <summary>
/// TrayIconManager 单元测试。
/// 测试范围限定在不依赖 H.NotifyIcon 实际图标创建的逻辑上：
/// 构造函数验证、Dispose、Hide 空安全、ShowBalloonTip 空安全、菜单构建逻辑。
/// Show() 及更复杂的生命周期测试需要 Windows 桌面环境
/// （H.NotifyIcon 的 TrayIcon.Create() 需要 Shell/Explorer），
/// 在单元测试环境中无法运行，需要在手动验证或 E2E 测试中覆盖。
/// </summary>
public class TrayIconManagerTests : IDisposable
{
    private readonly Mock<IServiceManager> _serviceManagerMock;
    private readonly TrayIconManager _manager;

    public TrayIconManagerTests()
    {
        _serviceManagerMock = new Mock<IServiceManager>(MockBehavior.Loose);
        _manager = new TrayIconManager(
            _serviceManagerMock.Object,
            port: 7102,
            onCheckUpdate: null,
            onExit: null);
    }

    public void Dispose()
    {
        _manager.Dispose();
        _serviceManagerMock.Reset();
    }

    // ================================================================
    // 构造函数参数验证
    // ================================================================

    [Fact]
    public void Constructor_NullServiceManager_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new TrayIconManager(null!, 7102);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serviceManager");
    }

    [Fact]
    public void Constructor_WithOnCheckUpdateAndOnExit_DoesNotThrow()
    {
        // Act
        var manager = new TrayIconManager(
            _serviceManagerMock.Object,
            port: 7102,
            onCheckUpdate: () => { },
            onExit: () => { });

        // Assert
        manager.Should().NotBeNull();
        manager.Dispose();
    }

    // ================================================================
    // Dispose() 行为
    // ================================================================

    [Fact]
    public void Dispose_WithoutShow_DoesNotThrow()
    {
        // Act
        var act = () => _manager.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        // Act
        _manager.Dispose();
        var act = () => _manager.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    // ================================================================
    // Hide() 行为 — 空安全调用
    // ================================================================

    [Fact]
    public void Hide_WithoutShow_DoesNotThrow()
    {
        // Act
        var act = () => _manager.Hide();

        // Assert
        act.Should().NotThrow();
    }

    // ================================================================
    // ShowBalloonTip() 行为 — 空安全（_trayIcon 为 null 时安全返回）
    // ================================================================

    [Fact]
    public void ShowBalloonTip_WithoutShow_DoesNotThrow()
    {
        // Act
        var act = () => _manager.ShowBalloonTip("Title", "Message");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ShowBalloonTip_WithWarningIcon_WithoutShow_DoesNotThrow()
    {
        // Act
        var act = () => _manager.ShowBalloonTip("Warning", "Something happened", ToolTipIcon.Warning);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ShowBalloonTip_WithErrorIcon_WithoutShow_DoesNotThrow()
    {
        // Act
        var act = () => _manager.ShowBalloonTip("Error", "Something went wrong", ToolTipIcon.Error);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ShowBalloonTip_AfterDispose_DoesNotThrow()
    {
        // Arrange
        _manager.Dispose();

        // Act
        var act = () => _manager.ShowBalloonTip("Title", "Message");

        // Assert
        act.Should().NotThrow();
    }

    // ================================================================
    // 菜单项动态显示逻辑（纯逻辑测试，不调用 WinForms API）
    // ================================================================

    [Fact]
    public void BuildMenu_WhenServiceNotInstalled_ContainsInstallItem()
    {
        // Arrange
        _serviceManagerMock.Setup(s => s.IsInstalled()).Returns(false);

        // Act
        var menu = InvokeBuildMenu(_manager);
        var itemTexts = GetMenuItemTexts(menu);

        // Assert
        itemTexts.Should().Contain(m => m.Contains("安装服务"));
    }

    [Fact]
    public void BuildMenu_WhenServiceInstalled_ContainsUninstallAndRestartItems()
    {
        // Arrange
        _serviceManagerMock.Setup(s => s.IsInstalled()).Returns(true);

        // Act
        var menu = InvokeBuildMenu(_manager);
        var itemTexts = GetMenuItemTexts(menu);

        // Assert
        itemTexts.Should().Contain(m => m.Contains("卸载服务"));
        itemTexts.Should().Contain(m => m.Contains("重启服务"));
    }

    [Fact]
    public void BuildMenu_WhenServiceNotInstalled_DoesNotContainUninstallOrRestartItems()
    {
        // Arrange
        _serviceManagerMock.Setup(s => s.IsInstalled()).Returns(false);

        // Act
        var menu = InvokeBuildMenu(_manager);
        var itemTexts = GetMenuItemTexts(menu);

        // Assert
        itemTexts.Should().NotContain(m => m.Contains("卸载服务"));
        itemTexts.Should().NotContain(m => m.Contains("重启服务"));
    }

    [Fact]
    public void BuildMenu_WhenServiceInstalled_DoesNotContainInstallItem()
    {
        // Arrange
        _serviceManagerMock.Setup(s => s.IsInstalled()).Returns(true);

        // Act
        var menu = InvokeBuildMenu(_manager);
        var itemTexts = GetMenuItemTexts(menu);

        // Assert
        itemTexts.Should().NotContain(m => m.Contains("安装服务"));
    }

    [Fact]
    public void BuildMenu_AlwaysContainsOpenMainPageItem()
    {
        // Arrange
        _serviceManagerMock.Setup(s => s.IsInstalled()).Returns(false);

        // Act
        var menu = InvokeBuildMenu(_manager);
        var itemTexts = GetMenuItemTexts(menu);

        // Assert
        itemTexts.Should().Contain(m => m.Contains("打开主界面"));
    }

    [Fact]
    public void BuildMenu_AlwaysContainsCheckUpdateItem()
    {
        // Arrange
        _serviceManagerMock.Setup(s => s.IsInstalled()).Returns(false);

        // Act
        var menu = InvokeBuildMenu(_manager);
        var itemTexts = GetMenuItemTexts(menu);

        // Assert
        itemTexts.Should().Contain(m => m.Contains("检查更新"));
    }

    [Fact]
    public void BuildMenu_AlwaysContainsAboutItem()
    {
        // Arrange
        _serviceManagerMock.Setup(s => s.IsInstalled()).Returns(false);

        // Act
        var menu = InvokeBuildMenu(_manager);
        var itemTexts = GetMenuItemTexts(menu);

        // Assert
        itemTexts.Should().Contain(m => m.Contains("关于"));
    }

    [Fact]
    public void BuildMenu_AlwaysContainsExitItem()
    {
        // Arrange
        _serviceManagerMock.Setup(s => s.IsInstalled()).Returns(false);

        // Act
        var menu = InvokeBuildMenu(_manager);
        var itemTexts = GetMenuItemTexts(menu);

        // Assert
        itemTexts.Should().Contain(m => m.Contains("退出"));
    }

    // ================================================================
    // 私有辅助方法（通过反射调用内部方法）
    // ================================================================

    /// <summary>
    /// 通过反射调用 <see cref="TrayIconManager"/> 的私有方法 <c>BuildMenu()</c>。
    /// 使用给定 <paramref name="manager"/> 实例，以便调用方可预先设置 mock 状态。
    /// </summary>
    private static PopupMenu InvokeBuildMenu(TrayIconManager? manager = null)
    {
        // 使用给定的 manager 实例；若未提供则创建一个临时实例（IsInstalled 默认 false）
        var instance = manager;
        if (instance == null)
        {
            var tempMock = new Mock<IServiceManager>(MockBehavior.Loose);
            tempMock.Setup(s => s.IsInstalled()).Returns(false);
            instance = new TrayIconManager(tempMock.Object, port: 7102);
        }

        var method = typeof(TrayIconManager).GetMethod("BuildMenu",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        var result = method.Invoke(instance, null);
        Assert.NotNull(result);
        return (PopupMenu)result;
    }

    /// <summary>
    /// 从 PopupMenu 中提取所有菜单项（非分隔符）的文本。
    /// </summary>
    private static List<string> GetMenuItemTexts(PopupMenu menu)
    {
        var texts = new List<string>();
        foreach (var item in menu.Items)
        {
            if (item is PopupMenuItem menuItem)
            {
                // 获取 Header 属性的值
                var headerProp = item.GetType().GetProperty("Header");
                if (headerProp != null)
                {
                    var header = headerProp.GetValue(item)?.ToString();
                    if (!string.IsNullOrEmpty(header))
                        texts.Add(header);
                }
                else
                {
                    // 回退：尝试 ToString() 或使用 Text 属性
                    var textProp = item.GetType().GetProperty("Text");
                    if (textProp != null)
                    {
                        var text = textProp.GetValue(item)?.ToString();
                        if (!string.IsNullOrEmpty(text))
                            texts.Add(text);
                    }
                }
            }
        }
        return texts;
    }

    // ================================================================
    // 说明：以下测试需要 Windows 桌面环境（Shell/Explorer），
    // 在 CI 或单元测试环境中无法运行，需在手动验证中覆盖：
    //
    // - Show() / Hide() 生命周期验证
    // - ShowBalloonTip() 在 Show() 后的实际调用
    // - 多实例独立生命周期
    //
    // Show() 调用 TrayIcon.Create() 内部会抛出异常，
    // 因此任何调用 Show() 的测试都不适合在单元测试中运行。
    // 这些测试对应 E2E 验证场景，见 quickstart.md § 托盘图标验证
    // ================================================================
}