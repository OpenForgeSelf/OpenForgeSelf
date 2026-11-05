# mini-task：SmartTerminal 死会话 404 轮询（task-18，轻量路径）

## Intent（为什么/做什么/到什么程度）
51888 升级后实测暴露：宿主重启后 S-R 恢复的会话 id 服务端已不存在，前端对 output/commands 轮询持续 404 + 反复 toast「会话不存在」，标签永不关闭。目标：首个 404 即停轮询 + 标签置灰 ✕ + 仅提示一次；同类轮询点全部按 problem-class-closure 扫查封口。

## Spec（行为定义 · 含交互设计）
- 触发：轮询/加载类请求收到 404（会话不存在，非网络抖动）。
- 行为：`markSessionDead(id)` 登记 deadSessions（Set）→ 停止该会话一切后续轮询（pollOutput/loadCommands 入口守卫）→ `banner-dead` 提示一次（文案含「已停止轮询」）→ 标签渲染置灰（opacity 0.55）+ 标题前缀「✕」+ 命令历史区「· 会话已失效」徽标。
- 边界：输入框与重跑按钮对死会话禁用；关闭死标签清除登记；demo/fixture 模式不受影响；无色值新增（token 合规）。
- 同类扫查（PC-024 口径）：pollOutput（output）/loadCommands（commands）/getSession（selectSession）三处 404 → markSessionDead；refreshApprovals 端点无 404 路径，仅补防御 catch（401 才 noteError，其余静默）；S-R 恢复是唯一把不存在 id 留在轮询面的入口（保留只读恢复态，不做预校验）。

## Plan（改动文件）
1. `Plugins/SmartTerminal/web/src/SmartTerminalView.vue` — 状态+助手+三处 404 守卫+防御 catch+模板/样式（11 处编辑）。
2. `Plugins/SmartTerminal/plugin.json` — Version 1.2.3 → 1.2.4（随批 bump）。
3. `ForgeSelf.Web/e2e/plugins/smart-terminal/smart-terminal.spec.ts` — 追加死会话 e2e（零 mock）+ 截图。

## Task
- Allowed：上述 3 文件 + 工件目录 + screenshots/e2e/smart-terminal/。
- Forbidden：SmartTerminal 后端 C#、其他插件、e2e fixtures/global-setup。
- 验证命令：`pnpm run build`（SmartTerminal web）/ `pnpm run check` / `design:check` / `npx playwright test e2e/plugins/smart-terminal/`（零 mock 真实后端）/ vitest / `dotnet test --filter SmartTerminal`。

## Evidence / Review
见 05-evidence.md / 06-review.md；问题类归档见 07-final-report.md 三节。

## Interaction Design（无界面交互 → 实为轻量 UI 行为，qa-ci 2026-11-05 补）

点开已失效会话的留存页签 → 页内出现一次性 banner「会话不存在/已失效」，该页签标题置灰并出现 ✕ 关闭钮；3 秒窗口内不重复发起会话读取（无新增 404 刷屏）。边界：banner 只提示一次；置灰页签仍可手动 ✕ 关闭；正常会话页签行为不变。S-R 路径对不存在 id 注入的响应码契约不变（404 语义保留，仅前端消费方式收敛）。