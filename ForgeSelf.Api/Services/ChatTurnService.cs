using ForgeSelf.Api.Entities;
using ForgeSelf.Abstractions;
using NewLife;
using NewLife.Log;
using XCode;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 聊天轮次（会话内一轮请求-响应）服务接口。原聊天记录服务改名而来。
/// </summary>
public interface IChatTurnService
{
    /// <summary>
    /// 保存轮次（首次插入）。
    /// </summary>
    Task SaveTurnAsync(ChatTurn turn);

    /// <summary>
    /// 写入轮次：若 Id 已存在（流式期间已插入首条）则更新，否则插入。
    /// 用于流式响应「同一请求 Id 追加更新」的场景。
    /// </summary>
    Task UpsertTurnAsync(ChatTurn turn);

    /// <summary>
    /// 获取单条轮次
    /// </summary>
    Task<ChatTurn> GetByIdAsync(long id);
}

/// <summary>
/// 聊天轮次服务实现
/// </summary>
public class ChatTurnService : IChatTurnService
{
    private readonly ILogService _logService;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ChatTurnService(ILogService logService)
    {
        _logService = logService;
    }

    /// <summary>
    /// 保存轮次
    /// </summary>
    public Task SaveTurnAsync(ChatTurn turn)
    {
        try
        {
            turn.Insert();
            _logService.Info("聊天轮次已保存，ID: {0}, 会话: {1}, TurnIndex: {2}, Style: {3}", turn.Id, turn.SessionKey, turn.TurnIndex, turn.Style);
        }
        catch (Exception ex)
        {
            // 持久化是尽力而为（best-effort）：保存失败绝不影响对外接口响应
            // （非流式路径已在拿到 AI 结果后才保存；流式路径的 SSE 响应已先行返回）。
            // 例如 RequestBody/ResponseBody 超长被 XCode 校验拦截时，仅记录日志，不向上抛出异常。
            _logService.Error("保存聊天轮次失败（不影响接口响应）: {0}", ex.Message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 写入轮次：若 Id 已存在（流式期间已插入首条）则更新，否则插入。
    /// 用于流式响应「同一请求 Id 追加更新」的场景。
    /// </summary>
    public Task UpsertTurnAsync(ChatTurn turn)
    {
        try
        {
            if (turn.Id > 0)
                turn.Update();
            else
                turn.Insert();
        }
        catch (Exception ex)
        {
            // 同 SaveTurnAsync：持久化尽力而为，失败仅记录日志，不向上抛。
            _logService.Error("写入聊天轮次失败（不影响接口响应）: {0}", ex.Message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取单条轮次
    /// </summary>
    public Task<ChatTurn> GetByIdAsync(long id)
    {
        try
        {
            var turn = ChatTurn.FindById(id);
            if (turn == null)
            {
                _logService.Warn("聊天轮次不存在，ID: {0}", id);
            }
            return Task.FromResult(turn!);
        }
        catch (Exception ex)
        {
            _logService.Error("获取聊天轮次失败: {0}", ex.Message);
            throw;
        }
    }
}
