# Specification

> 阶段：Stage 2｜Task ID：2026-10-10-aiagent-tools-features-fix

## Acceptance Criteria（AC，机械可校验）
- AC1：`AIAgentPlugin.RegisterToolFunctionExtensions` 注册 `UniversalTool`、`RunTerminalCommandTool`（pluginId=ai-agent）。
- AC2：composer 工具列表展示宿主 ToolRegistry 全量（剔除 submit_plan/complete_step/request_help），按 pluginId 分组徽标；不再按 pluginId 白名单过滤。
- AC3：工具挂载语义不变——未勾选=默认作用域（ai-agent+memory-system+design-system）；显式勾选（或 Agent.ToolAllowlist 非空）=候选池放开到全注册表再按名过滤（新增 `ResolveSelectableToolDefinitions`）。
- AC4：Agent 编辑「工具」字段改为 ElSelect 多选（filterable/collapse-tags），候选来自 `/api/ai-agent/chat/tools`，选项=工具名+pluginId 徽标+描述；留空=默认作用域。
- AC5：`WorkflowEnginePlugin.Apply` 将 IWorkflowService/IWorkflowExecutor/IWorkflowScheduler 实例 `ctx.Register` 进共享表；AIAgent 工作流工具解析失败时返回可读错误 JSON（不再抛裸异常）。
- AC6：「所有功能」页排序复用首页口径：新增 `rankFeatures`/`featureUsageKey`（homeEntries.ts），卡片右上角钉住按钮，钉住后置顶并进顶栏。
- AC7：TopNavbar 渲染钉住常驻标签（首页之后、动态标签之前，去重、不可 × 关闭、图钉可取消）；右区「所有功能」左侧加「插件管理」按钮（→/plugins）。

## Input / Output
- 输入：现有 Agent/工具/使用度数据；输出见 FR 各项（UI 变更 + API 行为不变仅语义放宽）。

## Business Rules
- 默认挂载作用域常量 `ToolScopePluginIds` 保持 3 项不变（回归测试 AIAgentToolScopeTests 仍须绿）。
- 钉住数据单一来源：usageStats store（localStorage `forge-home-usage-v2`），首页/所有功能页/顶栏共用。

## Boundary Conditions
- 无页面功能（path=null）可被钉住（键=id）但不进顶栏标签。
- 工具列表 112 项：popover 内搜索框过滤（name/description/pluginId）。

## Error Handling
- 工作流服务缺失 → `{success:false, error:"工作流服务不可用：workflow-engine 插件未启用…"}`（不再 500 裸异常文案）。
- 工具/工作流候选接口失败 → 静默降级空列表（不阻断编辑/聊天）。

## Compatibility
- API 契约不变；`enabledToolNames` 空数组行为与旧版一致。
- 钉住键与首页 buildHomeEntries key（path）一致，老数据直接兼容。

## Non-functional Requirements
- 0 自定义 token；Tailwind 布局原语；产物仅 index.js+style.css。

## Interaction Design（必写节）
- composer 🔧 工具 popover：新增插件徽标 + 底注「共 N 个（全插件）· 未勾选 = 启用默认作用域；勾选 = 仅启用所选」。
- Agent 编辑：工具行=多选下拉（占满整行）+ 下说明行；下拉项=名称+徽标+右侧省略描述（title 兜底）。
- 所有功能卡片：右上角图钉按钮（hover 浮现、钉住常显填充）；status-dot 右移让位。
- 顶栏：钉住标签 hover 显示图钉（点击=取消钉住）；其余标签行为不变；右区新增 Box 图标按钮（title=插件管理）。
