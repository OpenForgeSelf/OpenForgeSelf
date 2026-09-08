# 028 · 项目工作区（Project Workspace）

> 状态：已实现/已闭环（spec028：数据层 + 命令 CRUD + 运行管理 + 进程检测 + 前端面板；全量相关测试 31/31 通过；前端 `pnpm build` 0 错误）
> 最后更新：2026-08-31
> 关联：`specs/028-project-workspace/`（feature / requirements / design）、架构依据 `docs/01-architecture/host-capability-seams.md`（L1 契约 + L2 事件）

## 概述

项目工作区把「会话选定工作目录即登记为一个项目」的产物，升级为**宿主级数据库管理的项目档案 + 可运行命令 + 运行列表**系统。用户在 sems 插件面板可以：

- 查看所有项目（自动从 AIAgent 选目录登记而来），带类型徽标、标签、描述、git/可达状态；
- 编辑项目档案（类型 / 描述 / 标签）；
- 为每个项目维护一组**运行命令**（名称、脚本、运行后访问地址、排序）；
- 在运行面板**启动**命令、**停止**进程（面板启动或外部捕获）、点「检查」**捕获本机已运行**的该项目进程（WMI 路径+类型归属判定），并对运行中条目提供**快捷访问图标**（新标签页打开访问地址）。

## 关联文档

| 文档 | 位置 | 作用 |
|------|------|------|
| 功能文档 | [`specs/028-project-workspace/feature.md`](../specs/028-project-workspace/feature.md) | 用户视角功能说明 |
| 需求文档 | [`specs/028-project-workspace/requirements.md`](../specs/028-project-workspace/requirements.md) | FR1–FR7 + 验收标准 |
| 设计文档 | [`specs/028-project-workspace/design.md`](../specs/028-project-workspace/design.md) | 表结构 / 接缝 / API / 检测算法 / 前端结构 |
| 架构依据 | [`01-architecture/host-capability-seams.md`](../01-architecture/host-capability-seams.md) | L1 契约（pull 默认）/ L2 事件（push 补充）三层模型 |

## 代码落点

| 层 | 文件 |
|----|------|
| 宿主实体 | `ForgeSelf.Api/Entities/Project.cs` + `Project.Biz.cs`、`RunCommand.cs`（手写实体，ConnName=`ForgeSelf`，落宿主库 `~/.forgeself/ForgeSelf.db`） |
| 宿主接缝实现 | `ForgeSelf.Api/Services/HostProjectRegistry.cs`（`IProjectRegistry` 实现，宿主 `ProvideHostServices` seed 进 root，常驻；含一次性 JSON→库迁移，原文件保留） |
| 契约 | `ForgeSelf.Abstractions/IProjectRegistry.cs`（`ProjectInfo` / `RunCommandInfo` / `ProjectUpdate` / `RunCommandUpdate` DTO） |
| sems 运行管理 | `ForgeSelf.Api/Plugins/Sems/Services/RunnerService.cs`（`RunSession` / `Launch` / `Stop` / `StopExternal` / `CheckAndMerge` / `DetectViaWmi`） |
| sems 控制器 | `ForgeSelf.Api/Plugins/Sems/Controllers/`：`ProjectsController.cs`、`ProjectCommandsController.cs`、`RunsController.cs`（含 `RunnerController.cs` 冗余副本，见「已知问题」） |
| sems 进程匹配 | `ForgeSelf.Api/Plugins/Sems/Services/ProcessMatcher.cs`（路径前缀+分隔符边界+类型二次确认） |
| sems 前端 | `ForgeSelf.Api/Plugins/Sems/web/src/`：`SemsView.vue`（壳+数据装配）、`ProjectCard.vue`、`ProjectEditDialog.vue`、`CommandList.vue`、`RunPanel.vue`、`http.ts`、`types.ts` |

## 架构要点

