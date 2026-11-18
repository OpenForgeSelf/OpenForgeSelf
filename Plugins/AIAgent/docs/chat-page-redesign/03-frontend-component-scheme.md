# 智能体聊天页 · 前端组件方案

- 版本：v1.0
- 日期：2026-11-18（Asia/Shanghai）
- 状态：待与后端方案对齐；先不提交、不开工
- 依据：`01-requirements.md` v1.1、`prototype.html`、群内对统一入口与助手面板的要求
- 约束：不得读取、参考或复用既有智能体聊天页的视图、组件与布局；也不得把插件 `docs` 下旧工作台原型当作新界面来源。视觉以 `prototype.html` 与 `previews/` 为准。

## 1. 结论

同一套新组件支持两种布局：整页三栏（路由 `/ai-agent`），以及收缩后的右侧助手面板。数据库、智能终端等插件只嵌入这一套，禁止再各自实现聊天或执行界面。

能力归属在智能体插件（id `ai-agent`）。宿主只放**嵌入契约类型**和**加载器**，不实现第二套界面。本轮验收：智能体页能切到或演示助手面板形态，契约已预留并可传入布局、当前上下文、工具范围；其它插件的实际挂载在契约落地后接入。

调用一律走后端即将给出的**统一能力入口**，不用旧的聊天流 / 计划执行流 / 工作流进度作为本页或嵌入面板的主入口。

## 2. 落点

| 部分 | 位置 | 说明 |
|------|------|------|
| 全新 UI | `Plugins/AIAgent/web/src/assistant/`（新目录） | 全部新建。旧聊天页文件本轮不读、不改、不 import。路由改挂新页面容器后，旧入口不再暴露。 |
| 整页容器 | 新的页面视图，作为 `plugin.json` `frontend.views` 的路由导出 | `<AgentAssistant layout="page" />` |
| 可嵌入导出 | 同一构建的命名导出 `AgentAssistant` | 其它插件按名加载，不复制源码 |
| 嵌入契约与加载器 | 宿主 `ForgeSelf.Web/src/shared/agent-assistant/`（仅类型 + `loadAgentAssistant()`） | 用现有 `loadPluginView` 加载 `ai-agent` 的 `AgentAssistant`。符合「消费 ≠ 实现」：宿主和其它插件只消费，不重做界面。 |
| 主题 | `workshop-forge` 与 `DESIGN.md` token | 只用 `--el-*` 与设计 token；禁止自造 `--surface-*` / `--forge-*` / `--app-*`。组件内不显式 `import { ElXxx }`。三禁：无 emoji、无胶囊按钮、无蓝紫渐变。 |

插件前端仍是独立构建、经宿主远程加载。智能终端、数据库等插件通过加载器拿到同一组件实例，传入契约 props，而不是把 Vue 源码拷进自己的 `web/`。

## 3. 组件拆分

展示与取数分离：展示组件只接 props / 抛事件；会话与执行过程由组合式订阅统一入口；纯函数（消息归并、面板状态、主按钮让位）放到 `*.logic.ts`。

```text
AgentAssistant                  根：按 layout 在 page / panel 之间切换，不写两套页面
├── AgentSessionList            会话标题、模式、状态；选中态为金色左边线
├── AgentPageHead               眉题、会话标题、唯一金色主按钮（新建 / 让位给决策）
├── AgentStage
│   ├── AgentEmptyState         印章「匣」+ 定位句 + 干净输入
│   ├── AgentThread             用户气泡（右齐、无「你」）；助手通栏正文（无气泡、无名称）
│   │   ├── AgentToolRow        默认一行（名称 + 摘要），展开才看细节
│   │   ├── AgentApprovalCard   对话主区：批准后执行 / 取消
│   │   └── AgentStuckCard      原因 + 补信息后继续 / 跳过 / 取消
│   └── AgentComposer           模式、智能体、模型、+ 附加、发送 / 朱砂停止
└── AgentWorkspace
    ├── AgentWorkspaceCatalog   「打开面板」11 项入口（名称、说明、状态）
    └── 计划 / 执行记录 / 工作流 / 记忆 / 设计 / 建议 / 项目 / 待办 / 技能 / 工具 / 代码片段
```

右侧 11 个面板的 id 与原型一致：`plan`、`records`、`workflow`、`memory`、`design`、`suggest`、`project`、`todo`、`skills`、`tools`、`snippets`。入口列表的 id 为 `catalog`。

主按钮规则（整页与面板相同）：每屏只有一个金色主操作。默认是「新建会话」。对话里或工作区出现「确认并执行」「批准后执行」时，「新建会话」改为幽灵按钮，把主按钮让给当前决策。「全部面板」只是返回，不是主按钮。

## 4. 两种布局

**整页模式 `layout="page"`**：左会话、中页头 / 对话 / 输入、右工作区。尺寸与交互以原型为准（会话列约 248px，工作区约 380px）。短对话且右栏停在入口时，对话底对齐贴输入区；打开具体面板时对话顶对齐，与右栏齐平。空会话也不收掉右栏，默认停在入口。待确认计划、正在跑的工作流、卡住的执行记录可以自动打开对应面板，但必须能回到入口并改开其它面板。空会话、普通进行中的开放任务、对话里已有审批卡片时，右侧默认停在入口。

**助手面板模式 `layout="panel"`**：三栏不再并排。根组件 100% 填满宿主给出的右侧槽位，内部滚动，不得横向撑破宿主布局。建议宿主槽位宽 380–420px（与原型右栏一致），下限按 320px 做防溢出，不写死组件自身宽度。

面板内分层，不丢能力：

