---
id: 2026-11-05-smart-terminal-dead-session-poll-404
title: SmartTerminal 死会话轮询持续 404——S-R 恢复的陈旧会话 id 无失效机制
pc: PC-024
severity: P3
evidence: Verified
---

# case：SmartTerminal 死会话轮询持续 404（2026-11-05）

## 现象
51888 宿主升级（0430）后实测：SmartTerminal 前端 S-R 恢复的会话 id 服务端已不存在，前端对 `output`/`commands` 每秒轮询持续 404；「会话不存在」错误反复 toast；标签永不关闭。

## 时间线
- 2026-11-05 凌晨升级后走查发现（输入8 登记为 task-18）。
- 12:3x lead 接管实现（st-dev 侦查交接）：`pollOutput` catch 仅 reset 时 noteError（非 reset 静默吞 404）；`loadCommands` 无 catch（404 裸抛 → 反复 toast）；S-R 恢复是唯一把不存在 id 留在轮询面的入口。
- 12:5x 修复 + e2e 红/绿闭环：无修复红灯（`banner-dead` 不可见 + 404 计数持续增长），修复后 2/2 绿（3s 零新增 404）。

## 特征签名
- 网络面板：`/api/smart-terminal/sessions/{id}/(output|commands)` 404 每秒重复。
- UI：错误 banner 反复出现；标签一直活跃态。
- 触发前提：宿主重启/会话被服务端回收，前端 sessionStorage 恢复了陈旧会话 id。

## 修复
见 [PC-024](../problem-classes/PC-024-frontend-poll-error-handling.md)；实现 `Plugins/SmartTerminal/web/src/SmartTerminalView.vue`（deadSessions 登记 + 三处 404 分流 + approvals 防御 catch）。

## 守卫
`ForgeSelf.Web/e2e/plugins/smart-terminal/smart-terminal.spec.ts`「dead session restored」用例：S-R 注入不存在 id → banner 一次 + 标签置灰 ✕ + 3s 零新增 404（红/绿已验证）。