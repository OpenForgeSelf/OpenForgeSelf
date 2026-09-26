using System.Text.Json;
using CodeSnippetEntity = ForgeSelf.Api.Plugins.ScriptRunner.Entities.CodeSnippet;
using ScriptEntity = ForgeSelf.Api.Plugins.ScriptRunner.Entities.Script;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.ScriptRunner.Models;
using NewLife.Data;
using NewLife.Log;
using XCode;

namespace ForgeSelf.Api.Plugins.ScriptRunner.Services;

public class CodeSnippetService : ICodeSnippetService
{
    public Task<CodeSnippet> CreateSnippetAsync(CreateCodeSnippetRequest request)
    {
        var now = DateTime.Now;
        var entity = new CodeSnippetEntity
        {
            Title = request.Title,
            Description = request.Description,
            Code = request.Code,
            Language = request.Language,
            Category = request.Category,
            TagsJson = JsonSerializer.Serialize(request.Tags ?? []),
            IsFavorite = false,
            CreatedAt = now,
            UpdatedAt = now,
            UsageCount = 0,
            Source = (int)request.Source,
            SourceScriptId = request.SourceScriptId ?? 0
        };

        entity.Insert();

        XTrace.Log.Info("[CodeSnippetService] 创建代码片段成功，ID: {0}, 标题: {1}", entity.Id, entity.Title);
        return Task.FromResult(MapToCodeSnippet(entity));
    }

    public Task<CodeSnippet?> UpdateSnippetAsync(long id, UpdateCodeSnippetRequest request)
    {
        var entity = CodeSnippetEntity.FindById(id);
        if (entity == null)
        {
            return Task.FromResult<CodeSnippet?>(null);
        }

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.Code = request.Code;
        entity.Language = request.Language;
        entity.Category = request.Category;
        entity.TagsJson = JsonSerializer.Serialize(request.Tags ?? []);
        entity.UpdatedAt = DateTime.Now;

        entity.Update();

        XTrace.Log.Info("[CodeSnippetService] 更新代码片段成功，ID: {0}, 标题: {1}", id, entity.Title);
        return Task.FromResult<CodeSnippet?>(MapToCodeSnippet(entity));
    }

    public Task<bool> DeleteSnippetAsync(long id)
    {
        var entity = CodeSnippetEntity.FindById(id);
        if (entity == null)
        {
            return Task.FromResult(false);
        }

        entity.Delete();

        XTrace.Log.Info("[CodeSnippetService] 删除代码片段成功，ID: {0}, 标题: {1}", id, entity.Title);
        return Task.FromResult(true);
    }

    public Task<CodeSnippet?> GetSnippetAsync(long id)
    {
        var entity = CodeSnippetEntity.FindById(id);
        return Task.FromResult(entity == null ? null : MapToCodeSnippet(entity));
    }

    public Task<CodeSnippetListResponse> ListSnippetsAsync(
        string? keyword = null,
        string? language = null,
        string? category = null,
        bool? isFavorite = null,
        int page = 1,
        int pageSize = 20)
    {
        var exp = new WhereExpression();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            exp &= (CodeSnippetEntity._.Title.Contains(kw) | CodeSnippetEntity._.Description.Contains(kw) | CodeSnippetEntity._.TagsJson.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            exp &= CodeSnippetEntity._.Language == language;
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            exp &= CodeSnippetEntity._.Category == category;
        }

        if (isFavorite.HasValue)
        {
            exp &= CodeSnippetEntity._.IsFavorite == isFavorite.Value;
        }

        var pageParam = new PageParameter
        {
            PageIndex = page - 1,
            PageSize = pageSize,
            Sort = CodeSnippetEntity._.UpdatedAt.Name,
            Desc = true
        };

        var list = CodeSnippetEntity.FindAll(exp, pageParam);
        var total = (int)pageParam.TotalCount;

        return Task.FromResult(new CodeSnippetListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = list.Select(MapToCodeSnippet).ToList()
        });
    }

    public Task<bool> FavoriteSnippetAsync(long id, bool isFavorite)
    {
        var entity = CodeSnippetEntity.FindById(id);
        if (entity == null)
        {
            return Task.FromResult(false);
        }

        entity.IsFavorite = isFavorite;
        entity.UpdatedAt = DateTime.Now;
        entity.Update();

        XTrace.Log.Info("[CodeSnippetService] 收藏状态切换成功，ID: {0}, IsFavorite: {1}", id, isFavorite);
        return Task.FromResult(true);
    }

    public Task<bool> IncrementUsageAsync(long id)
    {
        var entity = CodeSnippetEntity.FindById(id);
        if (entity == null)
        {
            return Task.FromResult(false);
        }

        entity.UsageCount++;
        entity.LastUsedAt = DateTime.Now;
        entity.Update();

        return Task.FromResult(true);
    }

    public Task<List<string>> GetLanguagesAsync()
    {
        var allSnippets = CodeSnippetEntity.FindAll();

        var languages = allSnippets
            .Select(s => s.Language)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        return Task.FromResult(languages);
    }

    public Task<List<string>> GetCategoriesAsync()
    {
        var allSnippets = CodeSnippetEntity.FindAll();

        var categories = allSnippets
            .Select(s => s.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        return Task.FromResult(categories);
    }

    public Task<CodeSnippet?> CreateFromScriptAsync(long scriptId, string? title = null)
    {
        var script = ScriptEntity.FindById(scriptId);
        if (script == null)
        {
            XTrace.Log.Warn("[CodeSnippetService] 脚本不存在，无法创建片段，scriptId: {0}", scriptId);
            return Task.FromResult<CodeSnippet?>(null);
        }

        var snippetTitle = !string.IsNullOrWhiteSpace(title) ? title : script.Name;
        var now = DateTime.Now;

        var entity = new CodeSnippetEntity
        {
            Title = snippetTitle,
            Description = script.Description,
            Code = script.Code,
            Language = ((ScriptLanguage)script.Language).ToString(),
            Category = script.Category,
            TagsJson = script.TagsJson,
            IsFavorite = false,
            CreatedAt = now,
            UpdatedAt = now,
            UsageCount = 0,
            Source = (int)CodeSnippetSource.Script,
            SourceScriptId = scriptId
        };

        entity.Insert();

        XTrace.Log.Info("[CodeSnippetService] 从脚本创建代码片段成功，ID: {0}, 来源脚本: {1}", entity.Id, scriptId);
        return Task.FromResult<CodeSnippet?>(MapToCodeSnippet(entity));
    }

    private static CodeSnippet MapToCodeSnippet(CodeSnippetEntity entity)
    {
        var tags = new List<string>();
        try
        {
            if (!string.IsNullOrWhiteSpace(entity.TagsJson))
            {
                tags = JsonSerializer.Deserialize<List<string>>(entity.TagsJson) ?? [];
            }
        }
        catch
        {
            tags = [];
        }

        return new CodeSnippet
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            Code = entity.Code,
            Language = entity.Language,
            Category = entity.Category,
            Tags = tags,
            IsFavorite = entity.IsFavorite,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            UsageCount = entity.UsageCount,
            LastUsedAt = entity.LastUsedAt == DateTime.MinValue ? null : entity.LastUsedAt,
            Source = (CodeSnippetSource)entity.Source,
            SourceScriptId = entity.SourceScriptId
        };
    }
}
