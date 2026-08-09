using System.Text;
using NewLife.Log;
using OpenForgeSelf.Backend.Entities;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// 聊天记录流式录制器。
/// 在代理 LLM 流式响应时，一边由控制器把分片回给调用方，
/// 一边通过本服务把纯文本增量落库（每满 1000 字符或 500ms 落一次）+ WebSocket 推送给前端实时展示。
/// 同一请求生成唯一 RequestId，按该 Id 追加更新同一条记录；流结束只保留最后一批（完整响应体）。
/// </summary>
public interface IChatRecordStreamRecorder
{
    Task<ChatRecordStreamSession> BeginAsync(ChatRecord record);
}

public class ChatRecordStreamRecorder : IChatRecordStreamRecorder
{
    private readonly IChatRecordService _service;
    private readonly IWebSocketBroadcaster _broadcaster;

    public ChatRecordStreamRecorder(IChatRecordService service, IWebSocketBroadcaster broadcaster)
    {
        _service = service;
        _broadcaster = broadcaster;
    }

    public async Task<ChatRecordStreamSession> BeginAsync(ChatRecord record)
    {
        var session = new ChatRecordStreamSession(record, _service, _broadcaster);
        await session.InitializeAsync();
        return session;
    }
}

/// <summary>
/// 单次流式请求的会话状态：持有记录引用、累加纯文本、节流落库、推送前端。
/// </summary>
public class ChatRecordStreamSession
{
    private readonly IChatRecordService _service;
    private readonly IWebSocketBroadcaster _broadcaster;
    private readonly StringBuilder _sb = new();
    private int _flushedLength;
    private DateTime _lastFlush = DateTime.UtcNow;
    private bool _finalized;

    public ChatRecord Record { get; }
    public string RequestId { get; }

    public ChatRecordStreamSession(ChatRecord record, IChatRecordService service, IWebSocketBroadcaster broadcaster)
    {
        Record = record;
        _service = service;
        _broadcaster = broadcaster;
        RequestId = Guid.NewGuid().ToString("N");
        Record.RequestId = RequestId;
    }

    internal async Task InitializeAsync()
    {
        // 首条插入：让记录立即落库（含 RequestId），前端可尽早关联到该请求。
        await _service.UpsertRecordAsync(Record);
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
            sessionId = Record.SessionId,
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
        Record.ResponseText = _sb.ToString();
        await _service.UpsertRecordAsync(Record);
    }

    /// <summary>
    /// 流正常结束：用完整原始响应体覆盖、定稿，并通知前端「完成」。
    /// </summary>
    public async Task CompleteAsync(string finalResponseBody, int status, long durationMs)
    {
        if (_finalized)
            return;
        _finalized = true;

        Record.ResponseBody = finalResponseBody;
        Record.ResponseText = _sb.ToString();
        Record.ResponseStatus = status;
        Record.DurationMs = durationMs;
        Record.RequestId = RequestId;
        await _service.UpsertRecordAsync(Record);

        await _broadcaster.BroadcastAsync("chat_record_completed", new
        {
            requestId = RequestId,
            sessionId = Record.SessionId,
            recordId = Record.Id,
            isDone = true
        });
    }

    /// <summary>
    /// 流异常结束：保留已增量写入的纯文本，把错误体定稿，并通知前端「完成（错误）」。
    /// 调用方需先把 Record.ResponseBody 写为错误 JSON。
    /// </summary>
    public async Task FailAsync(int status, long durationMs)
    {
        if (_finalized)
            return;
        _finalized = true;

        Record.ResponseText = _sb.ToString();
        Record.ResponseStatus = status;
        Record.DurationMs = durationMs;
        Record.RequestId = RequestId;
        await _service.UpsertRecordAsync(Record);

        await _broadcaster.BroadcastAsync("chat_record_completed", new
        {
            requestId = RequestId,
            sessionId = Record.SessionId,
            recordId = Record.Id,
            isDone = true,
            error = true
        });
    }
}
