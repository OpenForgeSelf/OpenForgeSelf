using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife.Log;

namespace ForgeSelf.Api.Services.AI.Proxy;

/// <summary>
/// 一次已完成的网关往返，供旁路落库。只含值拷贝，不持有 HttpContext。
/// </summary>
public sealed class LlmTurnAuditWork
{
    public required string Style { get; init; }

    public required string Path { get; init; }

    public int StatusCode { get; init; }

    public bool Stream { get; init; }

    public string? OutboundJson { get; init; }

    public string ResponseText { get; init; } = "";

    public long DurationMs { get; init; }

    public Dictionary<string, string> Headers { get; init; } = new();
}

/// <summary>
/// 旁路记录入口。Enqueue 必须立刻返回：队列满则丢弃，不得等待落库。
/// </summary>
public interface ILlmTurnAudit
{
    void Enqueue(LlmTurnAuditWork work);
}

public interface ILlmTurnAuditStore
{
    Task SaveAsync(LlmTurnAuditWork work, CancellationToken cancellationToken);
}

/// <summary>
/// 单读者后台队列。SQLite 重建表、校验失败、广播失败都只留在这条线程上。
/// </summary>
public sealed class LlmTurnAuditQueue : BackgroundService, ILlmTurnAudit
{
    private readonly Channel<LlmTurnAuditWork> _channel;
    private readonly Func<LlmTurnAuditWork, CancellationToken, Task> _save;

    public LlmTurnAuditQueue(IServiceScopeFactory scopes)
        : this(async (work, cancellationToken) =>
        {
            await using var scope = scopes.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<ILlmTurnAuditStore>();
            await store.SaveAsync(work, cancellationToken);
        })
    {
    }

    internal LlmTurnAuditQueue(Func<LlmTurnAuditWork, CancellationToken, Task> save)
    {
        _save = save;
        _channel = Channel.CreateBounded<LlmTurnAuditWork>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void Enqueue(LlmTurnAuditWork work)
    {
        if (!_channel.Writer.TryWrite(work))
            XTrace.WriteLine("LLM 旁路记录队列已满，已丢弃一条（不影响响应）");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var work in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await _save(work, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    XTrace.WriteLine($"LLM 旁路记录失败（不影响响应）: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

/// <summary>把一条旁路快照写成会话和轮次。失败由队列吞掉。</summary>
public sealed class LlmTurnAuditStore : ILlmTurnAuditStore
{
    private readonly ILogService _log;
    private readonly IChatTurnService _turns;
    private readonly IChatTurnStreamRecorder _recorder;
    private readonly IChatSessionService _sessions;

    public LlmTurnAuditStore(
        ILogService log,
        IChatTurnService turns,
        IChatTurnStreamRecorder recorder,
        IChatSessionService sessions)
    {
        _log = log;
        _turns = turns;
        _recorder = recorder;
        _sessions = sessions;
    }

    public async Task SaveAsync(LlmTurnAuditWork work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JsonNode? outbound = null;
        if (!string.IsNullOrWhiteSpace(work.OutboundJson))
        {
            try { outbound = JsonNode.Parse(work.OutboundJson); }
            catch { /* 报文不是 JSON 时仍记状态码和原文 */ }
        }

        var http = new DefaultHttpContext();
        foreach (var pair in work.Headers)
            http.Request.Headers[pair.Key] = pair.Value;

        var model = outbound is JsonObject obj ? LlmJson.Str(obj["model"]) : null;
        var key = ChatSessionResolver.ResolveConversationKey(http.Request, outbound);
        var preview = ChatSessionResolver.ExtractFirstUserMessage(outbound);
        var session = await _sessions.UpsertSessionAsync(
            key, SessionSource.Proxy, model, ChatSessionResolver.DeriveClientKind(http.Request),
            work.Style, preview, 0, provider: null);

        var headers = JsonSerializer.Serialize(work.Headers, ForgeJson.Default);

        var turn = new ChatTurn
        {
            ChatSessionId = session.Id,
            TurnIndex = session.RequestCount,
            SessionKey = session.SessionKey,
            UserPreview = ChatSessionResolver.Truncate(preview, 500),
            Style = work.Style,
            Model = model,
            RequestMethod = "POST",
            RequestPath = work.Path,
            RequestHeaders = headers,
            RequestBody = work.OutboundJson,
            ResponseStatus = work.StatusCode,
            ResponseBody = work.ResponseText,
            DurationMs = work.DurationMs,
            CreatedTime = DateTime.Now
        };

        var streamSession = work.Stream ? await _recorder.BeginAsync(turn) : null;
        if (streamSession != null)
            await streamSession.CompleteAsync(work.ResponseText, work.StatusCode, work.DurationMs);
        else
            await _turns.SaveTurnAsync(turn);
        await _sessions.RecordTurnStatsAsync(session.Id, work.StatusCode, 0, 0);
    }
}
