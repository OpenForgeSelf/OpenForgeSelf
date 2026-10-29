namespace ForgeSelf.Abstractions;

/// <summary>项目目录下的一项（文件或子目录）。</summary>
public class ProjectFileEntry
{
    public string Name { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 项目工作区契约：管理「Agent 选择的一个目录当做一个项目」。
/// 选定的根目录作为项目根，所有读/写/列目录都相对该根进行，并做路径穿越防护。
/// 契约位于 Abstractions（2026-10-28 工具命名空间统一批次：mcp-center 的公共工具
/// run_terminal_command/list_files 需跨插件消费本契约，架构铁律 2=跨插件契约必须入 Abstractions）；
/// 实现仍在 AIAgent 插件（AIAgentPlugin.cs 经 ctx.Register 进共享表，消费方 ctx.Get 运行期解析，禁止缓存）。
/// </summary>
public interface IProjectWorkspaceService
{
    /// <summary>是否已选定项目根目录。</summary>
    bool IsProjectSet { get; }

    /// <summary>当前项目根目录绝对路径；未选中时为 null。</summary>
    string? ProjectRoot { get; }

    /// <summary>
    /// 未选项目目录时的回退根；null=无法确定，调用方应给引导错误。
    /// 解析优先级：环境变量 FORGESELF_DEFAULT_PROJECT_ROOT（存在且为目录）→ AppContext.BaseDirectory 上溯含 ForgeSelf.slnx/.git 的祖先 → null。
    /// </summary>
    string? DefaultRoot { get; }

    /// <summary>设定项目根目录。返回 false 并给出错误（目录不存在/不是目录）。</summary>
    bool TrySetProjectRoot(string root, out string? error);

    /// <summary>安全解析相对路径为绝对路径；越界/不存在时抛 <see cref="InvalidOperationException"/>。</summary>
    string ResolveSafePath(string relativePath);

    /// <summary>列出相对路径下的目录项（顶层传空串）。</summary>
    List<ProjectFileEntry> ListEntries(string relativePath);

    /// <summary>读取文件内容（UTF-8）。</summary>
    string ReadFile(string relativePath);

    /// <summary>写入文件内容（UTF-8，自动创建/覆盖），父目录不存在时自动创建。</summary>
    void WriteFile(string relativePath, string content);

    /// <summary>删除文件。</summary>
    void DeleteFile(string relativePath);
}
