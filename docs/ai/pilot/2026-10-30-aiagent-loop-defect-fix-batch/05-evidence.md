# 05 Evidence — PILOT-077 AIAgent 目标循环缺陷修复批

> 只记实际发生（Verified = 亲自跑过拿到真实输出；Inferred = 依据代码/日志推断；Unknown = 未验证）。
> 生成时间：2026-10-31 10:40（Asia/Shanghai）。

## 1 门禁跑批记录（快档：插件层批次）

| 门禁 | 命令 | 结果 | 来源 |
| --- | --- | --- | --- |
| 新守卫测试（TDD） | `dotnet test --filter "FullyQualifiedName~GoalRunSseEvidence"` | 红 3/9 → 绿 9/9（`ForgeSelf.Api.Tests`） | Verified（.temp/pilot077-red3.log、pilot077-green1.log） |
| AIAgent 回归过滤集 | `dotnet test --filter "FullyQualifiedName~AIAgent"` | 120 通过 / 15 失败 = **全部为已归档基线红**（ProjectWorkspaceDefaultRootTests，68df669 遗留，TODO 在案）→ 零新增红 | Verified（.temp/pilot077-aiagent-full.log） |
| 插件前端构建 | `cd Plugins/AIAgent/web && pnpm run build` | ✓ 98 modules，1.11s | Verified（.temp/pilot077-pluginweb-build.log） |
| 组件守卫测试 | `cd ForgeSelf.Web && pnpm run test -- --run BlockRenderer` | 22/22（含 D5 默认展开守卫 + collapsed 尊重断言） | Verified（.temp/pilot077-blockrenderer-test2.log） |
| 设计守卫 | `pnpm run design:check` | 双 PASS：硬编码色 1759 vs 基线 2501（减 742，D3 去 #0f172a/#cbd5e1 计入减少）；token 族基线持平 | Verified（.temp/pilot077-designcheck.log） |
| 冒烟 e2e | `pnpm exec playwright test e2e/plugins/ai-agent/ai-agent.spec.ts`（NO_PROXY + EnableDefaultCompressionFormats=false） | **7/7 passed**（1.2m）；版本徽标 v1.9.9 生效 | Verified（.temp/pilot077-e2e4.log） |
| 宿主构建（runs 路径真机） | dev-stack 起源码前后端（:7301/:7399） | healthy + 页面 200 | Verified（.temp/dev-stack.json、health 探测） |

未跑（快档不要求，已按 §5.6 说明）：宿主前端 `pnpm run check`（`ForgeSelf.Web/src` 本批零改动）、后端全量 dotnet test（未触宿主源码/仓库脚本/共享测试基建）、全量 e2e（未触 globalSetup/playwright 配置）。

## 2 验收标准逐条对账（对 02-spec）

| AC | 内容 | 证据 | 等级 |
| --- | --- | --- | --- |
| AC1 | goal_round_started/goal_evaluated 走 `{type,payload}` 封套 | 单测 ×2；真机：Run 3/4 运行事件行渲染「目标评估 第 1/3 轮」（此前该事件致前端 TypeError→连接失败） Verified |
| AC2 | 既有五类事件字节面不回归 | 单测 ×5（plan_created/step_started/step_completed/run_stuck/done）+ 专型事件封套守卫 ×1 | Verified |
| AC3 | 真实工具步骤 ToolCallsJson 非空（名/参/成败） | 单测（write_file 轨迹断言 + 出口工具不入轨迹）；**真机**：Run 4 step0 `toolCalls=[{"Name":"list_files","ArgumentsSummary":"…","ResultSummary":"成功 · 20ms","Success":true}]`（修复前恒为 `[]`）；Run 3 步骤内无工具执行 → 空数组=正确行为 | Verified |
| AC4 | 评估器提示词携带步骤产出 + 工具轨迹 | 单测断言 eval prompt 含产出文本/「工具轨迹」节/工具名/参数摘要；提示词渲染含截断（轨迹 200/产出 400） | Verified |
| AC5 | 色模式 I/O 块主题 | 实测 getComputedStyle：`.aiw-tool__pre` bg=rgb(245,247,250)、color=rgb(48,49,51)、border=rgb(220,223,230)（旧值 #0f172a 黑底消除）；截图 `ForgeSelf.Web/screenshots/dev/ai-agent/pilot077-light-tool-blocks.png` | Verified |
| AC6 | 工具卡内容与正文同线对齐 | 实测：行首/卡片外缘 x=274，正文文本线 x=286（气泡 274+12px 内边距，2026-10-26 左列对齐教义），I/O 块 x=286-287（body padding 0 + 1px 边框） | Verified |
| AC7 | 工具块默认展开 | 真机 11/11 块 aria-expanded=true；e2e 条件锁 + BlockRenderer vitest 无条件守卫（22/22） | Verified |
| AC8 | 显式收起仍受尊重 | vitest：collapsed:true → aria-expanded=false + body display:none | Verified |
| AC9 | 步骤时间线不压缩 | 真机：`.rrp__step` flexShrink=0、clipped=0、步骤卡全高渲染（截图 `pilot077-run-detail-steps.png`）；e2e 几何锁（flexShrink==='0' 且 clipped≤4px） | Verified |
| AC10（规划回放 plan-body 不叠画） | `.rrp__plan-body` overflow-y:auto | 真机 planOverflow 断言在 e2e 有模型分支内（本环境无模型未触发）+ 代码修入 | Inferred（样式修复 Verified，e2e 断言分支未触发） |

