# 05-evidence：SmartTerminal 死会话 404（task-18）

## 验证矩阵（全部 Verified · wt-st @ feat/smart-terminal）
| 门禁 | 命令 | 结果 |
|---|---|---|
| 插件 web build | `pnpm run build`（SmartTerminal/web） | ✅ dist/index.js 61.23 kB，374ms |
| 类型+lint | `pnpm run check`（ForgeSelf.Web） | ✅ 0 errors（166 warnings=既有基线；DbAdmin 假红为 wt-st 缺 node_modules，--ignore-scripts 补装后转绿） |
| 设计门禁 | `pnpm run design:check` | ✅ style-tokens 无新增（基线 2501→1761 持续下降）；design-compliance a=209 b=0 c=0 d=0 零新增 |
| 冒烟 e2e | `npx playwright test e2e/plugins/smart-terminal/` | ✅ 2 passed（qa 主链 1.8s + 死会话 4.6s，零 mock 真实后端） |
| 视觉证据 | screenshots/e2e/smart-terminal/dead-session.png | ✅ 读图：banner 文案/标签 ✕ 置灰/「· 会话已失效」徽标/版本徽标 v1.2.4 全部正确 |
| vitest | `pnpm run test` | ✅ 1151 passed；3 failed=AIAgent 既有基线（task-19 归属，与本改动无关） |
| 后端过滤集 | `dotnet test --filter SmartTerminal` | ✅ 25/25，0 失败 |

## TDD 红灯复现（问题类闭环 G1）
`git stash`（摘除修复）→ 重建 dist → 仅跑死会话用例 → 预期红（404 计数持续增长/无 banner）→ `stash pop` 恢复。结果见运行日志 `.temp/e2e-red.log`（红/绿结论以此为准，回填 07）。

## 关键实现锚点（SmartTerminalView.vue @ feat/smart-terminal）
- `deadSessions`（Set）+ `deadNotice`（仅提示一次守卫）
- `isSessionDead404(e,id)` / `markSessionDead(id)`（helpers）
- pollOutput：入口 dead 守卫 + catch 404 分流（非 reset 不再静默吞 404）
- loadCommands：补 try/catch（原版无 catch = 反复 toast 元凶）+ 404 分流
- selectSession.getSession catch：404 → markSessionDead
- refreshApprovals：防御 catch（无 404 路径，401 才上抛）
- closeTab：清 deadSessions 登记；runCommand/输入框/重跑按钮：dead 禁用
- 模板：tab `dead` class + 「✕ 」标题前缀 + banner-dead + dead-badge；CSS 仅 opacity/line-through（零色值）
