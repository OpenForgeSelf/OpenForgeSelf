# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。
> Task ID：2026-10-14-aiagent-widget-migration

## 项目结构

OpenForgeSelf（铸己匣）根目录关键组成（本次探查实测）：

```
ForgeSelf.Api/            ASP.NET Core API（.NET 10 + SQLite + NewLife.XCode）+ 插件装载器（ForgeSelf.Api/Plugins/ 为运行时代码目录）
ForgeSelf.Web/            Vue 3 SPA（Vue 3.5 + Vite 6 + TS + Element Plus + Tailwind 4 + Pinia + pnpm）+ Playwright e2e 目录
ForgeSelf.Api.Tests/      后端测试（xUnit + Moq + FluentAssertions）
Plugins/<PascalCase>/     插件源码（仓库根；AIAgent 在其中）—— 非宿主装载器目录
docs/                     规范、功能文档、ai/pilot 工件、18-templates 模板
e2e/（实际在 ForgeSelf.Web/e2e/）Playwright 插件层 e2e
scripts/                  PowerShell 构建/发布/验证脚本（一律 pwsh 调用）
```

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 宿主后端 | .NET 10 + ASP.NET Core + SQLite + NewLife.XCode | `ForgeSelf.Api/ForgeSelf.Api.csproj`、`ForgeSelf.Api/Data/XCodeConfig.cs` |
| 宿主前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia + pnpm | `ForgeSelf.Web/package.json`、`vite.config.ts` |
| 插件前端 | 独立 Vite 工程，产物 ESM（lib 模式），`vue`/`vue-router`/`pinia`/`element-plus`/`@element-plus/icons-vue` 全部 external，由宿主 import map 解析到同一实例 | `Plugins/AIAgent/web/vite.config.ts`、`plugin.json#frontend` |
| 插件测试 | 宿主 vitest（jsdom），include 覆盖 `../Plugins/*/web/src/**/*.test.{ts,tsx}` | `ForgeSelf.Web/vitest.config.ts:31-34` |
| e2e | Playwright（零 mock，真实宿主 + 真实 token） | `ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts` |

## 架构特点

### AIAgent 插件（本次替换目标）

- 目录：`Plugins/AIAgent/`，`plugin.json` 当前 `Version: 1.7.4`，`frontend.route: /ai-agent`，`views: ["AiAgentView"]`，入口 `web/dist/index.js`。
- 前端结构（`Plugins/AIAgent/web/src/`）：
  - `AiAgentView.vue`（34KB）：三栏布局根组件 —— 左栏 `SessionPanel`（会话列表 + Agent 列表 + 统计）、中栏 `FileEditor`(可选) + `ChatPanel`（**聊天区，本批替换目标**）、右栏 `ContextPanel`（目录/工具/技能/记忆）；另含 `AgentEditDialog`、`RunRecordPanel`。
  - `components/ChatPanel.vue`（50KB）：中栏聊天区 = 顶部条（折叠/标题+版本徽标 `.chat__version`/状态/执行记录/右栏折叠）+ `StepProgressCard`（计划驱动步骤卡 `.spc`）+ 消息列表（ElScrollbar + 工具调用卡片 `.chat__tool-card` + Markdown）+ 错误条 + composer（chips + 五个 popover 选择器：📁目录/🔧工具/⚡技能/🤖Agent/💠模型 + textarea `.chat__textarea` + 发送/停止）。
  - `http.ts`（22KB）：`streamAgentChat`（POST `/api/ai-agent/chat/stream`，自定义 SSE 事件：content/tool_call/tool_result/usage/done/error，camelCase）、`streamAgentRun`（POST `/api/ai-agent/runs`，计划驱动 SSE）、`fetchSessions`（GET `/api/ai-agent/chat/sessions`）、`apiGet/apiPost/apiPut/apiDelete`、`withQuery`。
  - `types.ts`：`ChatMessage`（含 `toolEvents`/`toolCalls`）、`AgentDefinition`（含 `executionMode: 'free'|'plan'`）、`AIModel`、`SessionSummary`、`ToolEvent` 等。
  - 测试：`http.test.ts`、`http.session.test.ts`、`sessionArchive.test.ts`、`toolEvents.test.ts`、`components/SessionPanel.test.ts`（宿主 vitest 跑）。
