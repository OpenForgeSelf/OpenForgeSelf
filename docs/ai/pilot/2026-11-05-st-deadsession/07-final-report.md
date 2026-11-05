# 07-final-report：SmartTerminal 死会话 404（task-18）

## 交付
- `Plugins/SmartTerminal/web/src/SmartTerminalView.vue`：deadSessions/deadNotice 状态 + isSessionDead404/markSessionDead 助手 + pollOutput/loadCommands/getSession 三处 404 终态分流 + refreshApprovals 防御 catch + closeTab 清登记 + runCommand/输入/重跑死态禁用 + 模板（banner-dead/✕ 前缀/dead class/dead-badge）+ CSS（opacity/line-through，零色值）。
- `Plugins/SmartTerminal/plugin.json`：1.2.3 → 1.2.4（随批 bump）。
- `ForgeSelf.Web/e2e/plugins/smart-terminal/smart-terminal.spec.ts`：追加「dead session restored」用例（零 mock + 截图证据）。

## Validation（验证结果，全部 Verified）
- e2e：红（无修复：banner-dead 不可见）→ 绿（修复：2/2 passed，3s 零新增 404）；截图读图通过（banner/✕/置灰/v1.2.4 徽标正确）。
- 门禁：web build ✅ / check 0 errors ✅ / design:check 零新增 ✅ / vitest 1151 绿（3 红=AIAgent 既有基线，task-19 归属）/ SmartTerminal 过滤集 25/25 ✅。

## 问题类归档
- case：[2026-11-05 死会话轮询 404](../../11-troubleshooting/cases/2026-11-05-smart-terminal-dead-session-poll-404.md)
- PC：[PC-024 前端轮询面错误处理缺失](../../11-troubleshooting/problem-classes/PC-024-frontend-poll-error-handling.md)（guarded，G2 active）
- 台账/速查表/index 均已更新（PC-024 行 + 现象速查行）。

## 重构提案
无（登记-终止模式即类级修法；后续新轮询点按 PC-024 不变量自查）。

## 悬空教训清单
1. **e2e 基建（转 qa task-13/14）**：fresh worktree 首启 AIAgent 初始化可能挂起 → init-token 空 → 全部用例假红于「ForgeSetting.config 中未找到 ApiToken」；建议 globalSetup 对 init-token 失败给出明确报错 + 一次重试。（已记 TODO，非本任务范围）
2. **e2e 选择器坑（转技能检查项，来自 db-dev 发现）**：`[data-testid^="x-"]` 前缀匹配 + hasText 可选中容器致 click 落空。（已记 TODO）

## Review 协作交接与合流路径
- 合入路径：feat/smart-terminal 提交后，等 qa 的 main 批次落地 → rebase（spec 追加段冲突预期 trivial）→ ff 合 main。
- 51888 侧载/更新：随下一批发布（版本已 bump 1.2.4）；本次不单独发布。
