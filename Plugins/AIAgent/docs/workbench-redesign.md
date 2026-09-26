# AIAgent 工作台面板重构设计（Stage 3）

> 状态：**设计稿，待用户确认原型后实施**
> 来源：输入3·2026-09-08（「按照业界主流的工作台面板重构，输入框下面是选择目录、选择mcp\技能、模型等，不要瞎猜，先调研业界主流做法」）
> 配套原型：[workbench-prototype.html](./workbench-prototype.html)（自包含，浏览器直接打开）

---

## 1. 目标与问题

### 现状问题

当前 `/ai-agent` 页面为固定三栏（左 220px 上下文 / 中聊天 / 右 260px 会话统计），存在以下交互问题：

| # | 问题 | 表现 |
|---|------|------|
| P1 | **模型选择远离输入区** | 模型选择器挂在顶部栏右上角，发送前用户视线需要在「输入框（底部）↔ 模型选择（顶部）」之间大幅跳动 |
| P2 | **上下文选择与发送动作割裂** | 目录/MCP 工具/技能在左栏，但左栏的「选中」并不影响发送内容——用户无法在发送前确认「这条消息会带上哪些上下文」 |
| P3 | **左栏工具点击无实际语义** | ContextPanel 中 MCP 工具可点击高亮（`selectedTool`），但该状态不进入请求，属伪交互 |
| P4 | **首屏认知负担大** | 三栏全部展开，新用户需同时理解 5 个分组（目录/工具/技能/指令/记忆）才能开始对话 |

### 重构目标（用户指令拆解）

1. **输入框下方承载上下文选择**：选择目录、选择 MCP/技能、模型等，集中在 composer 操作栏
2. **业界主流模式**：发送前可见、可选、可撤回（What you see is what gets sent）
3. 保留铸己匣「工作台」定位（文件浏览/编辑、会话统计等真实能力不丢）

---

## 2. 业界调研（2026-09 实测/资料）

### 2.1 消费级 Chat 产品（composer 卡片模式）

**ChatGPT**（chatgpt.com）：
- 输入区是一张**卡片**：上 textarea、下操作栏
- 操作栏左侧 `+` 菜单统一收纳：附件、Create image、Think、Deep research、Web search
- 选中的模式/附件以 **chips** 显示在输入框内，发送前可见、可移除
- 渐进式披露：默认安静（无 chips），能力藏在 + 菜单

**Claude.ai**：
- 同为 composer 卡片；左侧 `+` 菜单（attach/screenshot/**project**/**skills**/connectors/plugins/web search）
- **模型与 effort 放在操作栏右侧、发送按钮旁**——发送前显式可见（成本/质量权衡前置）
- 选中 skills/connectors 后同样以可移除 chips 呈现

**Gemini**（2025-2026 新 UI）：
- 与 ChatGPT 收敛到同一形态：`一个文本框 + 一个加号 + 语音按钮`，上传与工具收进 `+` 菜单，模型/思考档位在右侧

### 2.2 IDE / 编码 Agent（工作台面板模式）

**Cline / Roo Code**（VSCode 扩展，与本项目最可比）：
- 输入框下方**操作栏**：左侧 mode 下拉（Architect/Code/Ask/Debug）+ MCP 图标 + Enhance；右侧发送
- 上下文用 `@` 提及：文件/文件夹/终端输出/git commit/URL
- MCP 服务器管理是独立图标入口（选中工具在面板内配置）
- 模型选择在顶栏，`Plan/Act` 双模式绑定不同模型配置（profile）

**Cursor（Composer）**：
- Composer：textarea + **上下文 chips**（@file/@folder 追加，chip 可删）
- 底部操作栏：模型选择、Agent 模式（Agent/Ask/Manual）、Auto-run 开关
- 「@ 提及 + chips」是编码 Agent 的事实标准

**OpenWebUI**（开源自托管，多模型）：
- 输入区 `+` 图标：文档/图片附件、web search、自定义工具
- 输入框内命令系统：`/` 提示词、`@` 模型、`#` 知识库
- 模型选择在顶部（支持多模型并行）

