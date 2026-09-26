using ForgeSelf.Api.Plugins.QuickLinks.Models;

namespace ForgeSelf.Api.Plugins.QuickLinks.Services;

public interface IQuickLinkService
{
    Task<PagedResult<QuickLinkDto>> GetLinksAsync(long? categoryId = null, string? keyword = null, int page = 1, int pageSize = 20);
    Task<QuickLinkDto?> GetLinkByIdAsync(long id);
    Task<QuickLinkDto> CreateLinkAsync(CreateQuickLinkRequest request);
    Task<QuickLinkDto?> UpdateLinkAsync(long id, UpdateQuickLinkRequest request);
    Task<bool> DeleteLinkAsync(long id);
    Task<bool> IncrementClickCountAsync(long id);
    Task<List<QuickLinkCategoryDto>> GetCategoriesAsync();
    Task<QuickLinkCategoryDto> CreateCategoryAsync(CreateCategoryRequest request);
    Task<QuickLinkCategoryDto?> UpdateCategoryAsync(long id, UpdateCategoryRequest request);
    Task<bool> DeleteCategoryAsync(long id);
    Task<bool> ReorderLinksAsync(List<long> orderedIds);
    Task<int> ImportLinksAsync(ImportLinksRequest request);
    Task<List<QuickLinkDto>> ExportLinksAsync(long? categoryId = null);
}
