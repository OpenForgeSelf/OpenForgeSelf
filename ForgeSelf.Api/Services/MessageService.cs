using ForgeSelf.Api.Entities;
using ForgeSelf.Abstractions;
using ChatMessageModel = ForgeSelf.Api.Models.ChatMessageModel;
using NewLife;
using NewLife.Log;
using XCode;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 消息持久化服务接口
/// </summary>
/// <remarks>
/// B6（040 §2.5 随批项）：删除 <c>SaveMessageAsync</c>——生产调用方已为 0（B4 写路径改序后控制器不再双写，
/// 插件侧同名方法 B5 已删），直写 <c>ChatMessage</c> 的实现留着就是下一代旁路的种子。
/// 本接口自 B4 起退化为只读（<c>ChatMessage</c> 是会话日志的只读投影），写路径唯一走 <c>ISessionStore.Append</c>。
/// </remarks>
public interface IMessageService
{
    /// <summary>
    /// 获取会话历史消息
    /// </summary>
    Task<List<ChatMessageModel>> GetHistoryAsync(string sessionId, int limit = 50);

    /// <summary>
    /// 删除会话消息
    /// </summary>
    Task DeleteSessionAsync(string sessionId);
}

/// <summary>
/// 消息持久化服务实现
/// </summary>
public class MessageService : IMessageService
{
    private readonly ILogService _logService;

    /// <summary>
    /// 构造函数
    /// </summary>
    public MessageService(ILogService logService)
    {
        _logService = logService;
    }

    /// <summary>
    /// 获取会话历史消息
    /// </summary>
    public Task<List<ChatMessageModel>> GetHistoryAsync(string sessionId, int limit = 50)
    {
        try
        {
            // B4（040）：ChatMessage 已降级为会话日志的只读投影，行由 SessionProjectionService 按日志顺序写入，
            // 行主键升序 == 日志顺序；按 CreateTime 排序在同一毫秒内会串位，改为按 Id 稳定排序。
            var messages = ChatMessage.FindAll(ChatMessage._.SessionId == sessionId)
                .OrderBy(m => m.Id)
                .Take(limit)
                .ToList();

            _logService.Info("获取会话历史消息，SessionId: {0}, 数量: {1}", sessionId, messages.Count);

            return Task.FromResult(messages.Select(m => new ChatMessageModel
            {
                Id = m.Id,
                SessionId = m.SessionId,
                Role = m.Role,
                Content = m.Content,
                CreateTime = m.CreateTime,
                UpdateTime = m.UpdateTime
            }).ToList());
        }
        catch (Exception ex)
        {
            _logService.Error("获取历史消息失败: {0}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 删除会话消息
    /// </summary>
    public Task DeleteSessionAsync(string sessionId)
    {
        try
        {
            var messages = ChatMessage.FindAll(ChatMessage._.SessionId == sessionId);

            foreach (var message in messages)
            {
                message.Delete();
            }

            _logService.Info("删除会话消息，SessionId: {0}, 数量: {1}", sessionId, messages.Count);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logService.Error("删除会话消息失败: {0}", ex.Message);
            throw;
        }
    }
}
