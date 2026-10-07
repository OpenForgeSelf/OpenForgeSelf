using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Entities;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using ForgeSelf.Core;
using XCode;
using Xunit;
using TodoStatus = ForgeSelf.Api.Plugins.TodoTracker.TodoStatus;

namespace ForgeSelf.Api.Tests.Plugins.TodoTracker;

/// <summary>
/// 下发主链路（PILOT-054 · AC-3/5/6/7/10/11/12）：真库、真文件、零 mock 业务数据。
///
/// 唯一被替换的是<b>宿主项目注册表</b>（测试环境没有宿主）：这里用一份刻意做得比宿主更严的假实现 ——
/// 它只认「字符串完全相等」，正如真实宿主 <c>HostProjectRegistry.cs:71</c> 的 <c>Project.FindByRoot</c>。
/// 于是"同一路径算同一项目"这件事必须由插件的归一器挣来，而不是靠假实现放水。
/// </summary>
[Collection("XCode")]
public class TodoDispatchFlowTests : IClassFixture<XCodeTestFixture>
{
    private readonly TodoProjectService _projects;
    private readonly TodoService _todos;
    private readonly TaskExecutionService _records = new();
    private readonly ArtifactImportService _artifacts = new();
    private readonly TodoDispatchService _dispatch;

    /// <summary>真实存在的项目根：本仓库自己（它下面就有 docs/ai/pilot）。</summary>
    private static readonly string RepoRoot = LocateRepoRoot();

    public TodoDispatchFlowTests(XCodeTestFixture fixture)
    {
        var ctx = new Context();
        ctx.Register<IProjectRegistry>(_sharedRegistry);
        _projects = new TodoProjectService(ctx);
        _todos = new TodoService(_projects, _records);
        _dispatch = new TodoDispatchService(_records, new AgentTaskGateway(new Context()), _projects);
        _ = fixture;
    }

    [Fact]
    public void 同一目录的四种写法应关联到同一个项目且宿主只多一条档案()
    {
        var forms = new[]
        {
            RepoRoot,
            RepoRoot.Replace('\\', '/'),
            RepoRoot + "\\",
            ToMsysForm(RepoRoot)
        };

        var resolved = new List<ResolveProjectResult>();
        foreach (var form in forms) resolved.Add(_projects.Resolve(form, registerIfMissing: true));

        resolved.Should().OnlyContain(r => r.Ok, "本仓库根必然存在，四种写法都该被归一并命中");
        // 阳性对照：全是 0 也能"只有一个不同值"，所以必须先钉住"每个都拿到了真实档案 id"
        resolved.Select(r => r.ProjectId).Should().OnlyContain(id => id > 0,
            "关联必须落到宿主项目档案的真实 id（0 = 没登记上，项目过滤/计数/归并会全部失效）");
        resolved.Select(r => r.ProjectId).Distinct().Should().HaveCount(1,
            "用户要求：一致的路径认为是同一个项目");
        resolved.Select(r => r.Root).Distinct().Should().HaveCount(1, "存进库的项目根必须是同一串归一结果");
        // 首次登记只可能发生在"这一条目录第一次被关联"时（同类其它用例可能先跑，所以这里断"至多一次"而不是"恰好一次"）
        resolved.Count(r => r.Registered).Should().BeLessThanOrEqualTo(1, "四种写法最多只能登记一次，否则就是同一项目被登记成多条档案");

        // 宿主档案只长了一条（假实现按字符串精确匹配，若插件不归一，这里就会变成 4 条）
        _sharedRegistry.Count.Should().Be(1);
    }

