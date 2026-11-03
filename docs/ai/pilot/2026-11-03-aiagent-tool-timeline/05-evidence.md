# 05 Evidence — 工具折叠 + 时间序

## Changed Files

| 文件 | 变更 |
|------|------|
| `Plugins/AIAgent/web/src/agent-core/components/blocks/BlockRenderer.vue` | tool 默认折叠；`text` 块 |
| `Plugins/AIAgent/web/src/agentBridge.ts` | text 段 finalize + 工具交错 |
| `Plugins/AIAgent/web/src/agent-core/composables/useChat.js` | seg-text-live 清理；落库 content 去重 |
| `Plugins/AIAgent/web/src/agent-core/components/MessageItem.vue` | 交错时不重复 content 气泡 |
| `Plugins/AIAgent/web/src/agent-core/components/ChatWidget.vue` | 流式 blocks-only 展示 |
| `Plugins/AIAgent/web/src/agent-core/styles/blocks.css` | `.aiw-seg-text` |
| `Plugins/AIAgent/docs/ui-design-brief.md` | §4.2 / P9 |

## Validation（Verified）

| AC | 证据 |
|----|------|
| AC1 | BlockRenderer.test 默认折叠用例 |
| AC2 | agentBridge seg-text + onToolCall finalize |
| AC3 | MessageItem 有 text 块不重复 content |
| AC4 | ui-design-brief §4.2 |

| 命令 | 结果 |
|------|------|
| `pnpm run test -- BlockRenderer.test.ts`（ForgeSelf.Web） | 22/22 通过 |
