# Repository Understanding

> 阶段：Stage 0｜Task ID：`2026-11-14-db-admin-list-databases`｜日期：2026-11-14
> 依据：仓库实读，不凭常识。

## 项目结构

OpenForgeSelf（铸己匣）。解决方案 `ForgeSelf.slnx`。宿主 API `ForgeSelf.Api/`（.NET 10），宿主前端 `ForgeSelf.Web/`（Vue 3 + Vite + pnpm）。插件 `Plugins/DbAdmin/`（Id `db-admin`，版本 `0.2.1`，视图 `DbAdminView`）。测试：`ForgeSelf.Api.Tests/Plugins/DbAdminTests/`、插件前端 `Plugins/DbAdmin/web` 的 vitest、宿主 CT `ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts`、e2e `ForgeSelf.Web/e2e/plugins/db-admin/`。

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 插件后端 | .NET，XCode `DAL`，控制器 `DbAdminController` | `Plugins/DbAdmin/Controllers/DbAdminController.cs` |
| 插件前端 | Vue SFC + 手写 HTTP 客户端，无独立 UI 库封装在本页 | `Plugins/DbAdmin/web/src/DbAdminView.vue`、`api.ts` |
| 插件数据 | SQLite `DbAdmin.db`，密码 AES | `docs/02-features/044-db-admin.md` §6 |

## 架构特点

连接保存与枚举在 `DbConnectionService`。多库能力已在 2026-11-07 批落地后端：`GuardRequiredDatabase` 只强制 sqlite 文件路径与 oracle 服务名；`ListDatabases` 按引擎发只读枚举 SQL；`Resolve(id, database)` 为 mysql/sqlserver/postgresql 派生连接名切库。页面没有调用 `GET .../databases`。

## 测试方式

- 插件前端：`cd Plugins/DbAdmin/web && pnpm run test` 与 `pnpm run build`
- 后端本域：`dotnet test ForgeSelf.Api.Tests --filter DbAdmin`（本批默认不改后端行为，回归即可）
- 宿主 CT：`ForgeSelf.Web` 的 `DbAdmin.ct.spec.ts`（表单错误文案、点连接 loading）
- 插件冒烟 e2e：`ForgeSelf.Web/e2e/plugins/db-admin/`
- 快档另含宿主 `pnpm run check`、`pnpm run test`、`pnpm run design:check`（改了宿主 CT/story 时）

## 构建命令

```bash
cd Plugins/DbAdmin/web && pnpm run test && pnpm run build
dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~DbAdmin
```

## 主要目录职责

| 路径 | 职责 |
| --- | --- |
| `Plugins/DbAdmin/Services/DbConnectionService.cs` | 必填矩阵、连接串、列库、切库 |
| `Plugins/DbAdmin/web/src/connForm.ts` | 表单校验（当前非 sqlite 一律要库名） |
| `Plugins/DbAdmin/web/src/DbAdminView.vue` | 左树：点连接即 `loadTables` |
| `Plugins/DbAdmin/web/src/api.ts` | HTTP；无 `databases()` |
| `docs/02-features/044-db-admin.md` | 宿主功能登记；§8 是查询工作台竞品 backlog |
| `docs/ai/pilot/2026-11-07-db-multi-database/` | 后端多库已收口；其中「下拉选择器」交互未落地 |

## 适用问题类

相邻、本批要避开的复发面：

- PC-025 暗色选中行对比度：新节点沿用现有 `.node` / `.node.on`，不新增 primary-light 底。
- PC-026 插件类名撞宿主 Tailwind：新类名带 `dba-` 前缀，不用裸 `grid`。
- 异步空态抢先（2026-11-06 表加载）：库列表同样「先 loading，结束后才允许空态」，序号丢弃旧响应。

无「列库失败」既有 PC。本批是未接线的已有能力，不是同一缺陷的二次复发。

## 候选与选择

选定：把已有列库接口接到页面，交互对齐 Navicat / SSMS / pgAdmin / MySQL Workbench（点连接先列出全部可见库）。不重做后端枚举 SQL，不把 044 §8.2 的十条查询工作台项塞进本批。
