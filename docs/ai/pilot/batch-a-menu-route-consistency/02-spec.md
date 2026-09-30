# 批次A 菜单/路由真源一致性：方案（Spec）

> 功能编号：菜单/路由一致性专项 · 批次A
> 状态：已实施完毕并 APPROVED（2026-09-27）；本文为 2026-09-30 回溯建档
> 关联：验收标准与实测结果见 [`05-evidence.md`](05-evidence.md)；终审结论见 [`06-review.md`](06-review.md)

## 0. 结论先行

菜单声明（插件代码注册）与运行时路由（frontend-manifest / 插件路由）双轨并存且无对账，已产生三类实锤缺陷（D1 悬空菜单 / D2 缺入口 / D3 无界面悬空声明）。方案 = **合并派生单真源 + 三处缺陷修复 + 铁律19 + e2e 防漂移门禁**，四层防线确保漂移在开发期与 CI 双重拦截。

## 1. 真源定义

| 真源 | 内容 | 消费方 |
|---|---|---|
| 插件代码声明（`RegisterMenuExtensions`） | 有自定义菜单逻辑的插件（quick-links） | `GetMenuItems` 合并 |
| frontend-manifest（`/api/plugin/frontend-manifest`） | 声明了前端界面的插件的菜单三元组（名称/Path/图标） | `GetMenuItems` 合并 |
| 运行时路由 | 插件视图路由注册结果 | 必须与菜单 Path 可解析对应 |

判定标准：`menu-items` 每一项的 `Path` 必须能被运行时路由解析（无悬空）；每个「启用 + 有界面」插件恰有 1 条菜单项（无缺发/双发）。

## 2. 缺陷分析（D1/D2/D3）

- **D1**：`QuickLinksPlugin.cs` 菜单 `Path=/quicklinks` ≠ 运行时路由 `/quick-links` → 菜单点击渲染空 outlet。
- **D2**：mcp-center、design-system 在 frontend-manifest 声明了界面，但 `menu-items` 只读插件代码注册表 → 菜单缺入口。
- **D3**：scheduler、sample 纯后端插件无界面，却通过 `RegisterMenuExtensions` 声明菜单 → 永久悬空项。
- 根因：无合并派生、无对账门禁，两条轨道各自演化必然漂移。

## 3. 方案：T1–T5 五项交付

| # | 交付 | 内容 |
|---|---|---|
| T1 | `GetMenuItems` 合并派生 | `ForgeSelf.Api/Controllers/PluginController.cs`：代码声明与 manifest 菜单合并；manifest 项 `Id={m.Id}.menu.manifest`；逐字段相等；不双发；禁用插件不发 |
| T2 | 悬空路径一行修 | `QuickLinksPlugin.cs`：菜单 `Path` `/quicklinks` → `/quick-links` |
| T3 | 撤销无界面声明 | `SchedulerPlugin.cs` / `SamplePlugin.cs`：删除 `RegisterMenuExtensions` 菜单声明，保留工具函数注册，恢复指引注释在位 |
| T4 | 铁律19 固化 | `.agents/skills/plugin-development/SKILL.md` 铁律19 四点逐字（含③「任何插件 route 改名/增删必须同步 `menu-route-consistency.spec.ts` 并实跑通过」）+ 门禁文档 §四加条 |
| T5 | e2e 防漂移门禁 | 新增 `ForgeSelf.Web/e2e/menu-route-consistency.spec.ts` 四组断言：①双源逐条对账 diff=0；②真实路由导航渲染；③悬空菜单检测；④篡改必 Fail 的语义正确性 |

## 4. 验收标准（AC-1~8）

- AC-1：quicklinks 项 `Path=/quick-links` 且全表无 `/quicklinks` 残留；
- AC-2/3：scheduler、sample 悬空声明撤销（menu-items 中 0 项）；
- AC-4：mcp-center + design-system 补发且与 manifest 逐字段相等；
- AC-5：双源逐条对账 diff=0（10 个「启用+menu+route」插件全部恰 1 条）；
- AC-6：e2e 全绿 + 篡改必 Fail（篡改 Path 后 ③① 精确命中）；
- AC-7：`pnpm check/test` + `dotnet build/test` 零新增失败；
- AC-8：`GetMenuItems` 合并单测 3 例绿（真实调用 controller）。

## 5. 边界与非目标

- 不改插件 API 业务逻辑、不改路由注册机制本身（仅菜单派生面）；
- 不处理端口真源裁决（51888 vs 7102，移交项管哥，见 06 Findings 1）;
- 不修 live spec token 注入基建（独立缺陷单，见 06 Findings 2）；
- 不动批次E 既有失败用例。

## 6. 用户验证清单

- [x] T1 合并派生已落地（`PluginController.cs:308` `Id={m.Id}.menu.manifest` 实读确认）
- [x] T2/T3 已落地且篡改还原后与任务书清单一致
- [x] T4 铁律19③ 已入 SKILL.md（实读确认）
- [x] T5 e2e 已入库且篡改取证通过
- [x] AC-1~8 独立复跑全 Pass（见 05）
