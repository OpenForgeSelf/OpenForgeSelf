using System.Text;
using ForgeSelf.Api.Plugins.TodoTracker.Entities;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 工件导入实现（PILOT-054 · BR-5）。
///
/// 读取边界（三条，缺一即 400，且把 reason 原文交回界面）：
/// <list type="bullet">
/// <item>只读 <c>&lt;项目根&gt;/docs/ai/pilot/&lt;单层目录&gt;/NN-xxx.md</c>；目录名不得含分隔符或 <c>..</c>；</item>
/// <item>拼接后再做一次「仍在项目根内」校验（防 <c>..</c> 与绝对路径穿越）；</item>
/// <item>单文件 ≤ 200 KB、单次合计 ≤ 200,000 字符，超限让人少勾几个而不是截断。</item>
/// </list>
/// 读盘失败（占用/权限）**如实冒泡为异常**，由控制器映射 500 —— 不伪装成"参数错"（plugin-development §C）。
/// </summary>
public class ArtifactImportService : IArtifactImportService
{
    /// <summary>工件目录相对项目根的固定位置（本项目 AI-Native 闭环的产物落点，见规范 §2）。
    /// 用 Path.Combine 而不是写死反斜杠：宿主跑在非 Windows 上时该常量还要继续成立。</summary>
    public static readonly string ArtifactsSubPath = Path.Combine("docs", "ai", "pilot");

    /// <summary>默认视为「核心」的工件序号：01 意图 / 02 规格 / 03 计划 / 04 工作单元（02-spec U-3 拍板默认）。</summary>
    public static readonly int[] CoreIndexes = [1, 2, 3, 4];

    private const long MaxFileBytes = 200 * 1024;
    private const int MaxTotalChars = 200_000;
    private const int MaxContentLength = 262144;

    /// <inheritdoc />
    public ArtifactListResult ListSets(string? projectRoot)
    {
        var root = ProjectPathCanonicalizer.Normalize(projectRoot);
        if (!root.Ok) return ArtifactListResult.Failed(root.Error!);
        if (!Directory.Exists(root.Root)) return ArtifactListResult.Failed($"项目目录不可达：{root.Root}");

        var pilotRoot = Path.Combine(root.Root, ArtifactsSubPath);
        if (!Directory.Exists(pilotRoot))
            return ArtifactListResult.Empty($"该项目没有 {ArtifactsSubPath} 目录（AI-Native 工件尚未产出）");

        var list = new List<ArtifactSetDto>();
        foreach (var dir in Directory.EnumerateDirectories(pilotRoot).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(dir);
            var set = new ArtifactSetDto
            {
                Dir = name,
                RelativeDir = Path.Combine(ArtifactsSubPath, name)
            };

            foreach (var file in Directory.EnumerateFiles(dir, "*.md").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(file);
                if (!IsArtifactFile(fileName)) continue;

                var index = ParseIndex(fileName);
                set.Files.Add(new ArtifactFileDto
                {
                    Name = fileName,
                    Size = new FileInfo(file).Length,
                    Index = index,
                    IsCore = index >= 0 && CoreIndexes.Contains(index)
                });
            }

            set.HasCoreFiles = set.Files.Any(f => f.IsCore);
            list.Add(set);
        }

        return ArtifactListResult.Success(list);
    }

