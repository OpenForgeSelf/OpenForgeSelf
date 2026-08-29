using System.Text.Json;
using ForgeSelf.Api.Services;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Tests.Plugins;

namespace ForgeSelf.Api.Tests.Integration;

public class ToolRegistryIntegrationTests
{
    [Fact]
    public void RegisterTool_SingleTool_IsRegistered()
    {
        var registry = new ToolRegistry();
        var tool = new FakeToolFunctionExtension
        {
            Id = "test.tool1",
            Name = "test_tool",
            PluginId = "test.plugin",
            Description = "A test tool",
            ParametersJsonSchema = "{}"
        };

        registry.RegisterTool(tool);

        var retrieved = registry.GetTool("test_tool");
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be("test.tool1");
        retrieved.Name.Should().Be("test_tool");
    }

    [Fact]
    public void RegisterTool_MultipleTools_AllRegistered()
    {
        var registry = new ToolRegistry();
        var tool1 = new FakeToolFunctionExtension { Id = "t1", Name = "tool_one", PluginId = "p1" };
        var tool2 = new FakeToolFunctionExtension { Id = "t2", Name = "tool_two", PluginId = "p1" };
        var tool3 = new FakeToolFunctionExtension { Id = "t3", Name = "tool_three", PluginId = "p2" };

        registry.RegisterTool(tool1);
        registry.RegisterTool(tool2);
        registry.RegisterTool(tool3);

        var allTools = registry.GetAllTools().ToList();
        allTools.Should().HaveCount(3);
    }

    [Fact]
    public void RegisterTool_DuplicateName_SecondRegistrationFails()
    {
        var registry = new ToolRegistry();
        var tool1 = new FakeToolFunctionExtension { Id = "t1", Name = "duplicate_tool" };
        var tool2 = new FakeToolFunctionExtension { Id = "t2", Name = "duplicate_tool" };

        registry.RegisterTool(tool1);
        registry.RegisterTool(tool2);

        var allTools = registry.GetAllTools().ToList();
        allTools.Should().HaveCount(1);
        allTools[0].Id.Should().Be("t1");
    }

    [Fact]
    public void RegisterTool_NullTool_DoesNotThrow()
    {
        var registry = new ToolRegistry();

        var action = () => registry.RegisterTool(null!);

        action.Should().NotThrow();
    }

    [Fact]
    public void RegisterTool_EmptyName_DoesNotRegister()
    {
        var registry = new ToolRegistry();
        var tool = new FakeToolFunctionExtension { Id = "t1", Name = "" };

        registry.RegisterTool(tool);

        registry.GetAllTools().Should().BeEmpty();
    }

    [Fact]
    public void UnregisterTool_ExistingTool_IsRemoved()
    {
        var registry = new ToolRegistry();
        var tool = new FakeToolFunctionExtension { Id = "test.id", Name = "removable_tool" };
        registry.RegisterTool(tool);

        registry.UnregisterTool("test.id");

        registry.GetTool("removable_tool").Should().BeNull();
        registry.GetAllTools().Should().BeEmpty();
    }

    [Fact]
    public void UnregisterTool_NonExistingTool_DoesNotThrow()
    {
        var registry = new ToolRegistry();

        var action = () => registry.UnregisterTool("nonexistent");

        action.Should().NotThrow();
    }

    [Fact]
    public async Task ExecuteToolAsync_ExistingTool_ReturnsResult()
    {
        var registry = new ToolRegistry();
        var tool = new FakeToolFunctionExtension
        {
            Id = "exec.test",
            Name = "executable_tool",
            ExecuteHandler = (parameters) => Task.FromResult("{\"result\":\"success\"}")
        };
        registry.RegisterTool(tool);

        var result = await registry.ExecuteToolAsync("executable_tool", "{}");

        result.Should().Be("{\"result\":\"success\"}");
    }

    [Fact]
    public async Task ExecuteToolAsync_NonExistingTool_ReturnsError()
    {
        var registry = new ToolRegistry();

        var result = await registry.ExecuteToolAsync("nonexistent_tool", "{}");

        result.Should().Contain("\"Success\"");
        result.Should().Contain("false");
    }