| 层 | 内容 | 说明 |
|----|------|------|
| `chat`（默认） | 紧凑页头 + 对话 + 输入 | 对话与输入始终在；下任务、看执行、能介入、可复盘在这一层成立 |
| `sessions` | 会话列表 | 从页头「会话」进入，返回回到 `chat` |
| `workspace` | 入口或某一个功能面板 | 从页头「工作区」进入；顶栏「全部面板」回到入口 |

自动打开的计划 / 卡住等，在面板形态下改为推入 `workspace` 层，同样可以返回。主题、消息形态、工具行默认折叠、附加上下文规则与整页一致。页头改为紧凑型：保留「智能体 · 开放任务 / 计划任务」眉题和会话识别，主按钮规则不变。

本轮在智能体页提供布局切换（或同等演示入口），用于验收 F28、I13。该切换属于验收演示，不是产品常驻控件；正式产品里整页走路由，面板形态由宿主槽位挂载。助手面板形态的预览图在实现后补进 `previews/`，不能只交整页图。

## 5. 嵌入契约

本轮必须预留并在实现中落地。类型放在宿主共享目录，供本页与其它插件共用。

```ts
export type AgentAssistantLayout = 'page' | 'panel'

/** 宿主当前对象。助手只在此上下文中工作。 */
export interface AgentEmbedContext {
  pluginId: string            // 如 'ai-agent' | 'db-admin' | 'smart-terminal'
  surfaceKey?: string         // 同一插件多个槽位隔离，例如终端标签 id
  projectId?: string
  cwd?: string
  objectType?: string         // 如表、连接、终端会话
  objectId?: string
  title?: string
  extras?: Record<string, string>  // 仅展示，不扩大工具权限
}

/** 本插件允许使用的工具或能力边界。最终以后端执行为准，前端不得自行放行。 */
export interface AgentToolScope {
  allowTools?: string[]
  denyTools?: string[]
  allowCapabilities?: string[]     // 如 'shell' | 'fs.write' | 'db.write'
}

export type AgentWorkspacePanelId =
  | 'catalog' | 'plan' | 'records' | 'workflow' | 'memory'
  | 'design' | 'suggest' | 'project' | 'todo' | 'skills' | 'tools' | 'snippets'

export interface AgentAssistantProps {
  layout: AgentAssistantLayout
  context?: AgentEmbedContext
  toolScope?: AgentToolScope
  initialPanel?: AgentWorkspacePanelId
}

export interface AgentAssistantEmits {
  insertText: [text: string]   // 写入宿主编辑面（例如终端输入区），不得自动执行
  openPath: [path: string]
}
```

宿主或其它插件的用法（契约就绪后，本轮只预留，不在本轮改智能终端 / 数据库页面）：

```vue
<aside class="host-assistant-slot">
  <component
    :is="AgentAssistant"
    layout="panel"
    :context="context"
    :tool-scope="toolScope"
    @insert-text="insertToHost"
  />
</aside>
```

`loadAgentAssistant()` 从插件前端清单读取 `ai-agent` 入口，按名加载 `AgentAssistant`。会话按 `pluginId` + `surfaceKey` 隔离，避免整页与某插件侧栏、或两个终端标签抢同一份会话。传入的 `context` 随每次发起任务带给统一入口；切换宿主对象时更新 props，进行中的任务不暗改已发出请求的上下文。

工具范围：发起任务时原样传给统一入口。界面上的工具面板只展示后端按该范围返回的可见工具。未授权的能力不画入口，也不在前端「先调再等后端拒绝」作为正常路径。

## 6. 前端对统一入口的消费约定

路径与载荷以后端方案为准。前端需要能绑定下面这些实时事件和介入动作，并覆盖开放任务与计划任务（用参数区分模式，而不是两套互不相通的客户端）。

**实时事件（至少）**：文本增量与完成；工具开始 / 更新 / 结束（名称 + 摘要 + 可展开细节）；计划草稿与更新；步骤进度（完成 / 进行中 / 失败 / 卡住）与轮次；审批出现与结束；工作流状态；运行开始 / 停止 / 完成 / 失败 / 卡住；以及本轮检索到的记忆、建议、代码片段等可展示数据。

**介入动作（至少）**：发起任务（带 mode、agentId、modelId、正文、附加上下文、context、toolScope）；停止；确认计划 / 改计划；审批通过或取消；卡住时跳过、补信息后继续、取消；失败重试；显式写回记忆；下发待办。

附加上下文（技能、提示词、记忆、MCP、项目、图片）只在「+」菜单中选择；选中后在输入框上方出现可移除标记，未附加不占那一行。模型列表只读宿主已有配置，不引导另开账号。

若后端事件字段与上述对不齐，以对齐后的后端方案为准，本页不迁就旧接口做适配层长期并存。

## 7. 本轮边界与验收对应

- **做**：新组件整页走通 F01–F26、I01–I12；布局切换或演示满足 F28、I13；客户端只走新统一入口，满足 F27（与后端方案对齐后实现）。
- **预留、本轮不改其它插件页面**：智能终端、数据库的实际挂载。加载器与类型先落地，挂载在契约就绪后进行，仍然用这一套组件。
- **不做**：云端计算机、团队共享工作区、第二套运行时、即时通讯外部会话、在本页管理模型账号。
- 未锁定门禁前不开工、不宣布完成、不自行合并或推送。本文先放在本目录，提交另作安排。

## 8. 请后端对齐的点

1. 统一入口的事件名、载荷和介入 API，请覆盖第 6 节，并保证 `context` / `toolScope` 在服务端生效。
2. 会话列表是否按 `pluginId` + `surfaceKey` 过滤，请在方案里写明。
3. `insertText` 只是前端写回宿主编辑面，不经过 Agent 执行通道；与「批准后执行」不是同一条路。
