# Tasks — M13 设计令牌导入 / 回流

> 依据 `03-plan.md`。每个任务给 **Allowed / Forbidden / 验证命令**；改完即跑该命令，红就停在本任务。

| # | 任务 | Allowed（只这些文件） | Forbidden | 验证命令 |
|---|---|---|---|---|
| T1 | 常量：版本 2.7.0 + `ImportFormats` + `ImportLimits` | `DesignSystemConstants.cs`、`plugin.json` | 不动其他词表；不把上限写进控制器/前端 | `dotnet build ForgeSelf.Api` |
| T2 | `DtcgImporter.Parse()` 纯函数（组/叶、$type 继承、值映射、层级、来源、上限、诊断） | 新文件 `Services/DtcgImporter.cs` | **不得**引用 `DesignToken`/`Save`/`UpsertBatch`（不落库）；不 catch 后静默丢条目 | `dotnet test --filter DesignSystemTests`（新增解析用例先红后绿） |
| T3 | 控制器 `import/preview` + `import`；`/meta` 出 `importFormats`/`importLimits`，`capabilities += import` | `DesignSystemController.cs` | 不新增第二条落库路径；非法 theme → 400（不是 500）；不自动跑审计 | `dotnet test --filter DesignSystemTests` |
| T4 | 后端判据用例 I1…I10 + 边界（空文档 / 超限 / 成环 / 手改保护 / round-trip 逐字） | 新文件 `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/ImportTests.cs` | 不断言提示文案；不用 `GreaterThan(0)` 糊住计数 | `dotnet test --filter DesignSystemTests` |
| T5 | 前端：`api.importPreview/importTokens` + `MetaInfo` 字段；ExportCenter「导入 / 回流」区（选文件 → 预览三张清单 → overwrite 显式勾选 → 确认写入 → 结果 + 审计提示） | `web/src/api.ts`、`web/src/sections/ExportCenter.vue`、新 `web/src/design/importPlan.ts`(+test) 若需纯函数 | 不在前端重算差异（preview 必须打后端）；无 `import` 能力时入口置灰并说明原因，不藏起来 | `cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build` |
| T6 | 插件层 e2e：真宿主走完「选文件 → 预览 → 写入 → 库里读得到 → 再导出对得上」+ 截图读图 | `ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts` | 不 mock；截图前必须等数据就绪（技能第 28 条） | `pnpm exec playwright test e2e/plugins/design-system/design-system.spec.ts` |
| T7 | 全量后端 + 发布 + 文档回写 | `README.md`、`ROADMAP.md`(P1.20)、`docs/02-features/036`、`05-evidence`/`06-review`、技能、TODO、日记、记忆 | 不 commit（用户口径） | `dotnet test ForgeSelf.Api.Tests`；`release-local.ps1 -Version v2.7.0 -UpdateDir artifacts/update`；包内探针 |

**整批验收**（= `01-intent.md` 成功判据 7 条）：DesignSystem 用例全绿且每条 FR 有对应用例；round-trip 两次导出逐字一致；反例整批不写；手改保护仍生效；导入后审计能看见新令牌；e2e 真宿主走完全链并留证据；四层门禁 + zip + 探针 + 文档齐。
