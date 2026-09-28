using System.Collections.Concurrent;

namespace ForgeSelf.Api.Plugins.FileTools.Services;

/// <summary>单个目录的累加态。只在 job 的锁内被读写。类型对外可见只为满足可访问性一致性，属插件内部管道，不是公共契约。</summary>
public sealed class DirStat
{
    public int Depth;
    public string? Parent;
    /// <summary>本级文件直接字节。</summary>
    public long Direct;
    /// <summary>含全部后代（逐文件沿父链上卷，故任意时刻都已自洽，不只是扫完后）。</summary>
    public long Total;
    public long Files;
    public long ChildDirs;
}

/// <summary>一次扫描任务的可变状态。类型对外可见只为满足可访问性一致性，属插件内部管道，不是公共契约。</summary>
public sealed class ScanJob : IDisposable
{
    public required Guid Id { get; init; }
    public required string Root { get; init; }
    public required int Top { get; init; }

    public readonly object Sync = new();
    public readonly CancellationTokenSource Cts = new();
    public readonly Dictionary<string, DirStat> Dirs = new(StringComparer.Ordinal);

    public Models.ScanState State = Models.ScanState.Queued;
    public readonly DateTime StartedAt = DateTime.Now;
    public DateTime? FinishedAt;

    public long InaccessibleCount;
    public long SkippedReparseCount;
    public bool Truncated;
    public string CapNote = string.Empty;
    public string Error = string.Empty;

    /// <summary>已计入的文件总数（跨目录）。</summary>
    public long TotalFiles;

    public DirStat GetOrAdd(string path, int depth, string? parent)
    {
        if (!Dirs.TryGetValue(path, out var stat))
        {
            stat = new DirStat { Depth = depth, Parent = parent };
            Dirs[path] = stat;
        }
        return stat;
    }

    /// <summary>释放取消令牌源。任务被淘汰或显式删除时调用。</summary>
    public void Dispose() => Cts.Dispose();
}

/// <summary>
/// 内存态扫描任务表（单例）。跨请求存活，供控制器轮询；
/// 宿主进程重启即全部失效 —— 要复看历史须落快照（<see cref="IFolderSnapshotService"/>）。
/// </summary>
public interface IFolderScanJobStore : IDisposable
{
    ScanJob Create(string root, int top);
    bool TryGet(Guid id, out ScanJob? job);
    ScanJob? Get(Guid id);
    IReadOnlyList<Models.ScanState> RunningStates();
    /// <summary>淘汰已完成的最旧任务，使总数不超过 <see cref="MaxJobs"/>。</summary>
    int Trim(int maxJobs);
    bool Remove(Guid id);
    /// <summary>取消全部在跑/排队任务（插件卸载时调用；铁律14：不依赖宿主级 HostedService）。</summary>
    void CancelAll();
    int Count { get; }
}

public sealed class FolderScanJobStore : IFolderScanJobStore
{
    /// <summary>内存任务表上限（超出按完成时间淘汰最旧，规格 BR-5）。</summary>
    public const int MaxJobs = 20;

    private readonly ConcurrentDictionary<Guid, ScanJob> _jobs = new();

    public int Count => _jobs.Count;

    public ScanJob Create(string root, int top)
    {
        var job = new ScanJob
        {
            Id = Guid.NewGuid(),
            Root = root,
            Top = top
        };
        // 先建根的累加槽位，避免首个文件写入时再判空
        lock (job.Sync) job.GetOrAdd(root, 0, null);
        _jobs[job.Id] = job;
        Trim(MaxJobs);
        return job;
    }

    public bool TryGet(Guid id, out ScanJob? job) => _jobs.TryGetValue(id, out job);

    public ScanJob? Get(Guid id) => _jobs.TryGetValue(id, out var job) ? job : null;

    public IReadOnlyList<Models.ScanState> RunningStates() =>
        _jobs.Values.Where(j => j.State is Models.ScanState.Queued or Models.ScanState.Running)
                    .Select(j => j.State).ToList();

    public int Trim(int maxJobs)
    {
        if (_jobs.Count <= maxJobs) return 0;

        // 只淘汰已停下的任务；在跑的一律保留（否则用户正在等的任务会神秘消失）
        var finished = _jobs.Values
            .Where(j => j.State is Models.ScanState.Completed or Models.ScanState.Failed or Models.ScanState.Cancelled)
            .OrderBy(j => j.FinishedAt ?? j.StartedAt)
            .ToList();

        var removed = 0;
        foreach (var job in finished.Take(_jobs.Count - maxJobs))
        {
            if (_jobs.TryRemove(job.Id, out _))
            {
                removed++;
                job.Dispose();
            }
        }
        return removed;
    }

    public bool Remove(Guid id)
    {
        if (!_jobs.TryGetValue(id, out var job)) return false;
        if (job.State is Models.ScanState.Queued or Models.ScanState.Running) return false;
        if (!_jobs.TryRemove(id, out _)) return false;
        job.Dispose();
        return true;
    }

    public void CancelAll()
    {
        foreach (var job in _jobs.Values)
        {
            if (job.State is Models.ScanState.Queued or Models.ScanState.Running)
            {
                try { job.Cts.Cancel(); }
                catch (ObjectDisposedException) { /* 任务已被淘汰，忽略 */ }
            }
        }
    }

    public void Dispose()
    {
        foreach (var job in _jobs.Values) job.Dispose();
        _jobs.Clear();
    }
}
