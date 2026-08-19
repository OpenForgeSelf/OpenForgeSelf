using System.Text.Json;
using ScriptEntity = OpenForgeSelf.Backend.Plugins.ScriptRunner.Entities.Script;
using ScriptExecutionEntity = OpenForgeSelf.Backend.Plugins.ScriptRunner.Entities.ScriptExecution;
using OpenForgeSelf.Abstractions;
using NewLife.Data;
using NewLife.Log;
using XCode;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;

public class ScriptService : IScriptService
{
    public Task<Script> CreateScriptAsync(CreateScriptRequest request)
    {
        var now = DateTime.Now;
        var entity = new ScriptEntity
        {
            Name = request.Name,
            Description = request.Description,
            Language = (int)request.Language,
            Code = request.Code,
            Category = request.Category,
            TagsJson = JsonSerializer.Serialize(request.Tags ?? []),
            IsFavorite = false,
            ParametersJson = JsonSerializer.Serialize(request.Parameters ?? []),
            CreatedAt = now,
            UpdatedAt = now,
            UsageCount = 0,
            TimeoutSeconds = request.TimeoutSeconds
        };

        entity.Insert();

        XTrace.Log.Info("[ScriptService] 创建脚本成功，ID: {0}, 名称: {1}", entity.Id, entity.Name);
        return Task.FromResult(MapToScript(entity));
    }

    public Task<Script?> UpdateScriptAsync(long id, UpdateScriptRequest request)
    {
        var entity = ScriptEntity.FindById(id);
        if (entity == null)
        {
            return Task.FromResult<Script?>(null);
        }

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Language = (int)request.Language;
        entity.Code = request.Code;
        entity.Category = request.Category;
        entity.TagsJson = JsonSerializer.Serialize(request.Tags ?? []);
        entity.ParametersJson = JsonSerializer.Serialize(request.Parameters ?? []);
        entity.UpdatedAt = DateTime.Now;
        entity.TimeoutSeconds = request.TimeoutSeconds;

        entity.Update();

        XTrace.Log.Info("[ScriptService] 更新脚本成功，ID: {0}, 名称: {1}", id, entity.Name);
        return Task.FromResult<Script?>(MapToScript(entity));
    }

    public Task<bool> DeleteScriptAsync(long id)
    {
        var entity = ScriptEntity.FindById(id);
        if (entity == null)
        {
            return Task.FromResult(false);
        }

        entity.Delete();

        XTrace.Log.Info("[ScriptService] 删除脚本成功，ID: {0}, 名称: {1}", id, entity.Name);
        return Task.FromResult(true);
    }

    public Task<Script?> GetScriptAsync(long id)
    {
        var entity = ScriptEntity.FindById(id);
        return Task.FromResult(entity == null ? null : MapToScript(entity));
    }

