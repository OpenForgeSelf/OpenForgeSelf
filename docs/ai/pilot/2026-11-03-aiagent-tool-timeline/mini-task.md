# mini-task：工具默认折叠 + 正文/工具时间序交错

> 日期：2026-11-03｜级别：轻量（插件 web + agentBridge + agent-core）
> 闸门1：用户「改！」= 授权 Implement

## Intent

- 工具块默认**折叠**（running/error 可自动展开）；正文与工具按**发生顺序**交错展示，不再「全部工具压在整段文字上」。

## Spec

## Interaction Design

- 助手消息内：工具块默认只显示标题行，点击展开 I/O；执行中/失败块默认展开。
- 正文段落与工具块按发生顺序自上而下排列（不再整段文字被工具块整体盖住）。

- FR-1 `BlockRenderer`：tool 默认 `open=false`；`status===running|error` 初值展开；`open:true` / `collapsed:true` 显式覆盖。
- FR-2 新增块类型 `text`（段内 Markdown）；`agentBridge` 自由循环：`onContent` 推/更新 `id: seg-text-live`；`onToolCall` 前 finalize 文本段；流结束 flush 余下文本。
- FR-3 `MessageItem`：块按数组序渲染；含 `text` 块时持久消息不再重复渲染 `content`（流式尾巴仍用 `content`）。
- FR-4 更新 `BlockRenderer.test.ts` 守卫；brief §4.2/§6 P9 同步。

## Task

- **Allowed**：`Plugins/AIAgent/web/src/**`（agentBridge、agent-core、brief）、本 pilot、日记。
- **Forbidden**：宿主 ForgeSelf.Web、后端 SSE 契约变更、git 提交（除非用户另嘱）。
- **验收 AC1**：成功态 tool 默认折叠（单测守卫）。**AC2**：text+tool 交错 blocks 序。**AC3**：持久消息不重复 content 气泡。**AC4**：brief §4.2 同步。
- **验证**：`ForgeSelf.Web` → `pnpm run test -- BlockRenderer.test.ts`
