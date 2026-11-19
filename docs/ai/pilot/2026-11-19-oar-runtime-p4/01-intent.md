# Intent

> Task ID：`2026-11-19-oar-runtime-p4`

## Why

P3 已让兄弟插件能 `Get<IAgentRuntime>()`，但缺可回归的消费者试点与 Node≥24 运维说明；不补则「能 Get」只是推断，打包/CI 仍踩 Node 22。

## What

T-ConsumerQA：中档过滤测试覆盖 Get + Cli fallback；文档写回 Node≥24；可选 SessionKey≠OarSessionId 守卫；勾选 P0–P4。

## Done when

- Consumer pilot 测绿（Get 同实例 / 缺席降级 / Cli Submit / DTO 无 OarSessionId）
- 架构 §13 + RuntimeWorker README 含 Node≥24 运维；032/concurrency/seams 勾 P4 ✅
- 过滤测试 + Worker smoke 绿；commit trailer `Pilot: 2026-11-19-oar-runtime-p4`

## Out of scope

不自动 install；不停宿主；不重写 Watch SSE；不发明 P5；OAR 类型不进 Abstractionseds。
