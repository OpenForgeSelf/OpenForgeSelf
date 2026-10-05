# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜Task ID：PILOT-052（2026-10-05-mcp-center-endpoint-url）
> 规范：`docs/04-standards/ai-native-engineering-workflow.md` §2｜原则：所有条目来自**真实仓库内容**，禁止凭常识推测。

## 项目结构

- 根目录关键组成（实测 `Get-ChildItem`）：`ForgeSelf.Web/`（Vue 3 SPA + 全部 Playwright e2e）、`ForgeSelf.Api/`（ASP.NET Core 宿主 + 插件装载器）、`ForgeSelf.Api.Tests/`（xUnit）、`ForgeSelf.Core/`、`ForgeSelf.Abstractions/`、`Plugins/`（**18 个插件源码，PascalCase**）、`docs/`、`scripts/`、`forgeself-design/`。
- 插件源码在**仓库根 `Plugins/<PascalCase>/`**，不在 `ForgeSelf.Api/Plugins/`（后者是宿主插件装载器代码）——技能 `plugin-development` 铁律 3 已明示。
- 本任务目标插件：`Plugins/McpCenter/`（`plugin.json` / `McpCenterPlugin.cs` / `Controllers/` / `Services/` / `Models/` / `web/`）。

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 宿主前端 | Vue 3 + Vite + TS + Element Plus + Tailwind | `ForgeSelf.Web/package.json`、`AGENTS.md` §2.1 |
| 插件前端 | 独立预编译产物 ESM（`web/dist/index.js`），`vue/vue-router/pinia/element-plus` 全部 external | `Plugins/McpCenter/web/package.json`（仅 vite/vue/EP/tailwind 依赖，**无 vitest、无任何 `*.test.ts`**）、`plugin-development` §3.2 |
| 后端 | .NET 10 + ASP.NET Core + SQLite/NewLife.XCode | `ForgeSelf.Api/ForgeSelf.Api.csproj`、`Plugins/McpCenter/McpCenter.csproj` |
| 插件后端 | `IPlugin` 实现 + 自带 Kestrel（独立 MCP 端口） | `Plugins/McpCenter/McpCenterPlugin.cs`、`Services/McpGatewayServer.cs` |
| e2e | Playwright（真实宿主、零 mock、动态端口） | `ForgeSelf.Web/playwright.config.ts`、`e2e/global-setup.ts` |

## 架构特点

1. **插件注册**：`plugin.json` 声明 `Id/Version/EntryAssembly/EntryType/frontend{views,menu,route,icon,entry}`；宿主按 `frontend.entry` **远程加载**插件界面（`Plugins/McpCenter/plugin.json:12-18`，route=`/mcp-center`，entry=`web/dist/index.js`）。
2. **插件前端产物如何进运行环境**：`web/dist` 被插件 `.gitignore` 忽略（`Plugins/McpCenter/web/.gitignore` 含 `dist/`，`git ls-files` 实测 0 命中），但 `ForgeSelf.Api/ForgeSelf.Api.csproj` 的 `StageAllPlugins`（Build 后）与 `StagePluginsToPublish`（Publish 后）把 `..\Plugins\*\web\dist\**` 拷进 `$(OutDir)Plugins\<Name>\` → 发布目录 → 宿主提供 `/plugin-view/mcp-center`。**⇒ 改了插件前端源码必须在本机 `pnpm run build` 重建 `web/dist`，e2e 与发布才拿得到新界面。**
3. **MCP 中心架构（034）**：
   - 对外面 = 插件自己起的 Kestrel，绑定地址来自 `Services/McpGatewayConfig.cs:27` `ListenUrl => $"http://{ListenHost}:{Port}"`，被 `Services/McpGatewayServer.cs:55` `UseUrls(_config.ListenUrl)` 用于**真实绑定**；
   - 对外端点 = `POST /mcp`（JSON 响应）+ `GET /mcp`（SSE 心跳）+ `GET /health`（`Services/McpGatewayServer.cs`；`docs/02-features/034-mcp-center.md:85`、`docs/ai/pilot/sems-selfcontained-mcp-tools/02-spec.md:129`）；
   - 管理面 = `api/mcp`、`api/mcp-center/config`、`api/mcp-center/servers`（三者类级 `[Authorize("ApiKeyPolicy")]`）；
   - 界面取数 = `GET /api/mcp-center/config` → `McpCenterRuntime.GetInfo()`（`Services/McpCenterRuntime.cs:31-44`）装 `Models/McpCenterConfigDto.cs`（`Port/ListenHost/ListenUrl/HasToken/TokenMasked/IsRunning/Version`）。
4. **既有先例（同一件事在别的插件已做对）**：`Plugins/DesignSystem/web/src/delivery/snippets.ts:36-39` 的 `mcpEndpoint()` = `listenUrl` 去尾斜杠 + `/mcp`，有 vitest（`snippets.test.ts`）与 e2e 常驻判据（`e2e/plugins/design-system/design-system-showroom.spec.ts:655-658`，挂点 `[data-mcp-url]`）。本任务的 MCP 中心界面**尚未做**这件事。

## 测试方式（本仓库正规入口）

| 范围 | 命令 |
| --- | --- |
| 宿主前端 | `cd ForgeSelf.Web && pnpm run check`（`vue-tsc --noEmit && eslint`）、`pnpm run test`（vitest） |
| 插件前端 | `cd Plugins/McpCenter/web && pnpm run build`（**无 vitest，无单测脚本**） |
| 后端 | `cd ForgeSelf.Api && dotnet build`；`cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~McpCenter"` |
| 插件层 e2e | `cd ForgeSelf.Web && pnpm exec playwright test e2e/plugins/mcp-center/mcp-center.spec.ts`（真实宿主、零 mock；`globalSetup` 自动 publish 宿主） |

