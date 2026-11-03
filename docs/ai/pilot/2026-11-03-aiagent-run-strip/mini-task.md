# mini-task — AIAgent Run 状态条 + dev-stack 走查

## Intent
composer 上方展示当前 session 活跃 Run（pending/planning/running/stuck），支持继续/详情，与 §7.3 Run 条一致。

## Spec

## Interaction Design

- composer 上方 Run 条：左为状态+步骤+任务预览（点击展开 stuck hint）；右为「继续」（stuck/failed）与「详情」（打开执行记录并定位 Run）。

- 无活跃 Run 时不占位（`runStrip.visible=false`）。
- 有活跃 Run：状态文案、步骤 `cur/total`、任务预览（截断）、可选 stuck hint（点击主区展开）。
- 「详情」打开 RunRecordPanel 并 `selectRun(id)`；stuck/failed 显示「继续」。

## Plan
- `runStatusLabels.ts` + 单测
- `ChatWidget` + `widget.css` Run 条 UI
- `AiAgentView` 轮询 + watchEffect；`RunRecordPanel` expose `selectRun`

## Task
- Allowed: `Plugins/AIAgent/web/**`, screenshots, pilot, brief §7.3
- **AC1** 无活跃 Run 条隐藏。**AC2** 有 Run 显示状态/步骤/预览。**AC3** 详情打开 RunRecordPanel 并 selectRun。
- Verify: `pnpm run test` runStatusLabels; `Plugins/AIAgent/web pnpm run build`; dev-stack `:7399` 走查

## 闸门1
用户指令「做 Run 状态条 后 dev-stack 走查 + 截图」— 默认批准实施。
