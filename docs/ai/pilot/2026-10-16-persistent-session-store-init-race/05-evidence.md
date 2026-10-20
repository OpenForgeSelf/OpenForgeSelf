# Evidence

## Task
mini-task：修复 PersistentSessionStore 首次持久化写事件时数据库初始化竞态（`SessionEvent 数据库初始化失败: database is locked` → 后续写报 `no such table`）。来源：输入2（2026-10-16，AIAgent 冒烟 e2e 首条 SSE 事件 `type=error`）。轻量通道（mini-task + 05/06/07）。

> 本文件为 **2026-10-20 会话归档收口时补产出**：批次实现早在 2026-10-16 完成并随 2.4.x→2.5.x 发布链多次出包，但 05/06/07 一直未落（当时被 ModelProxy.cs 编译阻断连带暂停提交，之后遗忘）。证据分级如实标注「今日复跑 Verified」与「历史记录 Documented」两态。

## Changed Files
- `ForgeSelf.Api/Services/PersistentSessionStore.cs`（唯一业务文件）：`EnsureReady` 加 `_initializeGate` 锁 + 最多 6 次递增退避重试 `SessionEventEntity.EnsureCreated`；失败不再误标 `_initialized`（显式抛错），成功才置 1。
- 本 pilot 目录工件（本次补齐）。

## Build
- **Verified（2026-10-20）**：`dotnet build ForgeSelf.slnx -c Release` → exit 0，0 error（日志 `.forgeself/tmp-archive-build-20261020.log`）。

## Unit Test
- **Verified（2026-10-20）**：`dotnet test ForgeSelf.Api.Tests -c Release --filter "FullyQualifiedName~PersistentSessionStore|FullyQualifiedName~AIModelFeature"` → **38/38 通过**（含 `PersistentSessionStoreTests` + `PersistentSessionStoreAdversarialTests` 竞态对抗用例；日志 `.forgeself/tmp-archive-test-domain2-20261020.log`）。

## Integration Test
- 无新增集成测试；建表重试路径由 Adversarial 单测覆盖（来源等级 Verified，同上）。

## E2E
- **Documented（2026-10-16 日记 输入2续/输入4）**：AIAgent 单条流式 e2e PASS（修复后首条事件不再 `type=error`）。2026-10-20 收口未重跑 e2e（本批文件其后未被再改动，发布链 2.4.5~2.5.x 均含此修复且多轮 e2e 通过）。

## Static Analysis
- 无独立分析项（单文件竞态修复，无公共契约变化）。

## Screenshots
- 不适用（无 UI）。

## Known Limitations
- `EnsureCreated` 返回 false 的语义依赖 XCode「表已存在」判定；跨进程锁库（另一宿主实例占用）超出本修复范围。

## Unresolved Issues
- 无。AC-1（首用初始化重试语义）Verified；AC-2（e2e 无 type=error）Documented；AC-3（全链路）由发布链多批次回归覆盖，**部分 Verified**——如实记录：收口日只复跑了本域单测与全解决方案构建。
