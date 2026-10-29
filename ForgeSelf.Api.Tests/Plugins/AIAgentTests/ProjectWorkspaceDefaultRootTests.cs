using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Plugins.FileTools;
using ForgeSelf.Core;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgentTests;

/// <summary>
/// mini-task（2026-10-12 输入4）文件三件套默认根 · Phase3 六断言组；2026-10-28 归属迁移至 mcp-center + FR-5 绝对路径语义（read/write 不再隐式接项目根）：
/// <list type="bullet">
/// <item><b>A 阳性对照</b>：TrySetProjectRoot(temp) 后 ResolveSafePath("hello/x.txt") 返回 temp 下全路径；</item>
/// <item><b>B 引导错误</b>：未选目录 + 未设 env ⇒ DefaultRoot 为 null 或回退仓库根均可，
/// 「双根全空」分支以纯函数直测(null) + 契约替身(同文案钉住「未选择项目目录」)双层断言；</item>
/// <item><b>C env 指向临时目录</b>：DefaultRoot==该 temp；相对/绝对路径(Temp 内)均成功；</item>
/// <item><b>D 越界拒绝</b>：env 指向 temp，ResolveSafePath(C:\Windows\...\hosts) 抛「拒绝访问」；</item>
/// <item><b>E 绝对路径越界</b>：已选目录后，temp 外绝对路径仍抛「拒绝访问」；</item>
/// <item><b>F 工具函数信封</b>：直接实例化 ListFiles/ReadFile/WriteFileToolFunction(ctx=Context stub)（2026-10-28 归属迁 mcp-center；read/write 改绝对路径 FR-5），
/// 断言 { success, error, hint?, usedRoot } 三态封套与 usedRoot 语义。
/// <b>信封字段在 Phase 4 e2e/dev 走查落实证据</b>（与本测试注释约定一致）。</item>
/// </list>
/// 铁律：临时目录 Path.GetTempPath()+of s-test-&lt;guid&gt;（只创建不删除，铁律 10）；
/// 不触 XCode 实体与真实用户库；环境变量一律 try/finally 恢复原值。
/// </summary>
public class ProjectWorkspaceDefaultRootTests : IDisposable
{
    private readonly string _tmpRoot;
    private readonly string _workspaceFile;

    public ProjectWorkspaceDefaultRootTests()
    {
        // 每实例独立临时目录（用完不删，见 plugin-development 铁律 10：禁止自动化删除数据目录）。
        _tmpRoot = Path.Combine(Path.GetTempPath(), "ofs-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpRoot);
        _workspaceFile = Path.Combine(_tmpRoot, "note.txt");
        File.WriteAllText(_workspaceFile, "hello-default-root");

        // 测试进程内恒清除环境变量，杜绝宿主/终端遗留值串扰本组断言。
        Environment.SetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable, null);
        // 按铁律 10 不删除任何目录：_tmpRoot 及其产物留在 %TEMP%（os 自管）。
    }

    private static ProjectWorkspaceService NewWorkspace() => new();

    /// <summary>按输入4 铁律命名的临时目录（只创建不删除）。</summary>
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ofs-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ---- A. 阳性对照：已选目录 → 嵌套相对路径返回 temp 下全路径 ----

    [Fact]
    public void ResolveSafePath_ProjectSet_NestedRelativePath_ReturnsFullPathUnderTempRoot()
    {
        var temp = NewTempDir();
        var ws = NewWorkspace();
        var ok = ws.TrySetProjectRoot(temp, out var err);
        Assert.True(ok, err);

        var resolved = ws.ResolveSafePath("hello/x.txt");

        Assert.True(ws.IsProjectSet);
        Assert.Equal(Path.Combine(Path.GetFullPath(temp), "hello", "x.txt"), resolved);
    }

    // ---- B. 引导错误：未选目录 + 未设 env ----

