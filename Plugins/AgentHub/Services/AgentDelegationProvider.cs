using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using NewLife;
using NewLife.Log;
// Abstractions 里另有同名 IAgentRegistry（Agent 运行时注册表契约），本文件用的是本插件的注册中心，显式别名消歧。
using IAgentRegistry = ForgeSelf.Api.Plugins.AgentHub.Services.IAgentRegistry;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// <see cref="IAgentDelegation"/> 的提供方实现（PILOT-054 新增，供兄弟插件经 <c>ctx.Get</c> 消费）。
///
/// 定位：<b>纯适配层</b>。它把 <see cref="DelegationRuntime"/> 既有的「入队 + 后台执行 + 按 key 读回」
/// 包成跨边界契约，<b>不新增任何执行语义</b>——状态机、cwd 白名单（G7）、权限审批、并发互斥（G6）
/// 全部仍由 AgentHub 单方持有，避免同一事实两处实现。
///
/// 依赖解析姿势：<b>每次调用都从宿主 DI 取服务</b>（与 <c>Tools/AgentHubToolBase.GetService&lt;T&gt;()</c> 同款，统一走
/// <see cref="AgentHubDi.ResolveHost{T}"/>）。不在构造期固化 <see cref="DelegationRuntime"/>/<see cref="IAgentRegistry"/> 实例：
/// 插件 <c>IContext</c> 的 <c>GetService</c> 只解析「本地值 + 全局共享表」（<c>ForgeSelf.Core.Context</c>），
/// 不含宿主容器；必须经 <c>ctx.Get&lt;IServiceProvider&gt;()</c>（宿主 ProvideHostServices 已 seed）回落宿主根 provider
/// 再解析，才能与控制器共用同一份单例。若在 Apply 里 <c>services.BuildServiceProvider()</c> 另起一个容器，
/// 会造出**第二份** <c>PermissionBroker</c> 等单例，于是「接缝发起的任务」与「控制器审批面板」看到的待审批队列
/// 不是同一份（G2 人在回路直接失灵）。
///
/// 失败口径：契约约定「业务失败返回 Success=false + 原因原文，不抛业务异常」，
/// 判据与控制器 <c>AgentHubTasksController.Create</c> 一致（<c>Id&lt;=0</c> 或 <c>Status==Failed</c> 即前置失败，此时任务未落库）。
/// </summary>
public class AgentDelegationProvider : IAgentDelegation
{
    /// <summary>本接缝的默认发起方标识（进入 AgentHub 审计与统计）。</summary>
    public const String DefaultCreatedBy = "agent-delegation";

    private readonly IServiceProvider? _services;

