using ForgeSelf.Api.Plugins.FileTools.Models;

namespace ForgeSelf.Api.Plugins.FileTools.Services;

public interface IRenameService
{
    Task<List<RenamePreviewItem>> PreviewRenameAsync(List<string> files, List<RenameRule> rules);
    Task<RenameExecuteResult> ExecuteRenameAsync(List<string> files, List<RenameRule> rules);
    Task<RenameExecuteResult> UndoRenameAsync(string operationId);
}
