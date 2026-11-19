# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，不提前锁死实现细节代码。  
> Task ID：`2026-11-19-oar-agenthub-runtime`｜日期：2026-11-19

## Problem

1. 外部 Coding Agent（Claude Code / Codex / OpenCode 等）的启动、会话、事件、控制方式各异，AgentHub 目前只有 **CLI one-shot Transport**，难以统一长会话、能力探测与控制结果。
2. 跨插件只能经 **`IAgentDelegation`（入队 + 读回）** 用外部 Agent；缺少 Session 续接、事件订阅、明确控制拒绝等「Runtime」级能力。
3. Cordis 规划中的 **`IAgentRuntime` 接缝尚未落地**；若各插件自行适配 CLI/OAR，会重复造轮并泄漏运行时细节。
4. 调研稿将已删除的 `IAgentLoop` 与 `IAgentRuntime` 对举，易与 MAF/`IAgent` 混淆，需要与 ADR-005 划清边界后才能开工。

## Why

- OAR 提供统一 Runtime 生命周期与能力声明，适合作为 **AgentHub 内部适配层**，而非整仓基础依赖。
- 铸己匣愿景要求「个人可扩展 Agent 平台」：WorkflowEngine / SEMS / 未来插件应经 **同一 L1 契约** 使用外部 Agent。
- 不推翻现有 Registry / Delegation / Permission / 事件基础，升级为 Runtime Provider，风险低于重写。

## Expected Outcome

本批（文档/决策）完成后应得到：

1. **最终架构决策**已写入 ADR + 架构真源，可被后续 Implement 直接引用。
2. **与仓库事实对齐**的改造边界：OAR 落点、Worker 形态、Transport 选路、SessionKey、事件双层、公共 `IAgentRuntime`、与 `IAgentDelegation`/`IAgent`/ADR-005 关系。
3. **分阶段实施计划 P0–P4** 与「第一期必做 / 明确缓做」清单。
4. **闸门1 六段方案**落盘，待用户批准后才允许改业务代码（Abstractions / AgentHub / Worker）。

## Constraints

- 遵守 AI-Native 三道闸门；本批 **禁止** 改 `Plugins/AgentHub` 业务实现与 `ForgeSelf.Abstractions` 契约源码（仅文档允许写入计划中的路径）。
- 不违反 ADR-005：不另起本机 LocalBot/平行 MAF Runtime；OAR 仅服务 **外部** Coding Agent。
- 不把 `@botiverse/oar` 类型泄漏到 Abstractions 或其他插件。
- 数据层继续 NewLife.XCode；不引入 EF / 第二套插件互通机制。
- 安全约束（cwd 白名单、权限上限、不自动安装 Agent、密钥不入事件）不得因引入 OAR 放松。
- OAR npm 版本精确锁定（验证基线 `0.109.0`）；不使用 `^` 浮动范围。

## Success Criteria

| # | 可验证条件 |
| --- | --- |
| S1 | 存在 `docs/07-decisions/007-oar-agenthub-runtime.md`（Accepted 草案，待闸门1 确认后生效） |
| S2 | 存在 `docs/01-architecture/agenthub-oar-runtime.md`，含目标架构、接缝边界、阶段划分 |
| S3 | 存在 `docs/06-research/007-oar-agenthub-runtime.md`，含与仓库差距及对调研稿纠偏 |
| S4 | `docs/README.md` §一 有「外部 Agent Runtime / OAR / IAgentRuntime」真源行 |
| S5 | `cordis-kernel.md` / `host-capability-seams.md` / `032-agent-hub.md` 已交叉引用本决策 |
| S6 | pilot `00`–`04` + `gate1-proposal.md` 齐备；缓做项入 `not-taken-decisions.md` |
| S7 | 回复给出路径 + 待拍板项；**未**提交 AgentHub/Abstractions 业务代码变更 |
