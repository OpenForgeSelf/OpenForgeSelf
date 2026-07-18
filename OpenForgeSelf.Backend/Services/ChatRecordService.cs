using OpenForgeSelf.Backend.Entities;
using NewLife;
using NewLife.Log;
using XCode;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// 聊天记录服务接口
/// </summary>
public interface IChatRecordService
{
    /// <summary>
    /// 保存记录
    /// </summary>
    Task SaveRecordAsync(ChatRecord record);

    /// <summary>
    /// 获取单条记录
    /// </summary>
    Task<ChatRecord> GetByIdAsync(long id);

    /// <summary>
    /// 分页查询记录
    /// </summary>
    Task<(List<ChatRecord> Records, int Total)> GetRecordsAsync(string? sessionId, string? style, DateTime? from, DateTime? to, int page, int pageSize);
}

/// <summary>
/// 聊天记录服务实现
/// </summary>
public class ChatRecordService : IChatRecordService
{
    private readonly ILogService _logService;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ChatRecordService(ILogService logService)
    {
        _logService = logService;
    }

    /// <summary>
    /// 保存记录
    /// </summary>
    public Task SaveRecordAsync(ChatRecord record)
    {
        try
        {
            record.Insert();
            _logService.Info("聊天记录已保存，ID: {0}, SessionId: {1}, Style: {2}", record.Id, record.SessionId, record.Style);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logService.Error("保存聊天记录失败: {0}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 获取单条记录
    /// </summary>
    public Task<ChatRecord> GetByIdAsync(long id)
    {
        try
        {
            var record = ChatRecord.FindById(id);
            if (record == null)
            {
                _logService.Warn("聊天记录不存在，ID: {0}", id);
            }
            return Task.FromResult(record!);
        }
        catch (Exception ex)
        {
            _logService.Error("获取聊天记录失败: {0}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 分页查询记录
    /// </summary>
    public Task<(List<ChatRecord> Records, int Total)> GetRecordsAsync(string? sessionId, string? style, DateTime? from, DateTime? to, int page, int pageSize)
    {
        try
        {
            var where = new WhereExpression();

            if (!string.IsNullOrEmpty(sessionId))
            {
                where &= ChatRecord._.SessionId == sessionId;
            }

            if (!string.IsNullOrEmpty(style))
            {
                where &= ChatRecord._.Style == style;
            }

            if (from.HasValue)
            {
                where &= ChatRecord._.CreatedTime >= from.Value;
            }

            if (to.HasValue)
            {
                where &= ChatRecord._.CreatedTime <= to.Value;
            }

            var total = (int)ChatRecord.FindCount(where);
            var records = ChatRecord.FindAll(where)
                .OrderByDescending(r => r.CreatedTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            _logService.Info("查询聊天记录，SessionId: {0}, Style: {1}, Total: {2}, Page: {3}, PageSize: {4}",
                sessionId, style, total, page, pageSize);

            return Task.FromResult((records, total));
        }
        catch (Exception ex)
        {
            _logService.Error("查询聊天记录失败: {0}", ex.Message);
            throw;
        }
    }
}