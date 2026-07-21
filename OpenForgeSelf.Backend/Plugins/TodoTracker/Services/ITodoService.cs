using OpenForgeSelf.Backend.Plugins.TodoTracker.Models;

namespace OpenForgeSelf.Backend.Plugins.TodoTracker.Services;

public interface ITodoService
{
    Task<PagedResult<TodoDto>> GetTodosAsync(string? status = null, int page = 1, int pageSize = 20);
    Task<TodoDto?> GetTodoByIdAsync(int id);
    Task<TodoDto> CreateTodoAsync(CreateTodoRequest request);
    Task<TodoDto?> UpdateTodoAsync(int id, UpdateTodoRequest request);
    Task<bool> DeleteTodoAsync(int id);
    Task<TodoDto?> CompleteTodoAsync(int id);
    Task<TodoDto?> ReopenTodoAsync(int id);
}
