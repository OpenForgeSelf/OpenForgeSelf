using System.Text.RegularExpressions;
using Xunit;

namespace ForgeSelf.Api.Tests.Architecture;

/// <summary>
/// B9 收尾：全仓「受保护实体写入」允许列表门禁（QA 实现，替代 B4/B5 的白名单+方法名 grep 门禁的升级版）。
/// </summary>
/// <remarks>
/// <para>
/// <b>门禁不变量</b>（B4 写路径重排确立）：ChatMessage / AIChatMessage 是会话日志的<b>只读投影</b>，
/// 全仓只有两个投影同步器允许写入它们：
/// <list type="number">
/// <item><c>ForgeSelf.Api/Services/SessionProjectionService.cs</c>（宿主侧 ChatMessage 投影重建）；</item>
/// <item><c>Plugins/AIAgent/Services/AIAgentProjectionService.cs</c>（插件侧 AIChatMessage 投影重建）。</item>
/// </list>
/// 本门禁不再依赖「白名单文件 + SaveMessage 方法名」（旧形状被 B4 证明漏检
/// AIAgentChatCompletion 旁路），而是<b>全仓扫描实体写入调用</b>（XCode 快捷写入
/// Insert/InsertAsync/Update/Delete/Save/SaveAsync、EF SaveChanges），并用
/// <b>±12 行语句窗口</b>把每次写入归因到受保护实体——命中即报错并打印 <c>文件:行号</c>。
/// </para>
/// <para>
/// <b>归因方式</b>：受保护实体证据（new ChatMessage{...} / ChatMessage.FindAll / new AIChatMessageEntity 等）
/// 出现在写入调用 ±12 行内 → 该写入被视为对受保护实体的操作。窗口归因让「写自有实体 + 恰好提到
/// ChatMessage」的无关文件（如 ApiKeyService）不被误报，也让同文件内远离证据的无关写入不背锅。
/// </para>
/// <para>
/// <b>豁免纪律</b>：豁免必须显式条目 + 中文理由注释；生产文件只做<b>方法级</b>豁免
/// （只豁免具体写入方法，防止文件级豁免变成盾牌挡住同文件新增的其他违规写入）；
/// 不使用目录黑名单；每条豁免在防空转断言中被验证「确有命中」（防僵尸豁免）。
/// </para>
/// <para>
/// <b>已知限制</b>（grep 门禁的本质边界，记录备查）：① 只读静态可判定信息，无法做类型流分析——
/// 实体经变量转手多跳后仍可被窗口归因兜住（±12 行）；② EF DbSet.Add 后延迟 SaveChanges 的形状
/// 只能靠 SaveChanges 命中（本仓 EF 使用为 0，风险为 0）；③ 本文件自身为门禁实现，不扫描测试项目之外的
/// obj/bin/artifacts/publish/temp 等非源码目录。
/// </para>
/// </remarks>
[Collection("XCode")]
public class EntityWriteAllowlistGateTests
{
    // ─────────────── 允许清单（集中声明）：谁可以写受保护实体 ───────────────

    /// <summary>允许写入受保护实体（ChatMessage/AIChatMessage）的文件——全仓唯二投影同步器。</summary>
    private static readonly string[] AllowedWriters =
    {
        "ForgeSelf.Api/Services/SessionProjectionService.cs",
        "Plugins/AIAgent/Services/AIAgentProjectionService.cs",
    };

    // ─────────────── 豁免清单（显式条目 + 理由；生产文件只做方法级豁免） ───────────────

    /// <summary>
    /// 豁免条目。<see cref="MethodPattern"/> 为 null = 文件级豁免（仅限测试种子）；
    /// 非 null = 只豁免匹配该正则的写入方法（生产文件的唯一允许形状，防豁免盾牌化）。
    /// </summary>
    private sealed record Exemption(string File, string? MethodPattern, string Reason);

