using OpenForgeSelf.Backend.Entities;
using NewLife;
using NewLife.Log;
using XCode;

namespace OpenForgeSelf.Backend.Services;

/// <summary>聊天会话服务接口</summary>
public interface IChatSessionService
{
    /// <summary>幂等 upsert 会话（按 SessionKey）。返回会话实体。</summary>
    Task<ChatSession> UpsertSessionAsync(string key, SessionSource source, string? model, ClientKind clientKind, string? style, string? firstUserMsg, int messageCount, string? provider = null);

    /// <summary>分页查询会话（可按来源/客户端/风格/关键字过滤）。</summary>
    Task<(List<ChatSession> Sessions, int Total)> GetSessionsAsync(string? source, string? clientKind, string? style, DateTime? from, DateTime? to, string? key, int page, int pageSize);

    /// <summary>获取会话及其轮次明细。</summary>
    Task<(ChatSession? Session, List<ChatTurn> Turns)> GetSessionAsync(long id);

    /// <summary>轮次完成时回写会话级统计（末状态 + 累计 tokens）。best-effort，失败仅记日志。</summary>
    Task RecordTurnStatsAsync(long sessionId, int status, int promptTokens, int completionTokens);
}

/// <summary>聊天会话服务实现</summary>
public class ChatSessionService : IChatSessionService
{
    private readonly ILogService _logService;

    public ChatSessionService(ILogService logService)
    {
        _logService = logService;
    }

    public Task<ChatSession> UpsertSessionAsync(string key, SessionSource source, string? model, ClientKind clientKind, string? style, string? firstUserMsg, int messageCount, string? provider = null)
    {
        try
        {
            var session = ChatSession.FindBySessionKey(key);
            var isNew = session == null;
            session ??= new ChatSession { SessionKey = key, CreatedTime = DateTime.Now };

            session.Source = source.ToString();
            session.ClientKind = clientKind.ToString();
            if (!string.IsNullOrEmpty(model)) session.Model = model;
            if (!string.IsNullOrEmpty(style)) session.Style = style;
            if (!string.IsNullOrEmpty(provider)) session.Provider = provider;
            if (messageCount > 0) session.MessageCount = messageCount;
            session.UpdatedTime = DateTime.Now;

            if (isNew)
            {
                session.Title = ChatSessionResolver.Truncate(firstUserMsg, 200) ?? ChatSessionResolver.Truncate(model, 200);
                session.FirstUserMsg = ChatSessionResolver.Truncate(firstUserMsg, 500);
                // 每次调用代表一轮（代理=一次请求；app=一次收发），首轮即从 1 开始
                session.RequestCount = 1;
                session.TotalPromptTokens = 0;
                session.TotalCompletionTokens = 0;
                session.LastStatus = 0;
            }
            else
            {
                // 后续轮次累加；尽量复用入参 messageCount（app 传完整消息数更准）
                if (messageCount > 0) session.MessageCount = messageCount;
                session.RequestCount += 1;
            }

            session.Save();
            return Task.FromResult(session);
        }
        catch (Exception ex)
        {
            _logService.Error("UpsertSession 失败: {0}", ex.Message);
            // 会话归属失败不能影响主流程：返回一个内存态会话，调用方仍可继续写轮次（ChatSessionId=0）
            return Task.FromResult(new ChatSession { SessionKey = key, Source = source.ToString(), ClientKind = clientKind.ToString() });
        }
    }

    public Task<(List<ChatSession> Sessions, int Total)> GetSessionsAsync(string? source, string? clientKind, string? style, DateTime? from, DateTime? to, string? key, int page, int pageSize)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var where = new WhereExpression();
            if (!string.IsNullOrEmpty(source)) where &= ChatSession._.Source == source;
            if (!string.IsNullOrEmpty(clientKind)) where &= ChatSession._.ClientKind == clientKind;
            if (!string.IsNullOrEmpty(style)) where &= ChatSession._.Style == style;
            var start = from ?? DateTime.MinValue;
            var end = to ?? DateTime.MaxValue;
            where &= ChatSession._.CreatedTime.Between(start, end);
            if (!string.IsNullOrEmpty(key))
                where &= ChatSession._.SessionKey.StartsWith(key)
                      | ChatSession._.Title.Contains(key)
                      | ChatSession._.FirstUserMsg.Contains(key);

            var total = (int)ChatSession.FindCount(where);
            var sessions = ChatSession.FindAll(where)
                .OrderByDescending(s => s.UpdatedTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult((sessions, total));
        }
        catch (Exception ex)
        {
            _logService.Error("查询会话列表失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<(ChatSession? Session, List<ChatTurn> Turns)> GetSessionAsync(long id)
    {
        try
        {
            var session = ChatSession.FindById(id);
            if (session == null) return Task.FromResult<(ChatSession?, List<ChatTurn>)>((null, new List<ChatTurn>()));

            var turns = ChatTurn.FindAllByChatSessionId(id)
                .OrderBy(t => t.TurnIndex)
                .ToList();
            return Task.FromResult((session, turns));
        }
        catch (Exception ex)
        {
            _logService.Error("查询会话详情失败: {0}", ex.Message);
            throw;
        }
    }

    public Task RecordTurnStatsAsync(long sessionId, int status, int promptTokens, int completionTokens)
    {
        try
        {
            if (sessionId <= 0) return Task.CompletedTask;
            var session = ChatSession.FindById(sessionId);
            if (session == null) return Task.CompletedTask;
            session.LastStatus = status;
            session.TotalPromptTokens += promptTokens;
            session.TotalCompletionTokens += completionTokens;
            session.UpdatedTime = DateTime.Now;
            session.Save();
        }
        catch (Exception ex)
        {
            _logService.Error("回写会话统计失败: {0}", ex.Message);
        }
        return Task.CompletedTask;
    }
}
