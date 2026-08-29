using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Scheduler.Controllers;
using ForgeSelf.Api.Plugins.Scheduler.Models;
using ForgeSelf.Api.Plugins.Scheduler.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ForgeSelf.Api.Tests.Integration;

public class SchedulerIntegrationTests
{
    private readonly Mock<ISchedulerService> _mockSchedulerService;
    private readonly SchedulerController _controller;

    public SchedulerIntegrationTests()
    {
        _mockSchedulerService = new Mock<ISchedulerService>();
        _controller = new SchedulerController(_mockSchedulerService.Object);
    }

    [Fact]
    public async Task GetTasks_ValidRequest_ReturnsOk()
    {
        // Arrange
        var pagedResult = new ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>
        {
            Items = new List<ScheduledTaskDto>(),
            Total = 0,
            Page = 1,
            PageSize = 20
        };
        _mockSchedulerService.Setup(s => s.ListTasksAsync(null, null, 1, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetTasks();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>>;
        response!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetTasks_WithKeyword_ReturnsOk()
    {
        // Arrange
        var pagedResult = new ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>
        {
            Items = new List<ScheduledTaskDto>
            {
                new() { Id = 1, Name = "Test Task", Status = ScheduledTaskStatus.Enabled }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _mockSchedulerService.Setup(s => s.ListTasksAsync("test", null, 1, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetTasks(keyword: "test");

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>>;
        response!.Success.Should().BeTrue();
        response.Data!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTasks_WithStatusFilter_ReturnsOk()
    {
        // Arrange
        var pagedResult = new ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>
        {
            Items = new List<ScheduledTaskDto>
            {
                new() { Id = 1, Name = "Enabled Task", Status = ScheduledTaskStatus.Enabled }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _mockSchedulerService.Setup(s => s.ListTasksAsync(null, ScheduledTaskStatus.Enabled, 1, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetTasks(status: ScheduledTaskStatus.Enabled);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>>;
        response!.Success.Should().BeTrue();
        response.Data!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTask_ExistingId_ReturnsOk()
    {
        // Arrange
        var task = new ScheduledTaskDto
        {
            Id = 1,
            Name = "Test Task",
            Status = ScheduledTaskStatus.Enabled
        };
        _mockSchedulerService.Setup(s => s.GetTaskAsync(1))
            .ReturnsAsync(task);

        // Act
        var result = await _controller.GetTask(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<ScheduledTaskDto>;
        response!.Success.Should().BeTrue();
        response.Data!.Name.Should().Be("Test Task");
    }

    [Fact]
    public async Task GetTask_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockSchedulerService.Setup(s => s.GetTaskAsync(999))
            .ReturnsAsync((ScheduledTaskDto?)null);

        // Act
        var result = await _controller.GetTask(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateTask_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new CreateScheduledTaskRequest
        {
            Name = "New Task",
            TaskType = ScheduledTaskType.Workflow,
            TargetId = "workflow-1",
            ScheduleType = ScheduleType.Cron,
            CronExpression = "0 0 * * *"
        };
        var createdTask = new ScheduledTaskDto
        {
            Id = 1,
            Name = "New Task",
            TaskType = ScheduledTaskType.Workflow,
            Status = ScheduledTaskStatus.Enabled
        };
        _mockSchedulerService.Setup(s => s.CreateTaskAsync(It.IsAny<CreateScheduledTaskRequest>()))
            .ReturnsAsync(createdTask);

        // Act
        var result = await _controller.CreateTask(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<ScheduledTaskDto>;
        response!.Success.Should().BeTrue();
        response.Data!.Name.Should().Be("New Task");
    }

    [Fact]
    public async Task CreateTask_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateScheduledTaskRequest
        {
            Name = "",
            TaskType = ScheduledTaskType.Workflow,
            TargetId = "workflow-1"
        };

        // Act
        var result = await _controller.CreateTask(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result.Result as BadRequestObjectResult;
        var response = badRequest!.Value as ApiResponse<ScheduledTaskDto>;
        response!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTask_EmptyTargetId_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateScheduledTaskRequest
        {
            Name = "Test Task",
            TaskType = ScheduledTaskType.Workflow,
            TargetId = ""
        };

        // Act
        var result = await _controller.CreateTask(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateTask_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new UpdateScheduledTaskRequest
        {
            Name = "Updated Task"
        };
        var updatedTask = new ScheduledTaskDto
        {
            Id = 1,
            Name = "Updated Task"
        };
        _mockSchedulerService.Setup(s => s.UpdateTaskAsync(1, It.IsAny<UpdateScheduledTaskRequest>()))
            .ReturnsAsync(updatedTask);

        // Act
        var result = await _controller.UpdateTask(1, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<ScheduledTaskDto>;
        response!.Success.Should().BeTrue();
        response.Data!.Name.Should().Be("Updated Task");
    }

    [Fact]
    public async Task UpdateTask_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateScheduledTaskRequest
        {
            Name = "Updated Task"
        };
        _mockSchedulerService.Setup(s => s.UpdateTaskAsync(999, It.IsAny<UpdateScheduledTaskRequest>()))
            .ReturnsAsync((ScheduledTaskDto?)null);

        // Act
        var result = await _controller.UpdateTask(999, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateTask_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateScheduledTaskRequest
        {
            Name = ""
        };

        // Act
        var result = await _controller.UpdateTask(1, request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteTask_ExistingId_ReturnsOk()
    {
        // Arrange
        _mockSchedulerService.Setup(s => s.DeleteTaskAsync(1))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteTask(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse;
        response!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTask_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockSchedulerService.Setup(s => s.DeleteTaskAsync(999))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteTask(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ToggleTask_Enable_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new ToggleTaskStatusRequest { Enabled = true };
        var toggledTask = new ScheduledTaskDto
        {
            Id = 1,
            Name = "Test Task",
            Status = ScheduledTaskStatus.Enabled
        };
        _mockSchedulerService.Setup(s => s.ToggleTaskStatusAsync(1, true))
            .ReturnsAsync(toggledTask);

        // Act
        var result = await _controller.ToggleTask(1, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<ScheduledTaskDto>;
        response!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleTask_Disable_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new ToggleTaskStatusRequest { Enabled = false };
        var toggledTask = new ScheduledTaskDto
        {
            Id = 1,
            Name = "Test Task",
            Status = ScheduledTaskStatus.Disabled
        };
        _mockSchedulerService.Setup(s => s.ToggleTaskStatusAsync(1, false))
            .ReturnsAsync(toggledTask);

        // Act
        var result = await _controller.ToggleTask(1, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ToggleTask_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var request = new ToggleTaskStatusRequest { Enabled = true };
        _mockSchedulerService.Setup(s => s.ToggleTaskStatusAsync(999, true))
            .ReturnsAsync((ScheduledTaskDto?)null);

        // Act
        var result = await _controller.ToggleTask(999, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RunNow_ExistingId_ReturnsOk()
    {
        // Arrange
        _mockSchedulerService.Setup(s => s.RunNowAsync(1))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.RunNow(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse;
        response!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RunNow_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockSchedulerService.Setup(s => s.RunNowAsync(999))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.RunNow(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetTaskLogs_ValidRequest_ReturnsOk()
    {
        // Arrange
        var pagedResult = new ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskLogDto>
        {
            Items = new List<ScheduledTaskLogDto>(),
            Total = 0,
            Page = 1,
            PageSize = 20
        };
        _mockSchedulerService.Setup(s => s.GetTaskLogsAsync(1, 1, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetTaskLogs(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void ParseCron_ValidExpression_ReturnsOk()
    {
        // Arrange
        var request = new CronParseRequest
        {
            CronExpression = "0 0 * * *",
            Count = 5
        };
        var parseResult = new CronParseResult
        {
            Valid = true,
            NextRunTimes = new List<DateTime> { DateTime.UtcNow.AddHours(1) }
        };
        _mockSchedulerService.Setup(s => s.ParseCron("0 0 * * *", 5, "Asia/Shanghai"))
            .Returns(parseResult);

        // Act
        var result = _controller.ParseCron(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<CronParseResult>;
        response!.Success.Should().BeTrue();
        response.Data!.Valid.Should().BeTrue();
    }

    [Fact]
    public void ParseCron_EmptyExpression_ReturnsBadRequest()
    {
        // Arrange
        var request = new CronParseRequest
        {
            CronExpression = "",
            Count = 5
        };

        // Act
        var result = _controller.ParseCron(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void ParseCron_InvalidExpression_ReturnsOkWithInvalidResult()
    {
        // Arrange
        var request = new CronParseRequest
        {
            CronExpression = "invalid-cron",
            Count = 5
        };
        var parseResult = new CronParseResult
        {
            Valid = false,
            ErrorMessage = "无效的Cron表达式"
        };
        _mockSchedulerService.Setup(s => s.ParseCron("invalid-cron", 5, "Asia/Shanghai"))
            .Returns(parseResult);

        // Act
        var result = _controller.ParseCron(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ApiResponse<CronParseResult>;
        response!.Success.Should().BeTrue();
        response.Data!.Valid.Should().BeFalse();
    }
}
