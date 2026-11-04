> 产品经理请先读 [**00-目录.md**](./00-目录.md)。

# 数据库管理（DbAdmin）

可审批、可审计的 **SQL 工作台**插件。Id `db-admin` · toolPrefix `db` · ConnName `DbAdmin` · 默认权限 **AskWrite**。

## 行业对齐

| 能力 | 对标 |
|------|------|
| 连接 + schema 树 + 编辑器/结果格 | DBeaver / DataGrip / Beekeeper Studio 常用子集 |
| Agent 只读优先、写入审批 | VS Code Agent Approvals 分层思路；本仓 SmartTerminal AskWrite |
| 只追加审计 | SmartTerminal JSONL 审计口径 |

## 技术锁定

- 执行：XCode `DAL`（`DAL.AddConnStr` → `DAL.Create` → `Tables` / `Query` / `Execute`，参数化）  
- 系统库只读；用户连接 ConnName `DbAdminU_*`  
- 引擎：SQLite（实测）；MySQL / PostgreSQL / SQL Server 等（未实测，需额外 NuGet + `CopyLocalLockFileAssemblies`）

## Agent 工具

Must：`db_list_connections` / `db_list_tables` / `db_describe_table` / `db_query_readonly` / `db_execute_write`  
Should：`db_set_permission_mode` / `db_resolve_approval`  
实现：`Services/DbAdminToolFunctions.cs`（`DbAdminPlugin.Apply` 注册进 `ToolExtensions`）；契约与权限级别见 [04 §4](./04-权限与审计.md)；评测集见 [07](./07-Agent工具评测.md)。

## HTTP

前缀 `/api/db-admin/*`，类级 `ApiKeyPolicy`，详见 [03](./03-架构与接口.md)。

## 宿主登记

`XCodeConfig.PluginDbs["DbAdmin"]="db-admin"` · `ForgeSelf.Api.csproj` ProjectReference（`ReferenceOutputAssembly=false`） · `ForgeSelf.Api.Tests.csproj` + `ForgeSelf.Api.Tests/Plugins/DbAdminTests/` · `ToolNamespace.PrefixByPluginId["db-admin"]="db"` · CT `ForgeSelf.Web/src/components/DbAdmin.story.ts`

## 本地启用（实现后）

1. `dotnet build Plugins/DbAdmin/DbAdmin.csproj`  
2. `cd Plugins/DbAdmin/web && pnpm install && pnpm build`  
3. 宿主全量构建经 `StageAllPlugins` 暂存  
4. 插件管理页启用 **数据库管理**，打开 `/db-admin`

## CT 画廊

```bash
cd ForgeSelf.Web && pnpm run ct:server   # 侧栏 DbAdmin；深链 /?story=DbAdmin/Empty
```
