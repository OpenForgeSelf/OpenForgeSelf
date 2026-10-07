using ForgeSelf.Api.Plugins.TodoTracker.Models;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 工件导入服务（PILOT-054 · FR-3）：把项目里的 <c>docs/ai/pilot/&lt;task-id&gt;/</c> 工件
/// 组装成任务正文，让「下发」不必靠人复制粘贴。结果类型见 <see cref="ArtifactListResult"/>。
/// </summary>
public interface IArtifactImportService
{
    /// <summary>列出项目根下的工件目录（含各目录可选文件）。项目根缺失/不可达时返回失败结果而非抛异常。</summary>
    ArtifactListResult ListSets(string? projectRoot);

    /// <summary>把选中的文件组装成正文并写回任务（正文已有时需显式 overwrite）。</summary>
    Task<ImportArtifactsResultDto> ImportAsync(int todoId, ImportArtifactsRequest request);
}
