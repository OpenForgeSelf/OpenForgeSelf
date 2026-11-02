# 05 Evidence — goal-sse-evidence-fix（D1/D2 并入 PILOT-077 后的补录）

> 本任务已于 2026-10-30 21:2x 并入全量批次 `docs/ai/pilot/2026-10-30-aiagent-loop-defect-fix-batch/`（PILOT-077），
> 本文缺陷 A/B = 该批 D1/D2。本文件为闸门3 工件链补录：**全部证据为 PILOT-077 05-evidence 已记录的真实执行结果**，
> 此处只做交叉引用与对账，不新增执行、不重复承载证据本体。

## 1 缺陷-证据对账（来源 = PILOT-077 05-evidence §1/§2/§3）

| 本文缺陷 | PILOT-077 对应 | 证据 | 等级 |
| --- | --- | --- | --- |
| 缺陷 A：goal_round_started/goal_evaluated 落 default 分支 → 前端 `p.round` TypeError → SSE「连接失败」 | D1 | 新守卫红 3/9 → 绿 9/9；SSE 封套单测 ×2 + 既有五类字节面守卫；真机 Run 3/4 事件行渲染「目标评估 第 1/3 轮」 | Verified |
| 缺陷 B：帧循环从不填 `result.ToolCalls` → 轨迹恒 `[]` → 评估器零证据误判 Stuck | D2 | 轨迹/评估证据单测；真机 Run 4 step0 `list_files` 轨迹落库铁证（修复前恒 `[]`，修复后 `{"Name":"list_files",…,"Success":true}`） | Verified |

## 2 门禁跑批（均以 PILOT-077 日志为准 · Verified）

- `dotnet test --filter "FullyQualifiedName~GoalRunSseEvidence"`：红 3/9 → 绿 9/9（`.temp/pilot077-red3.log` / `pilot077-green1.log`）。
- AIAgent 过滤集 120 通过 / 15 失败全部为已归档基线红（`.temp/pilot077-aiagent-full.log`），零新增红。
- 冒烟 e2e `e2e/plugins/ai-agent/ai-agent.spec.ts` 7/7（`.temp/pilot077-e2e4.log`）。

## 3 证据文件索引

见 PILOT-077 05-evidence §5：`.temp/pilot077-*.log` 全套、截图 `ForgeSelf.Web/screenshots/dev/ai-agent/pilot077-*.png`、`.temp/dev-stack-backend.log` Run 3/4 段。

## Unknown

- goal 模式 achieved→Completed 的真机 happy-path 受「45s 步骤超时 × 慢模型」约束未现场取得——由单测确定性覆盖（PILOT-077 05 §3 原文）；属修复前既有的产品参数问题（TODO3 在案），非本修复缺口。
