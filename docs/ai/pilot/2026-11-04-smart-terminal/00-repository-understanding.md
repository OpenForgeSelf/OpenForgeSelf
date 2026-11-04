# 00 · Repository Understanding — 2026-11-04-smart-terminal

## 项目基线（复核自真实仓库）
- 前端：Vue 3.5 + Vite 6 + TS 5.7 + Element Plus（ForgeSelf.Web/，pnpm；`pnpm run check` = vue-tsc -b + eslint）
- 后端：.NET 10 + SQLite + NewLife.XCode（ForgeSelf.Api/；`dotnet build` + `dotnet test`）
- 插件：`Plugins/<X>/` + `plugin.json`（Id=kebab、目录=PascalCase、EntryAssembly、frontend.route 唯一声明）
- 测试体系：vitest 组件单测 + Playwright e2e（e2e/plugins/<id>/，globalSetup 起真实宿主）+ dotnet xUnit
- 门禁：`pwsh scripts/ci/run-gates.ps1 -Base <ref>` = pilot-gate（工件链/trailer/范围）+ risk（信息性）+ style-tokens（裸色值棘轮）

## 本任务触碰面（基线 59d9f07，分支 feat/smart-terminal @633c15c）
- 11 个提交，54 文件：新增插件 Plugins/SmartTerminal/**（后端 Controllers/Models/Services/Data + web/src）与 Plugins/SmartTerminal.Tests/**；宿主侧 ForgeSelf.slnx / Api csproj / Api.Tests csproj（构建顺序登记）；ForgeSelf.Web/ct/stories.ts + SmartTerminal CT/story 组件；e2e/plugins/smart-terminal/spec；pilot 工件 01/02/03。
- 分支与 main(2aa93aa) 侧文件零交集（71 vs 54，实测 rev-list/diff 比对）。

## 已核实事实（读码）
- SmartTerminalController 类级 `[Authorize("ApiKeyPolicy")]`、`[Route("api/smart-terminal")]`；18 个端点 + 8 个 `st_*` MCP 工具（toolPrefix=st）。
- plugin.json：Id=smart-terminal、Version=1.2.1、frontend.route=/smart-terminal、entry=web/dist/index.js、菜单「智能终端」。
- 交互面五态与浮层顺序（风险 ack → 交互密码 → 审批 → 审计）见插件 docs/01/02（PRD 与交互说明已定稿入库）。
- 前端 15 处裸色值（6 处 var() 兜底 + 9 处直写）已定位并全部替换为 --el-* token（style-tokens 0 新增，见 05-evidence）。
- XCodeConfig 无需登记：本插件无自有实体库（审计为 JSONL 文件，非 XCode 表）；已核对 SmartTerminalPlugin.cs 无 PC-005 原始 CreateScope/急切 Provider 解析模式（仅 RegisterTools 签名处出现 IServiceProvider 形参，属工具注册惯例）。

## 适用问题类
检索 docs/11-troubleshooting/problem-classes/README.md：
- PC-005（插件急切 Provider 解析/raw CreateScope）：已按 lens 扫查 SmartTerminalPlugin.cs ——**未命中**急切解析反模式（仅 RegisterTools 签名出现 IServiceProvider 形参，属工具注册惯例），无需修复。
- 其余 PC（构建编码/BOM/发布签名类）：本任务不含发布脚本与 ps1 变更，不适用。