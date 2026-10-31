# Plan

> 阶段：Stage 3 ｜ Task ID：PILOT-077 ｜ 日期：2026-10-30
> 文件清单已逐行核实（本会话全部读过现内容）。偏差先记录再修正。

## Files To Change

- `Plugins/AIAgent/Controllers/AgentRunsController.cs`（D1/FR-1：WriteSseEventAsync switch 加 `goal_round_started`/`goal_evaluated` 两 case → `{ type, payload = ev.Content }`）
- `Plugins/AIAgent/Services/RunOrchestratorService.cs`（D2/FR-2：帧循环聚合 `StepToolCallTrace` 写入 `result.ToolCalls`；复用 `IsStepExitTool` 排除出口工具）
- `Plugins/AIAgent/MafLoop/GoalPursuitService.cs`（D2/FR-2：评估提示词步骤清单附工具轨迹摘要，反序列化失败按无轨迹降级）
- `Plugins/AIAgent/web/src/agent-core/styles/blocks.css`（D3/FR-3 token 化 `.aiw-tool__pre`；D6/FR-6 `.aiw-tool__body` 横向 padding 归零 + `pre` 全宽）
- `Plugins/AIAgent/web/src/components/RunRecordPanel.vue`（D4/FR-4：`.rrp__step` 补 `flex-shrink:0`；`.rrp__plan-body` 补 `overflow-y:auto`）
- `Plugins/AIAgent/web/src/agent-core/styles/widget.css`（D4/FR-4 同类修：`.aiw-ask` 子项补 `flex-shrink:0`）
- `Plugins/AIAgent/web/src/agent-core/components/blocks/BlockRenderer.test.ts`（D5/FR-5：默认展开守卫断言）
- `ForgeSelf.Api.Tests/Plugins/AIAgent/GoalRunSseEvidenceTests.cs`（新增：AC-1/2/3/4 守卫；命名随既有 AIAgent 测试文件风格微调）
- `e2e/plugins/ai-agent/ai-agent.spec.ts`（新增：冒烟主链路 + AC-7 步骤不压扁断言；补 PILOT-051 遗留②）

## Implementation Steps

1. 后端 D1：AgentRunsController 两 case（TDD：先写 AC-1/2 红灯）。
2. 后端 D2：RunOrchestratorService 轨迹聚合（AC-3 红灯先行）→ GoalPursuitService 评估输入（AC-4）。
3. 前端 D3+D6：blocks.css token 化与对齐收口。
4. 前端 D4：RunRecordPanel.vue + widget.css 两处 `flex-shrink:0` + plan-body overflow。
5. 前端 D5：BlockRenderer.test.ts 默认展开断言（不改运行时）。
6. 冒烟 e2e：`e2e/plugins/ai-agent` 主链路（聊天发消息 → 工具块默认展开 → 执行记录步骤时间线不压扁）。
7. dev-stack 走查：ui-ux-design 清单逐项 + 复现截图（AC-5/6/7）。

## Test Plan

1. 后端：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~AIAgent"`（新增 GoalRunSseEvidenceTests 4 断言组）。
2. 插件前端：`cd Plugins/AIAgent/web && pnpm run build`；宿主侧 vitest 守卫按 §5.6 快档跑相关过滤集。
3. e2e：`pnpm exec playwright test e2e/plugins/ai-agent --reporter=line`（e2e-env 动态地址，NO_PROXY 先设）。
4. 走查：dev-stack 起源码前后端 → 同型目标 Run + 聊天各一发 → 截图 3 张（聊天亮色对齐 / Run 详情时间线 / 目标评估事件行）→ 读图核对。

## Verification

### Build

```bash
cd ForgeSelf.Api && dotnet build
cd Plugins/AIAgent/web && pnpm run build
```

### Unit Test

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~AIAgent"
cd ForgeSelf.Web && pnpm run test -- --run BlockRenderer
pnpm run design:check
```

### Integration Test

```bash
# N/A——本批无独立集成测试层，后端过滤集即集成面（插件宿主装配经 dev-stack 走查覆盖）
```

### E2E

```bash
$env:NO_PROXY='localhost,127.0.0.1,::1'; pnpm exec playwright test e2e/plugins/ai-agent
```

### Other Checks

- `pnpm run check`（宿主前端未改则按快档跳过说明）+ `pnpm run design:check`（棘轮基线不涨）。
- 门禁档位：**快档**（本批仅触插件层，未碰宿主源码/仓库脚本/共享测试基建 → 不触发中/深档；冒烟 e2e 属快档必跑项）。

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-30 21:1x | D5 实测不复现（四时点均默认展开） | 预设「需改默认值」 | 改为仅加守卫测试 + 向用户求证具体界面（01/02 已同步） |
| 2026-10-30 21:1x | D4 同类扫查命中 `.aiw-ask`/`.rrp__plan-body` | 仅修 `.rrp__step` | 按铁律 8 canonical fix 同批修同类（3 处） |
| 2026-10-31 09:0x | D2 轨迹 ResultSummary 承载「成败+耗时」而非结果全文 | 轨迹含「关键返回截断」 | `ToolCompleted` 帧只带 Outcome+DurationMs（宿主 Abstractions 契约，本批禁触宿主）；结果全文随行评估留宿主侧增强（TODO P2）。步骤产出（complete_step 的 OutputJson）+ 工具名/参数摘要/成败已构成评估判据 |
| 2026-10-31 10:4x | 029 e2e「无模型也必有 Run 行」期望过期 | 沿用既有断言 | v1.9.5 PC-013 fail-fast 起，无模型不建 Run（RunOrchestratorService.cs:182-194）；e2e 改为分叉：无模型锁「报错可见+不建 Run」契约，有模型环境才走 Run 明细 + AC-7/AC-9 几何锁 |
| 2026-10-31 10:5x | e2e 宿主 publish 反复崩于 BrotliCompress（MSB6006 0x1000000A） | 直接跑 playwright | 环境级绕过：`EnableDefaultCompressionFormats=false` 环境变量（MSBuild 属性继承，零仓库改动；仅失去 .gz/.br 预压缩，服务不受影响）；已沉淀 agent-workflow B2 |