- **数据供给 = L1 契约 + 宿主 seed（方案 B）**：`Project` / `RunCommand` 入宿主库，经 `IProjectRegistry`（宿主实现，seed 进 root 上下文）供给；AIAgent（登记触发源）与 sems（面板消费方）平等 `ctx.Get<IProjectRegistry>()` 消费，不缓存实例。
- **进程运行管理留在 sems 插件内**（`RunnerService` + `RunsController`）：当前唯一消费方，未升格为宿主接缝；第二个消费方出现时再升 `IProcessRunner`（记 not-to-do 触发条件）。
- **不引入 L2 事件**：运行列表靠请求时拉取 + 手动「检查」，无需实时推送。
- **检测（检查）算法**：一次 WMI 查询全量 `Win32_Process`（ProcessId/Name/ExecutablePath/CommandLine）→ 对每条按「候选路径集合（exe + 命令行出现的绝对路径）是否以某项目 Root 为前缀（带分隔符边界）→ 类型二次确认（node↔frontend/fullstack、dotnet↔backend/fullstack，宁漏勿误）」归属 → 与 Launched 会话按 PID 合并。只读、不落库、每次全量重建 Detected。WMI 不可用则降级仅 Launched。
- **启动 / 停止**：`cmd /c {script}` 在项目根执行；停止用 `taskkill /T /F /PID {pid}`（比 .NET `Kill(entireProcessTree)` 对 `cmd` 子进程树更可靠），Launched 与 Detected 统一按 PID 杀树。

## API（实际路由，grep 实测；全 `[Authorize("ApiKeyPolicy")]`）

| 方法 | 路由 | 说明 | 控制器 |
|------|------|------|--------|
| GET | `api/projects` | 列表（含每项目 commands 概要） | `ProjectsController` |
| PUT | `api/projects/{id}` | 编辑档案（name/type/description/tags） | `ProjectsController` |
| GET | `api/projects/{projectId}/commands` | 命令列表（按 Sort 升序） | `ProjectCommandsController` |
| POST | `api/projects/{projectId}/commands` | 新增命令 | `ProjectCommandsController` |
| PUT | `api/commands/{id}` | 编辑命令 | `ProjectCommandsController` |
| DELETE | `api/commands/{id}` | 删除命令 | `ProjectCommandsController` |
| POST | `api/commands/{id}/run` | 启动（同命令存活会话存在→400 拒绝重复） | `ProjectCommandsController` |
| POST | `api/commands/{id}/stop` | 停止（面板启动的） | `ProjectCommandsController` |
| GET | `api/runs` | 运行列表（Launched + Detected 合并） | `RunsController` |
| POST | `api/runs/check` | 触发 WMI 检测并返回合并结果 | `RunsController` |
| POST | `api/runs/{pid}/stop` | 停止外部捕获进程 | `RunsController` |

> 另存在 `RunnerController`（`api/runner/*`，提供 `commands/{id}/run|stop`、`runs`、`runs/check`、`runs/{pid}/stop` 重叠路由），与上方 `api/commands` + `api/runs` 集合语义重复，前端未使用 —— 见「已知问题」。

`RunSession` 响应字段：`commandId` / `projectId` / `projectName` / `commandName` / `pid` / `startedAt`(ISO；Detected 为 MinValue) / `origin`(`Launched`|`Detected`)。

## 前端结构（sems `web/`，原生 HTML+CSS + `--el-*` 变量）

