using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NewLife.Log;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 后端 WebSocket 广播器（单例）。
/// 维护所有连接到 /ws 的客户端，用于把实时事件（如聊天记录流式分片）推送给前端。
/// </summary>
public interface IWebSocketBroadcaster
{
    void Add(WebSocket socket);
    void Remove(WebSocket socket);
    Task BroadcastAsync(string eventType, object payload, CancellationToken cancellationToken = default);
}

public class WebSocketBroadcaster : IWebSocketBroadcaster
{
    private readonly ConcurrentDictionary<WebSocket, byte> _sockets = new();

    public void Add(WebSocket socket) => _sockets[socket] = 0;

    public void Remove(WebSocket socket) => _sockets.TryRemove(socket, out _);

    public async Task BroadcastAsync(string eventType, object payload, CancellationToken cancellationToken = default)
    {
        if (_sockets.IsEmpty)
            return;

        var envelope = new { type = eventType, data = payload };
        var json = JsonSerializer.Serialize(envelope);
        var bytes = Encoding.UTF8.GetBytes(json);

        foreach (var socket in _sockets.Keys)
        {
            if (socket.State != WebSocketState.Open)
                continue;

            try
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("WebSocket 广播失败（已忽略并移除）: {0}", ex.Message);
                Remove(socket);
            }
        }
    }
}
