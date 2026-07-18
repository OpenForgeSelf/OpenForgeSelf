namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;

public enum ScriptLanguage
{
    PowerShell,
    Python,
    NodeJs,
    Shell,
    Cmd
}

public enum ScriptParameterType
{
    String,
    Number,
    Boolean,
    Select,
    FilePath,
    DirectoryPath
}

public enum ScriptExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Timeout
}
