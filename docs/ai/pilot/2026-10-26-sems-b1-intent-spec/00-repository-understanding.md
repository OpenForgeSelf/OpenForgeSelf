# Repository Understanding

> 阶段：Stage 0｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> Task ID：2026-10-26-sems-b1-intent-spec
> 原则：条目均来自本会话实际读取的文件；未读取或未复核的写 Unknown。

## 项目结构（已读）

- 仓库根含 ForgeSelf.slnx、ForgeSelf.Web/、ForgeSelf.Api/、ForgeSelf.Api.Tests/、Plugins/（20 个插件）、docs/、scripts/、AGENTS.md、TODO.md。
- Plugins/Sems/ 含 bin/、Controllers/、obj/、Services/、web/，以及 plugin.json、Sems.csproj、SemsPlugin.cs、TODO.md、ToolExtensions.cs，另有 docs/requirements.md（本会话新建）。

## 技术栈（依据文件）

| 项 | 内容 | 依据 |
| --- | --- | --- |
| 宿主后端 | .NET 10（net10.0-windows）、SQLite、NewLife.XCode | ForgeSelf.Api/ForgeSelf.Api.csproj |
| Sems 插件 | net10.0，引用 NewLife.Core、System.Management，ProjectReference 仅 ForgeSelf.Core 与 ForgeSelf.Abstractions | Plugins/Sems/Sems.csproj |
| 前端 | Vue 3.5 + Vite + TS + Element Plus + pnpm（未复核版本细节） | AGENTS.md、技能 plugin-development |
| 版本号 | major.minor.patch.yyMMddHHmm | AGENTS.md、ForgeSelf.Api.csproj |

## Sems 现状（已读）

- 版本 1.1.1，名称「软件工程管理系统」，入口 /sems，视图 SemsView（Plugins/Sems/plugin.json）。
- 项目数据归宿主 IProjectRegistry 接缝，ProjectService 经 ctx.Get 每次重新解析；接缝不可用时返回 503 或空列表（Services/ProjectService.cs）。
- 运行管理在 RunnerService：cmd.exe /c 启动、taskkill /T /F 杀进程树、WMI 检测，会话表在内存，宿主重启清空（Services/RunnerService.cs）。
- 13 个 sems_* 工具统一封套 {success,data|error}，委托 IProjectService / IRunnerService，与 HTTP 端点共用实现（ToolExtensions.cs、SemsPlugin.cs）。
- Sems 自身没有实体库：在已读文件内未见 XCode 实体或建表代码。注意：Controllers/ 与 web/ 正文未读，此结论只覆盖已读文件。
- 已读文件中没有任何「规格」「验收标准」「变更关联」的概念，B1 的「Sems 现状」按未实现的方向倾向，但 requirements.md 仍标「未核实」，以全量读完为准。

## 架构与工程规范（已读，来自 AGENTS.md 与技能 plugin-development）

- 插件前端不得 import 宿主模块；菜单与路由只在 plugin.json frontend 声明一处。
- 有实体库的插件：改 Data/Model.xml 后用 xcode 生成；连接名必须登记进 ForgeSelf.Api/Data/XCodeConfig.cs 的 PluginDbs；ForgeSelf.Api.csproj 与测试工程的引用须齐；插件必须自行建表；禁止任何清库、删库自动化。
- 管理面控制器必须类级 [Authorize("ApiKeyPolicy")]。
- 插件任务六步门禁：门禁构建与测试、1 个冒烟 e2e、起源码本地验收、发布、运行实例只读复验、更新文档与日志。
- 禁止 agent 停、启、杀用户运行中的宿主进程。

## 测试与构建入口

- 后端 dotnet build 与 dotnet test（ForgeSelf.Api/、ForgeSelf.Api.Tests/）；前端 pnpm run check、pnpm run test；插件前端在其 web/ 下 pnpm。
- Sems 是否已有单测与 e2e 文件：Unknown，实现前须 list ForgeSelf.Api.Tests 与 e2e 目录核对。

## 打包链路（已读）

- StageAllPlugins 只拷插件 bin 输出、plugin.json、web/dist；scripts/package-plugin.ps1 只收 plugin.json、web/dist 与 publish 输出。因此 Plugins/Sems/docs/ 不进包；若 B1 选「落文件」且要在运行时读取规格文件，须另定落位，不能依赖源码目录的 docs/。

## 适用问题类

- 本任务是新功能，不是缺陷、回归或返工类。未查询 docs/11-troubleshooting/problem-classes/README.md 的 tags 表。写「无」，但实现阶段若发现缺陷须补查。

## 候选低风险任务

- 本任务不是 Pilot 试点选题，而是 Sems 需求 B1 的落地。风险点：涉及新增存储，可能触及数据库结构（硬性约束需闸门 1 授权）。

## Unknown

- Sems 现有测试与 e2e 文件。
- Controllers/ 与 web/ 的实现细节。
- 宿主 IProjectRegistry 能否扩展承载规格数据（未读宿主接缝定义）。
