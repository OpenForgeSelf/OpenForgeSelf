# Repository Understanding

> 阶段：Stage 0｜Task ID：2026-10-10-aiagent-tools-features-fix｜日期：2026-10-10
> 原则：所有条目来自真实仓库内容（本次为缺陷修复批，聚焦 AIAgent / WorkflowEngine / 宿主导航三域）。

## 项目结构
- 解决方案 `ForgeSelf.slnx`：`ForgeSelf.Api`（ASP.NET Core 10 宿主 + 插件架构）、`ForgeSelf.Core`（IContext/Context Cordis 式上下文）、`ForgeSelf.Abstractions`（跨插件契约：IToolRegistry/IWorkflowService 等）、`ForgeSelf.Web`（Vue 3.5 + Vite 6 + Element Plus 2.14 + Tailwind 4）、`ForgeSelf.Api.Tests`（xUnit）。
- 插件目录 `Plugins/<Name>/`（plugin.json 清单 + Controllers/Services/web）；本次涉及 `AIAgent`、`WorkflowEngine`。

## 技术栈
| 层 | 技术 | 依据 |
| --- | --- | --- |
| 宿主 | .NET 10 + NewLife.XCode | ForgeSelf.Api.csproj |
| 插件机制 | IPlugin.Apply(IContext) + 扩展点发现 | ForgeSelf.Core/IContext.cs、ForgeSelf.Api/Plugins/PluginManager.cs |
| 前端 | Vue 3.5 + Pinia + EP | ForgeSelf.Web/package.json |
| 测试 | xUnit / vitest / Playwright | 各项目 |

## 架构特点
- 插件服务双通道：`IServiceCollection`（宿主 DI）与 `IContext.Register`（Cordis 共享表）；兄弟插件经 `ctx.Get<T>()` 解析（ForgeSelf.Core/Context.cs GetService：本地 → 共享表）。**只 AddScoped 不 ctx.Register 的契约，兄弟插件解析恒为 null**（本次缺陷 ③ 根因）。
- 工具注册链：插件 `Apply` 构造 `IToolFunctionExtension` → 宿主 ExtensionPointManager 注册进 IToolRegistry → `/api/ai-agent/chat/tools` 返回 `GetAllTools()` → 前端 composer 过滤展示 → `AIAgentService.ResolveOwnToolDefinitions` 按 `ToolScopePluginIds` 插件白名单挂载给模型。
- 首页排序数据底座：`ForgeSelf.Web/src/stores/usageStats.ts`（localStorage `forge-home-usage-v2`，键=path）+ `src/data/homeEntries.ts`（pinned → 热度衰减 → 原顺序）。

## 测试方式
- 后端：`dotnet test ForgeSelf.Api.Tests`（filter 可按 FQN）。
- 前端单测：`ForgeSelf.Web` 下 `vitest run`（插件测试经宿主 vitest include 收编）。
- e2e：`ForgeSelf.Web` 下 `playwright test`（globalSetup 起隔离宿主 7102）。

## 构建命令
```bash
dotnet build Plugins/AIAgent/AIAgent.csproj
cd ForgeSelf.Web && node node_modules/vue-tsc/bin/vue-tsc.js -b && node node_modules/vitest/vitest.mjs run
# 插件前端出树构建（沙箱内插件目录 pnpm build 不可用）
```
