using OpenForgeSelf.Backend.Models;
using Newtonsoft.Json;

namespace OpenForgeSelf.Backend.Tests.E2E.ApiClient;

/// <summary>
/// AI 聊天 API 客户端
/// </summary>
public class AIChatClient : BaseApiClient
{
    public AIChatClient(HttpClient client, string? baseUrl = null) : base(client, baseUrl)
    {
    }

    /// <summary>
    /// 发送聊天消息
    /// </summary>
    public async Task<ChatResponse?> SendMessageAsync(string message, string? sessionId = null)
    {
        var request = new ChatRequest
        {
            Message = message,
            SessionId = sessionId ?? Guid.NewGuid().ToString("N"),
            Stream = false
        };

        return await PostAsync<ChatResponse>("api/chat", request);
    }

    /// <summary>
    /// 发送聊天消息并获取流式响应
    /// </summary>
    public async IAsyncEnumerable<string> SendMessageStreamAsync(string message, string? sessionId = null)
    {
        var request = new ChatRequest
        {
            Message = message,
            SessionId = sessionId ?? Guid.NewGuid().ToString("N"),
            Stream = true
        };

        var content = new StringContent(JsonConvert.SerializeObject(request), System.Text.Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("api/chat/stream", content);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line?.StartsWith("data: ") == true)
            {
                var data = line.Substring(6);
                string? contentValue = null;
                bool isDone = false;
                
                try
                {
                    var obj = JsonConvert.DeserializeObject<StreamData>(data);
                    isDone = obj?.Done == true;
                    contentValue = obj?.Content;
                }
                catch
                {
                    // 忽略解析错误
                }

                if (isDone)
                    yield break;
                    
                if (!string.IsNullOrEmpty(contentValue))
                    yield return contentValue;
            }
        }
    }

    /// <summary>
    /// 获取会话历史消息
    /// </summary>
    public async Task<List<ChatResponse>> GetHistoryAsync(string sessionId, int limit = 50)
    {
        return await GetAsync<List<ChatResponse>>($"api/chat/history/{sessionId}", new Dictionary<string, string> { ["limit"] = limit.ToString() })
            ?? new List<ChatResponse>();
    }

    /// <summary>
    /// 删除会话消息
    /// </summary>
    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        var response = await DeleteAsync<dynamic>($"api/chat/session/{sessionId}");
        return response != null;
    }

    private class StreamData
    {
        [JsonProperty("content")]
        public string? Content { get; set; }

        [JsonProperty("done")]
        public bool Done { get; set; }

        [JsonProperty("sessionId")]
        public string? SessionId { get; set; }
    }
}