- 后端：`Controllers/AIChatController.cs`（Route `api/ai-agent/chat`：stream/history/sessions/tools 等）、`AgentRunsController.cs`、`AgentsController.cs` 等；**无 OpenAI 兼容端点**（已实测 grep `chat/completions` 零命中）。
- e2e：`ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts` 现有 6 条用例，断言面包括：①远程加载+三栏 `.sess/.chat/.ctx`+版本徽标 `.chat__version`+左上角诊断 ②流式端点 SSE 结构（"type" 字段） ③composer 工作台交互（`.chat__actionbar` 五入口+popover+chips） ④Agent 增删改闭环（左栏 `.sess__*`） ⑤执行模式开关（`.aed__mode-opt`） ⑥计划驱动执行（`.chat__textarea` 发送→`.spc` 步骤卡→`.chat__bar-right` 执行记录） ⑦Agent 关联工作流区块。

### 迁移源组件（ai-assistant-widget-1.13.0）

- 位置：`D:\src\my-proj\ai-assistant-widget-1.13.0`（非 git 仓库；独立验证链 `npm run gate` 225/225 + mock 17/17 全绿，含本批前置修复：`useChat.js` return 补 `exportSession, exportAllSessions`）。
- 结构（`src/`）：`config.js`（约 60 个配置键，含 `inline`/`container`/`showHeader` 未有/`theme`/`primaryColor`/`darkMode`/`history.enabled`/`transport` 未有）、`components/ChatWidget.vue`（37KB：launcher + panel + header + 历史侧栏 + 消息区 + 刻度条 + quick + footer）、`components/MessageItem.vue`（2.5KB）、`components/blocks/BlockRenderer.vue`（21KB，Markdown 富块/图表渲染）、`composables/useChat.js`（33KB：messages/流式/重试/续传 Last-Event-ID/会话历史/导出/审批）、`composables/useRemote.js`（遥控）、`utils/`（auth/audit/charts/export/markdown/pairing/resume/schema/sessions）、`styles/widget.css`（27KB）+ `blocks.css`（26KB）、`embed.js`（Shadow DOM 挂载 + IIFE 出口）。
- 数据契约：默认走 OpenAI 兼容 `POST apiUrl`（payload `{model, messages:[system,...history], stream}`），SSE 解析 `choices[0].delta.content`（`useChat.js:457-535` requestStream），两级超时 + 断网续传（`Last-Event-ID`）。
- 依赖：**仅 vue**（devDeps: vite/jsdom 等），零第三方运行时依赖。

## 测试方式

| 通道 | 入口 | 覆盖 |
| --- | --- | --- |
| 宿主+插件前端单测 | `cd ForgeSelf.Web && pnpm run test`（vitest，include 插件 `*.test.{ts,tsx}`） | 宿主 src + 插件 web 纯逻辑 |
| 宿主类型/lint | `pnpm run check`（`vue-tsc -b && eslint . --ext .vue,.ts,.tsx`） | **不扫 `../Plugins`**（实测：插件 web 无独立类型门禁，构建仅 vite build/esbuild 转译） |
| 插件构建 | `cd Plugins/AIAgent/web && pnpm run build`（或出树 `pwsh scripts/build-plugin-web.ps1 -Plugin AIAgent`） | 产物 `web/dist/index.js` + `style.css` |
| 插件 e2e | `cd ForgeSelf.Web && pnpm exec playwright test e2e/plugins/ai-agent`（铁律 24：冒烟 1 条主链路） | 真实宿主 + 真实 token |
| 后端 | `cd ForgeSelf.Api && dotnet build`；`dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~AIAgent` | 本批无后端改动（N/A 预期） |
| 设计守卫 | `pnpm run design:check`（scripts/check-style-tokens.mjs + check-design-compliance.mjs） | 禁新增裸色值/禁 token 违规（快档门禁接线） |

## 构建命令

