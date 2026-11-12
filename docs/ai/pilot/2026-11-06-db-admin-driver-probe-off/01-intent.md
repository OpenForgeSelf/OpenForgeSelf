# Intent — db-admin 暂时关闭驱动预检

> 阶段：Stage 1｜Task ID：`2026-11-06-db-admin-driver-probe-off`｜日期：2026-11-06

## Problem

db-admin 在连接/测连前用 `DefaultDriverProbe` 探测本地是否存在 MySQL/PostgreSQL/SQL Server 驱动 DLL；探测不到就直接返回 `driver_missing`（HTTP 424）并提示用户手工放 DLL。用户已确认 XCode 默认会在真正连接时自行获取驱动，前置探测反而把可用路径拦死；且失败时只回显固定的「驱动未安装」文案，掩盖了真实错误。

## Why

用户明确要求（输入2）：「数据库插件，暂时屏蔽掉插件检查，XCode默认会下载驱动，如果连接失败则提示，能根据错误信息提示是驱动还是其他问题另说，先取消掉驱动校验」。当前行为与用户预期相反，非 SQLite 引擎无法直接尝试连接。

## Expected Outcome

1. `GET /api/db-admin/providers` 返回所有 provider `driverAvailable=true`（前端不再显示「需驱动」标记）。
2. `POST /api/db-admin/connections/test` 与连接解析不再被驱动预检拦截；直接交给 XCode 尝试连接。
3. 连接失败时返回 `sql_error` 并回显底层错误（脱敏），不把异常文本硬翻译成「驱动未安装」。
4. 保留驱动预检代码与 `driver_missing` 契约，便于后续按错误信息细分或重新开启预检。
5. 现有 SQLite 主链路、审批、脱敏、审计行为不变。

## Constraints

- 不随插件打包任何驱动；不改 `DbAdmin.csproj` 依赖。
- 不修改数据库结构、不改鉴权/权限逻辑。
- 不删除 `IDriverProbe`/`DefaultDriverProbe`/`DriverMissingMessage`（「暂时」关闭，保留恢复能力）。
- 不扩范围：不改前端渲染逻辑（后端返回 `true` 即可让标记消失）。
- 发布动作（本地目录更新源/宿主升级）须另行征得用户同意；本次不停/启/杀用户宿主进程。

## Success Criteria

- 代码中三处驱动预检（`ListProviders`/`Resolve`/`Test`）与异常分类均由 `DriverCatalog.ProbeEnabled` 开关控制，当前为 `false`。
- 定向测试：`dotnet test --filter "FullyQualifiedName~DbAdmin"` 全绿（含新增「provider 全部可用」「非 SQLite 不再判 driver_missing」用例）。
- 插件前端构建绿；插件冒烟 e2e 主链路仍绿（或如实记录环境阻塞）。
- 插件文档（docs/02/03/05/06/ui-design-brief + 功能文档 044）与实现一致。
