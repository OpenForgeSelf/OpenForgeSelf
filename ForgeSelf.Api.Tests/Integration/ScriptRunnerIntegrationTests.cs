using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.ScriptRunner.Controllers;
using ForgeSelf.Api.Plugins.ScriptRunner.Models;
using ForgeSelf.Api.Plugins.ScriptRunner.Services;
using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ForgeSelf.Api.Tests.Integration;

public class ScriptRunnerIntegrationTests
{
    private readonly Mock<IScriptService> _mockScriptService;
    private readonly Mock<IScriptExecutor> _mockScriptExecutor;
    private readonly Mock<IRuntimeDetector> _mockRuntimeDetector;
    private readonly Mock<IScriptTemplateService> _mockTemplateService;
    private readonly ScriptRunnerController _controller;

    public ScriptRunnerIntegrationTests()
    {
        _mockScriptService = new Mock<IScriptService>();
        _mockScriptExecutor = new Mock<IScriptExecutor>();
        _mockRuntimeDetector = new Mock<IRuntimeDetector>();
        _mockTemplateService = new Mock<IScriptTemplateService>();
        _controller = new ScriptRunnerController(
            _mockScriptService.Object,
            _mockScriptExecutor.Object,
            _mockRuntimeDetector.Object,
            _mockTemplateService.Object);
    }