    /// <inheritdoc />
    public async Task<ImportArtifactsResultDto> ImportAsync(int todoId, ImportArtifactsRequest request)
    {
        var todo = Todo.FindById(todoId);
        if (todo == null) return Failed($"任务 {todoId} 不存在");

        if (todo.ProjectId <= 0 && string.IsNullOrWhiteSpace(todo.ProjectRoot))
            return Failed("任务尚未关联项目：工件在项目磁盘里，先给出项目路径");

        if (request == null || string.IsNullOrWhiteSpace(request.Dir))
            return Failed("请选择工件目录（dir 不能为空）");

        var dir = request.Dir!.Trim();
        if (dir.Contains('/') || dir.Contains('\\') || dir.Contains("..") || Path.IsPathRooted(dir))
            return Failed($"工件目录名非法：{dir}（只接受 {ArtifactsSubPath} 的直接子目录名）");

        var rootCheck = ProjectPathCanonicalizer.Normalize(todo.ProjectRoot);
        if (!rootCheck.Ok) return Failed(rootCheck.Error ?? "项目根无效");

        var setDir = Path.Combine(rootCheck.Root, ArtifactsSubPath, dir);
        var guard = ProjectPathCanonicalizer.ResolveInside(rootCheck.Root, Path.Combine(ArtifactsSubPath, dir));
        if (!guard.Ok) return Failed(guard.Error!);
        if (!Directory.Exists(setDir)) return Failed($"工件目录不存在：{Path.Combine(ArtifactsSubPath, dir)}");

        var wanted = (request.Files is { Count: > 0 } ? request.Files : DefaultCoreFiles(setDir))
            .Select(f => f.Trim())
            .ToList();
        if (wanted.Count == 0) return Failed($"目录 {dir} 里没有可导入的核心工件（01~04），请手工勾选文件");

        // 先全部校验再读：任何一个文件不合规就整笔拒绝，避免"导入了一半"的正文
        var readable = new List<(string Name, string Text)>();
        var totalChars = 0;
        foreach (var name in wanted)
        {
            if (!IsArtifactFile(name)) return Failed($"文件名不合规：{name}（须形如 02-spec.md）");

            var fileGuard = ProjectPathCanonicalizer.ResolveInside(rootCheck.Root, Path.Combine(ArtifactsSubPath, dir, name));
            if (!fileGuard.Ok) return Failed(fileGuard.Error!);

            var file = fileGuard.Root;
            if (!File.Exists(file)) return Failed($"文件不存在：{Path.Combine(ArtifactsSubPath, dir, name)}");

            var size = new FileInfo(file).Length;
            if (size > MaxFileBytes) return Failed($"文件过大：{name}（{size} 字节 > {MaxFileBytes}），请只勾核心件");

            var text = await File.ReadAllTextAsync(file);
            totalChars += text.Length;
            if (totalChars > MaxTotalChars)
                return Failed($"勾选内容合计 {totalChars} 字符，超过上限 {MaxTotalChars}，请减少文件");

            readable.Add((name, text));
        }

        var content = Compose(todo, dir, readable);
        if (content.Length > MaxContentLength)
            return Failed($"组装后正文 {content.Length} 字符，超过列宽 {MaxContentLength}，请减少文件");

        if (!string.IsNullOrWhiteSpace(todo.Content) && !request.Overwrite)
            return Conflict($"任务正文已有内容（{todo.Content.Length} 字符），导入会覆盖它；确认后再来一次 overwrite=true", content.Length);

        todo.Content = content;
        todo.ArtifactRef = Path.Combine(ArtifactsSubPath, dir);
        todo.UpdatedAt = DateTime.Now;

        // 从 04-task 里提取验收判据（只在原判据为空时填，不覆盖人写过的判据）
        var extracted = ExtractAcceptance(readable);
        var filled = false;
        if (extracted.Count > 0 && string.IsNullOrWhiteSpace(todo.Acceptance))
        {
            todo.Acceptance = string.Join("\n", extracted);
            filled = true;
        }

        await todo.UpdateAsync();
        XTrace.Log.Info("[todo-tracker] 工件已导入：todo={0} dir={1} files={2} chars={3} 判据{4}",
            todoId, dir, readable.Count, content.Length, filled ? "已提取" : "未覆盖");

        return new ImportArtifactsResultDto
        {
            Ok = true,
            ArtifactRef = todo.ArtifactRef,
            Imported = readable.Select(r => r.Name).ToList(),
            ContentLength = content.Length,
            AcceptanceExtracted = filled ? extracted.Count : 0
        };
    }

    /// <summary>默认核心集合：目录里序号属于 <see cref="CoreIndexes"/> 的文件。</summary>
    private static List<string> DefaultCoreFiles(string setDir) =>
        Directory.EnumerateFiles(setDir, "*.md")
            .Select(Path.GetFileName)
            .Where(n => n != null && IsArtifactFile(n) && CoreIndexes.Contains(ParseIndex(n!)))
            .Select(n => n!)
            .ToList();

    /// <summary>
    /// 组装正文。标题行带来源（目录 + 项目根 + 导入时间），每段以 <c>### 文件名</c> 起头，
    /// 让 agent 一眼知道"这段是哪份工件"，也让界面可以反查 ArtifactRef。
    /// </summary>
    private static string Compose(Todo todo, string dir, IReadOnlyCollection<(string Name, string Text)> readable)
    {
        var sb = new StringBuilder();
        sb.Append("## 来源工件：").Append(dir).Append("（项目 ").Append(todo.ProjectRoot).Append("）\n\n");
        sb.Append("导入时间：").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.Append("；文件：").Append(string.Join("、", readable.Select(r => r.Name))).Append("\n\n");

        foreach (var (name, text) in readable)
        {
            sb.Append("---\n\n");
            sb.Append("### ").Append(name).Append("\n\n");
            sb.Append(text.Trim()).Append("\n\n");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>从 04-task（或任何含 checkbox 的文件）里提取 <c>- [ ]</c> 行作为验收判据。</summary>
    private static List<string> ExtractAcceptance(IEnumerable<(string Name, string Text)> readable) =>
        readable
            .Where(r => ParseIndex(r.Name) == 4)
            .SelectMany(r => r.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("- [ ]", StringComparison.Ordinal))
            .ToList();

    private static bool IsArtifactFile(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name!.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && ParseIndex(name) >= 0;

    /// <summary>取文件名前两位数字序号；不合规返回 -1。</summary>
    private static int ParseIndex(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name!.Length < 3) return -1;
        return char.IsDigit(name[0]) && char.IsDigit(name[1]) && int.TryParse(name.AsSpan(0, 2), out var index) ? index : -1;
    }

    private static ImportArtifactsResultDto Failed(string error) => new() { Ok = false, Error = error };

    private static ImportArtifactsResultDto Conflict(string error, int length) => new()
    {
        Ok = false,
        Conflict = true,
        Error = error,
        ContentLength = length
    };
}

/// <summary>工件目录列举结果（Ok/Error/说明 + 目录集合）。</summary>
public class ArtifactListResult
{
    public bool Ok { get; init; } = true;

    /// <summary>失败或空集合时的人话说明（界面空态直接展示，能自证成因）。</summary>
    public string? Error { get; init; }

    public List<ArtifactSetDto> Items { get; init; } = [];

    public static ArtifactListResult Success(IEnumerable<ArtifactSetDto> items) => new() { Items = items.ToList() };

    public static ArtifactListResult Empty(string reason) => new() { Error = reason };

    public static ArtifactListResult Failed(string error) => new() { Ok = false, Error = error };
}
