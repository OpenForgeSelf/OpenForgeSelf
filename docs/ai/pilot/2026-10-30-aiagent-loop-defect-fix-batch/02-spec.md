# Spec

> 阶段：Stage 2 ｜ Task ID：PILOT-077 ｜ 日期：2026-10-30
> 交互设计审查工具：ui-ux-design（CRAP）。设计基准：DESIGN.md token 体系 + 参考形态（仅工具调用带框）。

## Functional Requirements

### FR-1（D1）goal 事件进 SSE payload 白名单
- `AgentRunsController.WriteSseEventAsync` 对 `goal_round_started`、`goal_evaluated` 输出 `{ type, payload = ev.Content }`，与 `run_stuck` 同构；payload 结构沿用 `AgentRunModels.cs:223/238`，不改字段。
- 其余五类既有事件输出字节面不变（守卫断言）。

### FR-2（D2）步骤工具轨迹回填 + 评估证据可见
- `RunOrchestratorService.ExecuteStepsCoreAsync`：`ToolStarted` 建轨迹条目（name/args），`ToolCompleted` 回填 result/success/duration；步骤收束前写入 `result.ToolCalls`（`MarkStepCompleted/MarkStepStuck` 既有序列化不改）。出口工具（complete_step/request_help）不入轨迹（与 SSE 转译同一判定 `IsStepExitTool`）。
- `GoalPursuitService.EvaluateAsync`：评估提示词的步骤清单附每步工具轨迹摘要（工具名/成败/关键返回，单条截断 ≤200 字符防爆量）；判定语义（BR-4）不变，只补证据输入。

### FR-3（D3）I/O 代码块主题化
- `.aiw-tool__pre` 背景改 `var(--aiw-bg-soft)`、文字改 `var(--aiw-text)`、加 `1px solid var(--aiw-line)` 边框；删除硬编码 `#0f172a/#cbd5e1`。亮色 = 浅灰面深字（WCAG ≥ AA），暗色 = 深面浅字（token 自动适配，独立与内联模式皆然）。禁止新增裸色值（design:check 棘轮）。

### FR-4（D4）步骤时间线不压扁
- `.rrp__steps` 直接子项（`.rrp__step`）补 `flex-shrink: 0` → 步骤保持自然高度，容器 `overflow-y:auto` 正常滚动。
- 同类修（canonical fix 同批）：`.aiw-ask` 子项 `.aiw-ask__item` 补 `flex-shrink:0`；`.rrp__plan-body` 补 `overflow-y:auto`（现 max-height 无 overflow 会叠画）。

### FR-5（D5）工具块默认展开（守卫锁行为）
- 新增 vitest：`type:'tool'` 块缺省挂载 → `.aiw-tool__head[aria-expanded="true"]` 且 I/O 体可见；`collapsed:true` 时仍可收起（既有行为不回归）。
- 不改运行时代码（当前默认即展开，dev 四时点实测 Verified）。

### FR-6（D6）消息列统一左对齐线
- 对齐基准 = 正文文本左缘（实测 x=286：气泡 padding 12px 线，与行首图标槽同线，2026-10-26 用户拍板的既有对齐线）。
- `.aiw-tool__body` 横向内边距归零（`padding: 8px 0 9px`），I/O `pre` 全宽（`margin:0`）→ 深色块左缘 = 卡片边（≈286-287）与文本同线；`输入/输出` 小标签与 `pre` 同线（≤±2px）。思考正文缩进（42px，对齐思考行标签）为既有拍板设计，不动。

## Interaction Design（交互规格）

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 工具块折叠头 | 点击 | 展开/收起 I/O 卡片 | open 默认 true；用户点击后本块记忆翻转 | 箭头同位叠放旋转（既有） | 无 I/O 时显示「这次调用没有留下可展开的输入或输出」（既有） | 长 I/O 块内滚动（max-height 170 既有） |
| 步骤时间线 | 滚动 | 全部步骤自然高度、容器滚动 | 每步详情（目标/出参）默认展开（既有） | 步骤状态色/当前步高亮（既有） | 无步骤时走既有空态 | 9+ 步可滚动，无裁切残行 |
| 目标评估事件行 | Run 流式推送 | 事件行「目标评估 第 N/M 轮 + 结论」实时追加 | 每轮一条，不中断流 | 事件行内联（既有样式） | 无 | 3 轮×N 步事件流不断（修后复测） |

