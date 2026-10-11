# M5 Batch Evidence — Plugin-First 架构规范落地

> 日期：2026-10-11｜批次：design-overhaul M5｜任务：manifest surfaces 四类规范化 + 宿主消费管道
> 范围：Abstractions manifest 类型/校验 + 前端 surface 路由消费 + store 规范化 + 功能文档。
> 禁碰：宿主视图、插件 web 前端、三组件、workshop-forge.css、router 之外前端业务逻辑。

## 1. 执行清单

| # | 动作 | 文件 | 结果 |
|---|---|---|---|
| 1 | 读 03-plan.md M5 节 + DESIGN.md Plugin-First 节 | docs/ai/pilot/2026-10-11-design-overhaul/03-plan.md:123-138；DESIGN.md:372-379 | Verified |
| 2 | 读 Abstractions / dynamicPlugins / pluginRoutes / pluginManifest 现状 | ForgeSelf.Abstractions/PluginMetadata.cs；ForgeSelf.Web/src/router/{dynamicPlugins,pluginRoutes}.ts；ForgeSelf.Web/src/stores/pluginManifest.ts | Verified |
| 3 | 新增后端 surface 规范类型 | ForgeSelf.Abstractions/PluginSurfaces.cs | Verified（build 0 err） |
| 4 | FrontendContributes 加 Surfaces 字段 | ForgeSelf.Abstractions/PluginMetadata.cs:171-184 | Verified |
| 5 | LoadPluginManifest 清洗未知 surface（归一/去重/记日志，不阻断） | ForgeSelf.Api/Plugins/PluginManager.cs:402-413 | Verified |
| 6 | devEntry 分支 DTO 拷贝补 Surfaces | ForgeSelf.Api/Controllers/PluginController.cs:363-371 | Verified |
| 7 | 前端 PluginSurface 类型 + FrontendContributes.surfaces | ForgeSelf.Web/src/types/plugin.ts | Verified（vue-tsc 0 err） |
| 8 | 前端 surface 规范化工具 | ForgeSelf.Web/src/utils/pluginSurfaces.ts | Verified（5 单测绿） |
| 9 | dynamicPlugins：tab surface 门控 + route meta.surfaces | ForgeSelf.Web/src/router/dynamicPlugins.ts | Verified（2 新增单测绿） |
| 10 | pluginManifest store：homeWidgets/quickActions/statusBadges/tabRoutes/surfacesOf | ForgeSelf.Web/src/stores/pluginManifest.ts | Verified（2 新增单测绿） |
| 11 | 功能文档 | docs/02-features/040-plugin-first-manifest-surfaces.md | Verified |
| 12 | 后端新增 4 测试 | ForgeSelf.Api.Tests/Plugins/PluginFrontendManifestTests.cs | Verified（12/12 定向绿） |
| 13 | 前端新增测试 | utils/__tests__/pluginSurfaces.test.ts；stores/__tests__/pluginManifest.test.ts；router/__tests__/dynamicPlugins.test.ts | Verified（vitest 792/792） |

## 2. 验证结果

### 后端（中档）
- `ForgeSelf.Api dotnet build`：**0 错误 / 1346 警告**（警告全为既有 nullable 警告，非本次引入）。Verified。
- `ForgeSelf.Api.Tests dotnet test` 全量：**通过 2891 / 失败 13 / 总计 2904**（22m43s）。
  - 13 个失败全部为**预存基线红**，与本次改动无关：
    1. `ScriptRunnerDiIntegrationTests.GetRuntimes_...CordisContext`（integration，测试宿主未加载插件控制器）
    2. `TodosControllerTests.CompleteThenReopen_...FlipStatus`（integration）
    3-8. `WorkflowPlanningIntegrationTests.*`（6 例，TODO 已登记 P2 存量）
    9-10. `PluginFrontendFileMiddlewareTests.Invoke_*Cache`（Cache-Control 期望 no-cache/immutable 实得 no-store，中间件被前序批次改过，非本次）
    11. `DesignAgentToolContractTests.ExecuteAsync_空参数_不抛`（DesignSystem，success=False，非本次）
    12. `WorkflowEnginePluginRegistrationTests.Apply_后_三契约经上下文可解析`（line 58 NotNull 失败；该测试文件本身预存编译错误——引用不存在的 `WorkflowServiceResolver`，见下「阻塞」节）
    13. `RepositoryScriptTests.ScriptsWithNonAscii_MustHaveUtf8Bom(backfill-feature-ids.ps1)`（脚本缺 BOM，非本次改动）
  - **本次新增红 = 0**。新增的 4 个 surface 测试全绿（PluginFrontendManifestTests 12/12）。

