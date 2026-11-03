# Spec — SmartTerminal

## In

- 插件 `Plugins/SmartTerminal`（Id `smart-terminal`，Name 智能终端）
- Windows：cmd / powershell / pwsh + 探测 WT profiles；非 Windows 优雅降级
- 权限：Auto / AskWrite / AskHighRisk；可配置 JSON 规则
- JSONL 审计 + 历史重跑；脱敏
- 自带 web UI；`st_*` Agent 工具
- 接入 slnx + ForgeSelf.Api ProjectReference

## Out

- 不改 mcp-center `run_terminal_command` 白名单语义
- 不上移契约到 Abstractions（首个消费者后再 ADR）
- 不做 IM 推送审批

## Interaction Design

见 `Plugins/SmartTerminal/docs/ui-design-brief.md`。