    private static readonly Exemption[] Exemptions =
    {
        // 生产文件：方法级豁免。观察 B（B6 复验发现）：DeleteSessionAsync 直删投影行而不动日志，
        // 与 SessionProjectionService.SyncAsync 的幂等重投影冲突（删后一次同步即复活）。
        // 该行为已知待修（Engineer backlog）——修复后必须删除本条目，届时门禁若因此变红即为提醒。
        new("ForgeSelf.Api/Services/MessageService.cs",
            @"\.Delete\(\)",
            "观察B：DeleteSessionAsync 直删投影行与 SyncAsync 重投影冲突，待修复（修复后删本条）"),

        // 插件侧同款（门禁首跑抓到的真实发现，与宿主侧观察 B 对称）：
        // 插件 MessageService.DeleteSessionAsync 直删 AIChatMessage 投影行而不动会话日志，
        // 与 AIAgentProjectionService 的幂等重投影冲突（删后一次同步即复活）。
        // 待修复（Engineer backlog）——修复后必须删除本条目。
        new("Plugins/AIAgent/Services/MessageService.cs",
            @"\.Delete\(\)",
            "插件侧观察B：DeleteSessionAsync 直删 AIChatMessage 投影行与投影重建冲突，待修复（修复后删本条）"),

        // 测试种子：文件级豁免——以下测试直插实体行构造前置数据，等价于投影同步器写入（只读路径的合法测试素材）。
        new("ForgeSelf.Api.Tests/Unit/MessageServiceTests.cs", null,
            "GetHistory 只读用例的种子数据直插（SaveMessageAsync 已删，种子改实体直插）"),
        new("ForgeSelf.Api.Tests/Services/SessionProjectionServiceTests.cs", null,
            "投影同步器自身的单元测试种子（验证 SyncAsync 重建逻辑的输入数据）"),
        new("ForgeSelf.Api.Tests/Integration/ChatControllerInvariantTests.cs", null,
            "不变量门禁测试的红轮素材（构造孤儿投影行/脏日志数据，锁定日志单一真相源）"),
        new("ForgeSelf.Api.Tests/Plugins/SessionManagementTests.cs", null,
            "会话管理测试的库清理与种子直插（AIChatMessageEntity/AIChatSession 清理重建）"),
        new("ForgeSelf.Api.Tests/Plugins/AIAgentProjectionAdversarialTests.cs", null,
            "QA 对抗探针的投影种子（直插 AIChatMessageEntity 模拟历史行）"),
    };

    // ─────────────── 扫描定义 ───────────────

    /// <summary>实体写入调用（XCode 快捷写入 + EF SaveChanges）。</summary>
    private static readonly Regex WritePattern =
        new(@"\.(Insert|InsertAsync|Update|UpdateAsync|Delete|DeleteAsync|Save|SaveAsync|SaveChanges)\(\s*\)",
            RegexOptions.Compiled);

    /// <summary>受保护实体证据（行级）。注意 (?![\w(]) 防止误伤 Microsoft.Extensions.AI 的
    /// ChatMessage 模型类（其构造恒带参数如 new ChatMessage(ChatRole.User, ...)）与
    /// ChatMessageModel / AIChatMessageDelta 等同名前缀 DTO。</summary>
    private static readonly Regex GuardedEvidence = new(
        @"new\s+ChatMessage(?![\w(])" +          // 实体对象初始化器（含跨行的 new ChatMessage 换行{）
        @"|new\s+ChatMessage\(\s*\)" +           // 实体无参构造（M.E.AI 模型类无此构造）
        @"|\bChatMessage\.(Find|Meta|_)" +       // 静态查询/元数据访问（写目标定位）
        @"|new\s+AIChatMessage(?![\w(])" +       // 插件实体（同上防误伤）
        @"|new\s+AIChatMessageEntity(?![\w(])" +
        @"|\bAIChatMessageEntity\.(Find|Meta|_)",
        RegexOptions.Compiled);

    /// <summary>写入归因窗口（行）：写入调用 ±N 行内出现受保护实体证据即归因。</summary>
    private const int AttributionWindow = 12;

    /// <summary>扫描的源码根目录（全仓生产 + 测试项目）。</summary>
    private static readonly string[] ScanRoots =
    {
        "ForgeSelf.Api", "ForgeSelf.Core", "ForgeSelf.Abstractions", "ForgeSelf.Web",
        "ForgeSelf.Bootstrapper", "Plugins",
        "ForgeSelf.Api.Tests", "ForgeSelf.Core.Tests", "ForgeSelf.Abstractions.Tests",
    };

    /// <summary>跳过的非源码目录名。</summary>
    private static readonly string[] SkippedDirs =
    {
        "obj", "bin", "artifacts", "publish", "temp", "build", ".git", ".vs", "node_modules",
    };

    /// <summary>扫描结果：一次静态扫描全仓的所有命中。</summary>
    private sealed record ScanResult(
        List<string> AllScannedFiles,
        List<(string File, int Line, string Method, string Text)> AllWriteMatches,
        List<string> FilesWithGuardedEvidence,
        List<(string File, int Line, string Method, string Text)> Violations);

    // ─────────────── 门禁主体 ───────────────

