# 00 Repository Understanding — AIAgent 新组件 composer 升级

调研日期：2026-10-15 · 调研方式：本仓实读代码（非推测）

## 1. 现状：中栏输入区有什么

| 位置 | 内容 |
| --- | --- |
| 组件 `ChatWidget.vue` `<footer>` | `.aiw-input`（textarea `placeholder=config.placeholder` + `.aiw-send` 发送/停止键）、`.aiw-note`（`showFooterNote` 为 false，不显示） |
| 键盘 | `onKeydown`（491 行）：仅处理 `Enter`（`!shiftKey && !isComposing`）→ `doSend()`；`onKeydownGlobal` 处理 Esc / Tab 焦点陷阱 |
| 快捷 chips | `config.quickReplies`（空会话欢迎态显示，发送后 `showQuick` 关闭） |
| 上下文选择 | **无** |

→ 迁移到 `agent-core` 组件后，旧 `ChatPanel.vue` 的 composer 能力**全部丢失**，输入区退化成「纯 textarea + 发送键」。

## 2. 旧 composer 有什么（可搬的能力）

来源：`git show HEAD:Plugins/AIAgent/web/src/components/ChatPanel.vue`（随迁移删除，1645 行）

| 组成 | 细节 |
| --- | --- |
| chips 行 | 目录 chip / 工具 chips / 技能 chips / Agent chip，可单个删除 —— 「所见即所发」 |
| textarea | `placeholder="输入消息…（Enter 发送 / Shift+Enter 换行）"` |
| 操作栏 popover × 5 | 📁**目录**（绝对路径输入 + 最近使用 + 提示）／ 🔧**工具**（多选 + 搜索，未勾选=默认作用域，勾选=仅启用所选）／ ⚡**技能**（多选 + 搜索，注入 system prompt）／ 🤖**Agent**（单选）／ 💠**模型**（单选 + 搜索，持久化） |
| 发送/停止 | 一个按钮两种形态 |

数据源保存情况（`AiAgentView.vue` 实读）：
- `projectDir`（213 行附近）/ `selectedToolNames`(233) / `selectedSkillIds`(235) / `activeAgentId`(213) 全部保留；
- `loadAgentTools()`(416) → `/api/ai-agent/chat/tools`；`loadAgentSkills()`(438) → `/api/project/skills` 或 `/api/skills`；
- 上面这些状态**已经在喂** `buildAgentChatConfig` 的 deps（`agentBridge.ts` 260-263 行）→ 后端发消息时工具/技能/Agent 本来就带上了，**只是前端没 UI 可以改**。

## 3. 数据源盘点

| 能力 | 端点 | 状态 |
| --- | --- | --- |
| 工具 | `GET /api/ai-agent/chat/tools` | ✅ 现成（112 个，按 pluginId 排序，已过滤 `submit_plan/complete_step/request_help` 三个计划流内部工具） |
| 技能 | `GET /api/project/skills` / `GET /api/skills` | ✅ 现成（有项目目录走前者） |
| Agent | `GET /api/agents` | ✅ 现成 |
| 模型 | `GET /api/ai-models`（宿主 `aiModelsApi.ts`） | ✅ 现成 |
| 脚本 | `GET /api/scripts`（ScriptRunner 插件，`scriptRunnerApi.ts:68`） | ✅ 现成（插件提供） |
| 项目目录 | `POST /api/project/directory`、`GET /api/project/directory`、`GET /api/project/browse-directories?path=`、`GET /api/project/files?path=`、`GET /api/project/file?path=` | ✅ 现成 |
| **已登记项目列表** | 后端 `IProjectRegistryService.GetAll()` **已实现**（`ProjectRegistryService.cs:18`，按最近活动倒序；`POST /api/project/directory` 时自动 `Register` 进 sems 共享清单） | ⚠️ **无 HTTP 端点暴露**（`ProjectController` 只有 directory/skills/files/file，无 list） |
| 命令目录 | — | ❌ **本仓与组件源均无**（`CommandCatalog` 全仓 0 命中；组件源 `REQUIREMENTS.md`/`ROADMAP.md` 无此需求，ROADMAP 只有「附件上传」待定） |

## 4. 组件源（ai-assistant-widget-1.13.0）能复用什么

- 没有任何命令/提及能力 → **`/` 与 `@` 是本项目新增，组件里只能做「通用骨架」**。
- 组件既有扩展范式（v1.8.0 迁移已定的三条）：`showHeader` / `transport` / `sessionId` —— **一律是 config 注入，组件不含宿主业务**。
- 结论：新能力必须沿用这个范式 —— 组件提供「触发面板骨架 + 渲染」，**数据源与业务语义由宿主注入**，组件默认关闭，独立交付时行为不变。

## 5. 约束

1. 组件代码在 `Plugins/AIAgent/web/src/agent-core/**`（迁移资产），改动要保持「可独立交付」，默认行为不得变化；
2. `0 自定义 token`：样式只用 `--el-*` / 已有 `--aiw-*`，不发明新色值（`design:check` 棘轮）；
3. 插件前端构建走出树（宿主 vite + wrapper config），改完必须 `pnpm run build`；
4. 按 `AGENTS.md` §11：闸门1 批准后才允许 Implement；本任务含后端端点 → 需 `dotnet build` + xUnit + 插件 e2e + 现场验收。
