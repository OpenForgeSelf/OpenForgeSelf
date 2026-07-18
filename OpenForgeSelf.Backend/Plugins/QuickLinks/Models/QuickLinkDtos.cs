namespace OpenForgeSelf.Backend.Plugins.QuickLinks.Models;

public class QuickLinkDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int SortOrder { get; set; }
    public int ClickCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class QuickLinkCategoryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LinkCount { get; set; }
}

public class CreateQuickLinkRequest
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public long? CategoryId { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateQuickLinkRequest
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public long? CategoryId { get; set; }
    public int SortOrder { get; set; }
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
}

public class ReorderLinksRequest
{
    public List<long> OrderedIds { get; set; } = new();
}

public class ImportLinksRequest
{
    public List<ImportLinkItem> Links { get; set; } = new();
    public string Mode { get; set; } = "append";
}

public class ImportLinkItem
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? CategoryName { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
