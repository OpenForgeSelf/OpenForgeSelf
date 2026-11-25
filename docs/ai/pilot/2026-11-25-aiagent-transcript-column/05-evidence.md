# 05 Evidence：智能体对话列随页面变宽，消息从顶部往下排

## Changed Files

- `Plugins/AIAgent/web/src/assistant/assistant.css`
- `Plugins/AIAgent/web/src/assistant/assistant.layout.test.ts`
- `Plugins/AIAgent/web/src/assistant/AgentAssistant.vue`
- `Plugins/AIAgent/web/src/assistant/useAgentSession.ts`
- `Plugins/AIAgent/docs/chat-page-redesign/01-requirements.md`
- `Plugins/AIAgent/docs/chat-page-redesign/02-test-plan.md`
- `Plugins/AIAgent/docs/chat-page-redesign/03-frontend-component-scheme.md`
- `Plugins/AIAgent/docs/chat-page-redesign/prototype.html`

## 验证

### AC-1 守卫先红后绿（Verified）

改样式前：`pnpm test -- src/assistant/assistant.layout.test.ts`（`Plugins/AIAgent/web`）为 2 failed / 1 passed。失败点是样式表仍有 `margin-top: auto`，且 `.aa-col` 不是铺满。

改样式后同一命令：3 passed。

### AC-2 插件前端测试与构建（Verified）

- 全量 `pnpm test`：`assistant.layout.test.ts` 3 passed。合计 12 files passed / 13 failed，141 passed / 15 failed。失败均为既有项：10 个套件 `Failed to load url vue`，以及 `http.session.test.ts`、`http.test.ts`、`sessionArchive.test.ts` 的 `localStorage is not defined`。与本次布局无关，TODO 已有「AIAgent 插件 web 测试基建债」。
- `pnpm run build`：`built in 421ms`，`dist/style.css` 含对话列 `width:100%`。

### AC-3 浏览器走查（Verified）

源码栈：前端 `http://127.0.0.1:7399/ai-agent`，后端 `http://127.0.0.1:7301`，插件源为仓库 `Plugins`。未动 `:51888`。

空会话：舞台类名 `aa-stage is-empty`，输入框宽 720px。

发送「布局走查：首条应该在顶部」后：

| 视口 | 对话列宽 | 输入框宽 | 首条距对话区顶部 |
| --- | --- | --- | --- |
| 1440×900 | 756px | 720px | 8px（等于区内边距） |
| 1800×900 | 1116px | 720px | 8px |

输入框在两档都水平居中：1440 时列左 276、框左 294（差值等于 `(756-720)/2`）；1800 时列左 276、框左 474（差值等于 `(1116-720)/2`）。列的 `margin-top` 为 `0px`。

截图：`ForgeSelf.Web/screenshots/dev/aiagent-transcript-1440.png`、`ForgeSelf.Web/screenshots/dev/aiagent-transcript-1800.png`。读图：首条在标题下方、输入框上方留有大块空白，没有贴住输入框。1440 上用户气泡靠对话列右缘；1800 上列更宽，气泡仍靠右缘，输入框仍是居中的窄条。

隔离数据根没有可用模型，会话列表标了「失败」。用户消息仍留在顶部，本次量的是这条消息的位置，不是模型回复正文。

## Known Limitations

- 未提交。`:51888` 上的已发布实例还是旧样式。
- 助手回复没有在这次走查里生成。后续消息与首条同一列，列本身从顶部往下排。

## Unresolved Issues

无布局未解项。插件前端既有 15 条测试失败不在本批。
