---
task_id: 2026-11-06-db-admin-driver-probe-off
feature: 044-db-admin
risk: { level: L2, triggers: ["Plugins/DbAdmin/Services/**", "ForgeSelf.Api.Tests/Plugins/DbAdminTests/**"], raised_by_agent: false }
gate1: { mode: user, at: "2026-11-06T09:0x+08:00", decision: "用户输入2 直接指令『数据库插件，暂时屏蔽掉插件检查……先取消掉驱动校验』，视为闸门1 已批" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/DbAdmin/Services/DriverCatalog.cs
  - Plugins/DbAdmin/Services/DbConnectionService.cs
  - Plugins/DbAdmin/plugin.json
  - Plugins/DbAdmin/DbAdmin.csproj
  - ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbConnectionServiceTests.cs
  - ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbAdminContractTests.cs
  - ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts
  - Plugins/DbAdmin/docs/02-交互说明.md
  - Plugins/DbAdmin/docs/03-架构与接口.md
  - Plugins/DbAdmin/docs/05-已知风险-多引擎与密码.md
  - Plugins/DbAdmin/docs/06-UI交互评审稿.md
  - Plugins/DbAdmin/docs/ui-design-brief.md
  - docs/02-features/044-db-admin.md
  - docs/ai/pilot/2026-11-06-db-admin-driver-probe-off/**
rollback: "git checkout -- Plugins/DbAdmin/ ForgeSelf.Api.Tests/Plugins/DbAdminTests/ docs/02-features/044-db-admin.md（或 git revert <commit>）"
writeback: { feature_doc: done, reason: "044 + 插件 docs/02/03/05/06/ui-design-brief 已同步" }
---

# Agent Task

## Task ID

`2026-11-06-db-admin-driver-probe-off`

## Objective

让 db-admin 暂时不再前置校验数据库驱动：provider 列表恒可用、连接/测连交给 XCode，失败直接回显底层错误；保留开关可一键恢复。

## Scope

### Allowed

- 03-plan 列出的 9 个文件 + `docs/ai/pilot/2026-11-06-db-admin-driver-probe-off/` 工件。
- 修改 `DriverCatalog` / `DbConnectionService` 的条件判断与注释。
- 更新 DbAdmin 相关测试与文档。

### Forbidden

- 不新增/删除 NuGet 依赖，不改 `DbAdmin.csproj`。
- 不删除 `IDriverProbe` / `DefaultDriverProbe` / `DriverMissingMessage` / `driver_missing` 契约。
- 不改数据库结构、实体、`Model.xml`、宿主登记。
- 不改前端业务逻辑（`DbAdminView.vue` / `normalize.ts`）。
- 不停/启/杀 `:51888` 或用户运行中的宿主；不执行 git commit/push/tag/发布（发布需另行授权）。

## Acceptance Criteria

- [ ] AC1 预检关闭时 `/providers` 全部 `driverAvailable=true`，其余字段不变。
- [ ] AC2 非 SQLite 测连失败返回 `sql_error` + 底层消息（脱敏），不返回 `driver_missing`。
- [ ] AC3 `Resolve` 不因驱动探测被拦截。
- [ ] AC4 SQLite 测连成功/缺文件报错两条既有用例仍绿。
- [ ] AC5 插件前端测试/构建绿；界面无「需驱动」标记（截图）。
- [ ] AC6 插件文档 + `docs/02-features/044-db-admin.md` 已同步。

## Expected Files

- Plugins/DbAdmin/Services/DriverCatalog.cs
- Plugins/DbAdmin/Services/DbConnectionService.cs
- Plugins/DbAdmin/plugin.json
- Plugins/DbAdmin/DbAdmin.csproj
- ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbConnectionServiceTests.cs
- ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbAdminContractTests.cs
- ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts
- Plugins/DbAdmin/docs/{02,03,05,06}.md、Plugins/DbAdmin/docs/ui-design-brief.md
- docs/02-features/044-db-admin.md

## Verification Commands

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~DbAdmin"
cd Plugins/DbAdmin/web && pnpm run test && pnpm run build
cd ForgeSelf.Web && pnpm run design:check
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-06-db-admin-driver-probe-off -EnforceInteraction
```