- `SemsView.vue`：布局壳（标题/统计卡：项目总数 + 运行命令数）+ 项目网格 + 运行面板区；拉 `GET /api/projects` 装配 `commandUrls`/`launchableCount`，编辑弹层挂载。
- `ProjectCard.vue`：类型徽标（frontend/backend/fullstack/library/tool/other 预设配色，自定义走中性色）、标签 chip、描述双行省略、git/不可达徽标、展开内嵌 `CommandList`。
- `ProjectEditDialog.vue`：类型下拉（预设↔自定义自由文本）、描述、标签多标签输入 → `PUT api/projects/{id}`。
- `CommandList.vue`：命令列表（名称/脚本/访问地址图标）+ 新增/编辑/删除（二次确认）/排序（上移下移并持久化 Sort）。
- `RunPanel.vue`：运行列表（项目/命令/PID/已运行时长/来源徽标）；启动全部（遍历调起）；停止（Launched→`api/commands/{id}/stop`，Detected→`api/runs/{pid}/stop`，均二次确认，外部捕获文案明示「杀整棵树可能含他人进程」）；快捷访问 `<a target=_blank rel=noopener>`；检查 `POST api/runs/check`（loading 态）。不常驻轮询。

## 测试覆盖

- 后端（`ForgeSelf.Api.Tests`，spec028 相关共 **31/31 通过**）：
  - `HostProjectRegistryTests`（登记/唯一键/更新/命令 CRUD/迁移幂等）—— XCode SQLite 临时目录。
  - `RunnerServiceTests`（启动-停止进程树、重复启动拒绝、Stop 同步移除会话、外部未知 PID 返回 false）。
  - `ProcessMatchTests`（路径前缀+分隔符边界+类型二次确认：node↔backend 拒绝、dotnet↔backend 命中、fullstack 分隔符边界等）。
  - `SemsSharedContractTests`（Abstractions，4 例：JSON 序列化/反序列化兼容旧共享契约）。
- 前端：构建 `pnpm run build` **0 错误（18 modules）**；组件级 vitest 视项目配置可选（当前 sems web 未配 vitest，未强求）。

## 验证结果（Verification-Centric）

- `dotnet test --filter "Sems|HostProjectRegistry"`：**失败 0 / 通过 31 / 总计 31**（Verified）。
- `cd ForgeSelf.Api/Plugins/Sems/web && pnpm run build`：**0 错误，18 modules transformed**（Verified，主代理亲跑）。
- `dotnet build ForgeSelf.Api`：0 错误；CA1416（WMI windows-only）已在 `RunnerService.cs` 文件作用域 `#pragma warning disable CA1416` 消除（不改 sems.csproj TargetFramework，避免掩盖其他真问题）。
- 遗留清理：`ForgeSelf.Abstractions/SemsShared.cs` 的 `SemsShared`/`ProjectRecord` 已标 `[Obsolete]`；`HostProjectRegistry` 迁移改用内部 `LegacyProjectRecord`，运行态不再依赖 Abstractions 的 `ProjectRecord`（仅契约测试引用以验证 JSON 兼容）；未硬删，避免回归。

## 已知问题 / 待办

### 1. `RunnerController`（`api/runner/*`）与 `RunsController`+`ProjectCommandsController` 路由重叠

**问题本质**：两套控制器提供语义重复的端点（`api/runner/commands/{id}/run` vs `api/commands/{id}/run`、`api/runner/runs` vs `api/runs` 等）。前端仅消费 `api/runs` + `api/commands` 集合，`api/runner/*` 为冗余。

**影响**：路由不冲突（前缀不同），功能不受影响；但维护者易混淆，且任一修改需同步两处。

**建议动作**：后续将 `api/runner/*` 端点统一收敛到 `RunsController` / `ProjectCommandsController`，删除 `RunnerController`（或保留其一作为唯一规范控制器），并在前端/测试锁定规范端点。属低风险重构，非阻塞。

### 2. 宿主重启丢运行态（设计如此）

运行列表来自进程实时状态 + WMI 检测，宿主重启后 Launched 会话清空；「检查」按钮可恢复 Detected 捕获。属 NFR 设计接受项。

## 使用要点

- 项目自动从 AIAgent「选工作目录」登记；也可经 `IProjectRegistry.Register` 程序化登记。
- 运行命令在项目卡片展开后维护；「启动全部」遍历所有命令（已运行由后端 400 拒绝，前端吞掉继续）。
- 停止外部捕获进程前务必确认——杀树可能波及他人进程且不可恢复。
