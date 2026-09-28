using System.Diagnostics;
using ForgeSelf.Api.Plugins.FileTools.Models;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.FileTools.Services;

/// <summary>
/// 目录大小排行扫描（批次C）。要点：
/// ① 流式遍历（<see cref="Directory.EnumerateFileSystemEntries"/>）+ 显式栈，不用
///    <c>GetFiles(...,AllDirectories)</c> —— 后者会把整棵树的路径一次性物化成数组，大目录上是内存与延迟悬崖；
/// ② 一趟遍历 + 一次自底向上归并（walk-up），不做「每个目录各自递归重扫」的 O(n²)；
/// ③ 后台任务 + 可取消，HTTP 侧只受理与轮询。
/// 既有 <see cref="IFileStatsService"/> 的方法一律不动（规格偏差记录 D-3）。
/// </summary>
public interface IFolderScanService
{
    /// <summary>校验路径并受理一次后台扫描（不阻塞调用线程）。</summary>
    ScanAccepted StartScan(FolderScanRequest request);

    /// <summary>取任务视图（Running 期间为部分结果）。任务不存在返回 null。</summary>
    ScanView? GetScan(Guid scanId);

    /// <summary>请求取消。任务不存在或已停下返回 false。</summary>
    bool CancelScan(Guid scanId);

    /// <summary>释放内存态任务。在跑的任务拒绝（返回 false）。</summary>
    bool RemoveScan(Guid scanId);

    /// <summary>
    /// 同步跑完一次扫描并返回视图。HTTP 侧不用它（那里要的是「受理 + 轮询」），
    /// 供 AI 工具这类「一次调用要拿到完整结果」的消费方。
    /// </summary>
    ScanView ScanAndWait(FolderScanRequest request);
}

public class FolderScanService : IFolderScanService
{
    /// <summary>单个任务最多计入的目录数（触顶即停并标截断）。</summary>
    internal const int MaxDirectories = 200_000;

    /// <summary>单个任务最多计入的文件数。</summary>
    internal const long MaxFiles = 5_000_000;

    /// <summary>每处理多少个条目检查一次取消。</summary>
    private const int CancelCheckGranularity = 256;

    private readonly IFolderScanJobStore _store;

    public FolderScanService(IFolderScanJobStore store) => _store = store;

    public ScanAccepted StartScan(FolderScanRequest request)
    {
        request ??= new FolderScanRequest();
        var path = NormalizeRoot(request.Directory);

        var top = Math.Clamp(request.Top <= 0 ? 50 : request.Top, 1, 500);
        var job = _store.Create(path, top);

        // 扫描在后台线程池跑；取消令牌由 job 自持，插件卸载时统一取消（见 FileToolsPlugin.Apply）
        _ = Task.Run(() => RunAsync(job));

        return new ScanAccepted { ScanId = job.Id, RootPath = path, State = job.State };
    }

    public ScanView ScanAndWait(FolderScanRequest request)
    {
        request ??= new FolderScanRequest();
        var path = NormalizeRoot(request.Directory);
        var job = _store.Create(path, Math.Clamp(request.Top <= 0 ? 50 : request.Top, 1, 500));
        try
        {
            RunAsync(job);
        }
        finally
        {
            // 同步用完即从内存任务表摘掉，不占 20 个配额（工具调用方不需要轮询它）
            _store.Remove(job.Id);
        }
        return BuildView(job);
    }

    private static string NormalizeRoot(string? raw)
    {
        if (raw.IsNullOrWhiteSpace()) throw new ArgumentException("目录路径不能为空", nameof(raw));

        string full;
        try
        {
            full = Path.GetFullPath(raw.Trim().TrimEnd('"'));
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"路径格式不合法: {ex.Message}", nameof(raw), ex);
        }

        // 去掉尾分隔符，但保留盘符根的形态（C:\ 不能压成 C:）
        var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var isDriveRoot = trimmed.Length == 2 && trimmed[1] == ':';
        var root = isDriveRoot ? trimmed + Path.DirectorySeparatorChar : trimmed;