    /// <summary>构造。传入插件容器（Apply(IContext) 收到的那个 provider，与工具基类同源）。</summary>
    public AgentDelegationProvider(IServiceProvider? services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public async Task<AgentDelegationOutcome> SubmitAsync(AgentDelegationRequest request, CancellationToken ct = default)
    {
        if (request == null) return AgentDelegationOutcome.Fail("委派请求不能为空");
        if (request.Prompt.IsNullOrWhiteSpace()) return AgentDelegationOutcome.Fail("提示词不能为空");

        var runtime = AgentHubDi.ResolveHost<DelegationRuntime>(_services);
        if (runtime == null) return AgentDelegationOutcome.Fail("AgentHub 运行时不可用（插件未正确加载）");

        // 权限模式在进运行时之前先拒（与 AgentPolicy 黑名单同口径；运行时内部还会再校验一次，双层不互相替代）
        if (AgentPolicy.IsForbiddenMode(request.PermissionMode))
            return AgentDelegationOutcome.Fail($"权限模式「{request.PermissionMode}」被禁止（不得使用无限权限模式）");

        var inner = new DelegationRequest
        {
            Prompt = request.Prompt,
            AgentId = request.AgentId is > 0 ? request.AgentId : null,
            Cwd = request.Cwd.IsNullOrEmpty() ? null : request.Cwd,
            PermissionMode = request.PermissionMode.IsNullOrEmpty() ? null : request.PermissionMode,
            CreatedBy = request.CreatedBy.IsNullOrEmpty() ? DefaultCreatedBy : request.CreatedBy
        };

        var dto = await runtime.CreateAsync(inner);
        if (dto == null) return AgentDelegationOutcome.Fail("委派运行时返回空结果");
        if (dto.Id <= 0 || dto.Status == TaskStatus.Failed)
            return AgentDelegationOutcome.Fail(dto.Message ?? "委派任务创建失败");

        // 入队后立即后台执行（与控制器同构，不阻塞调用方）；本接缝语义 = 入队即返回。
        var taskId = dto.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                await runtime.RunAsync(taskId);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[AgentHub] 接缝后台执行任务 {0} 异常: {1}", taskId, ex.Message);
            }
        }, CancellationToken.None);

        return AgentDelegationOutcome.Ok(dto.TaskKey, dto.AgentId, dto.AgentName, dto.Status);
    }

    /// <inheritdoc />
    public Task<AgentDelegationSnapshot?> FindAsync(string taskKey, CancellationToken ct = default)
    {
        if (taskKey.IsNullOrEmpty()) return Task.FromResult<AgentDelegationSnapshot?>(null);

        var runtime = AgentHubDi.ResolveHost<DelegationRuntime>(_services);
        var dto = runtime?.GetByKey(taskKey);
        return Task.FromResult(dto == null ? null : ToSnapshot(dto));
    }

    /// <inheritdoc />
    public Task<bool> MarkCompletedAsync(string taskKey, CancellationToken ct = default)
    {
        if (taskKey.IsNullOrEmpty()) return Task.FromResult(false);

        var runtime = AgentHubDi.ResolveHost<DelegationRuntime>(_services);
        return Task.FromResult(runtime?.CompleteByKey(taskKey) ?? false);
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentDelegationAgent> ListAvailableAgents()
    {
        try
        {
            var registry = AgentHubDi.ResolveHost<IAgentRegistry>(_services);
            if (registry == null) return [];

            return registry.List(true)
                .Select(a => new AgentDelegationAgent
                {
                    Id = a.Id,
                    Name = a.Name ?? String.Empty,
                    Vendor = a.Vendor ?? String.Empty,
                    DefaultCwd = a.DefaultCwd
                })
                .ToList();
        }
        catch (Exception ex)
        {
            // 自检类只读能力：不可用时返回空集合，让消费方走「无候选」分支，绝不把异常抛给调用面
            XTrace.Log.Warn("[AgentHub] 列出可用 agent 失败: {0}", ex.Message);
            return [];
        }
    }

    private static AgentDelegationSnapshot ToSnapshot(DelegationTaskDto dto) => new()
    {
        TaskKey = dto.TaskKey,
        Status = dto.Status,
        Terminal = TaskStatus.IsTerminal(dto.Status),
        // 未开始/未结束时 ExitCode 仍是列默认 0；只在终态才把它当读数（0≠成功，判定以 Status 为准）
        ExitCode = TaskStatus.IsTerminal(dto.Status) ? dto.ExitCode : null,
        ErrorCode = dto.ErrorCode.IsNullOrEmpty() ? null : dto.ErrorCode,
        ElapsedMs = dto.ElapsedMs,
        ResultText = dto.ResultText.IsNullOrEmpty() ? null : dto.ResultText,
        Cwd = dto.Cwd.IsNullOrEmpty() ? null : dto.Cwd,
        Artifacts = ParseArtifacts(dto.ArtifactsJson)
    };

    /// <summary>
    /// 解析本插件自产的 <c>ArtifactsJson</c>（数组，元素形如 <c>{type,tool,text}</c>，
    /// 见 <c>DelegationRuntime</c> 的产物收集段）。内部格式不外泄：契约交出的是结构化列表。
    /// 解析失败一律返回空列表并告警，不得让整个状态回读挂掉。
    /// </summary>
    private static IReadOnlyList<AgentDelegationArtifact> ParseArtifacts(String? json)
    {
        if (json.IsNullOrEmpty()) return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

            var list = new List<AgentDelegationArtifact>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                list.Add(new AgentDelegationArtifact
                {
                    Type = GetString(el, "type"),
                    Tool = NullIfEmpty(GetString(el, "tool")),
                    Text = NullIfEmpty(GetString(el, "text"))
                });
            }
            return list;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AgentHub] 解析 ArtifactsJson 失败: {0}", ex.Message);
            return [];
        }
    }

    private static String GetString(JsonElement el, String name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? (v.GetString() ?? String.Empty)
            : String.Empty;

    private static String? NullIfEmpty(String value) => value.IsNullOrEmpty() ? null : value;
}
