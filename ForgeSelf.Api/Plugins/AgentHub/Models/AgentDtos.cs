namespace ForgeSelf.Api.Plugins.AgentHub.Models;

/// <summary>Agent 展示 DTO（供 UI 与工具消费）。</summary>
public class AgentDto
{
    /// <summary>主键</summary>
    public Int32 Id { get; set; }

    /// <summary>显示名</summary>
    public String Name { get; set; } = String.Empty;

    /// <summary>厂商标识</summary>
    public String Vendor { get; set; } = String.Empty;

    /// <summary>厂商展示名</summary>
    public String? DisplayName { get; set; }

    /// <summary>类型（Coding|Generic）</summary>
    public String? Kind { get; set; }

    /// <summary>擅长标签</summary>
    public List<String> Tags { get; set; } = [];

    /// <summary>能力矩阵</summary>
    public AgentCapabilityMatrix Capabilities { get; set; } = new();

    /// <summary>默认工作目录</summary>
    public String? DefaultCwd { get; set; }

    /// <summary>策略</summary>
    public AgentPolicy Policy { get; set; } = new();

    /// <summary>是否启用</summary>
    public Boolean Enabled { get; set; }

    /// <summary>选路优先级</summary>
    public Int32 Priority { get; set; }

    /// <summary>是否已授信</summary>
    public Boolean Trusted { get; set; }

    /// <summary>授信范围</summary>
    public List<String> TrustedScopes { get; set; } = [];

    /// <summary>备注</summary>
    public String? Notes { get; set; }

    /// <summary>交互口列表（含健康状态）</summary>
    public List<AgentAccessPointDto> AccessPoints { get; set; } = [];

    /// <summary>整体健康状态（取各交互口最差者：Missing > Degraded > Unknown > Ok）</summary>
    public String Health { get; set; } = "Unknown";

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
}

/// <summary>交互口 DTO。</summary>
public class AgentAccessPointDto
{
    /// <summary>主键</summary>
    public Int32 Id { get; set; }

    /// <summary>所属 Agent</summary>
    public Int32 AgentId { get; set; }

    /// <summary>所属 Agent 的厂商标识（由注册表填充，供 transport 取 profile）</summary>
    public String? Vendor { get; set; }

    /// <summary>能力等级（OneShot|Sessionful）</summary>
    public String Mode { get; set; } = "OneShot";

    /// <summary>传输方式（Cli|Acp|Http）</summary>
    public String Transport { get; set; } = "Cli";

    /// <summary>可执行文件</summary>
    public String? Executable { get; set; }

    /// <summary>参数模板</summary>
    public String? ArgsTemplate { get; set; }

    /// <summary>环境变量映射（只含变量名）</summary>
    public Dictionary<String, String> EnvVars { get; set; } = [];

    /// <summary>提示词注入方式</summary>
    public String PromptInjection { get; set; } = "Arg";

    /// <summary>输出格式</summary>
    public String OutputFormat { get; set; } = "Text";

    /// <summary>会话续接参数模板</summary>
    public String? SessionFlagTemplate { get; set; }

    /// <summary>是否支持取消</summary>
    public Boolean CancelSupported { get; set; }

    /// <summary>探测参数</summary>
    public String? ProbeArgs { get; set; }

    /// <summary>健康状态</summary>
    public String Health { get; set; } = "Unknown";

    /// <summary>最近探测时间</summary>
    public DateTime LastProbeTime { get; set; }

    /// <summary>探测到的版本</summary>
    public String? LastVersion { get; set; }

    /// <summary>最近错误</summary>
    public String? LastError { get; set; }

    /// <summary>是否默认交互口</summary>
    public Boolean IsDefault { get; set; }
}

/// <summary>新增 / 更新 Agent 的请求体。</summary>
public class AgentSaveRequest
{
    /// <summary>显示名（必填）</summary>
    public String? Name { get; set; }

