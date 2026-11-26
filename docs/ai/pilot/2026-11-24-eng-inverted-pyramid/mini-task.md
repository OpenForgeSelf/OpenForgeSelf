# mini-task · 工程流程稳定迭代（土豆姐倒置金字塔三阶段改造）

> 轻量任务形态（2026-11-24 输入32/输入33）。用户特例授权：不必完整走 AI-Native 九阶段，直接执行改造；本目录仅为 commit-msg 门禁 trailer 依据 + 改造留痕。

## 目标
借鉴土豆姐（Lauren Tan @poteto）倒置金字塔理念（约束可靠性从底到顶：代码库 → 静态分析 → 验证 → prose → 人工 review），按「稳定迭代、精简优化」三阶段改造工程流程。

## 范围（做且只做）
1. **阶段1 修门禁可信度**：hook 工具链三缺口归零（mini-task 假红 / Windows 安装 / denylist 恢复），每项配守卫测试。
2. **阶段2 固化迁移首批**：2–3 条高频 prose 规则下沉为 lint/CI 守卫，同步压缩对应 prose。
3. **阶段3 prose 瘦身**：清理过时/重复/死引用条款；不改三闸门数量、不砍八工件、不动汇报模板。

## 变更摘要
- 批1（阶段1）：`verify-pilot-feature-writeback.ps1` mini-task 假红修复；`install-git-hooks.ps1` 补 BOM（逻辑 PC-043 已闭环）；`check-commit-hygiene.ps1` 修 2 个深层 bug（单元素数组解包 / 单行文件逐字符扫描）；`.forgeself/commit-hygiene.denylist.txt` 重建（本地不入库）；守卫测试 3 类 7 用例。
- 批2（阶段2）：新增 `scripts/check-engineering-rules.cjs`（eng.no-temp-probe / eng.pwsh-only / eng.ps1-bom）+ 4 守卫测试；接线 pre-commit 第 5 道门 / run-gates.ps1 / pnpm run check。
- 批3（阶段3）：AGENTS.md 5 处修正 + 2 段压缩为守卫指针；4 份规则文档死引用/过时清理；B1 固化迁移小节 + c-changelog 台账。

## Interaction Design
无界面交互——纯脚本/lint/守卫测试/文档改造，不触碰任何前端页面与用户交互路径。

## 约束
- 脚本一律 pwsh；代码注释中文；只 commit 不 push。
- 验证必须真跑（pnpm check/test、dotnet build/test、守卫实跑），标注 Verified/Inferred/Unknown。
