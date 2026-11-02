# Intent

> 阶段：Stage 1｜Task ID：`2026-11-02-maf-first-class`｜日期：2026-11-02

## Problem

产品与架构仍把 MAF 写成「AIAgent 内的一个 loop 实现细节」；愿景文档停留在 2026-08「个人 AI 工作台」口径，未把 **MAF 一等公民** 与「本机个人 Agent Runtime」远景（统一 Run、审批、本机能力插件）写成真源。ADR-005 正确禁止了平行 LocalBot，但措辞过窄（只写复用、不定品牌）。旧名 FreeLoop/PlanDriven/ReactLoop 对外叙事混乱，诱导平行引擎提案。

## Why

用户裁决：后续所有功能围绕 MAF 展开；更新产品说明与远景；可重新设计命名与流程；插件简化；绿场不做迁移。若不先定真源，P0–P4 实现会再次分叉成 LocalBot 或第三套 loop。

## Expected Outcome

1. 愿景/目标/原则/方向以 MAF 为中心更新，并在 `docs/README.md` §一可定位。
2. 架构命名体系落盘（四层 + 旧→新对照）；ADR-006 明确「一等公民 + 绿场」。
3. pilot 00–04 齐备，Allowed/Forbidden 清楚。对外可见命名：插件名 **智能体**（菜单/标题/功能清单/模式文案）；MAF 只作执行中枢技术名。
4. P0–P4 写入目标与 plan 后续批次，**本回合不实现全谱**。

## 用户拍板（2026-11-02 输入7 · 覆盖闸门1 默认与上一批「展示名改为 MAF」）

用户已批准，本文件以下两项不再是待拍板默认：

1. **闸门1 · 路由不改**：插件 Id 仍 `ai-agent`，路由不改成 `/maf`。
2. **闸门1 · 插件名「智能体」**：用户可见插件名、菜单、页面标题、`plugin.json` 展示名、功能清单用「智能体」。MAF 仍为执行中枢技术名（架构层 MAF Runtime），不是插件名。

本拍板**不授权**开始实现 P0 审批代码。

## Constraints

- 闸门1 未批：**禁止**大规模改 MafLoop / 新建 Browser / Scheduler→AgentRun / DB 大迁移 / 改 API 路径。
- 允许：产品/架构/ADR/功能档案/README；plugin 展示名与 UI 文案；废弃旁路文档标记。
- 不改用户计划文件 `maf_复用不重做_*.plan.md`。
- 遵循 AGENTS.md §0 / §11；架构走 `architecture-design`。

## Success Criteria

- [ ] `docs/00-vision/01-vision.md` 明确铸己匣=本机个人 Agent 平台、MAF=一等中枢；含不做清单
- [ ] `docs/00-vision/02-goals.md` 含 P0–P4 MAF-centric 路线图
- [ ] `docs/01-architecture/maf-centric-architecture.md` 含四层与命名对照表
- [ ] ADR-006 落盘；ADR-005 指向 006；not-taken 增补云端/兼容层/本回合缓做
- [ ] 本目录 00–04 齐全；`docs/README.md` §一已登记
- [ ] 对外文案至少：`plugin.json` 展示名、`features.ts`、工作台标题为「智能体」；执行模式标签为开放任务/计划任务；文案区分执行中枢 MAF。路由仍 `/ai-agent`
- [ ] 回复给出待拍板 ≤2 问（带默认推荐）
