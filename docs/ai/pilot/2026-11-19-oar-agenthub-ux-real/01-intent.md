# Intent

> Task：oar-agenthub-ux-real

## Problem

本机仅装 Grok，但 AgentHub UI 把 scripted / 未装 vendor 显示成可用或同质占位图标；Runtime 与 Session 挤在一页、JSON 常开，交互乱、无章法。

## Why

用户需要「一切走真实的」：未装即 not_found；测试 runtime 不进产品面；各 Agent 有品牌辨识；界面按 CRAP 分区。

## Expected Outcome

1. 真源审计文档确认 Grok-only。
2. 产品 Runtime 列表无 scripted；Available 仅 executable 安装。
3. Runtime/Agent 卡用 OAR 品牌图标（缺则字母 glyph）。
4. Runtime 库存与 Session 操作分区重设计；亮/暗截图。

## Constraints

- 不杀 :51888；不自动安装缺失 vendor；scripted 保留于 Worker smoke。
- 走 AI-Native pilot 00–07；plugin-development 门禁。

## Success Criteria

- `oar-real-vs-simulated.md` 已写。
- UI/API 列表无 scripted；Grok=已安装，claude/codex/opencode=not_found。
- 品牌图标互不相同。
- AgentHub web build 绿；Worker smoke 绿；截图入 `/opt/cursor/artifacts/oar-ux-real/`。
