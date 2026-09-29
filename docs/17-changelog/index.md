# 17-changelog — 变更记录

> 状态：部分（2026-08-12 反向更新；2026-09-29 补 dsh 对齐 B1–B9 八批次收官登记）
> 最后更新：2026-09-29

版本/阶段的变更摘要（git log 的人工提炼，不记代码级 diff）。

## dsh 架构对齐专项（040–042，2026-09 收官）

| 批次 | 核心改动 | 验证 |
|------|----------|------|
| B1 会话事件溯源 | `SessionEvent` 13 子类联合化 + `SessionEventMap` 注册 + 运行时校验 | 契约往返单测全绿 |
| B2 持久化 | `ISessionStore` Sqlite/XCode 持久化（`PersistentSessionStore`）+ 契约测试基类（内存/持久化双实现防漂移） | 契约测试 + 定向集 |
| B3 总线冒泡 | `EventBus` 父子链冒泡（方案 B：子 emit 父收、父 emit 子不收），打通「插件→宿主总线」实证缺口 | Core.Tests 32/32 |
| B4 写路径改序 | `DeriveMessages` 成为唯一 prompt 来源；`ChatMessage` 降为投影（`SessionProjectionService.SyncAsync` 幂等全量重投影）；`SaveMessageAsync` 直写两侧全删 | 不变量测试先红后绿 |
| B5 Turn/Step 运行时 | `IAgent`/`IAgentRegistry`/`TurnFrame` 联合 + `ReactLoopAgent` 状态机（旧 `IAgentLoop`/`InMemoryAgentLoop` 退役） | 事件序门禁先红后绿 |
| B6 收件箱 | `PersistentInbox` + followup/steer/inject 三通道真生效；`SaveMessageAsync` 写路径切除 | 三通道门禁 |
| B7 计划驱动统一 | `AgentRun/AgentStepRun` 升格 turn/step，`RunOrchestratorService` 薄壳化 + FreeLoop 统一 `ReactLoopAgent` | 029 快照测试全绿 |
| B8 工具管线 | 六闸门（pre-execute 三态 → 单调守卫 → execute waterfall → post-execute waterfall → finalize 恰好一次 → result 冻结快照）+ `ExecuteBatchAsync` model-ordered commit + `StreamChunk` 带 ToolCall/Usage/FinishReason | 12 条门禁先红后绿；定向集 161/161 |
| B9 退役收官 | 旧执行面退役（`ToolCallContext` 删 + grep 守门）、前端 FIFO 配对、plan:{runId} 展示、`FORGESELF_DATA_ROOT` 测试基建根治、ConfigUnifier 降级健壮化、spill 32KiB 进配置中心 | 退役定向集 109/109 零行为差异；收官全量 1676 条（1662 绿/失败面剩个位数基线族） |

| 时间 | 变更 | 影响 |
|------|------|------|
| 2026-08-12 | docs 体系按代码反推补全（19 目录从 3 有正文→全落文档） | 文档追上未提交代码（ChatSession/ChatTurn 重构、多模态缓存、端口校正 7102/7002） |
| 2026-08-11 | 聊天记录会话化重构落地（ChatRecord→ChatTurn + ChatSession 聚合） | 代理录制与 app 聊天按会话聚合；旧 607 行不迁移 |
| 2026-08-11 | 多模态图片识别本地缓存（IImageRecognitionCache） | 同会话同图复用识别结果，token 骤降 |
| 2026-08-10 | 修复流式响应缺失 usage 字段 | 网关 + provider 两端 DTO 补 usage，流式透传 |
| 2026-08-09 | 硬编码 hex → 官方 `--el-*`（5 文件） | 去绿框、统一设计 token |
| 2026-08-07 | 聊天记录详情渲染 + WS 推送（去轮询） | 实时推送 + JSON 树 + 展开/收起 |
| 2026-08-06 | 文档体系按认知模型重构（分目录 + 模板） | 建立 docs/19 目录骨架 |

> 详情见对应 git commit / `02-features/` 各功能文档。
