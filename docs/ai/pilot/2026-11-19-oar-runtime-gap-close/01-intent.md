# Intent

> Task ID：`2026-11-19-oar-runtime-gap-close`

## Why

P0–P4 计划内代码已落地，但审计判定 PARTIAL：架构 §7.2 / §9 与设计 AC-R4 仍缺耐久事件双层与管理面端点；Session 主链路无入库 e2e。用户指令「把应该做却没做的补上」。

## What

1. 落地最小耐久双层事件：`OarSequence` 水位 + `AgentRuntimeRecord` 表 + 单调 `AgentHubSequence` 写入路径。  
2. 补齐架构 §9 / 本批 Required API：`probe` / `models` / `abort` / `records` / `stream`；类级 ApiKeyPolicy。  
3. 一条 AgentHub Session 主链路插件 e2e 冒烟。  
4. 回写 design AC-R*、P0–P4 勾选滞后、032 / 架构进度、审计 md。

## Done when

- 过滤单测覆盖序列单调 + Record 落库 + 新 API 行为（无真实 vendor）。  
- AgentHub web build 绿；1 条 Session e2e 冒烟绿（或证据证明环境阻塞并已记风险）。  
- 032 / `agenthub-oar-runtime.md` §7.2·§9·阶段表 / design AC 勾选 / audit 刷新。  
- commits 带 `Pilot: 2026-11-19-oar-runtime-gap-close`；推 OAR 分支。

## Out of scope

Skills/MCP/Subagents；真实 Claude/Codex/OpenCode 安装登录；全量 Watch SSE 重写；P5 产品功能；PR#7 WindowStyle；停/启 :51888。

## 闸门1

用户任务明文授权补齐 P0–P4/架构第一波未完项 ⇒ **gate1 = authorized-by-task**（§8.5 自动迭代 + 本指令范围）。
