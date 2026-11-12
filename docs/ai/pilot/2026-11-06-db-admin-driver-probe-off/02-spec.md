# Specification — db-admin 暂时关闭驱动预检

> 阶段：Stage 2｜Task ID：`2026-11-06-db-admin-driver-probe-off`｜日期：2026-11-06

## Functional Requirements

- FR1 `DriverCatalog.ProbeEnabled=false` 时，`DbConnectionService.ListProviders()` 对所有 provider 返回 `DriverAvailable=true`；provider 目录（id/displayName/tested/driverPackage/defaultPort）不变。
- FR2 `Resolve(id)` 不再因驱动探测结果抛 `driver_missing`；继续按原逻辑注册 `DbAdminU_<id>` 交给 XCode。
- FR3 `Test(input)` 不再做驱动预检；直接 `DAL.AddConnStr + DAL.Create(...).Db.ServerVersion` 尝试连接。
- FR4 `ProbeEnabled=false` 时，测连失败一律返回 `sql_error` + 底层异常消息（经 `SqlRedactor.RedactText`）；不按异常文本推断 `driver_missing`。
- FR5 `ProbeEnabled=true` 时保持原行为（预检拦截 + `driver_missing` + 驱动包提示），开关单点切换即可恢复。
- FR6 SQLite 文件必须已存在的守卫（`EnsureSqliteFileExists`）与系统库文件防绕过守卫（`GuardNotSystemFile`）不变。

## Input

- `GET /api/db-admin/providers`（无参数）
- `POST /api/db-admin/connections/test`，body=`ConnectionInput`（provider/host/port/database/userName/password/mode/connStr/id?）
- 内部 `Resolve(connectionId)`（由 `/connections/{id}/tables`、`/query` 等入口调用）

## Output

- `/providers`：`ProviderInfo[]`，camelCase：`{id, displayName, driverAvailable, tested, driverPackage, defaultPort}`。
- `/connections/test`：恒 200，`{ok, code, message, serverVersion, elapsedMs}`；`code ∈ ok | sql_error | driver_missing(仅预检开启)`。
- `Resolve`：`ResolvedConnection(Id, ConnName, IsSystem, Readonly, Provider)`。

## Business Rules

- 驱动预检是「前置门」，不是连接能力的唯一判定；关闭后由 XCode 在连接期决定如何获取驱动。
- 驱动缺失错误码 `driver_missing`（HTTP 424）契约保留，但预检关闭时不主动产生。
- 异常消息出口必须脱敏，不得泄露密码/连接串。
- 系统只读连接、只读硬拒、审批、审计逻辑不受影响。

## Boundary Conditions

- SQLite 目标文件不存在 → 仍返回 `sql_error`「SQLite 文件不存在」，且不创建文件。
- 非 SQLite 目标不可达/驱动缺失/认证失败 → 统一 `sql_error` + 底层错误消息（预检关闭时）。
- 系统连接（`sys:<ConnName>`）走既有只读分支，不进入用户连接驱动路径。
- `ProbeEnabled=true` 还原后：无驱动 → 测连返回 `driver_missing` + 包名；`ListProviders` 对应 `false`。

## Error Handling

| 场景 | 预检关闭（本批） | 预检开启（还原后） |
| --- | --- | --- |
| provider 不支持 | `invalid_request` 400 | 同左 |
| 驱动 DLL 不存在 / 加载失败 | `sql_error` 400 + 底层消息 | `driver_missing` 424 + 包名 |
| 网络/认证/超时 | `sql_error` 400 + 底层消息 | 同左（仅异常类型匹配驱动时例外） |
| SQLite 文件不存在 | `sql_error` 400 | 同左 |

## Compatibility

- 前端 `normalize.ts` / `DbAdminView.vue` 不需改动：`driverAvailable=true` 时「需驱动」标记自然消失；`driver_missing` 分支保留（未来开启预检仍可用）。
- `DriverCatalog.IsDriverException` / `DriverMissingMessage` / `IDriverProbe` / `DefaultDriverProbe` 保留导出，测试夹具 `SqliteOnlyProbe` 不受影响。
- 插件版本号本次不 bump（未发布；发布链会按打包流程处理）。

## Non-functional Requirements

- 不新增依赖、不改 `DbAdmin.csproj`。
- 改动为常量开关 + 三处条件判断，无性能影响。
- 代码注释写明「2026-11-06 用户输入2 暂时关闭」及恢复方式。

## Interaction Design（交互设计 · 必写节）

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| provider 下拉 | 打开连接表单 | 选项均为 `displayName`，不再出现「（需驱动）」后缀；MySQL/PostgreSQL/SQL Server 仍可选 | 全部可选（预检关闭） | 无额外提示 | 不适用 | provider 仅 4 项，顺序不变 |
| 表单「需驱动」灰标 | 选择非 SQLite provider | 灰标不显示 | 仅当 `driverAvailable=false` 显示 | 不适用 | 不适用 | 预检开启还原后恢复显示 |
| 测试连接失败 | 点「测试连接」且连接失败 | 结果区红色提示，内容为后端返回的底层错误（已脱敏）；不再显示固定的「驱动缺失」文案 | 测连中 / 成功 / 失败 / 驱动缺失（仅预检开启） | 失败留窗可读，不自动关闭 | 不适用 | 错误文本过长时按容器换行，不撑破布局 |
| SQLite 主链路 | 选择 SQLite + 已有库文件 | 与改动前一致：成功显示服务器版本，失败显示原因 | 五态不变 | 同左 | 同左 | 文件不存在仍报错 |

### 视觉规格（目标态）

- 本批不新增/修改样式，仅减少两处条件渲染（下拉后缀 + 灰标）；沿用 `--el-*` token 与现有表单布局。
- 主题矩阵：本批不触及任何颜色/背景/边框值，亮/暗两态均沿用既有 token（`--el-text-color-secondary`、`--el-fill-color-light` 等）。

### 交互验收标准

- AC2/AC5：provider 下拉与表单无「需驱动」标记；测连失败显示后端 message 原文（脱敏后）。

### 走查符合性（DoD 绑定）

- 涉及 UI 表现变化，走查（dev-stack）按 `ui-ux-design` 走查清单核对表单五态与文案；截图存 `ForgeSelf.Web/screenshots/dev/db-admin/`。

## Acceptance Criteria

- AC1 `GET /providers` 所有 provider `driverAvailable=true`（预检关闭状态），且 `id/displayName/tested/defaultPort` 与改动前一致。
- AC2 非 SQLite 测连不再返回 `driver_missing`；返回 `sql_error` 且 `message` 非空、已脱敏（不含密码）。
- AC3 `Resolve` 不为驱动探测拦截（以单测/API 验证非 SQLite 连接可进入解析）。
- AC4 SQLite 测连成功 + 缺文件报错两条既有用例仍绿（无回归）。
- AC5 插件前端 `pnpm run test` + `pnpm run build` 绿；界面上 provider 无「需驱动」标记（截图）。
- AC6 插件文档与功能文档已同步（docs/02、03、05、06、ui-design-brief、044）。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| XCode 在本机是否真的会自动下载非 SQLite 驱动 | 影响「开箱可用」体验，不影响本批「取消预检 + 回显错误」的验收 | 本批不依赖下载成功：测试断言失败路径；真机若下载成功即为加分项，失败按错误信息回显 |
