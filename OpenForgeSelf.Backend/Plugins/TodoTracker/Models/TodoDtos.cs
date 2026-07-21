namespace OpenForgeSelf.Backend.Plugins.TodoTracker.Models;

public class TodoDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class CreateTodoRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public DateTime? DueDate { get; set; }
}

public class UpdateTodoRequest
{
    public string? Title { get; set; }
    public string? Remark { get; set; }
    public DateTime? DueDate { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
