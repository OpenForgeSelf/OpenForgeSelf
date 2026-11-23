# Mini Task — GitHub 绑定 UX 二轮（市场前置检查 + 对比度守卫）

> Task ID：PILOT-2026-11-22-github-bind-ux-round2｜轻量形态｜主任务：`2026-11-20-github-device-binding`（feature: 101）。

## Intent（为什么）

一轮只覆盖设置页主动绑定；用户在插件市场点「从 GitHub 安装」时若未绑定，会直接收到 404/401 而不知原因。二轮把绑定前置到安装动作：未绑定先引导绑定，绑定成功自动续装（armed-resume），并补绑定域对比度守卫。

## Spec（做什么·到什么程度）

- `PluginStore.vue` 安装前置：`installSource('github')` 先 `ensureGithubBindingChecked()`；未绑定 → 不发安装请求，侧栏提示「安装 GitHub 源插件需要先绑定 GitHub 账号（私有仓库凭据）」+ 打开绑定对话框 + 武装本次安装（armed）；`onGithubBound` 自动续装；用户关掉对话框 → 「已绑定 GitHub 账号，继续安装」按钮手动续装；已绑定 → 行为不变。
- 绑定状态缓存 `githubBound: boolean | null`（null=未查过，进页/安装前查一次）。
- 对比度守卫：`workshop-forge.contrast.test.ts` +AC-11 用例（绑定域自定义面明暗两态 ≥4.5；EP 状态横幅暗色欠账另批记 TODO）。
- 错误体透传文案：绑定失败横幅直接呈现后端合并文案（「描述 · 错误码」），不再包装成通用网络异常。

## Interaction Design

- **点什么出现什么**：点「安装」（GitHub 源）→ 未绑定时：安装请求不发出（网络层零请求），侧栏出现提示行 + 绑定对话框弹出；完成绑定 → 对话框关闭、自动续装原目标；中途关闭对话框 → 提示行旁出现「已绑定 GitHub 账号，继续安装」按钮。
- **空态**：无（市场页已存在）。
- **加载**：前置检查期间安装按钮维持既有 busy 态。
- **出错**：续装失败走既有 installError 通道（侧栏 + toast）。
- **边界**：armed 状态不跨刷新持久（会话内一次性）；绑定成功但用户已切换目标时以 armed 目标为准。
- **验收标准**：主任务 02-spec AC11/AC12。

## Plan / Allowed

| 文件 | 动作 |
| --- | --- |
| `src/views/PluginStore.vue` | +前置检查/armed/续装/对话框挂载（+45 行量级） |
| `src/styles/themes/workshop-forge.contrast.test.ts` | +AC-11 用例 |
| `src/components/github/GithubBindPanel.vue` | 失败文案透传口径对齐（合并文案直呈） |

**Forbidden**：不改后端与安装主流程（只加前置钩子）；不改主题 css 色值；不动一轮组件四态结构。

## 验证

`pnpm run test -- --run src/styles/themes/workshop-forge.contrast.test.ts src/components/github` → 全绿；`pnpm run design:check`（提交批）。
