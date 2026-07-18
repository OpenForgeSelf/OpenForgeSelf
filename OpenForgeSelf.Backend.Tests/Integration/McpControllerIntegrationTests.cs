using OpenForgeSelf.Backend.Controllers;
using OpenForgeSelf.Backend.Models.Mcp;
using OpenForgeSelf.Backend.Models.Plugins;
using OpenForgeSelf.Backend.Plugins.AIAgent.Services;
using OpenForgeSelf.Backend.Services.Mcp;
using Microsoft.AspNetCore.Mvc;

namespace OpenForgeSelf.Backend.Tests.Integration;

/// <summary>
/// McpController 集成测试
/// </summary>
public class McpControllerIntegrationTests : IClassFixture<XCodeTestFixture>
{
    private readonly McpController _controller;

    public McpControllerIntegrationTests(XCodeTestFixture fixture)
    {
        var toolRegistry = new ToolRegistry(null);
        var service = new McpService(toolRegistry);
        _controller = new McpController(service);
    }

    [Fact]
    public async Task GetServers_ShouldReturnSeededServers()
    {
        // Act
        var result = await _controller.GetServers();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<List<McpServerDto>>)okResult.Value!;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Count.Should().BeGreaterThanOrEqualTo(4);
        response.Data.Should().Contain(s => s.Name == "OpenForgeSelf Local");
        response.Data.Should().Contain(s => s.Name == "Filesystem");
    }

    [Fact]
    public async Task GetTools_WithValidServer_ShouldReturnTools()
    {
        // Act
        var result = await _controller.GetTools("mcp-forgeself-local");

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<List<McpToolDto>>)okResult.Value!;
        response.Data.Should().NotBeNull();
        response.Data!.Count.Should().BeGreaterThan(0);
        response.Data.Should().OnlyContain(t => t.ServerId == "mcp-forgeself-local");
    }

    [Fact]
    public async Task GetTools_WithKeyword_ShouldFilterResults()
    {
        // Act
        var result = await _controller.GetTools("mcp-forgeself-local", keyword: "code");

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<List<McpToolDto>>)okResult.Value!;
        response.Data.Should().OnlyContain(t =>
            t.Name.Contains("code", StringComparison.OrdinalIgnoreCase) ||
            t.Description.Contains("code", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetTools_WithCategory_ShouldFilterResults()
    {
        // Act
        var result = await _controller.GetTools("mcp-filesystem", category: "file");

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<List<McpToolDto>>)okResult.Value!;
        response.Data.Should().OnlyContain(t => t.Category.Equals("file", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ToggleTool_ShouldFlipEnabledState()
    {
        // Arrange - 使用默认启用的工具
        var toolId = "mcp-tool-forge-1";

        // Act
        var result = await _controller.ToggleTool(toolId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<McpToolDto>)okResult.Value!;
        response.Data.Should().NotBeNull();
        response.Data!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleTool_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var result = await _controller.ToggleTool("non-existent-tool");

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task TestTool_WithEnabledTool_ShouldReturnSuccess()
    {
        // Arrange - 使用已启用的工具
        var toolId = "mcp-tool-forge-2";

        // Act
        var result = await _controller.TestTool(toolId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<McpTestResultDto>)okResult.Value!;
        response.Data.Should().NotBeNull();
        response.Data!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TestTool_WithDisabledTool_ShouldReturnFailure()
    {
        // Arrange - 使用默认禁用的工具
        var toolId = "mcp-tool-gh-1";

        // Act
        var result = await _controller.TestTool(toolId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<McpTestResultDto>)okResult.Value!;
        var testResult = response.Data!;
        testResult.Success.Should().BeFalse();
        testResult.Message.Should().Contain("禁用");
    }

    [Fact]
    public async Task TestTool_WithNonExistentId_ShouldReturnFailure()
    {
        // Act
        var result = await _controller.TestTool("non-existent-tool");

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<McpTestResultDto>)okResult.Value!;
        response.Data!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task TestServer_WithConnectedServer_ShouldReturnSuccess()
    {
        // Act
        var result = await _controller.TestServer("mcp-forgeself-local");

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<McpTestResultDto>)okResult.Value!;
        response.Data!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TestServer_WithDisconnectedServer_ShouldReturnFailure()
    {
        // Act
        var result = await _controller.TestServer("mcp-github-api");

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<McpTestResultDto>)okResult.Value!;
        response.Data!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task TestServer_WithNonExistentId_ShouldReturnFailure()
    {
        // Act
        var result = await _controller.TestServer("non-existent-server");

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = (ApiResponse<McpTestResultDto>)okResult.Value!;
        response.Data!.Success.Should().BeFalse();
    }
}
