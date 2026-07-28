using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using OpenForgeSelf.Backend.Models.Plugins;
using OpenForgeSelf.Backend.Plugins.TodoTracker.Controllers;
using OpenForgeSelf.Backend.Plugins.TodoTracker.Models;
using OpenForgeSelf.Backend.Plugins.TodoTracker.Services;
using Xunit;

namespace OpenForgeSelf.Backend.Tests.Integration;

/// <summary>
/// TodosController 集成测试（直接构造控制器 + TodoService，依赖 XCodeTestFixture 建表）
/// </summary>
[Collection("XCode")]
public class TodosControllerTests : IClassFixture<XCodeTestFixture>
{
    private readonly TodosController _controller = new(new TodoService());

    public TodosControllerTests(XCodeTestFixture fixture)
    {
    }

    private static async Task<int> CreateTodoAndGetIdAsync(string title)
    {
        var controller = new TodosController(new TodoService());
        var result = await controller.CreateTodo(new CreateTodoRequest { Title = title });
        var created = (ObjectResult)result.Result!;
        return ((ApiResponse<TodoDto>)created.Value!).Data!.Id;
    }

    [Fact]
    public async Task GetTodos_ShouldReturnOkWithPagedResult()
    {
        var result = await _controller.GetTodos();

        result.Result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result.Result!;
        var resp = (ApiResponse<PagedResult<TodoDto>>)ok.Value!;
        resp.Success.Should().BeTrue();
        resp.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTodos_WithInvalidStatus_ShouldReturnBadRequest()
    {
        var result = await _controller.GetTodos(status: "XXInvalid");

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetTodoById_WithExistingId_ShouldReturnOk()
    {
        var id = await CreateTodoAndGetIdAsync("详情查询");

        var result = await _controller.GetTodoById(id);

        result.Result.Should().BeOfType<OkObjectResult>();
        var resp = (ApiResponse<TodoDto>)((OkObjectResult)result.Result!).Value!;
        resp.Data!.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetTodoById_WithNonExistentId_ShouldReturnNotFound()
    {
        var result = await _controller.GetTodoById(99999);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetTodoById_WithNonPositiveId_ShouldReturnBadRequest()
    {
        var result = await _controller.GetTodoById(0);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateTodo_WithValidBody_ShouldReturnCreated()
    {
        var request = new CreateTodoRequest { Title = "控制器创建", Remark = "集成测试" };

        var result = await _controller.CreateTodo(request);

        result.Result.Should().BeOfType<ObjectResult>();
        var created = (ObjectResult)result.Result!;
        created.StatusCode.Should().Be(201);
        var resp = (ApiResponse<TodoDto>)created.Value!;
        resp.Success.Should().BeTrue();
        resp.Data!.Title.Should().Be("控制器创建");
        resp.Data.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task CreateTodo_WithEmptyTitle_ShouldReturnBadRequest()
    {
        var request = new CreateTodoRequest { Title = "   " };

        var result = await _controller.CreateTodo(request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateTodo_WithTooLongTitle_ShouldReturnBadRequest()
    {
        var request = new CreateTodoRequest { Title = new string('A', 201) };

        var result = await _controller.CreateTodo(request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateTodo_WithValidBody_ShouldReturnOk()
    {
        var id = await CreateTodoAndGetIdAsync("更新前");

        var result = await _controller.UpdateTodo(id, new UpdateTodoRequest { Title = "更新后" });

        result.Result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result.Result!;
        var resp = (ApiResponse<TodoDto>)ok.Value!;
        resp.Data!.Title.Should().Be("更新后");
    }

    [Fact]
    public async Task UpdateTodo_WithNonExistentId_ShouldReturnNotFound()
    {
        var result = await _controller.UpdateTodo(99999, new UpdateTodoRequest { Title = "x" });

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteTodo_WithExistingId_ShouldReturnNoContent()
    {
        var id = await CreateTodoAndGetIdAsync("待删除");

        var result = await _controller.DeleteTodo(id);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteTodo_WithNonExistentId_ShouldReturnNotFound()
    {
        var result = await _controller.DeleteTodo(99999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteTodo_WithNonPositiveId_ShouldReturnBadRequest()
    {
        var result = await _controller.DeleteTodo(0);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CompleteTodo_WithNonPositiveId_ShouldReturnBadRequest()
    {
        var result = await _controller.CompleteTodo(0);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CompleteThenReopen_Flow_ShouldFlipStatus()
    {
        var id = await CreateTodoAndGetIdAsync("完成重开流");

        var completed = await _controller.CompleteTodo(id);
        var completedResp = (ApiResponse<TodoDto>)((OkObjectResult)completed.Result!).Value!;
        completedResp.Data!.Status.Should().Be("Completed");

        var reopened = await _controller.ReopenTodo(id);
        var reopenedResp = (ApiResponse<TodoDto>)((OkObjectResult)reopened.Result!).Value!;
        reopenedResp.Data!.Status.Should().Be("Pending");
    }
}
