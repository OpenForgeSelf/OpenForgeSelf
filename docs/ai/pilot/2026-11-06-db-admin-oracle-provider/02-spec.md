# Specification — db-admin 增加 Oracle 类型

> 阶段：Stage 2｜Task ID：`2026-11-06-db-admin-oracle-provider`｜日期：2026-11-06

## Functional Requirements

- FR1 `DriverCatalog.Providers` 新增第 5 项：`new("oracle", "Oracle", "Oracle", 1521, false, "Oracle.ManagedDataAccess", ["Oracle.ManagedDataAccess.Client.OracleClientFactory, Oracle.ManagedDataAccess"])`；`Find("oracle")` 与大小写变体（`Oracle`/`ORACLE`）可命中。
- FR2 `DbConnectionService.BuildConnStr` 新增 `case "oracle"`（form 模式）：`Data Source=//{host}:{port}/{database}`（port 缺省 1521）+ `User Id={userName}` + `Password={pwd}`；connstr 模式原样透传（既有逻辑）。
- FR3 前端 `connForm.ts`：`DEFAULT_PORTS.oracle=1521`、`PROVIDER_LABELS.oracle='Oracle'`；`buildConnStr` oracle 分支输出与 FR2 同构（`Data Source=//h:1521/d;User Id=u;Password=p`），供「切连接串模式」预填。`types.ts` `ProviderId` 联合加 `'oracle'`。
- FR4 契约样例同步：`backend-samples.json` providers.body 增 oracle 项（按当前后端真值：`driverAvailable:true, tested:false, driverPackage:'Oracle.ManagedDataAccess', defaultPort:1521`）；CT story providers fixture 增 oracle 项（其余 4 项保持演示值不动）。
- FR5 CT 断言同步：`DbAdmin.ct.spec.ts` `cf-provider` option 数 4 → 5。
- FR6 文档同步：`Plugins/DbAdmin/docs/{00,01,02,03,05,README}`、`docs/02-features/044-db-admin.md` provider 清单补 Oracle（标「未实测」）；`ui-design-brief.md` 如有 provider 清单一并补。
- FR7 `plugin.json` Version `0.2.0 → 0.2.1`，Description 补 Oracle（未实测口径）。
- FR8 Oracle 在无驱动/无实例环境的行为与其他非 SQLite 引擎一致：测连/解析/查询失败 → `sql_error`（预检关闭）+ 脱敏底层消息；不产生 `driver_missing`。

## Input

- `GET /api/db-admin/providers`（无参）
- `POST/PUT /api/db-admin/connections`（body `ConnectionInput`，provider=oracle）
- `POST /api/db-admin/connections/test`；`/connections/{id}/tables`、`/query`（经 `Resolve`）
- 表单前端状态：provider 下拉选择、`onProviderChange` 端口联动

## Output

- `/providers`：5 项 `ProviderInfo[]`（camelCase，结构不变）。
- oracle 连接串（服务端构造，仅内存）：`Data Source=//host:port/service;User Id=…;Password=…`。
- 前端：下拉含 `Oracle` 选项；选择后表单字段与 MySQL 系一致、端口预填 1521、「未实测」标签显示。

## Business Rules

- provider id 统一小写 `oracle`（与既有约定一致）；`Find` 大小写不敏感。
- 「未实测」标注：oracle `Tested=false`，连接详情与表单显示「未实测」标签——不得给用户「已验证可用」暗示。
- 驱动策略不变：不随插件打包 Oracle 驱动；预检关闭时连接期失败按 `sql_error` 回显（脱敏）。
- 密码/连接串出口脱敏与掩码规则不变（`SqlRedactor` / `MaskConnStr`）。
- 系统连接、审批、审计、SqlGuard、多语句切分等逻辑不受 provider 扩展影响（均与引擎无关）。

## Boundary Conditions

- 表单模式缺 host 或 database → 既有校验拒绝（「需填写主机」「需填写数据库名」），oracle 无特殊分支。
- port=0/未填 → 连接串用默认 1521。
- connstr 模式（TNS 别名 / 完整 DESCRIPTION 形态）原样透传，不做解析改写。
- `Find(null)` / 未知 provider → 既有 `invalid_request` 路径不变。

## Error Handling

| 场景 | 行为 |
| --- | --- |
| 无 Oracle 客户端驱动（本机常态） | 测连/查询抛底层异常 → `sql_error` 400 + 脱敏消息（预检关闭，不判 driver_missing） |
| Oracle 实例不可达 / 认证失败 | 同上 `sql_error` + 底层消息 |
| 表单缺字段 | 400 `invalid_request`（文案同其他引擎） |
| 连接名重复 | 400 `invalid_request`「连接名已存在」 |

## Compatibility

