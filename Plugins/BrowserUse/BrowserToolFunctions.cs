using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.BrowserUse;

public abstract class BrowserToolBase : IToolFunctionExtension
{
    private readonly string _pluginId;
    protected readonly BrowserSession Session;

    protected BrowserToolBase(string pluginId, BrowserSession session)
    {
        _pluginId = pluginId;
        Session = session;
    }

    public string Id => $"{_pluginId}.{Name}";
    public abstract string Name { get; }
    public string PluginId => _pluginId;
    public abstract string Description { get; }
    public abstract string ParametersJsonSchema { get; }
    public abstract Task<string> ExecuteAsync(string parameters);
}

public sealed class BrowserOpenTool : BrowserToolBase
{
    public BrowserOpenTool(string pluginId, BrowserSession session) : base(pluginId, session) { }
    public override string Name => "browser_open";
    public override string Description => "启动本机 Chromium（默认有界面，便于人工接管）。可选 url；不在域名白名单则拒绝且不跳转。重复调用复用已打开的浏览器。";
    public override string ParametersJsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "url": { "type": "string", "description": "可选。打开后进入的绝对 http(s) 地址。不在白名单则拒绝。" }
          }
        }
        """;
    public override Task<string> ExecuteAsync(string parameters) => Session.OpenAsync(parameters);
}

public sealed class BrowserNavigateTool : BrowserToolBase
{
    public BrowserNavigateTool(string pluginId, BrowserSession session) : base(pluginId, session) { }
    public override string Name => "browser_navigate";
    public override string Description => "在已打开的浏览器中导航到绝对 http(s) 地址。域名不在白名单时拒绝跳转，并在结果中标记 needsApproval。重定向落到白名单外同样拒绝并退回。";
    public override string ParametersJsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "url": { "type": "string", "description": "绝对 http(s) 地址" }
          },
          "required": ["url"]
        }
        """;
    public override Task<string> ExecuteAsync(string parameters) => Session.NavigateAsync(parameters);
}

public sealed class BrowserSnapshotTool : BrowserToolBase
{
    public BrowserSnapshotTool(string pluginId, BrowserSession session) : base(pluginId, session) { }
    public override string Name => "browser_snapshot";
    public override string Description => "读取当前页的无障碍快照（aria snapshot）。遇到 CAPTCHA 或 SSO 时不返回页面内容，改为需要人工接管。";
    public override string ParametersJsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "selector": { "type": "string", "description": "可选 CSS 选择器，默认 body" }
          }
        }
        """;
    public override Task<string> ExecuteAsync(string parameters) => Session.SnapshotAsync(parameters);
}

public sealed class BrowserClickTool : BrowserToolBase
{
    public BrowserClickTool(string pluginId, BrowserSession session) : base(pluginId, session) { }
    public override string Name => "browser_click";
    public override string Description => "点击选择器对应元素。提交、支付、删除类点击属于高风险工具 browser_click，调用前需审批。点击若跳到白名单外的地址会被退回并标记 needsApproval。CAPTCHA/SSO 页面上拒绝点击。";
    public override string ParametersJsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "selector": { "type": "string", "description": "要点击的 CSS 选择器" },
            "button": { "type": "string", "enum": ["left", "right", "middle"], "description": "鼠标按键，默认 left" }
          },
          "required": ["selector"]
        }
        """;
    public override Task<string> ExecuteAsync(string parameters) => Session.ClickAsync(parameters);
}

public sealed class BrowserFillTool : BrowserToolBase
{
    public BrowserFillTool(string pluginId, BrowserSession session) : base(pluginId, session) { }
    public override string Name => "browser_fill";
    public override string Description => "向输入框填入文本。CAPTCHA/SSO 页面上拒绝填写，不代填、不绕过。填写后若页面跳到白名单外则退回。";
    public override string ParametersJsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "selector": { "type": "string", "description": "输入框 CSS 选择器" },
            "value": { "type": "string", "description": "要填入的文本，空字符串表示清空" }
          },
          "required": ["selector", "value"]
        }
        """;
    public override Task<string> ExecuteAsync(string parameters) => Session.FillAsync(parameters);
}

public sealed class BrowserExtractTool : BrowserToolBase
{
    public BrowserExtractTool(string pluginId, BrowserSession session) : base(pluginId, session) { }
    public override string Name => "browser_extract";
    public override string Description => "提取元素的可见文本或某个属性。遇到 CAPTCHA 或 SSO 时不提取，返回需要人工接管。";
    public override string ParametersJsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "selector": { "type": "string", "description": "CSS 选择器" },
            "attribute": { "type": "string", "description": "可选。要读取的属性名；省略则取可见文本" }
          },
          "required": ["selector"]
        }
        """;
    public override Task<string> ExecuteAsync(string parameters) => Session.ExtractAsync(parameters);
}

public sealed class BrowserScreenshotTool : BrowserToolBase
{
    public BrowserScreenshotTool(string pluginId, BrowserSession session) : base(pluginId, session) { }
    public override string Name => "browser_screenshot";
    public override string Description => "把当前页或某个元素截成 PNG，保存到插件数据目录 screenshots，返回绝对路径。CAPTCHA/SSO 页面不截图。";
    public override string ParametersJsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "selector": { "type": "string", "description": "可选。只截该元素；省略则截整页" },
            "fullPage": { "type": "boolean", "description": "整页截图。仅在未指定 selector 时有效" }
          }
        }
        """;
    public override Task<string> ExecuteAsync(string parameters) => Session.ScreenshotAsync(parameters);
}

public sealed class BrowserDownloadTool : BrowserToolBase
{
    public BrowserDownloadTool(string pluginId, BrowserSession session) : base(pluginId, session) { }
    public override string Name => "browser_download";
    public override string Description => "把文件下载到插件数据目录 downloads 并返回路径。只保存，不执行。工具名 browser_download 在高风险名单中（下载到执行需先审批）。url 不在白名单则拒绝。";
    public override string ParametersJsonSchema =>
        """
        {
          "type": "object",
          "properties": {
            "url": { "type": "string", "description": "直接下载的绝对 http(s) 地址。与 selector 二选一" },
            "selector": { "type": "string", "description": "点击后触发下载的 CSS 选择器。与 url 二选一" },
            "filename": { "type": "string", "description": "可选保存文件名，只取文件名，不接受路径" }
          }
        }
        """;
    public override Task<string> ExecuteAsync(string parameters) => Session.DownloadAsync(parameters);
}
