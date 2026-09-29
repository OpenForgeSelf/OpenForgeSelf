# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式｜状态只报事实，禁止模糊表述（对齐 AGENTS.md §10.4/§10.5）。
> 本报告覆盖批次 1（输入31）+ 批次 2（输入34）全链终态。

## 1. Repository Understanding

我确认了：打包/升级备份/缓存/备份相关规范散落 7 处；宿主更新双链路（036 StagedUpdateService + update-agent.ps1 / 008 UpdateService）都做整目录备份且永不清除；插件 `_backups` 暂存+备份双语义（新版本两次复制）；ImageRecognitionCache 无清理策略；发布主路径 = 打 tag 自动发布 + 页面自动更新（禁停宿主）；安装目录为扁平全量覆盖布局。

## 2. Selected Task

PILOT-028：打包/升级备份/缓存/备份 优化（QQNT 式方向）。输入30 统一真源（已提交）；输入31 用户拍板改代码落地——批次1（去插件 `_backups`、去宿主整目录备份、更新缓存清理、图片缓存 TTL）已实施并提交 aed3789；输入34 用户拍板立项批次2——宿主 QQNT 目录结构 + 008 冻结 + 插件装包版本化已实施（未提交，待用户指示）。

## 3. Changed Files

**批次 1（已提交 aed3789 + 推送双远程）**：
- `PluginVersionService.cs`（重写去 `_backups`）、`PluginInstallerService.cs`（删三处 BackupPlugin）
- `scripts/update-agent.ps1`（去整目录备份 + 清 Updates/<tag>/ + Backups 退役）、`publish-plugin.ps1`（stage → versions/）
- `LocalFileImageRecognitionCache.cs`（TTL 30 天）
- 测试 2 件、`PluginDetailDto.cs` + `types/plugin.ts`
- 文档 6 处（真源/035/038/agent-workflow/Plugins README/guides）+ AGENTS.md §2.3

**批次 2（未提交 git，未获提交授权）**：
- `ForgeSelf.Bootstrapper/`（新建根启动器：versions/current 指针 + ALC 版本目录解析 + Assembly.EntryPoint 调 Main）
- `AppBuilder.cs` / `Program.cs`（路径基准改入口程序集目录 + tray 收敛 036）
- `UpdateService.cs`（008 冻结）/ `StagedUpdateService.cs`（唯一链路头）
- `PluginVersionService.cs`（+StageUploadedPackage）/ `PluginInstallerService.cs`（UpdateFromPackage 版本化重写）
- `PluginInstallerServiceTests.cs`（新建 4 用例）
- `scripts/update-agent.ps1`（QQNT 版本化应用重写）
- `scripts/release/publish-bootstrapper.ps1`（新建）+ `publish-host.ps1` + `package-release.ps1` + `release-local.ps1`（QQNT 布局）
- `ForgeSelf.slnx`（add Bootstrapper）
- 真源 `docs/04-standards/packaging-upgrade-backup.md`（批次 2 标记）

## 4. Validation

Build: ✅ 0 error（322 存量 warning，Verified）
Unit Test: ✅ 批次1 过滤集 56/56；批次2 终态过滤集 23/23（Verified）；全量 1542 过/9 失败（全部既有四族 + 040-B1 在飞区，零交集，Verified）
端到端演练（批次2，全部 Verified）：
- QQNT 布局冒烟：根启动器拉起 versions/2.2.10 业务层，ContentRoot = versions/2.2.10，插件目录 = 公共根/Plugins
- update-agent 版本化应用 8 项全过（versions/2.3.0 落盘、current 切换、扁平残留清理、旧版保留、Plugins 合并、staged/Backups 清理、重启段启动器拉起）
- 发布管线：v2.2.10 zip 65.3MB（旧扁平 72-78MB）+ SHA256SUMS + RELEASE-NOTES
E2E: N/A（无 UI 行为变化；升级链路以端到端演练替代）

## 5. Evidence

详见 `docs/ai/pilot/028-packaging-upgrade-backup/05-evidence.md`（全部 Verified 实测：build 输出、过滤集、端到端演练逐项、发布产物尺寸、已知限制与未决项）。

## 6. Review

`docs/ai/pilot/028-packaging-upgrade-backup/06-review.md`：Final Decision = **APPROVED（批次1 + 批次2）**；风险 L2（批次2 高影响面已通过演练实测降低；未触碰用户运行实例）；批次2 未提交 git 如实标注。

## 7. Risk

L2。批次1 L1；批次2 L3 高影响面（宿主路径基准/更新链路形态）——已通过隔离实例冒烟 + update-agent 演练 + 发布管线实测降至 L2；**未触碰用户运行实例（51888 / D:\src\tools\ForgeSelf），未停/启/杀任何宿主进程**；批次2 未提交 git 等用户审核。