        if (File.Exists(root)) throw new DirectoryNotFoundException($"该路径是文件，不是目录: {root}");
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"目录不存在: {root}");

        return root;
    }

    private void RunAsync(ScanJob job)
    {
        var sw = Stopwatch.StartNew();
        job.State = ScanState.Running;
        try
        {
            Walk(job);

            lock (job.Sync)
            {
                if (job.State != ScanState.Cancelled)
                    job.State = ScanState.Completed;
            }
        }
        catch (OperationCanceledException)
        {
            lock (job.Sync) job.State = ScanState.Cancelled;
        }
        catch (Exception ex)
        {
            // 遍历内部已逐条目容错，走到这里说明是根级意外；记录原因，不把异常抛回请求线程
            lock (job.Sync)
            {
                job.Error = ex.Message;
                job.State = ScanState.Failed;
            }
            XTrace.Log.Warn("[FileTools] 目录扫描失败 {0}: {1}", job.Root, ex.Message);
        }
        finally
        {
            lock (job.Sync) job.FinishedAt = DateTime.Now;
            sw.Stop();
        }
    }

    /// <summary>
    /// 显式栈深度优先遍历。**逐文件沿父链上卷 Total**（不是扫完再归并一趟）：
    /// 部分结果（Running/Cancelled）也必须自洽——否则根分母只有根级字节，
    /// 界面会给出「总占用 29B / 某子目录 160KB / 占比 564965%」这种假数字（e2e 截图实测抓到）。
    /// </summary>
    private void Walk(ScanJob job)
    {
        var stack = new Stack<(string Path, int Depth)>();
        stack.Push((job.Root, 0));
        var touched = 0;

        while (stack.Count > 0)
        {
            var (dir, depth) = stack.Pop();

            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(dir);
            }
            catch (Exception ex)
            {
                // 整个目录不可枚举（权限/正在消失）：计入无权限数，继续扫别处，不整体失败
                lock (job.Sync) { job.InaccessibleCount++; job.Error = $"{dir}: {ex.Message}"; }
                continue;
            }

            var self = job.GetOrAdd(dir, depth, null);

            foreach (var entry in entries)
            {
                // 每 256 个条目检查一次取消与上限，避免逐条进锁
                if (++touched % CancelCheckGranularity == 0)
                {
                    if (job.Cts.IsCancellationRequested) { MarkCancelled(job); return; }
                    if (OverCap(job)) return;
                }

                try
                {
                    var attrs = File.GetAttributes(entry);
                    if ((attrs & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        // 不跟随符号链接/junction/挂载点：否则既可能成环，也会把根外的容量算进根内
                        if ((attrs & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                        {
                            lock (job.Sync) job.SkippedReparseCount++;
                            continue;
                        }

                        lock (job.Sync)
                        {
                            if (job.Dirs.Count >= MaxDirectories)
                            {
                                job.Truncated = true;
                                job.CapNote = $"目录数达到上限 {MaxDirectories:N0}，已停止深入";
                            }
                            job.GetOrAdd(entry, depth + 1, dir);
                            self.ChildDirs++;
                        }
                        stack.Push((entry, depth + 1));
                    }
                    else
                    {
                        var len = new FileInfo(entry).Length;
                        lock (job.Sync)
                        {
                            self.Direct += len;
                            self.Files++;
                            job.TotalFiles++;
                            for (var node = self; node != null; node = ParentOf(job, node))
                                node.Total += len;
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    lock (job.Sync) job.InaccessibleCount++;
                }
                catch (IOException)
                {
                    // 条目在枚举后被删走/被占用：计入无权限/不可读数即可，不能让一次扫描整体失败
                    lock (job.Sync) job.InaccessibleCount++;
                }
            }
        }
    }

    private static bool OverCap(ScanJob job)
    {
        lock (job.Sync)
        {
            if (job.TotalFiles > MaxFiles)
            {
                job.Truncated = true;
                job.CapNote = $"文件数达到上限 {MaxFiles:N0}，已停止深入";
                return true;
            }
            if (job.Dirs.Count >= MaxDirectories && job.Truncated) return true;
            return false;
        }
    }

    private static void MarkCancelled(ScanJob job)
    {
        lock (job.Sync)
        {
            job.State = ScanState.Cancelled;
            job.CapNote = "已被取消，以下为取消前累计的部分结果";
        }
    }

    /// <summary>取父目录统计（父在压栈前已登记，故链上必然可解析；到根即 null）。</summary>
    private static DirStat? ParentOf(ScanJob job, DirStat node) =>
        node.Parent != null && job.Dirs.TryGetValue(node.Parent, out var parent) ? parent : null;

    public ScanView? GetScan(Guid scanId)
    {
        var job = _store.Get(scanId);
        return job == null ? null : BuildView(job);
    }

    public bool CancelScan(Guid scanId)
    {
        var job = _store.Get(scanId);
        if (job == null) return false;
        if (job.State is not (ScanState.Queued or ScanState.Running)) return false;
        job.Cts.Cancel();
        return true;
    }

    public bool RemoveScan(Guid scanId) => _store.Remove(scanId);

    /// <summary>
    /// 由累加态构建对外视图。排行恒为「直接子目录」这一切片 ——
    /// 只有同一层的切片才互不重叠，才能证明 Σ(行) + 其他 + 根本级 == 根总量。
    /// 要看更深层用钻取（以该目录为新根重扫），不在这里做多级展开。
    /// </summary>
    internal static ScanView BuildView(ScanJob job)
    {
        lock (job.Sync)
        {
            var root = job.GetOrAdd(job.Root, 0, null);
            var rootTotal = root.Total;
            var children = job.Dirs.Where(kv => kv.Value.Depth == 1)
                                   .Select(kv => (Path: kv.Key, Stat: kv.Value))
                                   .OrderByDescending(x => x.Stat.Total)
                                   .ToList();

            var view = new ScanView
            {
                ScanId = job.Id,
                State = job.State,
                RootPath = job.Root,
                RootTotalBytes = rootTotal,
                RootTotalFormatted = FileSizeFormatter.FormatSize(rootTotal),
                RootOwnBytes = root.Direct,
                RootOwnFormatted = FileSizeFormatter.FormatSize(root.Direct),
                DirectoryCount = Math.Max(0, job.Dirs.Count - 1),
                FileCount = job.TotalFiles,
                DurationMs = (int)(job.FinishedAt ?? DateTime.Now).Subtract(job.StartedAt).TotalMilliseconds,
                InaccessibleCount = job.InaccessibleCount,
                SkippedReparseCount = job.SkippedReparseCount,
                Truncated = job.Truncated || children.Count > job.Top,
                CapNote = job.CapNote,
                Partial = job.State is ScanState.Queued or ScanState.Running,
                Top = job.Top,
                ChildCount = children.Count,
                Error = job.State == ScanState.Failed ? job.Error : string.Empty
            };

            var kept = children.Take(job.Top).ToList();
            foreach (var child in kept) view.Items.Add(RowOf(child.Path, job.Root, child.Stat, rootTotal));

            var rest = children.Skip(job.Top).ToList();
            if (rest.Count > 0)
            {
                view.OtherRow = new FolderSizeRow
                {
                    RelativePath = string.Empty,
                    Name = $"其他（{rest.Count:N0} 个目录）",
                    TotalBytes = rest.Sum(x => x.Stat.Total),
                    DirectBytes = rest.Sum(x => x.Stat.Direct),
                    FileCount = rest.Sum(x => x.Stat.Files),
                    DirCount = rest.Sum(x => x.Stat.ChildDirs),
                    Percentage = Percent(rest.Sum(x => x.Stat.Total), rootTotal),
                    IsOther = true
                };
                FormatRow(view.OtherRow);
            }

            if (root.Direct > 0)
            {
                view.RootOwnRow = new FolderSizeRow
                {
                    RelativePath = string.Empty,
                    Name = "本级文件（不在任何子目录）",
                    TotalBytes = root.Direct,
                    DirectBytes = root.Direct,
                    FileCount = root.Files,
                    Percentage = Percent(root.Direct, rootTotal)
                };
                FormatRow(view.RootOwnRow);
            }

            return view;
        }
    }

    private static FolderSizeRow RowOf(string fullPath, string root, DirStat stat, long rootTotal)
    {
        var relative = fullPath.Length > root.Length ? fullPath[(root.Length + 1)..].Replace('\\', '/') : fullPath;
        var name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar));
        if (name.IsNullOrEmpty()) name = fullPath;

        var row = new FolderSizeRow
        {
            RelativePath = relative,
            Name = name,
            TotalBytes = stat.Total,
            DirectBytes = stat.Direct,
            FileCount = stat.Files,
            DirCount = stat.ChildDirs,
            Percentage = Percent(stat.Total, rootTotal)
        };
        return FormatRow(row);
    }

    /// <summary>占根总量百分比，两位小数；根为 0 时全 0（不产生 NaN/∞）。</summary>
    internal static decimal Percent(long part, long total)
    {
        if (total <= 0 || part <= 0) return 0m;
        return Math.Round((decimal)part * 100m / total, 2, MidpointRounding.AwayFromZero);
    }

    private static FolderSizeRow FormatRow(FolderSizeRow row)
    {
        row.TotalFormatted = FileSizeFormatter.FormatSize(row.TotalBytes);
        row.DirectFormatted = FileSizeFormatter.FormatSize(row.DirectBytes);
        return row;
    }
}
