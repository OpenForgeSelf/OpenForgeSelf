using ForgeSelf.Api.Plugins.Dev;

namespace ForgeSelf.Api.Tests.Plugins.Dev;

/// <summary>
/// DevMode 总闸判定矩阵（真实 env 驱动；每个用例自带清理，避免污染并行测试）。
/// </summary>
public class DevModeTests : IDisposable
{
    private readonly List<string> _setEnvNames = [];

    public void Dispose()
    {
        foreach (var name in _setEnvNames)
            Environment.SetEnvironmentVariable(name, null);
    }

    private void SetEnv(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value);
        if (!_setEnvNames.Contains(name)) _setEnvNames.Add(name);
    }

    [Fact]
    public void Initialize_NoEnvNoCli_Disabled()
    {
        DevMode.Initialize([]);

        DevMode.Enabled.Should().BeFalse();
        DevMode.ShadowCopyEnabled.Should().BeFalse();
        DevMode.WebSrcEnabled.Should().BeFalse();
        DevMode.DiagnosticsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Initialize_EnvOne_EnabledWithDefaultSubSwitches()
    {
        SetEnv(DevMode.EnvName, "1");

        DevMode.Initialize([]);

        DevMode.Enabled.Should().BeTrue();
        // 子开关默认随总闸
        DevMode.ShadowCopyEnabled.Should().BeTrue();
        DevMode.WebSrcEnabled.Should().BeTrue();
        DevMode.DiagnosticsEnabled.Should().BeTrue();
        // HMR 例外：默认关，显式 =1 才开
        DevMode.WebHmrEnabled.Should().BeFalse();
    }

    [Fact]
    public void Initialize_CliDevFlag_Enabled()
    {
        DevMode.Initialize(["--dev"]);

        DevMode.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Initialize_SubSwitchExplicitZero_Off()
    {
        SetEnv(DevMode.EnvName, "1");
        SetEnv("FORGESELF_DEV_SHADOWCOPY", "0");

        DevMode.Initialize([]);

        DevMode.Enabled.Should().BeTrue();
        DevMode.ShadowCopyEnabled.Should().BeFalse();
        DevMode.WebSrcEnabled.Should().BeTrue();
    }

    [Fact]
    public void Initialize_WebHmrExplicitOne_On()
    {
        SetEnv(DevMode.EnvName, "1");
        SetEnv("FORGESELF_DEV_WEB_HMR", "1");

        DevMode.Initialize([]);

        DevMode.WebHmrEnabled.Should().BeTrue();
    }

    [Fact]
    public void Initialize_PluginsDirCli_WinsOverEnv()
    {
        SetEnv(DevMode.EnvPluginsDir, "D:/from-env");

        DevMode.Initialize(["--plugins-dir=D:/from-cli"]);

        DevMode.PluginsDirectoryOverride.Should().Be("D:/from-cli");
    }

    [Fact]
    public void Initialize_PluginsDirEnvOnly_UsedAsFallback()
    {
        SetEnv(DevMode.EnvPluginsDir, "D:/from-env");

        DevMode.Initialize([]);

        DevMode.PluginsDirectoryOverride.Should().Be("D:/from-env");
    }

    [Fact]
    public void Initialize_NoOverride_Null()
    {
        DevMode.Initialize([]);

        DevMode.PluginsDirectoryOverride.Should().BeNull();
    }

    [Fact]
    public void Initialize_BuildConfigDefaults_DebugAndNet10()
    {
        DevMode.Initialize([]);

        DevMode.BuildConfiguration.Should().Be("Debug");
        DevMode.TargetFramework.Should().Be("net10.0");
    }
}