    /// <summary>厂商标识（必填）</summary>
    public String? Vendor { get; set; }

    /// <summary>类型</summary>
    public String? Kind { get; set; }

    /// <summary>擅长标签</summary>
    public List<String>? Tags { get; set; }

    /// <summary>能力矩阵（缺省时按 profile 填充）</summary>
    public AgentCapabilityMatrix? Capabilities { get; set; }

    /// <summary>默认工作目录</summary>
    public String? DefaultCwd { get; set; }

    /// <summary>策略</summary>
    public AgentPolicy? Policy { get; set; }

    /// <summary>是否启用</summary>
    public Boolean? Enabled { get; set; }

    /// <summary>选路优先级</summary>
    public Int32? Priority { get; set; }

    /// <summary>备注</summary>
    public String? Notes { get; set; }

    /// <summary>
    /// 交互口列表：不传表示「按 profile 预填默认交互口」；
    /// 传空数组表示「显式清空」。
    /// </summary>
    public List<AgentAccessPointSaveRequest>? AccessPoints { get; set; }
}

/// <summary>交互口新增 / 更新请求体。</summary>
public class AgentAccessPointSaveRequest
{
    /// <summary>主键（更新时传）</summary>
    public Int32? Id { get; set; }

    /// <summary>能力等级</summary>
    public String? Mode { get; set; }

    /// <summary>传输方式</summary>
    public String? Transport { get; set; }

    /// <summary>可执行文件</summary>
    public String? Executable { get; set; }

    /// <summary>参数模板</summary>
    public String? ArgsTemplate { get; set; }

    /// <summary>环境变量映射（变量名）</summary>
    public Dictionary<String, String>? EnvVars { get; set; }

    /// <summary>提示词注入方式</summary>
    public String? PromptInjection { get; set; }

    /// <summary>输出格式</summary>
    public String? OutputFormat { get; set; }

    /// <summary>输出字段映射 JSON</summary>
    public String? OutputMappingJson { get; set; }

    /// <summary>会话续接参数模板</summary>
    public String? SessionFlagTemplate { get; set; }

    /// <summary>是否支持取消</summary>
    public Boolean? CancelSupported { get; set; }

    /// <summary>探测参数</summary>
    public String? ProbeArgs { get; set; }

    /// <summary>是否默认交互口</summary>
    public Boolean? IsDefault { get; set; }
}

/// <summary>发现候选（探测到但尚未登记的本机 agent）。</summary>
public class DiscoveredAgentDto
{
    /// <summary>厂商标识</summary>
    public String Vendor { get; set; } = String.Empty;

    /// <summary>厂商展示名</summary>
    public String DisplayName { get; set; } = String.Empty;

    /// <summary>可执行文件完整路径</summary>
    public String Executable { get; set; } = String.Empty;

    /// <summary>探测到的版本</summary>
    public String? Version { get; set; }

    /// <summary>是否已在注册表中（是则 UI 不给「添加」按钮）</summary>
    public Boolean AlreadyRegistered { get; set; }

    /// <summary>匹配到的 profile 标识（无内置 profile 时为 custom）</summary>
    public String ProfileId { get; set; } = String.Empty;
}

/// <summary>探测结果。</summary>
public class ProbeResultDto
{
    /// <summary>是否成功定位到可执行文件</summary>
    public Boolean Found { get; set; }

    /// <summary>健康状态（Ok|Degraded|Missing）</summary>
    public String Health { get; set; } = "Unknown";

    /// <summary>探测到的版本</summary>
    public String? Version { get; set; }

    /// <summary>可执行文件路径</summary>
    public String? Path { get; set; }

    /// <summary>错误信息（探测失败时）</summary>
    public String? Error { get; set; }

    /// <summary>profile 漂移提示（profile 断言不通过时给出，不静默失败）</summary>
    public String? ProfileWarning { get; set; }

    /// <summary>探测耗时（毫秒）</summary>
    public Int64 ElapsedMs { get; set; }
}
