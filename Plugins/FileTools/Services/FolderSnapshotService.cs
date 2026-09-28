using ForgeSelf.Api.Plugins.FileTools.Data;
using ForgeSelf.Api.Plugins.FileTools.Entities;
using ForgeSelf.Api.Plugins.FileTools.Models;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.FileTools.Services;

/// <summary>
/// 目录扫描快照（批次C）。快照是**不可变事实**：保存只插新行，历史快照永不 UPDATE；
/// 趋势 = 两个快照之间按 RelativePath 现算（规格 BR-6）。
/// 删除只删数据库行 —— 本文件任何地方都不允许出现对文件系统路径的删除动作（铁律10）。
/// </summary>
public interface IFolderSnapshotService
{
    /// <summary>把一个已停下的扫描任务落成快照，返回快照头。</summary>
    SnapshotSummary SaveScan(Guid scanId, string? note = null);

    List<SnapshotSummary> ListSnapshots(int take = 50);

    /// <summary>快照明细；不存在返回 null。</summary>
    SnapshotDetail? GetSnapshot(long snapshotId);

    /// <summary>删除快照及其明细行（仅数据库）。返回是否删到东西。</summary>
    bool DeleteSnapshot(long snapshotId);

    /// <summary>两个快照之间的目录级增减。</summary>
    List<CompareRow> Compare(long fromSnapshotId, long toSnapshotId);
}

public class FolderSnapshotService : IFolderSnapshotService
{
    private readonly IFolderScanJobStore _store;

    public FolderSnapshotService(IFolderScanJobStore store) => _store = store;

    public SnapshotSummary SaveScan(Guid scanId, string? note = null)
    {
        var job = _store.Get(scanId)
            ?? throw new KeyNotFoundException($"扫描任务不存在或已过期，请重新扫描: {scanId}");

        var view = FolderScanService.BuildView(job);
        if (view.State is ScanState.Queued or ScanState.Running)
            throw new InvalidOperationException("扫描仍在进行中，请等它完成或先取消，再保存快照");

        var snapshot = new ScanSnapshot
        {
            RootPath = view.RootPath,
            ScannedAt = DateTime.Now,
            DurationMs = view.DurationMs,
            RootTotalBytes = view.RootTotalBytes,
            RootOwnBytes = view.RootOwnBytes,
            DirectoryCount = view.DirectoryCount,
            ChildCount = view.ChildCount,
            FileCount = view.FileCount,
            Top = view.Top,
            Truncated = view.Truncated,
            InaccessibleCount = view.InaccessibleCount,
            SkippedReparseCount = view.SkippedReparseCount,
            Note = note ?? string.Empty
        };

        snapshot.Insert();
        try
        {
            foreach (var row in view.Items)
            {
                var entry = new ScanFolderEntry
                {
                    SnapshotId = snapshot.Id,
                    RelativePath = row.RelativePath,
                    Name = row.Name,
                    TotalBytes = row.TotalBytes,
                    DirectBytes = row.DirectBytes,
                    FileCount = row.FileCount,
                    DirCount = row.DirCount
                };
                entry.Insert();
            }
        }
        catch
        {
            // 补偿：不留「有快照头、缺排行行」的半张快照。删的是刚写的数据库行，绝不触碰文件系统。
            ScanFolderEntry.Delete(ScanFolderEntry._.SnapshotId == snapshot.Id);
            snapshot.Delete();
            throw;
        }

        XTrace.Log.Info("[FileTools] 目录扫描快照已保存 Id={0} Root={1} 行数={2}",
            snapshot.Id, snapshot.RootPath, view.Items.Count);

        return SummaryOf(snapshot);
    }

    public List<SnapshotSummary> ListSnapshots(int take = 50)
    {
        if (take <= 0) take = 50;
        return ScanSnapshot.FindAll()
            .OrderByDescending(s => s.ScannedAt)
            .Take(take)
            .Select(SummaryOf)
            .ToList();
    }

    public SnapshotDetail? GetSnapshot(long snapshotId)
    {
        var snapshot = ScanSnapshot.Find(ScanSnapshot._.Id == snapshotId);
        if (snapshot == null) return null;

        var detail = new SnapshotDetail
        {
            Snapshot = SummaryOf(snapshot),
            Items = ReadRows(snapshot)
        };
        detail.RootOwnRow = RowOf("本级文件（不在任何子目录）", string.Empty,
            snapshot.RootOwnBytes, snapshot.RootOwnBytes, 0, 0, snapshot.RootTotalBytes);

        var hidden = snapshot.ChildCount - detail.Items.Count;
        if (hidden > 0)
        {
            var hiddenBytes = snapshot.RootTotalBytes - snapshot.RootOwnBytes - detail.Items.Sum(r => r.TotalBytes);
            if (hiddenBytes < 0) hiddenBytes = 0;
            detail.OtherRow = RowOf($"其他（{hidden:N0} 个目录）", string.Empty,
                hiddenBytes, 0, 0, 0, snapshot.RootTotalBytes, true);
        }

        return detail;
    }

