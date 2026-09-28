using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.FileTools.Models;
using ForgeSelf.Api.Plugins.FileTools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.FileTools.Controllers;

[ApiController]
// 批次C：本控制器全部端点会按请求传入的任意绝对路径读写宿主文件系统（重命名/清理/压缩/扫描），
// 属管理面 → 类级鉴权（plugin-development 铁律17）。策略声明见 ForgeSelf.Api/AppBuilder.cs:214。
// 兼容性依据：全仓 git grep "api/filetools" 仅命中本文件的 [Route]（既有端点无 HTTP 消费者），
// AI 工具走服务直调不经 HTTP，故收紧不破坏任何现有调用方（规格偏差记录 D-2）。
[Authorize("ApiKeyPolicy")]
[Route("api/filetools")]
public class FileToolsController : ControllerBase
{
    private readonly IRenameService _renameService;
    private readonly ICleanupService _cleanupService;
    private readonly IArchiveService _archiveService;
    private readonly IFileStatsService _fileStatsService;
    private readonly IFolderScanService _folderScanService;
    private readonly IFolderSnapshotService _folderSnapshotService;

    public FileToolsController(
        IRenameService renameService,
        ICleanupService cleanupService,
        IArchiveService archiveService,
        IFileStatsService fileStatsService,
        IFolderScanService folderScanService,
        IFolderSnapshotService folderSnapshotService)
    {
        _renameService = renameService;
        _cleanupService = cleanupService;
        _archiveService = archiveService;
        _fileStatsService = fileStatsService;
        _folderScanService = folderScanService;
        _folderSnapshotService = folderSnapshotService;
    }

    [HttpGet]
    public ActionResult<ApiResponse<object>> GetOverview()
    {
        var tools = new
        {
            rename = new[] { "preview", "execute" },
            cleanup = new[] { "preview", "execute", "empty-folders", "duplicates" },
            archive = new[] { "compress", "extract", "info" },
            stats = new[] { "directory", "large-files", "types" },
            folders = new[] { "scan", "scan/{scanId}", "scan/{scanId}/cancel", "snapshots", "snapshots/{id}", "compare" }
        };
        return Ok(ApiResponse<object>.Ok(tools, "文件工具概览"));
    }

