# 00 Repository Understanding — 发布本地 zip 被 safe-delete 批量删除守卫拦截

## 项目用途与结构
- 铸己匣 OpenForgeSelf：宿主前端 `ForgeSelf.Web/`（Vue 3.5 + Vite 6 + TS 5.7），后端 `ForgeSelf.Api/`（.NET 10 + SQLite + XCode），插件 `Plugins/<X>/web/`（Vite lib 模式独立预编译）。
- 发布链：`scripts/release/release-local.ps1` 编排 build-frontend（宿主 web → 各插件 web）→ publish-host → publish-bootstrapper → publish-updateagent → package-release（QQNT 布局 zip）→ make-release-notes。
- 沙箱环境：WorkBuddy 注入 `node-safe-delete-shim`（`NODE_OPTIONS=--require ...`），对 node 的删除操作做「移回收站 + 批量删除守卫」；守卫需 `CODEBUDDY_SAFE_DELETE_BULK_STATE_DIR` + `CODEBUDDY_TOOL_CALL_ID` 同时存在才生效（缺一即直接放行，见 shim `checkBulkDeleteGuard:230`）。

## 本次触达事实（已核实 2026-11-24）
- 发布失败现场（用户 11:29 外部启动的 `-Sign` 进程 PID 67036/74088，卡 19 分钟 CPU 恒 0.55s）+ 本人足量超时复现（`.temp/release-local-run2.log`）：真实报错 = `pnpm build (plugin-web/AgentHub) failed with exit code 1`，日志正文 = `[safe-delete][SAFE_DELETE_BULK_CONFIRM_REQUIRED] {count:50, threshold:50, scope:turn, target:.../Plugins/AgentHub/web/dist/index.js}` at `vite emptyDir → prepareOutDir`。
- 宿主 `ForgeSelf.Web/vite.config.ts:31` 已设 `emptyOutDir: false`（SOP 真源 `docs/04-standards/doc-reverse-sync-sop.md:49`），故宿主 web 构建通过；**15 个插件 `vite.config.ts` 全为 `emptyOutDir: true`**，vite 构建清空 dist 时触发批量删除守卫 → 发布链首插件即挂。
- 15 个插件（AIAgent/AgentHub/ClockBadge/CostScope/DbAdmin/DesignSystem/FileTools/Home/ImGateway/McpCenter/QuickLinks/Sems/SmartTerminal/TodoTracker/ToolBridge）配置结构完全一致（lib 模式、outDir dist、固定文件名 index.js/style.css），同一行 `emptyOutDir: true,`。
- `emptyOutDir: false` 副作用：dist 旧文件不清空；因产物为固定文件名（index.js/style.css）会覆盖写，故无多版本残留风险（与宿主同构）。

## 测试/门禁形态
- 守卫落点：`ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`（.NET xUnit 既有 G2 类级枚举守卫文件，已有 10+ PC 不变量先例），新增一条枚举测试：遍历 `Plugins/*/web/vite.config.ts` 断言不含 `emptyOutDir: true`（阳性对照：命中文件数 ≥13）。
- pre-commit：缺陷类任务 07 三节必查；`scripts/check-problem-classes.ps1` 校验 PC signature/guards/expires。
- 验证入口：红灯 = `pwsh release-local.ps1` 复现批量删除守卫（已 Verified）；绿灯 = 修复后重跑整条发布链产出 zip + `dotnet test --filter ProblemClassEnumerationTests`。

## 适用问题类（P0 分诊）
- 台账 tags 检索（README.md）：`写/改 *.ps1 脚本、发布链路 → PC-006`（PowerShell 编码，非本次根因）；PC-043（跨平台脚本短路，非本次根因）。**无命中的问题类** → 新建 PC-047（本任务归档）。
- 相邻 lens：PC-006「发布链本机编码/环境前置」与本次「本机沙箱环境前置需纳入发布排障」同属发布链环境域，02 Failure Modes 预填。

## 候选任务与选择理由
本任务即用户明示的发布链排障，无另选。闸门1 已于用户回复「a」批准方案 A（15 个插件配置 emptyOutDir true→false + G2 守卫 + PC/Case 归档）。