- `ProviderId` 为开放联合（`(string & {})`），存量连接与既有 4 provider 契约零变化；新增 id 纯增量。
- CT `ProviderDriverMissing` story 演示 fixture 不动（演示「需驱动」UI 态，非后端契约）。
- 前端 `normalize.ts` / 视图层零改动（数据驱动）。

## Non-functional Requirements

- 零新 NuGet 依赖；插件产物体积不变（不加驱动 DLL）。
- 后端新增代码 ≤2 处小改（目录项 + switch 分支），前端 ≤3 处枚举/映射。

## Interaction Design（交互设计 · 必写节）

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| provider 下拉 | 打开新建/编辑连接表单 | 第 5 项 `Oracle` 出现，均无「（需驱动）」后缀（预检关闭） | 5 项均可选 | 无额外提示 | 不适用 | 顺序 = 后端数组序（oracle 在末尾） |
| 选择 Oracle | 下拉选 oracle | 端口自动预填 1521；字段集与 MySQL 系一致（host/port/database/userName/password）；「未实测」灰标显示 | form/connstr 两模式可用 | 端口联动（既有 `onProviderChange`） | 不适用 | database=服务名，placeholder 沿用 |
| 切「连接串」模式 | 模式切换 | 预填 `Data Source=//host:1521/db;User Id=…`（oracle 分支）；输入框遮罩 | 预填可改 | 同既有 | 不适用 | host 未填时输出 `//`:1521/db（用户改串） |
| Oracle 连接详情 | 选中已存 oracle 连接 | 详情卡 provider 标签显示 `Oracle` + 「未实测」tag | 只读展示 | 同既有 | 不适用 | — |
| Oracle 测连失败 | 点「测试连接」且本机无驱动/无实例 | 结果区红条：`sql_error` + 脱敏底层错误原文；不显示「驱动缺失」固定文案 | 失败留窗可重试 | 失败留窗 | 不适用 | 长错误按容器换行 |

### 视觉规格（目标态）

- 零新增样式/颜色：全部沿用既有 `--el-*` token、`.tag-untested`「未实测」样式（与 MySQL/PG/SQLServer 一致）。
- 主题矩阵：亮/暗两态均无新色值（PC-025 lens：无新 token 引用、无新裸色值）。

### 交互验收标准

- 见 AC5（下拉含 Oracle、无「需驱动」）与 AC6（测连失败显示脱敏原文、未实测标签在）。

### 走查符合性（DoD 绑定）

- dev-stack 起源码前后端走查：新建 Oracle 连接（表单）→ 校验/保存/测连失败路径 → 截图存 `ForgeSelf.Web/screenshots/dev/db-admin/`；按 `ui-ux-design` 清单核对表单五态与标签。

## Failure Modes（v1.2.0 节）

- 命中 PC：无（00 §适用问题类已核）。预防性检查：① 不新增裸色值（design:check 棘轮守护）；② 不新增 `<table>`/工具类名（既有守卫覆盖面不变）；③ 前后端 provider 枚举新增后，contract.test 与 CT option 数断言必须同步改，防「绿但样本过期」。

## Acceptance Criteria

- AC1 `GET /providers` 返回 5 项，含 `{id:'oracle', displayName:'Oracle', driverAvailable:true, tested:false, defaultPort:1521}`（后端单测断言；顺序末位）。
- AC2 oracle 表单连接 Create → `BuildConnStr` 输出 EZConnect 形态（含 host:port/service、User Id、Password）；`Resolve` 注册 `DbAdminU_<id>`（单测）。
- AC3 oracle 测连（本机无驱动/实例）返回 `ok=false, code=sql_error, message` 非空且不含密码明文（单测）。
- AC4 前端：`contract.test` provider 清单含 oracle；`connForm` `defaultPort('oracle')=1521`、`providerLabel('oracle')='Oracle'`、`buildConnStr` oracle 形态正确。
- AC5 插件前端 `pnpm run test` + `pnpm run build` 绿；CT S9 option 数断言更新为 5 且 CT 相关用例绿。
- AC6 dev-stack 走查截图：下拉含 Oracle、未实测标签在、测连失败显示脱敏原文。
- AC7 文档同步（FR6/FR7 清单）完成；`verify-pilot-artifacts` 通过。
- AC8 `release-local.ps1 -Sign` 产物落 updates 目录，包内 db-admin plugin.json = 0.2.1。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| XCode "Oracle" provider 名到 `XCode.DataAccessLayer.Oracle` 类的解析细节 | 若解析失败，测连异常类型/消息不同 | 单测断言「失败必有 sql_error + 非空消息」覆盖两种失败形态；实机有驱动属加分项 |
| 本机无 Oracle 驱动时的具体异常消息 | 只影响走查截图文案 | 断言「非空 + 脱敏」而非具体文案 |