### 2.3 共性规律（业界共识）

| 规律 | 说明 | 出处 |
|------|------|------|
| **R1 输入区卡片化** | composer 是一张圆角卡片，与消息流背景区分 | ChatGPT/Claude/Gemini |
| **R2 下方操作栏** | 卡片内、textarea 之下一条工具栏：左 = 上下文入口，右 = 模型 + 发送 | 全部 |
| **R3 chips 呈现已选上下文** | 选中项变成可移除小徽章，发送前「所见即所发」 | ChatGPT/Claude/Cursor |
| **R4 渐进式披露** | 默认只显示一行图标，能力藏在 popover；首屏安静 | ChatGPT/Claude |
| **R5 模型选择靠近发送** | 模型/档位放操作栏右侧（发送旁），不是顶栏 | Claude/Cursor |
| **R6 目录=项目上下文** | IDE Agent 用 @ 提及文件/文件夹；消费级用 project 概念 | Claude(project)/Cursor(@) |

---

## 3. 方案设计

### 3.1 布局（两案备选，默认推荐 A）

**方案 A（推荐）：保留三栏工作台 + composer 承载选择**

```
┌──────────┬──────────────────────────────┬──────────┐
│ 左栏 240px │        中栏 聊天 + Composer        │ 右栏 260px │
│ (可折叠)   │                              │ (可折叠)  │
│ 文件浏览    │  ┌────────────────────────┐  │ 会话统计   │
│ ──────    │  │ [chips: 已选上下文…]      │  │ Agent 列表│
│ MCP 工具    │  │                          │  │          │
│ (查看+配置) │  │ textarea                 │  │          │
│ ──────    │  │                          │  │          │
│ 技能       │  │ 📁目录 🔧工具 ⚡技能 🤖Agent │  │          │
│ (查看)     │  │      [模型▾]     [发送 ➤] │  │          │
│ ──────    │  └────────────────────────┘  │          │
│ 记忆       │                              │          │
└──────────┴──────────────────────────────┴──────────┘
```

- 保留工作台定位：左栏文件浏览/编辑（FileEditor）、右栏统计继续可用
- **所有「发送相关」的选择集中到 composer**：目录、工具、技能、Agent、模型
- 左栏从「选择器」降级为「浏览器/查看器」：MCP 工具/技能列表保留展示（带描述），但不再承载「选中高亮」的伪交互
- 左右栏可折叠（图标按钮），折叠后接近主流单栏体验

**方案 B：单栏 + 抽屉（更接近消费级）**

- 只保留中栏；文件浏览/统计收进侧滑抽屉
- 更像 ChatGPT/Claude web，但牺牲「工作台」多面板并览能力
- 改动面大（ContextPanel/SessionPanel 需重写为抽屉），且与插件既有 FileEditor 流程冲突

**推荐 A 的理由**：用户指令为「工作台面板」（workbench），IDE 系（Cursor/Cline）均保留左树+中聊天的结构；A 改动集中在 ChatPanel（composer），风险可控。

### 3.2 Composer 结构（核心）

```
┌─ composer 卡片（圆角 8px、边框、聚焦时主色描边）─────────────┐
│                                                              │
│  [chips 区]（有已选项时显示）                                  │
│  ┌ 📁 D:\src\my-proj × ┐ ┌ 🔧 read_file × ┐ ┌ ⚡ 代码审查 × ┐ │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │  textarea（2~6 行自适应，Enter 发送 / Shift+Enter 换行）│    │
│  └────────────────────────────────────────────────────┘    │
│                                                              │
│  [操作栏]                                                     │
│  📁 目录   🔧 工具   ⚡ 技能   🤖 Agent          模型▾   ➤发送 │
└──────────────────────────────────────────────────────────────┘
```

#### 操作栏各控件

