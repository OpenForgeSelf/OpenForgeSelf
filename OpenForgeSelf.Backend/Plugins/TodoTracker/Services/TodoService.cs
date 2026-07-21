using OpenForgeSelf.Backend.Plugins.TodoTracker.Entities;
using OpenForgeSelf.Backend.Plugins.TodoTracker.Models;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;

namespace OpenForgeSelf.Backend.Plugins.TodoTracker.Services;

public class TodoService : ITodoService
{
    private const int MaxPageSize = 100;

    public Task<PagedResult<TodoDto>> GetTodosAsync(string? status = null, int page = 1, int pageSize = 20)
    {
        try
        {
            XTrace.Log.Debug("获取待办列表，status={0}, page={1}, pageSize={2}", status, page, pageSize);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            var exp = new WhereExpression();

            var statusValue = TodoStatus.ToValue(status);
            if (statusValue.HasValue)
            {
                exp &= Todo._.Status == statusValue.Value;
            }

            var pageParam = new PageParameter
            {
                PageIndex = page - 1,
                PageSize = pageSize,
                Sort = "CreatedAt DESC, Id DESC"
            };

            var list = Todo.FindAll(exp, pageParam);
            var total = (int)pageParam.TotalCount;

            var items = list.Select(ToDto).ToList();

            XTrace.Log.Info("获取待办列表成功，总数: {0}, 当前页数量: {1}", total, items.Count);

            return Task.FromResult(new PagedResult<TodoDto>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取待办列表失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<TodoDto?> GetTodoByIdAsync(int id)
    {
        try
        {
            XTrace.Log.Debug("获取待办详情，id={0}", id);

            var todo = Todo.FindById(id);
            if (todo == null)
            {
                return Task.FromResult<TodoDto?>(null);
            }

            return Task.FromResult<TodoDto?>(ToDto(todo));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取待办详情失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<TodoDto> CreateTodoAsync(CreateTodoRequest request)
    {
        try
        {
            XTrace.Log.Info("创建待办，Title={0}", request.Title);

            ValidateTitle(request.Title);
            ValidateRemark(request.Remark);

            var now = DateTime.Now;
            var todo = new Todo
            {
                Title = request.Title.Trim(),
                Remark = request.Remark,
                Status = TodoStatus.PendingValue,
                DueDate = request.DueDate ?? DateTime.MinValue,
                CreatedAt = now,
                UpdatedAt = now,
                CompletedAt = DateTime.MinValue
            };

            todo.Insert();

            XTrace.Log.Info("待办创建成功，Id={0}", todo.Id);

            return Task.FromResult(ToDto(todo));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("创建待办失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<TodoDto?> UpdateTodoAsync(int id, UpdateTodoRequest request)
    {
        try
        {
            XTrace.Log.Info("更新待办，id={0}", id);

            var todo = Todo.FindById(id);
            if (todo == null)
            {
                return Task.FromResult<TodoDto?>(null);
            }

            if (request.Title != null)
            {
                ValidateTitle(request.Title);
                todo.Title = request.Title.Trim();
            }

            if (request.Remark != null)
            {
                ValidateRemark(request.Remark);
                todo.Remark = request.Remark;
            }

            // DueDate 显式传入（包括 null）时更新
            if (request.DueDate.HasValue)
            {
                todo.DueDate = request.DueDate.Value;
            }

            todo.Update();

            XTrace.Log.Info("待办更新成功，Id={0}", id);

            return Task.FromResult<TodoDto?>(ToDto(todo));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新待办失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<bool> DeleteTodoAsync(int id)
    {
        try
        {
            XTrace.Log.Info("删除待办，id={0}", id);

            var todo = Todo.FindById(id);
            if (todo == null)
            {
                return Task.FromResult(false);
            }

            todo.Delete();

            XTrace.Log.Info("待办删除成功，Id={0}", id);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("删除待办失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<TodoDto?> CompleteTodoAsync(int id)
    {
        try
        {
            XTrace.Log.Info("标记待办完成，id={0}", id);

            var todo = Todo.FindById(id);
            if (todo == null)
            {
                return Task.FromResult<TodoDto?>(null);
            }

            if (todo.Status != TodoStatus.CompletedValue)
            {
                todo.Status = TodoStatus.CompletedValue;
                todo.CompletedAt = DateTime.Now;
                todo.Update();
                XTrace.Log.Info("待办标记完成成功，Id={0}", id);
            }

            return Task.FromResult<TodoDto?>(ToDto(todo));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("标记待办完成失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<TodoDto?> ReopenTodoAsync(int id)
    {
        try
        {
            XTrace.Log.Info("重新打开待办，id={0}", id);

            var todo = Todo.FindById(id);
            if (todo == null)
            {
                return Task.FromResult<TodoDto?>(null);
            }

            if (todo.Status != TodoStatus.PendingValue)
            {
                todo.Status = TodoStatus.PendingValue;
                todo.CompletedAt = DateTime.MinValue;
                todo.Update();
                XTrace.Log.Info("待办重新打开成功，Id={0}", id);
            }

            return Task.FromResult<TodoDto?>(ToDto(todo));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("重新打开待办失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("标题不能为空", nameof(title));
        }
        if (title.Length > 200)
        {
            throw new ArgumentException("标题长度不能超过 200 字符", nameof(title));
        }
    }

    private static void ValidateRemark(string? remark)
    {
        if (remark != null && remark.Length > 1000)
        {
            throw new ArgumentException("备注长度不能超过 1000 字符", nameof(remark));
        }
    }

    private static TodoDto ToDto(Todo todo)
    {
        return new TodoDto
        {
            Id = todo.Id,
            Title = todo.Title,
            Remark = string.IsNullOrEmpty(todo.Remark) ? null : todo.Remark,
            Status = TodoStatus.ToName(todo.Status),
            DueDate = todo.DueDate == DateTime.MinValue ? null : todo.DueDate,
            CreatedAt = todo.CreatedAt,
            UpdatedAt = todo.UpdatedAt,
            CompletedAt = todo.CompletedAt == DateTime.MinValue ? null : todo.CompletedAt
        };
    }
}
