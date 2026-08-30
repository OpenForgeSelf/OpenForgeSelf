using ForgeSelf.Api.Plugins.QuickLinks.Entities;
using ForgeSelf.Api.Plugins.QuickLinks.Models;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;

namespace ForgeSelf.Api.Plugins.QuickLinks.Services;

public class QuickLinkService : IQuickLinkService
{
    public Task<PagedResult<QuickLinkDto>> GetLinksAsync(long? categoryId = null, string? keyword = null, int page = 1, int pageSize = 20)
    {
        try
        {
            XTrace.Log.Debug("获取快捷链接列表，categoryId={0}, keyword={1}, page={2}, pageSize={3}", categoryId, keyword, page, pageSize);

            var exp = new WhereExpression();

            if (categoryId.HasValue)
            {
                exp &= QuickLink._.CategoryId == categoryId.Value;
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                exp &= (QuickLink._.Name.Contains(kw) | QuickLink._.Url.Contains(kw) | QuickLink._.Description.Contains(kw));
            }

            var pageParam = new PageParameter
            {
                PageIndex = page - 1,
                PageSize = pageSize,
                Sort = "SortOrder, Id",
                RetrieveTotalCount = true
            };

            var list = QuickLink.FindAll(exp, pageParam);
            var total = (int)pageParam.TotalCount;

            var categoryIds = list.Where(l => l.CategoryId > 0).Select(l => l.CategoryId).Distinct().ToList();
            var categories = new Dictionary<long, QuickLinkCategory>();
            foreach (var catId in categoryIds)
            {
                var cat = QuickLinkCategory.FindById(catId);
                if (cat != null)
                {
                    categories[catId] = cat;
                }
            }

            var items = list.Select(l => new QuickLinkDto
            {
                Id = l.Id,
                Name = l.Name,
                Url = l.Url,
                Icon = l.Icon,
                Description = l.Description,
                CategoryId = l.CategoryId > 0 ? l.CategoryId : null,
                CategoryName = l.CategoryId > 0 && categories.ContainsKey(l.CategoryId)
                    ? categories[l.CategoryId].Name
                    : null,
                SortOrder = l.SortOrder,
                ClickCount = l.ClickCount,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt
            }).ToList();

            XTrace.Log.Info("获取快捷链接列表成功，总数: {0}, 当前页数量: {1}", total, items.Count);

            return Task.FromResult(new PagedResult<QuickLinkDto>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取快捷链接列表失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<QuickLinkDto?> GetLinkByIdAsync(long id)
    {
        try
        {
            XTrace.Log.Debug("获取快捷链接详情，id={0}", id);

            var link = QuickLink.FindById(id);
            if (link == null)
            {
                return Task.FromResult<QuickLinkDto?>(null);
            }

            string? categoryName = null;
            if (link.CategoryId > 0)
            {
                var category = QuickLinkCategory.FindById(link.CategoryId);
                categoryName = category?.Name;
            }

            return Task.FromResult<QuickLinkDto?>(new QuickLinkDto
            {
                Id = link.Id,
                Name = link.Name,
                Url = link.Url,
                Icon = link.Icon,
                Description = link.Description,
                CategoryId = link.CategoryId > 0 ? link.CategoryId : null,
                CategoryName = categoryName,
                SortOrder = link.SortOrder,
                ClickCount = link.ClickCount,
                CreatedAt = link.CreatedAt,
                UpdatedAt = link.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取快捷链接详情失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<QuickLinkDto> CreateLinkAsync(CreateQuickLinkRequest request)
    {
        try
        {
            XTrace.Log.Info("创建快捷链接，Name={0}, Url={1}", request.Name, request.Url);

            var now = DateTime.Now;
            var link = new QuickLink
            {
                Name = request.Name,
                Url = request.Url,
                Icon = request.Icon,
                Description = request.Description,
                CategoryId = request.CategoryId ?? 0,
                SortOrder = request.SortOrder,
                ClickCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            link.Insert();

            XTrace.Log.Info("快捷链接创建成功，Id={0}", link.Id);

            return Task.FromResult(new QuickLinkDto
            {
                Id = link.Id,
                Name = link.Name,
                Url = link.Url,
                Icon = link.Icon,
                Description = link.Description,
                CategoryId = link.CategoryId > 0 ? link.CategoryId : null,
                SortOrder = link.SortOrder,
                ClickCount = link.ClickCount,
                CreatedAt = link.CreatedAt,
                UpdatedAt = link.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("创建快捷链接失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<QuickLinkDto?> UpdateLinkAsync(long id, UpdateQuickLinkRequest request)
    {
        try
        {
            XTrace.Log.Info("更新快捷链接，id={0}", id);

            var link = QuickLink.FindById(id);
            if (link == null)
            {
                return Task.FromResult<QuickLinkDto?>(null);
            }

            link.Name = request.Name;
            link.Url = request.Url;
            link.Icon = request.Icon;
            link.Description = request.Description;
            link.CategoryId = request.CategoryId ?? 0;
            link.SortOrder = request.SortOrder;
            link.UpdatedAt = DateTime.Now;

            link.Update();

            XTrace.Log.Info("快捷链接更新成功，Id={0}", id);

            return Task.FromResult<QuickLinkDto?>(new QuickLinkDto
            {
                Id = link.Id,
                Name = link.Name,
                Url = link.Url,
                Icon = link.Icon,
                Description = link.Description,
                CategoryId = link.CategoryId > 0 ? link.CategoryId : null,
                SortOrder = link.SortOrder,
                ClickCount = link.ClickCount,
                CreatedAt = link.CreatedAt,
                UpdatedAt = link.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新快捷链接失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<bool> DeleteLinkAsync(long id)
    {
        try
        {
            XTrace.Log.Info("删除快捷链接，id={0}", id);

            var link = QuickLink.FindById(id);
            if (link == null)
            {
                return Task.FromResult(false);
            }

            link.Delete();

            XTrace.Log.Info("快捷链接删除成功，Id={0}", id);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("删除快捷链接失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<bool> IncrementClickCountAsync(long id)
    {
        try
        {
            XTrace.Log.Debug("增加快捷链接点击数，id={0}", id);

            var link = QuickLink.FindById(id);
            if (link == null)
            {
                return Task.FromResult(false);
            }

            link.ClickCount++;
            link.UpdatedAt = DateTime.Now;
            link.Update();

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("增加点击数失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<List<QuickLinkCategoryDto>> GetCategoriesAsync()
    {
        try
        {
            XTrace.Log.Debug("获取快捷链接分类列表");

            var categories = QuickLinkCategory.FindAll()
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Id)
                .ToList();

            var allLinks = QuickLink.FindAll();
            var linkCounts = allLinks
                .Where(l => l.CategoryId > 0)
                .GroupBy(l => l.CategoryId)
                .ToDictionary(g => g.Key, g => g.Count());

            var items = categories.Select(c => new QuickLinkCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Icon = c.Icon,
                SortOrder = c.SortOrder,
                CreatedAt = c.CreatedAt,
                LinkCount = linkCounts.ContainsKey(c.Id) ? linkCounts[c.Id] : 0
            }).ToList();

            XTrace.Log.Info("获取快捷链接分类列表成功，数量: {0}", items.Count);
            return Task.FromResult(items);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取快捷链接分类列表失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<QuickLinkCategoryDto> CreateCategoryAsync(CreateCategoryRequest request)
    {
        try
        {
            XTrace.Log.Info("创建快捷链接分类，Name={0}", request.Name);

            var now = DateTime.Now;
            var category = new QuickLinkCategory
            {
                Name = request.Name,
                Icon = request.Icon,
                SortOrder = request.SortOrder,
                CreatedAt = now
            };

            category.Insert();

            XTrace.Log.Info("快捷链接分类创建成功，Id={0}", category.Id);

            return Task.FromResult(new QuickLinkCategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Icon = category.Icon,
                SortOrder = category.SortOrder,
                CreatedAt = category.CreatedAt,
                LinkCount = 0
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("创建快捷链接分类失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<QuickLinkCategoryDto?> UpdateCategoryAsync(long id, UpdateCategoryRequest request)
    {
        try
        {
            XTrace.Log.Info("更新快捷链接分类，id={0}", id);

            var category = QuickLinkCategory.FindById(id);
            if (category == null)
            {
                return Task.FromResult<QuickLinkCategoryDto?>(null);
            }

            category.Name = request.Name;
            category.Icon = request.Icon;
            category.SortOrder = request.SortOrder;

            category.Update();

            XTrace.Log.Info("快捷链接分类更新成功，Id={0}", id);

            var linkCount = QuickLink.FindCount(QuickLink._.CategoryId == id);

            return Task.FromResult<QuickLinkCategoryDto?>(new QuickLinkCategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Icon = category.Icon,
                SortOrder = category.SortOrder,
                CreatedAt = category.CreatedAt,
                LinkCount = (int)linkCount
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新快捷链接分类失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<bool> DeleteCategoryAsync(long id)
    {
        try
        {
            XTrace.Log.Info("删除快捷链接分类，id={0}", id);

            var category = QuickLinkCategory.FindById(id);
            if (category == null)
            {
                return Task.FromResult(false);
            }

            QuickLink.Update(QuickLink._.CategoryId == 0, QuickLink._.CategoryId == id);

            category.Delete();

            XTrace.Log.Info("快捷链接分类删除成功，Id={0}", id);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("删除快捷链接分类失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<bool> ReorderLinksAsync(List<long> orderedIds)
    {
        try
        {
            XTrace.Log.Info("重新排序快捷链接，数量={0}", orderedIds.Count);

            if (orderedIds.Count == 0)
            {
                return Task.FromResult(true);
            }

            var exp = new WhereExpression();
            foreach (var id in orderedIds)
            {
                exp |= QuickLink._.Id == id;
            }

            var links = QuickLink.FindAll(exp);
            var linkDict = links.ToDictionary(l => l.Id);

            for (int i = 0; i < orderedIds.Count; i++)
            {
                if (linkDict.TryGetValue(orderedIds[i], out var link))
                {
                    link.SortOrder = i;
                    link.UpdatedAt = DateTime.Now;
                    link.Update();
                }
            }

            XTrace.Log.Info("快捷链接重新排序成功");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("重新排序快捷链接失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<int> ImportLinksAsync(ImportLinksRequest request)
    {
        try
        {
            XTrace.Log.Info("导入快捷链接，数量={0}, 模式={1}", request.Links.Count, request.Mode);

            if (request.Mode == "replace")
            {
                var allLinks = QuickLink.FindAll();
                foreach (var link in allLinks)
                {
                    link.Delete();
                }
            }

            var now = DateTime.Now;
            var categoryCache = new Dictionary<string, QuickLinkCategory>();
            int sortOrder = (int)QuickLink.FindCount();

            foreach (var item in request.Links)
            {
                long categoryId = 0;

                if (!string.IsNullOrWhiteSpace(item.CategoryName))
                {
                    if (!categoryCache.TryGetValue(item.CategoryName, out var category))
                    {
                        category = QuickLinkCategory.FindAll(QuickLinkCategory._.Name == item.CategoryName).FirstOrDefault();

                        if (category == null)
                        {
                            category = new QuickLinkCategory
                            {
                                Name = item.CategoryName,
                                SortOrder = categoryCache.Count,
                                CreatedAt = now
                            };
                            category.Insert();
                        }

                        categoryCache[item.CategoryName] = category;
                    }

                    categoryId = category.Id;
                }

                var link = new QuickLink
                {
                    Name = item.Name,
                    Url = item.Url,
                    Icon = item.Icon,
                    Description = item.Description,
                    CategoryId = categoryId,
                    SortOrder = sortOrder++,
                    ClickCount = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                link.Insert();
            }

            XTrace.Log.Info("快捷链接导入成功，数量={0}", request.Links.Count);
            return Task.FromResult(request.Links.Count);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("导入快捷链接失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<List<QuickLinkDto>> ExportLinksAsync(long? categoryId = null)
    {
        try
        {
            XTrace.Log.Info("导出快捷链接，categoryId={0}", categoryId);

            var exp = new WhereExpression();

            if (categoryId.HasValue)
            {
                exp &= QuickLink._.CategoryId == categoryId.Value;
            }

            var links = QuickLink.FindAll(exp)
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.Id)
                .ToList();

            var categoryIds = links.Where(l => l.CategoryId > 0).Select(l => l.CategoryId).Distinct().ToList();
            var categories = new Dictionary<long, QuickLinkCategory>();
            foreach (var catId in categoryIds)
            {
                var cat = QuickLinkCategory.FindById(catId);
                if (cat != null)
                {
                    categories[catId] = cat;
                }
            }

            var items = links.Select(l => new QuickLinkDto
            {
                Id = l.Id,
                Name = l.Name,
                Url = l.Url,
                Icon = l.Icon,
                Description = l.Description,
                CategoryId = l.CategoryId > 0 ? l.CategoryId : null,
                CategoryName = l.CategoryId > 0 && categories.ContainsKey(l.CategoryId)
                    ? categories[l.CategoryId].Name
                    : null,
                SortOrder = l.SortOrder,
                ClickCount = l.ClickCount,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt
            }).ToList();

            XTrace.Log.Info("快捷链接导出成功，数量={0}", items.Count);
            return Task.FromResult(items);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("导出快捷链接失败: {0}", ex.Message);
            throw;
        }
    }
}