| 控件 | 交互 | 数据源（现有 API，零新增后端） |
|------|------|------|
| 📁 目录 | popover：路径输入 + 加载 + 最近使用；选定后 chip 显示目录名 | `POST /api/project/directory` |
| 🔧 工具 | popover：搜索 + 多选列表（带描述）；选中项变 chips | `GET /api/mcp-tools`（插件工具） |
| ⚡ 技能 | popover：多选（项目技能 + 内置技能）；chips | `GET /api/project/skills` |
| 🤖 Agent | popover：单选（含能力描述）；当前 Agent 以 chip + 头像呈现 | `GET /api/agents` |
| 模型▾ | 下拉（右起第一位，发送旁）；沿用 chatModelId | `GET /api/ai-models?enabledOnly=true` |
| ➤ 发送 | 主色按钮，发送中变 ■（停止） | 现有流式接口 |

#### chips 规则

- 每类上下文的已选项渲染为 chip（图标 + 名称 + ×）
- chip × = 移除该项；「目录」chip × = 切回未选目录状态
- 超出宽度横向滚动（或折叠为 `+N`）
- **发送请求 = 当前全部 chips**（所见即所发）

#### popover 规则（渐进式披露）

- 点击操作栏图标 → 上弹（输入框在底部）面板，含搜索框 + 列表
- 再次点击/Esc/点击外部关闭
- 默认安静：未选中任何项时 chips 区不占高度

### 3.3 选中状态 → 请求映射（后端契约变化）

当前请求体：`{ sessionId, message, chatModelId, agentId }`。重构后新增：

```jsonc
{
  "sessionId": "...",
  "message": "...",
  "chatModelId": "default:google/gemma-4-e4b",
  "agentId": "agent.generalist",
  // —— 以下为新增（后端 AgentChatStreamRequest 扩展）——
  "enabledToolNames": ["aiagent.read_file", "memory.search"],  // 🔧 已选工具（空=全部默认工具）
  "skillIds": ["code-review", "unit-test-gen"]                  // ⚡ 已选技能（注入 system prompt）
}
```

> 目录为全局状态（影响文件工具的根），维持现有 `POST /api/project/directory` 语义，不进消息级请求。

后端改动点（实现阶段评估）：
- `AgentChatRequest`/流式请求 DTO 增 `enabledToolNames`/`skillIds`（可空，向后兼容）
- `RunAgentLoopAsync` 的工具白名单按请求过滤（当前按 pluginId 白名单全挂）
- 技能注入方式：选中技能的 description/prompt 拼入 system prompt（具体拼装格式实现时定）

### 3.4 消息气泡不变项

- 消息流、markdown 渲染、工具卡片折叠、流式逐字——全部保留
- 用户气泡上方可选展示本条消息携带的上下文 chips（只读，回溯「当时发了什么上下文」）——**V1 可不做**，记为增强项

### 3.5 左栏调整（方案 A 下）

| 分组 | 现状 | 重构后 |
|------|------|--------|
| 项目目录 | 输入 + 文件浏览 | 文件浏览保留；「选择目录」入口移至 composer；选中目录后左栏自动展开文件树 |
| MCP 工具 | 平铺 + 伪选中高亮 | 只读展示（名称+描述+分组），去伪交互；说明文案「在输入框下方配置本会话工具」 |
| 技能 | 静态展示 | 保留展示（含来源标签 agents/commands） |
| 提示指令 | 空占位 | 不变（后端缺口，如实标注） |
| 记忆 | 面板展示 | 保留 |

### 3.6 视觉

- 主题沿用 `workshop-forge.css` tokens（琥珀主色 `--el-color-primary: #F59E0B`）
- 插件前端不使用 EP 组件（预编译产物约束），全部原生 HTML/CSS + `--el-*` 变量（带兜底）——与现状一致
- 原型 html 内联同一套 tokens，保证视觉预览与最终实现一致

---

## 4. 交互细节清单（原型覆盖）

