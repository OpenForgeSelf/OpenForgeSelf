# 批次A 任务分解（Task）

> 状态：T1–T5 全部落地并 APPROVED（2026-09-27）；本文为 2026-09-30 回溯建档
> 前置：任务书 7 文件清单（seq99）；门禁口径见 [`03-plan.md`](03-plan.md)

## 0. 任务清单与完成状态

| # | 任务 | 文件 | 状态 |
|---|---|---|---|
| T1 | `GetMenuItems` 合并派生（manifest 项 `Id={m.Id}.menu.manifest`、逐字段、不双发、禁用不发） | `ForgeSelf.Api/Controllers/PluginController.cs` | ✅ 已落地（实读 :269/:308/:326 确认） |
| T2 | 菜单 Path 一行修 `/quicklinks` → `/quick-links` | `Plugins/QuickLinks/QuickLinksPlugin.cs` | ✅ 已落地 |
| T3 | 撤销 scheduler/sample 无界面菜单声明 + 恢复指引注释 | `SchedulerPlugin.cs` / `SamplePlugin.cs` | ✅ 已落地 |
| T4 | 铁律19 四点逐字 + 门禁文档 §四加条 | `.agents/skills/plugin-development/SKILL.md` 等 | ✅ 已落地（铁律19③ 实读确认） |
| T5 | e2e 四组断言（对账/渲染/悬空检测/篡改必 Fail） | `ForgeSelf.Web/e2e/menu-route-consistency.spec.ts` | ✅ 已落地（4 passed 50.8s） |

配套新增：`PluginMenuItemsMergeTests` 3 例（真实调用 controller，非 mock）。

## 边界约定（Allowed / Forbidden）

### Allowed

- 修改任务书 7 文件清单内的文件（T1–T5 指定文件 + 配套测试 + e2e spec）；
- 在 `.agents/skills/plugin-development/SKILL.md` 增补铁律19 条款；
- 篡改取证（临时改 Path 验证 e2e 必 Fail），但**必须当场还原**并 grep 确认零残留；
- 按正规通道发布验证（build → publish 宿主 → health → API 实测 + DLL 探针）。

### Forbidden

- 禁止修改宿主端口配置真源（`PortNumber=7102` 归属项管哥裁决，测审未擅改）；
- 禁止删/跳任何既有测试（批次E 9 项失败名单原样保留，只比对不处置）；
- 禁止 e2e 走 mock 通道（必须 globalSetup 真实宿主）；
- 禁止手动 `Copy-Item`/`dotnet publish` 散拷产物进 `publish/`（发布规范废除项）；
- 禁止 `Stop-Process` 用户运行中的宿主实例；
- 禁止修改任务书清单外文件（AGENTS.md/docs 等并行会话改动不计入本批）。

## 验证作业单（QA 直接执行）

1. `dotnet test`：全量不新增失败（基线 1466P/9F，失败=批次E 9 项）；`PluginMenuItemsMergeTests` 3/3 绿；
2. `pnpm run check`（0 errors）+ `pnpm run test`（43 files / 473 passed）；
3. e2e `menu-route-consistency.spec.ts` 4 passed；篡改 Path 重跑必须 2 failed 2 passed（③① 命中）；
4. 运行实例 `GET /api/plugin/menu-items`：AC-1~5 逐条实测；
5. DLL 探针：`QuickLinks.dll` FOUND `quick-links` / ABSENT 旧串；`ForgeSelf.dll` FOUND `menu.manifest`。
