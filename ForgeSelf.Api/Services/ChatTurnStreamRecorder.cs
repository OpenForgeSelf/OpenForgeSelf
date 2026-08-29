using System.Text;
using NewLife.Log;
using ForgeSelf.Api.Entities;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 聊天轮次流式录制器。
/// 在代理 LLM 流式响应时，一边由控制器把分片回给调用方，
/// 一边通过本服务把纯文本增量落库（每满 1000 字符或 500ms 落一次）+ WebSocket 推送给前端实时展示。
/// 同一请求生成唯一 RequestId，按该 Id 追加更新同一条轮次；流结束只保留最后一批（完整响应体）。
/// 轮次归属的会话（ChatSessionId/ChatSessionId/SessionKey/TurnIndex）由控制器在调用前填好。
/// </summary>
public interface IChatTurnStreamRecorder
{
    Task<ChatTurnStreamSession> BeginAsync(ChatTurn turn);
}

public class ChatTurnStreamRecorder : IChatTurnStreamRecorder
{
    private readonly IChatTurnService _service;
    private readonly IWebSocketBroadcaster _broadcaster;

    public ChatTurnStreamRecorder(IChatTurnService service, IWebSocketBroadcaster broadcaster)
    {
        _service = service;
        _broadcaster = broadcaster;
    }

    public async Task<ChatTurnStreamSession> BeginAsync(ChatTurn turn)
    {
        var session = new ChatTurnStreamSession(turn, _service, _broadcaster);
        await session.InitializeAsync();
        return session;
    }
}

/// <summary>
/// 单次流式请求的会话状态：持有轮次引用、累加纯文本、节流落库、推送前端。
/// </summary>
public class ChatTurnStreamSession
{
    private readonly IChatTurnService _service;
    private readonly IWebSocketBroadcaster _broadcaster;
    private readonly StringBuilder _sb = new();
    private int _flushedLength;
    private DateTime _lastFlush = DateTime.UtcNow;
    private bool _finalized;

    public ChatTurn Turn { get; }
    public string RequestId { get; }

    public ChatTurnStreamSession(ChatTurn turn, IChatTurnService service, IWebSocketBroadcaster broadcaster)
    {
        Turn = turn;
        _service = service;
        _broadcaster = broadcaster;
        RequestId = Guid.NewGuid().ToString("N");
        Turn.RequestId = RequestId;
    }

    internal async Task InitializeAsync()
    {
        // 首条插入：让轮次立即落库（含 RequestId），前端可尽早关联到该请求。
        await _service.UpsertTurnAsync(Turn);
    }

    /// <summary>
    /// 收到一个流式分片：累加到纯文本、推送前端、按节流条件增量落库。
    /// </summary>
    public async Task AppendChunkAsync(string? text)
    {
        if (_finalized)
            return;
        if (string.IsNullOrEmpty(text))
            return;

        _sb.Append(text);
        await _broadcaster.BroadcastAsync("chat_record_chunk", new
        {
            requestId = RequestId,
            sessionId = Turn.SessionKey,
            text,
            isDone = false
        });

        if (_sb.Length - _flushedLength >= 1000 || (DateTime.UtcNow - _lastFlush).TotalMilliseconds >= 500)
            await FlushAsync();
    }

    private async Task FlushAsync()
    {
        _flushedLength = _sb.Length;
        _lastFlush = DateTime.UtcNow;
        Turn.ResponseText = _sb.ToString();
        await _service.UpsertTurnAsync(Turn);
    }

    /// <summary>
    /// 流正常结束：用完整原始响应体覆盖、定稿，并通知前端「完成」。
    /// </summary>
    public async Task CompleteAsync(string finalResponseBody, int status, long durationMs)
    {
        if (_finalized)
            return;
        _finalized = true;

        Turn.ResponseBody = finalResponseBody;
        Turn.ResponseText = _sb.ToString();
        Turn.ResponseStatus = status;
        Turn.DurationMs = durationMs;
        Turn.RequestId = RequestId;
        await _service.UpsertTurnAsync(Turn);

        await _broadcaster.BroadcastAsync("chat_record_completed", new
        {
            requestId = RequestId,
            sessionId = Turn.SessionKey,
            recordId = Turn.Id,
            isDone = true
        });
    }

    /// <summary>
    /// 流异常结束：保留已增量写入的纯文本，把错误体定稿，并通知前端「完成（错误）」。
    /// 调用方需先把 Turn.ResponseBody 写为错误 JSON。
    /// </summary>
    public async Task FailAsync(int status, long durationMs)
    {
        if (_finalized)
            return;
        _finalized = true;

        Turn.ResponseText = _sb.ToString();
        Turn.ResponseStatus = status;
        Turn.DurationMs = durationMs;
        Turn.RequestId = RequestId;
        await _service.UpsertTurnAsync(Turn);

        await _broadcaster.BroadcastAsync("chat_record_completed", new
        {
            requestId = RequestId,
            sessionId = Turn.SessionKey,
            recordId = Turn.Id,
            isDone = true,
            error = true
        });
    }
}