    [Fact]
    public async Task GetScripts_ValidRequest_ReturnsOk()
    {
        // Arrange
        var response = new ScriptListResponse
        {
            Items = new List<Script>(),
            Total = 0,
            Page = 1,
            PageSize = 20
        };
        _mockScriptService.Setup(s => s.ListScriptsAsync(null, null, null, null, 1, 20))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetScripts();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<ScriptListResponse>;
        apiResponse!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetScripts_WithFilters_ReturnsOk()
    {
        // Arrange
        var response = new ScriptListResponse
        {
            Items = new List<Script>
            {
                new() { Id = 1, Name = "Test Script", Language = ScriptLanguage.PowerShell }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _mockScriptService.Setup(s => s.ListScriptsAsync("test", "automation", ScriptLanguage.PowerShell, true, 1, 20))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetScripts("test", "automation", ScriptLanguage.PowerShell, true);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<ScriptListResponse>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetScriptById_ExistingId_ReturnsOk()
    {
        // Arrange
        var script = new Script
        {
            Id = 1,
            Name = "Test Script",
            Language = ScriptLanguage.PowerShell
        };
        _mockScriptService.Setup(s => s.GetScriptAsync(1))
            .ReturnsAsync(script);

        // Act
        var result = await _controller.GetScriptById(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<Script>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.Name.Should().Be("Test Script");
    }

    [Fact]
    public async Task GetScriptById_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockScriptService.Setup(s => s.GetScriptAsync(999))
            .ReturnsAsync((Script?)null);

        // Act
        var result = await _controller.GetScriptById(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateScript_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new CreateScriptRequest
        {
            Name = "New Script",
            Language = ScriptLanguage.PowerShell,
            Code = "Write-Host 'Hello'"
        };
        var createdScript = new Script
        {
            Id = 1,
            Name = "New Script",
            Language = ScriptLanguage.PowerShell
        };
        _mockScriptService.Setup(s => s.CreateScriptAsync(It.IsAny<CreateScriptRequest>()))
            .ReturnsAsync(createdScript);

        // Act
        var result = await _controller.CreateScript(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<Script>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.Name.Should().Be("New Script");
    }

    [Fact]
    public async Task CreateScript_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateScriptRequest
        {
            Name = "",
            Language = ScriptLanguage.PowerShell
        };

        // Act
        var result = await _controller.CreateScript(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result.Result as BadRequestObjectResult;
        var apiResponse = badRequest!.Value as ApiResponse<Script>;
        apiResponse!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateScript_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new UpdateScriptRequest
        {
            Name = "Updated Script"
        };
        var updatedScript = new Script
        {
            Id = 1,
            Name = "Updated Script"
        };
        _mockScriptService.Setup(s => s.UpdateScriptAsync(1, It.IsAny<UpdateScriptRequest>()))
            .ReturnsAsync(updatedScript);

        // Act
        var result = await _controller.UpdateScript(1, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<Script>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.Name.Should().Be("Updated Script");
    }

    [Fact]
    public async Task UpdateScript_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateScriptRequest
        {
            Name = "Updated Script"
        };
        _mockScriptService.Setup(s => s.UpdateScriptAsync(999, It.IsAny<UpdateScriptRequest>()))
            .ReturnsAsync((Script?)null);

        // Act
        var result = await _controller.UpdateScript(999, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateScript_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateScriptRequest
        {
            Name = ""
        };

        // Act
        var result = await _controller.UpdateScript(1, request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteScript_ExistingId_ReturnsOk()
    {
        // Arrange
        _mockScriptService.Setup(s => s.DeleteScriptAsync(1))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteScript(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse;
        apiResponse!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteScript_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockScriptService.Setup(s => s.DeleteScriptAsync(999))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteScript(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ToggleFavorite_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new FavoriteRequest { IsFavorite = true };
        _mockScriptService.Setup(s => s.FavoriteScriptAsync(1, true))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ToggleFavorite(1, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse;
        apiResponse!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleFavorite_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var request = new FavoriteRequest { IsFavorite = true };
        _mockScriptService.Setup(s => s.FavoriteScriptAsync(999, true))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.ToggleFavorite(999, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        // Arrange
        var categories = new List<string> { "Automation", "DevOps", "Data" };
        _mockScriptService.Setup(s => s.GetCategoriesAsync())
            .ReturnsAsync(categories);

        // Act
        var result = await _controller.GetCategories();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<List<string>>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTags_ReturnsOk()
    {
        // Arrange
        var tags = new List<string> { "backup", "deploy", "monitor" };
        _mockScriptService.Setup(s => s.GetTagsAsync())
            .ReturnsAsync(tags);

        // Act
        var result = await _controller.GetTags();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<List<string>>;
        apiResponse!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetRuntimes_ReturnsOk()
    {
        // Arrange
        var runtimes = new List<RuntimeEnvironment>
        {
            new() { Language = ScriptLanguage.PowerShell, IsAvailable = true, Version = "5.1" },
            new() { Language = ScriptLanguage.Python, IsAvailable = true, Version = "3.9" }
        };
        _mockRuntimeDetector.Setup(s => s.DetectAllAsync())
            .ReturnsAsync(runtimes);

        // Act
        var result = await _controller.GetRuntimes();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<List<RuntimeEnvironment>>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteScript_ExistingId_ReturnsOk()
    {
        // Arrange
        var script = new Script { Id = 1, Name = "Test Script" };
        var execution = new ScriptExecution
        {
            Id = 1,
            ScriptId = 1,
            Status = ScriptExecutionStatus.Running
        };
        _mockScriptService.Setup(s => s.GetScriptAsync(1))
            .ReturnsAsync(script);
        _mockScriptExecutor.Setup(s => s.ExecuteAsync(1, null))
            .ReturnsAsync(execution);
        _mockScriptService.Setup(s => s.IncrementUsageAsync(1))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ExecuteScript(1, null);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<ScriptExecution>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.Status.Should().Be(ScriptExecutionStatus.Running);
    }

    [Fact]
    public async Task ExecuteScript_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockScriptService.Setup(s => s.GetScriptAsync(999))
            .ReturnsAsync((Script?)null);

        // Act
        var result = await _controller.ExecuteScript(999, null);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ExecuteCode_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new ExecuteCodeRequest
        {
            Code = "Write-Host 'Hello World'",
            Language = ScriptLanguage.PowerShell
        };
        var execution = new ScriptExecution
        {
            Id = 1,
            Status = ScriptExecutionStatus.Running
        };
        _mockScriptExecutor.Setup(s => s.ExecuteCodeAsync(
            It.IsAny<string>(),
            It.IsAny<ScriptLanguage>(),
            null,
            null))
            .ReturnsAsync(execution);

        // Act
        var result = await _controller.ExecuteCode(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<ScriptExecution>;
        apiResponse!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteCode_EmptyCode_ReturnsBadRequest()
    {
        // Arrange
        var request = new ExecuteCodeRequest
        {
            Code = "",
            Language = ScriptLanguage.PowerShell
        };

        // Act
        var result = await _controller.ExecuteCode(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetExecution_ExistingId_ReturnsOk()
    {
        // Arrange
        var execution = new ScriptExecution
        {
            Id = 1,
            ScriptId = 1,
            Status = ScriptExecutionStatus.Completed
        };
        _mockScriptExecutor.Setup(s => s.GetExecutionAsync(1))
            .ReturnsAsync(execution);

        // Act
        var result = await _controller.GetExecution(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<ScriptExecution>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.Status.Should().Be(ScriptExecutionStatus.Completed);
    }

    [Fact]
    public async Task GetExecution_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockScriptExecutor.Setup(s => s.GetExecutionAsync(999))
            .ReturnsAsync((ScriptExecution?)null);

        // Act
        var result = await _controller.GetExecution(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CancelExecution_ExistingId_ReturnsOk()
    {
        // Arrange
        _mockScriptExecutor.Setup(s => s.CancelAsync(1))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CancelExecution(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse;
        apiResponse!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CancelExecution_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockScriptExecutor.Setup(s => s.CancelAsync(999))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CancelExecution(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetExecutions_ValidRequest_ReturnsOk()
    {
        // Arrange
        var response = new ExecutionListResponse
        {
            Items = new List<ScriptExecution>(),
            Total = 0,
            Page = 1,
            PageSize = 20
        };
        _mockScriptService.Setup(s => s.ListExecutionsAsync(null, null, 1, 20))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetExecutions();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<ExecutionListResponse>;
        apiResponse!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetExecutions_WithFilters_ReturnsOk()
    {
        // Arrange
        var response = new ExecutionListResponse
        {
            Items = new List<ScriptExecution>
            {
                new() { Id = 1, ScriptId = 1, Status = ScriptExecutionStatus.Completed }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _mockScriptService.Setup(s => s.ListExecutionsAsync(1, ScriptExecutionStatus.Completed, 1, 20))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetExecutions(1, ScriptExecutionStatus.Completed);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetTemplates_ReturnsOk()
    {
        // Arrange
        var templates = new List<ScriptTemplate>
        {
            new() { Id = "1", Name = "Template 1", Language = ScriptLanguage.PowerShell }
        };
        _mockTemplateService.Setup(s => s.GetTemplatesAsync(null, null, null))
            .ReturnsAsync(templates);

        // Act
        var result = await _controller.GetTemplates();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<List<ScriptTemplate>>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTemplates_WithFilters_ReturnsOk()
    {
        // Arrange
        var templates = new List<ScriptTemplate>
        {
            new() { Id = "1", Name = "PowerShell Template", Language = ScriptLanguage.PowerShell }
        };
        _mockTemplateService.Setup(s => s.GetTemplatesAsync("automation", "backup", ScriptLanguage.PowerShell))
            .ReturnsAsync(templates);

        // Act
        var result = await _controller.GetTemplates("automation", "backup", ScriptLanguage.PowerShell);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetTemplateCategories_ReturnsOk()
    {
        // Arrange
        var categories = new List<ScriptTemplateCategory>
        {
            new() { Id = "1", Name = "Automation" }
        };
        _mockTemplateService.Setup(s => s.GetCategoriesAsync())
            .ReturnsAsync(categories);

        // Act
        var result = await _controller.GetTemplateCategories();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<List<ScriptTemplateCategory>>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().HaveCount(1);
    }
}
