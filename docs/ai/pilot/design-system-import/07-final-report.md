# 07 Final Report — design-system-import

任务：M13 — 外部 DTCG 文件读进库（导入）+ 库数据导回 DTCG（回流），v2.7.0 收口。

## 结论

- 状态：**COMPLETED**（Intent 7 条成功判据全部满足；Spec FR-I1…I10 全交付，Unknown U1…U6 均已在实现期消解）。
- 范围：首期只做 W3C DTCG（2025.10）、只导到已有项目、默认 `overwrite=false` 保护手改/导入行、导入预览与导入共用一次解析。

## Validation（Verified 证据）

- 后端门禁：DesignSystem 197/197 含 `ImportTests.cs`（纯解析器、全批回滚、环/悬挂/重复逐项诊断、5000 条 / 4MB 限额、tier/type 仅认证据、不搬迁、来源只进列）。
- e2e（零 mock 真宿主）：导入 55517 字节 DTCG → 预览 → 确认 → 回流导出逐字一致（round-trip），网络行与断言记录于 `05-evidence.md`。
- vitest：导入相关前端逻辑（预览表渲染、档位词表）随 75 项全绿。
- 发布：v2.7.0 zip（SHA256 `45a4e5a9…426c`，后补探针证据）与 v2.7.1 zip（`a9be045c…96d4e`）均含 `DtcgImporter`（UTF-16LE DLL 探针命中；`ImportLimits` 为 const 内联不作探针判据）。

## Review

- 决策（用户/agent）：首期 DTCG-only、不覆盖手改、预览必经 —— 用户拍板；路径前缀猜测降级为不可信证据 —— 本轮实证教训（语义路径 `chart.series-1` 被误判为 primitive 导致整批拒绝）。
- Final Decision：`06-review.md` = **APPROVED_WITH_RISK**（风险：外部文件格式长尾未穷举；限额内已兜底整批回滚）。
- 教训沉淀：`design-system-verify` 技能第 33 条（导入/回流四问 + e2e 从 select value 读主题而非 innerText）。

## 遗留

- Tokens Studio 完整格式导入、Figma 双向同步：记 TODO 候选，未做。