    /// <summary>
    /// 显式「关联项目」必须登记宿主档案、让任务拿到<b>真实</b> ProjectId。
    ///
    /// 缺陷形态（2026-10-07 浏览器走查实测抓到）：详情面板显示「未关联项目」，列表却显示已关联 ——
    /// 因为 <c>LinkProjectAsync</c> 走了 <c>registerIfMissing:false</c>，宿主里没有该目录的档案时
    /// ProjectId 落成 0；界面按 <c>projectId</c> 判关联、列表按 <c>projectRoot</c> 显示，两边当场分叉，
    /// 项目过滤 / 项目计数 / "一致路径归同一项目" 全部失效。
    /// 上面那条"四种写法"用例走的是 <c>_projects.Resolve</c> 直连，绕过了服务入口，所以照绿 —— 故本用例必须走服务。
    ///
    /// 本用例<b>自带一份注册表与服务</b>：类内 <c>_sharedRegistry</c> 是 static，往里登记会污染
    /// 别处"宿主只多一条档案"的断言（实测把那条用例顶成 2 条档案即为此）。
    /// </summary>
    [Fact]
    public async Task 点关联应登记宿主档案并让任务拿到真实项目Id()
    {
        var registry = new StrictHostLikeRegistry();
        var ctx = new Context();
        ctx.Register<IProjectRegistry>(registry);
        var todos = new TodoService(new TodoProjectService(ctx), new TaskExecutionService());

        var dir = Path.Combine(RepoRoot, "Plugins", "TodoTracker", "Services");   // 真实存在的一个目录

        // 先断建单入口：随手写的路径不登记宿主档案（但根要归一存下）
        var casual = await todos.CreateTodoAsync(new CreateTodoRequest
        {
            Title = "建单随手写路径", ProjectPath = ToMsysForm(dir)
        });
        casual.ProjectId.Should().Be(0, "建单入口不登记宿主档案（登记是显式「关联」动作）");
        casual.ProjectRoot.Should().Be(Path.GetFullPath(dir), "根仍要归一存下");
        registry.Count.Should().Be(0, "建单不该往宿主项目清单里塞档案");

        // 显式关联：必须登记并拿到真实 id
        var created = await todos.CreateTodoAsync(new CreateTodoRequest { Title = "关联要落真实项目Id" });
        var linked = await todos.LinkProjectAsync(created.Id, new LinkProjectRequest { Path = ToMsysForm(dir) });

        linked.Ok.Should().BeTrue(linked.Error ?? "显式关联应成功");
        linked.Data!.ProjectId.Should().BeGreaterThan(0,
            "关联必须登记宿主档案并拿到真实 id；0 会让详情面板显示「未关联项目」");
        linked.Data.ProjectRoot.Should().Be(Path.GetFullPath(dir));
        linked.Data.ProjectName.Should().Be("Services", "档案名要回得来，界面才显示得出项目而不只是一串路径");
        registry.Count.Should().Be(1, "第一次关联该目录 ⇒ 宿主只多一条档案");

        // 同一目录换写法再关联一次：必须命中同一条档案，不新增
        var again = await todos.LinkProjectAsync(created.Id, new LinkProjectRequest { Path = dir });
        again.Ok.Should().BeTrue(again.Error ?? "换写法再关联应成功");
        again.Data!.ProjectId.Should().Be(linked.Data.ProjectId, "一致的路径必须是同一个项目");
        registry.Count.Should().Be(1, "同一目录换写法不得再登记一条档案");
    }

    [Fact]
    public async Task 正文写入一万两千字符应原样读回不被裁短()
    {
        var content = new string('内', 12_000);
        var created = await _todos.CreateTodoAsync(new CreateTodoRequest
        {
            Title = "长正文不裁短",
            ProjectPath = RepoRoot,
            Content = content
        });

        var read = await _todos.GetTodoByIdAsync(created.Id);

        read.Should().NotBeNull();
        read!.Content.Length.Should().Be(12_000,
            "工件组装的正文可以远超旧 Remark 的量级；一旦被静默裁短，下发的内容就是残缺的而界面毫无提示");
        read.ProjectRoot.Should().Be(RepoRoot.Replace('/', '\\').TrimEnd('\\'));
    }

