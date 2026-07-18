using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;

public interface IRuntimeDetector
{
    Task<List<RuntimeEnvironment>> DetectAllAsync();
    Task<RuntimeEnvironment> DetectAsync(ScriptLanguage language);
    string GetInterpreterPath(ScriptLanguage language);
    string GetVersion(ScriptLanguage language);
}