## 8. Problems Found

- 全量测试 9 条既有失败（WorkflowPlanning 404×6 等）与 040-B1 在飞区并存——非本任务引入，dsh 收口时处理。
- `DataStoragePanel.vue`「清除缓存」按钮未见后端 API 接线（P2 TODO 已登记）。
- C 盘 4GB 剩余，测试 TEMP 走 D 盘绕开（环境问题）。
- 冒烟遗留僵尸进程（Access denied）：不监听不占 mutex。
- 端口真源（51888 vs 7102）待用户拍板统一。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS（全仓盘点落真源 §1） |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS（批次1 + 批次2） |
| Code → Test | PASS（56/56 + 23/23 + 演练 + 全量归因） |
| Test → Evidence | PASS（全 Verified） |
| Evidence → Review | PASS（APPROVED 批次1+2） |

## 10. 最重要的问题

批次1 因用户输入31「直接改代码落地」先行实施，工件链在输入33 后补齐供审核（九阶段闸门1 正常时序为「先工件、后实施」，本次为满足用户"只看最终结果"反向补齐）；批次2 在输入34 拍板后按同一工件链推进，**自始未提交 git**，等待用户明确提交指示。

## 11. 下一步建议

用户审核本工件链 + 真源（`docs/04-standards/packaging-upgrade-backup.md`）后：① 明确提交指示 → 批次2 全部改动与 040-B1 分开批次一次性提交（pre-commit hook 校验本任务 00-07 已齐）；② 打 tag 发布走发布主路径（tag → CI → GitHub Release → 页面自动更新，宿主自更新，agent 不停宿主）；③ 遗留项（DataStoragePanel 清除缓存接线、端口真源统一、C 盘空间）按 TODO 队列推进。

---

## 输入36 单文件化追加（2026-09-28）

**状态**：PARTIALLY_COMPLETED（公共层已交付并 Verified；业务层 publish 被 040-B1 在飞区编译错误阻断待验）。

- **已落地**：① 清理 artifacts 918MB（用户批准「可清」）；② Bootstrapper 改**进程拉起模式**（FDD 单文件业务层是 apphost，弃 ALC，Start 子进程 + DOTNET_ROOT=安装根 + WaitForExit 透传退出码）；③ **FDD 单文件方案**（公共层 ForgeSelf.exe ~170KB managed-only bundle + DOTNET_ROOT 结构运行时 = 本机 dotnet host/fxr + shared 三框架 + 根 app-local shim；业务层 publish-host 加 PublishSingleFile + native/content 外置）；④ package-release 适配（业务层 exe 保留不再删）。
- **Verified**：公共层组装 627 文件 + 启动器 0.16MB；链路演练（启动器→versions/current→子进程→退出码 7 透传，业务层 BaseDirectory=versions/<ver>）；组装 zip 83.9MB；**自包含单文件弃用踩坑**（解压缓存 + BaseDirectory/ProcessPath 指向提取目录，实测）。
- **未验证（040-B1 阻断）**：业务层真实 publish（AgentHub `IAgentRegistry` 二义 CS0104/CS0311，并行会话在飞区，本任务不碰）+ TerminalCommandGuard 断言验证。待 040-B1 收口后 `release-local.ps1` 全量复验 + update-agent 端到端升级演练。
- **未提交 git**（用户偏好：提交须明确指示；同任务一次性提交）。
- **下一步**：040-B1 收口 → 全量复验真实业务层单文件 → 端到端升级演练 → 用户审核后一次性提交（批次2 + 输入36 合并）。


---

## 输入37 目录命名统一小写追加（2026-09-29）

**状态**：COMPLETED（目录命名统一小写全量落地；未提交 git，等用户指示）。

**Selected Task（追加）**：输入37 在 PILOT-028 框架内追加第三批——安装/运行布局目录统一小写（plugins/data/log/config）+ NewLife 日志外置（查 DeepWiki NewLifeX/X 后落地）。

**Changed Files（输入37 追加）**：C# 8 文件（AppBuilder/DataLocationService/IDataLocationService/ServiceCollectionExtensions/Program/ForgeConfig/ImGateway×2）+ 测试断言 8 文件 + 脚本 7 件（update-agent §6.5 目录名规范化 / package-release / publish-host / release-local / publish-bootstrapper / migrate-plugin-versions / publish-plugin）+ 仓库目录 `build/runtime/Plugins`→`plugins` + 文档 11 件（真源/AGENTS.md/agent-workflow/034/032/035/052/configuration/Plugins README/DesignSystem/plugin 技能）+ 探针工程 `.forgeself/test-tmp/nl-probe`。详见 05-evidence 输入37 节。

