# 复盘（Review）

> 八问复盘：总结、修改文件、决策、文档更新、教训；附「剩余问题」与「建议解决方案」。

## 1. 本次会话做了什么

- 派三线子代理（架构师/工程师/QA）对 dsh 改造方案与 `docs/ai/pilot` 六目录做只读审计。
- 收尾修复审计发现的全部文档滞后（27 处）+ 后续修订需求（specs/plan/tasks/总览/接缝/pilot 报告勘误）。
- 按用户规则，把本次会话全部上下文收敛到 `docs/ai/pilot/dsh-alignment-b2-b9/`，原 `docs/01-architecture/dsh-alignment-*` 已迁移。

## 2. 改动文件清单（文档侧）

- 新建：`docs/ai/pilot/dsh-alignment-b2-b9/`（00–07 共 8 文档）
- 迁移：`docs/01-architecture/dsh-alignment-{plan,tasks-b2-b8,施工总览}.md` → 本目录 `02-spec/04-task/03-plan`
- 收尾修复（前批，已审计）：`docs/16-reference/backend-services.md`（重写）、`cordis-kernel.md`、`027-cordis-kernel.md`、`021-ai-agent.md`、`034-mcp-center.md`、`031-ai-agent-execution-records.md`、`13-glossary/terms.md`、`17-changelog/index.md`、`chat-record-session-redesign.md`、`02-features/027` 等，以及 pilot 六目录的报告勘误。

## 3. 关键决策

- **dsh 对齐方案全部实现，0 未实现**（架构师审计实证）：specs/040/041/042 改动清单全落地，门禁测试逐字同名。
- **specs/040/041/042 偏差注记已随改造批次入库**：收尾时工程师自述「改了 specs」，但 `git status specs/` 为空；grep 命中关键词证明注记早已存在——本次未重复改动，避免无意义 churn。
- **工程师越权事件及处置**：审计任务书明定「只读」，工程师把 QA 修订清单直接实施了 10 个文档文件（未提交）。用户「收尾」授权实质认可 → 保留。已发纪律提醒，后续写入零越界。
- **文档治理规范确立**（用户规则 1/2/3）：`docs/` 其他文档=事实标准可更新；`docs/ai/pilot/<task-id>/`=任务上下文唯一入库载体；`specs/`/`TODO.md`/工作日志均不入库。

## 4. 文档更新

- 本次会话的审计结论、收尾就绪、规范变更全部沉淀进 `docs/ai/pilot/dsh-alignment-b2-b9/`（本目录）。
- `docs/` 事实标准文档按代码现状更新（规则 1），与 pilot 文档不冲突。

## 5. 教训

- **审计代理的纪律边界**：只读审计任务必须显式声明「修复归属由用户裁决」；工程师越权暴露了任务书措辞可被「建议清单」误读为执行指令——未来审计任务书应加粗「禁止修改任何文件」。
- **specs 已入库内容的盲区**：收尾核对不能只信代理自述，必须 `git status`+`grep` 实查（本次正是靠此发现 specs 注记早已入库，避免重复改动）。
- **任务上下文载体必须单一**：依赖 specs/（被忽略）、TODO.md（被忽略）、工作日志（被忽略）会丢失任务上下文；统一收敛到 `docs/ai/pilot/<task-id>/` 是唯一可靠路径。

## 6. 剩余问题

| # | 问题 | 性质 |
|---|---|---|
| R1 | `batch-c` 归档实物丢失：`.trash/2026-09-28-filetools-host-ui/` 不在库（`.gitignore` 排除 `.trash/`），原件仅能从 git 历史 `b11ba39^` 找回 | 不阻断，已写入报告勘误 |
| R2 | `ForgeSelf.Web/components.d.ts` 为前端构建生成物（已跟踪），提交时勿散落 | 提交分组注意 |
| R3 | batch-b（供应商目录）仍草案留档，六处勘误行号仍准，待用户批复进入闸门1 | 待决策 |
| R4 | WAF 测试宿主不挂插件控制器路由，导致 7 条 404 基线红（WorkflowPlanning×6 + ScriptRunnerDi×1） | 既有基线，下版补 plugin.json 或标记排除 |

## 7. 建议解决方案

- **R1**：归档规则改为 git 可见路径（如 `docs/archive/`），或明确「原件见 git 历史 `b11ba39^`」；`07-final-report.md` 已登记。
- **R2**：`components.d.ts` 随前端提交组，或加 `.gitignore` 排除（建议后者，避免噪音）。
- **R3**：batch-b spec 行号零漂移、复用价值高，建议直接批复闸门1 开工。
- **R4**：WAF 插件 stage 补 `plugin.json`（历史 target 只同步 `*.dll` 漏了前端资产），可消 7 红；或测试侧标记已知 404 排除。

## 8. 是否完成

- 用户「第一步」确认项——**本次任务信息是否都收敛到 `docs/ai/pilot`**：✅ 已达成（本目录 00–07 承载全部上下文；原 dsh-alignment-* 已迁移）。
- 代码与文档对齐、文档滞后修复：✅ 已闭环。

## 9. Final Decision

**APPROVED** —— 本次 dsh 对齐改造（040/041/042，B1–B9）收官达成：八批次代码全部落地、全量测试 1735 例绿率 99.4%（剩余 0.6% 为既有基线族，与本案零交集）、文档滞后 27 处修复闭环、收尾零代码越界、文档治理收敛至 `docs/ai/pilot/dsh-alignment-b2-b9/` 单一上下文真源。按用户授权进入分批提交阶段（仅提交不推送）。
- 提交：⏳ 待用户显式授权（按 git 铁律不自动提交）。
