# 00 Repository Understanding

> Task ID：PILOT-077 ｜ 日期：2026-10-30 ｜ 批次：aiagent-loop-defect-fix-batch
> 范围：Plugins/AIAgent（插件前端 agent-core/组件 + 插件后端 Controllers/Services）+ 守卫测试。宿主零触及。

## 1. 涉及区域现状（本会话逐文件核实，均带实测/行号）

### 1.1 插件前端（Plugins/AIAgent/web/）
- `src/agent-core/styles/blocks.css`
  - `:156-161` `.aiw-tool__pre` **硬编码** `background:#0f172a; color:#cbd5e1`（不随主题；实测亮色 `data-theme="light"` 下仍为黑底白字）。
  - `:140-149` `.aiw-tool__body` 卡片 `margin: 2px var(--aiw-space-5)` + `padding: 8px 10px 9px` + 1px 边框 → 内部 I/O 深色块左缘比正文文本右移 11px（实测：文本 x=286 / 卡片盒 286 / I/O 块 297）。
  - 主题 token 真源：`widget.css:21-26`（独立模式 `--aiw-bg-soft:#f6f7fb`）+ `:1315-1324`（内联模式映射宿主 `--el-*`）+ `:1384-1394`（`.aiw-dark` 反相）。
- `src/agent-core/styles/widget.css`
  - `:633-648` `.aiw-ask`（待你动作浮层）：`display:flex; flex-direction:column; max-height:46vh; overflow-y:auto`，子项 `.aiw-ask__item` **缺 `flex-shrink:0`** → 审批卡多时会被压扁裁切（潜伏，同类）。
  - `:254-261` `.aiw-body`（聊天消息滚动区）为普通块级滚动容器（非 flex 列）→ 不受此类缺陷影响，已排除。
- `src/components/RunRecordPanel.vue`
  - `:989-997` `.rrp__steps`：`flex:1; overflow-y:auto; display:flex; flex-direction:column`；`:999-1005` `.rrp__step` 含 `overflow:hidden` 但**缺 `flex-shrink:0`** → 步骤总高超出容器时被 flex 压扁，详情区（目标/出参，实测单步内容高 195px 被压进 43px）被裁出残行 = 用户看到的「步骤 3/4 之间杂内容」（蓝字 = `.rrp__label` `目标` 标签色 `rgb(64,158,255)`）。
  - `:957-961` `.rrp__plan-body`：`max-height:200px` **无 overflow** → 规划过程条目超高时溢出叠画到后续内容（潜伏，同类）。
- `src/agent-core/components/blocks/BlockRenderer.vue`
  - `:21` `open = ref(props.block.open !== false && !props.block.collapsed)` → **工具/思考/分组块默认展开**；`utils/schema.js:218` group 块默认 `collapsed=false`。全仓无任何代码路径把工具块置为收起。
  - **实测四时点**（dev v1.9.8，21:13-21:19）：直播流式 2s/6s/10s 三采样 + 完成后 + 刷新后（15 块）`aria-expanded` 全为 `"true"` → 「默认收起」**在当前源码不复现**。
- 插件前端门禁：`cd Plugins/AIAgent/web && pnpm run build`（+ check/test 命令同宿主约定）。

### 1.2 插件后端（Plugins/AIAgent/）
- `Controllers/AgentRunsController.cs:309-336` `WriteSseEventAsync`：payload 白名单仅 `plan_created/step_started/step_completed/run_stuck/done` 五类；`goal_round_started/goal_evaluated`（v1.9.7 新增）落 default 分支输出 `{type, content}`，而前端 `http.ts:512` 统一读 `obj.payload` → `RunRecordPanel.vue:316` `p.round` 抛 TypeError → SSE 断流「连接失败」。
- `Services/RunOrchestratorService.cs`
  - `:653-709` `ExecuteStepsCoreAsync` 帧循环：`ToolStarted/ToolCompleted` 仅转发 SSE（`callNames` 只服务 SSE 转译），**从不填充 `result.ToolCalls`**；`:824-834/:836-844` 序列化恒得 `"[]"`（Run #2 九步实测全 `[]`，而后端日志证明 write_file/read_file 真实成功）。
  - `:44` `StepRunResult.ToolCalls`（`List<StepToolCallTrace>`）= 回填点。
- `MafLoop/GoalPursuitService.cs`：评估器为无工具 LLM 调用，输入仅「任务原文 + 步骤产出摘要 + 判据」，**不含工具轨迹** → 文件已创建仍三轮判「缺工具执行证据」→ 误置 Stuck。

### 1.3 测试与验证方式（真实仓库确认）
- 后端：`ForgeSelf.Api.Tests`（xUnit；AIAgent 过滤集 `--filter FullyQualifiedName~AIAgent`）。
- 前端：插件 `web/` vitest（`BlockRenderer.test.ts` / `widget-contract.test.ts` 已有块契约测试；**无**「工具块默认展开」显式断言）。
- e2e：`e2e/plugins/ai-agent` **不存在**（PILOT-051 遗留项②，TODO 在案）→ 本批补 1 条冒烟主链路。
- dev-stack：`pwsh scripts/dev-stack.ps1`（:7301/:7399，token 见 `.temp/dev-stack.json`；`NO_PROXY=localhost,127.0.0.1,::1` 必设）。

## 2. 适用问题类（problem-classes/README.md 检索）

| 命中 | 结论 |
| --- | --- |
| PC-018（success 但副作用未落定） | **lens**：D2 为其镜像（副作用已落定、证据未随行）——评估证据链修复需保证「轨迹随行落库」有断言，防反向复发 |
| 铁律 8（flex 滚动容器子项缺 `flex-shrink:0`，plugin-development 技能 Home 实测坑） | **同类复发**（第 2 实例：Home → AIAgent `.rrp__steps`；新增第 3 处潜伏 `.aiw-ask`）→ 本批按 canonical fix 修同类 + 建档 **PC-019（新）** |
| 无既有 PC 覆盖「SSE 新事件未进 payload 白名单」 | 随 D1 修复建档为 PC-019 同批候选（事件契约漂移类），复现测试落 AIAgent 后端测试 |