    public Task<ScriptListResponse> ListScriptsAsync(string? keyword = null, string? category = null, ScriptLanguage? language = null, bool? isFavorite = null, int page = 1, int pageSize = 20)
    {
        var exp = new WhereExpression();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            exp &= (ScriptEntity._.Name.Contains(kw) | ScriptEntity._.Description.Contains(kw) | ScriptEntity._.TagsJson.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            exp &= ScriptEntity._.Category == category;
        }

        if (language.HasValue)
        {
            exp &= ScriptEntity._.Language == (int)language.Value;
        }

        if (isFavorite.HasValue)
        {
            exp &= ScriptEntity._.IsFavorite == isFavorite.Value;
        }

        var pageParam = new PageParameter
        {
            PageIndex = page - 1,
            PageSize = pageSize,
            Sort = ScriptEntity._.UpdatedAt.Name,
            Desc = true
        };

        var list = ScriptEntity.FindAll(exp, pageParam);
        var total = (int)pageParam.TotalCount;

        return Task.FromResult(new ScriptListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = list.Select(MapToScript).ToList()
        });
    }

    public Task<bool> FavoriteScriptAsync(long id, bool isFavorite)
    {
        var entity = ScriptEntity.FindById(id);
        if (entity == null)
        {
            return Task.FromResult(false);
        }

        entity.IsFavorite = isFavorite;
        entity.UpdatedAt = DateTime.Now;

        entity.Update();

        XTrace.Log.Info("[ScriptService] 脚本收藏状态变更，ID: {0}, IsFavorite: {1}", id, isFavorite);
        return Task.FromResult(true);
    }

    public Task<bool> IncrementUsageAsync(long id)
    {
        var entity = ScriptEntity.FindById(id);
        if (entity == null)
        {
            return Task.FromResult(false);
        }

        entity.UsageCount++;
        entity.LastUsedAt = DateTime.Now;

        entity.Update();
        return Task.FromResult(true);
    }

    public Task<List<string>> GetCategoriesAsync()
    {
        var allScripts = ScriptEntity.FindAll();

        var categories = allScripts
            .Where(s => !string.IsNullOrEmpty(s.Category))
            .Select(s => s.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        return Task.FromResult(categories);
    }

    public Task<List<string>> GetTagsAsync()
    {
        var allScripts = ScriptEntity.FindAll();
        var allTags = allScripts.Select(s => s.TagsJson).ToList();

        var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tagsJson in allTags)
        {
            try
            {
                var tags = JsonSerializer.Deserialize<List<string>>(tagsJson);
                if (tags != null)
                {
                    foreach (var tag in tags)
                    {
                        if (!string.IsNullOrWhiteSpace(tag))
                        {
                            tagSet.Add(tag.Trim());
                        }
                    }
                }
            }
            catch
            {
            }
        }

        return Task.FromResult(tagSet.OrderBy(t => t).ToList());
    }

    public Task<ExecutionListResponse> ListExecutionsAsync(long? scriptId = null, ScriptExecutionStatus? status = null, int page = 1, int pageSize = 20)
    {
        var exp = new WhereExpression();

        if (scriptId.HasValue)
        {
            exp &= ScriptExecutionEntity._.ScriptId == scriptId.Value;
        }

        if (status.HasValue)
        {
            exp &= ScriptExecutionEntity._.Status == (int)status.Value;
        }

        var pageParam = new PageParameter
        {
            PageIndex = page - 1,
            PageSize = pageSize,
            Sort = ScriptExecutionEntity._.StartTime.Name,
            Desc = true
        };

        var list = ScriptExecutionEntity.FindAll(exp, pageParam);
        var total = (int)pageParam.TotalCount;

        return Task.FromResult(new ExecutionListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = list.Select(MapToExecution).ToList()
        });
    }

    private static Script MapToScript(ScriptEntity entity)
    {
        return new Script
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Language = (ScriptLanguage)entity.Language,
            Code = entity.Code,
            Category = entity.Category,
            Tags = JsonSerializer.Deserialize<List<string>>(entity.TagsJson) ?? [],
            IsFavorite = entity.IsFavorite,
            Parameters = JsonSerializer.Deserialize<List<ScriptParameter>>(entity.ParametersJson) ?? [],
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            UsageCount = entity.UsageCount,
            LastUsedAt = entity.LastUsedAt == DateTime.MinValue ? null : entity.LastUsedAt,
            TimeoutSeconds = entity.TimeoutSeconds
        };
    }

    private static ScriptExecution MapToExecution(ScriptExecutionEntity entity)
    {
        return new ScriptExecution
        {
            Id = entity.Id,
            ScriptId = entity.ScriptId,
            ScriptName = entity.ScriptName,
            Status = (ScriptExecutionStatus)entity.Status,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime == DateTime.MinValue ? null : entity.EndTime,
            ExitCode = entity.ExitCode,
            Output = entity.Output,
            ErrorOutput = entity.ErrorOutput,
            DurationMs = entity.DurationMs,
            ParametersJson = entity.ParametersJson,
            OutputLogs = JsonSerializer.Deserialize<List<ScriptExecutionLog>>(entity.OutputLogsJson) ?? []
        };
    }
}