**Validation（输入37）**：`dotnet build` 0 errors（040-B1 已收口）✅；目录相关单测 6/6 绿 ✅；NewLife 探针实测 ✅；脚本 AST 7/7 ✅；规范化 dummy 含幂等 ✅；全量 597 失败归因系统 Temp 环境拦截（XCodeTestFixture 共享夹具，git status 确认未触碰，与本任务零交集）⚠️（登记 TODO）。

**Evidence / Review**：05-evidence 输入37 节（全 Verified）；06-review 输入37 节（APPROVED）。

**Risk**：L2（低～中）。路径基准语义不变、Windows 大小写兼容存量、update-agent 幂等规范化兜底；日志外置为启动早期单点设置。未触碰用户运行实例（51888 / D:\src\tools\ForgeSelf）。

**Problems Found（输入37 追加）**：
- 全量测试 597 失败 = `XCodeTestFixture` 写系统 Temp 被拒（环境问题，与本任务零交集；TMP 重定向 testhost 崩溃证实环境级不稳定）→ TODO P1。
- 系统 Temp 残留 757 个测试目录（历史从不清理）→ 随上述 TODO 一并处置。
- 批次2 + 输入36 + 输入37 全部改动未提交 git（等用户明确提交指示；git add 精确隔离 040-B1）。

**下一步建议**：用户审核真源 + 工件链后：① 明确提交指示 → 批次2+输入36+输入37 汇总一次性提交（pre-commit hook 校验 00-07 齐）；② 打 tag 走发布主路径（tag → CI → GitHub Release → 页面自动更新）；③ TODO 队列推进（测试 Temp 环境、DataStoragePanel 清除缓存接线、端口真源统一、TerminalCommandGuard 断言验证）。


---

## 输入37 发布复验与日志外置修正追加（2026-09-29 二次）

**状态**：COMPLETED_WITH_RISK（发布全链路 v2.2.11 验证通过 + XTrace.LogPath 外置修正验证通过；运行残留 TODO P2；040-B1 编译阻塞为外部因素）。

**Selected Task（追加）**：输入37 收尾复验——发布全链路（业务层真实 FDD 单文件）+ QQNT 小写布局冒烟 + 暴露遗留修复（LogPath 外置生效）。

**Changed Files（二次追加）**：`ForgeSelf.Api/Program.cs`（顶部 ConfigUnifier 前 `XTrace.LogPath = {数据根}/log` + Setting 同步持久化，首次日志写入前）、`ForgeSelf.Api/AppBuilder.cs`（同款幂等设置）；真源 packaging-upgrade-backup.md（R9/T8/变更记录 3 处修正 + 复验记录）；PILOT-028 05/06/07 追加节。

**Validation（二次追加）**：`release-local.ps1 -Version v2.2.11` 全链路 PASS（zip 101.8MB，QQNT 小写布局无大写残留）✅；Api 本体 `-p:BuildProjectReferences=false` 编译 0 errors ✅；探针对照（Setting vs XTrace.LogPath）实测 ✅；开发态冒烟：日志落 data/log、程序目录无 Log ✅；全量 build 受 040-B1 外部阻塞 ⚠️。

**Evidence / Review**：05-evidence 二次追加节（全 Verified）；06-review 二次追加节（APPROVED，COMPLETED_WITH_RISK）。

**Risk**：L2。日志外置为启动早期单点设置（双处幂等）；运行残留为库行为已登记 TODO；未触碰用户运行实例。

**Problems Found（二次追加）**：
- 运行态 XCode 探测在版本目录生成 Plugins/（SQLite 3 件 ~15MB/版本）→ TODO P2（update-agent 清理非当前版本候选）。
- 040-B1 并行会话破坏 Abstractions/Core 接口 → AIAgent 编译断（CS0019，非本任务文件，git status 实证）→ 待 040-B1 收口。
- 全部改动未提交 git（等用户明确指示；git add 精确隔离 040-B1）。

**下一步建议**：① 用户审核真源 + 工件链 + `artifacts/layout/` 效果目录；② 040-B1 收口后跑全量 `dotnet build`+`dotnet test` 复验；③ 明确提交指示 → 批次2+输入36+输入37 汇总一次性提交（pre-commit 校验 00-07）；④ TODO 队列推进（测试 Temp 环境 P1、运行残留清理 P2、DataStoragePanel 接线 P2、端口真源、TerminalCommandGuard 断言）。

---

## 输入38：SQLite 驱动正式依赖化（2026-09-29）

[状态] 任务完成（输入38）