    [Fact]
    public async Task 真实工件目录应能组装进正文并回填来源()
    {
        var created = await _todos.CreateTodoAsync(new CreateTodoRequest
        {
            Title = "导入本任务自己的工件",
            ProjectPath = RepoRoot
        });

        var result = await _artifacts.ImportAsync(created.Id, new ImportArtifactsRequest
        {
            Dir = "2026-10-07-todo-agent-dispatch",
            Files = ["01-intent.md", "02-spec.md", "04-task.md"],
            Overwrite = true
        });

        result.Ok.Should().BeTrue(result.Error);
        result.Imported.Should().HaveCount(3);

        var read = await _todos.GetTodoByIdAsync(created.Id);
        read!.Content.Should().Contain("### 01-intent.md");
        read.Content.Should().Contain("### 04-task.md");
        read.Content.Should().Contain("来源工件：2026-10-07-todo-agent-dispatch");
        read.ArtifactRef.Should().Contain("2026-10-07-todo-agent-dispatch");
        read.Acceptance.Should().Contain("- [ ] AC-1", "04-task 里的判据应被提取，且只在原判据为空时填");
    }

    [Theory]
    [InlineData("../Windows")]                 // 目录名穿越
    [InlineData("2026-10-07-todo-agent-dispatch/../sems-selfcontained-mcp-tools")]
    public async Task 目录名越出pilot根应被拒(string dir)
    {
        var created = await _todos.CreateTodoAsync(new CreateTodoRequest { Title = "越界目录", ProjectPath = RepoRoot });

        var result = await _artifacts.ImportAsync(created.Id, new ImportArtifactsRequest { Dir = dir, Files = ["01-intent.md"] });

        result.Ok.Should().BeFalse($"{dir} 不该被接受");
        result.Error.Should().NotBeNullOrWhiteSpace("必须给出可自证成因的原因");
    }

    [Fact]
    public async Task 文件名不合规与非md应被拒_但同目录的合规件确实读得到()
    {
        var created = await _todos.CreateTodoAsync(new CreateTodoRequest { Title = "坏文件名", ProjectPath = RepoRoot });

        foreach (var name in new[] { "notes.txt", "intent.md", "09-secret.md", "../docs/02-spec.md" })
        {
            var result = await _artifacts.ImportAsync(created.Id, new ImportArtifactsRequest
            {
                Dir = "2026-10-07-todo-agent-dispatch",
                Files = [name]
            });
            result.Ok.Should().BeFalse($"{name} 不是 NN-*.md 合规工件");
            result.Error.Should().NotBeNullOrWhiteSpace();
        }

        // 阳性对照：同一个夹具里换成合规文件名就必须成功，否则上面的"全被拒"是空转
        var ok = await _artifacts.ImportAsync(created.Id, new ImportArtifactsRequest
        {
            Dir = "2026-10-07-todo-agent-dispatch",
            Files = ["00-repository-understanding.md"],
            Overwrite = true
        });
        ok.Ok.Should().BeTrue(ok.Error);
    }

    [Fact]
    public async Task 正文已有时导入应409_带overwrite才覆盖()
    {
        var created = await _todos.CreateTodoAsync(new CreateTodoRequest
        {
            Title = "覆盖确认",
            ProjectPath = RepoRoot,
            Content = "人手写的正文，别被我冲掉"
        });

        var request = new ImportArtifactsRequest
        {
            Dir = "2026-10-07-todo-agent-dispatch",
            Files = ["01-intent.md"]
        };

        var conflict = await _artifacts.ImportAsync(created.Id, request);
        conflict.Ok.Should().BeFalse();
        conflict.Conflict.Should().BeTrue("没确认就覆盖用户内容是不可逆的静默破坏");
        (await _todos.GetTodoByIdAsync(created.Id))!.Content.Should().Be("人手写的正文，别被我冲掉");

        request.Overwrite = true;
        var forced = await _artifacts.ImportAsync(created.Id, request);
        forced.Ok.Should().BeTrue(forced.Error);
    }

