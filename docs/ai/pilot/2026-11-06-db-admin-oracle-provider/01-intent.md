# Intent — db-admin 增加 Oracle 类型

> 阶段：Stage 1｜Task ID：`2026-11-06-db-admin-oracle-provider`｜日期：2026-11-06

## Problem

数据库管理插件（db-admin）当前只支持 SQLite / MySQL / PostgreSQL / SQL Server 四类 provider，用户需要连接管理 **Oracle** 数据库（表单/连接串建连、测连、Schema 浏览、查询）。

## Why

用户直接指令（2026-11-06 23:16）：「数据库管理插件，增加oracle类型，改完打本地zip」——扩展 DbAdmin 的多引擎覆盖；XCode 12.0 已内置 Oracle provider（`XCode.DataAccessLayer.Oracle`），扩展成本与既有 4 provider 同构。

## Expected Outcome

1. `/providers` 返回含 `oracle`（display "Oracle"，defaultPort 1521，tested=false）共 5 项。
2. 新建/编辑连接表单可选 Oracle：表单模式（host/port/database=服务名/user/password）与连接串模式均可保存；测连、Schema、查询链路与其他非 SQLite 引擎一致（预检关闭，失败回显脱敏底层错误）。
3. 前端契约样例（backend-samples.json / CT story fixture）与文档同步含 Oracle；插件版本 bump 并更新描述。
4. 门禁全绿后打本地 zip（用户已明确授权）。

## Constraints

- **不打包 Oracle 驱动**：沿用 2026-11-06 驱动决策（预检关闭、XCode 连接期自行获取/由用户放置 DLL），`DbAdmin.csproj` 零 NuGet 变更。
- **仅 SQLite 实测**原则不变：Oracle 标「未实测」，验收不含真实 Oracle 实例（本机无 Oracle 环境，测试断言失败路径）。
- 不改 `/providers` 响应结构、不改鉴权/审批/审计/SqlGuard 逻辑；不新增依赖；不动 :51888 用户实例。

## Success Criteria

- AC1-AC8（见 02-spec §Acceptance Criteria）全部通过；`release-local.ps1 -Sign` 本地 zip 产物落 `D:\src\my-proj\OpenForgeSelf\updates` 且包内 db-admin 清单为新版本。