> 铁律（`AGENTS.md` §5.3）：验证/截图/浏览器驱动一律走上述唯一测试体系，**禁止一次性 `.cjs` 脚本**当验证手段。

## 构建命令

```bash
# 插件前端（本任务必跑；dist 不入库但驱动 e2e 与发布）
cd Plugins/McpCenter/web && pnpm run build
# 插件层 e2e（零 mock，自动构建宿主）
cd ForgeSelf.Web && pnpm exec playwright test e2e/plugins/mcp-center/mcp-center.spec.ts --workers=1
# 后端回归
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~McpCenter"
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/McpCenter/web/src/` | 插件自带界面源码（`McpCenterView.vue` 单文件视图 + `api/` + `types/`） |
| `Plugins/McpCenter/web/dist/` | 构建产物（gitignored，随 `StageAllPlugins` 进运行/发布目录） |
| `Plugins/McpCenter/Services/` | 网关配置/服务器/转发器/外部 MCP 客户端 |
| `ForgeSelf.Web/e2e/plugins/mcp-center/` | 该插件的插件层 e2e（本任务判据落点） |
| `docs/02-features/034-mcp-center.md` | 该插件的功能文档（界面/端点/验证记录） |
| `docs/ai/pilot/<日期>-<task-id>/` | 本任务 00–07 工件 |

## 代码组织方式

- 插件前端为**单根视图 SFC**（`McpCenterView.vue`，约 1500 行：script setup + template + `<style>`），`plugin.json frontend.views[0] = McpCenterView` 必须与导出名一致。
- 视图内地址展示共 **3 处**（实测行号）：
  - `McpCenterView.vue:552-553` 顶部 chip（`class="gateway-address-chip"`，`:title="gatewayConfig.listenUrl"`，文本 `listenUrl`）；
  - `McpCenterView.vue:651-653` 「MCP 服务地址」卡（`class="card-value mono"`，文本 `listenUrl`，**无 `data-*` 挂点**）；
  - `McpCenterView.vue:181-191` `copyListenUrl()`：`navigator.clipboard.writeText(gatewayConfig.value?.listenUrl)`。
- 运行状态卡（`:673`）显示 `监听 {{ listenHost }}:{{ port }}` —— 绑定事实的展示位，本任务不动。

## 现有工程规范（对本任务有约束力）

1. `AGENTS.md` §0 预飞铁律（先写日志 → 建 TODO → 读规范 → 查技能）、§5.6 门禁分档、§11 唯一开发流程（本任务属**插件任务 ⇒ 全量 00–06 工件**）。
2. `docs/04-standards/ai-native-engineering-workflow.md`：九阶段 + 三道闸门（§1.1）；**闸门1 经用户批准后才允许 Implement**；§1 硬性约束（不改生产/DB/鉴权、不新增依赖、不无关重构）。
3. `plugin-development` 技能：铁律 7「改完插件 = 代码 + 门禁 + 插件层 e2e + 发布 + 隔离实例走查 + 运行实例只读复验」；铁律 13 版本徽标；铁律 16 发布规范（禁停宿主）；§四 维护闭环。
4. `AGENTS.md` §2.3：脚本一律 `pwsh`；git 提交/推送须用户明确授权（本轮只做本地工作）。
5. 用户偏好：同一任务改动汇总后**一次性**提交。

## 候选低风险任务

| # | 候选 | 风险 | 说明 |
| --- | --- | --- | --- |
| C1 | MCP 中心界面地址补 `/mcp` 路径（**本任务**） | 低 | 只动插件前端 + e2e + 文档 + 插件版本号；不动后端契约、不动绑定地址、不新增依赖 |
| C2 | 运行实例 `:51888` 插件零加载（宿主插件根解析缺陷） | 高 | 触宿主源码 + 用户现场实例 + 不可逆面，已在 TODO 单独立项等拍板 |
| C3 | MCP 2.0 协议支持 | 中 | 已有 worktree `wt-mcp2` 在做，另有 `wt-mcp-playground` 在飞 |
| C4 | `todo-tracker` 工具链路 `IServiceScopeFactory` 缺陷 | 中 | 触插件工具上下文与宿主 DI，面比 C1 大，等派单 |

## 选择该任务的原因

- **用户本轮直接指令**（最高优先级）：2026-10-05 输入1「看下MCP中心插件，我记得之前已经下过任务，mcp中心地址增加/mcp」；经检索确认为 **2026-09-27 输入16 的历史欠账**（当日工作日记原文：「如图 帮我完善下，增加地址，不写路径会让人误会」，三条拆解全未勾选），根 `TODO.md:166` 记为「暂缓——用户转向输入17」，**至今未实施**（3 处展示实测仍为裸 `listenUrl`）。
- 满足「低风险、小范围、易测试」：改动集中在单个 SFC 的 3 处展示、判据可落在既有插件层 e2e、无 DB/无鉴权/无宿主契约变更。
- 顺带消除一个真实误连：对外端点根路径 404，用户复制界面给出的地址去连客户端必然失败（2026-09-27 输入15 已实测确认「URL 必须含 /mcp 路径（根路径 404）」）。
