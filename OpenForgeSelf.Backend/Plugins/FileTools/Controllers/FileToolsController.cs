using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.FileTools.Models;
using OpenForgeSelf.Backend.Plugins.FileTools.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.FileTools.Controllers;

[ApiController]
[Route("api/filetools")]
public class FileToolsController : ControllerBase
{
    private readonly IRenameService _renameService;
    private readonly ICleanupService _cleanupService;
    private readonly IArchiveService _archiveService;
    private readonly IFileStatsService _fileStatsService;

    public FileToolsController(
        IRenameService renameService,
        ICleanupService cleanupService,
        IArchiveService archiveService,
        IFileStatsService fileStatsService)
    {
        _renameService = renameService;
        _cleanupService = cleanupService;
        _archiveService = archiveService;
        _fileStatsService = fileStatsService;
    }

    [HttpGet]
    public ActionResult<ApiResponse<object>> GetOverview()
    {
        var tools = new
        {
            rename = new[] { "preview", "execute" },
            cleanup = new[] { "preview", "execute", "empty-folders", "duplicates" },
            archive = new[] { "compress", "extract", "info" },
            stats = new[] { "directory", "large-files", "types" }
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
}
