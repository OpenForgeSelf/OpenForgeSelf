# 00 Repository Understanding — design-system-import

任务：DesignSystem 插件 DTCG 导入/回流（v2.7.0，M13）。本目录是 M13 独立工件链；底座理解见 `../design-system-v2/00-repository-understanding.md`。

## 仓库事实（导入路径相关）

- 宿主：ASP.NET Core (.NET 10) 插件架构；插件 `Plugins/DesignSystem` 经 `plugin.json` 注册，控制器路由 `/ds/*`。
- 数据层：NewLife.XCode + SQLite；表定义 `Plugins/DesignSystem/Data/DesignSystemTables.cs`，令牌唯一键 `(ProjectId, ThemeId, Path)`（`TokenGraph` 维护环/悬挂/重复校验）。
- 生成引擎：`Services/DesignGenerator.cs` 产出三层（primitive/semantic/component）×多主题令牌；`Generator`/`GeneratorSeed` 列记录来源与种子。
- 前端：`Plugins/DesignSystem/web/`（ESM lib，`web/dist/index.js`），令牌工作台 `sections/TokenStudio.vue`。
- 已有导出投影：`Services/ExportService.cs`（DTCG/CSS/Tailwind v4/SCSS/Tokens Studio/DESIGN.md/registry/SQL），导出 DTCG 为导入的对偶面。

## 测试方式（从仓库确认）

- 后端 xUnit：`ForgeSelf.Api.Tests/Plugins/DesignSystemTests/`（导入用例 `ImportTests.cs`）。
- 前端 vitest：`Plugins/DesignSystem/web/src/**/*.test.ts`；插件层 e2e：`ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts`（零 mock 真宿主）。
- 门禁脚本：`scripts/verify-pilot-artifacts.ps1`（工件链）、`design-system-verify` 技能四层门禁。

## 技术栈版本

.NET 10 / NewLife.XCode / Vue 3.5 + Vite 6 + TS 5.7 / Element Plus 2.14 / Playwright / pnpm 10。