**五态核对（插件页面级）**：本批不改页面骨架，加载/空/出错/无权限态维持现状；走查时按清单复核不回归。

**CRAP 自查**：对齐——正文/图标槽/I/O 块统一 286 线（FR-6）；对比——I/O 块浅灰面+边框与气泡面分层（FR-3）；重复——同一线贯穿消息列与时间线；亲密性——未触碰分组逻辑。

## Failure Modes（PC lens）

| 来源 | 预防 |
| --- | --- |
| PC-018 镜像（证据未随行落定） | FR-2 守卫断言「真实调过工具的步骤 ToolCallsJson 非空」，防「成功但轨迹空」反向复发 |
| 铁律 8 / PC-019（新档） | FR-4 canonical fix + 冒烟 e2e 视觉断言（步骤列表滚动无裁切） |
| 事件契约漂移（SSE 新事件漏映射） | FR-1 守卫断言逐一覆盖全部当前事件类型的映射形态；新增事件类时测试表会红 |
| 评估提示词加长 | 轨迹摘要逐条截断 ≤200 字符；评估输入总量走查观察 token 增幅 |

## Business Rules

- BR-4（评估判定语义）不变：只补证据输入，不改「判据匹配→达成」的判定逻辑。
- 工具块默认展开为产品行为基线（2026-10-30 实测确认），由测试锁定；用户显式收起后尊重用户操作。

## Boundary Conditions

- 9 步 + 长目标文本（Run #2 真实数据）为时间线压扁复现样本，修后必须全展开。
- 无工具调用的纯文本步骤：轨迹为空数组 → `ToolCallsJson = "[]"` 合法，评估提示词显示「（本步无工具调用）」。
- 深色模式与独立悬浮模式（非 inline）下 FR-3 同样成立（token 两源均有定义）。

## Error Handling

- 评估器输入组装遇 `ToolCallsJson` 反序列化失败：按无轨迹处理（不抛异常中断评估），日志 warn。
- SSE 映射未知事件仍走 default（向前兼容新事件不崩前端：前端 http.ts 对 goal 事件缺 payload 时本批不加防御性改动——保持最小面，前端容错属可选加固，闸门1 时拍板）。

## Compatibility

- 旧 Run（九步 `toolCallsJson="[]"`）详情展示不变；新 Run 起轨迹可见。
- 非目标模式（普通计划 Run）走同一 `ExecuteStepsCoreAsync`，轨迹回填同样生效（增强，非破坏）。
- 聊天历史会话（含本批测试产生的会话）渲染不受。

## Non-functional Requirements

- 亮暗主题下 I/O 块文字对比度 ≥ WCAG AA；新增样式零裸色值（design:check 棘轮不涨）。
- 动效零新增（遵守 DESIGN.md 动效红线）。

## Acceptance Criteria

1. AC-1：goal 两事件 SSE 输出含 `payload` 字段且 JSON 可解析（后端测试）。
2. AC-2：既有五类事件映射形态逐字节不变（后端测试）。
3. AC-3：含工具调用的步骤 `ToolCallsJson` 反序列化后非空且含 name/success（后端测试）。
4. AC-4：评估提示词包含步骤工具轨迹摘要（后端测试断言提示词组装）。
5. AC-5：dev 目标 Run 复测（同型任务）：事件行三轮可见无「连接失败」；达成 → `Completed`。
6. AC-6：亮色模式截图：I/O 块浅色面；文本与 I/O 块左缘同线（±2px，读图核对）。
7. AC-7：执行记录 Run #2 复测：步骤 1-9 全部自然高度，无残行，容器可滚动（读图 + e2e 断言）。
8. AC-8：`pnpm run build`（插件 web）+ vitest 新增默认展开断言绿；`design:check` 棘轮不涨。
9. AC-9：冒烟 e2e `e2e/plugins/ai-agent` 一条主链路（发消息 → 工具块且默认展开 执行记录步骤不压）绿。
