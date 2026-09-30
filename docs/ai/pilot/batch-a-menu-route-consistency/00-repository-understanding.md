# 仓库现状理解（Repository Understanding）

> 本文记录批次A「菜单/路由真源一致性」任务**启动前**的仓库真实状态。
> 快照：2026-09-27 任务启动前；本文为 2026-09-30 回溯建档，依据同目录 05-evidence.md / 06-review.md 与代码实读，不凭推测。

## 1. 技术栈与菜单/路由相关分层

- 后端：.NET 10 / ASP.NET Core 10 / NewLife.XCode（唯一 ORM）/ Serilog
- 前端：Vue 3 + Vite（宿主 `ForgeSelf.Web`，e2e 位于 `ForgeSelf.Web/e2e/`）
- 菜单 API：`ForgeSelf.Api/Controllers/PluginController.cs` `GET /api/plugin/menu-items`
- 前端清单 API：`PluginController.cs` `GET /api/plugin/frontend-manifest`
- 插件菜单声明：插件侧代码注册（如 `RegisterMenuExtensions`）与 `frontend-manifest` 两轨并存

## 2. 菜单/路由双轨漂移（任务前痛点）

菜单项来源（插件代码声明）与运行时路由（frontend-manifest / 插件实际注册路由）为两条独立轨道，无对账机制，漂移已在三处实锤：

- **D1 · 菜单悬空**：`QuickLinksPlugin.cs` 菜单项声明 `Path=/quicklinks`，而运行时路由实为 `/quick-links`——点击菜单渲染 outlet 空壳。
- **D2 · manifest 不补发**：带 `frontend-manifest` 的插件（mcp-center、design-system）的界面未在 `menu-items` 中补发菜单项——菜单缺入口。
- **D3 · 纯后端插件声明界面菜单**：scheduler、sample 无任何前端界面，却注册了菜单扩展声明——纯悬空项。

## 3. 防漂移机制缺失

- 无菜单/路由一致性 e2e（`ForgeSelf.Web/e2e/menu-route-consistency.spec.ts` 尚不存在）；
- `GetMenuItems` 无合并派生单测（`PluginMenuItemsMergeTests` 尚不存在）；
- 插件开发规范（`.agents/skills/plugin-development/SKILL.md`）无铁律19「菜单/路由一致性回归」条款，门禁文档无对应加条。

## 4. 测试基线（任务前）

- Api.Tests：通过 1466 / 失败 9（失败名单=批次E 既有项：WorkflowPlanning×6、ScriptRunnerDi、TerminalCommandGuard、ForgeConfig）；
- Abstractions 13 通过 / Core 12 通过；
- 前端 `pnpm run test`：43 files / 473 passed；既有 4 红 e2e（app.spec:35 / home.spec:35 / mcp-center:158 / quicklinks.spec:164）与本域零关联。

## 5. 既有文档与流程背景

- 多岗位流水协作：seq95 验收包 → seq99 任务书 → seq102 交付回报 → seq105 派单与裁决（SOP 驱动）；
- 相邻批次：批次B（供应商目录）、批次C（folder-size 插件）、批次E（既有失败基线族）另立 pilot 目录；
- 运行实例端口惯例口径 `:51888` 与配置真源 `PortNumber=7102` 的冲突在任务前未被显式记录（任务中实测暴露，见 05 §三）。

## 6. 本次任务要解决的核心问题

「菜单声明与运行时路由必须是同一真源的派生，且漂移必须被门禁自动暴露」——合并派生 `GetMenuItems`、修正悬空路径、撤销无界面声明、固化铁律19、新增防漂移 e2e，使 D1/D2/D3 三类缺陷在开发期与 CI 双重拦截。
