# Repository Understanding

> Task ID: PILOT-079｜日期: 2026-11-07｜来源: 输入8（用户直接指令=闸门1 依据）
> 现场复核基于本仓真实内容 + 2026-11-07 :51888 实例演示实证（`.forgeself/memory/2026-11-07.md` 输入6/7）。

## ① 项目用途
OpenForgeSelf（铸己匣）：个人可扩展的 AI Agent 工具平台。宿主（ForgeSelf.Api）+ Vue 3 SPA（ForgeSelf.Web）+ 插件架构（`Plugins/<X>` + plugin.json 清单 + 宿主三处登记）。内置 AIAgent 插件提供对话式 Agent，可经出口工具（设计/文件/终端/调度/审批等）自主开发插件。

## ② 主要技术栈
- 后端：.NET 10（net10.0-windows）+ ASP.NET Core + NewLife.XCode（唯一 ORM，SQLite）
- 前端：Vue 3.5 + Vite 6 + TS 5.7 + Element Plus + Tailwind 4 + pnpm
- 脚本：pwsh 7（禁 powershell 5.1）

## ③ Backend/Frontend 结构
- `ForgeSelf.Api/`：宿主（AppBuilder 装配、PluginManager 加载插件、ToolRegistry 出口工具表、ForgeSetting 配置）
- `ForgeSelf.Web/`：SPA 壳 + 插件视图远程加载（plugin.json → web/dist/index.js）
- `Plugins/<X>/`：插件（后端 .cs + 可选 web/）；`ForgeSelf.Core/Context.cs` IContext=IServiceProvider（本地+共享表，非 MS-DI 容器）；`ForgeSelf.Abstractions/PluginContextExtensions.cs` RequireHostProvider/CreateHostScope=执行期取宿主 provider 的规范接缝
- 插件依赖桥：`PluginServiceRegistry.CollectForwardDescriptors` 把插件 ServiceCollection 转发进宿主容器（AppBuilder.cs:378-381）

## ④ 测试方式
- 后端 `ForgeSelf.Api.Tests/`（xUnit）：全量 `dotnet test`（基线红清单见 AGENTS §5.6）；架构守卫 `Tests/Architecture/*GuardTests`；问题类守卫由 `ProblemClassRegistryTests` 兜底
- 前端：`pnpm run check` + `pnpm run test` + 插件层 e2e（Playwright）
- 缺陷类：`problem-class-closure` 闭环（先红后绿），`scripts/check-problem-classes.ps1` 机器校验

## ⑤ 构建方式
- 后端 `dotnet build`（基线 474 警告）；插件前端 `cd Plugins/<X>/web && pnpm install && pnpm run build`
- 本地起前后端**唯一**入口：`pwsh scripts/dev-stack.ps1`（默认后端 7301 + 前端 7399，占用顺延；隔离数据根 `.temp/dev-stack/data`；实例 ID 隔离不影响运行中的 :51888；状态落 `.temp/dev-stack.json`；`-Stop` 清理）

## ⑥ 主要目录职责（本批触碰面）
| 路径 | 职责 |
|---|---|
| `Plugins/SmartTerminal/` | 终端会话插件（st_* 工具×8，四 shell） |
| `Plugins/McpCenter/Services/PublicTools/` | run_terminal_command 等公共出口工具 + TerminalCommandGuard 校验 |
| `Plugins/AIAgent/Services/` | 对话回合、模型解析（ChatModelResolver）、审批、调度、工具编排 |
| `ForgeSelf.Api/Models/ForgeSetting.cs` | NewLife [Setting] 宿主配置 |
| `ForgeSelf.Api.Tests/Architecture/` | 插件 DI/守卫类架构测试 |

## ⑦ 代码组织方式
Controllers/Services/Entities 分层；插件经 IContext 与宿主互通（ctx.Register 共享表 / PluginManager.HostProvidedServiceContracts 宿主契约清单）；出口工具经 ToolRegistry 按 enabledToolNames 过滤暴露给 Agent。

## ⑧ 已有工程规范
AGENTS.md（§0 预飞/§5.6 门禁分档/§11 AI-Native 闭环）+ `docs/04-standards/ai-native-engineering-workflow.md` v1.2.0 + agent-workflow/README Part B + problem-class-closure 技能。触碰区域命中的问题类：**PC-013**（P1②/P3② 同族复发）、**PC-005**（P1① 同族，10-17 TodoTracker 同错误串）、PC-011（终端输出码页裁决器，改 RunTerminalCommand 输出面时必须复用 ProcessOutputText 单点）、PC-017（工具 schema 合法性，改 schema 时适用）、PC-018（工具 success 必有副作用落定，重定向落盘测试适用）。

## 候选低风险任务及选择理由
本任务 = 用户点名的缺陷修复批（7 项，输入7 记录、输入8 批准修复）+ 本地实例复测迭代。均为既有行为修复，不新增依赖、不动 DB 结构、不动鉴权；风险集中在 AIAgent 回合收束语义（P1②）与终端工具放权（P2①），由先红后绿 + 既有守卫回归覆盖。
