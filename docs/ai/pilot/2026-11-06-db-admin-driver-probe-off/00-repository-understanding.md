# Repository Understanding — db-admin 驱动预检关闭

> 阶段：Stage 0｜Task ID：`2026-11-06-db-admin-driver-probe-off`｜日期：2026-11-06

## 项目结构

- 解决方案：`ForgeSelf.slnx`；宿主后端 `ForgeSelf.Api/`（.NET 10 + NewLife.XCode + SQLite）；宿主前端 `ForgeSelf.Web/`（Vue 3 + Vite + pnpm）。
- 插件源码在仓库根 `Plugins/<PascalCase>/`；本任务插件为 `Plugins/DbAdmin/`（清单 `plugin.json`，Id=`db-admin`，版本 0.1.3）。
- 插件测试：`ForgeSelf.Api.Tests/Plugins/DbAdminTests/`；插件层 e2e：`ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`。
- 功能文档：`docs/02-features/044-db-admin.md`；插件文档真源：`Plugins/DbAdmin/docs/01~07`。

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端 | .NET 10（net10.0-windows 宿主 / net10.0 插件）+ NewLife.XCode 12.0.2026.701 | `ForgeSelf.Api/ForgeSelf.Api.csproj:82`、`Plugins/DbAdmin/DbAdmin.csproj` |
| 驱动 | 宿主仅随附 SQLite（`XCode.SQLite` 11.24.2026.302） | `ForgeSelf.Api/ForgeSelf.Api.csproj:86` |
| 前端 | Vue 3 + Vite（插件 `web/dist/index.js`，lib 模式） | `Plugins/DbAdmin/web/vite.config.ts`、`plugin.json frontend.entry` |
| 测试 | xUnit（后端）、vitest（插件前端）、Playwright（插件 e2e） | `ForgeSelf.Api.Tests/`、`Plugins/DbAdmin/web/src/*.test.ts`、`e2e/plugins/db-admin/` |

## 架构特点

- 连接解析：`DbConnectionService.Resolve` 把用户连接注册为 `DbAdminU_<id>`，经 `DAL.AddConnStr(name, connStr, null, def.XCodeProvider)` 交给 XCode。
- 当前驱动预检：`DriverCatalog.IDriverProbe` / `DefaultDriverProbe` 在 `ListProviders`、`Resolve`、`Test` 三处判断驱动 DLL 是否存在；缺失即抛/返回 `driver_missing`（HTTP 424）。
- 驱动打包决策：插件不打包 MySQL/PostgreSQL/SQL Server 驱动（`Plugins/DbAdmin/docs/05-已知风险-多引擎与密码.md` 决策 1）。
- 前端消费：`Plugins/DbAdmin/web/src/DbAdminView.vue` 用 `driverAvailable=false` 显示 provider 下拉「（需驱动）」与表单「需驱动」灰标；`normalize.ts` 兼容 `driver_missing` 测连结果。

## 测试方式

- 后端定向：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DbAdmin"`
- 插件前端：`cd Plugins/DbAdmin/web && pnpm run build`（单测 `pnpm run test`）
- 插件 e2e：`cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin`
- 宿主后端构建：`cd ForgeSelf.Api && dotnet build`

## 构建命令

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~DbAdmin"
cd Plugins/DbAdmin/web && pnpm run build && pnpm run test
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/DbAdmin/Services/` | `DriverCatalog`（provider/驱动探测）、`DbConnectionService`（连接/测连/解析）、`DbQueryService`、`SqlGuard`、脱敏/审计 |
| `Plugins/DbAdmin/web/src/` | 插件界面（连接表单、provider 下拉、测连结果、结果格） |
| `Plugins/DbAdmin/docs/` | 插件功能/交互/架构/风险文档（契约真源 docs/03 §3.1） |
| `ForgeSelf.Api.Tests/Plugins/DbAdminTests/` | 后端单测与夹具（`DbAdminTestFixture` 提供 `SqliteOnlyProbe` 替身） |

## 代码组织方式

- 控制器只注入插件自有服务（`DbAdminController`），HTTP 错误码映射在 `StatusOf`。
- 服务层以 `DbAdminException(code, message)` 表达业务失败；`Test` 不抛业务异常，返回 `ConnectionTestResult`。
- 前端 `normalize.ts` 是后端契约唯一适配点；`http.ts` 解析 424/400 等错误码。

## 现有工程规范

- AGENTS.md §0/§11：开发任务走 AI-Native 闭环，产物落 `docs/ai/pilot/YYYY-MM-DD-<task-id>/`。
- `.agents/skills/plugin-development/SKILL.md`：插件任务维护闭环（门禁 → 冒烟 e2e → dev-stack 验收 → 发布 → 运行实例只读复验 → 文档）。
- 插件控制器类级 `[Authorize("ApiKeyPolicy")]`（铁律 17）；插件不构造注入宿主契约（铁律 12c）。
- 驱动能力相关决策真源：`Plugins/DbAdmin/docs/05-已知风险-多引擎与密码.md` 决策 1。

## 适用问题类

无。本任务为用户明确要求的行为调整（暂时关闭驱动预检），非缺陷/回归/返工类；`docs/11-troubleshooting/problem-classes/README.md` tags 检索未命中 db-admin 问题类。

## 候选低风险任务

- 本任务：关闭 db-admin 驱动预检（后端 2 文件 + 测试 1 文件 + 插件文档），低风险、易测试、不涉数据/鉴权/架构。

## 选择该任务的原因

用户输入2 直接指令「数据库插件，暂时屏蔽掉插件检查……先取消掉驱动校验」，优先级最高；且当前预检会导致 XCode 自动获取驱动的能力被拦截。
