# 00 Repository Understanding · tool-bridge-ui-fix

> 日期 2026-10-07 ｜ Task ID：TB-UI-FIX-20261007 ｜ 范围：ToolBridge 插件界面两处修正（1.0.2 → 1.0.3）

## 项目事实（均为本会话实读，Verified）
- 插件前端 = Plugins/ToolBridge/web/src/ToolBridgeView.vue（script setup，原生 HTML + --el-* 变量）+ clipboard.ts（copyText 依赖注入编排，无去重、无节流，每次点击都真写剪贴板）。
- 复制反馈现状：按钮文案 copiedKey === key ? 已复制 : 复制…；copiedKey 只在 doParse 里清空，doTurn / doExecuteParsed / reloadTurn 不清；反馈文案写进全页共用的 copyHint，渲染在页面最底部，会被保存工作根、本轮耗时等消息覆盖。
- 粘贴框 tb-paste-input 无任何聚焦处理。
- e2e = ForgeSelf.Web/e2e/plugins/tool-bridge/tool-bridge.spec.ts，6 条用例；无任何复制按钮用例；无 grantPermissions；仓内唯一涉及 clipboard 的是 design-system-showroom.spec.ts:695（defineProperty 模拟写入被拒）。
- 既有 3 处 tb-hint 断言（已载入回合 / 未落台账 / 回读一致）只依赖 copyHint，本任务不改 copyHint 的其他写入方。
- 版本：Plugins/ToolBridge/plugin.json 的 Version = 1.0.2。
- 门禁：scripts/verify-pilot-artifacts.ps1 要求任务目录内 00~07 各恰一份且非空；先例 2026-09-30-sign-default-off 为 00~07 + mini-task.md，故本任务出 00~07 八件，不另写 mini-task。
- 验证入口：插件 web 的 pnpm run build；发布链同参数 vue-tsc -b；插件层 e2e（e2e/plugins/tool-bridge）。

## Unknown（不得假定）
- Plugins/ToolBridge/web 是否已配 vitest（待 list_dir 确认；决定是否新增 clipboard.test.ts）。
- e2e 实际使用的浏览器内核（playwright.config 未读；grantPermissions 仅 Chromium 系保证可用）。
- 窗口切走再切回时 Chromium 是否对 textarea 触发 blur（设计依赖此点，只能 Inferred，需用户手测）。