## 3 现场走查记录（dev-stack，真实模型 qwen3_8_vision）

- 聊天链路：任务「创建 walk-note.txt…」→ 工具块逐一出现且全部默认展开；write_file 被项目根沙箱**正确拒绝**（D:\agent-test 在 PROJECT_ROOT 外，守卫按设计工作）；随后聊天流触发既有 45s 响应看门狗（慢模型既有行为，非本批缺陷）。
- 目标 Run ×2（Run 3 / Run 4）：计划生成 → 目标评估第 1/3 轮（goal 事件渲染正常）→ 步骤内模型续跑请求耗时 45-70s（gpustack 8B 当前负载下带工具结果的请求显著变慢；对照：昨天同流程 3 轮评估跑通）→ 45s 步骤超时 → 卡住（人工介入按钮组正常）。Run 4 内 list_files 真实执行成功并落轨迹（AC3 铁证）。
- **未在现场取得**：goal 模式 achieved→Completed 的真机 happy-path。原因 = 上述模型速度环境约束（步骤 45s 超时先于评估轮触发）。该路径由单测确定性覆盖（脚本化 write_file→complete_step→评估 achieved=true→done 事件，9/9）。
- stuck 恢复语义新发现：对卡住 Run 点「继续」后若再次卡住，新一轮 StepRunResult **覆写**前轮 ToolCallsJson（Run 4 恢复后 trace 回到 "[]"）。属修复前即存在的覆写语义（此前恒空无从丢失），本批不扩范围 → TODO3。

## 4 变清单（插件 1.9.8 → 1.9.9）

| 文件 | 变更 |
| --- | --- |
| `Plugins/AIAgent/Controllers/AgentRunsController.cs` | payload 组 +2 case（goal_round_started/goal_evaluated）；WriteSseEventAsync private→internal（测试可见性） |
| `Plugins/AIAgent/Services/RunOrchestratorService.cs` | 帧循环聚合工具轨迹（ToolStarted 建 trace / ToolCompleted 回填成败+耗时 / 出口工具排除 / 摘要截断 200）；CollectStepEvidence（产出 400 + 轨迹反序列化 fail-safe）；EvaluateAsync 传证据 |
| `Plugins/AIAgent/MafLoop/GoalPursuitService.cs` | 新 StepRunEvidence record；EvaluateAsync 签名 +证据参数；评估提示词渲染每步产出与工具轨迹节；评估系统提示同步；轨迹渲染 fail-safe |
| `Plugins/AIAgent/web/src/agent-core/styles/blocks.css` | D3：`.aiw-tool__pre` token 化（bg-soft/text/line）；D6：`.aiw-tool__body` 横向 padding 归零 |
| `Plugins/AIAgent/web/src/components/RunRecordPanel.vue` | D4：`.rrp__step` flex-shrink:0；`.rrp__plan-body` overflow-y:auto |
| `Plugins/AIAgent/web/src/agent-core/styles/widget.css` | D4 同类：`.aiw-ask__item` flex-shrink:0 |
| `/AIAgent/web/src/agent-core/components/blocks/BlockRenderer.test.ts` | D5 守卫（默认展开 + 显式收起） |
| `ForgeSelf.Api.Tests/Plugins/AIAgent/GoalRunSseEvidenceTests.cs` | 新建 9 用例（SSE 封套 ×2 组 + 轨迹/评估证据） |
| `ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts` | 029 用例：PC-013 无模型分叉（锁 fail-fast 契约）+ AC7 条件锁 + AC9 几何锁 |
| `Plugins/AIAgent/plugin.json` | Version 1.9.9 + Description 修复批说明（csproj 版本行未动） |
| `docs/11-troubleshooting/` | PC-019 建档 + case 2026-10-31-aiagent-flex-shrink-compression + README 台账/检索行 |

## 5 证据文件索引

- `.temp/pilot077-red3.log` / `green1.log` / `aiagent-full.log` / `pluginweb-build.log` / `blockrenderer-test2.log` / `designcheck.log` / `e2e4.log`（门禁日志）
- `ForgeSelf.Web/screenshots/dev/ai-agent/pilot077-light-tool-blocks.png`（AC5/6/7 现场截图）
- `ForgeSelf.Web/screenshots/dev/ai-agent/pilot077-run-detail-steps.png`（AC9 现场截图 + goal 事件行）
- `.temp/dev-stack-backend.log`（Run 3/4 工具执行与轨迹时序）
