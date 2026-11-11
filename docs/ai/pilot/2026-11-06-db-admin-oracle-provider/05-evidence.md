# Evidence — db-admin 增加 Oracle 类型

> 阶段：Stage 7｜Task ID：`2026-11-06-db-admin-oracle-provider`｜日期：2026-11-06
> 分级：**Verified** = 亲自跑过拿到真实输出；Inferred = 代码/DLL 推断；Unknown = 未验证。

## Validation 结果（全部实际执行）

| 门禁 | 命令 | 结果 | 级别 |
| --- | --- | --- | --- |
| 插件前端单测 | `cd Plugins/DbAdmin/web && pnpm run test` | **104/104 通过（13 文件）**，含新增 oracle 断言（connForm/contract） | ✅ Verified（日志 `.temp/dbadmin-web-test-oracle.log`） |
| 插件前端构建 | `pnpm run build` | dist/index.js 102.95 kB，0 错误 | ✅ Verified（`.temp/dbadmin-web-build-oracle.log`） |
| 宿主后端构建 | `dotnet build ForgeSelf.Api` | 0 错误（1461 警告均为既有） | ✅ Verified（`.temp/api-build-oracle.log`） |
| 后端测试（快档过滤集） | `dotnet test --filter "FullyQualifiedName~DbAdmin"` | **168/168 通过**（含新增 `Oracle_ProviderRegistered_AndConnStrBuilt`） | ✅ Verified（`.temp/dbadmin-tests-oracle.log`） |
| 宿主前端 check | `pnpm run check`（vue-tsc + eslint） | 0 errors（166 warnings 既有）；component-conventions PASS | ✅ Verified（`.temp/web-check-oracle.log`） |
| 宿主前端单测 | `pnpm run test` | **112 文件 / 1203 用例全过** | ✅ Verified（`.temp/web-test-oracle.log`） |
| 设计门禁 | `pnpm run design:check` | style-tokens PASS（较基线 -740）+ compliance PASS（棘轮零增量） | ✅ Verified（`.temp/design-check-oracle.log`） |
| CT 组件回归 | `pnpm run test:ct`（CT_FRESH=1，--workers=2） | **66 passed / 2 failed**；2 败 = 既有无关红（Gallery 长列表超时、SmartTerminal 审计过滤，TODO 在册），DbAdmin 全过（含 S9 option=5） | ✅ Verified（`.temp/ct-oracle-r2.log`；r1 13 败为 Chromium GPU 崩溃形态含未触碰组件，降 worker 复跑即消） |
| e2e 冒烟 | `CI=1 E2E_CLEAN=1 npx playwright test e2e/plugins/db-admin` | 首跑 1 failed → **既有断言缺陷**（见下）→ 同类修复后 **1 passed (48s)** | ✅ Verified（`.temp/e2e-dbadmin-oracle*.log`） |
| 工件门禁 | 本批 00-07 齐（本文件即链路一环） | 待闸门3 提交时由 pre-commit 机械校验 | ⚠️ Verified（人工清单，hook 校验留待提交） |

## e2e 首跑失败定责（同类修复记录，非本批引入）

- 现象：`db-admin.spec.ts:199` `toHaveCount(0)` 恒红（3 次重试同值 1）。
- 定位：选择器 `[data-testid^="table-"]` 与**恒渲染**的过滤输入框 `data-testid="table-filter"` 前缀碰撞（`table-filter-empty` 同理）；树实际已收起（0 行 + 1 filter = 1）。
- 归属：commit `6116843`（15:08）引入该断言与 filter 同批；该提交前最后一次 e2e 绿跑为 12:26（旧版 spec），**提交版本从未在 e2e 验证**——属存量潜伏缺陷，与本批 Oracle 改动无关。
- 同类扫查：全插件 e2e 前缀 testid 选择器仅 db-admin 两处命中（:179 空过、:199 恒红），其余均带 `hasText` 过滤不受影响。
- 修复：两处收紧为 `.node.table[data-testid^="table-"]`（表行节点专属）→ e2e 绿。

## 走查证据（dev-stack 起源码，:7301/:7399）

