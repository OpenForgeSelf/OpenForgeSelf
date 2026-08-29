using FluentAssertions;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// TodoService 单元测试（覆盖 CRUD、状态切换、校验、分页过滤）
/// 依赖 XCodeTestFixture 为 TodoTracker 连接建表。
/// </summary>
[Collection("XCode")]
public class TodoServiceTests : IClassFixture<XCodeTestFixture>
{
    private readonly TodoService _service = new();

    public TodoServiceTests(XCodeTestFixture fixture)
    {
        // fixture 已为 TodoTracker 连接建立 Todo 表
    }

    [Fact]
    public async Task CreateTodo_WithValidTitle_ShouldReturnPendingDto()
    {
        var request = new CreateTodoRequest { Title = "写单元测试", Remark = "覆盖 TodoService" };

        var dto = await _service.CreateTodoAsync(request);

        dto.Should().NotBeNull();
        dto!.Id.Should().BeGreaterThan(0);
        dto.Title.Should().Be("写单元测试");
        dto.Remark.Should().Be("覆盖 TodoService");
        dto.Status.Should().Be("Pending");
        dto.CreatedAt.Should().BeAfter(DateTime.MinValue);
    }

    [Fact]
    public async Task CreateTodo_WithEmptyTitle_ShouldThrowArgumentException()
    {
        var request = new CreateTodoRequest { Title = "   " };

        var act = async () => await _service.CreateTodoAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*标题*");
    }

    [Fact]
    public async Task CreateTodo_WithTooLongTitle_ShouldThrowArgumentException()
    {
        var request = new CreateTodoRequest { Title = new string('A', 201) };

        var act = async () => await _service.CreateTodoAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*200*");
    }

    [Fact]
    public async Task GetTodoById_AfterCreate_ShouldReturnSameTodo()
    {
        var created = await _service.CreateTodoAsync(new CreateTodoRequest { Title = "查询测试" });

        var fetched = await _service.GetTodoByIdAsync(created!.Id);

        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.Title.Should().Be("查询测试");
    }

    [Fact]
    public async Task GetTodoById_WithNonExistentId_ShouldReturnNull()
    {
        var fetched = await _service.GetTodoByIdAsync(99999);

        fetched.Should().BeNull();
    }

    [Fact]
    public async Task GetTodos_WithStatusFilter_ShouldReturnOnlyMatching()
    {
        await _service.CreateTodoAsync(new CreateTodoRequest { Title = "待办A" });
        var completed = await _service.CreateTodoAsync(new CreateTodoRequest { Title = "待办B" });
        await _service.CompleteTodoAsync(completed!.Id);

        var pending = await _service.GetTodosAsync("Pending");
        var done = await _service.GetTodosAsync("Completed");

        pending.Items.Should().OnlyContain(t => t.Status == "Pending");
        done.Items.Should().OnlyContain(t => t.Status == "Completed");
        done.Items.Should().Contain(t => t.Id == completed.Id);
    }

    [Fact]
    public async Task GetTodos_ShouldDefaultSortByCreatedAtDesc()
    {
        var first = await _service.CreateTodoAsync(new CreateTodoRequest { Title = "先建" });
        var second = await _service.CreateTodoAsync(new CreateTodoRequest { Title = "后建" });

        var result = await _service.GetTodosAsync();

        result.Items[0].Id.Should().Be(second!.Id);
        result.Items[1].Id.Should().Be(first!.Id);
    }

    [Fact]
    public async Task UpdateTodo_ShouldChangeFieldsAndKeepStatus()
    {
        var created = await _service.CreateTodoAsync(new CreateTodoRequest { Title = "旧标题", Remark = "旧备注" });

        var updated = await _service.UpdateTodoAsync(created!.Id, new UpdateTodoRequest { Title = "新标题" });

        updated.Should().NotBeNull();
        updated!.Title.Should().Be("新标题");
        updated.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task UpdateTodo_WithNonExistentId_ShouldReturnNull()
    {
        var updated = await _service.UpdateTodoAsync(99999, new UpdateTodoRequest { Title = "x" });

        updated.Should().BeNull();
    }

    [Fact]
    public async Task DeleteTodo_ShouldRemoveAndReturnTrue()
    {
        var created = await _service.CreateTodoAsync(new CreateTodoRequest { Title = "待删除" });

        var deleted = await _service.DeleteTodoAsync(created!.Id);
        var after = await _service.GetTodoByIdAsync(created.Id);

        deleted.Should().BeTrue();
        after.Should().BeNull();
    }

    [Fact]
    public async Task DeleteTodo_WithNonExistentId_ShouldReturnFalse()
    {
        var deleted = await _service.DeleteTodoAsync(99999);

        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteTodo_ShouldSetCompletedAndRecordTime()
    {
        var created = await _service.CreateTodoAsync(new CreateTodoRequest { Title = "待完成" });

        var completed = await _service.CompleteTodoAsync(created!.Id);

        completed.Should().NotBeNull();
        completed!.Status.Should().Be("Completed");
        completed.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteTodo_WithNonExistentId_ShouldReturnNull()
    {
        var completed = await _service.CompleteTodoAsync(99999);

        completed.Should().BeNull();
    }

    [Fact]
    public async Task ReopenTodo_ShouldSetPendingAndClearCompletedAt()
    {
        var created = await _service.CreateTodoAsync(new CreateTodoRequest { Title = "重开测试" });
        await _service.CompleteTodoAsync(created!.Id);

        var reopened = await _service.ReopenTodoAsync(created.Id);

        reopened.Should().NotBeNull();
        reopened!.Status.Should().Be("Pending");
        reopened.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task ReopenTodo_WithNonExistentId_ShouldReturnNull()
    {
        var reopened = await _service.ReopenTodoAsync(99999);

        reopened.Should().BeNull();
    }
}