    [HttpPost("rename/preview")]
    public async Task<ActionResult<ApiResponse<List<RenamePreviewItem>>>> PreviewRename([FromBody] RenamePreviewRequest request)
    {
        try
        {
            XTrace.Log.Info("调用重命名预览接口");
            var result = await _renameService.PreviewRenameAsync(request.Files, request.Rules);
            return Ok(ApiResponse<List<RenamePreviewItem>>.Ok(result, "重命名预览成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("重命名预览失败: {0}", ex.Message);
            return BadRequest(ApiResponse<List<RenamePreviewItem>>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("重命名预览异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<RenamePreviewItem>>.Error("重命名预览失败: " + ex.Message));
        }
    }

    [HttpPost("rename/execute")]
    public async Task<ActionResult<ApiResponse<RenameExecuteResult>>> ExecuteRename([FromBody] RenameExecuteRequest request)
    {
        try
        {
            XTrace.Log.Info("调用重命名执行接口");
            var result = await _renameService.ExecuteRenameAsync(request.Files, request.Rules);
            return Ok(ApiResponse<RenameExecuteResult>.Ok(result, result.Message));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("重命名执行失败: {0}", ex.Message);
            return BadRequest(ApiResponse<RenameExecuteResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("重命名执行异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<RenameExecuteResult>.Error("重命名执行失败: " + ex.Message));
        }
    }

    [HttpPost("cleanup/preview")]
    public async Task<ActionResult<ApiResponse<CleanupPreviewResult>>> PreviewCleanup([FromBody] CleanupPreviewRequest request)
    {
        try
        {
            XTrace.Log.Info("调用清理预览接口");
            var result = await _cleanupService.PreviewCleanupAsync(request.Directory, request.Rules, request.Recursive);
            return Ok(ApiResponse<CleanupPreviewResult>.Ok(result, "清理预览成功"));
        }
        catch (DirectoryNotFoundException ex)
        {
            XTrace.Log.Warn("清理预览失败: {0}", ex.Message);
            return BadRequest(ApiResponse<CleanupPreviewResult>.Error(ex.Message, 400));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("清理预览失败: {0}", ex.Message);
            return BadRequest(ApiResponse<CleanupPreviewResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("清理预览异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<CleanupPreviewResult>.Error("清理预览失败: " + ex.Message));
        }
    }

    [HttpPost("cleanup/execute")]
    public async Task<ActionResult<ApiResponse<CleanupExecuteResult>>> ExecuteCleanup([FromBody] CleanupExecuteRequest request)
    {
        try
        {
            XTrace.Log.Info("调用清理执行接口");
            var result = await _cleanupService.ExecuteCleanupAsync(
                request.Directory,
                request.Rules,
                request.Recursive,
                request.DeletePermanently);
            return Ok(ApiResponse<CleanupExecuteResult>.Ok(result, result.Message));
        }
        catch (DirectoryNotFoundException ex)
        {
            XTrace.Log.Warn("清理执行失败: {0}", ex.Message);
            return BadRequest(ApiResponse<CleanupExecuteResult>.Error(ex.Message, 400));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("清理执行失败: {0}", ex.Message);
            return BadRequest(ApiResponse<CleanupExecuteResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("清理执行异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<CleanupExecuteResult>.Error("清理执行失败: " + ex.Message));
        }
    }

    [HttpPost("cleanup/empty-folders")]
    public async Task<ActionResult<ApiResponse<EmptyFoldersResult>>> FindEmptyFolders([FromBody] EmptyFoldersRequest request)
    {
        try
        {
            XTrace.Log.Info("调用查找空文件夹接口");
            var result = await _cleanupService.FindEmptyFoldersAsync(request.Directory, request.Recursive);
            return Ok(ApiResponse<EmptyFoldersResult>.Ok(result, "查找空文件夹成功"));
        }
        catch (DirectoryNotFoundException ex)
        {
            XTrace.Log.Warn("查找空文件夹失败: {0}", ex.Message);
            return BadRequest(ApiResponse<EmptyFoldersResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("查找空文件夹异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<EmptyFoldersResult>.Error("查找空文件夹失败: " + ex.Message));
        }
    }

    [HttpPost("cleanup/duplicates")]
    public async Task<ActionResult<ApiResponse<DuplicateFilesResult>>> FindDuplicateFiles([FromBody] DuplicateFilesRequest request)
    {
        try
        {
            XTrace.Log.Info("调用查找重复文件接口");
            var result = await _cleanupService.FindDuplicateFilesAsync(request.Directory, request.Recursive, request.MinSizeBytes);
            return Ok(ApiResponse<DuplicateFilesResult>.Ok(result, "查找重复文件成功"));
        }
        catch (DirectoryNotFoundException ex)
        {
            XTrace.Log.Warn("查找重复文件失败: {0}", ex.Message);
            return BadRequest(ApiResponse<DuplicateFilesResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("查找重复文件异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<DuplicateFilesResult>.Error("查找重复文件失败: " + ex.Message));
        }
    }

    [HttpPost("archive/compress")]
    public async Task<ActionResult<ApiResponse<CompressResult>>> Compress([FromBody] CompressRequest request)
    {
        try
        {
            XTrace.Log.Info("调用压缩文件接口");
            var result = await _archiveService.CompressAsync(
                request.Files,
                request.OutputPath,
                request.Format,
                request.Password,
                request.VolumeSizeBytes,
                request.CompressionLevel);
            return Ok(ApiResponse<CompressResult>.Ok(result, result.Message));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("压缩文件失败: {0}", ex.Message);
            return BadRequest(ApiResponse<CompressResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("压缩文件异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<CompressResult>.Error("压缩文件失败: " + ex.Message));
        }
    }

    [HttpPost("archive/extract")]
    public async Task<ActionResult<ApiResponse<ExtractResult>>> Extract([FromBody] ExtractRequest request)
    {
        try
        {
            XTrace.Log.Info("调用解压文件接口");
            var result = await _archiveService.ExtractAsync(
                request.ArchivePath,
                request.OutputPath,
                request.Password,
                request.Overwrite);
            return Ok(ApiResponse<ExtractResult>.Ok(result, result.Message));
        }
        catch (FileNotFoundException ex)
        {
            XTrace.Log.Warn("解压文件失败: {0}", ex.Message);
            return BadRequest(ApiResponse<ExtractResult>.Error(ex.Message, 400));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("解压文件失败: {0}", ex.Message);
            return BadRequest(ApiResponse<ExtractResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("解压文件异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ExtractResult>.Error("解压文件失败: " + ex.Message));
        }
    }

    [HttpGet("archive/info")]
    public async Task<ActionResult<ApiResponse<ArchiveInfoResult>>> GetArchiveInfo([FromQuery] string archivePath)
    {
        try
        {
            XTrace.Log.Info("调用获取压缩包信息接口");
            var result = await _archiveService.GetArchiveInfoAsync(archivePath);
            return Ok(ApiResponse<ArchiveInfoResult>.Ok(result, result.Message));
        }
        catch (FileNotFoundException ex)
        {
            XTrace.Log.Warn("获取压缩包信息失败: {0}", ex.Message);
            return BadRequest(ApiResponse<ArchiveInfoResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取压缩包信息异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ArchiveInfoResult>.Error("获取压缩包信息失败: " + ex.Message));
        }
    }

    [HttpPost("stats/directory")]
    public async Task<ActionResult<ApiResponse<DirectoryStatsResult>>> GetDirectoryStats([FromBody] DirectoryStatsRequest request)
    {
        try
        {
            XTrace.Log.Info("调用目录统计接口");
            var result = await _fileStatsService.GetDirectoryStatsAsync(request.Directory, request.Recursive);
            return Ok(ApiResponse<DirectoryStatsResult>.Ok(result, "目录统计成功"));
        }
        catch (DirectoryNotFoundException ex)
        {
            XTrace.Log.Warn("目录统计失败: {0}", ex.Message);
            return BadRequest(ApiResponse<DirectoryStatsResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("目录统计异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<DirectoryStatsResult>.Error("目录统计失败: " + ex.Message));
        }
    }

    [HttpPost("stats/large-files")]
    public async Task<ActionResult<ApiResponse<LargeFilesResult>>> GetLargeFiles([FromBody] LargeFilesRequest request)
    {
        try
        {
            XTrace.Log.Info("调用大文件列表接口");
            var result = await _fileStatsService.GetLargeFilesAsync(request.Directory, request.Limit, request.Recursive);
            return Ok(ApiResponse<LargeFilesResult>.Ok(result, "获取大文件列表成功"));
        }
        catch (DirectoryNotFoundException ex)
        {
            XTrace.Log.Warn("获取大文件列表失败: {0}", ex.Message);
            return BadRequest(ApiResponse<LargeFilesResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取大文件列表异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<LargeFilesResult>.Error("获取大文件列表失败: " + ex.Message));
        }
    }

    [HttpPost("stats/types")]
    public async Task<ActionResult<ApiResponse<FileTypesBreakdownResult>>> GetFileTypesBreakdown([FromBody] FileTypesBreakdownRequest request)
    {
        try
        {
            XTrace.Log.Info("调用文件类型分布接口");
            var result = await _fileStatsService.GetFileTypesBreakdownAsync(request.Directory, request.Recursive);
            return Ok(ApiResponse<FileTypesBreakdownResult>.Ok(result, "获取文件类型分布成功"));
        }
        catch (DirectoryNotFoundException ex)
        {
            XTrace.Log.Warn("获取文件类型分布失败: {0}", ex.Message);
            return BadRequest(ApiResponse<FileTypesBreakdownResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取文件类型分布异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<FileTypesBreakdownResult>.Error("获取文件类型分布失败: " + ex.Message));
        }
    }

    #region 批次C · 目录大小排行（folders）

    /// <summary>受理一次后台目录扫描（不阻塞请求线程）。结果用 GET folders/scan/{scanId} 轮询。</summary>
    [HttpPost("folders/scan")]
    public ActionResult<ApiResponse<ScanAccepted>> StartFolderScan([FromBody] FolderScanRequest request)
    {
        try
        {
            return Ok(ApiResponse<ScanAccepted>.Ok(_folderScanService.StartScan(request), "扫描任务已受理"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ScanAccepted>.Error(ex.Message, 400));
        }
        catch (DirectoryNotFoundException ex)
        {
            return BadRequest(ApiResponse<ScanAccepted>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("受理目录扫描失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ScanAccepted>.Error("受理目录扫描失败: " + ex.Message));
        }
    }

    /// <summary>查询扫描状态与排行；Running 期间返回截至当前的部分结果。</summary>
    [HttpGet("folders/scan/{scanId}")]
    public ActionResult<ApiResponse<ScanView>> GetFolderScan(Guid scanId)
    {
        var view = _folderScanService.GetScan(scanId);
        return view == null
            ? BadRequest(ApiResponse<ScanView>.Error($"扫描任务不存在或已过期（内存任务表只保留最近 {FolderScanJobStore.MaxJobs} 个），请重新扫描", 404))
            : Ok(ApiResponse<ScanView>.Ok(view, "扫描状态"));
    }

    /// <summary>请求取消扫描。已停下的任务返回 400。</summary>
    [HttpPost("folders/scan/{scanId}/cancel")]
    public ActionResult<ApiResponse<bool>> CancelFolderScan(Guid scanId)
    {
        return _folderScanService.CancelScan(scanId)
            ? Ok(ApiResponse<bool>.Ok(true, "已请求取消"))
            : BadRequest(ApiResponse<bool>.Error("扫描任务不存在或已结束，无需取消", 400));
    }

    /// <summary>释放内存中的扫描任务（在跑的任务拒绝）。不动快照、不动文件系统。</summary>
    [HttpDelete("folders/scan/{scanId}")]
    public ActionResult<ApiResponse<bool>> RemoveFolderScan(Guid scanId)
    {
        return _folderScanService.RemoveScan(scanId)
            ? Ok(ApiResponse<bool>.Ok(true, "任务已释放"))
            : BadRequest(ApiResponse<bool>.Error("扫描任务不存在或仍在进行，进行中的任务请先取消", 400));
    }

    /// <summary>把已结束的扫描任务落成快照（不可变）。</summary>
    [HttpPost("folders/snapshots")]
    public ActionResult<ApiResponse<SnapshotSummary>> SaveFolderSnapshot([FromBody] SaveSnapshotRequest request)
    {
        try
        {
            var summary = _folderSnapshotService.SaveScan(request.ScanId, request.Note);
            return Ok(ApiResponse<SnapshotSummary>.Ok(summary, "快照已保存"));
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ApiResponse<SnapshotSummary>.Error(ex.Message, 404));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(ApiResponse<SnapshotSummary>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("保存目录扫描快照失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<SnapshotSummary>.Error("保存目录扫描快照失败: " + ex.Message));
        }
    }

    /// <summary>快照列表（按扫描时间降序）。</summary>
    [HttpGet("folders/snapshots")]
    public ActionResult<ApiResponse<List<SnapshotSummary>>> ListFolderSnapshots([FromQuery] int take = 50)
    {
        try
        {
            return Ok(ApiResponse<List<SnapshotSummary>>.Ok(_folderSnapshotService.ListSnapshots(take), "快照列表"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("读取快照列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<SnapshotSummary>>.Error("读取快照列表失败: " + ex.Message));
        }
    }

    /// <summary>快照头 + 排行明细。</summary>
    [HttpGet("folders/snapshots/{snapshotId:long}")]
    public ActionResult<ApiResponse<SnapshotDetail>> GetFolderSnapshot(long snapshotId)
    {
        var detail = _folderSnapshotService.GetSnapshot(snapshotId);
        return detail == null
            ? BadRequest(ApiResponse<SnapshotDetail>.Error($"快照不存在: {snapshotId}", 404))
            : Ok(ApiResponse<SnapshotDetail>.Ok(detail, "快照明细"));
    }

    /// <summary>两个快照之间的目录级增减（同根路径才可比）。</summary>
    [HttpGet("folders/compare")]
    public ActionResult<ApiResponse<List<CompareRow>>> CompareFolderSnapshots([FromQuery] long from, [FromQuery] long to)
    {
        try
        {
            return Ok(ApiResponse<List<CompareRow>>.Ok(_folderSnapshotService.Compare(from, to), "趋势对比"));
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ApiResponse<List<CompareRow>>.Error(ex.Message, 404));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<List<CompareRow>>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("快照对比失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<CompareRow>>.Error("快照对比失败: " + ex.Message));
        }
    }

    /// <summary>删除快照及其明细行（只删数据库行，绝不触碰文件系统）。</summary>
    [HttpDelete("folders/snapshots/{snapshotId:long}")]
    public ActionResult<ApiResponse<bool>> DeleteFolderSnapshot(long snapshotId)
    {
        return _folderSnapshotService.DeleteSnapshot(snapshotId)
            ? Ok(ApiResponse<bool>.Ok(true, "快照已删除"))
            : BadRequest(ApiResponse<bool>.Error($"快照不存在: {snapshotId}", 404));
    }

    #endregion
}