    [Fact]
    public void EntityWrites_OutsideAllowlist_AreForbidden()
    {
        var scan = ScanRepository();

        // ── 防空转：扫描面必须非空且符合预期规模，否则门禁在静默空转 ──
        Assert.True(scan.AllScannedFiles.Count >= 100,
            $"全仓扫描文件数异常（{scan.AllScannedFiles.Count} < 100），扫描根目录或路径解析失效");
        Assert.NotEmpty(scan.AllWriteMatches);
        Assert.NotEmpty(scan.FilesWithGuardedEvidence);

        // ── 防空转：两个合法写入者必须真实出现在受保护实体证据集中（门禁看得见它们才算在工作）──
        foreach (var allowed in AllowedWriters)
        {
            Assert.Contains(allowed, scan.FilesWithGuardedEvidence);
        }

        // ── 防僵尸豁免：每条豁免必须在基线上确有命中（文件存在且确有写入点），否则该条目已过期应删除 ──
        foreach (var exemption in Exemptions)
        {
            var path = Path.Combine(RepoRoot(), exemption.File);
            Assert.True(File.Exists(path), $"豁免条目指向不存在的文件：{exemption.File}");
            var writes = scan.AllWriteMatches.Where(w => NormalizePath(w.File) == NormalizePath(path)).ToList();
            Assert.True(writes.Count > 0,
                $"豁免条目已无对应写入点（僵尸豁免，应删除）：{exemption.File}");
        }

        // ── 门禁主体：违规 = 归因到受保护实体、且不在允许清单、且未被豁免条目覆盖的写入 ──
        var violations = scan.Violations;

        Assert.True(violations.Count == 0,
            "发现受保护实体（ChatMessage/AIChatMessage）的越权写入——一切进模型的消息只能经 ISessionStore.Append " +
            "落日志、再由投影同步器重建（文件:行号：\n" +
            string.Join("\n", violations.Select(v => $"{v.File}:{v.Line}: {v.Text.Trim()}")) + "\n" +
            "若属合法写入方，请在 AllowedWriters/Exemptions 显式登记并附理由注释。");
    }

    // ─────────────── 扫描实现 ───────────────

    private static ScanResult ScanRepository()
    {
        var root = RepoRoot();
        var allFiles = new List<string>();
        var writeMatches = new List<(string, int, string, string)>();
        var evidenceFiles = new List<string>();
        var violations = new List<(string, int, string, string)>();

        foreach (var scanRoot in ScanRoots)
        {
            var dir = Path.Combine(root, scanRoot);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
                         .Where(f => !SkippedDirs.Any(d =>
                             f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                 .Contains(d, StringComparer.OrdinalIgnoreCase))))
            {
                allFiles.Add(file);
                var lines = File.ReadAllLines(file);
                var evidenceLines = new List<int>();
                var writes = new List<(int Line, string Method, string Text)>();

                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//")) continue;          // 跳过注释行（Biz 模板注释不构成写入）

                    if (GuardedEvidence.IsMatch(line)) evidenceLines.Add(i);
                    var m = WritePattern.Match(line);
                    if (m.Success) writes.Add((i, m.Groups[1].Value, line));
                }

                if (evidenceLines.Count > 0)
                {
                    evidenceFiles.Add(NormalizePath(file));
                }

                foreach (var (lineIdx, method, text) in writes)
                {
                    writeMatches.Add((NormalizePath(file), lineIdx + 1, method, text));

                    // 窗口归因：±12 行内有受保护实体证据 → 本次写入是对受保护实体的操作
                    var attributed = evidenceLines.Any(e => Math.Abs(e - lineIdx) <= AttributionWindow);
                    if (!attributed) continue;

                    var rel = NormalizePath(file);
                    if (AllowedWriters.Contains(rel)) continue;      // 允许清单：全仓唯二投影同步器

                    var exempted = Exemptions.Any(x =>
                        NormalizePath(Path.Combine(root, x.File)) == rel
                        && (x.MethodPattern == null || Regex.IsMatch(text, x.MethodPattern)));
                    if (exempted) continue;

                    violations.Add((rel, lineIdx + 1, method, text));
                }
            }
        }

        return new ScanResult(allFiles, writeMatches, evidenceFiles, violations);
    }

    // ─────────────── 工具 ───────────────

    private static string? _repoRoot;

    /// <summary>从测试程序集输出目录向上回溯定位仓库根（含 ForgeSelf.Api 与 Plugins 的目录）。</summary>
    private static string RepoRoot()
    {
        if (_repoRoot != null) return _repoRoot;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "ForgeSelf.Api"))
                && Directory.Exists(Path.Combine(dir.FullName, "Plugins")))
            {
                _repoRoot = dir.FullName;
                return _repoRoot;
            }
            dir = dir.Parent!;
        }

        throw new InvalidOperationException("无法定位仓库根（未找到同时含 ForgeSelf.Api 与 Plugins 的目录）");
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/').Replace(RepoRoot().Replace('\\', '/') + "/", "");
}