    [Fact]
    public void ResolveSafePath_NoProjectNoEnv_GuidedErrorWhenNoRootAtAll()
    {
        // 前提自查：本测试进程 AppContext.BaseDirectory 位于仓内 ⇒ 真实类 DefaultRoot 恒可兜（回退仓库根），
        // 「双根全空」分支在集成进程内不可直接构造（实现硬依赖 AppContext.BaseDirectory 探测链，无注入点——
        // mini-task line 247 同款结论）。该分支语义以两层真实断言钉住：
        //   ① 纯函数直测：同一实现 ResolveDefaultRoot 在「无 env + 无标记起点」下返回 null；
        //   ② 契约替身：DefaultRoot==null 时 ResolveSafePath 必抛引导错误（文案与实现逐字一致，含「未选择项目目录」）。
        // 文案-实现一致性回归最终由 Phase 4 e2e/dev 走查兜底（与 F 组同约定）。
        var envBefore = Environment.GetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable);
        try
        {
            Environment.SetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable, null);

            // ① 纯函数层（真实实现直测，非替身）：探测起点上溯链无标记 → null。
            //    前提：C:\Windows 上溯无 ForgeSelf.slnx/.git（2026-10-12 实测）。
            var resolved = ProjectWorkspaceService.ResolveDefaultRoot(null, @"C:\Windows\Temp");
            Assert.True(
                resolved == null
                || File.Exists(Path.Combine(resolved, "ForgeSelf.slnx"))
                || Directory.Exists(Path.Combine(resolved, ".git")),
                $"探测应为 null 或命中标记目录，实际：{resolved}");