任务：安装 `XCode.SQLite` 包使 SQLite 驱动成为正式依赖，根除 XCode 运行时「探测 → 建 Plugins/ + 外网下载」路径（输入37 遗留 ~15MB/版本运行残留），并保持单文件发布兼容。

完成内容：
- csproj：`XCode.SQLite` 11.24.2026.302（依赖 XCode ≥11.25 不升主版本）+ Target `ExcludeSqliteFromSingleFile`（FilesToBundle Remove System.Data.SQLite.dll）
- publish-host.ps1：publish 后从 NuGet 缓存外置复制 System.Data.SQLite.dll（兜底）
- package-release.ps1：删 inject 块；`build/runtime/plugins` 移 `.trash/`
- 文档：真源 §1.1 + §5 变更记录、agent-workflow.md L611/729/736/451 同步

验证结果（全部 Verified）：
- ✅ 探针：XCode 12.0 + XCode.SQLite 共存、方言注册、真实 SQL rows=1、无 Plugins/ 生成
- ✅ dev-bin 冒烟：`加载 ...System.Data.SQLite.dll v2.0.2.0` 零下载 + 8 库连接成功 + bin/Plugins 未生成
- ✅ 单文件发布：exe 16,424→16,036KB（-388KB 内嵌移除）+ 外置 386KB
- ✅ 单文件最终冒烟：落盘命中零下载、8 库 `数据库连接成功 (ServerVersion=3.50.4)`、无 Plugins/、7102 监听
- ✅ 脚本/仓库清理：inject 块删除、build/runtime 移 .trash、无残留引用
- ⚠️ 已知环境项（非本任务）：XCodeConfigTests 4 失败（系统 Temp 拦截，记 TODO）；托盘 H.NotifyIcon 异常（无桌面会话，宿主服务正常）

设计决策：XCode 12.0 SQLite provider 只认文件探测（显式工厂三变体实测抛异常，方向封死）→ 唯一解法 = 让探测命中本地文件：包依赖落盘（多文件）+ 单文件剔除内嵌（bundle）双管齐下。

代价·收益：exe 单文件减 388KB；彻底消除每版本 ~15MB 运行残留与升级后的空间累积；不再依赖外网下载（离线可用）；发布脚本少一个注入步骤。

不做事决策：不升级 XCode 12.2 主版本（需新版本 XCode.SQLite 12.x 配套，记未来选项）。

风险：无阻塞。托盘异常与环境 Temp 问题均已隔离说明。

结论：完成 · 可交付 · 未提交 git（等用户明确指示；提交时汇总 028 全批次并隔离 040-B1 文件）。

### 输入39（版本号生成 + 文件图标 + 包信息）

[状态] 任务完成（输入39 · 028 系列收尾）
任务：参照 CrazyCoder.csproj 完善本项目版本号自动生成、文件图标与包信息设置。
完成内容：① ForgeSelf.Api.csproj + ForgeSelf.Bootstrapper.csproj 加 CrazyCoder 式版本生成（VersionPrefix 2.2 + 日期后缀 yyyy.MMdd → FileVersion 2.2.2026.0929，AssemblyVersion 2.2.* 通配，Deterministic=false）；② Bootstrapper 补 ApplicationIcon（复用 Api 的 Assets\\ForgeSelf.ico）——公共层根 ForgeSelf.exe 从此带图标；③ 包信息（AssemblyTitle 铸己匣 / 铸己匣启动器、Description、Company=OpenForgeSelf、Product=ForgeSelf（铸己匣）、Copyright=©2026 OpenForgeSelf）。
验证结果（Verified）：两 exe 文件属性逐项实测正确（FileVer/Title/Company/Product/Copyright）；ExtractAssociatedIcon 均返回 32x32 图标；dotnet build 0 errors（322 既有 nullable 警告非本次引入）。
设计决策：版本语义分层——csproj 日期版本 = 构建事实（程序集/文件属性），发布 tag v<X.Y.Z> = 更新语义（update-agent），并存不冲突；图标单一来源 = ForgeSelf.Api\\Assets\\ForgeSelf.ico（两个 exe 共用）。
代价·收益：构建产物版本号每次自动可追溯（InformationalVersion 含 git hash）；公共层启动器图标补齐（用户可见）；无逻辑/结构代价。
不做事决策：Core/Abstractions 与其余插件不加版本生成（McpCenter 保持人工固定版本 = 插件语义；需要时再统一）。
风险：无阻塞。遗留：win-x64 历史残留 bin（无害，发布不依赖）；XCodeConfigTests 4 失败为既有环境项。
结论：完成 · 可交付 · 未提交 git（等用户明确指示；提交时汇总 028 全批次并隔离 040-B1 文件）。