### 前端
- `npm run design:check`：style-tokens 硬编码 1862（基线 2501，↓639）；compliance a=244 / b=0 / c=0 / d=0（基线 a=244 b=0 c=0 d=1，无新增上涨）。PASS。Verified。
- `pnpm run check`（vue-tsc + eslint）：**0 errors / 145 warnings**。本次改动文件 0 warning（145 warning 全为 M4 新组件 PageHead/ForgeCard/EmptyState 的 SVG 属性格式 warning）。Verified。
- `pnpm run test`（vitest）：**70 files / 792 tests 全绿**（基线 67/771，新增 3 文件含 pluginSurfaces.test.ts 5 例 + store/router 新增用例）。Verified。

### e2e 定向
- `e2e/plugin-store.spec.ts`（7 workers 并行）：1 passed / 6 failed（.plugin-card 不可见）。**判定：预存 flake**——TODO 已登记「plugin-store e2e 并行冷启动偶发空态（6/7 偶发，--workers=1 稳定）」。
- `e2e/plugin-store.spec.ts`（--workers=1 重跑）：**7 passed (48.7s)**。Verified。并行失败确认为冷启动竞态，非本次改动。
- `e2e/plugins/home`（--workers=1）：**1 passed (38.9s)**（重定向 /home，四区块渲染 + 导航桥 /todo + 截图无溢出）。Verified。

## 3. 决策四要素

- **决策**：surfaces 用 `List<string>` 而非强类型枚举；后端 `PluginSurfaces` 静态类提供常量 + Normalize/NormalizeAll。
- **背景**：DESIGN.md 要求四类 surface（tab/home-widget/quick-action/status-badge）作为宿主校验基准；仓库既有 manifest 无 surfaces 字段。
- **理由**：(1) 与既有 `Tags/Provides/Consumes` 等 `List<string>` 字段风格一致；(2) 对未来新增 surface 向后兼容——未知值清洗丢弃不阻断加载；(3) 前端 `resolveSurfaces` 对旧插件（无 surfaces 但带 route+views）默认 `['tab']`，既有插件零回归。
- **准则**：manifest surfaces 是声明式契约，宿主消费点（store getters / 路由门控）按 surface 分发；UI 渲染位（首页 slot / 命令面板 / StatusBar 徽标）留给后续 M 批接视图。

## 4. 未做 / 风险

- **未做**：
  - 未改既有插件 plugin.json 的 surfaces 显式声明（Home 等仍走默认 tab；显式声明留给插件版本化发布时一并补）。
  - 未新建首页 slot 渲染组件 / 命令面板 / StatusBar 徽标 UI（属 M4/M8 视图改造，本批禁碰视图）。
  - 未发布插件 / 运行实例复验（本批为宿主类型与管道，未改插件产物）。
- **风险**：
  - 前端 `resolveSurfaces` 的「旧插件默认 tab」推断依赖 `route+views` 同时存在；若某插件只声明 home-widget 但误带了 route，会被额外当作 tab 注册路由。当前无此插件，后续若出现需在 manifest 显式声明 surfaces 消歧。
  - 预存编译错误 `WorkflowServiceResolver`（2026-10-10 提交引入）导致测试工程在未临时解锁状态下无法 build；本次为跑全量测试临时注释该行后已恢复原状。该问题需单独修复（补 WorkflowServiceResolver 类型或删该断言）。

## 5. 阻塞登记（非本次引入）

- `ForgeSelf.Api.Tests/Plugins/WorkflowEngineTests/WorkflowEnginePluginRegistrationTests.cs:63` 引用 `ForgeSelf.Api.Plugins.AIAgent.WorkflowServiceResolver`，该类型在源码中不存在（全仓 grep 0 命中）。导致测试工程无法编译。本次验证时临时注释该行解锁，跑完已恢复原状。**归属：2026-10-10 提交 31e7dbd（aiagent-tools-features-fix）遗留**。
