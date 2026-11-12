# AI-Native Pilot Result（最终汇报）— db-admin 暂时关闭驱动预检

> Task ID：`2026-11-06-db-admin-driver-probe-off`｜日期：2026-11-06｜状态：⚠️ COMPLETED_WITH_RISK（源码闭环完成；未发布到用户 :51888，发布需另行授权）

## 1. Repository Understanding

我确认了：宿主 .NET 10 + NewLife.XCode 12.0.2026.701，仅随附 `XCode.SQLite`；DbAdmin 插件在 `Plugins/DbAdmin/`，驱动预检在 `Services/DriverCatalog.cs`（`DefaultDriverProbe`）并由 `DbConnectionService` 的 `ListProviders/Resolve/Test/Wrap` 消费；前端 `DbAdminView.vue` 用 `driverAvailable=false` 渲染「需驱动」后缀与灰标；插件测试在 `ForgeSelf.Api.Tests/Plugins/DbAdminTests/`，冒烟 e2e 在 `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`。

## 2. Selected Task

用户输入2 明确指令：数据库插件暂时屏蔽驱动检查，交给 XCode 自动获取驱动，连接失败直接提示；错误细分「另说」。

## 3. Changed Files

- `Plugins/DbAdmin/Services/DriverCatalog.cs`（`ProbeEnabled=false` + 注释）
- `Plugins/DbAdmin/Services/DbConnectionService.cs`（四处预检/分类开关 + `ErrorMessageOf`）
- `Plugins/DbAdmin/plugin.json`（0.1.3 → 0.1.4）
- `Plugins/DbAdmin/DbAdmin.csproj`（注释）
- `ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbConnectionServiceTests.cs`
- `ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbAdminContractTests.cs`
- `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`
- `Plugins/DbAdmin/docs/{02,03,05,06}.md`、`ui-design-brief.md`
- `docs/02-features/044-db-admin.md`
- 本 pilot 工件目录 00-07

## 4. Validation

Build：`dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj` → PASS（0 error，352 既有 warning，Verified）。
Unit Test：`dotnet test --filter "FullyQualifiedName~DbAdmin"` → PASS 167/167（Verified）。
Plugin Web：`pnpm run test && pnpm run build`（Plugins/DbAdmin/web）→ PASS 61/61 + build 成功（Verified）。
E2E：`pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin` → PASS 1/1（1.0m，Verified）。
Static：`pnpm run design:check` → PASS（Verified）。
Runtime API：dev-stack `GET /providers` 全 `driverAvailable=true`；MySQL 测连返回 `sql_error`「由于目标计算机积极拒绝，无法连接。」（8.6s，Verified，证明 XCode 已取得驱动并发起真实连接）。

## 5. Evidence

见 `docs/ai/pilot/2026-11-06-db-admin-driver-probe-off/05-evidence.md`：命令原文、退出码、通过数、日志路径（`.temp/tmp/dbadmin-tests-r6.log`、`dbadmin-e2e-r5.log`）、API JSON、截图（`ForgeSelf.Web/screenshots/e2e/db-admin/02-provider-no-driver-tag.png`）与读图结论；AC1–AC6 逐条对照。

## 6. Review

见 `06-review.md`：八问全部通过，Requirement/Scope/Test/Architecture Check = PASS，Risk = L2，Final Decision = **APPROVED**。

## 7. Risk

L2。语义变化：非 SQLite 连接失败码由 `driver_missing`(424) 改为 `sql_error`(400)（前端已有 `sql_error` 渲染分支，文档已同步）。回滚点：`DriverCatalog.ProbeEnabled` 改回 `true` 即恢复原预检与 424 行为。

## 8. Problems Found

- 非 SQLite 测连放进单元测试会真实发起网络连接，单跑绿、组内偶发红 → 已改为网络无关断言（`Resolve_NonSqlite_NoDriverGate` + SQLite 缺文件），真实驱动行为由 dev-stack API 实测取证。
- e2e `global-setup.ts` 清理重试段 `cmd /c timeout` 在非交互 stdout 下失败 → 已记 TODO（P2）。
- 用户「另说」的错误细分未做 → 已记 TODO（P3）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS（含 3 条偏差记录） |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

### 9.5 本批自复盘

- **经验提取**：① 先取证再定测法——dev-stack 实连一次 MySQL 立刻证明「XCode 会取驱动」，避免用网络依赖单测猜测；② 契约测试用 SQLite 缺文件代替外连，保持契约断言确定性；③ UI 断言直接挂进既有冒烟 e2e，不新增第二个 e2e 用例（铁律 24）。
- **待改进**：① 第一版把非 SQLite 实连写进单测，导致一次偶发红（已改）；② e2e 残留宿主清理依赖外部 `timeout`，环境脆弱（已入 TODO）。

## 10. 问题类归档

无。本任务为行为调整（非缺陷/回归/返工），未命中 `docs/11-troubleshooting/problem-classes/README.md` 的 DbAdmin 相关类；不新建 PC。

## 11. 重构提案

R0（不重构）。驱动预检以单常量开关保留、可一键恢复，符合「暂时关闭」意图；若后续确认永久取消，再按 R1 局部清理 `IDriverProbe`/`DefaultDriverProbe`/`driver_missing` 分支（届时需同步契约文档与前端分支）。

## 12. 悬空教训清单（必须为空）

空。Problems Found 两项已落 `TODO.md`（252 行 P3 错误细分、253 行 P2 e2e 清理重试）；其余无遗留。
