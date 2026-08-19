using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Abstractions;
using ChatMessageModel = OpenForgeSelf.Backend.Models.ChatMessageModel;
using NewLife;
using NewLife.Log;
using XCode;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// 消息持久化服务接口
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// 保存消息
    /// </summary>
    Task<long> SaveMessageAsync(string sessionId, string role, string content);

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
    /// 保存消息
    /// </summary>
    public Task<long> SaveMessageAsync(string sessionId, string role, string content)
    {
        try
        {
            var message = new ChatMessage
            {
                SessionId = sessionId,
                Role = role,
                Content = content,
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };

            message.Insert();

            _logService.Info("消息已保存，ID: {0}, SessionId: {1}, Role: {2}", message.Id, sessionId, role);

            return Task.FromResult(message.Id);
        }
        catch (Exception ex)
        {
            _logService.Error("保存消息失败: {0}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 获取会话历史消息
    /// </summary>
    public Task<List<ChatMessageModel>> GetHistoryAsync(string sessionId, int limit = 50)
    {
        try
        {
            var messages = ChatMessage.FindAll(ChatMessage._.SessionId == sessionId)
                .OrderBy(m => m.CreateTime)
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