    [Fact]
    public async Task 未关联项目时导入应点名缺项目()
    {
        var created = await _todos.CreateTodoAsync(new CreateTodoRequest { Title = "没有项目" });

        var result = await _artifacts.ImportAsync(created.Id, new ImportArtifactsRequest
        {
            Dir = "2026-10-07-todo-agent-dispatch",
            Files = ["01-intent.md"]
        });

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("尚未关联项目");
    }

    [Fact]
    public async Task 执行记录应按任务内序号递增且append_only三面同形状()
    {
        var created = await _todos.CreateTodoAsync(new CreateTodoRequest
        {
            Title = "记录序号",
            ProjectPath = RepoRoot,
            Objective = "序号连续",
            Content = "正文",
            Acceptance = "- [ ] 序号 1/2/3",
            Verification = "dotnet test"
        });

        for (var i = 1; i <= 3; i++)
        {
            var appended = await _records.AppendAsync(created.Id, new CreateTaskExecutionRequest
            {
                Actor = "qoder",
                Action = $"第 {i} 步操作",
                Result = $"结果 {i}",
                FilesChangedText = $"Plugins/TodoTracker/File{i}.cs",
                Verification = $"dotnet test #{i} PASS",
                Risks = "无",
                Residuals = "无"
            }, "manual");
            appended.Ok.Should().BeTrue(appended.Error);
        }

        var list = await _records.ListAsync(created.Id, 1, 50);
        // 任务内序号必须连续可回放（Equal(params) 的 because 文案会被当成第 4 个期望值，故不带 because）
        list.Items.Select(e => e.Seq).Should().Equal(1, 2, 3);
        list.Total.Should().Be(3);
        list.Items[0].Action.Should().Be("第 1 步操作");
        list.Items[0].FilesChanged.Should().ContainSingle(f => f.Path.EndsWith("File1.cs"));
        list.Items[0].Actor.Should().Be("qoder", "显式 actor 优先于调用面默认值");

        // 三面同形状：按 key 走 agent 回报面再写一条，序号应接着排到 4
        var byKey = await _todos.GetTodoByKeyAsync(created.TaskKey);
        var fourth = await _records.AppendAsync(byKey!.Id, new CreateTaskExecutionRequest
        {
            Action = "第 4 步（agent 回报面）",
            FilesChanged = [new ChangedFileDto { Path = "a.cs", Change = "M" }, new ChangedFileDto { Path = "a.cs", Change = "D" }]
        }, "rest");
        fourth.Ok.Should().BeTrue(fourth.Error);

        var after = await _records.ListAsync(created.Id, 1, 50);
        after.Items.Select(e => e.Seq).Should().Equal(1, 2, 3, 4);
        after.Items[3].Actor.Should().Be("rest", "缺省按调用面记 actor");
        after.Items[3].FilesChanged.Should().ContainSingle(f => f.Path == "a.cs" && f.Change == "D",
            "同一路径只留最后一次变更");
    }

    [Fact]
    public async Task 缺必填项不应被下发且点名缺哪一项()
    {
        var created = await _todos.CreateTodoAsync(new CreateTodoRequest { Title = "信息不全" });

        var result = await _dispatch.DispatchAsync(created.Id, "qoder", "manual");

        result.Ok.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Error.Should().Contain("objective").And.Contain("acceptance").And.Contain("verification");
        result.Error.Should().Contain("content");
    }

