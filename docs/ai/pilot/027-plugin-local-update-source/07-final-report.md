# AI-Native Pilot Result（最终汇报）

> PILOT-027 · 插件本地目录更新源｜2026-09-28
> 状态只报事实（对齐 AGENTS.md §10.4/§10.5）。

## 1. Repository Understanding

确认了：
- 项目为 Vue 3 SPA（ForgeSelf.Web）+ ASP.NET Core .NET 10 API（ForgeSelf.Api），SQLite + NewLife.XCode 唯一 ORM；插件经 `Plugins/` + `plugin.json` 注册，运行期版本化侧载链路已存在（`_backups/<id>/<ver>/` stage → `POST /api/plugin/update/{id}` → `versions/<ver>/` + current 指针 + ALC 换载，不重启宿主）。
- 宿主自动更新已有完整链路（spec 036：UpdateConfig/UpdateChecker/StagedUpdateService/UpdateController + 页面「设置-版本更新」），更新源已支持 GitHub/Gitee/本地目录。
- 插件打包能力已有 `PluginPackagerService`（.forgeself-plugin 包），但无独立打包脚本、无「插件侧更新源」配置。
- 运行宿主 D:\src\tools\ForgeSelf 实测 v2.2.8（FileVersion=2.2.8.0，对应 commit 78d065c）；本地更新目录 `D:\src\my-proj\OpenForgeSelf\updates` 已存 0.2.5/0.2.6/2.2.7/2.2.8。

## 2. Selected Task

用户诉求「宿主更新已解决（本地 zip 目录更新源），插件更新怎么办」→ 推荐并确认「插件本地包目录更新源」（与宿主同构）：设置-插件管理 tab 配置本地插件包目录（LocalDir，留空=停用）→ 插件市场「检查更新」扫描目录内 `*.forgeself-plugin`（版本 > current 且 > _backups 最高、Id 匹配、含入口 DLL）→ 点更新从包 stage 后走既有版本化侧载，**不重启宿主**；与宿主更新源完全分离。新增 `scripts/package-plugin.ps1` 打包单插件。

## 3. Changed Files

后端：`PluginUpdateSettings.cs`（新）、`PluginUpdateSettingsService.cs`（新）、`AppBuilder.cs`、`PluginDetailDto.cs`（Source）、`PluginVersionService.cs`（重写）、`PluginController.cs`（GET/PUT update-settings）
测试：`PluginUpdateSettingsServiceTests.cs`（新×5）、`PluginVersionUpdateSourceTests.cs`（新×7）、`PluginVersionServiceTests.cs`/`PluginFrontendManifestTests.cs`/`PluginMenuItemsMergeTests.cs`（仅补 ctor 参数，断言未改）
前端：`types/plugin.ts`、`services/pluginApi.ts`、`components/settings/PluginsPanel.vue`、`__tests__/PluginsPanel.test.ts`（新×4）
脚本/文档：`scripts/package-plugin.ps1`（新）、`docs/02-features/038-plugin-local-update-source.md`（新）、`ForgeSelf.Api/Plugins/README.md`（9.6 节）

## 4. Validation

Build: `dotnet build ForgeSelf.Api` —— 0 error（Verified）
Unit Test:
- 新测试 `PluginUpdateSettingsServiceTests` + `PluginVersionUpdateSourceTests`：12/12 绿（Verified）
- 插件全量回归：507/509 绿；2 失败为既有问题（`ScriptRunnerDiIntegrationTests` GET /api/scripts/runtimes 404、`TerminalCommandGuardTests` 大小写断言），与本任务无文件交集（Verified）
- `RepositoryScriptTests`（.ps1 BOM 守卫）：10/10 绿（Verified）
- 前端 vitest：477/477 绿（Verified）
E2E: N/A（宿主配置面板 + API + 脚本改动；前端交互以单测覆盖，UI 走查待发布后按需进行）

## 5. Evidence

见 `docs/ai/pilot/027-plugin-local-update-source/05-evidence.md`：Build 0 error、新测试 12/12、插件回归 507/509（2 既有失败已排除）、BOM 10/10、前端 check 0 error（81 既有 warning）、前端 477/477，全部 Verified。

## 6. Review

见 `06-review.md`：审查八问全 PASS，Requirement/Scope/Test/Architecture Check 全 PASS，Risk=L1，**Final Decision: APPROVED**。

## 7. Risk

L1（低：2 个既有失败测试与本次无关；会话B dsh 在途文件存在编译抖动可能，未触碰未提交）。

## 8. Problems Found

- 插件全量回归 2 个既有失败测试（ScriptRunnerDiIntegrationTests 404 / TerminalCommandGuardTests 大小写）——非本任务引入，已记 TODO 批次E，建议各自 owner 跟进。
- 插件市场前端 `PluginStore.vue` 未展示后端已返回的 `source` 字段（前端类型已加 `source?`）——可选项，未扩范围，记 TODO。
- 会话B（dsh）在途文件与本任务并发于同一工作区，提交时逐条核对排除，未混入。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS（既有版本化侧载/打包链路、宿主更新源结构均从真实代码确认） |
| Intent → Spec | PASS（用户「按你推荐的来」= 闸门1 确认；子代理审查 CHANGES_REQUIRED 返工 v2 后定稿） |
| Spec → Plan | PASS（文件级计划，落点=设置-插件管理 tab，用户确认） |
| Plan → Code | PASS（按 04-task Allowed/Forbidden 实施，无越界） |
| Code → Test | PASS（新测试 12 + 前端 4；既有 3 个 ctor 适配） |
| Test → Evidence | PASS（05-evidence 全部 Verified 记录，2 既有失败如实标注） |
| Evidence → Review | PASS（06-review 八问 + APPROVED） |

## 10. 最重要的问题

子代理只读审查（P0×1 + P1×8 + P2×12）在实现前捕获了「改 ctor 需补既有测试」「纯包源插入点」「版本号正则防路径穿越」等关键缺陷——**先审查后编码的闸门1 把关价值显著**；若跳过审查直接实现，P0/P1 级问题将混入提交。

## 11. 下一步建议

用户页面实测 v2.2.9（含本功能）→ 设置-插件管理配置插件包目录 → `scripts/package-plugin.ps1` 打包 AIAgent 实测插件更新链路；若通过，将「插件更新源」纳入正式发布规范（与宿主更新源同构文档化）。
