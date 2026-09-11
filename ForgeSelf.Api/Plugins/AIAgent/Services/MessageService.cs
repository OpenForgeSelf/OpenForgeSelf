using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using NewLife.Data;
using NewLife.Log;
using XCode;
using AIChatMessageEntity = ForgeSelf.Api.Plugins.AIAgent.Entities.AIChatMessage;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

public interface IPluginMessageService
{
    Task<long> SaveMessageAsync(string sessionId, string role, string content);
    Task<long> SaveMessageAsync(string sessionId, string role, string content, string? toolCallsJson);
    Task<List<ChatMessageModel>> GetHistoryAsync(string sessionId, int limit = 50);
    Task DeleteSessionAsync(string sessionId);
}

public class PluginMessageService : IPluginMessageService
{
    public Task<long> SaveMessageAsync(string sessionId, string role, string content)
        => SaveMessageAsync(sessionId, role, content, null);

    public Task<long> SaveMessageAsync(string sessionId, string role, string content, string? toolCallsJson)
    {
        try
        {
            var message = new AIChatMessageEntity
            {
                SessionId = sessionId,
                Role = role,
                Content = content,
                ToolCallsJson = toolCallsJson ?? string.Empty,
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };

            message.Insert();

            XTrace.Log.Info("[AIAgentPlugin] 消息已保存，ID: {0}, SessionId: {1}, Role: {2}, 工具轨迹: {3}", message.Id, sessionId, role, toolCallsJson?.Length ?? 0);

            return Task.FromResult(message.Id);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 保存消息失败: {0}", ex.Message);
            throw;
        }
    }

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
}
