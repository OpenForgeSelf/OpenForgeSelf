namespace OpenForgeSelf.Backend.Plugins.ProxyCapture.Models;

/// <summary>监听器配置 DTO（含运行时状态）。</summary>
public class ListenerConfigDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ListenAddress { get; set; } = "0.0.0.0";
    public int ListenPort { get; set; }
    public string? TargetHost { get; set; }
    public int? TargetPort { get; set; }
    public bool Enabled { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>运行时是否正在监听。</summary>
    public bool IsRunning { get; set; }
}

public class CreateListenerRequest
{
    public string Name { get; set; } = string.Empty;
    public string ListenAddress { get; set; } = "0.0.0.0";
    public int ListenPort { get; set; }
    public string? TargetHost { get; set; }
    public int? TargetPort { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Description { get; set; }
}

public class UpdateListenerRequest : CreateListenerRequest
{
}

/// <summary>抓包记录摘要（列表用）。</summary>
public class CaptureSessionSummaryDto
{
    public long Id { get; set; }
    public int ListenerId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string ClientIp { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string? Method { get; set; }
    public string? Url { get; set; }
    public int? StatusCode { get; set; }
    public long RequestBytes { get; set; }
    public long ResponseBytes { get; set; }
    public long DurationMs { get; set; }
    public bool Forwarded { get; set; }
}

/// <summary>抓包记录详情（详情抽屉用）。</summary>
public class CaptureSessionDetailDto : CaptureSessionSummaryDto
{
    public string LocalEndpoint { get; set; } = string.Empty;
    public string? HttpVersion { get; set; }
    public string? RequestHeaders { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseHeaders { get; set; }
    public string? ResponseBody { get; set; }
    public string? RawPreview { get; set; }
    public string? ErrorMessage { get; set; }
}
