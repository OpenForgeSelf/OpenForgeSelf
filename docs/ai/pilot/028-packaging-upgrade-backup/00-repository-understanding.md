# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自
>
> **真实仓库内容**
>
> ，禁止凭常识推测。
> 任务：打包 / 升级备份 / 缓存 / 备份 优化（QQNT 式目录结构方向，PILOT-028）｜2026-09-28

## 项目结构



* 解决方案：`ForgeSelf.slnx`（19 项目，2026-09-21 由 .sln 迁移）

* 后端：`ForgeSelf.Api/`（[ASP.NET](https://ASP.NET) Core API，.NET 10 + SQLite + NewLife.XCode 唯一 ORM，插件架构 `Plugins/` + `plugin.json` 注册）

* 前端：`ForgeSelf.Web/`（Vue 3.5 + Vite 6 + TS + Element Plus + pnpm）

* 契约 / 内核：`ForgeSelf.Abstractions/`、`ForgeSelf.Core/`

* 测试：`ForgeSelf.Api.Tests/`（xUnit）、`ForgeSelf.Abstractions.Tests/`、`ForgeSelf.Core.Tests/`

* 发布脚本：`scripts/release/release-local.ps1`（唯一打包入口：build-frontend → publish-host → package-release → make-release-notes，与 CI `.github/workflows/release.yml` 同一命令）、`scripts/release/package-release.ps1`（打包时清 Data/Log/Config/\_backups/pdb + 写 SHA256SUMS）、`scripts/package-plugin.ps1`（038 包源，产 `<id>-<ver>.forgeself-plugin`）

* 升级脚本：`scripts/update-agent.ps1`（036 宿主自更新代理：下载 / 解压 staging → 应用 → 重启宿主）

* 规范 / 工件：`docs/04-standards/`（agent-workflow.md、ai-native-engineering-workflow.md、packaging-upgrade-backup.md）、`docs/ai/pilot/`（AI-Native 工件链）、`AGENTS.md`（Agent 工作手册）

## 技术栈



| 层  | 技术                                                         | 依据（文件 / 配置）                                      |
| -- | ---------------------------------------------------------- | ------------------------------------------------ |
| 后端 | [ASP.NET](https://ASP.NET) Core .NET 10                    | ForgeSelf.Api/ForgeSelf.Api.csproj               |
| 数据 | SQLite + NewLife.XCode                                     | ForgeSelf.Api 配置 / XCodeConfig.cs（唯一 ORM 约定）     |
| 前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 | ForgeSelf.Web/package.json                       |
| 插件 | Plugins/ + plugin.json 清单，运行时版本化侧载（versions/current + ALC） | ForgeSelf.Api/Plugins/\*、PluginVersionService.cs |
| 发布 | PowerShell 脚本编排 + GitHub Actions（release.yml）              | scripts/release/\*、.github/workflows/release.yml |

## 架构特点



* **插件机制**：后端插件通过 `Plugins/` 目录 + `plugin.json` 注册（活动插件目录 = `AppContext.BaseDirectory/Plugins`）；运行期版本化布局 `Plugins/{id}/versions/<ver>/` + `current` 文本指针（`PluginVersionLayout` 原子写）；`PluginVersionService` 负责更新发现 / 执行 / 回滚，`PluginInstallerService` 负责安装 / 卸载 / 包更新；插件数据目录 = `ctx.EnsurePluginDataDirectory()` → `{数据根}/Plugins/{id}`（生产 `~/.forgeself`），随数据走的文件不得放发布目录。

* **宿主更新双链路并存**：`StagedUpdateService.cs`（036 现行：下载解压到 `%LOCALAPPDATA%\ForgeSelf\Updates\<tag>\` 后拉起 `update-agent.ps1`）与 `UpdateService.cs`（008 Windows 服务旧链路）。两者都曾往 `%LOCALAPPDATA%\ForgeSelf\Backups\<ts>\` 做整目录 robocopy 备份、永不清除（update-agent.ps1 原 L58 仅排除 Data/Log/Config/\_backups）。

* **缓存 / 备份堆积点**（真源 §2 六项浪费点）：① 宿主升级整目录备份（Backups/ 永不清）② 更新缓存（Updates// 永不清）③ 插件 `_backups/<id>/<ver>/` 暂存 + 备份双语义 ④ Backups 与插件 `_backups` 双重备份 ⑤ 插件备份（插件本就多版本共存，版本化即回滚能力）⑥ `ImageRecognitionCache`（AppBuilder.cs:247 数据根下）无清理策略。

* **发布铁律（2026-09-27 起）**：禁止 agent 停 / 启 / 杀任何用户宿主进程；宿主升级由 update-agent 自更新（页面点「自动更新」）；活动插件目录只放插件自身 DLL（宿主共享 DLL 入插件目录 → 宿主启动即崩）。

## 测试方式



* 后端：`dotnet build ForgeSelf.slnx` / `cd ForgeSelf.Api && dotnet build`；测试 `dotnet test ForgeSelf.Api.Tests`

* 前端：`cd ForgeSelf.Web && pnpm run check && pnpm run test`

* 插件层 e2e：`e2e/plugins/<id>/<id>.spec.ts`（Playwright，零 mock）

* 项目唯一测试体系铁律：验证 / 截图 / 浏览器驱动一律走 Playwright e2e /vitest/dotnet test，禁止一次性临时脚本当验证（AGENTS.md §5.0/§5.3）

## 构建命令



```
dotnet build ForgeSelf.slnx                 # 全量后端
cd ForgeSelf.Api && dotnet build            # 后端单项目（本任务主门禁）
dotnet test ForgeSelf.Api.Tests             # 后端测试（全量）
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~PluginVersion|FullyQualifiedName~PluginInstaller"   # 插件相关过滤集
cd ForgeSelf.Web && pnpm run check && pnpm run test   # 前端（本任务仅注释改动，未触发）
```

## 主要目录职责



| 目录                                | 职责                                                                                 |
| --------------------------------- | ---------------------------------------------------------------------------------- |
| ForgeSelf.Api/                    | 宿主 API：Controllers/Services/Plugins（插件体系）/Models                                   |
| ForgeSelf.Api/Services/           | 宿主服务：StagedUpdateService（036）、UpdateService（008）、ImageRecognitionCache 等           |
| ForgeSelf.Api/Plugins/            | 插件体系：PluginManager、PluginVersionService、PluginInstallerService、PluginVersionLayout |
| ForgeSelf.Api.Tests/Plugins/      | 插件相关单测（PluginVersionServiceTests、PluginVersionUpdateSourceTests 等）                 |
| scripts/                          | update-agent.ps1、publish-plugin.ps1、release/\*（打包编排）                               |
| docs/04-standards/                | 规范真源（agent-workflow.md Part B；packaging-upgrade-backup.md 本任务真源）                   |
| docs/ai/pilot/                    | AI-Native 工件链（00-07 八件 / 任务）                                                       |
| ForgeSelf.Web/src/types/plugin.ts | 插件类型定义（source 注释同步点）                                                               |

## 代码组织方式



* 后端 Controllers/Services/Entities 分层；插件按 `Plugins/<PascalCase>/` + `plugin.json` 注册；实体改动走 `Data/Model.xml` → `xcode Model.xml` 生成（禁手改生成件）

* 前端组件拆分独立 .vue；样式走 `--el-*` 变量 + Tailwind

* 构建产物：`release-local.ps1` 编排 build-frontend → publish-host → package-release → make-release-notes，产物在 `artifacts/release/`

## 现有工程规范



* `AGENTS.md` §2.3：**打包 / 升级 / 备份 / 缓存 / 安装目录结构唯一真源 =&#x20;**`docs/04-standards/packaging-upgrade-backup.md`（2026-09-28 输入 30 建立）；AGENTS.md/agent-workflow.md/ 功能文档 / 技能只保留操作流程与踩坑，不再重复承载结构事实

* `AGENTS.md` §0/§11：AI-Native 九阶段闭环为开发任务唯一流程；PILOT 工件链门禁（pre-commit hook 校验 `docs/ai/pilot/<task-id>/` 00-07 八件）

* 用户偏好：git 提交 / 推送须用户明确指示；同一任务改动汇总一次性提交；重大决策先请示（本任务批次 2 宿主结构即此类）

* 发布铁律：禁止 agent 停 / 启 / 杀宿主进程；活动插件目录只放插件自身 DLL

## 候选低风险任务



* 插件去 `_backups`/BackupPlugin（改 PluginVersionService/PluginInstallerService/ 侧载脚本 + 测试）—— 中风险（公共服务内部重构，无外部 API 面变化，PluginController 未暴露备份端点）

* update-agent.ps1 去整目录备份 + 更新缓存清理 + Backups 退役 —— 中风险（升级链路行为变化，但为简化而非破坏）

* ImageRecognitionCache TTL 清理 —— 低风险

* 宿主 QQNT 式目录结构（根启动器 + versions/current + plugins 并排 + 发布脚本布局）——**高风险（更新链路形态变更），需用户拍板立项**

## 选择该任务的原因

用户输入 30/31 明确要求：优化打包 / 升级备份 / 缓存 / 备份的空间浪费、统一一处真源、按 QQNT 式组织程序目录；输入 31 明确批评「只改文档不提交代码」，拍板直接改代码落地。真源已建（输入 30），本任务实施代码化改造（批次 1 已完成，批次 2 待立项）。