```powershell
# 插件前端（沙箱外直跑）
cd Plugins/AIAgent/web && pnpm run build
# 出树构建兜底（沙箱场景）
pwsh scripts/build-plugin-web.ps1 -Plugin AIAgent
# 宿主+插件单测 / 类型 lint
cd ForgeSelf.Web && pnpm run test
cd ForgeSelf.Web && pnpm run check
# 本插件冒烟 e2e
cd ForgeSelf.Web && pnpm exec playwright test e2e/plugins/ai-agent --grep "冒烟"
# 本地前后端（唯一合法入口，禁手敲 dotnet/vite 运行命令）
pwsh scripts/dev-stack.ps1   # 默认后端 7301 / 前端 7399；-Stop 停止
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/AIAgent/web/src/` | AIAgent 插件前端（AiAgentView + ChatPanel/SessionPanel/ContextPanel 等） |
| `Plugins/AIAgent/web/src/agent-core/` | **（本批新建）** 迁移源组件核心（零插件依赖，纯 Vue + props/config 驱动） |
| `Plugins/AIAgent/Controllers/` `Services/` `Models/` `Data/` | 插件后端（本批不动） |
| `ForgeSelf.Web/e2e/plugins/ai-agent/` | AIAgent 插件层 e2e（本批更新冒烟断言面） |
| `docs/ai/pilot/` | AI-Native 闭环工件（00-07） |

## 代码组织方式

- 插件 web 为独立 Vite 工程；`index.ts` 导出 `AiAgentView`（导出名=plugin.json `views[0]`），ep 组件样式由插件自备（`index.ts` import `element-plus/es/components/*/style/css`），颜色走宿主 `--el-*` 变量。
- 插件禁止 `import` 宿主模块（`@/` 别名、宿主 store/service/组件）；`useRouter` 走导航桥（`inject('forgeOpenPage')` / `window.__FORGE_OPEN_PAGE__`）。
- 宿主 vitest 对插件测试提供 `vue`/`@vue/test-utils`/`@element-plus/icons-vue` 三个测试期 alias（指向宿主 node_modules）。

## 现有工程规范

- `AGENTS.md` §11：开发任务唯一流程 = AI-Native 九阶段闭环（00-07 工件 + 闸门1/2/3；缺件 pre-commit hook 硬拦）。
- `plugin-development` 铁律：改插件必走维护闭环六步（①门禁 ②本插件 1 冒烟 e2e ③起源码本地前后端验收 ④本地目录发布（须用户同意）⑤真实走查 ⑥文档+日志）；插件根视图必显示版本徽标（铁律 13）；e2e 冒烟仅 1 条主链路（铁律 24）；禁止 agent 停/启/杀宿主；本地起前后端只能用 `scripts/dev-stack.ps1`。
- 设计门禁三步：`pnpm run design:check` / Spec 必含 Interaction Design 节 / 走查符合性（`ui-ux-design` 清单）。
- 交互硬约束（§3.4）：点即保存、操作成败可见、防闪、空态分级、分页边界、破坏性操作二次确认、版本展示、禁用态可见原因。
- 数据安全：禁止删除任何数据目录/文件；测试库用随机后缀隔离目录。
- 发布规范：本地目录更新源 `release-local.ps1 -Version v<X.Y.Z> -Sign -UpdateDir <目录>`（给人装必 `-Sign`）；禁止手工 Copy-Item 产物。

## 候选低风险任务

（不适用 —— 本任务为**用户明确指定**的组件迁移 + 聊天区替换，非从候选池选取。候选中风险最低的切面 = 先完成「agent-core 迁入 + AiAgentView 中栏替换 + 冒烟 e2e 更新」，暂不触碰后端/数据/插件发布链之外的范围。）

## 选择该任务的原因

用户指令（2026-10-14）：「将该组件迁移至本项目中，然后替换掉当前 AIAgent 的聊天区」，并在澄清中明确：组件（ai-assistant-widget）升级为项目**重点组件**（一个组件代表一个 agent，未来所有 agent 功能都往组件上做），支持内嵌/悬浮双形态 + 属性控制显示隐藏；本批先完成迁移 + AIAgent 页面引用组件展示聊天信息，目录/agent/工具/技能/模型等组件暂无功能**先记录 TODO、迭代补上**。
