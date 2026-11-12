# Intent

> 阶段：Stage 1｜Task ID：PILOT-079（2026-11-03-im-gateway-task-dispatch）｜日期：2026-11-03

## Problem

- IM 网关今天只有「聊天快答」一条链路（`IChatCompletion.CompleteAsync` → 通道回发，`ImGatewayRouter.cs:74-113`）：消息进来只能换回一次对话回复，**长任务无法从 IM 派发**——改代码、跑验证、整理资料这类活儿在 IM 里没法交出去。
- 任务中心（B3，commit `2c6b30c`，2026-11-02）只做了**只读总览**（三入口聚合投影，02-spec §2 NOT-to-do 明确不做任务创建），**没有外部消息入口**：人在 IM 里时，任务进不去任务中心，结果也回不到 IM。
- 用户原话（输入1）：收到消息 → 调用创建任务/查询任务结果等工具挂上去 → 直接委派任务 → 指派指定的 Agent → 收到任务自动去干活 → 干完交由 IM 网关回复。

## Why

- 架构真源 `docs/01-architecture/maf-centric-architecture.md` §2：**「Chat 只是入口之一；驱动单元是 Task/Run」**——IM 消息派任务就是给 MAF Run 新增第四个入口，方向与真源一致。
- 全部依赖已现成：`RunOrchestratorService`（建 Run→规划→逐步执行→终局合成）、`RunRequest.AgentId`（指派字段已存在）、`AgentRegistryService`（Agent 名册）、任务中心 planned 桶自动投影 AgentRun、`IImChannel.SendAsync` 支持主动推送（`WeComChannel.cs:154-178`）。**属低风险拼装，不新造任何执行语义。**
- 收益：IM 成为最顺手任务入口；任务全程在任务中心可见/可重试；结果自动回流 IM。

## Expected Outcome

企微里发「/task <任务描述> [@某Agent]」→ 立即收到回执 → 任务中心计划任务桶出现该 Run（已指派 Agent）→ Agent 后台自动执行 → 终态后 IM 收到结果/失败原因的主动回复；「任务状态」指令可查进度；普通聊天消息行为不变（功能默认关闭，设置面板可开）。

## Constraints

- 不修改宿主 DB 结构（新表仅插件自有库，走 XCode 实体自动建表既有模式，无手工迁移）。
- 不修改鉴权/权限核心；新管理面只读端点（若有）继续 `ApiKeyPolicy`。
- 契约进 `ForgeSelf.Abstractions`；插件间禁 HTTP 自调/共享文件/静态类；`ctx.Get` 每次取禁缓存（architecture-design 铁律 2/3/4）。
- 不另起 Runtime：复用 `IRunOrchestratorService` 同进程驱动（`ScheduledAgentRunTrigger` 先例）；写操作仍走现有审批，不关闭审批。
- 功能默认关闭（Enabled=false 零行为变化）；发布动作须用户同意；不停/杀运行中宿主（:51888）。

## Success Criteria

1. 单测矩阵全绿：契约校验、AgentRef 三态解析、派发服务（即回 taskKey/终态快照/同会话并发拒绝/RunId 回退查询）、网关（前缀命中/绑定落库/watcher 回复幂等/重启恢复）。
2. 集成实测（dev-stack）：Submit 后 `tasks-overview` 出现该 Run（sessionId 前缀 `imtask:`），终态快照 ResultText 非空。
3. e2e 冒烟 1 条主链路绿（真实宿主零 mock）；Enabled=false 时既有 im-gateway e2e 全绿（回归零破坏）。
4. 中档全量 `dotnet test` 对表基线 0 归本批（新契约=公共接口变更，按 §5.6 升中档）。
5. 工件 00-07 齐 + pre-commit 门禁绿；发布与运行实例复验按用户节奏另批。