    [Fact]
    public async Task ExecuteToolWithResultAsync_ExistingTool_SuccessIsTrue()
    {
        var registry = new ToolRegistry();
        var tool = new FakeToolFunctionExtension
        {
            Id = "r.test",
            Name = "result_tool",
            ExecuteHandler = (parameters) => Task.FromResult("{\"ok\":true}")
        };
        registry.RegisterTool(tool);

        var result = await registry.ExecuteToolWithResultAsync("result_tool", "{}");

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ToolName.Should().Be("result_tool");
        result.Result.Should().Be("{\"ok\":true}");
        result.DurationMs.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task ExecuteToolWithResultAsync_ToolThrows_SuccessIsFalse()
    {
        var registry = new ToolRegistry();
        var tool = new FakeToolFunctionExtension
        {
            Id = "err.test",
            Name = "error_tool",
            ExecuteHandler = (parameters) => throw new InvalidOperationException("Tool error")
        };
        registry.RegisterTool(tool);

        var result = await registry.ExecuteToolWithResultAsync("error_tool", "{}");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Tool error");
    }

    [Fact]
    public void ValidateParameters_ValidParameters_ReturnsValid()
    {
        var registry = new ToolRegistry();
        var schema = @"
        {
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""count"": { ""type"": ""integer"" }
            },
            ""required"": [""name""]
        }";
        var tool = new FakeToolFunctionExtension
        {
            Id = "v.test",
            Name = "validation_tool",
            ParametersJsonSchema = schema
        };
        registry.RegisterTool(tool);

        var result = registry.ValidateParameters("validation_tool", "{\"name\":\"test\",\"count\":42}");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateParameters_MissingRequiredParameter_ReturnsInvalid()
    {
        var registry = new ToolRegistry();
        var schema = @"
        {
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            },
            ""required"": [""name""]
        }";
        var tool = new FakeToolFunctionExtension
        {
            Id = "mr.test",
            Name = "missing_required_tool",
            ParametersJsonSchema = schema
        };
        registry.RegisterTool(tool);

        var result = registry.ValidateParameters("missing_required_tool", "{}");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("name"));
    }

    [Fact]
    public void ValidateParameters_WrongType_ReturnsInvalid()
    {
        var registry = new ToolRegistry();
        var schema = @"
        {
            ""type"": ""object"",
            ""properties"": {
                ""count"": { ""type"": ""integer"" }
            }
        }";
        var tool = new FakeToolFunctionExtension
        {
            Id = "wt.test",
            Name = "wrong_type_tool",
            ParametersJsonSchema = schema
        };
        registry.RegisterTool(tool);

        var result = registry.ValidateParameters("wrong_type_tool", "{\"count\":\"not_a_number\"}");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateParameters_InvalidJson_ReturnsInvalid()
    {
        var registry = new ToolRegistry();
        var tool = new FakeToolFunctionExtension
        {
            Id = "ij.test",
            Name = "invalid_json_tool",
            ParametersJsonSchema = "{}"
        };
        registry.RegisterTool(tool);

        var result = registry.ValidateParameters("invalid_json_tool", "not valid json {");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateParameters_NonExistingTool_ReturnsInvalid()
    {
        var registry = new ToolRegistry();

        var result = registry.ValidateParameters("nonexistent", "{}");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("不存在"));
    }

    [Fact]
    public void GetToolDefinitions_WithRegisteredTools_ReturnsDefinitions()
    {
        var registry = new ToolRegistry();
        var schema = "{\"type\":\"object\",\"properties\":{}}";
        var tool1 = new FakeToolFunctionExtension
        {
            Id = "td1",
            Name = "tool_def_1",
            Description = "First tool",
            ParametersJsonSchema = schema
        };
        var tool2 = new FakeToolFunctionExtension
        {
            Id = "td2",
            Name = "tool_def_2",
            Description = "Second tool",
            ParametersJsonSchema = schema
        };
        registry.RegisterTool(tool1);
        registry.RegisterTool(tool2);

        var definitions = registry.GetToolDefinitions();

        definitions.Should().HaveCount(2);
        definitions.Should().Contain(d => d.Function.Name == "tool_def_1");
        definitions.Should().Contain(d => d.Function.Name == "tool_def_2");
    }

    [Fact]
    public async Task ExecuteToolAsync_MultipleTools_ExecutesCorrectOne()
    {
        var registry = new ToolRegistry();
        var tool1 = new FakeToolFunctionExtension
        {
            Id = "mt1",
            Name = "tool_alpha",
            ExecuteHandler = (p) => Task.FromResult("{\"tool\":\"alpha\"}")
        };
        var tool2 = new FakeToolFunctionExtension
        {
            Id = "mt2",
            Name = "tool_beta",
            ExecuteHandler = (p) => Task.FromResult("{\"tool\":\"beta\"}")
        };
        registry.RegisterTool(tool1);
        registry.RegisterTool(tool2);

        var resultAlpha = await registry.ExecuteToolAsync("tool_alpha", "{}");
        var resultBeta = await registry.ExecuteToolAsync("tool_beta", "{}");

        resultAlpha.Should().Contain("alpha");
        resultBeta.Should().Contain("beta");
    }

    [Fact]
    public async Task ExecuteToolAsync_ReceivesCorrectParameters()
    {
        var registry = new ToolRegistry();
        string? receivedParams = null;
        var tool = new FakeToolFunctionExtension
        {
            Id = "param.test",
            Name = "param_tool",
            ExecuteHandler = (p) =>
            {
                receivedParams = p;
                return Task.FromResult("{}");
            }
        };
        registry.RegisterTool(tool);

        var testParams = "{\"key1\":\"value1\",\"key2\":123}";
        await registry.ExecuteToolAsync("param_tool", testParams);

        receivedParams.Should().Be(testParams);
    }

    [Fact]
    public void ValidateParameters_EnumValue_ValidEnumPasses()
    {
        var registry = new ToolRegistry();
        var schema = @"
        {
            ""type"": ""object"",
            ""properties"": {
                ""format"": {
                    ""type"": ""string"",
                    ""enum"": [""json"", ""xml"", ""html""]
                }
            }
        }";
        var tool = new FakeToolFunctionExtension
        {
            Id = "enum.test",
            Name = "enum_tool",
            ParametersJsonSchema = schema
        };
        registry.RegisterTool(tool);

        var result = registry.ValidateParameters("enum_tool", "{\"format\":\"json\"}");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_EnumValue_InvalidEnumFails()
    {
        var registry = new ToolRegistry();
        var schema = @"
        {
            ""type"": ""object"",
            ""properties"": {
                ""format"": {
                    ""type"": ""string"",
                    ""enum"": [""json"", ""xml"", ""html""]
                }
            }
        }";
        var tool = new FakeToolFunctionExtension
        {
            Id = "enum.test2",
            Name = "enum_tool2",
            ParametersJsonSchema = schema
        };
        registry.RegisterTool(tool);

        var result = registry.ValidateParameters("enum_tool2", "{\"format\":\"yaml\"}");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteToolAsync_MultiRoundCalls_AllSucceed()
    {
        var registry = new ToolRegistry();
        var callCount = 0;
        var tool = new FakeToolFunctionExtension
        {
            Id = "multi.test",
            Name = "multi_call_tool",
            ExecuteHandler = (p) =>
            {
                callCount++;
                return Task.FromResult($"{{\"call\":{callCount}}}");
            }
        };
        registry.RegisterTool(tool);

        for (int i = 1; i <= 5; i++)
        {
            var result = await registry.ExecuteToolWithResultAsync("multi_call_tool", "{}");
            result.Success.Should().BeTrue();
            callCount.Should().Be(i);
        }

        callCount.Should().Be(5);
    }

    [Fact]
    public void GetTool_CaseSensitive_ReturnsCorrectTool()
    {
        var registry = new ToolRegistry();
        var toolLower = new FakeToolFunctionExtension { Id = "t1", Name = "mytoggle" };
        var toolUpper = new FakeToolFunctionExtension { Id = "t2", Name = "MyToggle" };

        registry.RegisterTool(toolLower);
        registry.RegisterTool(toolUpper);

        registry.GetAllTools().Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteToolWithResultAsync_DurationIsRecorded()
    {
        var registry = new ToolRegistry();
        var tool = new FakeToolFunctionExtension
        {
            Id = "dur.test",
            Name = "duration_tool",
            ExecuteHandler = async (p) =>
            {
                await Task.Delay(10);
                return "{}";
            }
        };
        registry.RegisterTool(tool);

        var result = await registry.ExecuteToolWithResultAsync("duration_tool", "{}");

        result.Success.Should().BeTrue();
        result.DurationMs.Should().BeGreaterOrEqualTo(0);
    }
}
