# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。
> Task ID：PILOT-plugin-dev-experience ｜ 日期：2026-10-01

## 项目结构

根 `D:/src/my-proj/OpenForgeSelf/OpenForgeSelf`：`ForgeSelf.slnx` + 后端宿主 `ForgeSelf.Api/` + 前端 `ForgeSelf.Web/` + 后端测试 `ForgeSelf.Api.Tests/` + 抽象层 `ForgeSelf.Abstractions/`（`ForgeSelf.Abstractions.Tests/`）+ `ForgeSelf.Core/`（`ForgeSelf.Core.Tests/`）+ 启动器 `ForgeSelf.Bootstrapper/` + **18 个插件源码 `Plugins/`** + 构建脚本（`build.ps1`、`scripts/*.ps1`、`scripts/release/*.ps1`）+ 文档 `docs/` + 已弃用的 `specs/`。

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 宿主后端 | .NET 10 / ASP.NET Core / SQLite / NewLife.XCode（唯一 ORM） | `ForgeSelf.Api/ForgeSelf.Api.csproj`、`AppBuilder.cs:104` `AddXCode` |
| 宿主前端 | Vue 3 + Vite 6 + TS 5.7 + Element Plus + Tailwind + Pinia + pnpm | `ForgeSelf.Web/vite.config.ts`、`ForgeSelf.Web/index.html` |
| 后端测试 | xUnit + Moq + FluentAssertions | `ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj` |
| 前端测试 | vitest + Playwright（三份配置） | `ForgeSelf.Web/vitest.config.ts`、`playwright.config.ts` / `.e2e-published` / `.live` |
| 日志 | NewLife `XTrace.Log`（**非** Microsoft.Extensions.Logging） | `ForgeSelf.Api/Services/LogService.cs:16/24/32/40` 转发到静态 `XTrace.Log` |
| 打包/发布 | PowerShell 脚本 + tag 触发 CI | `scripts/`、`scripts/release/`、`build.ps1` |

## 架构特点

- **插件装载**（`ForgeSelf.Api/Plugins/PluginManager.cs`）：扫描 `{BaseDirectory}/plugins` 下含 `plugin.json` 的目录（`:266-307`，只校验 `Id` 非空）→ 按 `Dependencies` 拓扑排序（`:834-874`）→ `new PluginLoadContext(path, id)`（`PluginLoadContext.cs:25-30`，`isCollectible: true` + `AssemblyDependencyResolver`）→ `LoadFromAssemblyPath` → `Activator.CreateInstance` 出 `IPlugin`（`IPlugin.cs:5-9`，契约仅 `void Apply(IContext ctx)`）→ Build 后注册 MVC `AssemblyPart`（`AppBuilder.cs:344-369`）。
- **卸载链路已完备**：`PluginManager.cs:712-760` Disable → 移除 AssemblyPart → Unmount 插件 DI 子容器 → fiber.Dispose → `ALC.Unload()`；`PluginAssemblyUnloader.cs:15-30` 强制 GC + FileStream 独占探测。
- **无 shadow copy**：ALC 直接从 `versions/<current>/<EntryAssembly>` 加载（`PluginVersionLayout.cs:64-86`），因此运行中 DLL 被进程独占锁。
- **文件 watcher 已删除**：`AppBuilder.cs:331-333` 注释明确「插件文件级热更新监听已移除；一律走版本化显式更新 `POST /api/plugin/update/{id}`」。
- **版本化侧载**：`PluginVersionService.UpdatePlugin:152-184` 要求 stage 版本**严格大于**当前版本（`Compare <= 0` 即 return），`ActivateVersion:215-267` = Disable → ForceCollect → TryOpenExclusive → 写 `current` → 同步清单 → RefreshMetadata → Enable → Prune（保留 2 版）。
- **前端动态加载**：`main.ts` 拉 `/api/plugin/frontend-manifest` → `router/dynamicPlugins.ts:130` 注册路由 → `utils/pluginViewLoader.ts:146` 运行期 `import(entryUrl)` 远程加载 `web/dist/index.js`；共享依赖靠宿主 `index.html:13-21` 的 **import map** 指向 `/shared/*.js` shim（`public/shared/` 下有 vue/vue-router/pinia/element-plus/element-plus-icons 五个 js）。
- **静态资源服务**：`PluginFrontendFileMiddleware`（`PathPrefix="/plugins"`，只允许 `web/` 下白名单 MIME，拒路径穿越）；带 `?v=` 时下发 `public, max-age=31536000, immutable`（`:126-128`），不带则 `no-cache`；root 解析优先 `versions/<current>/web` 回退扁平 `web`（`:158-178`）。
- **破缓存指纹**：`PluginController.ComputeWebVersion:374-415` 对 `index.js + style.css` 做 SHA256 前 6 字节 ×2 拼接 → 内容变化即可破缓存，**无需升 plugin.json 版本**。
- **四层部署布局**（`docs/04-standards/packaging-upgrade-backup.md` §1.6）：根启动器 / `versions/<ver>/` 业务层 / `plugins/<id>/`（与 versions 并排）/ 数据层 `~/.forgeself`。

