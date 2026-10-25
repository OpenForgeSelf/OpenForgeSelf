# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 是。真实下发在隔离实例上跑到 `Run.status=3`（Completed）、两步 `outputJson` 非空、待办 `stage=Done`；无可用模型时下发返回明确 400 文案，不再静默换模型。
2. **实现是否符合 Spec？** 是。FR-1~FR-4 全部落地：有序解析链（`ChatModelResolver`）、解析落日志、`ResumeAsync` 走同一链、空响应判 `TurnFailed`；未新增 `DelegateToAgentRequest.ChatModelId` 字段、未新增数据库列。
3. **是否超出了 Scope？** 有限度超出的两处已按「问题类闭环」纳入并记录：① 三处 `ActorHint()`/基址拼接空安全（R2 同类扫查命中，属同根因缺陷）；② `PluginManager.HostProvidedServiceContracts` 补 `ITurnTelemetryQuery`（解析链需要，属契约共享表缺失）。其余旁支缺陷（`ToolChoice` 未接线、`tokensUsed=0`、`toolCallsJson` 不落库）未动，已入 TODO。
4. **是否修改了不应该修改的文件？** 未碰宿主业务逻辑（`ForgeSelf.Api/**` 仅 `PluginManager` 一行契约登记）；未碰用户运行实例（`:51888`）数据与进程。
5. **测试是否覆盖 Acceptance Criteria？** 覆盖。AC1 由 `ChatModelResolverTests`（4 条优先级 + 未命中）；AC2 由 `ReactLoopAgentTests.EmptyUpstreamResponse_FailsInsteadOfCompleting`；AC4/AC5 由隔离实例真实下发与停用模型两条现场验收；AC3 构建与过滤集见 05。
6. **是否存在明显回归风险？** 低。解析链只在 builtin/resume 入口生效，不改 `AIAgentService.ResolveModelId/ResolveProvider`（主聊天语义不变）；空响应守卫只收紧「无正文 + 无工具调用 + 0 completion token + 非 length」，不改变有内容/有调用的正常收束。
7. **是否存在架构不一致？** 否。解析链通过 `IContext` 懒取宿主契约（PC-001/PC-005 语义），契约经 `HostProvidedServiceContracts` 进共享表；未新增插件间直接引用。
8. **Evidence 是否足以证明任务完成？** 是（`05-evidence.md` 全部 Verified，含日志、接口响应、台账、截图）。

## Requirement Check

PASS —— `mini-task.md` 的 6 条 Acceptance Criteria 逐条有对应证据（AC6 由本文件与 07 补齐）。

## Scope Check

PASS —— 变更文件与 `mini-task.md`「Files To Change」基本一致；超出项（R2 空安全、契约登记）均为同根因/必要前置，已在本文件 Findings 披露。

## Test Check

PASS —— 构建 0 错误；定向过滤集 37/38（唯一红为既有 `ToolChoice` 基线）；TodoTracker 域过滤集 165/165；全量中档门禁结果与基线红比对见 `05-evidence.md`「Full Suite (medium gate)」；TodoTracker 冒烟 e2e 与截图读图见「E2E」。

## Architecture Check

PASS —— 解析链位于插件内部服务，经宿主契约获取模型目录；未绕过 `IContext` 直接 new 宿主服务（对照 PC-005）。

## Risk

L1 —— 低风险：新增服务与守卫均已单测覆盖；真实链路已在隔离实例跑通。残余风险是「宿主默认模型不在启用目录时改用目录首个启用模型」可能选中视觉模型（`qwen3_8_vision`），依赖上游把文字请求转向可用对话模型（本次实测成功）；若上游不转向，会在空响应守卫处明确失败而非静默卡住。

## Findings

### Critical

无。

### Major

无。

### Minor

1. `ChatModelResolver` 的「目录首个启用模型」不区分模态（视觉/嵌入模型也可能被选中）；当前依赖上游转向，后续可加模态偏好过滤（已入 TODO）。
2. 全量门禁中 `AIServiceTests` / `MultimodalProcessorTests` 因测试进程未初始化 `AIModel` 表而红（顺序相关，非本批改动），已记 TODO 待加夹具。
3. `ToolChoice` 未接线导致 2 条既存红，本批未处理（与空响应守卫无关，已核实无 ToolChoice 赋值点）。

## Final Decision

APPROVED

<!-- CHANGES_REQUIRED → 回退阶段与理由；BLOCKED → 阻塞原因与升级对象 -->