    [Fact]
    public async Task 必填齐备可从草稿一路下发并留下两条流转痕()
    {
        // 走草稿起点：下发时自动补一跳「就绪」，台账里才有"就绪"这一格；直接从就绪起跳只会留一条（那是另一条用例）
        var created = await _todos.CreateTodoAsync(new CreateTodoRequest
        {
            Title = "下发留痕",
            ProjectPath = RepoRoot,
            Objective = "可验证目标",
            Content = "正文内容",
            Acceptance = "- [ ] 判据一",
            Verification = "dotnet test"
        });
        created.Stage.Should().Be("Draft", "新建任务默认是草稿");

        var result = await _dispatch.DispatchAsync(created.Id, "qoder", "manual");

        result.Ok.Should().BeTrue(result.Error);
        result.Data!.Stage.Should().Be("Dispatched");
        result.Data.DispatchedAt.Should().NotBeNull("下发时间是要给 agent 看的");

        var history = await _records.ListAsync(created.Id, 1, 50);
        history.Items.Select(i => i.Action).Should().Equal("状态流转：Draft → Ready", "下发任务");
        result.Data.RecordCount.Should().Be(2);

        var preview = await _dispatch.PreviewAsync(created.Id, "http://localhost:7102");
        preview!.PromptMarkdown.Should().Contain("/api/todos/by-key/" + created.TaskKey + "/records");
        preview.CanDelegate.Should().BeFalse("测试环境没装 agent-hub，接缝缺席时必须如实报不可用，不许显示成能一键跑");
        preview.DelegationError.Should().Contain("agent-hub");
    }

    [Fact]
    public async Task 非法流转应409并列出可达目标()
    {
        var created = await ReadyTask("非法流转");

        // Ready 直接跳 Review 是非法边（下发这一步必须留痕）
        var bad = await _todos.ChangeStageAsync(created.Id, new StageChangeRequest { Stage = "Review" }, "manual");
        bad.Ok.Should().BeFalse();
        bad.StatusCode.Should().Be(409);
        bad.Error.Should().Contain("Dispatched", "拒绝时必须告诉对方这态能去哪，否则人只能试");

        var blocked = await _todos.ChangeStageAsync(created.Id, new StageChangeRequest { Stage = "Cancelled" }, "manual");
        blocked.Ok.Should().BeTrue(blocked.Error);
        (await _todos.GetTodoByIdAsync(created.Id))!.Status.Should().Be("Pending", "取消不等于完成");
    }

    [Fact]
    public async Task 进入阻塞必须给原因()
    {
        var created = await DispatchedTask("阻塞缺原因");

        var noReason = await _todos.ChangeStageAsync(created.Id, new StageChangeRequest { Stage = "Blocked" }, "manual");
        noReason.Ok.Should().BeFalse();
        noReason.Error.Should().Contain("blockReason");

        var withReason = await _todos.ChangeStageAsync(created.Id,
            new StageChangeRequest { Stage = "Blocked", BlockReason = "缺真实 API 密钥" }, "manual");
        withReason.Ok.Should().BeTrue(withReason.Error);
        (await _todos.GetTodoByIdAsync(created.Id))!.Stage.Should().Be("Blocked");
    }

