---
task_id: 2026-10-28-plugin-design-boundary
feature: plugin-design-boundary
risk: { level: L2, triggers: ["docs/04-standards/plugin-design-boundary.md"], raised_by_agent: false }
gate1: { mode: reviewer, at: 2026-10-28, ref: "04-task 头部" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - docs/04-standards/plugin-design-boundary.md
  - docs/README.md
  - docs/07-decisions/not-taken-decisions.md
  - docs/02-features/021-ai-agent.md
rollback: "git checkout -- docs/04-standards/plugin-design-boundary.md"
writeback: { feature_doc: pending, reason: "标准已写入 docs/04-standards/plugin-design-boundary.md" }
---

# 04 · Task

## 头部

- risk: 低-中（纯文档 + 审计结论；无代码改动；中风险点=审计结论可能触发后续搬迁任务，本批不实施）
- expected_files: docs/04-standards/plugin-design-boundary.md（新）、docs/README.md、docs/07-decisions/not-taken-decisions.md、docs/02-features/021-ai-agent.md、pilot 05-07
- feature: 插件功能边界设计标准（plugin-design-boundary）
- rollback: 全部为文档新增/小节追加，git checkout 单文件即回滚

## 批次任务

- [ ] B1 标准文档 `docs/04-standards/plugin-design-boundary.md` v1.0.0（T0-T4 / 判定三问 / 六条款 / AIAgent 反例复盘 / 准入流程）
- [ ] B2 审计报告：20/20 插件逐行判定（❌V1 text-dev 三对重复 / ❌V2 文件域劈半 A/B 两案 / ⚠️W1-W4 / ✅16 个）
- [ ] B3 docs/README.md 登记标准
- [ ] B4 not-taken 登记：执行三通道不合并（触发条件=第三执行通道出现或 UI 融合需求）；dev 颜色三件套暂不迁 design-system
- [ ] B5 021-ai-agent.md 增「插件划分」小节指引（指到标准+审计）
- [ ] B6 验证：verify-pilot-artifacts PASS + 审计计数 20 核对

## AC

- [x] AC-1 标准文档含 T0-T4 分类学 + 判定三问 + ≥6 条款 + AIAgent 反例复盘 + 准入流程（七条款落地）
- [x] AC-2 审计覆盖 20/20 插件，每插件有判定与依据（见 03-plan 审计节）
- [x] AC-3 两违规项各有：归属建议 + 代价 + 触发条件（拍板后已随批实施）
- [x] AC-4 not-taken 登记 ≥2 项（046/047/048）
- [x] AC-5 verify-pilot-artifacts PASS

## 审计节（B2 · 20/20 插件逐行判定）

| 插件 | 层 | 判定 | 依据 | 说明 |
|------|----|------|------|------|
| ai-agent | T3 | ✅ | §3.2 | 修订后 4 工具全协议/编排（aiagent_plan_workflow + 3 协议词），反例已修 |
| agent-hub | T3 | ✅ | §3.2 | 5 个 hub_* 委派/查询域内聚 |
| mcp-center | T1 | ⚠️W1 | §2 T1 红线 | 网关+公共工具双职责已立界（红线=公共工具面只收跨域基础小工具）；文件三件套已迁出（D-V2） |
| dev-tools | T2 | ⚠️W4→✅ | §4.3 | 颜色三件套留 dev（048 立界）；text-tools 并入后 27 工具域声明已扩 |
| file-tools | T2 | ✅ | §3.5 | 文件域归一（9 工具：管理 6 + 三件套保留名） |
| text-tools | T2 | ❌V1→已并入 | §3.3 | 三对同功能双实现（base64/hash/format_json）→ 用户拍板整插件并入 dev-tools |
| memory-system | T2 | ✅ | §3.1 | 5 个 memory_* 域内聚 |
| workflow-engine | T2 | ✅ | §3.1 | 5 个 workflow_* 域内聚（AIAgent 重复实现已清） |
| system-monitor | T2 | ✅ | §3.1 | 5 个 sys_* 域内聚 |
| script-runner | T2 | ⚠️W2 | §3.7 | script_run 与其他执行通道语义可区分，046 立界不合并 |
| quick-links | T2 | ✅ | §3.1 | link_* 域内聚 |
| scheduler | T2 | ✅ | §3.1 | schedule_* 域内聚 |
| design-system | T2 | ✅ | §3.1 | 8 个 design_* 资产库域内聚 |
| sems | T2 | ⚠️W2 | §3.7 | 22 工具域内聚；sems_run_command 046 立界 |
| todo-tracker | T2 | ⚠️W3 | §3.7 | todo_dispatch 特化委派，047 立界（新增特化委派须底层复用 hub 契约） |
| im-gateway | T2 | ✅ | §3.1 | IM 网关域，无工具注册 |
| cost-scope | T2 | ✅ | §3.1 | 成本可视化数据域，无工具注册 |
| home | T4 | ✅ | §2 | 界面壳，无工具 |
| sample | T4 | ✅ | §2 | 演示插件（现兼测试载具） |
| tool-bridge | T4 | ✅ | §2 | HTTP 桥测试台，不注册工具（not-to-do 035） |

## Allowed / Forbidden（执行边界）

- Allowed：上表批次所列文件；`.trash` 归档 TextTools。
- Forbidden：不搬 ADR-004 已拍板内容；不碰外部会话 WIP（AIProvider/AIService 面）；不在本批做 W2/W3/W4 的代码级合并（仅立界）。
