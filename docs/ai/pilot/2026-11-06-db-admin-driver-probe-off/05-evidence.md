# Evidence — db-admin 暂时关闭驱动预检

> 阶段：Stage 7｜Task ID：`2026-11-06-db-admin-driver-probe-off`｜日期：2026-11-06
> 只记录实际发生的事；来源等级：Verified（亲自跑过且有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。

## Task

`2026-11-06-db-admin-driver-probe-off`

## Changed Files

- `Plugins/DbAdmin/Services/DriverCatalog.cs`：新增 `ProbeEnabled=false` 常量 + 注释（暂时关闭原因/恢复方式）。
- `Plugins/DbAdmin/Services/DbConnectionService.cs`：`ListProviders`/`Resolve`/`Test`/`Wrap` 四处预检与异常分类由开关控制；新增 `ErrorMessageOf` 保证失败必有非空提示。
- `Plugins/DbAdmin/plugin.json`：版本 `0.1.3 → 0.1.4`。
- `Plugins/DbAdmin/DbAdmin.csproj`：驱动打包注释同步。
- `ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbConnectionServiceTests.cs`：新增 `ListProviders_AllAvailable_WhenDriverProbeDisabled`、`Resolve_NonSqlite_NoDriverGate`。
- `ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbAdminContractTests.cs`：`TestResult_OkPlusCode` 改为 `sql_error` + 非空 message（SQLite 缺文件，无网络依赖）。
- `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`：冒烟主链路新增「provider 下拉无『需驱动』」断言 + 截图。
- `Plugins/DbAdmin/docs/02-交互说明.md`、`03-架构与接口.md`、`05-已知风险-多引擎与密码.md`（新增决策 3）、`06-UI交互评审稿.md`、`ui-design-brief.md`。
- `docs/02-features/044-db-admin.md`：定位/非目标/已知边界同步。
- 本工件目录 `docs/ai/pilot/2026-11-06-db-admin-driver-probe-off/`。

## Build

Command:

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -v q --nologo
```

Result: PASS（来源等级：Verified）

```text
BUILD_EXIT=0；已成功生成；352 个警告（均为既有警告）；0 个错误
产物 ForgeSelf.Api/bin/Debug/net10.0-windows/Plugins/DbAdmin/plugin.json 已为 Version=0.1.4
```

## Unit Test

Command:

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~DbAdmin" --nologo -v q
```

Result: PASS（来源等级：Verified）

```text
已通过! - 失败: 0，通过: 167，已跳过: 0，总计: 167，持续时间: 14 s
日志：.temp/tmp/dbadmin-tests-r6.log
```

> 首轮曾出现 `DbAdminContractTests.TestResult_OkPlusCode` 偶发失败（mysql 实连受环境影响），已按 Plan 偏差改为 SQLite 缺文件 + `sql_error` 失败形态，复跑两轮全绿。

## Integration Test

N/A。DbAdmin HTTP 契约由 `ForgeSelf.Api.Tests/Plugins/DbAdminTests`（含 `DbAdminContractTests`）覆盖；无独立集成测试工程。

## E2E

Command:

```bash
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

Result: PASS（来源等级：Verified）

```text
1 passed (1.0m)
日志：.temp/tmp/dbadmin-e2e-r5.log
用例：建连 → 测连 → 建表（批准）→ schema → SELECT → INSERT（拒绝）→ 清理
新增断言：provider 下拉选项文本不含「需驱动」；cf-needs-driver 元素计数为 0
```

插件前端（门禁）：

```bash
cd Plugins/DbAdmin/web && pnpm run test && pnpm run build
```

Result: PASS（来源等级：Verified）

```text
Test Files 9 passed (9)；Tests 61 passed (61)；WEB_TEST_EXIT=0
dist/index.js 68.65 kB；dist/style.css 12.43 kB；✓ built in 280ms；WEB_BUILD_EXIT=0
```

## Static Analysis

Command:

```bash
cd ForgeSelf.Web && pnpm run design:check
```

Result: PASS（来源等级：Verified）

```text
[style-tokens] PASS：无新增硬编码色值，较基线减少 740 处
[design-compliance] PASS：无新增设计规范违反（a=209 b=0 c=0 d=0，与基线一致）
```

## Runtime API（dev-stack 起源码验收）

Command（dev-stack 起后台 7301/7399，隔离数据根）：

```powershell
GET http://localhost:7301/api/db-admin/providers
POST http://localhost:7301/api/db-admin/connections/test  (mysql 127.0.0.1:3306)
```

Result: PASS（来源等级：Verified）

```text
GET /providers → 4 个 provider 全部 driverAvailable=true（sqlite/mysql/postgresql/sqlserver）
POST /connections/test (mysql) → { ok: false, code: "sql_error", message: "由于目标计算机积极拒绝，无法连接。", elapsedMs: 8628 }
```

结论：预检已关闭，XCode 成功加载/取得 MySQL 驱动并发起真实连接（因此报「目标计算机积极拒绝」而非「驱动未安装」）；失败回显底层错误且未泄露密码。

浏览器走查（browser_use，dev-stack 7399，真实点击）：

Result: PASS（来源等级：Verified）

```text
点击「+ 新建连接」→ 表单标题「新建连接」可见；
Provider 下拉选项文本 = SQLite / MySQL / MariaDB / PostgreSQL / SQL Server，均无「需驱动」后缀；
页面 innerText 搜索「需驱动」= false。
备注：浏览器工具默认截图目录未落到 ForgeSelf.Web/screenshots/dev/db-admin/，同场景截图以 e2e 产物为准。
```

## Screenshots

- `ForgeSelf.Web/screenshots/e2e/db-admin/02-provider-no-driver-tag.png`（新建连接表单，Provider 下拉；读图确认无「需驱动」灰标）
- `ForgeSelf.Web/screenshots/e2e/db-admin/01-loaded.png` … `07-rejected-clean.png`（冒烟主链路各步，本次全部重新生成）
- 读图结论：表单布局正常，无「需驱动」标签；Provider 下拉当前未选中时显示为空（既有行为，本批未触及）。

## Acceptance Criteria 对照

- AC1：`ListProviders_AllAvailable_WhenDriverProbeDisabled` 单测 + dev-stack `GET /providers` 实测全部 `driverAvailable=true`（Verified）。
- AC2：`TestResult_OkPlusCode` + `Resolve_NonSqlite_NoDriverGate` 断言失败为 `sql_error` 且 message 非空；dev-stack MySQL 实连实测 `sql_error`「目标计算机积极拒绝」且不含密码（Verified）。
- AC3：`Resolve_NonSqlite_NoDriverGate` 建 MySQL 配置后 `Resolve` 正常返回 `DbAdminU_<id>`（Verified）。
- AC4：`Test_Sqlite_Ok_And_RegistersIsolatedConnName`、`Test_MissingSqliteFile_Errors_AndDoesNotCreateFile` 均在 167/167 绿集中（Verified）。
- AC5：插件 web 61/61 绿 + build 绿 + e2e 新增断言绿 + 截图读图（Verified）。
- AC6：插件 docs/02、03、05、06、ui-design-brief 与 `docs/02-features/044-db-admin.md` 已同步（Verified by 文件内容）。

## Known Limitations

- e2e `global-setup.ts` 的 `rmDirOS` 重试分支使用 `cmd /c timeout /t 2 /nobreak`，在本机非交互 stdout 下报「Input redirection is not supported」；仅当目录首次删除失败（残留宿主句柄）时触发。本次清理残留宿主后单次删除成功、e2e 通过。属既有基建缺陷，未在本任务 Scope 内修改（已在 03-plan 偏差记录登记）。
- 未发布到用户 `:51888` 宿主（发布需用户授权；本任务只做源码改动 + 本地验证）。
- XCode 是否在纯净离线环境自动下载驱动未单独验证（本机已取得 MySQL 驱动并真实发起连接）；用户「另说」的『按错误信息区分驱动/网络/认证』未实现。

## Unresolved Issues

无（本任务范围内）。