1. composer 聚焦描边主色；textarea 自动增高（rows 2→6）
2. 未选目录时 📁 目录 chip 显示「未选择目录」提示态
3. popover 内搜索实时过滤
4. chips 移除即时生效
5. 模型下拉与顶栏旧选择器二选一（顶栏移除模型选择，仅留状态灯 + token 计数）
6. 发送中：发送按钮 → 停止按钮（■）；Enter 不再发送
7. 发送后 chips 清空（工具/技能选择随消息发出）；目录与模型、Agent 保持会话级持久（localStorage，与现状一致）
8. 左右栏折叠按钮（V1 可缓做，原型先展示全展开态 + 折叠按钮示意）

## 5. 实施范围预估（确认后执行）

| 层 | 文件 | 改动 |
|----|------|------|
| 前端 | `ChatPanel.vue` | 重写输入区为 composer（chips + 操作栏 + popovers）；移除顶栏模型选择 |
| 前端 | `ContextPanel.vue` | 去伪交互；目录入口迁移说明 |
| 前端 | `AiAgentView.vue` | 状态上移（选中工具/技能集合）；请求体组装 |
| 前端 | `types.ts`/`http.ts` | 请求类型扩展 |
| 后端 | `AgentChatController.cs`/`Services` | 请求 DTO + 工具过滤 + 技能注入 |
| 测试 | 插件 e2e | ai-agent.spec.ts 更新选择器；新增 composer 交互用例 |

走 `plugin-development` §四 维护闭环四步（门禁 → 插件 e2e → 发布 51888 → 浏览器走查）。注意：**若涉及工具类后端改动，热重载无效，须冷重启宿主**（ToolRegistry 残留旧实例，见 MEMORY 雷区）。

## 6. 方案定稿（2026-09-09，用户授权「按行业主流 + 项目实际 + 远景推进」）

| 决策项 | 定稿 | 依据 |
|--------|------|------|
| **布局** | 方案 A（保留三栏工作台 + composer 承载选择） | 行业主流（Cursor/Cline 保留左树+中聊天）；项目实际（FileEditor 文件编辑、SessionPanel 统计、记忆/技能展示均为工作台面板能力，方案 B 单栏抽屉会丢弃）；远景（铸己匣 = 工作台定位） |
| **工具语义** | 未勾选 = 默认全挂（13 个，**向后兼容**现有行为）；勾选 = 仅启用所选 | 最小破坏；符合 Cline 渐进控制；不勾选时行为与重构前完全一致 |
| **技能注入 V1** | 选中技能的 **名称 + 描述 + 相对路径** 注入 system prompt，注明「如需按技能工作请读取其内容」；**不全文注入** | 本地小模型（gemma-4-e4b）prompt 预算有限（14 工具即 400 过）；全文注入留 V2（需项目根 + 预算管理） |
| **气泡上下文回溯**（§3.4） | V1 **不做**，记 decision-not-to-do | 低 ROI；触发条件 = 用户反馈需要回溯「当时发了什么上下文」 |
| **模型选择位置** | 从顶栏移入 composer 操作栏右侧（发送旁） | Claude/Cursor 模式：发送前显式可见成本/档位 |
| **左右栏折叠** | V1 加折叠按钮（折叠后单栏接近主流对话体验） | 低成本可用性增强，与原型一致 |

**实施范围**（§5 裁剪为 V1 最小集）：
- 后端：`ChatRequest` 增 `enabledToolNames`/`skillIds`（可空向后兼容）；`RunAgentLoopAsync` 加可选参数；工具白名单按请求过滤；技能名称+描述+路径注入 system prompt；`AIChatController` 两端点传参
- 前端：`ChatPanel.vue` 输入区重构为 composer（chips + 操作栏 + popovers + 模型下拉）；`ContextPanel.vue` MCP 工具去伪交互（只读）；`AiAgentView.vue` 状态上移 + 请求组装；`http.ts` 请求体扩展
- 测试：`ai-agent.spec.ts` 更新选择器 + 新增 composer 交互用例
- 发布：后端工具类改动热重载无效 → **全量发布 + 冷重启 51888**；前端随发布生效
