# 05 Evidence — AIAgent Run 状态条

## 验证（Verified）

| AC | 证据 |
|----|------|
| AC1 | 无 Run 时 baseline 截图 / watchEffect visible false |
| AC2 | runstrip-mock-active 截图 + runStatusLabels |
| AC3 | RunRecordPanel defineExpose selectRun + openRunDetail |

| 项 | 命令/动作 | 结果 |
| --- | --- | --- |
| runStatusLabels 单测 | `ForgeSelf.Web` → `pnpm run test -- '../Plugins/AIAgent/web/src/runStatusLabels.test.ts'` | 2 passed |
| 插件构建 | `Plugins/AIAgent/web` → `pnpm run build` | dist/index.js OK |
| dev-stack | `pwsh scripts/dev-stack.ps1` | 前端 `:7399`、后端 `:7301`（`.temp/dev-stack.json`） |
| 走查 | 浏览器打开 `/ai-agent` | 见截图 |

## 走查截图

| 文件 | 说明 |
| --- | --- |
| `ForgeSelf.Web/screenshots/live-7399/aiagent-composer-baseline.png` | 无活跃 Run 时 composer 区（Run 条隐藏） |
| `ForgeSelf.Web/screenshots/live-7399/aiagent-runstrip-mock-active.png` | Run 条 UI（走查时对 `ChatWidget` config 注入 mock 活跃 Run；本 dev 数据无真实 plan Run） |
| `ForgeSelf.Web/screenshots/live-7399/aiagent-run-panel-empty.png` | 执行记录 Drawer + Run 条同框 |

## 备注

- 当前 dev-stack 会话「暂无执行记录」；真实 Run 条数据依赖计划任务 Agent + 后端 Run。轮询与 `runCard` 合并逻辑见 `AiAgentView.vue` `watchEffect`。
- 工具块默认折叠 + 交错 timeline 见同批 `BlockRenderer` / `agentBridge`（前序输入）。