## 测试方式

- 后端：`dotnet test ForgeSelf.Api.Tests`（⚠️ 须带 verbose logger——`docs/04-standards/agent-workflow.md:607-610` 记录 quiet logger 会**假绿**）；过滤 `dotnet test --filter "FullyQualifiedName~<X>"`。
- 前端：`cd ForgeSelf.Web && pnpm run check`（lint+typecheck）与 `pnpm run test`（vitest，include 覆盖 `../Plugins/*/web/src/**/*.test.ts`）。
- e2e：`pnpm exec playwright test e2e/plugins/<id>`；`e2e/global-setup.ts` 起隔离宿主（`FORGESELF_PORT` + `FORGESELF_DATA_ROOT` + 动态端口认领），冷启最长等 120s。⚠️ 全量一轮 ≈38 分钟。

## 构建命令

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug --nologo -v m
dotnet test ForgeSelf.Api.Tests --logger "console;verbosity=detailed"
cd ForgeSelf.Web && pnpm run check && pnpm run test
cd Plugins/<X>/web && pnpm run build          # 插件前端（沙箱内改用 scripts/build-plugin-web.ps1）
pwsh scripts/publish-plugin.ps1 -Plugin <X> -Force
curl.exe -X POST http://localhost:7102/api/plugin/update/<kebab-id>
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Api/Plugins/` | 插件装载器与版本化服务（**注意：是运行时代码，不是插件源码目录**） |
| `Plugins/<PascalCase>/` | 插件源码：`plugin.json` + `.csproj` + `Controllers/` `Services/` `Data/` + `web/`（9 个插件有） |
| `ForgeSelf.Web/e2e/plugins/<id>/` | 9 个插件的 Playwright e2e |
| `scripts/` | 发布/打包/审计脚本（**无启动开发宿主脚本**） |
| `docs/04-standards/` | 真源级工程规范（packaging-upgrade-backup / agent-workflow / ai-native-engineering-workflow） |
| `.agents/skills/` | 流程技能（plugin-development 等） |

## 代码组织方式

宿主业务逻辑在 `ForgeSelf.Api/`，契约在 `ForgeSelf.Abstractions/`；插件经 `<ProjectReference ReferenceOutputAssembly="false">` 列入宿主 csproj（`ForgeSelf.Api.csproj:77-106`）仅建立构建顺序，`StageAllPlugins` target（`:109-138`）把产物复制到宿主输出目录 `plugins/`。日志一律走静态 `XTrace.Log`（宿主 58 处 + **插件 177 处**），`ILogService` 虽已 Scoped 注册（`AppBuilder.cs:177`）并 seed 进插件根 Context（`PluginManager.cs:229`），但极少被使用（仅 `Plugins/AIAgent/Services/AIAgentService.cs:20`）。

## 现有工程规范

| 条目 | 位置 | 对本任务的约束 |
| --- | --- | --- |
| 禁止 agent 停/启/杀用户运行中的宿主进程 | `AGENTS.md:30/95/107`、`packaging-upgrade-backup.md:164` R8③ | 不得用重启宿主实现热重载 |
| 活动插件目录只放插件自身 DLL，禁拷宿主共享 DLL | `packaging-upgrade-backup.md:164` R8②、`.agents/skills/plugin-development/SKILL.md:390-391` | shadow 目录必须排除 `ForgeSelf.*`/`NewLife.*`/`XCode.dll` |
| 端口禁止硬编码 7102/7002 | `AGENTS.md:94` | dev 端点不得写死端口 |
| 禁止手写一次性 `temp/*.cjs` 作为验证手段 | `AGENTS.md:37-41` | 验证必须走 dotnet test / vitest / playwright |
| dotnet test 须带 verbose logger | `agent-workflow.md:607-610` | 验证环节强制 |
| 插件任务五步门禁 | `AGENTS.md:30` | 本次属宿主改动，须评估是否触发 |

## 候选低风险任务

本次进入 Stage 0 时任务已由用户指定（插件开发/调试体验优化），并已在计划轮完成选型与范围裁决（P1+P2+P3 全套 + 完整工件链），因此**不做低风险任务重选**；候选列表保持为空并由 `2026-10-01-plugin-dev-experience` 单任务承接：切片化交付（P1 止血 / P2 正餐 / P3 增强），每片独立可验收、独立回滚，以此把原本的大范围改动降级为可控风险。

## 选择该任务的原因

用户直接指派（输入57）。其技术必要性由三条实测事实支撑：① 侧载更新被语义锁死在同一版本号之后（`PluginVersionService.cs:167-173`）；② ALC 直接加载导致 DLL 不可覆盖，叠加禁停宿主铁律后无本地优雅解法；③ 实测 `publish/plugins/AIAgent/web/dist/index.js`（09-26）落后源码产物（09-29）**3 天**，即"改了但没生效"已被实证。