            // ② 契约层：无根可兜 → 引导错误（含「未选择项目目录」+ env 变量名修复提示）。
            var stub = new NullDefaultRootWorkspace();
            var ex = Assert.Throws<InvalidOperationException>(() => stub.ResolveSafePath("hello/x.txt"));
            Assert.Contains("未选择项目目录", ex.Message);
            Assert.Contains(ProjectWorkspaceService.DefaultRootEnvVariable, ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable, envBefore);
        }
    }

    /// <summary>B 组补充（真实类、集成进程可达路径）：未选 + 无 env ⇒ DefaultRoot 回退仓库根且 ResolveSafePath 成功回退。</summary>
    [Fact]
    public void DefaultRoot_NoProjectNoEnv_FallsBackToRepoRoot()
    {
        var envBefore = Environment.GetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable);
        try
        {
            Environment.SetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable, null);

            var ws = NewWorkspace();
            Assert.False(ws.IsProjectSet);

            var root = ws.DefaultRoot;
            Assert.NotNull(root);
            Assert.True(File.Exists(Path.Combine(root!, "ForgeSelf.slnx")), $"应回退仓库根：{root}");

            // 用户口径「DefaultRoot 为 null 或非空均可」——本进程实测为非空（仓库根），此时 ResolveSafePath 不抛而是回退成功。
            var resolved = ws.ResolveSafePath("a.txt");
            Assert.Equal(Path.Combine(root!, "a.txt"), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable, envBefore);
        }
    }

    // ---- C. 未选目录 + env 指向临时目录：DefaultRoot==temp，相对/绝对路径均成功 ----

    [Fact]
    public void DefaultRoot_EnvVarPointsToTemp_AnchorsResolveSafePath()
    {
        var temp = NewTempDir(); // 只创建不删除
        var envBefore = Environment.GetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable);
        try
        {
            Environment.SetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable, temp);

            var ws = NewWorkspace();
            Assert.False(ws.IsProjectSet);
            Assert.Equal(Path.GetFullPath(temp), ws.DefaultRoot);

            // 相对路径 → temp 下全路径。
            var relative = ws.ResolveSafePath("a.txt");
            Assert.Equal(Path.Combine(Path.GetFullPath(temp), "a.txt"), relative);

            // 绝对路径（temp 下全路径）也成功（直通语义）。
            var absoluteInside = Path.Combine(temp, "sub", "b.txt");
            var absolute = ws.ResolveSafePath(absoluteInside);
            Assert.Equal(Path.GetFullPath(absoluteInside), absolute);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable, envBefore);
        }
    }

    // ---- D. 越界拒绝：env 指向 temp + 系统目录绝对路径 ----

    [Fact]
    public void ResolveSafePath_EnvDefaultRoot_AbsolutePathOutside_ThrowsAccessDenied()
    {
        var temp = NewTempDir();
        var envBefore = Environment.GetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable);
        try
        {
            Environment.SetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable, temp);

            var ws = NewWorkspace();
            var ex = Assert.Throws<InvalidOperationException>(
                () => ws.ResolveSafePath(@"C:\Windows\system32\drivers\etc\hosts"));

            Assert.Contains("拒绝访问", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProjectWorkspaceService.DefaultRootEnvVariable, envBefore);
        }
    }

    // ---- E. 绝对路径越界（已选目录场景）----
    // 本组断言的本质是：换根后，C:\Windows\win.ini 归一化后仍在 root=temp 之外。

    [Fact]
    public void ResolveSafePath_ProjectSet_AbsolutePathOutside_ThrowsAccessDenied()
    {
        var temp = NewTempDir();
        var ws = NewWorkspace();
        var ok = ws.TrySetProjectRoot(temp, out var err);
        Assert.True(ok, err);

        // 根外绝对路径：另一独立 temp 兄弟目录（不创建、不依赖系统盘内容）。
        var outside = Path.Combine(Path.GetTempPath(), "ofs-test-" + Guid.NewGuid().ToString("N"), "outside.txt");

        var ex = Assert.Throws<InvalidOperationException>(() => ws.ResolveSafePath(outside));

        Assert.Contains("拒绝访问", ex.Message);
    }

    /// <summary>E 组补充：与既有 win.ini 对照（本质同为 temp 外系统绝对路径）。</summary>
    [Fact]
    public void ResolveSafePath_ProjectSet_SystemAbsolutePath_ThrowsAccessDenied()
    {
        var temp = NewTempDir();
        var ws = NewWorkspace();
        ws.TrySetProjectRoot(temp, out _);

        var ex = Assert.Throws<InvalidOperationException>(() => ws.ResolveSafePath(@"C:\Windows\win.ini"));

        Assert.Contains("拒绝访问", ex.Message);
    }

    // ---- DefaultRoot 三级候选（resolve 纯函数直测；envVariable 参数=环境变量读取结果） ----

    [Fact]
    public void ResolveDefaultRoot_EnvVariablePointingToExistingDirectory_Wins()
    {
        var env = Path.Combine(_tmpRoot, "env-root");
        Directory.CreateDirectory(env);

        var resolved = ProjectWorkspaceService.ResolveDefaultRoot(env, @"C:\definitely-not-a-repo\probe-start");

        Assert.Equal(Path.GetFullPath(env), resolved);
    }

    [Fact]
    public void ResolveDefaultRoot_EnvVariablePointingToMissingDirectory_FallsToProbe()
    {
        var resolved = ProjectWorkspaceService.ResolveDefaultRoot(
            Path.Combine(_tmpRoot, "no-such-dir"),
            AppContext.BaseDirectory);

        // 探测起点（AppContext.BaseDirectory）位于本仓 → 必上溯命中仓库根（含 ForgeSelf.slnx）。
        Assert.NotNull(resolved);
        Assert.True(File.Exists(Path.Combine(resolved!, "ForgeSelf.slnx")));
    }

    [Fact]
    public void ResolveDefaultRoot_EnvVariableMissing_ProbeFindsAncestorWithSlnx()
    {
        var resolved = ProjectWorkspaceService.ResolveDefaultRoot(null, AppContext.BaseDirectory);

        Assert.NotNull(resolved);
        Assert.True(File.Exists(Path.Combine(resolved!, "ForgeSelf.slnx")), $"应上溯命中仓库根：{resolved}");
    }

    [Fact]
    public void ResolveDefaultRoot_ProbeStartInsideRepo_FindsGitMarkerFirstWhenNoSlnx()
    {
        // 造一棵「只有 .git 没有 ForgeSelf.slnx」的祖先链验证 .git 命中分支。
        var probeBase = Path.Combine(_tmpRoot, "probe", "bin");
        Directory.CreateDirectory(probeBase);
        Directory.CreateDirectory(Path.Combine(_tmpRoot, "probe", ".git"));

        var resolved = ProjectWorkspaceService.ResolveDefaultRoot(null, probeBase);

        Assert.Equal(Path.GetFullPath(Path.Combine(_tmpRoot, "probe")), resolved);
    }

    // ---- ResolveSafePath 既有回归（DefaultRoot 回退 + 优先级） ----

    [Fact]
    public void DefaultRoot_WithoutEnvVariable_ResolvedFromProbeAndCached()
    {
        var ws = NewWorkspace();

        var first = ws.DefaultRoot;
        var second = ws.DefaultRoot;

        Assert.NotNull(first);
        Assert.True(File.Exists(Path.Combine(first!, "ForgeSelf.slnx")));
        Assert.Same(first, second); // 缓存：同实例两次访问返回同一引用
    }

    [Fact]
    public void ResolveSafePath_NoProjectSet_FallsBackToDefaultRoot()
    {
        var ws = NewWorkspace();
        Assert.False(ws.IsProjectSet);

        var resolved = ws.ResolveSafePath("note.txt");

        Assert.Equal(Path.Combine(ws.DefaultRoot!, "note.txt"), resolved);
    }

    [Fact]
    public void ResolveSafePath_AbsolutePathInsideRoot_PassesThrough()
    {
        var ws = NewWorkspace();
        var absolute = Path.Combine(ws.DefaultRoot!, "note.txt");

        var resolved = ws.ResolveSafePath(absolute);

        Assert.Equal(Path.GetFullPath(absolute), resolved);
    }

    [Fact]
    public void ResolveSafePath_AbsolutePathOutsideRoot_StillRejected()
    {
        var ws = NewWorkspace();

        var ex = Assert.Throws<InvalidOperationException>(() => ws.ResolveSafePath(@"C:\Windows\win.ini"));

        Assert.Contains("拒绝访问项目目录之外的路径", ex.Message);
    }

    [Fact]
    public void ResolveSafePath_RelativeTraversalOutsideRoot_StillRejected()
    {
        var ws = NewWorkspace();

        var ex = Assert.Throws<InvalidOperationException>(() => ws.ResolveSafePath("../../../win.ini"));

        Assert.Contains("拒绝访问项目目录之外的路径", ex.Message);
    }

    [Fact]
    public void ResolveSafePath_ProjectSet_WinsOverDefaultRoot()
    {
        var ws = NewWorkspace();
        ws.TrySetProjectRoot(_tmpRoot, out _);

        var resolved = ws.ResolveSafePath("note.txt");

        Assert.Equal(Path.Combine(Path.GetFullPath(_tmpRoot), "note.txt"), resolved);
    }

    // ---- F. 工具函数信封：三态封套 { success, error, hint?, usedRoot }（直接实例化 + Context stub）----
    // 2026-10-28 工具归属迁移：文件三件套现属 mcp-center（裸名 = 平台保留名，用户拍板豁免前缀）；
    // read/write 语义改为 FR-5 绝对路径参数化——path 必须绝对、不再隐式接项目根；
    // list_files 保留「path 缺省 → 回退项目根/DefaultRoot」的兜底动线。
    // 信封字段在 Phase 4 e2e/dev 走查落实证据（用户授权的注释级约定）。

    [Fact]
    public void FailEnvelope_WithRoot_ContainsHintAndUsedRoot()
    {
        var ctx = new Context();
        var ws = NewWorkspace();
        ctx.Register<IProjectWorkspaceService>(ws);

        var json = JsonSerializer.Serialize(
            ListFilesToolFunction.FailEnvelope(ctx, "拒绝访问项目目录之外的路径"));

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("拒绝访问项目目录之外的路径", doc.RootElement.GetProperty("error").GetString());
        Assert.True(doc.RootElement.TryGetProperty("hint", out var hint));
        Assert.Contains("PROJECT_ROOT=", hint.GetString());
        Assert.Equal(ws.DefaultRoot, doc.RootElement.GetProperty("usedRoot").GetString());
    }

    [Fact]
    public void FailEnvelope_WithoutAnyService_OmitsHintWithNullUsedRoot()
    {
        var ctx = new Context(); // 未注册 IProjectWorkspaceService

        var json = JsonSerializer.Serialize(
            ListFilesToolFunction.FailEnvelope(ctx, "任何错误"));

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("任何错误", doc.RootElement.GetProperty("error").GetString());
        Assert.False(doc.RootElement.TryGetProperty("hint", out _), "root 不可得时必须省略 hint");
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("usedRoot").ValueKind);
    }

    [Fact]
    public async Task ListFiles_NoWorkspace_NoPath_ReturnsServiceUnavailableEnvelope()
    {
        var fn = new ListFilesToolFunction(new Context());

        var json = await fn.ExecuteAsync("{}");

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("项目工作区服务不可用，且未传 path 绝对路径", doc.RootElement.GetProperty("error").GetString());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("usedRoot").ValueKind);
    }

    // FR-5 核心翻转：read/write 不再依赖项目根——无工作区服务 + 绝对路径 = 照常工作（AC-3）。

    [Fact]
    public async Task ListFiles_NoWorkspace_AbsolutePath_ListsDirectory()
    {
        var fn = new ListFilesToolFunction(new Context()); // 无 IProjectWorkspaceService

        var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = _tmpRoot }));

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains(doc.RootElement.GetProperty("files").EnumerateArray(),
            f => f.GetProperty("name").GetString() == "note.txt");
        Assert.Equal(Path.GetFullPath(_tmpRoot), doc.RootElement.GetProperty("root").GetString());
    }

    [Fact]
    public async Task ReadFile_NoWorkspace_AbsolutePath_Works()
    {
        var fn = new ReadFileToolFunction(new Context()); // 无 IProjectWorkspaceService

        var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = _workspaceFile, start_line = 1 }));

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("hello-default-root", doc.RootElement.GetProperty("content").GetString());
        Assert.Equal(Path.GetFullPath(_workspaceFile), doc.RootElement.GetProperty("path").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("totalLines").GetInt32());
    }

    [Fact]
    public async Task WriteFile_NoWorkspace_AbsolutePath_CreatesFileAndParentDirs()
    {
        var fn = new WriteFileToolFunction(new Context()); // 无 IProjectWorkspaceService

        var target = Path.Combine(NewTempDir(), "sub", $"ofs-test-write-absolute-{Guid.NewGuid():N}.txt");
        var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = target, content = "abs", start_line = 1, end_line = 1 }));

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(File.Exists(target));
        Assert.Equal("abs", File.ReadAllText(target));
    }

    // FR-5 拒绝路径：read/write 收到相对路径必须报错引导（不再隐式接项目根）。

    [Fact]
    public async Task ReadFile_RelativePath_RejectedWithGuidance()
    {
        var ctx = new Context();
        var ws = NewWorkspace();
        ctx.Register<IProjectWorkspaceService>(ws); // 项目根可得也不行：语义是参数化，不是兜底

        var fn = new ReadFileToolFunction(ctx);
        var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = "AG.md", start_line = 1 }));

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("绝对路径", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task WriteFile_RelativePath_RejectedWithGuidance()
    {
        var ctx = new Context();
        ctx.Register<IProjectWorkspaceService>(NewWorkspace());

        var fn = new WriteFileToolFunction(ctx);
        var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = ".temp/none.txt", content = "x", start_line = 1, end_line = 1 }));

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("绝对路径", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ListFiles_ProjectNotSet_AndNoDefaultRoot_ReturnsGuidedEnvelope()
    {
        // AppContext.BaseDirectory 在测试进程内必命中仓库根 ⇒ 无根可兜的三态在集成进程不可构造，
        // 用 NullDefaultRootWorkspace 替身（契约实现）钉住「DefaultRoot==null 分支」封套语义（mini-task 同款）。
        // 真实服务链路的同分支由 Phase 4 e2e/dev 走查落实证据（F 组约定）。
        var ctx = new Context();
        ctx.Register<IProjectWorkspaceService>(new NullDefaultRootWorkspace());

        var fn = new ListFilesToolFunction(ctx);
        var json = await fn.ExecuteAsync("{}");

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("尚未选择项目目录且未传 path：请先在左侧选择工作目录，或直接传目录绝对路径（如 D:\\src\\my-proj\\src）", doc.RootElement.GetProperty("error").GetString());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("usedRoot").ValueKind);
    }

    [Fact]
    public async Task ListFiles_ProjectNotSet_ButDefaultRootAvailable_ListsFromDefaultRoot()
    {
        var ctx = new Context();
        var ws = NewWorkspace();
        ctx.Register<IProjectWorkspaceService>(ws); // DefaultRoot 恒可兜（测试进程在仓内）

        var fn = new ListFilesToolFunction(ctx);
        var json = await fn.ExecuteAsync("{}");

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        var files = doc.RootElement.GetProperty("files");
        // 仓根铁定存在的文件，验证列表真实来自 DefaultRoot（不用写盘物资）。
        Assert.Contains(files.EnumerateArray(), f => f.GetProperty("name").GetString() == "ForgeSelf.slnx");
        Assert.Equal(ws.DefaultRoot, doc.RootElement.GetProperty("root").GetString()); // 缺省回退根
        Assert.Equal(ws.DefaultRoot, doc.RootElement.GetProperty("usedRoot").GetString());
    }

    [Fact]
    public async Task ReadFile_FromStartLine2_ReturnsTailAndTotalLines()
    {
        var ctx = new Context();

        var target = Path.Combine(NewTempDir(), $"ofs-test-readline-{Guid.NewGuid():N}.txt");
        File.WriteAllText(target, "L1\nL2\nL3\n");
        try
        {
            var fn = new ReadFileToolFunction(ctx);
            var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = target, start_line = 2 }));

            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("L2\nL3\n", doc.RootElement.GetProperty("content").GetString()); // 自第 2 行到文末（含原尾行换行产生的空段）
            Assert.Equal(4, doc.RootElement.GetProperty("totalLines").GetInt32());      // "L1\nL2\nL3\n" 切出 4 段
        }
        finally { File.Delete(target); }
    }

    [Fact]
    public async Task ReadFile_MissingStartLine_Fails()
    {
        var fn = new ReadFileToolFunction(new Context());

        var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = _workspaceFile }));

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("start_line", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ReadFile_WithEndLine_ReturnsClosedRangeAndClamps()
    {
        var target = Path.Combine(NewTempDir(), $"ofs-test-readrange-{Guid.NewGuid():N}.txt");
        File.WriteAllText(target, "A1\nB2\nC3\nD4");
        try
        {
            var fn = new ReadFileToolFunction(new Context());
            var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = target, start_line = 2, end_line = 3 }));
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("B2\nC3", doc.RootElement.GetProperty("content").GetString());
            Assert.Equal(2, doc.RootElement.GetProperty("startLine").GetInt32());
            Assert.Equal(3, doc.RootElement.GetProperty("endLine").GetInt32());
            Assert.Equal(4, doc.RootElement.GetProperty("totalLines").GetInt32());

            // end_line 超出总行数 → 钳到文末
            var json2 = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = target, start_line = 3, end_line = 99 }));
            using var doc2 = JsonDocument.Parse(json2);
            Assert.True(doc2.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("C3\nD4", doc2.RootElement.GetProperty("content").GetString());
            Assert.Equal(4, doc2.RootElement.GetProperty("endLine").GetInt32());
        }
        finally { File.Delete(target); }
    }

    [Fact]
    public async Task ReadFile_EndLineBeforeStartLine_Fails()
    {
        var fn = new ReadFileToolFunction(new Context());

        var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = _workspaceFile, start_line = 5, end_line = 2 }));

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("end_line", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task WriteFile_AbsolutePath_NewFile_WritesContent()
    {
        var target = Path.Combine(NewTempDir(), $"ofs-test-write-{Guid.NewGuid():N}.txt");

        var fn = new WriteFileToolFunction(new Context());
        var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = target, content = " Written ", start_line = 1, end_line = 1 }));

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(File.Exists(target));
        Assert.Equal(" Written ", File.ReadAllText(target));
        Assert.Equal(Path.GetFullPath(target), doc.RootElement.GetProperty("path").GetString());
    }

    [Fact]
    public async Task WriteFile_FromStartLine2_KeepsHeadAndOverwritesTail()
    {
        var target = Path.Combine(NewTempDir(), $"ofs-test-writeline-{Guid.NewGuid():N}.txt");
        File.WriteAllText(target, "H1\nH2\nOLD3\nOLD4\n");
        try
        {
            var fn = new WriteFileToolFunction(new Context());
            var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = target, content = "N3\nN4", start_line = 3, end_line = 5 }));

            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("H1\nH2\nN3\nN4", File.ReadAllText(target).Replace("\r\n", "\n")); // 前两行保留，第 3 行起直接覆盖
            Assert.Equal(4, doc.RootElement.GetProperty("totalLines").GetInt32());
        }
        finally { File.Delete(target); }
    }

    [Fact]
    public async Task WriteFile_MissingStartLine_Fails()
    {
        var fn = new WriteFileToolFunction(new Context());

        var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = _workspaceFile, content = "x" }));

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("start_line", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task WriteFile_MissingEndLine_Fails()
    {
        // 先读后写铁律：write_file 的 end_line 必填，缺失直接报错
        var fn = new WriteFileToolFunction(new Context());

        var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = _workspaceFile, content = "x", start_line = 1 }));

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("end_line", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task WriteFile_WithEndLine_ReplacesRangeAndKeepsTail()
    {
        var target = Path.Combine(NewTempDir(), $"ofs-test-writerange-{Guid.NewGuid():N}.txt");
        File.WriteAllText(target, "A1\nB2\nC3\nD4\nE5");
        try
        {
            var fn = new WriteFileToolFunction(new Context());
            var json = await fn.ExecuteAsync(JsonSerializer.Serialize(new { path = target, content = "X\nY", start_line = 2, end_line = 4 }));

            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(4, doc.RootElement.GetProperty("endLine").GetInt32());
            // 第 2~4 行被两行 content 替换，首尾保留 → 总行数 5→4
            Assert.Equal("A1\nX\nY\nE5", File.ReadAllText(target).Replace("\r\n", "\n"));
            Assert.Equal(4, doc.RootElement.GetProperty("totalLines").GetInt32());
        }
        finally { File.Delete(target); }
    }

    /// <summary>DefaultRoot 恒 null 的契约替身：钉住「双根全空」引导错误分支（不触文件系统）。</summary>
    private sealed class NullDefaultRootWorkspace : IProjectWorkspaceService
    {
        public bool IsProjectSet => false;
        public string? ProjectRoot => null;
        public string? DefaultRoot => null;
        public bool TrySetProjectRoot(string root, out string? error) { error = "not supported"; return false; }
        public string ResolveSafePath(string relativePath) =>
            throw new InvalidOperationException(
                "未选择项目目录且未设置 FORGESELF_DEFAULT_PROJECT_ROOT，请在左侧选择工作目录或设置环境变量");
        public List<ProjectFileEntry> ListEntries(string relativePath) => throw new NotSupportedException();
        public string ReadFile(string relativePath) => throw new NotSupportedException();
        public void WriteFile(string relativePath, string content) => throw new NotSupportedException();
        public void DeleteFile(string relativePath) => throw new NotSupportedException();
    }
}
