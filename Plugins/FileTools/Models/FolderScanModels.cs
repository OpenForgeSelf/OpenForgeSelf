namespace ForgeSelf.Api.Plugins.FileTools.Models;

/// <summary>
/// 目录大小扫描（批次C）。与 <see cref="FileStatsModels.cs"/> 的「单根总量」系 DTO 刻意分开：
/// 那边是既有端点的返回契约（AI 工具 filetools.file_stats 也在用），本文件只服务新增的 folders 链路。
/// </summary>
public enum ScanState
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>创建扫描任务。</summary>
public class FolderScanRequest
{
    /// <summary>要统计的目录绝对路径。</summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>排行保留条数，夹紧到 [1,500]。</summary>
    public int Top { get; set; } = 50;
}

/// <summary>任务受理回执（扫描在后台跑，不同步返回结果）。</summary>
public class ScanAccepted
{
    public Guid ScanId { get; set; }
    public string RootPath { get; set; } = string.Empty;
    public ScanState State { get; set; }
}

/// <summary>一个子目录的排行行。占比由后端统一算，前端不得自算第二套口径。</summary>
public class FolderSizeRow
{
    /// <summary>相对根的目录路径，/ 分隔；「其他」行为空。</summary>
    public string RelativePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>含全部后代的字节数。</summary>
    public long TotalBytes { get; set; }
    public string TotalFormatted { get; set; } = string.Empty;
    /// <summary>本级直接字节数（不含子目录）。</summary>
    public long DirectBytes { get; set; }
    public string DirectFormatted { get; set; } = string.Empty;
    public long FileCount { get; set; }
    public long DirCount { get; set; }
    /// <summary>占根总量百分比，两位小数。</summary>
    public decimal Percentage { get; set; }
    /// <summary>是否为合成的截断行「其他（N 个目录）」。</summary>
    public bool IsOther { get; set; }
}

/// <summary>任务状态 + 进度 + 排行（Running 期间返回截至当前的部分结果）。</summary>
public class ScanView
{
    public Guid ScanId { get; set; }
    public ScanState State { get; set; }
    public string RootPath { get; set; } = string.Empty;

    /// <summary>根总量 = RootOwnBytes + Σ(全部直接子目录 TotalBytes)。不变式，测试据此断言。</summary>
    public long RootTotalBytes { get; set; }
    public string RootTotalFormatted { get; set; } = string.Empty;
    /// <summary>根本级文件字节（不属于任何子目录）。</summary>
    public long RootOwnBytes { get; set; }
    public string RootOwnFormatted { get; set; } = string.Empty;

    public long DirectoryCount { get; set; }
    public long FileCount { get; set; }
    public long DurationMs { get; set; }
    public long InaccessibleCount { get; set; }
    public long SkippedReparseCount { get; set; }

    /// <summary>排行被 Top 截断，或遍历触到条目/文件上限。</summary>
    public bool Truncated { get; set; }
    /// <summary>截断原因；未截断为空。</summary>
    public string CapNote { get; set; } = string.Empty;
    /// <summary>Running 中，本视图是部分结果。</summary>
    public bool Partial { get; set; }

    public int Top { get; set; }
    /// <summary>已知的直接子目录总数（未截断前）。</summary>
    public long ChildCount { get; set; }

    public List<FolderSizeRow> Items { get; set; } = new();
    /// <summary>被 Top 挤掉的子目录合并行；无截断时为空。</summary>
    public FolderSizeRow? OtherRow { get; set; }
    /// <summary>根本级文件这一行；便于界面把占比凑满 100%。</summary>
    public FolderSizeRow? RootOwnRow { get; set; }

    public string Error { get; set; } = string.Empty;
}

/// <summary>把已完成任务落成快照。</summary>
public class SaveSnapshotRequest
{
    public Guid ScanId { get; set; }
    public string Note { get; set; } = string.Empty;
}

/// <summary>快照头信息。</summary>
public class SnapshotSummary
{
    public long Id { get; set; }
    public string RootPath { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
    public long DurationMs { get; set; }
    public long RootTotalBytes { get; set; }
    public string RootTotalFormatted { get; set; } = string.Empty;
    public long RootOwnBytes { get; set; }
    public long DirectoryCount { get; set; }
    public long FileCount { get; set; }
    public int Top { get; set; }
    public bool Truncated { get; set; }
    public long InaccessibleCount { get; set; }
    public long SkippedReparseCount { get; set; }
    public string Note { get; set; } = string.Empty;
    /// <summary>快照里的根路径当前是否仍存在（仅提示，不据此改数据）。</summary>
    public bool RootPathMissing { get; set; }
}

/// <summary>快照明细。</summary>
public class SnapshotDetail
{
    public SnapshotSummary Snapshot { get; set; } = new();
    public List<FolderSizeRow> Items { get; set; } = new();
    public FolderSizeRow? OtherRow { get; set; }
    public FolderSizeRow? RootOwnRow { get; set; }
}

/// <summary>趋势对比：同一根路径下两个快照之间，按 RelativePath 匹配。</summary>
public class CompareRequest
{
    public long FromSnapshotId { get; set; }
    public long ToSnapshotId { get; set; }
}

public class CompareRow
{
    public string RelativePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long FromBytes { get; set; }
    public long ToBytes { get; set; }
    public long DeltaBytes { get; set; }
    /// <summary>相对 From 的变化百分比；From 为 0 时置 0。</summary>
    public decimal DeltaPercent { get; set; }
    /// <summary>在 To 快照中已消失。</summary>
    public bool Missing { get; set; }
    /// <summary>在 From 快照中不存在（新增）。</summary>
    public bool Added { get; set; }
}
