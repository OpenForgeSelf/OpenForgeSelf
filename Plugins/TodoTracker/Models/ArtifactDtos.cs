namespace ForgeSelf.Api.Plugins.TodoTracker.Models;

/// <summary>工件目录条目（PILOT-054 · FR-3）。九件套所在目录，来自项目根下的 docs/ai/pilot/。</summary>
public class ArtifactSetDto
{
    /// <summary>目录名（如 2026-10-07-todo-agent-dispatch）。</summary>
    public string Dir { get; set; } = string.Empty;

    /// <summary>相对项目根的完整路径（如 docs/ai/pilot/2026-10-07-todo-agent-dispatch）。</summary>
    public string RelativeDir { get; set; } = string.Empty;

    /// <summary>目录内可选的工件文件。</summary>
    public List<ArtifactFileDto> Files { get; set; } = [];

    /// <summary>是否含本插件默认视为「核心」的工件（01/02/03/04）。</summary>
    public bool HasCoreFiles { get; set; }
}

/// <summary>单个工件文件。</summary>
public class ArtifactFileDto
{
    /// <summary>文件名（如 02-spec.md）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>字节数（界面展示，也让用户自己判断要不要勾）。</summary>
    public long Size { get; set; }

    /// <summary>序号前缀（00..07），无则为 -1。</summary>
    public int Index { get; set; } = -1;

    /// <summary>是否在默认勾选集合里（01-intent/02-spec/03-plan/04-task）。</summary>
    public bool IsCore { get; set; }
}

/// <summary>导入请求。</summary>
public class ImportArtifactsRequest
{
    /// <summary>工件目录名（docs/ai/pilot 的<b>直接子目录名</b>，不接受路径分隔符与 ..）。</summary>
    public string? Dir { get; set; }

    /// <summary>要导入的文件名（同一目录内，须形如 NN-xxx.md）。空=用默认核心集合。</summary>
    public List<string>? Files { get; set; }

    /// <summary>正文已有内容时是否覆盖（false 且已有正文 ⇒ 409，界面据此提示确认）。</summary>
    public bool Overwrite { get; set; }
}

/// <summary>导入结果。</summary>
public class ImportArtifactsResultDto
{
    public bool Ok { get; set; }

    /// <summary>失败原因原文（服务端 reason，界面必须原样显示，让用户能自证成因）。</summary>
    public string? Error { get; set; }

    /// <summary>冲突（正文已有内容且未要求覆盖）。HTTP 映射 409。</summary>
    public bool Conflict { get; set; }

    public string ArtifactRef { get; set; } = string.Empty;

    /// <summary>实际导入的文件名。</summary>
    public List<string> Imported { get; set; } = [];

    /// <summary>组装后正文长度。</summary>
    public int ContentLength { get; set; }

    /// <summary>从 04-task 提取出的验收判据条数（无则为 0）。</summary>
    public int AcceptanceExtracted { get; set; }

    /// <summary>正文是否被写坏（超长等）。</summary>
    public string? Warning { get; set; }
}
