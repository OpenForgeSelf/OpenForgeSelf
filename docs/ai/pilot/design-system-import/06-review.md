# 06 · Review — M13 DTCG 导入/回流（v2.7.0）

日期：2026-09-30。范围：`Plugins/DesignSystem`（Services/DtcgImporter、TokenRepository.ImportLookups、
Controllers import/preview+import、/meta 契约、web ExportCenter 导入块、api.ts）、
`ForgeSelf.Api.Tests/Plugins/DesignSystemTests/ImportTests.cs`、e2e 10b 步骤。

## 八问

1. **需求原文是什么？** 用户拍板「按推荐开工（DTCG · 只进已有项目 · 默认不覆盖 · 带预览）」；
   成功判据（01-intent）：导入真 DTCG → 库里出现对应 tier/别名行 → 再导出对得上（round-trip）。
2. **交付了什么？** preview+import 两端点（`/meta` 声明能力与上限）、纯函数解析器 `DtcgImporter`、
   `ImportLookups` 一次读库、导出交付页导入回流块（选文件→预览→确认→生效值可查）、`ImportTests` 11 条、e2e 10b。
3. **判据逐条对上了吗？** 对上了：effective 读回别名顺到 `#123456`；round-trip 55517 B 逐字一致；成环/悬空/重复整批不写；
   手改保护默认生效；上限 5000/4MB 拒；导入令牌进同一套对比度门禁。全部有断言（Verified）。
4. **与既有行为冲突吗？** `Apply()` 语义（null=不动列）被导入复用，既有列不被顺手抹掉；
   `UpsertBatch` 的全图校验被沿用，成环样本在服务层被拒（有用例钉）。
5. **边界与错误处理？** 解析器不 catch 不静默丢条目；`ImportException` → 400；未知 format/theme → 400/404；
   字节数超限在控制器（4MB）与解析器（5000 条）各自成立且同链。
6. **测试够吗？** 后端 11 条覆盖 round-trip / 不搬家 / 层级以库为准 / 手改保护 / 上限 / 成环 / 门禁联动；
   e2e 真宿主走完整 UI 链并断言三判据；守卫（classes/vocabulary）仍绿。
7. **证据可信吗？** 全部来自实跑日志与截图读图（见 05-evidence，分级 Verified）；无 Inferred/Unknown 冒充结论。
8. **遗留什么？** Tokens Studio 完整格式（`$themes`/多 set）导入未做（`/meta.importFormats` 留扩展位，TODO 已登记）；
   Figma 双向同步仍属 P3；本次 9 项后端红均为非 DesignSystem（已归类，2 项新观察记 TODO）。

## Final Decision

**APPROVED_WITH_RISK** —— 功能与判据全部达成且经真宿主 e2e 与全量后端验证（DesignSystem 0 红）；
风险仅在于：全量后端有 9 项非本插件红（7 项已知 staging 家族 + 1 项已知脆断言 + 1 项新观察 ForgeConfigTests 待归因），
以及首期只支持单文件 DTCG（格式扩展位已留）。发布物：本地 zip v2.7.0（不打 tag、不触发 CI，遵用户口径）。
