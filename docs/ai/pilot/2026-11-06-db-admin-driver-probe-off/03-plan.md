# Plan — db-admin 暂时关闭驱动预检

> 阶段：Stage 3｜Task ID：`2026-11-06-db-admin-driver-probe-off`｜日期：2026-11-06

## Files To Change

- file: `Plugins/DbAdmin/Services/DriverCatalog.cs`
  reason: 新增 `ProbeEnabled=false` 常量并更新类注释（说明暂时关闭原因与恢复方式）。
- file: `Plugins/DbAdmin/Services/DbConnectionService.cs`
  reason: `ListProviders` / `Resolve` / `Test` 三处预检、`Test` 异常分类、`Wrap` 驱动映射均由开关控制。
- file: `ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbConnectionServiceTests.cs`
  reason: 原 `Test_DriverMissing_ClearHint` 改为新语义用例（全部 provider 可用 + 非 SQLite 解析不再被驱动门拦截）。
- file: `ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbAdminContractTests.cs`
  reason: `TestResult_OkPlusCode` 改为失败形态通用断言（`sql_error` + 非空 message），用 SQLite 缺文件避免网络依赖。
- file: `Plugins/DbAdmin/plugin.json`
  reason: 行为/契约变化，版本 0.1.3 → 0.1.4。
- file: `Plugins/DbAdmin/DbAdmin.csproj`
  reason: 更新驱动打包注释（预检已关闭）。
- file: `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`
  reason: 冒烟主链路增加「provider 下拉无『需驱动』标记」断言与截图（AC5 的机器判据）。
- file: `Plugins/DbAdmin/docs/02-交互说明.md`
  reason: 删除「需驱动」灰标与固定驱动缺失文案描述。
- file: `Plugins/DbAdmin/docs/03-架构与接口.md`
  reason: 更新驱动缺失说明与 `/providers` 字段语义。
- file: `Plugins/DbAdmin/docs/05-已知风险-多引擎与密码.md`
  reason: 追加决策 3（2026-11-06 暂时关闭驱动预检）并标注决策 1 暂时挂起。
- file: `Plugins/DbAdmin/docs/06-UI交互评审稿.md`
  reason: 测连结果矩阵同步（驱动缺失态仅预检开启时存在）。
- file: `Plugins/DbAdmin/docs/ui-design-brief.md`
  reason: 面向设计师的功能描述同步。
- file: `docs/02-features/044-db-admin.md`
  reason: 功能速查与「非目标-驱动在线下载」条目同步新行为。
- file: `docs/ai/pilot/2026-11-06-db-admin-driver-probe-off/`（00-07）
  reason: 本任务工件链。

## Implementation Steps

1. `DriverCatalog` 增加 `ProbeEnabled` 开关（false），注释写明用户输入2 与恢复方式。
2. `DbConnectionService.ListProviders`：关闭时 `DriverAvailable=true`。
3. `DbConnectionService.Resolve`：仅在开关开启时做探测拦截。
4. `DbConnectionService.Test`：仅在开关开启时做预检与 `driver_missing` 分类；关闭时失败回显底层消息。
5. `DbConnectionService.Wrap`：仅在开关开启时把驱动类异常映射为 `driver_missing`。
6. 更新 `DbConnectionServiceTests`：新增两个用例（provider 全可用、非 SQLite 不判 driver_missing）。
7. 同步插件文档 + 功能文档 044。
8. 跑门禁与证据记录（05/06/07）。

## Test Plan

1. 后端定向：`dotnet test --filter "FullyQualifiedName~DbAdmin"`；重点覆盖 `DbConnectionServiceTests`。
2. 插件前端：`cd Plugins/DbAdmin/web && pnpm run test && pnpm run build`。
3. 宿主构建：`dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj`（插件 DLL 进宿主产物）。
4. 插件冒烟 e2e：`ForgeSelf.Web` 下跑 `e2e/plugins/db-admin`（1 条主链路）。
5. dev-stack 走查：打开 `/db-admin` 连接表单，确认无「需驱动」标记；截图读图；用 MySQL 测连一次记录失败文案（不要求成功）。
6. 静态守卫：`cd ForgeSelf.Web && pnpm run design:check`（本批未改宿主前端样式，仍按门禁跑）。

## Verification

### Build

```bash
cd ForgeSelf.Api && dotnet build
cd Plugins/DbAdmin/web && pnpm run build
```

### Unit Test

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~DbAdmin"
cd Plugins/DbAdmin/web && pnpm run test
```

### Integration Test

```bash
# 无独立集成测试工程；插件 HTTP 契约由 ForgeSelf.Api.Tests/Plugins/DbAdminTests 覆盖
```

### E2E

```bash
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

### Other Checks

```bash
cd ForgeSelf.Web && pnpm run design:check
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-06-db-admin-driver-probe-off -EnforceInteraction
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-11-06 09:2x | 非 SQLite 测连单测会真实发起网络连接（XCode 自动加载驱动/连接超时），单跑全绿但一组内偶发失败 | 用 mysql/postgresql/sqlserver 三 provider 直接 `Test` 断言 `sql_error` | 改为网络无关用例：`Resolve_NonSqlite_NoDriverGate`（只验证不再被门拦截）+ SQLite 缺文件测连（验证 `sql_error` + 非空 message）；真实 XCode 驱动行为放到 dev-stack API 实测记录 |
| 2026-11-06 09:3x | 契约测试 `TestResult_OkPlusCode` 用 mysql 实连会引入环境依赖 | 保留 mysql 断言 | 改 SQLite 缺文件路径，契约形态不变 |
| 2026-11-06 09:4x | 行为/契约变化需版本标注 | Plan 未列 plugin.json | bump `0.1.3 → 0.1.4` 并补 e2e 断言 |
| 2026-11-06 09:1x~09:4x | e2e globalSetup 清理段 `cmd /c timeout` 在本机非交互 stdout 下恒失败；残留宿主句柄导致重试路径被触发 | — | 记录为环境/基建问题（见 05 Known Limitations），清理残留宿主后按单次成功清理路径复跑通过；不本次修改 globalSetup |
