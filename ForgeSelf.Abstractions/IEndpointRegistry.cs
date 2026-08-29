namespace ForgeSelf.Abstractions;

/// <summary>端点注册接缝：宿主桥接到实际 HTTP 路由；返回的句柄用于卸载时移除。</summary>
public interface IEndpointRegistry
{
    IDisposable Map(string pattern, Delegate handler);
}
