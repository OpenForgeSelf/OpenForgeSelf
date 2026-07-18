using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.AIAgent.Models;
using OpenForgeSelf.Backend.Plugins.AIAgent.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

public class ToolRegistryTests
{
    private readonly ToolRegistry _registry;

    public ToolRegistryTests()
    {
        _registry = new ToolRegistry();
    }

    private class TestTool : IToolFunctionExtension
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PluginId { get; set; } = string.Empty;
        public string ParametersJsonSchema { get; set; } = "{}";
        public Func<string, Task<string>>? ExecuteHandler { get; set; }

        public Task<string> ExecuteAsync(string parameters)
        {
            if (ExecuteHandler != null)
                return ExecuteHandler(parameters);
            return Task.FromResult("{\"result\":\"ok\"}");
        }
    }

    #region Register & Unregister

    [Fact]
    public void RegisterTool_ValidTool_AddsToRegistry()
    {
        // Arrange
        var tool = new TestTool { Id = "test.tool1", Name = "test_tool", Description = "A test tool" };

        // Act
        _registry.RegisterTool(tool);

        // Assert
        var result = _registry.GetTool("test_tool");
        result.Should().NotBeNull();
        result!.Id.Should().Be("test.tool1");
    }

    [Fact]
    public void RegisterTool_DuplicateName_PreservesFirst()
    {
        // Arrange
        var tool1 = new TestTool { Id = "test.v1", Name = "duplicate_tool", Description = "Version 1" };
        var tool2 = new TestTool { Id = "test.v2", Name = "duplicate_tool", Description = "Version 2" };

        // Act
        _registry.RegisterTool(tool1);
        _registry.RegisterTool(tool2);

        // Assert
        var result = _registry.GetTool("duplicate_tool");
        result.Should().NotBeNull();
        result!.Id.Should().Be("test.v1");
    }

    [Fact]
    public void UnregisterTool_ExistingTool_RemovesFromRegistry()
    {
        // Arrange
        var tool = new TestTool { Id = "test.remove", Name = "remove_tool" };
        _registry.RegisterTool(tool);

        // Act
        _registry.UnregisterTool("test.remove");

        // Assert
        var result = _registry.GetTool("remove_tool");
        result.Should().BeNull();
    }

    [Fact]
    public void UnregisterTool_NonExistingTool_DoesNotThrow()
    {
        // Act
        var action = () => _registry.UnregisterTool("nonexistent.tool");

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region GetTool & GetAllTools

    [Fact]
    public void GetTool_ExistingTool_ReturnsTool()
    {
        // Arrange
        var tool = new TestTool { Id = "test.get", Name = "get_tool", Description = "Test get" };
        _registry.RegisterTool(tool);

        // Act
        var result = _registry.GetTool("get_tool");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("get_tool");
    }

    [Fact]
    public void GetTool_NonExistingTool_ReturnsNull()
    {
        // Act
        var result = _registry.GetTool("nonexistent_tool");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetAllTools_ReturnsAllRegisteredTools()
    {
        // Arrange
        _registry.RegisterTool(new TestTool { Id = "test.1", Name = "tool_1" });
        _registry.RegisterTool(new TestTool { Id = "test.2", Name = "tool_2" });
        _registry.RegisterTool(new TestTool { Id = "test.3", Name = "tool_3" });

        // Act
        var result = _registry.GetAllTools();

        // Assert
        result.Count().Should().BeGreaterThanOrEqualTo(3);
    }

    #endregion

    #region GetToolDefinitions

    [Fact]
    public void GetToolDefinitions_ReturnsCorrectFormat()
    {
        // Arrange
        var tool = new TestTool
        {
            Id = "test.def",
            Name = "def_tool",
            Description = "A tool for testing definitions",
            ParametersJsonSchema = "{\"type\":\"object\",\"properties\":{\"input\":{\"type\":\"string\"}}}"
        };
        _registry.RegisterTool(tool);

        // Act
        var result = _registry.GetToolDefinitions();

        // Assert
        result.Should().NotBeEmpty();
        var def = result.FirstOrDefault(d => d.Function.Name == "def_tool");
        def.Should().NotBeNull();
        def!.Function.Description.Should().Be("A tool for testing definitions");
    }

    #endregion

    #region ExecuteToolAsync

    [Fact]
    public async Task ExecuteToolAsync_ExistingTool_ReturnsResult()
    {
        // Arrange
        var tool = new TestTool
        {
            Id = "test.exec",
            Name = "exec_tool",
            ExecuteHandler = (param) => Task.FromResult("{\"result\":\"success\"}")
        };
        _registry.RegisterTool(tool);

        // Act
        var result = await _registry.ExecuteToolAsync("exec_tool", "{}");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("result");
        result.Should().Contain("success");
    }

    [Fact]
    public async Task ExecuteToolAsync_NonExistingTool_ReturnsError()
    {
        // Act
        var result = await _registry.ExecuteToolAsync("nonexistent_tool", "{}");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\"Success\"");
        result.Should().Contain("false");
        result.Should().Contain("TOOL_NOT_FOUND");
    }

    [Fact]
    public async Task ExecuteToolAsync_WithParameters_PassesParametersToTool()
    {
        // Arrange
        string? receivedParams = null;
        var tool = new TestTool
        {
            Id = "test.params",
            Name = "params_tool",
            ExecuteHandler = (param) =>
            {
                receivedParams = param;
                return Task.FromResult("{\"ok\":true}");
            }
        };
        _registry.RegisterTool(tool);

        // Act
        await _registry.ExecuteToolAsync("params_tool", "{\"key\":\"value\"}");

        // Assert
        receivedParams.Should().Be("{\"key\":\"value\"}");
    }

    #endregion

    #region ExecuteToolWithResultAsync

    [Fact]
    public async Task ExecuteToolWithResultAsync_ExistingTool_SuccessIsTrue()
    {
        // Arrange
        var tool = new TestTool
        {
            Id = "test.result",
            Name = "result_tool",
            ExecuteHandler = (param) => Task.FromResult("{\"data\":\"test\"}")
        };
        _registry.RegisterTool(tool);

        // Act
        var result = await _registry.ExecuteToolWithResultAsync("result_tool", "{}");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Result.Should().Contain("data");
        result.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ExecuteToolWithResultAsync_NonExistingTool_SuccessIsFalse()
    {
        // Act
        var result = await _registry.ExecuteToolWithResultAsync("nonexistent_tool", "{}");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteToolWithResultAsync_ToolThrows_SuccessIsFalse()
    {
        // Arrange
        var tool = new TestTool
        {
            Id = "test.throw",
            Name = "throw_tool",
            ExecuteHandler = (param) => throw new InvalidOperationException("Tool failed")
        };
        _registry.RegisterTool(tool);

        // Act
        var result = await _registry.ExecuteToolWithResultAsync("throw_tool", "{}");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region ExecuteToolWithTimeoutAsync

    [Fact]
    public async Task ExecuteToolWithTimeoutAsync_FastTool_CompletesSuccessfully()
    {
        // Arrange
        var tool = new TestTool
        {
            Id = "test.fast",
            Name = "fast_tool",
            ExecuteHandler = async (param) =>
            {
                await Task.Delay(10);
                return "{\"result\":\"fast\"}";
            }
        };
        _registry.RegisterTool(tool);

        // Act
        var result = await _registry.ExecuteToolWithTimeoutAsync("fast_tool", "{}", timeoutSeconds: 5);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Should().Contain("fast");
    }

    #endregion

    #region ValidateParameters

    [Fact]
    public void ValidateParameters_NonExistingTool_ReturnsInvalid()
    {
        // Act
        var result = _registry.ValidateParameters("nonexistent_tool", "{}");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateParameters_ExistingToolWithEmptySchema_ReturnsValid()
    {
        // Arrange
        var tool = new TestTool
        {
            Id = "test.val",
            Name = "validate_tool",
            ParametersJsonSchema = "{}"
        };
        _registry.RegisterTool(tool);

        // Act
        var result = _registry.ValidateParameters("validate_tool", "{}");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_InvalidJson_ReturnsInvalid()
    {
        // Arrange
        var tool = new TestTool
        {
            Id = "test.invalidjson",
            Name = "invalid_json_tool",
            ParametersJsonSchema = "{}"
        };
        _registry.RegisterTool(tool);

        // Act
        var result = _registry.ValidateParameters("invalid_json_tool", "not valid json");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    #endregion
}
