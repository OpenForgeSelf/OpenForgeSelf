# Intent

> 阶段：Stage 1 ｜ Task ID：PILOT-077 ｜ 日期：2026-10-30
> 来源：输入3（aiagent 插件自主任务实测，2 个 P1 后端缺陷）+ 输入4（用户看截图指出 4 个 UI 缺陷）。合并为一个修复批，一次过闸门1。

## Problem

六个已实证缺陷（详证据见 00 与日记 输入3/输入4）：
1. **D1（P1·后端）** 目标模式 SSE `goal_round_started/goal_evaluated` 未进 payload 白名单 → 前端读 `obj.payload` 为 undefined → `p.round` TypeError → 执行记录面板「连接失败」+ 流中断（AC-UI2 不通过）。
2. **D2（P1·后端）** 步骤工具轨迹从不回填 `StepRunResult.ToolCalls` → 落库恒 `[]`，且评估器输入不含轨迹 → 文件已创建仍判「缺工具执行证据」→ 误置 Stuck、纠正预算空转。
3. **D3（P1·UI）** 聊天工具调用 I/O 代码块硬编码黑底（`#0f172a`），亮色模式下突兀刺眼。
4. **D4（P1·UI）** 执行记录步骤时间线被 flex 压扁，详情裁出残行（用户看到「步骤 3/4 之间混入其他内容」）。
5. **D5（P2·UI）** 用户反馈「聊天内工具块默认收起」；dev v1.9.8 四时点实测均默认展开（不复现）→ 需守卫锁行为 + 向用户求证具体界面。
6. **D6（P2·UI）** 工具调用 I/O 深色块左缘（297）与正文文本左缘（286）不齐，文本显得「太靠左」——同列消息缺一条对齐线。

## Why

- D1/D2 使 v1.9.7 目标模式（PILOT-051）的核心验收（AC-UI2、终态正确性）失效，评估终局可信度被破坏。
- D3/D4/D6 是用户直接指出的可见缺陷，破坏消息列与时间线的可读性（CRAP 对齐/对比原则）。
- D5 需以守卫测试固化当前正确行为，防止未来回归成用户所述形态。

## Expected Outcome

- 目标 Run：SSE 事件行实时显示「目标评估 第 N/M 轮」不崩；真实调过工具的步骤 `toolCallsJson` 非空；证据充分的目标 Run 达成 → `Completed`（非误报 Stuck）。
- 聊天区：亮色下 I/O 块为浅色主题面；工具块默认展开有测试守卫；消息列文本与工具 I/O 块共享同一条左对齐线（±2px）。
- 执行记录：步骤时间线完整展开、容器滚动，无压扁残行；规划过程体不叠画。

## Constraints（规范 §1 对照）

- 不碰生产 `:51888`、不停/启宿主；不改数据库结构；不做无关重构；不新增依赖。
- 聊天链路（场景A 已验证通过）行为不回归；既有五类 SSE 事件字节面不回归。
- 插件任务六步闭环（plugin-development §四）：门禁 + 冒烟 e2e + dev 走查 + 发布（须用户同意）+ 运行实例复验（更新后）+ 文档。

## Success Criteria

1. `dotnet test --filter FullyQualifiedName~AIAgent` 全绿（含新增守卫：goal 两事件 SSE 映射断言、步骤 ToolCalls 非空断言、既有五类事件不回归断言）。
2. `cd Plugins/AIAgent/web && pnpm run build` 绿；vitest 增「工具块默认展开」断言绿。
3. `e2e/plugins/ai-agent/ai-agent.spec.ts` 冒烟主链路绿（含步骤时间线不压扁视觉断言）。
4. dev-stack 走查（ui-ux-design 清单）：亮色 I/O 块浅色、左对齐一线、步骤 3/4 无残行、目标 Run 达成 → Completed，截图落档。
