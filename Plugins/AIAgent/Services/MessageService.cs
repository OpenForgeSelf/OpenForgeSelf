using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using NewLife.Data;
using NewLife.Log;
using XCode;
using AIChatMessageEntity = ForgeSelf.Api.Plugins.AIAgent.Entities.AIChatMessage;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <remarks>
/// B5（041）：本接口<b>只提供读</b>——写路径唯一走宿主会话事件日志（<c>ISessionStore</c>），
/// 插件 <c>AIChatMessage</c> 表降级为只读投影（由 <see cref="AIAgentProjectionService"/> 从日志重投影）。
/// 旧的 <c>SaveMessageAsync</c> 直写方法已删除：迁移后无任何调用方，留着就是未来旁路的种子
/// （QA 定性：IM 网关经 IChatCompletion 直写投影表，会与日志前缀对齐互相覆盖 → 数据丢失）。
/// </remarks>
public interface IPluginMessageService
{
    Task<List<ChatMessageModel>> GetHistoryAsync(string sessionId, int limit = 50);
    Task<List<SessionSummaryModel>> GetSessionsAsync(SessionArchivedFilter filter = SessionArchivedFilter.Active);
    Task DeleteSessionAsync(string sessionId);

    /// <summary>归档 / 取消归档会话（软标记，不删消息）。返回归档状态是否发生变更。</summary>
    Task<bool> ArchiveSessionAsync(string sessionId, bool archived);
}

public class PluginMessageService : IPluginMessageService
{
    public Task<List<ChatMessageModel>> GetHistoryAsync(string sessionId, int limit = 50)
    {
        try
        {
            var page = new PageParameter
            {
                PageIndex = 0,
                PageSize = limit
            };

            var messages = AIChatMessageEntity.FindAll(AIChatMessageEntity._.SessionId == sessionId, page);

            XTrace.Log.Info("[AIAgentPlugin] 获取会话历史消息，SessionId: {0}, 数量: {1}", sessionId, messages.Count);

            return Task.FromResult(messages.Select(m => new ChatMessageModel
            {
                Id = m.Id,
                SessionId = m.SessionId,
                Role = m.Role,
                Content = m.Content,
                CreateTime = m.CreateTime,
                UpdateTime = m.UpdateTime,
                ToolCallsJson = m.ToolCallsJson
            }).ToList());
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 获取历史消息失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<List<SessionSummaryModel>> GetSessionsAsync(SessionArchivedFilter filter = SessionArchivedFilter.Active)
    {
        try
        {
            // 直查 DB（铁律 11：列表/聚合查询不走实体缓存，避免跨执行上下文串扰）
            var all = AIChatMessageEntity.FindAll();

            // 会话级归档标记：一次取全量集合，避免逐会话查询（N+1）
            var archivedIds = AIChatSession.GetArchivedSessionIds();

            var sessions = all
                .GroupBy(m => m.SessionId)
                .Select(g => new SessionSummaryModel
                {
                    SessionId = g.Key,
                    MessageCount = g.Count(),
                    LastTime = g.Max(m => m.CreateTime),
                    // 标题取该会话首条 user 消息正文前 20 字；无 user 消息则回退「新会话」
                    Title = TakeTitle(g.Where(m => m.Role == "user").OrderBy(m => m.CreateTime).FirstOrDefault()?.Content),
                    Archived = archivedIds.Contains(g.Key)
                })
                .Where(s => filter switch
                {
                    SessionArchivedFilter.Active => !s.Archived,
                    SessionArchivedFilter.Archived => s.Archived,
                    _ => true
                })
                .OrderByDescending(s => s.LastTime)
                .ToList();

            XTrace.Log.Info("[AIAgentPlugin] 列出会话，筛选: {0}, 数量: {1}", filter, sessions.Count);
            return Task.FromResult(sessions);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 列出会话失败: {0}", ex.Message);
            throw;
        }
    }

    /// <summary>从首条 user 消息取标题（前 20 字，去换行；空则「新会话」）。</summary>
    private static string TakeTitle(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "新会话";
        var flat = content.Replace("\r", " ").Replace("\n", " ").Trim();
        return flat.Length <= 20 ? flat : flat.Substring(0, 20) + "…";
    }

    public Task DeleteSessionAsync(string sessionId)
    {
        try
        {
            var messages = AIChatMessageEntity.FindAll(AIChatMessageEntity._.SessionId == sessionId);

            foreach (var message in messages)
            {
                message.Delete();
            }

            XTrace.Log.Info("[AIAgentPlugin] 删除会话消息，SessionId: {0}, 数量: {1}", sessionId, messages.Count);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 删除会话消息失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<bool> ArchiveSessionAsync(string sessionId, bool archived)
    {
        try
        {
            // 归档为软标记：只改 AIChatSession.Archived，不删任何消息（区别于 DeleteSessionAsync 的硬删）
            var changed = AIChatSession.SetArchived(sessionId, archived);

            XTrace.Log.Info("[AIAgentPlugin] 归档会话，SessionId: {0}, Archived: {1}, 变更: {2}", sessionId, archived, changed);
            return Task.FromResult(changed);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 归档会话失败: {0}", ex.Message);
            throw;
        }
    }
}