    public bool DeleteSnapshot(long snapshotId)
    {
        var snapshot = ScanSnapshot.Find(ScanSnapshot._.Id == snapshotId);
        if (snapshot == null) return false;

        var rows = ScanFolderEntry.Delete(ScanFolderEntry._.SnapshotId == snapshotId);
        snapshot.Delete();

        XTrace.Log.Info("[FileTools] 快照已删除 Id={0} 明细行数={1}（仅数据库，未触碰文件系统）", snapshotId, rows);
        return true;
    }

    public List<CompareRow> Compare(long fromSnapshotId, long toSnapshotId)
    {
        var from = GetSnapshot(fromSnapshotId) ?? throw new KeyNotFoundException($"快照不存在: {fromSnapshotId}");
        var to = GetSnapshot(toSnapshotId) ?? throw new KeyNotFoundException($"快照不存在: {toSnapshotId}");

        if (!string.Equals(from.Snapshot.RootPath.TrimEnd('\\', '/'), to.Snapshot.RootPath.TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("两个快照的扫描根路径不同，无法对比");

        var left = from.Items.ToDictionary(r => r.RelativePath, StringComparer.OrdinalIgnoreCase);
        var right = to.Items.ToDictionary(r => r.RelativePath, StringComparer.OrdinalIgnoreCase);

        var keys = left.Keys.Union(right.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.Ordinal);
        var rows = new List<CompareRow>();
        foreach (var key in keys)
        {
            var l = left.TryGetValue(key, out var lv) ? lv : null;
            var r = right.TryGetValue(key, out var rv) ? rv : null;
            var fromBytes = l?.TotalBytes ?? 0;
            var toBytes = r?.TotalBytes ?? 0;

            rows.Add(new CompareRow
            {
                RelativePath = key,
                Name = (r ?? l)!.Name,
                FromBytes = fromBytes,
                ToBytes = toBytes,
                DeltaBytes = toBytes - fromBytes,
                DeltaPercent = fromBytes > 0
                    ? Math.Round((decimal)(toBytes - fromBytes) * 100m / fromBytes, 2, MidpointRounding.AwayFromZero)
                    : 0m,
                Missing = r == null,
                Added = l == null
            });
        }

        return rows.OrderByDescending(r => Math.Abs(r.DeltaBytes)).ToList();
    }

    private static List<FolderSizeRow> ReadRows(ScanSnapshot snapshot) =>
        ScanFolderEntry.FindAll(ScanFolderEntry._.SnapshotId == snapshot.Id)
            .OrderByDescending(e => e.TotalBytes)
            .Select(e => RowOf(e.RelativePath, e.RelativePath, e.TotalBytes, e.DirectBytes,
                e.FileCount, e.DirCount, snapshot.RootTotalBytes))
            .ToList();

    private static FolderSizeRow RowOf(string name, string relativePath, long total, long direct,
        long files, long dirs, long rootTotal, bool isOther = false) => new()
    {
        Name = name,
        RelativePath = relativePath,
        TotalBytes = total,
        TotalFormatted = FileSizeFormatter.FormatSize(total),
        DirectBytes = direct,
        DirectFormatted = FileSizeFormatter.FormatSize(direct),
        FileCount = files,
        DirCount = dirs,
        Percentage = FolderScanService.Percent(total, rootTotal),
        IsOther = isOther
    };

    private static SnapshotSummary SummaryOf(ScanSnapshot s) => new()
    {
        Id = s.Id,
        RootPath = s.RootPath,
        ScannedAt = s.ScannedAt,
        DurationMs = s.DurationMs,
        RootTotalBytes = s.RootTotalBytes,
        RootTotalFormatted = FileSizeFormatter.FormatSize(s.RootTotalBytes),
        RootOwnBytes = s.RootOwnBytes,
        DirectoryCount = s.DirectoryCount,
        FileCount = s.FileCount,
        Top = s.Top,
        Truncated = s.Truncated,
        InaccessibleCount = s.InaccessibleCount,
        SkippedReparseCount = s.SkippedReparseCount,
        Note = s.Note ?? string.Empty,
        RootPathMissing = !s.RootPath.IsNullOrEmpty() && !Directory.Exists(s.RootPath)
    };
}