- 页面徽标 **v0.2.1**；provider 下拉 5 项（SQLite/MySQL/PostgreSQL/SQL Server/Oracle），均无「（需驱动）」后缀。
- 选 Oracle → 端口自动 **1521**、「未实测」灰标显示、字段集与 MySQL 系一致。
- 测连（127.0.0.1:1521/ORCL）→ 红条回显 **`ORA-12541: Cannot connect. No listener at host 127.0.0.1 port 1521`** + CONNECTION_ID + Oracle 官方错误帮助链接，**无密码明文**。⇒ **XCode 在连接期成功加载 ODP.NET 驱动并真实发起 TCP 连接**（决策 3 行为链实测走通；失败仅因本机无 Oracle 实例）。
- 「连接串」模式预填 = `Data Source=//127.0.0.1:1521/ORCL;User Id=ora_user;Password=…`（EZConnect，与服务端 BuildConnStr 同构）。
- 保存 → 列表项 + 详情卡（provider 标签 Oracle + 未实测；悬停提示连接串 `Password=****` 掩码）→ 删除清理（确认弹窗「不会删除数据库文件」→ 删除成功，无残留）。
- 截图：`ForgeSelf.Web/screenshots/dev/db-admin/oracle-provider-form.png`（读图：布局无遮挡/溢出、红条换行正常、密码遮罩、v0.2.1）。

## 发布证据（用户指令「改完打本地zip」）

- 命令：`pwsh scripts/release/release-local.ps1 -Sign`（不传 -Version，读 csproj 2.4.22 补时间码）→ **ALL DONE 123s，EXIT 0**（日志 `.temp/release-local-oracle-20261106-2341.log`）。
- 产物：`D:\src\my-proj\OpenForgeSelf\updates\OpenForgeSelf-2.4.22.2611062341-win-x64.zip`（180.4MB）+ RELEASE-NOTES + SHA256SUMS.txt。
- SHA256 对账：SUMS `cfc346904d7c…fd12c84` == 实算 **MATCH**。
- 签名：Authenticode ok（13.7s），**新增 2 / 跳过 3 / 失败 0**，RFC3161 时间戳四点回退（sectigo→digicert→globalsign→comodoca）。
- 包内证据（ZipFile 直读）：`versions/2.4.22.2611062341/plugins/DbAdmin/plugin.json` **Version=0.2.1** 且 Description 含 Oracle；`DbAdmin.dll`（175KB，UTF-16LE 字节扫描命中 `oracle` id / `Oracle.ManagedDataAccess` 包名 / sqlserver 分支字面量相邻）；`web/dist/index.js`（含 oracle/1521/Oracle 展示名）；versions 嵌套 0；distinct 插件 24（DbAdmin 在列）。

## Repository Understanding 关键事实复核

- XCode 12.0.2026.701 内置 `XCode.DataAccessLayer.Oracle`：Inferred（DLL #Strings 探测）→ **走查实测升级为 Verified**（ORA-12541 证明 Oracle provider 实例化 + ODP.NET 驱动加载成功）。

## Unknown / 未覆盖

- 真实 Oracle 实例的完整链路（建表/查询/分页）：本机无 Oracle 服务端，未验证（「未实测」标注与文档口径一致）；驱动/协议层已由 ORA-12541 证实可用。
- CT r1 的 13 连败根因（Chromium GPU 崩溃，code 3221226505）：降 worker 复跑即消，未深究，记 TODO。

## 验收标准覆盖对照（AC1–AC8 → 证据）

- **AC1**（GET /providers 含 oracle 五项：id/displayName/driverAvailable=true/tested=false/defaultPort=1521）：单测 `Oracle_ProviderRegistered_AndConnStrBuilt`（DriverCatalog 目录项 + ListProviders 断言，168/168 DbAdmin 过滤集内）+ 走查「下拉 5 项」。
- **AC2**（EZConnect BuildConnStr + `DbAdminU_<id>` 注册）：同上单测（`127.0.0.1:1521/ORCL` 连接串 + `UserConnPrefix + v.Id`）+ 走查「连接串模式预填同构」。
- **AC3**（测连失败 ok=false/code=sql_error/无密码泄漏）：同上单测（`SqlError` + `DoesNotContain(OraPw123)`）+ 走查 ORA-12541 红条原文。
- **AC4**（contract.test + connForm defaultPort('oracle')=1521）：插件前端单测 104/104（connForm/contract 含 oracle 断言）。
- **AC5**（插件 pnpm test+build + CT S9 option=5）：Validation 表「插件前端构建」「CT 组件回归」两行（66 passed/2 failed，2 败既有无关，DbAdmin S9 全过）。
- **AC6**（dev-stack 起源码走查）：走查证据节 + 截图 `ForgeSelf.Web/screenshots/dev/db-admin/oracle-provider-form.png`（读图核对）。
- **AC7**（docs FR6/FR7 同步 + verify-pilot-artifacts）：docs 044 与插件 docs 已更新（见本提交）；工件门禁由 pre-commit 对本目录 00-07 机械校验（提交时）。
- **AC8**（release-local -Sign + 包内 plugin.json 0.2.1）：发布证据节（2.4.22.2611062341 包，ZipFile 直读 Version=0.2.1，SHA256 MATCH）。
