using OpenForgeSelf.Backend.Plugins.FileTools.Models;

namespace OpenForgeSelf.Backend.Plugins.FileTools.Services;

public interface IRenameService
{
    Task<List<RenamePreviewItem>> PreviewRenameAsync(List<string> files, List<RenameRule> rules);
    Task<RenameExecuteResult> ExecuteRenameAsync(List<string> files, List<RenameRule> rules);
    Task<RenameExecuteResult> UndoRenameAsync(string operationId);
}