    [Fact]
    public async Task 完成与重开应保持旧端点语义且Status与Stage一致()
    {
        var created = await ReadyTask("完成重开");

        var done = await _todos.CompleteTodoAsync(created.Id);
        done!.Status.Should().Be("Completed");
        done.Stage.Should().Be("Done");
        done.CompletedAt.Should().NotBeNull();

        var reopened = await _todos.ReopenTodoAsync(created.Id);
        reopened!.Status.Should().Be("Pending");
        reopened.Stage.Should().Be("Draft");
        reopened.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task 并发领取不得把同一条任务发给两个agent()
    {
        var a = await DispatchedTask("领取竞争 A");
        var b = await DispatchedTask("领取竞争 B");

        var first = await _dispatch.ClaimNextAsync(null, 0, "agent-1");
        var second = await _dispatch.ClaimNextAsync(null, 0, "agent-2");

        first.Ok.Should().BeTrue(first.Error);
        second.Ok.Should().BeTrue(second.Error);
        first.Data.Should().NotBeNull();

        if (second.Data != null)
        {
            second.Data.Id.Should().NotBe(first.Data!.Id, "两条领取必须落在不同任务上");
        }

        // 领到的那条已经不在 Dispatched，不可能被第三次领到同一条
        var third = await _dispatch.ClaimNextAsync(null, 0, "agent-3");
        if (third.Data != null) third.Data.Id.Should().NotBe(first.Data!.Id);

        a.Should().NotBeNull();
        b.Should().NotBeNull();
    }

    [Fact]
    public async Task 旧三参查询与新建形状保持兼容()
    {
        var created = await _todos.CreateTodoAsync(new CreateTodoRequest { Title = "Home 形状兼容", Remark = "只有三个字段" });

        var page = await _todos.GetTodosAsync("Pending", 1, 20);

        page.Total.Should().BeGreaterThan(0, "RetrieveTotalCount 关掉会让 Home 的计数显示成 0");
        var item = page.Items.First(t => t.Id == created.Id);
        item.Should().Match<TodoDto>(t =>
            t.Title == "Home 形状兼容" &&
            t.Remark == "只有三个字段" &&
            t.Status == "Pending" &&
            t.DueDate == null &&
            t.CreatedAt != default &&
            t.UpdatedAt != default);
        item.TaskKey.Should().NotBeNullOrWhiteSpace("新增键只增不减，旧键语义不变");
        item.Stage.Should().Be("Draft");
    }

    [Fact]
    public async Task 按key读任务应命中且空key不误伤()
    {
        var created = await _todos.CreateTodoAsync(new CreateTodoRequest { Title = "按 key 读" });

        var hit = await _todos.GetTodoByKeyAsync("  " + created.TaskKey.ToUpperInvariant() + " ");
        hit.Should().NotBeNull("外部键大小写与空白不应影响命中");
        hit!.Id.Should().Be(created.Id);

        (await _todos.GetTodoByKeyAsync(null)).Should().BeNull();
        (await _todos.GetTodoByKeyAsync("不存在的东西")).Should().BeNull();
    }

    [Fact]
    public async Task 一键委派在接缝缺席时不应改状态也不应写成功痕()
    {
        var created = await ReadyTask("接缝缺席的委派");
        var before = (await _todos.GetTodoByIdAsync(created.Id))!.Stage;

        var result = await _dispatch.DelegateAsync(created.Id, new DelegateToAgentRequest { PermissionMode = "read-only" }, "manual");

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("agent-hub");
        result.TaskKey.Should().BeEmpty();

        var after = await _todos.GetTodoByIdAsync(created.Id);
        after!.Stage.Should().Be(before, "委派失败不能把任务推到执行中");
        after.AgentTaskKey.Should().BeEmpty();
    }

    [Theory]
    [InlineData("yolo")]
    [InlineData("danger-full-access")]
    [InlineData("dangerously-skip-permissions")]
    public async Task 无限权限模式应在进接缝之前就被拒(string mode)
    {
        var created = await ReadyTask("权限模式黑名单");

        var result = await _dispatch.DelegateAsync(created.Id, new DelegateToAgentRequest { PermissionMode = mode }, "manual");

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("被禁止");
    }

    [Fact]
    public async Task 历史行回填应补齐外部键并把已完成落回Done()
    {
        // 造一条"扩列之前"形状的行：无 TaskKey、Status=Completed、Stage=0
        var legacy = new Todo
        {
            Title = "扩列前的老行",
            Status = TodoStatus.CompletedValue,
            Stage = 0,
            Priority = 1,
            TaskKey = string.Empty,
            PermissionMode = string.Empty,
            CreatedAt = new DateTime(2026, 1, 1),
            UpdatedAt = new DateTime(2026, 2, 1),
            CompletedAt = DateTime.MinValue
        };
        legacy.Insert();

        var fixedCount = await _todos.BackfillLegacyRowsAsync();

        fixedCount.Should().BeGreaterThan(0);
        var read = await _todos.GetTodoByIdAsync(legacy.Id);
        read!.TaskKey.Should().HaveLength(32);
        read.Stage.Should().Be("Done", "Status=Completed 而 Stage=Draft 是同一行自相矛盾");
        read.CompletedAt.Should().Be(new DateTime(2026, 2, 1), "缺失完成时间时用 UpdatedAt 兜底，比 MinValue 贴近事实");

        // 幂等：再跑一次不应再动它
        var second = await _todos.GetTodoByIdAsync(legacy.Id);
        var again = await _todos.BackfillLegacyRowsAsync();
        again.Should().BeGreaterThanOrEqualTo(0);
        (await _todos.GetTodoByIdAsync(legacy.Id))!.TaskKey.Should().Be(second!.TaskKey);
    }

    private static readonly StrictHostLikeRegistry _sharedRegistry = new();

    private async Task<TodoDto> ReadyTask(string title) => await _todos.CreateTodoAsync(new CreateTodoRequest
    {
        Title = title,
        Stage = "Ready",
        ProjectPath = RepoRoot,
        Objective = "可验证目标",
        Content = "正文内容",
        Acceptance = "- [ ] 判据一",
        Verification = "dotnet test"
    });

    private async Task<TodoDto> DispatchedTask(string title)
    {
        var created = await ReadyTask(title);
        var dispatched = await _dispatch.DispatchAsync(created.Id, "qoder", "manual");
        dispatched.Ok.Should().BeTrue(dispatched.Error);
        return dispatched.Data!;
    }

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) return dir.FullName;
            dir = dir.Parent;   // 必须推进指针（plugin-development §B）
        }

        throw new FileNotFoundException("找不到仓库根（ForgeSelf.slnx）");
    }

    /// <summary>把 D:\x\y 翻成 Git-Bash 的 /d/x/y（用真实路径构造，避免测试自说自话）。</summary>
    private static string ToMsysForm(string windowsPath)
    {
        var drive = char.ToLowerInvariant(windowsPath[0]);
        return $"/{drive}/" + windowsPath[2..].Replace('\\', '/').TrimStart('/');
    }

    /// <summary>
    /// 按宿主真实语义实现的假注册表：<c>Path.GetFullPath</c> + 字符串精确匹配（对照
    /// <c>ForgeSelf.Api/Services/HostProjectRegistry.cs:56,71</c>）。
    /// 它<b>不会</b>认大小写与斜杠差异 —— 于是"同一项目"完全靠插件的归一器挣来。
    /// </summary>
    private sealed class StrictHostLikeRegistry : IProjectRegistry
    {
        private readonly List<ProjectInfo> _items = [];
        private int _nextId = 1;

        public int Count => _items.Count;

        public bool Register(string root, out string? error) => Register(root, null, out error);

        public bool Register(string root, string? source, out string? error)
        {
            error = null;
            var full = System.IO.Path.GetFullPath(root.Trim());
            if (!Directory.Exists(full))
            {
                error = $"目录不存在：{full}";
                return false;
            }

            // 与宿主一致：只认字符串完全相等
            if (_items.Any(p => p.Root == full)) return true;

            _items.Add(new ProjectInfo
            {
                Id = _nextId++,
                Root = full,
                Name = new DirectoryInfo(full).Name,
                Source = string.IsNullOrWhiteSpace(source) ? "ai-agent" : source,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                LastActiveAt = DateTime.Now,
                PathExists = true,
                IsGitRepo = Directory.Exists(Path.Combine(full, ".git"))
            });
            return true;
        }

        public List<ProjectInfo> GetAll() => _items.OrderByDescending(p => p.LastActiveAt).ToList();

        public ProjectInfo? Get(int id) => _items.FirstOrDefault(p => p.Id == id);

        public bool Update(int id, ProjectUpdate update)
        {
            var p = Get(id);
            if (p == null) return false;
            if (update.Name != null) p.Name = update.Name;
            return true;
        }

        public bool Remove(int id, out string? error)
        {
            var p = Get(id);
            error = p == null ? "不存在" : null;
            if (p != null) _items.Remove(p);
            return p != null;
        }

        public int AddCommand(int projectId, RunCommandInfo command) => 0;
        public bool UpdateCommand(int commandId, RunCommandUpdate update) => false;
        public bool DeleteCommand(int commandId) => false;
        public List<RunCommandInfo> GetCommands(int projectId) => [];
    }
}
