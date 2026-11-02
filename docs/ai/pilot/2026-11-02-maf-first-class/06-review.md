# 06 Review — 2026-11-02-maf-first-class

> 阶段：Stage 8｜Task ID：`2026-11-02-maf-first-class`｜日期：2026-11-02｜审查人：lead（Team Lead，走查批执行者）

## 八问

1. **需求是什么？** FR-1..6：愿景/架构/ADR/命名真源更新 + 对外展示名「智能体」（Id/路由不改）+ 废弃标记。
2. **改了什么？** 文档：vision 四篇、maf-centric-architecture（新）、ADR-005/006（新）、not-taken +3、021/041 节、README 索引；展示：plugin.json、tabs.ts、features.ts、AiAgentView/AgentEditDialog 文案；本 pilot 00–07。
3. **验收标准？** AC1–AC7（02-spec）；全部逐条验证（见 05）。
4. **验证跑了吗？** 文档核对 + 浏览器走查（截图 01–04）+ 命名 grep；未跑全量 e2e（本批无行为变更，风险低）。
5. **影响面？** 文档与字符串；API/契约/存储零变更（executionMode 值 free/plan 保持，Id/路由保持）。
6. **风险？** ① 宿主外旧名残留（Home 已由走查批修，TodoTracker 委派文案待小批，TODO P2）；② 历史文档中 FreeLoop/PlanDriven 未批量改写（spec 允许）；③ 架构文档 §4「审批/杀进程缺口」行已过时——已回写状态行（2026-11-02 晚）。
7. **遗留？** TODO：B2–B5 批次（Run 级 Verify/任务中心/权限可配置/旁路收敛）；ui-design-brief 与 responses-incomplete-status 两个 pilot 待各自闸门1。
8. **问题类闭环？** PC-021（agent-entry-blank-or-old-name）命中同类 → Home 部分已修（走查批）；台账条目随提交生效。

## Final Decision

**APPROVED**（文档与命名批；配套功能块按各自 pilot 收口。可交付，建议随走查批一并提交入库）。

## 悬空教训

- 真源文档的状态行滞后于实现（「P0 未开工」vs 实际已建成）——并行会话模式下，架构真源的状态字段应由收口方在闸门2 前统一回写；本批已由 lead 回写。
- 展示名改动必须以「宿主壳 + 全部插件包」为扫查面（PC-021 二次命中 Home 包内残留即证明）。
