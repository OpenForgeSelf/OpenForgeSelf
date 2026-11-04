---
task_id: 2026-11-05-db-admin
feature: 044
risk: { level: L2, triggers: ["ForgeSelf.Web/e2e/menu-route-consistency.spec.ts", "ForgeSelf.Web/e2e/plugins/db-admin/**", "docs/ai/pilot/2026-11-05-db-admin/**"], raised_by_agent: true }
gate1: { mode: user, at: "2026-11-05T01:45:00+08:00", decision: "夜冲刺 lead 任务板 task-5 [T-E] 派发即开工令（闸门1 视为已批；lead 2026-11-05 消息与任务正文在案）" }
gate2: { mode: reviewer, at: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Abstractions/ToolNamespace.cs
  - ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj
  - ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbAdminContractTests.cs
  - ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbAdminTestFixture.cs
  - ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbAdminToolEvalReplayTests.cs
  - ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbAdminToolFunctionTests.cs
  - ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbConnectionServiceTests.cs
  - ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbQueryServiceTests.cs
  - ForgeSelf.Api.Tests/Plugins/DbAdminTests/RedactionAndCryptoTests.cs
  - ForgeSelf.Api.Tests/Plugins/DbAdminTests/SqlGuardScopeTests.cs
  - ForgeSelf.Api.Tests/Plugins/DbAdminTests/SqlGuardTests.cs
  - ForgeSelf.Api.Tests/XCodeTestFixtureIronRuleTests.cs
  - ForgeSelf.Api/Data/XCodeConfig.cs
  - ForgeSelf.Api/ForgeSelf.Api.csproj
  - ForgeSelf.Web/ct/stories.ts
  - ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts
  - ForgeSelf.Web/src/components/DbAdmin.story.ts
  - ForgeSelf.Web/src/components/DbAdminStoryHost.vue
  - ForgeSelf.slnx
  - Plugins/DbAdmin/Controllers/DbAdminController.cs
  - Plugins/DbAdmin/Data/DbAdmin.htm
  - Plugins/DbAdmin/Data/DbAdminTables.cs
  - Plugins/DbAdmin/Data/Entities/.editorconfig
  - Plugins/DbAdmin/Data/Entities/DbApproval.Biz.cs
  - Plugins/DbAdmin/Data/Entities/DbApproval.cs
  - Plugins/DbAdmin/Data/Entities/DbAuditLog.Biz.cs
  - Plugins/DbAdmin/Data/Entities/DbAuditLog.cs
  - Plugins/DbAdmin/Data/Entities/DbConnection.Biz.cs
  - Plugins/DbAdmin/Data/Entities/DbConnection.cs
  - Plugins/DbAdmin/Data/Entities/DbQueryHistory.Biz.cs
  - Plugins/DbAdmin/Data/Entities/DbQueryHistory.cs
  - Plugins/DbAdmin/Data/Model.xml
  - Plugins/DbAdmin/DbAdmin.csproj
  - Plugins/DbAdmin/DbAdminPlugin.cs
  - Plugins/DbAdmin/Models/DbAdminModels.cs
  - Plugins/DbAdmin/Services/DbAdminAgentService.cs
  - Plugins/DbAdmin/Services/DbAdminToolFunctions.cs
  - Plugins/DbAdmin/Services/DbAuditWriter.cs
  - Plugins/DbAdmin/Services/DbConnectionService.cs
  - Plugins/DbAdmin/Services/DbQueryService.cs
  - Plugins/DbAdmin/Services/DriverCatalog.cs
  - Plugins/DbAdmin/Services/PermissionModeStore.cs
  - Plugins/DbAdmin/Services/ResultLimiter.cs
  - Plugins/DbAdmin/Services/SecretProtector.cs
  - Plugins/DbAdmin/Services/SqlGuard.cs
  - Plugins/DbAdmin/Services/SqlRedactor.cs
  - Plugins/DbAdmin/docs/agent-tool-eval-cases.json
  - Plugins/DbAdmin/plugin.json
  - Plugins/DbAdmin/web/.gitignore
  - Plugins/DbAdmin/web/package.json
  - Plugins/DbAdmin/web/pnpm-lock.yaml
  - Plugins/DbAdmin/web/pnpm-workspace.yaml
  - Plugins/DbAdmin/web/src/DbAdminView.vue
  - Plugins/DbAdmin/web/src/__fixtures__/backend-samples.json
  - Plugins/DbAdmin/web/src/api.test.ts
  - Plugins/DbAdmin/web/src/api.ts
  - Plugins/DbAdmin/web/src/approvalFlow.test.ts
  - Plugins/DbAdmin/web/src/approvalFlow.ts
  - Plugins/DbAdmin/web/src/connForm.test.ts
  - Plugins/DbAdmin/web/src/connForm.ts
  - Plugins/DbAdmin/web/src/contract.test.ts
  - Plugins/DbAdmin/web/src/http.test.ts
  - Plugins/DbAdmin/web/src/http.ts
  - Plugins/DbAdmin/web/src/index.ts
  - Plugins/DbAdmin/web/src/normalize.test.ts
  - Plugins/DbAdmin/web/src/normalize.ts
  - Plugins/DbAdmin/web/src/pager.test.ts
  - Plugins/DbAdmin/web/src/pager.ts
  - Plugins/DbAdmin/web/src/redact.test.ts
  - Plugins/DbAdmin/web/src/redact.ts
  - Plugins/DbAdmin/web/src/restore.test.ts
  - Plugins/DbAdmin/web/src/restore.ts
  - Plugins/DbAdmin/web/src/types.ts
  - Plugins/DbAdmin/web/vite.config.ts
  - Plugins/DbAdminTests/DbAdminTests.csproj
  - ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts
  - ForgeSelf.Web/e2e/menu-route-consistency.spec.ts
rollback: "git branch backup/db-admin-pre-rebase 留回滚点；门禁红则不合并、worktree 可整体废弃重建（git worktree remove wt-db + worktree add 重来）；走查/测试产生的连接与历史经 UI 删除，绝不触碰用户 :51888 数据"
writeback: { feature_doc: "docs/02-features/044-db-admin.md 随工件提交入库（创建即修改，满足范围门）", reason: "契约冻结于 Plugins/DbAdmin/docs/03 §3.1；本批零逻辑改动" }
---

# Task — db-admin 并入 main（收口与验收）

> Task ID：`2026-11-05-db-admin`｜执行者：db-dev｜派发：lead 任务板 task-5 [T-E]（2026-11-05 夜冲刺）

## Allowed（本任务写面）

- `docs/ai/pilot/2026-11-05-db-admin/`：00-07 全部工件
- `docs/02-features/044-db-admin.md`（新功能文档；043 已归 smart-terminal，st-dev 2026-11-05 01:50 确认）
- git 历史重排（仅 wt-db 内 feat/db-admin）：rebase --onto main、rebase -i 排序与 reword trailer
- `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`（新，恰 1 条冒烟）
- `ForgeSelf.Web/e2e/menu-route-consistency.spec.ts`（仅 ④ 回归清单补 smart-terminal + db-admin 两项；st-dev 协调定稿）
- 截图（screenshots/ 不入库）与 .temp 测试数据（不入库）
- TODO.md「协作交接」登记 dev-stack 起停（主工作区，仅追加本批行）

## Forbidden

- `Plugins/DbAdmin/**` 业务逻辑改动（测试暴露缺陷才修，修则记偏差 + 重跑 AC2/AC3）
- 宿主登记文件逻辑（XCodeConfig/ToolNamespace/csproj/slnx——已在历史提交，本批不碰）
- `:51888` 实例停/启/杀/配置；git push；主工作区切分支
- ImGateway/AIAgent WIP 全部路径与 docs/02-features/031-im-gateway.md（只读）
- 破坏性 SQL 真执行（一律走审批→拒绝路径）；删除非本批自建的任何数据
- 生成件手改（xcode 生成 `.cs`）、全量 e2e（深档不触发：不碰 global-setup/playwright.config/fixtures）

## 验证命令（快档 + 指定项）

```powershell
$env:NO_PROXY='localhost,127.0.0.1,::1'; $env:TEMP="$PWD\.temp\tmp"; $env:TMP="$PWD\.temp\tmp"
pwsh scripts/ci/run-gates.ps1 -Base main                 # AC2（读日志正文 PASS）
dotnet build ForgeSelf.Api\ForgeSelf.Api.csproj          # AC3（0 error）
dotnet test ForgeSelf.Api.Tests\ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~DbAdmin|XCodeConfigTests|PluginLoadDependencyGuardTests|PluginToolConstructorDiGuardTests"
cd Plugins\DbAdmin\web; pnpm run build; pnpm test        # AC4
cd ..\..\ForgeSelf.Web; pnpm run check; pnpm run test; npx vue-tsc -b
pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin   # AC5
```

判定一律看日志正文（PASS/通过数），不拿 exit code 当证据；全量中档归 qa-ci（本任务知会）。

## 完成定义

AC1..AC8 全绿（02-spec §9）+ 05/06/07 齐且真值 + 报 lead merge-ready。

## 执行偏差记录（实现期回填）

- （暂无）

