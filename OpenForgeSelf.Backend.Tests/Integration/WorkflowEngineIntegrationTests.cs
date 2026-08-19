using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace OpenForgeSelf.Backend.Tests.Integration;

public class WorkflowEngineIntegrationTests
{
    private readonly Mock<IWorkflowService> _mockWorkflowService;
    private readonly WorkflowController _controller;

    public WorkflowEngineIntegrationTests()
    {
        _mockWorkflowService = new Mock<IWorkflowService>();
        _controller = new WorkflowController(_mockWorkflowService.Object);
    }

    [Fact]
    public async Task GetWorkflows_ValidRequest_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<WorkflowDefinitionDto>
        {
            Items = new List<WorkflowDefinitionDto>(),
            Total = 0,
            Page = 1,
            PageSize = 20
        };
        _mockWorkflowService.Setup(s => s.ListWorkflowsAsync(null, null, 1, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetWorkflows();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<PagedResult<WorkflowDefinitionDto>>;
        response!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetWorkflows_WithKeyword_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<WorkflowDefinitionDto>
        {
            Items = new List<WorkflowDefinitionDto>
            {
                new() { Id = 1, Name = "Test Workflow", Status = WorkflowStatus.Ready }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _mockWorkflowService.Setup(s => s.ListWorkflowsAsync("test", null, 1, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetWorkflows(keyword: "test");

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<PagedResult<WorkflowDefinitionDto>>;
        response!.Success.Should().BeTrue();
        response.Data!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetWorkflowById_ExistingId_ReturnsOk()
    {
        // Arrange
        var workflow = new WorkflowDefinitionDto
        {
            Id = 1,
            Name = "Test Workflow",
            Status = WorkflowStatus.Ready
        };
        _mockWorkflowService.Setup(s => s.GetWorkflowAsync(1))
            .ReturnsAsync(workflow);

        // Act
        var result = await _controller.GetWorkflowById(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<WorkflowDefinitionDto>;
        response!.Success.Should().BeTrue();
        response.Data!.Name.Should().Be("Test Workflow");
    }

    [Fact]
    public async Task GetWorkflowById_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockWorkflowService.Setup(s => s.GetWorkflowAsync(999))
            .ReturnsAsync((WorkflowDefinitionDto?)null);

        // Act
        var result = await _controller.GetWorkflowById(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateWorkflow_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new CreateWorkflowRequest
        {
            Name = "New Workflow",
            Description = "Test description"
        };
        var createdWorkflow = new WorkflowDefinitionDto
        {
            Id = 1,
            Name = "New Workflow",
            Description = "Test description",
            Status = WorkflowStatus.Draft
        };
        _mockWorkflowService.Setup(s => s.CreateWorkflowAsync(It.IsAny<CreateWorkflowRequest>()))
            .ReturnsAsync(createdWorkflow);

        // Act
        var result = await _controller.CreateWorkflow(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<WorkflowDefinitionDto>;
        response!.Success.Should().BeTrue();
        response.Data!.Name.Should().Be("New Workflow");
    }

    [Fact]
    public async Task CreateWorkflow_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateWorkflowRequest
        {
            Name = "",
            Description = "Test description"
        };

        // Act
        var result = await _controller.CreateWorkflow(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result.Result as BadRequestObjectResult;
        var response = badRequest!.Value as ApiResponse<WorkflowDefinitionDto>;
        response!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateWorkflow_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new UpdateWorkflowRequest
        {
            Name = "Updated Workflow"
        };
        var updatedWorkflow = new WorkflowDefinitionDto
        {
            Id = 1,
            Name = "Updated Workflow"
        };
        _mockWorkflowService.Setup(s => s.UpdateWorkflowAsync(1, It.IsAny<UpdateWorkflowRequest>()))
            .ReturnsAsync(updatedWorkflow);

        // Act
        var result = await _controller.UpdateWorkflow(1, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<WorkflowDefinitionDto>;
        response!.Success.Should().BeTrue();
        response.Data!.Name.Should().Be("Updated Workflow");
    }

    [Fact]
    public async Task UpdateWorkflow_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateWorkflowRequest
        {
            Name = "Updated Workflow"
        };
        _mockWorkflowService.Setup(s => s.UpdateWorkflowAsync(999, It.IsAny<UpdateWorkflowRequest>()))
            .ReturnsAsync((WorkflowDefinitionDto?)null);

        // Act
        var result = await _controller.UpdateWorkflow(999, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateWorkflow_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateWorkflowRequest
        {
            Name = ""
        };

        // Act
        var result = await _controller.UpdateWorkflow(1, request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteWorkflow_ExistingId_ReturnsOk()
    {
        // Arrange
        _mockWorkflowService.Setup(s => s.DeleteWorkflowAsync(1))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteWorkflow(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse;
        response!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteWorkflow_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockWorkflowService.Setup(s => s.DeleteWorkflowAsync(999))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteWorkflow(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task FavoriteWorkflow_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new FavoriteWorkflowRequest { IsFavorite = true };
        _mockWorkflowService.Setup(s => s.FavoriteWorkflowAsync(1, true))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.FavoriteWorkflow(1, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse;
        response!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task FavoriteWorkflow_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var request = new FavoriteWorkflowRequest { IsFavorite = true };
        _mockWorkflowService.Setup(s => s.FavoriteWorkflowAsync(999, true))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.FavoriteWorkflow(999, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ExecuteWorkflow_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new ExecuteWorkflowRequest();
        var execution = new WorkflowExecutionDto
        {
            Id = 1,
            WorkflowId = 1,
            Status = WorkflowStatus.Running
        };
        _mockWorkflowService.Setup(s => s.ExecuteWorkflowAsync(1, It.IsAny<ExecuteWorkflowRequest>()))
            .ReturnsAsync(execution);

        // Act
        var result = await _controller.ExecuteWorkflow(1, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<WorkflowExecutionDto>;
        response!.Success.Should().BeTrue();
        response.Data!.Status.Should().Be(WorkflowStatus.Running);
    }

    [Fact]
    public async Task GetExecutions_ValidRequest_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<WorkflowExecutionDto>
        {
            Items = new List<WorkflowExecutionDto>(),
            Total = 0,
            Page = 1,
            PageSize = 20
        };
        _mockWorkflowService.Setup(s => s.GetExecutionsAsync(null, 1, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetExecutions();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetExecutionDetail_ExistingId_ReturnsOk()
    {
        // Arrange
        var detail = new WorkflowExecutionDetailDto
        {
            Id = 1,
            WorkflowId = 1,
            Status = WorkflowStatus.Completed
        };
        _mockWorkflowService.Setup(s => s.GetExecutionDetailAsync(1))
            .ReturnsAsync(detail);

        // Act
        var result = await _controller.GetExecutionDetail(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<WorkflowExecutionDetailDto>;
        response!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetExecutionDetail_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockWorkflowService.Setup(s => s.GetExecutionDetailAsync(999))
            .ReturnsAsync((WorkflowExecutionDetailDto?)null);

        // Act
        var result = await _controller.GetExecutionDetail(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task PauseExecution_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockWorkflowService.Setup(s => s.GetExecutionDetailAsync(999))
            .ReturnsAsync((WorkflowExecutionDetailDto?)null);

        // Act
        var result = await _controller.PauseExecution(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ResumeExecution_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockWorkflowService.Setup(s => s.GetExecutionDetailAsync(999))
            .ReturnsAsync((WorkflowExecutionDetailDto?)null);

        // Act
        var result = await _controller.ResumeExecution(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CancelExecution_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockWorkflowService.Setup(s => s.GetExecutionDetailAsync(999))
            .ReturnsAsync((WorkflowExecutionDetailDto?)null);

        // Act
        var result = await _controller.CancelExecution(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetTemplates_ReturnsOk()
    {
        // Arrange
        var templates = new List<WorkflowTemplateDto>
        {
            new() { Id = "1", Name = "Template 1" }
        };
        _mockWorkflowService.Setup(s => s.GetTemplatesAsync())
            .ReturnsAsync(templates);

        // Act
        var result = await _controller.GetTemplates();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<List<WorkflowTemplateDto>>;
        response!.Success.Should().BeTrue();
        response.Data.Should().HaveCount(1);
    }
}
