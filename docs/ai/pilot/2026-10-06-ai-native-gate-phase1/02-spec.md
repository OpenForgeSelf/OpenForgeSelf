# Specification

> 阶段：Stage 2｜从真实 Repository Understanding 与 Intent 推导。Task ID：2026-10-06-ai-native-gate-phase1

## Functional Requirements
- FR1 提供机械判级脚本，按路径/内容/规模触发表给出 L0–L4。
- FR2 工件链校验脚本支持"全量 00–07"与"轻量 mini-task.md + 05/06/07"两种形态。
- FR3 工件链校验脚本可校验 04 头部块与 02→05 的 AC 覆盖。
- FR4 pre-commit 钩子改用 pwsh，并在缺失时回退且告警。
- FR5 新增 commit-msg 钩子：非文档改动必须带 `Pilot: <目录名>`。
- FR6 提供 PR/push 级 CI，执行上游闸门。
- FR7 闸门校验"回写"：`feature` 指向的功能文档须同区间被修改；`requirements` 中的 ID 须已登记。
- FR8 判级脚本计入未跟踪新文件。
- FR9 提供需求 ID 规范 + 模板 + 半自动回填工具。

## Input
仓库真实目录树、现有脚本与钩子、现有 39 篇功能文档。

## Output
新增/修改的脚本、钩子、CI、模板、文档；本 pilot 工件链。

## Business Rules
- 只上调不下调判级；取最高。
- 宪法层（AGENTS/流程规范/模板/钩子/闸门脚本）只能由人改。
- 回填只增不改、幂等、默认 dry-run。
- 文档与实现冲突时，事实性出入以代码实测为准并回写。

## Boundary Conditions
- 本机 pwsh 无法执行 git（桌面 AppContainer 限制）→ 依赖 git 的脚本在 CI/本机 7 可运行，在此环境下会大声失败而非静默放行。
- 现有 39 篇功能文档本批只做样例，不批量注入占位。

## Acceptance Criteria
- [ ] AC1 判级脚本存在且语法有效（Parser OK）
- [ ] AC2 工件链校验对轻量目录 PASS、对缺头部块目录 FAIL
- [ ] AC3 工件链校验含 04 头部块字段与 AC 覆盖检查
- [ ] AC4 pre-commit 使用 pwsh 且缺失时回退并告警
- [ ] AC5 commit-msg 强制 `Pilot:` trailer
- [ ] AC6 CI 工作流存在，且非纯文档提交被门禁覆盖
- [ ] AC7 闸门含回写检查（feature 文档同区间被改 + requirements 已登记）
- [ ] AC8 判级脚本计入未跟踪新文件
- [ ] AC9 需求 ID 规范/模板/回填工具就绪，dry-run 覆盖 39 篇
- [ ] AC10 本 pilot 工件链自身可被门禁 PASS

## Unknown
| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 本机 pwsh 能否执行 git | 依赖 git 的脚本无法在本环境端到端验证 | 记录为 Unknown；由用户在 Git Bash / 系统 PS7 复跑 |
| GitHub runner 的 pwsh 是否可用 | CI 能否跑 | 暂标注：ubuntu-latest 预装 pwsh 7，未实测 |
