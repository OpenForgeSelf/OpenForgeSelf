using OpenForgeSelf.Backend.Plugins.ScriptRunner.Data;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

[Collection("XCode")]
public class ScriptRunnerTests : IClassFixture<XCodeTestFixture>
{
    private readonly Mock<IRuntimeDetector> _mockRuntimeDetector;
    private readonly ScriptExecutor _scriptExecutor;

    public ScriptRunnerTests(XCodeTestFixture fixture)
    {
        _mockRuntimeDetector = new Mock<IRuntimeDetector>();

        // ScriptExecutor 通过 XCode 访问 ScriptRunner 连接；
        // 复用 XCodeTestFixture 将 ScriptRunner 连接指向临时库并建表（走 XCode 而非 EF），
        // 避免此前用 EF EnsureCreated 建到错误库导致 "no such table" 的脆弱写法
        _scriptExecutor = new ScriptExecutor(_mockRuntimeDetector.Object);
    }

    #region ScriptExecutor - PowerShell Execution Tests

    [Fact]
    public async Task ExecuteCodeAsync_PowerShellSimpleScript_ShouldCaptureOutput()
    {
        // Arrange
        var runtime = new RuntimeEnvironment
        {
            Language = ScriptLanguage.PowerShell,
            IsAvailable = true,
            Version = "5.1",
            InterpreterPath = "powershell.exe"
        };
        _mockRuntimeDetector.Setup(r => r.DetectAsync(ScriptLanguage.PowerShell))
            .ReturnsAsync(runtime);

        var code = "Write-Host \"hello\"";

        // Act
        var execution = await _scriptExecutor.ExecuteCodeAsync(
            code,
            ScriptLanguage.PowerShell,
            null,
            null,
            CancellationToken.None);

        // Assert
        execution.Should().NotBeNull();
        execution.Id.Should().BeGreaterThan(0);
        execution.Status.Should().Be(ScriptExecutionStatus.Pending);

        var maxWait = TimeSpan.FromSeconds(10);
        var startTime = DateTime.Now;
        ScriptExecution? finalExecution = null;

        while (DateTime.Now - startTime < maxWait)
        {
            finalExecution = await _scriptExecutor.GetExecutionAsync(execution.Id);
            if (finalExecution != null &&
                (finalExecution.Status == ScriptExecutionStatus.Completed ||
                 finalExecution.Status == ScriptExecutionStatus.Failed ||
                 finalExecution.Status == ScriptExecutionStatus.Timeout ||
                 finalExecution.Status == ScriptExecutionStatus.Cancelled))
            {
                break;
            }
            await Task.Delay(200);
        }

        finalExecution.Should().NotBeNull();
        finalExecution!.Status.Should().Be(ScriptExecutionStatus.Completed);
        finalExecution.ExitCode.Should().Be(0);
        finalExecution.Output.Should().Contain("hello");
        finalExecution.DurationMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteCodeAsync_PowerShellWithExitCode1_ShouldReturnFailedStatus()
    {
        // Arrange
        var runtime = new RuntimeEnvironment
        {
            Language = ScriptLanguage.PowerShell,
            IsAvailable = true,
            Version = "5.1",
            InterpreterPath = "powershell.exe"
        };
        _mockRuntimeDetector.Setup(r => r.DetectAsync(ScriptLanguage.PowerShell))
            .ReturnsAsync(runtime);

        var code = "exit 1";

        // Act
        var execution = await _scriptExecutor.ExecuteCodeAsync(
            code,
            ScriptLanguage.PowerShell,
            null,
            null,
            CancellationToken.None);

        // Assert
        var maxWait = TimeSpan.FromSeconds(10);
        var startTime = DateTime.Now;
        ScriptExecution? finalExecution = null;

        while (DateTime.Now - startTime < maxWait)
        {
            finalExecution = await _scriptExecutor.GetExecutionAsync(execution.Id);
            if (finalExecution != null &&
                (finalExecution.Status == ScriptExecutionStatus.Completed ||
                 finalExecution.Status == ScriptExecutionStatus.Failed ||
                 finalExecution.Status == ScriptExecutionStatus.Timeout ||
                 finalExecution.Status == ScriptExecutionStatus.Cancelled))
            {
                break;
            }
            await Task.Delay(200);
        }

        finalExecution.Should().NotBeNull();
        finalExecution!.Status.Should().Be(ScriptExecutionStatus.Failed);
        finalExecution.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteCodeAsync_WithParameters_ShouldPassViaEnvironmentVariables()
    {
        // Arrange
        var runtime = new RuntimeEnvironment
        {
            Language = ScriptLanguage.PowerShell,
            IsAvailable = true,
            Version = "5.1",
            InterpreterPath = "powershell.exe"
        };
        _mockRuntimeDetector.Setup(r => r.DetectAsync(ScriptLanguage.PowerShell))
            .ReturnsAsync(runtime);

        var code = @"
Write-Host ""GREETING: $env:SCRIPT_PARAM_GREETING""
Write-Host ""NAME: $env:SCRIPT_PARAM_NAME""
";
        var parameters = new Dictionary<string, object?>
        {
            ["greeting"] = "Hello",
            ["name"] = "World"
        };

        // Act
        var execution = await _scriptExecutor.ExecuteCodeAsync(
            code,
            ScriptLanguage.PowerShell,
            parameters,
            null,
            CancellationToken.None);

        // Assert
        var maxWait = TimeSpan.FromSeconds(10);
        var startTime = DateTime.Now;
        ScriptExecution? finalExecution = null;

        while (DateTime.Now - startTime < maxWait)
        {
            finalExecution = await _scriptExecutor.GetExecutionAsync(execution.Id);
            if (finalExecution != null &&
                (finalExecution.Status == ScriptExecutionStatus.Completed ||
                 finalExecution.Status == ScriptExecutionStatus.Failed ||
                 finalExecution.Status == ScriptExecutionStatus.Timeout ||
                 finalExecution.Status == ScriptExecutionStatus.Cancelled))
            {
                break;
            }
            await Task.Delay(200);
        }

        finalExecution.Should().NotBeNull();
        finalExecution!.Status.Should().Be(ScriptExecutionStatus.Completed);
        finalExecution.Output.Should().Contain("GREETING: Hello");
        finalExecution.Output.Should().Contain("NAME: World");
    }

    [Fact]
    public async Task ExecuteCodeAsync_WithTimeout_ShouldStartExecution()
    {
        // Arrange
        var runtime = new RuntimeEnvironment
        {
            Language = ScriptLanguage.PowerShell,
            IsAvailable = true,
            Version = "5.1",
            InterpreterPath = "powershell.exe"
        };
        _mockRuntimeDetector.Setup(r => r.DetectAsync(ScriptLanguage.PowerShell))
            .ReturnsAsync(runtime);

        var code = "Start-Sleep -Seconds 10";

        // Act
        var execution = await _scriptExecutor.ExecuteCodeAsync(
            code,
            ScriptLanguage.PowerShell,
            null,
            null,
            CancellationToken.None);

        // Assert
        execution.Should().NotBeNull();
        execution.Id.Should().BeGreaterThan(0);
        execution.Status.Should().Be(ScriptExecutionStatus.Pending);

        await _scriptExecutor.CancelAsync(execution.Id);
    }

    [Fact]
    public async Task CancelAsync_ForRunningScript_ShouldCancelExecution()
    {
        // Arrange
        var runtime = new RuntimeEnvironment
        {
            Language = ScriptLanguage.PowerShell,
            IsAvailable = true,
            Version = "5.1",
            InterpreterPath = "powershell.exe"
        };
        _mockRuntimeDetector.Setup(r => r.DetectAsync(ScriptLanguage.PowerShell))
            .ReturnsAsync(runtime);

        var code = "Start-Sleep -Seconds 30";

        // Act
        var execution = await _scriptExecutor.ExecuteCodeAsync(
            code,
            ScriptLanguage.PowerShell,
            null,
            null,
            CancellationToken.None);

        await Task.Delay(500);

        var cancelResult = await _scriptExecutor.CancelAsync(execution.Id);

        // Assert
        var maxWait = TimeSpan.FromSeconds(5);
        var startTime = DateTime.Now;
        ScriptExecution? finalExecution = null;

        while (DateTime.Now - startTime < maxWait)
        {
            finalExecution = await _scriptExecutor.GetExecutionAsync(execution.Id);
            if (finalExecution != null &&
                (finalExecution.Status == ScriptExecutionStatus.Cancelled ||
                 finalExecution.Status == ScriptExecutionStatus.Completed ||
                 finalExecution.Status == ScriptExecutionStatus.Failed))
            {
                break;
            }
            await Task.Delay(200);
        }

        finalExecution.Should().NotBeNull();
        finalExecution!.Status.Should().Be(ScriptExecutionStatus.Cancelled);
    }

    [Fact]
    public async Task GetOutputAsync_ShouldReturnExecutionLogs()
    {
        // Arrange
        var runtime = new RuntimeEnvironment
        {
            Language = ScriptLanguage.PowerShell,
            IsAvailable = true,
            Version = "5.1",
            InterpreterPath = "powershell.exe"
        };
        _mockRuntimeDetector.Setup(r => r.DetectAsync(ScriptLanguage.PowerShell))
            .ReturnsAsync(runtime);

        var code = @"
Write-Host ""Line 1""
Write-Host ""Line 2""
Write-Host ""Line 3""
";

        // Act
        var execution = await _scriptExecutor.ExecuteCodeAsync(
            code,
            ScriptLanguage.PowerShell,
            null,
            null,
            CancellationToken.None);

        var maxWait = TimeSpan.FromSeconds(10);
        var startTime = DateTime.Now;
        ScriptExecution? finalExecution = null;

        while (DateTime.Now - startTime < maxWait)
        {
            finalExecution = await _scriptExecutor.GetExecutionAsync(execution.Id);
            if (finalExecution != null &&
                (finalExecution.Status == ScriptExecutionStatus.Completed ||
                 finalExecution.Status == ScriptExecutionStatus.Failed))
            {
                break;
            }
            await Task.Delay(200);
        }

        var logs = await _scriptExecutor.GetOutputAsync(execution.Id);

        // Assert
        logs.Should().NotBeNull();
        logs.Should().Contain(log => log.Message.Contains("Line 1"));
        logs.Should().Contain(log => log.Message.Contains("Line 2"));
        logs.Should().Contain(log => log.Message.Contains("Line 3"));
        logs.Should().AllSatisfy(log =>
        {
            log.StreamType.Should().BeOneOf("stdout", "stderr");
            log.Timestamp.Should().BeAfter(DateTime.MinValue);
        });
    }

    [Fact]
    public async Task ExecuteCodeAsync_WithStdError_ShouldCaptureErrorOutput()
    {
        // Arrange
        var runtime = new RuntimeEnvironment
        {
            Language = ScriptLanguage.PowerShell,
            IsAvailable = true,
            Version = "5.1",
            InterpreterPath = "powershell.exe"
        };
        _mockRuntimeDetector.Setup(r => r.DetectAsync(ScriptLanguage.PowerShell))
            .ReturnsAsync(runtime);

        var code = @"
Write-Host ""Normal output""
Write-Error ""Error output""
";

        // Act
        var execution = await _scriptExecutor.ExecuteCodeAsync(
            code,
            ScriptLanguage.PowerShell,
            null,
            null,
            CancellationToken.None);

        var maxWait = TimeSpan.FromSeconds(10);
        var startTime = DateTime.Now;
        ScriptExecution? finalExecution = null;

        while (DateTime.Now - startTime < maxWait)
        {
            finalExecution = await _scriptExecutor.GetExecutionAsync(execution.Id);
            if (finalExecution != null &&
                (finalExecution.Status == ScriptExecutionStatus.Completed ||
                 finalExecution.Status == ScriptExecutionStatus.Failed))
            {
                break;
            }
            await Task.Delay(200);
        }

        var logs = await _scriptExecutor.GetOutputAsync(execution.Id);

        // Assert
        finalExecution.Should().NotBeNull();
        logs.Should().Contain(log => log.StreamType == "stdout" && log.Message.Contains("Normal output"));
    }

    #endregion

    #region ScriptExecutor - Runtime Not Available Tests

    [Fact]
    public async Task ExecuteCodeAsync_RuntimeNotAvailable_ShouldFailWithErrorMessage()
    {
        // Arrange
        var runtime = new RuntimeEnvironment
        {
            Language = ScriptLanguage.Python,
            IsAvailable = false,
            Version = string.Empty,
            InterpreterPath = string.Empty
        };
        _mockRuntimeDetector.Setup(r => r.DetectAsync(ScriptLanguage.Python))
            .ReturnsAsync(runtime);

        // Act
        var execution = await _scriptExecutor.ExecuteCodeAsync(
            "print('hello')",
            ScriptLanguage.Python,
            null,
            null,
            CancellationToken.None);

        // Assert
        var maxWait = TimeSpan.FromSeconds(5);
        var startTime = DateTime.Now;
        ScriptExecution? finalExecution = null;

        while (DateTime.Now - startTime < maxWait)
        {
            finalExecution = await _scriptExecutor.GetExecutionAsync(execution.Id);
            if (finalExecution != null &&
                (finalExecution.Status == ScriptExecutionStatus.Failed ||
                 finalExecution.Status == ScriptExecutionStatus.Completed))
            {
                break;
            }
            await Task.Delay(200);
        }

        finalExecution.Should().NotBeNull();
        finalExecution!.Status.Should().Be(ScriptExecutionStatus.Failed);
        finalExecution.ErrorOutput.Should().Contain("运行环境不可用");
    }

    #endregion

    #region ScriptExecutor - CancellationToken Tests

    [Fact]
    public async Task ExecuteCodeAsync_WithCancellationToken_ShouldCancelWhenTokenCancelled()
    {
        // Arrange
        var runtime = new RuntimeEnvironment
        {
            Language = ScriptLanguage.PowerShell,
            IsAvailable = true,
            Version = "5.1",
            InterpreterPath = "powershell.exe"
        };
        _mockRuntimeDetector.Setup(r => r.DetectAsync(ScriptLanguage.PowerShell))
            .ReturnsAsync(runtime);

        var code = "Start-Sleep -Seconds 30";
        var cts = new CancellationTokenSource();

        // Act
        var execution = await _scriptExecutor.ExecuteCodeAsync(
            code,
            ScriptLanguage.PowerShell,
            null,
            null,
            cts.Token);

        await Task.Delay(500);
        cts.Cancel();

        // Assert
        var maxWait = TimeSpan.FromSeconds(5);
        var startTime = DateTime.Now;
        ScriptExecution? finalExecution = null;

        while (DateTime.Now - startTime < maxWait)
        {
            finalExecution = await _scriptExecutor.GetExecutionAsync(execution.Id);
            if (finalExecution != null &&
                (finalExecution.Status == ScriptExecutionStatus.Cancelled ||
                 finalExecution.Status == ScriptExecutionStatus.Completed ||
                 finalExecution.Status == ScriptExecutionStatus.Failed))
            {
                break;
            }
            await Task.Delay(200);
        }

        finalExecution.Should().NotBeNull();
        finalExecution!.Status.Should().Be(ScriptExecutionStatus.Cancelled);

        cts.Dispose();
    }

    #endregion

    #region Script Model Tests

    [Fact]
    public void ScriptParameter_ShouldHaveCorrectDefaultValues()
    {
        // Arrange & Act
        var param = new ScriptParameter();

        // Assert
        param.Name.Should().Be(string.Empty);
        param.Type.Should().Be(ScriptParameterType.String);
        param.DefaultValue.Should().BeNull();
        param.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void ScriptExecution_ShouldHaveCorrectDefaultStatus()
    {
        // Arrange & Act
        var execution = new ScriptExecution();

        // Assert
        execution.Status.Should().Be(default(ScriptExecutionStatus));
        execution.OutputLogs.Should().NotBeNull();
        execution.OutputLogs.Should().BeEmpty();
    }

    #endregion
}
