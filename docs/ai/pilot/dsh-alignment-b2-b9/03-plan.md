# 施工总览：OpenForgeSelf 向 dsh 架构对齐

> 功能编号：dsh 架构对齐专项（040–042）· 施工总览
> 状态：**八批次（B1–B9）全部收官**（2026-09-28，B9 退役与清理批完成 = 全专项闭环）
> 最后更新：2026-09-28
> 关联：总纲 [`02-spec.md`](02-spec.md)；详细设计（原 `specs/040|041|042` 三份详细设计稿已被 `.gitignore` 忽略、不入库，要点摘要与本专项全部上下文见 [`02-spec.md`](02-spec.md)）；对标调研 [`../../../06-research/003-deepseek-harness-运行全链路.md`](../../../06-research/003-deepseek-harness-运行全链路.md)

> 适用前提（用户拍板，2026-09-27）：**不考虑现有实现的历史包袱；项目起步期，直接按正确方案落地。**
> 编号沿用 specs 惯例；040/041/042 为本专项新增。027（Cordis 内核）保留，其中"待办 5 事件总线跨上下文"并入 040-B3。

---

## 1. 一条主线

```
一切进模型的东西，必须先落日志，再从日志派生出来。
（Model-visible means logged，附运行时断言与契约测试双重强制）
```

## 2. 三阶段 × 九批次

| 阶段 | spec | 批次 | 内容 | 前置 |
|---|---|---|---|---|
| 一 会话事件溯源 | 040 | B1 | `SessionEvent` 联合化 + `SessionEventMap` + 运行时校验 | — |
|  |  | B2 | `ISessionStore` 持久化（Sqlite/XCode）+ 契约测试基类 | B1 |
|  |  | B3 | `EventBus` 父子冒泡（027 待办 5 方案 B） | 可与 B1 并行 |
|  |  | B4 | 写路径改序：`DeriveMessages` 成为唯一 prompt 来源；`ChatMessage` 降为投影 | B1+B2 |
| 二 Turn/Step 运行时 | 041 | B5 | `IAgent`/`IAgentRegistry`/`TurnFrame` 联合 + `ReactLoopAgent` 状态机 | 040 全量 |
|  |  | B6 | `IInbox` 持久化 + `followup`/`steer`/`inject` 三通道真生效 | B5 |
|  |  | B7 | `AgentRun/AgentStepRun` 升格为 turn/step，FreeLoop 与计划驱动统一 | B5 |
| 三 工具管线 | 042 | B8 | 三态决策 + 单调守卫 + waterfall 修正 + model-ordered commit + skipped + spill + usage | 041-B5 |
|  |  | B9 | 旧执行面退役（旧两方法/`ToolCallContext` 删 + 3 调用方迁移 + grep 守门）+ 前端 FIFO 配对 + `plan:{runId}` 展示 + `ConfigUnifier` 降级健壮化 + spill 32KiB 进配置中心 + 测试基建根治（`FORGESELF_DATA_ROOT` 双解析重载，方案 A 已批准落地）+ 文档同步 | B8 |

**依赖图**

```
B1 ──┐
B2 ◄─┼── B4 ──┐
B3 ──┘        │
              ├──► B5 ──► B6
              │        └─► B7
              └──► B8（仅需 B5 的 loop 骨架即可开工，B8 与 B6/B7 可并行）──► B9（退役清理收官）
```

## 3. 统一事件命名规范（040/041/042 共用）

- **持久事件**（落日志，可重建）：`<域>/<动作>`，全部注册进 `SessionEventMap`；
- **live 事件**（跨插件拦截/观察，不落盘）：`agent/*`、`tools/*`、`llm/stream`；
- **能力事件**（策略与适配器）：`fs/*`、`telemetry/*`（本期不涉，预留）。

## 4. 提交纪律

- 每个批次 = 一次提交；提交说明按批次表逐条列文件；
- 每批次必须带**自己的门禁测试**（详见各设计稿"门禁"节），不得后置；
- 跨批次公共类型（事件 record、`TurnFrame`、`PreToolDecision`）只在所属批次定义，下游批次只引用不重复定义；
- 全程 `dotnet test` 基线不回退：Backend 988/988 + Core 12/12 + Abstractions 13/13（以当前 main 为准，实施时以本地最新基线为准）。

## 5. 风险总表

| # | 风险 | 触发批次 | 缓解 |
|---|---|---|---|
| 1 | B4 动真实写路径，可能短时破坏聊天 | B4 | 先落"不变量测试"再改序；真机走查 spec 036 流程 |
| 2 | `ReactLoopAgent` 状态机实现量大 | B5 | 先跑通 `turn→step→tool→step→turn` 最短路径，再补取消/重试 |
| 3 | 029 计划驱动模式回归 | B7 | 升格前先补 `RunOrchestratorService` 当前行为快照测试 |
| 4 | 持久化后 `Replay` 性能 | B2 | `SessionId` 建索引；`DeriveMessages` 只读不重投影 |
| 5 | waterfall 修正改变既有 `tools/*` 语义 | B8 | 回归 `ToolRegistryTests`；`Ask` 无审批服务时 fail-closed |

## 6. 文件索引

- 040 · 会话事件溯源 详细设计：原 `specs/040-session-event-sourcing/` 已被 `.gitignore` 忽略、不入库；要点摘要见 [`02-spec.md`](02-spec.md)
- 041 · Turn/Step 运行时 详细设计：原 `specs/041-turn-step-runtime/` 已被 `.gitignore` 忽略、不入库；要点摘要见 [`02-spec.md`](02-spec.md)
- 042 · 工具管线 详细设计：原 `specs/042-tool-pipeline/` 已被 `.gitignore` 忽略、不入库；要点摘要见 [`02-spec.md`](02-spec.md)
