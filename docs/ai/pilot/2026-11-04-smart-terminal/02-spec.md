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

## Linux 一等公民（反馈修订）

- 外壳探测：`bash` / `zsh` / `sh`（及 `fish` / `dash`）与 Windows 壳并列注册；不可用项 `available=false`。
- 默认壳：`$SHELL` → bash → zsh → sh（Linux/macOS）；Windows 仍优先 pwsh/powershell/cmd。
- 权限 / 审批 / 审计路径与 Windows 共用同一套会话服务。
- CT 画廊：`ForgeSelf.Web` → `pnpm run ct:server` → 侧栏 **SmartTerminal**，覆盖空会话、直播输出、三级徽章、审批弹窗、拒绝/批准、审计过滤、脱敏、错误、Linux 多壳选择器。
