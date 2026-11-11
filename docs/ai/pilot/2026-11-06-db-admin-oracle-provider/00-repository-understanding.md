# Repository Understanding — db-admin 增加 Oracle 类型

> 阶段：Stage 0｜Task ID：`2026-11-06-db-admin-oracle-provider`｜日期：2026-11-06

## 仓库现状（实测读码）

- 插件位置：`Plugins/DbAdmin/`（plugin.json：Id=`db-admin`，当前 Version `0.2.0`，EntryAssembly `DbAdmin.dll`，前端 entry `web/dist/index.js`）。
- **provider 目录唯一真源**：`Plugins/DbAdmin/Services/DriverCatalog.cs` `Providers` 数组，当前 4 项：
  `sqlite / mysql / postgresql / sqlserver`，每项为 `ProviderDef(Id, DisplayName, XCodeProvider, DefaultPort, Tested, DriverPackage, FactoryTypes)`。
- `DriverCatalog.ProbeEnabled=false`（2026-11-06 用户输入2 暂时关闭驱动预检）：`ListProviders` 恒 `driverAvailable=true`；测连/解析不再拦驱动，失败回显底层错误（`sql_error` + 脱敏消息）。
- **连接串构造**：`DbConnectionService.BuildConnStr`（DbConnectionService.cs:252-279）按 provider switch 拼 `DbConnectionStringBuilder`；无 oracle 分支（default 抛 `invalid_request`）。
- **前端表单**：`Plugins/DbAdmin/web/src/connForm.ts`——`DEFAULT_PORTS`（mysql/postgresql/sqlserver）、`PROVIDER_LABELS`、`buildConnStr`（SQLite 与通用 Server=… 分支）、`validateForm`（非 SQLite 需 host+database）。
- **前端类型**：`types.ts:7` `ProviderId` 联合类型 `'sqlite' | 'mysql' | 'postgresql' | 'sqlserver' | (string & {})`。
- **视图层**：`DbAdminView.vue` provider 下拉由后端 `/providers` 数据驱动（:1415-1419），无 provider 硬编码分支；「未实测」标签由 `isTested` 驱动。
- **契约测试（前端）**：`contract.test.ts:22` 断言 provider 清单**恰好** `['sqlite','mysql','postgresql','sqlserver']`；样本 `__fixtures__/backend-samples.json`（「fixture 即契约样例」）providers.body 4 项。
- **CT 画廊**：`ForgeSelf.Web/src/components/DbAdmin.story.ts:20-25` providers fixture（注释「真实 DriverCatalog」）；`DbAdmin.ct.spec.ts:210` 断言下拉 `option` 数 **=4**；`ProviderDriverMissing` story（:223-228）依赖 providers 数组中 pg 的 `driverAvailable:false` 演示「需驱动」状态（演示性 fixture，预检关闭后端已不产生该态）。
- **e2e 冒烟**：`e2e/plugins/db-admin/db-admin.spec.ts:79-80` 断言下拉无「需驱动」后缀（数据驱动，新增选项不破坏）。
- **后端测试**：`ForgeSelf.Api.Tests/Plugins/DbAdminTests/`（fixture 隔离 SQLite；`DbConnectionServiceTests` 覆盖 CRUD/测连/解析；`Resolve_NonSqlite_NoDriverGate` 以 mysql 验证预检关闭路径）。

## XCode Oracle 支持（关键事实，决定可行性）

- 宿主引用 NewLife.XCode **12.0.2026.701**（Plugins/DbAdmin/DbAdmin.csproj）。
- 包 DLL 字符串探测（Verified）：含 `XCode.DataAccessLayer.Oracle`、`OracleSession`、`OracleMeta`、`DatabaseType`、`GetProviderType` 类型名；用户串含 `Oracle.ManagedDataAccess.dll`、`Oracle.ManagedDataAccess.Client.OracleClientFactory`、版本 `23.x`、`oracleclient`。
- 结论：XCode 内置 Oracle provider（走 ODP.NET Managed Driver，NuGet `Oracle.ManagedDataAccess`）；`DAL.AddConnStr(name, connStr, null, "Oracle")` 与既有 `MySql/PostgreSQL/SqlServer` 同构。驱动**不随插件打包**（与 05 文档决策1 一致），缺驱动时连接期失败回显（预检关闭）。
- Oracle 连接串（EZConnect 形态）：`Data Source=//host:port/service;User Id=xxx;Password=xxx`。

## 测试方式

- 后端：`dotnet build` + `dotnet test --filter FullyQualifiedName~DbAdmin`。
- 插件前端：`pnpm run test` + `pnpm run build`（ Plugins/DbAdmin/web）。
- 宿主前端（触及 DbAdmin.story.ts / ct.spec）：`pnpm run check` + `pnpm run test` + `pnpm run design:check` + CT `pnpm run test:ct`。
- 插件层 e2e：`e2e/plugins/db-admin`（零 mock，1 冒烟）。
- 走查：`scripts/dev-stack.ps1` 起源码前后端 + 截图读图。

## 适用问题类（problem-classes 检索）

- PC-025（暗色 light-N 倒置）：本批不新增任何颜色/样式值，全部沿用 token —— lens 检查通过（无新裸色值）。
- PC-026（插件 class 命中宿主 Tailwind 工具类）：本批不新增 table/CSS 类 —— lens 检查通过。
- 其余 PC 与本次触碰区域（provider 目录/连接串构造/前端枚举）无命中。

## 候选低风险任务与选择理由

用户直接指令唯一任务：**给 DbAdmin 增加 Oracle 类型**，完成后**打本地 zip**。完全跟随既有 4-provider 模式扩展，无新依赖（预检关闭下甚至无需 NuGet 包）、无 DB 迁移、无 API 契约破坏（`ProviderId` 为开放字符串联合，新增 id 向后兼容），风险 L1。
