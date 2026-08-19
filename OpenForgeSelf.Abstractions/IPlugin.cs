using OpenForgeSelf.Core;

namespace OpenForgeSelf.Abstractions;

/// <summary>插件契约：一切贡献（服务/工具/端点/副作用/事件）都在 Apply 内声明；生命周期由宿主 Fiber 统一管理。</summary>
public interface IPlugin
{
    void Apply(IContext ctx);
}
