# Intent

> Task ID：`2026-11-19-oar-remaining-close`

## Problem

OAR×AgentHub 主路径已 NEAR-FULL，但管理面仍缺：agents/tasks ApiKeyPolicy、Session 操作 UI、Runtime probe/models UI、安装确认闭环、能力不支持时 Steer 禁用；验证与走查证据不足。

## Why

铁律 17 + design Interaction Design + 用户要求「没做的做完 + TODO 核对 + 跑起来走查截图」。

## Expected Outcome

1. F1–F5 落地（F2 最小 confirm 路径；vendor 真装属缓做）。
2. V* 有 HTTP/测试证据文件（V9/V10 ⏸）。
3. W* 走查截图（W9/W10 ⏸）。
4. TODO 勾选与证据一致；032 / 架构 / gaps 回写；pilot 00–07。

## Constraints

- 不实现缓做 D1–D12 / 不重写 Watch SSE / 不发明 P5。
- 禁止停杀 :51888；脚本用 pwsh；插件改动走 plugin-development 门禁+1 e2e。
- 提交卫生：无绝对路径/人名。

## Success Criteria

- agents/tasks 无 token → 401；有 token 管理面可用。
- Session UI 可 input/abort/close/records（stream 可见或可解释）。
- Runtime UI 可 probe/models；安装：plan→确认→执行（scripted/bundled 可解释）。
- 无 session 能力时 Steer 控件禁用+Reason。
- artifacts 目录有 verify JSON + walkthrough 截图；TODO 未完成仅剩 ⏸。
