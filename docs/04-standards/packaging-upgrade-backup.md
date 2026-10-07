-| 2026-09-29 | 输入38 SQLite 驱动正式依赖化**已实施**：①csproj 加 `XCode.SQLite` 11.24.2026.302（依赖 NewLife.XCode ≥11.25 不升级主版本；System.Data.SQLite 2.0.2 + SourceGear.sqlite3 3.53.4 传递落地）；②**根除运行态探测**——探针（XCode 12.0 + XCode.SQLite 共存、真实 SQL 建表查询）与 dev-bin 冒烟双证：本地加载零下载、不再生成 `Plugins/`；③**单文件兼容**——实测内嵌（默认 ALC 已加载）+ 落盘 LoadFrom 同 identity 冲突（FileLoadException already loaded）→ csproj Target `ExcludeSqliteFromSingleFile`（`AfterTargets=_ComputeFilesToBundle` + `BeforeTargets=GenerateSingleFileBundle`，从 `FilesToBundle` Remove `System.Data.SQLite.dll`）+ `publish-host.ps1` 从 NuGet 缓存外置复制兜底 → exe 16,424→16,036KB、外置 386KB；单文件冒烟：`[System.Data.SQLite.SQLite] 加载 ...\System.Data.SQLite.dll v2.0.2.0` + 8 库全部 `数据库连接成功 (ServerVersion=3.50.4)` + 无 Plugins/ + 7102 监听；④`package-release.ps1` 删 inject 块、仓库 `build/runtime/plugins` 移 `.trash/`；⑤文档同步 agent-workflow（L611/729/736/451）+ 本真源 §1.1；⑥全部改动未提交 git（等用户明确指令）。 |
--
规范定位: 打包·升级·备份·缓存 目录结构与生命周期规则——**唯一真源**
状态: 真源建立（2026-09-28，输入30）；批次1（输入31）与批次2（输入34）已全部实施：插件去 _backups / 更新缓存应用后清理 / Backups 退役 / 图片缓存 TTL / 宿主 QQNT 式 versions 结构（根启动器 + 发布脚本布局 + update-agent 版本化应用 + 008 冻结）；输入36 单文件化**部分实施**：公共层 FDD 单文件启动器 + DOTNET_ROOT 结构运行时已落地并验证（启动器进程模式、退出码透传、组装 zip），业务层 FDD 单文件 publish 已于 2026-09-29 随 040-B1 收口验证；输入37 目录命名统一小写**已实施**（Plugins/Data/Log/Config → plugins/data/log/config，代码+脚本+测试+文档；update-agent 带存量目录规范化；日志外置数据根/log）；**2026-10-02 输入9/输入10：版本号规则改为「三段号 + 时间码」（§4-R10），废止输入43 双轨**
最后更新: 2026-10-02
关联: AGENTS.md §0/§2.3；docs/04-standards/agent-workflow.md B4/B5/B10；docs/02-features/035-plugin-versioned-layout.md、008-tray-service-autoupdate.md（036 链路功能文档）、038-plugin-local-update-source.md；.agents/skills/plugin-development、plugin-publish-verify；scripts/release/*、update-agent.ps1、package-plugin.ps1、publish-plugin.ps1、migrate-plugin-versions.ps1、build.ps1；ForgeSelf.Api（StagedUpdateService/UpdateService/UpdateChecker/PluginVersionService/PluginInstallerService/PluginVersionLayout/AppBuilder/DataLocationService）
---

# 打包·升级·备份·缓存 —— 目录结构与生命周期规范（唯一真源）

## 0. 定位与引用规则（真源声明）

本文是「**打包 / 宿主自更新 / 备份 / 缓存 / 安装目录结构**」相关规则的**唯一真源**：

1. **目录结构、版本保留策略、备份与缓存生命周期规则**以本文为准；
2. AGENTS.md、agent-workflow.md（B4/B5/B10）、035/036/038 功能文档、插件技能、发布脚本中的相关描述**只保留操作流程与踩坑记录，不再承载目录结构事实**，并引用本文；
3. 规则冲突时以本文为准。**现状事实以代码为准（标注代码位置）**；**目标规则以本文为准（标注「目标」）**；未实施的规则不得被当作已实现写入其他文档。

---

## 1. 现状盘点（2026-09-28 代码查证）

### 1.1 打包与发布（产物 = 扁平布局 zip）

| 环节 | 位置 | 现状事实 |
|---|---|---|
| 一键编排 | `scripts/release/release-local.ps1` | build-frontend → publish-host → package-release → make-release-notes →（`-UpdateDir` 拷贝 zip+SHA256SUMS+说明）。本地与 CI 同一命令（B10 契约） |
| 前端构建 | `scripts/release/build-frontend.ps1` | 宿主 web → `ForgeSelf.Api/wwwroot`；各插件 web → `Plugins/<X>/web/dist` |
| 宿主发布 | `scripts/release/publish-host.ps1` | `dotnet publish -c Release -r win-x64 --self-contained` → `artifacts/publish`（**扁平布局**） |
| 打包含清洁 | `scripts/release/package-release.ps1` | 清理 `Data/Log/Config`、`Plugins/_backups`、pdb → zip + `SHA256SUMS.txt`（输入38：SQLite 运行时构件注入块已删除——`XCode.SQLite` 包把驱动做成正式依赖，随发布外置） |
| 发版/发布 | `make-release-notes.ps1` / `publish-release.ps1` | tag 注解→RELEASE-NOTES；`gh release create` |
| CI | `.github/workflows/release.yml` | tag `v*` 触发，只调 `release-local.ps1` + `publish-release.ps1`。**本地复现 CI 必须用同参数（不带 `-SkipFrontend`）**：该开关会跳过前端构建段（`vue-tsc -b`），2026-10-02 输入12 实测「本地带 `-SkipFrontend` 全绿、CI 在 `vue-tsc` 处失败」——本地发布链验证不得跳段 |
| 版本号机制 | ForgeSelf.Api.csproj / ForgeSelf.Bootstrapper.csproj（VersionPrefix 2.3 + VersionSuffix 0.<yyMMddHHmm>）+ scripts/release/release-local.ps1（发行串单点生成）+ new-version.ps1（辅助打印） | **2026-10-02 起新规则（输入9/输入10）：发行串 = 三段号 + 时间码** —— `V = <major>.<minor>.<patch>.<yyMMddHHmm>`（10 位时间码，例 `2.3.0.2609161125`）；git tag / `versions/<ver>/` 目录名 / `versions/current` / zip 名 / 两个 exe 的 FileVersion+ProductVersion / 设置页「当前版本」**全部同一串**。发行串由 `release-local.ps1` 单点生成（显式 `-Version 2.3.0` → 自动补时间码；4 段 → 幂等原样；CI tag / 本地缺省串 `0.0.0-local` 原样不改写）并逐级注入 publish-host / publish-bootstrapper。**废止输入43 的「发行号与文件版本各司其职」双轨**。完整规则、代价与踩坑（CS7035 / NuGet NU1105 / 世代比较）见 §4-R10 |
| Authenticode 签名 | scripts/sign-publish.ps1（经 `release-local.ps1 -Sign` 调用，签名在 zip 打包前） | **签名策略（2026-10-04 输入11 定稿）：本地发布＝必带签名，流水线＝默认不签**。凡**给人装的本地宿主包**（`release-local.ps1` 出到本地更新源、交给用户点「重启并更新」的那一类）**必须显式加 `-Sign`**，签名无效的包不得交付；CI 流水线**默认不传** ⇒ 不签（原因：自签证书生成会把 GitHub runner 卡死 20min+，run 36664225915 两次实测，见变更记录 2026-09-30 行）。签与不签都走同一条 `release-local.ps1 -Sign` 通道：自签证书 CN=OpenForgeSelf 铸己匣 自动生成/复用 + certutil 静默信任 + signtool SHA256 + **RFC3161 时间戳多点回退（2026-10-06 改，此前单点 DigiCert）**：默认候选链 `sectigo → digicert → globalsign → comodoca`，某个文件当前一家失败自动换下一家，全链仍失败才 throw；`-TimestampServer` 只是把某家排到最前，`-TimestampFallbacks` 可自定义顺序。**改此单点的原因（实测）**：2026-10-06 22:33 `release-local -Sign` 整条链跑到最后一段被 `SignTool Error: The specified timestamp server either could not be reached or returned an invalid response` 挡死，`Number of files successfully Signed: 0` ⇒ **zip 根本没产出**；同日 `curl` 实测该主机对四家 TSA 均可达、均能签成，即 digicert 是**间歇返回无效响应**（2026-10-04 亦红过一次），单点依赖＝发布链的可用性瓶颈。仍**禁止**用 `-NoTimestamp` 交付给人装的包（无时间戳＝证书 2029-09-26 过期即签名失效）；商业证书传 -PfxPath/-PfxPassword 可插拔；指纹记录 .forgeself/codesign-thumbprint.txt（CI 可 -Thumbprint 复用）。**递归签全部 exe**（顶层根启动器 + `versions/<ver>/` 业务层每版快照），漏签会破坏多版本回滚的签名一致性 |
| 内置插件落位 | `scripts/release/package-release.ps1` 第 3 步（2026-10-04 输入18 改） | **内置插件必须留在 `versions/<ver>/plugins/`**：publish 产出什么就随版本进什么，不再复制到安装根、也不再从版本目录删除；版本目录里没有 `plugins/` 时脚本**当场 throw**（不许静默出"升级后 0 插件"的空包）。常驻守卫：`ForgeSelf.Api.Tests/RepositoryScriptTests.PackageRelease_MustKeepBundledPluginsInsideVersionDirectory`。运行侧判据见 §1.6 ③。
| 插件打包 | `scripts/package-plugin.ps1`（输入27/038） | 产 `<id>-<ver>.forgeself-plugin`（plugin.json + 入口 DLL + web/dist；排除宿主共享 DLL）→ `artifacts/plugin-packages` |
| 插件侧载 | `scripts/publish-plugin.ps1` / `publish-plugin-full.ps1` | 直落 stage 到 `Plugins/<id>/versions/<ver>/`（2026-09-28 输入31 去 _backups） |
| 存量迁移 | `scripts/migrate-plugin-versions.ps1` | 扁平插件 → `versions/<ver>/` + current（幂等；跳过 `_*` 目录） |
| 全量构建 | `scripts/build.ps1` | legacy 全量构建覆盖 `publish/`（宿主二进制被锁会静默漏更，B5 有坑） |

### 1.2 宿主升级与备份（**两套并存**，都向 `%LOCALAPPDATA%\ForgeSelf` 写数据且**无清理策略**）

| 链路 | 位置 | 备份/缓存行为 |
|---|---|---|
| 分阶段更新（036，现行·唯一链路） | `ForgeSelf.Api/Services/StagedUpdateService.cs` | 下载+校验+解压到 `%LOCALAPPDATA%\ForgeSelf\Updates\<tag>\`（`update.zip` + `extracted/`）→ 拉起 `update-agent.ps1` → 宿主自停。**批次2：QQNT 版本化应用**（update-agent 落 versions/<ver>/ + current 指针 + 重启根启动器） |
| 自更新代理 | `scripts/update-agent.ps1` | **批次2（输入34）已实施 QQNT 版本化应用**：等宿主退出 → 新版本落 `versions/<ver>/`（不动公共层旧版本）→ current 指针切换 → 重启根启动器（公共层跨版本共享）。**无任何整目录备份**；扁平存量迁移清理；版本保留 current+上一版；应用后清理 staged tag + 退役 Backups 存量 |
| Windows 服务更新（008，旧） | `ForgeSelf.Api/Services/UpdateService.cs` | **批次2（输入34）已冻结**：类头冻结声明，不再维护；全流程方法（下载/备份/服务启停/回滚）为历史保留禁止新调用；托盘检查已收敛到 036 `CheckAsync`；DI 注册保留防 WindowsService 模式引用 |
| 检查/下载 | `ForgeSelf.Api/Services/UpdateChecker.cs` + `UpdateSettingsService.cs` | GitHub 资产 API / 本地目录更新源（038 宿主侧同款） |

### 1.3 插件版本与备份（**已统一为 `versions/` 单轨；`_backups` 已移除，输入31 批次1**）

| 位置 | 现状事实 |
|---|---|
| `ForgeSelf.Api/Plugins/Services/PluginVersionService.cs` | **已实施（输入31）**：无 `_backups` 概念；新版本（包源/侧载）直落 `versions/<ver>/`；`CheckForUpdates` 更新源 = versions/ 已直落未生效版本（source=staged）+ 包目录（source=package）；`UpdatePlugin` = 激活已直落版本（切 current + 同步清单 + 热切换，不复制）；`RollbackPlugin` 只认 versions/ 内保留版本（当前+上一版）；`BackupPlugin`/`GetBackupList`/`RestoreFromBackup` 已删除 |
| `ForgeSelf.Api/Plugins/Services/PluginInstallerService.cs` | **已实施（输入31）**：安装/更新/卸载不再调用任何备份；卸载直接删除插件目录与 versions/ 快照 |
| `ForgeSelf.Api/Plugins/PluginVersionLayout.cs` | 插件侧已按 QQNT 模式：`versions/<semver>/` 不可变快照 + `current` 指针（原子切换）+ 扁平兜底 |
| `PluginFrontendFileMiddleware.cs` | web/dist 版本化读取（current 优先，扁平兜底） |
| 035/038 文档 | `_backups` 语义已废弃（输入31 批次1 实施后，新版本直落 `versions/`，无备份） |

### 1.4 缓存

| 位置 | 现状 |
|---|---|
| `%LOCALAPPDATA%\ForgeSelf\Updates\<tag>\` | 更新下载包 + 解压目录；**应用成功后清理已实施**（update-agent.ps1，输入31 批次1，保留 agent-*.log） |
| `%LOCALAPPDATA%\ForgeSelf\Backups\<ts>` | 整目录备份**已退役并清理存量**（update-agent.ps1 不再写入、更新成功后清理，输入31 批次1；008 UpdateService 批次2 输入34 已冻结，不再写 Backups） |
| `{数据根}/Data/ImageRecognitionCache/<会话键>/<sha256>.json` | 图片识别缓存（`AppBuilder.cs:246-248`）；**TTL 清理已实施**（`LocalFileImageRecognitionCache` 初始化清理超 30 天会话目录，输入31 批次1 R6） |
| `publish/wwwroot` 旧 `.br/.gz` | 发布前需删除旧预压缩文件（agent-workflow B3 既有规则） |
| `ForgeSelf.Web DataStoragePanel.vue`「清除缓存」 | 前端按钮存在；**未见后端 API 接线**（2026-09-28 grep 核查，疑为占位，待核） |
| Entity `Meta.Cache` | 进程内缓存（AsyncLocal），非磁盘，不算空间浪费 |

### 1.5 安装目录结构现状（QQNT 式布局，批次2 输入34 实施 + 输入36 单文件化 + 输入37 目录小写统一 · 代码事实）

```
安装根/（公共层，跨版本共享）
├── ForgeSelf.exe                    ← 根启动器（FDD 单文件，输入36：~170KB managed-only bundle，无解压缓存）
├── hostfxr.dll / hostpolicy.dll     ← app-local shim（让 FDD 启动器自身解析公共运行时）
├── host/fxr/<ver>/ + shared/<fw>/<ver>/   ← .NET 运行时 DOTNET_ROOT 结构（NETCore+AspNetCore+WindowsDesktop，公共一份，业务层 FDD 共用；来源=发布机 dotnet 安装目录拷贝，版本取最新 10.0.x）
├── update-agent.ps1                 ← 自更新代理（公共层）
├── versions/                        ← 宿主业务层版本目录（与 plugins 并排）
│   ├── <semver>/ForgeSelf.exe（FDD 单文件：托管程序集+satellite 内嵌）+ wwwroot/** + appsettings.json + SQLite 原生 + **plugins/**（内置插件随版本，输入18）   ← 每版不可变快照
│   └── current                      ← 当前生效版本指针（文本 = <semver>）
├── plugins/<id>/{plugin.json, current, versions/<ver>/**}   ← 插件版本化布局（历史：批次2 曾把内置插件外置到这里；**2026-10-04 输入18 起内置插件改随版本走 `versions/<ver>/plugins/`，用户安装的插件包落数据根 `~/.forgeself/plugins/`，安装根 plugins 不再是扫描路径**）
└── data/ log/ config/                                        ← 运行时生成（zip 时清理；生产态数据根 ~/.forgeself 外置：{数据根}/log 日志、{数据根}/config 配置、插件数据 {数据根}/plugins）
```
启动器 = **进程拉起模式**（输入36）：读 versions/current（或 --forge-version=<v>）→ Start 子进程 `versions/<ver>/ForgeSelf.exe`（WorkingDirectory=versionDir，DOTNET_ROOT=安装根，剔除 --forge-version 透传其余参数）→ WaitForExit 转发退出码；update-agent 的 HostPid 即业务层进程。
路径基准 = **业务层入口程序集目录**（`Path.GetDirectoryName(typeof(AppBuilder).Assembly.Location)`）：扁平=BaseDirectory、QQNT=`versions/<ver>/`（FDD 单文件下 Assembly.Location = exe 目录，实测）；`ContentRootPath` 当业务层目录含 appsettings.json 时指向该目录，否则保 CWD（保护 dotnet run）；tray `SetBasePath` 同步（`AppBuilder.cs`/`Program.cs` 批次2）。插件目录 = **两路**（2026-10-04 输入18 定，取代批次2 的「安装根 `/plugins` 单路」）：内置路 = 业务层旁边的 `versions/<ver>/plugins/`（随版本发布、可随版本回滚），用户路 = 数据根 `~/.forgeself/plugins/`；接线见 `AppBuilder.cs:317-333`（`SetPluginsDirectory` + `AddPluginRoot`），扫描只认子目录里有 `plugin.json` 者。

> ⚠ **单文件关键踩坑（输入36 实测 2026-09-28）**：**自包含单文件**（无论压缩与否）启动时把 bundle 解压到 `DOTNET_BUNDLE_EXTRACT_BASE_DIR` 缓存运行，`AppContext.BaseDirectory` / `Environment.ProcessPath` 均指向提取目录 → 启动器无法定位安装根 + 解压缓存长期占盘（版本变化残留）——**弃用**。**FDD 单文件**（managed-only bundle）原地直跑，BaseDirectory = exe 目录、无解压缓存；运行时由公共层 DOTNET_ROOT 结构提供（根 hostfxr app-local shim 让启动器自身解析，业务层由启动器传 DOTNET_ROOT）。
### 1.6 程序架构分层（输入40 沉淀 · 分程序分层，各司其职）

| 层 | 位置 | 程序 | 职责（只管自己的事） |
|---|---|---|---|
| **① 公共层**（跨版本共享） | 安装根 | `ForgeSelf.exe`（根启动器 = ForgeSelf.Bootstrapper，~170KB 薄壳） | **唯一程序入口**：读 `versions/current`（或 `--forge-version=<v>`）→ Start 子进程业务层 → WaitForExit 透传退出码。**不碰业务** |
| | | `host/` + `shared/`（.NET 公共运行时结构） | 提供 DOTNET_ROOT 运行时，全部版本共用一份 |
| | | `update-agent.ps1`（自更新代理，独立脚本进程） | **只做更新**：下载 zip → 解压 → 写 `versions/<新ver>/` → 原子切 current → 重启根启动器 |
| **② 版本层**（每版不可变快照） | `versions/<ver>/` | `ForgeSelf.exe`（业务层 = ForgeSelf.Api，FDD 单文件） | **全部业务所在**：① Web 宿主（Kestrel：HTTP API + wwwroot 前端）；② 后台服务（PluginManager 插件加载/生命周期、StagedUpdateService 更新检查、常驻任务）；③ 托盘（H.NotifyIcon，System.Windows.Forms——管本进程托盘交互/退出，**不拆独立程序**）；④ 数据（XCode/SQLite 初始化，落 `~/.forgeself`） |
| **③ 插件层**（两路，2026-10-04 输入18 定） | **内置路**：`versions/<ver>/plugins/`（随每个版本一起发布、与该版本快照绑定，可随版本回滚）；**用户路**：数据根 `~/.forgeself/plugins/`（用户自行安装/更新的插件包） | 18 个内置插件（各自 `<Dir>.dll + plugin.json + web/dist`）+ 数据目录里的用户插件包 | **各管各的功能**：独立 ALC 隔离加载，控制器/服务/界面归插件；宿主启动时**两路合并扫描**（内置在前），同 Id 由版本号裁决（畸形版本视为相等⇒保留内置），每条发现日志带来源根；插件自身版本更新仍走 `plugins/<id>/versions/<ver>/ + current`。**禁止**把内置插件外置到安装根 `plugins/` 并从版本目录删除——那会让"业务层旁边扫不到插件"，2026-10-04 现场表现为升级后整台实例 0 插件 |
| **④ 数据层**（运行时生成） | `~/.forgeself`（小写） | — | `data/`（数据库）、`config/`（配置）、`log/`（日志）、`plugins/`（**既是插件数据目录：`plugins/{插件Id}/{连接名}.db`，也是用户插件包的落位**；两者靠"子目录内是否有 `plugin.json`"区分，扫描只认带清单者）——随数据走，发布覆盖不影响 |

**启动链路**：用户双击安装根 `ForgeSelf.exe`（根启动器）→ 读 `versions/current` → Start 子进程 `versions/<ver>/ForgeSelf.exe`（DOTNET_ROOT=安装根）→ 业务层起 Kestrel + 托盘 + PluginManager **两路合并扫描插件根**（内置 `versions/<ver>/plugins/` 在前 + 数据目录 `~/.forgeself/plugins/` 在后，同 Id 按版本号裁决；版本化 current 生效）→ 用户点「检查更新」→ 宿主调用 → update-agent.ps1 下载新 zip → 落 `versions/<新ver>/` + 原子切 current → 重启根启动器 → 新版本生效（旧版本保留可回滚）。

**职责边界铁律**：入口=根启动器（薄壳不碰业务）；业务/后台服务/托盘=业务层进程（托盘管的就是本进程，故不拆独立程序）；更新=update-agent 独立脚本；插件=各自隔离程序集。一层一个程序，各做各的。

---

## 1.7 插件落位与生效裁决（速答表 · 2026-10-07 实测沉淀）

> 立此节的原因：2026-10-07 用户连问「同名插件放两个根哪个生效」「内置根为什么是 PascalCase」「只发插件该放哪」，
> §1.6 只有"同 Id 由版本号裁决"一句结论，**精度不够**（漏了"比的是哪份版本号""同版本谁胜""current 不参与"），
> 命名成因与动线则完全没写 ⇒ 我又去读一遍代码、还脑补了一个不存在的 UI 入口。**下次先查本节。**

| 问题 | 答案（含证据位置） |
| --- | --- |
| 两路根同名插件谁生效？ | **比版本号，高者胜**；**同版本则保留先扫者＝内置根**（内置在前）。`PluginManager.cs:299-353`（`winners` 字典 + `ComparePluginVersions` `:374-379`，`cmp<=0` 跳过）。日志形态：`同名插件按版本覆盖生效: <id> v1.0.2（来源 …\.forgeself\plugins\tool-bridge）取代 v1.0.0（来源 …\versions\<ver>\plugins\ToolBridge）` |
| 比的是哪份版本号？ | **各插件目录「顶层扁平 `plugin.json`」的 `Version`**（`PluginManager.cs:320-330` 只读顶层清单）。⚠️ `versions/<ver>/` 与 `current` **不参与跨根裁决**——只放 `versions/1.0.3` 而不改顶层清单，裁决时它仍是旧版本 |
| 那 `versions/<current>` 管什么？ | 管**胜者目录内部**加载哪份程序集：`versions/<current>/<入口DLL>` 优先、回退扁平（`PluginVersionLayout.cs:76-85`）；前端同理 `versions/<current>/web` 优先（`PluginFrontendFileMiddleware.cs:153-181`）。依赖解析吃**入口 DLL 同目录的 `deps.json`**（`PluginLoadContext.cs:29`）⇒ 版本目录必须自带 `deps.json` |
| 目录名到底该是什么格式？ | **加载不认名字**（只认"子目录顶层有 plugin.json"，身份取清单 `Id`）。但**宿主代码约定＝kebab `metadata.Id`**：安装器建目录 `Path.Combine(_pluginsDirectory, metadata.Id)`（`PluginInstallerService.cs:60`）、版本服务同口径（`PluginVersionService.cs:126`）。**内置根里的 PascalCase 是构建链副产品**：`ForgeSelf.Api.csproj` 按工程名产出 `Plugins\ToolBridge\`，而 §4-R9/输入37 的小写归一**只改了外层 `Plugins→plugins`**（`package-release.ps1:71-78`），插件子目录名没人动 ⇒ 现状：内置根 PascalCase、数据根 kebab 并存，**不是规范，是遗留**（用户 2026-10-07 裁定：沿用现状，不做迁移） |
| 只发一个插件该放哪？ | 放进**该插件实际所在的那个目录**（＝裁决胜者的 `metadata.PluginDirectory`）下的 `versions/<新版本>/`，含 `plugin.json` + 入口 DLL + `deps.json` + `web/dist/*`。**不要动 `current`、不要手改顶层清单**——那是宿主 `ActivateVersion` 的职责（切指针 + `SyncActiveManifest` + 热切换）。内置根场景**别用 `publish-plugin.ps1 -PluginsRoot <内置根>` 直跑**：它按 kebab 新建目录且只写 `versions/`，缺顶层清单 ⇒ 冷启动扫不到、更新判定也看不见（孤岛） |
| 光放文件会生效吗？ | **不会**。文件系统监视器 2026-09-24 已移除（`publish-plugin.ps1` 头部原文），staged 副本只在「显式切换」或「冷启动」后生效。不重启的三条口子见 §1.8 |

## 1.8 用户更新动线（2026-10-07 真访问宿主实测 · 含未修缺陷）

**插件更新（不重启宿主）的真实路径**：地址栏 `http://<host>:<port>/plugins/updates` →「插件更新」页 → 目标插件行点「更新」（或右上「全部更新」）。
后端动作＝`POST /api/plugin/update/{id}` → `ActivateVersion`：停旧 → 回收释放 DLL 句柄 → 切 `current` → `SyncActiveManifest` → 刷新元数据 → 从 `versions/<new>` 加载 → 裁剪旧版（`PluginVersionService.cs:215-260`）。

**⚠️ 当前该动线不可发现（缺陷，未修）**：`/plugins`（插件市场）与 `/plugins/updates` **没有任何导航指向**——
顶部导航只有 `首页 / AI Agent / 技能管理 / 系统监控`（可关闭标签，关掉就没了）+「所有功能」+「设置」；
「所有功能」`/all-features` 是插件卡片墙只有「打开/配置」；「设置 → 插件管理」面板只有**「插件更新源」目录输入框**，无更新动作。
另：插件市场 `onMounted` 不调 `checkForUpdates()`（`PluginStore.vue:245-250`），所以其左侧「可更新」角标恒空（数据源 `:290-294` 脱节）。
⇒ 只能手输 URL。已记 TODO（P1 UX 债：入口可达性 3 处）。**汇报口径铁律**：说"入口在 X"必须先在真实页面走到一次（截图为证），不得由后端能力/路由表反推 UI。

**三条不重启口子的判据**：

| 口子 | 生效条件 | 坑 |
| --- | --- | --- |
| `POST /api/plugin/update/{id}` | `versions/` 最高 staged **>** 内存 `metadata.Version`（`:167-173`） | 若有人手工把顶层清单改成新版本 ⇒ 内存版本被抬高，判"已是最新"，**切换不发生**（2026-10-06 我踩过） |
| `POST /api/plugin/rollback/{id}` `{version}` | 只校验 `versions/<version>/` 存在（`:200-205`），**不比版本号** | 最稳的强制激活；但页面入口是「版本历史」弹窗，而该弹窗受下述缺陷影响 |
| `disable` → `enable` | 启用时重走 `ResolveEntryAssemblyPath`，`versions/<current>/` 优先 | 需 `current` 已指向目标版本 |

**已知宿主缺陷（本批未修，见 TODO）**：① `PluginVersionService.cs:126` 用 `_pluginsDirectory + pluginId` 硬拼路径，
不用 `metadata.PluginDirectory`、不跟随两路根 ⇒ 内置根 PascalCase 目录与数据根插件的**版本历史查不到**
（实测 `GET /api/plugin/tool-bridge/versions` 只回内存那一条，`versions/1.0.3` 在盘上也不显示）⇒ 页面「回滚」按钮出不来；
② `AppBuilder.cs:335-336` + `PluginVersionService.Initialize(pluginsPath)`（`AppBuilder.cs:390`）只喂内置根 ⇒ **数据根那一路无版本化能力**（Inferred，未实测）；
③ `SyncActiveManifest` 只同步清单**不拷 DLL**（`:515-524`）⇒ 顶层扁平件可能长期是旧字节，`current` 丢失时回退到"显示新版跑旧版"。

---

## 2. 空间浪费点（优化目标清单）

1. **宿主升级整目录备份**：~~每次更新把安装目录全量拷贝到 `Backups\<ts>`（含 Plugins、wwwroot、全部 DLL，单次可达数百 MB），无保留/清理策略 → 多次升级累积数 GB~~ **已消除（批次1 输入31 退役 Backups；批次2 输入34 升级=新版本快照+切指针，无备份）**。
2. **更新暂存缓存不清理**：~~每次更新下载 ~74MB zip + 解压 ~200MB 到 `Updates\<tag>\`，应用后留存~~ **已实施清理**（输入31 批次1）。
3. **插件 `_backups` 三重冗余**：~~同一插件内容同时存在于 `versions/`、`_backups/<id>/`、活动扁平目录~~ **已消除**（输入31 批次1：`_backups`/`BackupPlugin` 全删，仅 versions/ 单轨）。
4. **图片识别缓存无清理**：~~`ImageRecognitionCache` 只增不减~~ **已实施 TTL 30 天清理**（输入31 批次1）。
5. **本地构建产物堆积**：`artifacts/publish`、`artifacts/release`、`artifacts/plugin-packages` 每版 ~300MB+，不入库但占磁盘。
6. **双更新链路并存**：~~`UpdateService`(008) 与 `StagedUpdateService`(036) 都写 Backups/Updates，语义重叠~~ **已消除（批次2 输入34）**：008 冻结（不再维护/不再写入 Backups），036 为唯一更新链路（QQNT 版本化应用，无备份）。

---

## 3. 目标目录结构（QQNT 式 · 用户拍板方向 · **已全部实施**：批次1 输入31 = 插件去 _backups + 更新缓存清理 + Backups 退役 + 图片缓存 TTL；批次2 输入34 = 宿主 versions 结构（根启动器 + 发布脚本布局 + update-agent 版本化应用 + 008 冻结）；输入36 = 单文件化（公共层 FDD 单文件启动器 + 业务层 FDD 单文件，运行时 DOTNET_ROOT 结构公共共享）；输入37 = 目录命名统一小写（plugins/data/log/config））

对标 `C:\Program Files\Tencent\QQNT`：**公共/稳定的放外面，每次要更新的放 `versions/`，插件目录与 `versions/` 并排**。

```
安装根/
├── ForgeSelf.exe               # 公共/启动层（FDD 单文件，稳定，不随版本变；类似 QQ.exe，负责拉起当前版本）
├── hostfxr.dll / host/fxr/ / shared/   # .NET 运行时公共一份（DOTNET_ROOT 结构，业务层 FDD 共用；QQNT 的 node 框架同理）
├── update-agent.ps1            # 自更新代理（随包分发）
├── versions/                   # 宿主版本目录：每次更新要动的全落这里
│   └── <semver>/
│       ├── ForgeSelf.exe       # 业务层 FDD 单文件（托管程序集+satellite 内嵌；wwwroot/appsettings/SQLite 原生外置）
│       ├── wwwroot/**          # 静态资源（外置，路径基准 = exe 目录）
│       ├── appsettings.json
│       └── current(或根级 current 指针，原子切换)        # 当前生效版本指针
├── plugins/                    # 与 versions 并排；宿主升级不触碰（输入37 目录小写统一）
│   └── <id>/{plugin.json, current, versions/<ver>/**}   # 插件自身版本化（现状已如此）
└── data/ log/ config/          # 运行时生成（生产态走数据根 ~/.forgeself，已外置；输入37 全小写）
```

设计要点（对应现有机制，逐条可落）：

| # | 要点 | 与现状差异 |
|---|---|---|
| T1 | 宿主每版内容不可变快照入 `versions/<ver>/`，`current` 指针原子切换 | **已实施（批次2 输入34）**：发布脚本产出 versions/<ver>/ + current；update-agent 版本化应用；根启动器按指针拉起 |
| T2 | **升级不再整目录备份**：旧版本目录天然保留（当前 + 上一版），回滚 = 切指针 | **已实施（批次2 输入34）**：update-agent 无备份；版本保留 current+上一版；Backups 已退役清理 |
| T3 | `plugins/` 与 `versions/` 并排，宿主升级只增版本目录，不碰插件目录 | **已实施（批次2 输入34）**：发布脚本把 Plugins 移到公共根与 versions 并排；宿主升级不动插件目录 |
| T4 | **插件目录无 `_backups`、无插件备份**：新版本（包源/侧载）直接 stage 到 `versions/<ver>/`（未切 current 即惰性，side-by-side 安全）；回滚走 `versions/` 内保留版本 | **已实施（输入31 批次1）**：`_backups`/`BackupPlugin` 全删，直落 versions/ |
| T5 | 公共/启动层只放稳定件（exe、代理脚本），路径基准改为「安装根」而非 `AppContext.BaseDirectory` | **已实施（批次2 输入34 + 输入36）**：根启动器 FDD 单文件 + 公共层 DOTNET_ROOT 运行时（app-local shim + host/shared 结构）；业务层路径基准 = 入口程序集目录（AppBuilder/Program）；Data/Log/Config 生产态仍走 ~/.forgeself |
| T6 | 更新暂存（`Updates/<tag>\`）应用成功后即清理；`Backups\` 退役并清理存量 | **已实施（输入31 批次1）**：update-agent.ps1 应用后清理 staged tag + 退役 Backups（008 侧待收口） |
| T7 | **单文件化（输入36）**：公共层 = FDD 单文件启动器（~170KB，托管 bundle 内嵌）；业务层每版 = FDD 单文件（托管程序集+satellite 内嵌）+ 外置 wwwroot/appsettings/SQLite 原生（~10 文件/版）；运行时 DOTNET_ROOT 结构公共一份（跨版本零重复、零解压缓存） | **已实施（输入36 + 2026-09-29 040-B1 收口验证）**：公共层已落地并验证（启动器进程模式、退出码透传、组装 zip 83.9MB）；业务层 publish-host 单文件参数生效（PublishSingleFile=true + native/content 外置） |
| T8 | **目录命名统一小写（输入37）**：安装根/数据根运行目录全小写 —— `Plugins→plugins`（安装根插件目录 + 数据根插件数据）、`Data→data`（开发态数据根）、`Log→log`（日志，外置数据根/log）、`Config→config`（配置目录）；源码工程目录 `Plugins/<X>`（命名空间绑定）与仓库内源码路径保持 PascalCase 不动；Windows NTFS 大小写不敏感 → 存量大写目录无需强制迁移，update-agent 应用时做一次性目录名规范化（MoveFileEx 只改大小写标志） | **已实施（输入37 2026-09-29）**：AppBuilder/DataLocationService/ForgeConfig/Program/ImGateway 代码改小写 + NewLife 日志外置（**`XTrace.LogPath = {数据根}/log`** 直接设置——Setting.LogPath 不联动 XTrace 已实测修正，Program.cs 顶部前置 + AppBuilder 幂等）+ 发布/侧载/迁移脚本 + 测试断言 + 本真源；update-agent 新增步骤 6.5 目录名规范化（dummy 演练含幂等通过） |

**可行性结论**：结构可行。宿主 .NET 应用的本体（DLL+wwwroot）全部随版本变化，公共层只有启动器 exe/运行时/代理脚本，与 QQNT 的「QQ.exe + versions/」同构。**已按该结论实施（批次1 输入31 + 批次2 输入34），经端到端演练验证**：发布管线产出 QQNT 布局 zip（65.3MB，旧扁平 72-78MB）；根启动器拉起 versions/<ver> 业务层冒烟通过；update-agent 版本化应用演练 8 项全过。

---

## 4. 生命周期规则（真源 · 实施后生效）

> 以下为**目标规则**。**已实施项标注**（输入31 批次1 / 输入34 批次2）；未实施项现状行为见 §1。

- **R1 宿主版本保留**：`versions/` 保留**当前 + 上一版**（对齐插件 `PruneVersions` 的 MaxRetainedVersions=2）；更旧版本在版本切换后延迟删除（沿用 `PluginAssemblyUnloader.TryDeleteDirectory` 模式：被占用则跳过下轮重试，绝不阻塞）。**已实施（批次2 输入34）**：update-agent.ps1 应用后保留 current+最高旧版、删除其余。
- **R2 升级不做整目录备份**：升级 = 新增版本快照 + 原子切 `current`；回滚 = 切指针到保留版本。**已实施（输入31 批次1 + 输入34 批次2）**：update-agent.ps1 备份步骤已删、Backups 退役清理、QQNT 版本化应用（versions/<ver>/ + current + 根启动器重启）；008 UpdateService 已冻结（不再写 Backups）。
- **R3 更新缓存清理**：`%LOCALAPPDATA%\ForgeSelf\Updates\<tag>\` 在**应用成功后**删除该 tag 目录（保留日志 `agent-*.log` 供排查）。**已实施（输入31 批次1）**：update-agent.ps1 应用成功后删除 Updates/<tag> 并清理 Backups 存量。
- **R4 插件无备份**：**已实施（输入31 批次1）**：`BackupPlugin`/`GetBackupList`/`RestoreFromBackup` 全删；安装/更新/卸载无备份调用；新版本（包源/侧载）直落 `versions/<ver>/`；CheckForUpdates/GetPluginVersions/EnsureStagedFromPackageSource 全部直读写 `versions/`；publish-plugin.ps1 stage 目标同步。
- **R5 插件版本保留**：沿用 `PruneVersions`（当前 + 上一版，更旧延迟删除）。
- **R6 图片识别缓存**：**已实施（输入31 批次1）**：`LocalFileImageRecognitionCache` 初始化清理 LastWriteTimeUtc 超 **30 天**的会话目录（TTL 常量 `CacheMaxAge`；失败占位结果不写缓存的既有行为保留）。
- **R7 构建产物**：`artifacts/publish|release|plugin-packages` 属可再生物，定期手动清理（不入库，`.gitignore` 已覆盖）。
- **R8 禁止事项**：① 禁止在升级/更新链路中自动化「整目录备份+覆盖」；② 禁止把宿主共享 DLL（`XCode.dll`/`NewLife.*.dll`/`ForgeSelf.*.dll`/`Stardust.dll` 等）拷入插件目录或版本快照（类型分裂，宿主启动即崩，plugin-development 铁律）；③ 禁止 agent 停/启/杀用户运行中的宿主进程（B10/AGENTS §0）；④ 禁止自动化删除数据目录（plugin-development 铁律 10）。
- **R9 目录命名统一小写（输入37 实施）**：安装/运行布局目录名一律全小写 —— `plugins`（安装根插件目录与数据根插件数据，`IDataLocationService.PluginDataRootName="plugins"`）、`data`（开发态数据根）、`log`（日志外置 `{数据根}/log`——**须直接设 `XTrace.LogPath`**：实测 2026-09-29 `NewLife.Setting.Current.LogPath` 不联动 XTrace（独立静态属性），仅设 Setting 日志仍落程序目录 `Log/`；Program.cs 顶部 + AppBuilder 双设幂等）、`config`（配置文件，`ForgeConfig`/`ConfigUnifier` 统一落 `{数据根}/config`）；`versions`/`wwwroot` 本就小写。**存量兼容**：Windows NTFS 大小写不敏感，旧大写目录（Plugins/Data/Log/Config）仍可正常读写，**无需强制迁移**；update-agent 应用时（步骤 6.5）对安装根与 `~/.forgeself` 做一次性目录名规范化（`MoveFileEx` 纯大小写改名，幂等）。**源码工程目录不变**：`ForgeSelf.Api/Plugins/<X>/`、`Plugins/<X>/web/`（命名空间/程序集绑定 PascalCase），发布/侧载脚本里的 `repoRoot/Plugins` 路径保持大写，仅安装形态目标目录用小写。
- **R10 版本号规则（2026-10-02 用户拍板 · 输入9/输入10；废止输入43 双轨）**：发行串 **V = `<major>.<minor>.<patch>.<yyMMddHHmm>`**（10 位时间码 = yyMMdd + HHmm，例 `2.3.0.2609161125`；发行线自 2.3 起）。**单点生成**在 `scripts/release/release-local.ps1`（显式 `-Version 2.3.0` → 自动补时间码；4 段 → 幂等原样；CI tag / 本地缺省串原样不改写），**同一串**注入 `publish-host.ps1` 与 `publish-bootstrapper.ps1`（通道 = **环境变量 `FORGESELF_RELEASE_VERSION`**，只被两个 exe 项目的 csproj 读取；仅纯数字 3/4 段时设置，`0.0.0-local` 之类不设、走 csproj 兜底，发布脚本设值后 `try/finally` 复位）。⚠️ **不得改用命令行 `-p:Version` 注入**（2026-10-02 两次实测失败后定案）：`-p:` 是**全局属性**，会传播到项目图里所有被引用项目（`ForgeSelf.Core` / `ForgeSelf.Abstractions` / `Plugins/*`），10 位时间码会同时触发 ① NuGet 项目版本校验 `NU1105`「不是有效的版本字符串」（错误只写进各自 `obj/project.nuget.cache`/`project.assets.json` 的 logs → **restore 静默失败**，对外表现为「未能还原 <项目>」或 `NU1201 …不支持任何目标框架`）与 ② `Microsoft.NET.GenerateAssemblyInfo.targets` 的 `GetAssemblyVersion` 校验 `NETSDK1018` 无效的 NuGet 版本字符串（该 task 仅在 `AssemblyVersion` 为空时执行，故只报被引用项目、不报显式设了 `AssemblyVersion` 的两个 exe）。落地约束：① csproj 兜底（未注入时）= `VersionPrefix 2.3` + `VersionSuffix 0.<yyMMddHHmm>`、`FileVersion=$(Version)`、`AssemblyVersion=$(VersionPrefix).*`；② PE 文件版本每段上限 65535 → 10 位时间码必触发 `CS7035`，已 `<NoWarn>$(NoWarn);CS7035</NoWarn>` 显式抑制，数值字段被截断为低 16 位（字符串字段完整，更新判定只读字符串）；③ NuGet 身份层必须给合法三段值 → csproj `<PackageVersion>$(VersionPrefix).0</PackageVersion>`（NuGet 按 int 解析版本段，10 位时间码 ≈2.6e9 > int.MaxValue 会 NU1105，且错误只写进 `obj/project.assets.json` 的 logs → **restore 静默失败**，实测踩坑）；④ 升级比较（`UpdateChecker`）：**第 4 段 > 65535 判为新规则世代、恒大于旧编号世代**（否则旧装 `2.2.2026.x` 第 3 段是年份、永远压过新版本 → 已装实例收不到更新），同世代逐段数值比较；`Core` 用 `long[]`、`ParseSemVer` 用 `long.TryParse`、`TryParseVersion` 对时间码串退回按前 3 段解释（`CurrentVersion` 字段受 `System.Version` 每段 int 上限约束，显示侧走 `/api/update/status` 的 `currentVersion` 原样串）；⑤ 辅助入口 `scripts/release/new-version.ps1 -Version 2.3.0` 打印完整串 + `git tag` 命令（只打印，不做任何 git 写操作；提交/打 tag/推送一律由人执行）；⑥ **预览版 tag（2026-10-02 输入12）**：`v<三段号>-preview`（如 `v2.3.0-preview`）→ `Get-FullReleaseVersion` 补时间码得 `<V>-preview`，可选 `-<后缀>` 原样保留在 **tag / `versions/<V>/` 目录名 / zip 名 / `RELEASE-NOTES-<V>-<后缀>.md` / Release 页**；注入给 exe 的串由 `Get-ReleaseVersionInjectible` **剥掉 `-<后缀>`**（PE 文件版本只接受纯数字段）→ exe 的 FileVersion/ProductVersion = `<V>` 无后缀，`versions/current`、zip 名、tag 带后缀。预览语义由 GitHub `prerelease` 承载：`publish-release.ps1` 见到版本串含 `-` 自动加 `--prerelease`，更新端 `UpdateChecker` 仅 `channel != stable` 才收 prerelease（`UpdateChecker.cs:336/L351`）。 ⑦ **升级顺序硬约束（2026-10-04 实测现形，`:51888`）**：**④ 的世代兜底只存在于 2026-10-02 之后的构建里**——早于它的实例（PE 戳 `2.7.1.0`，跑在 `versions/2.2.11/versions/2.2.2026.0930/ForgeSelf.exe`）用 `System.Version` 逐段比较，**既认不出 `2.3.0.<10位时间码>`（第 4 段 ≈2.6e9 超 `System.Version` 段上限，`ParseSemVer` 退回前 3 段后又被 `2.7.1.0` 压过），也拿不到"有更新"**：实测该实例在更新源目录已有 `OpenForgeSelf-2.3.0.2610041706-win-x64.zip` 时仍回 `latestVersion=2.2.9 / hasUpdate=false`。⇒ 规则：**这类"老宿主"必须先经一次人工可完成的跳号升级（发一个号更大、且四段每段 ≤ 65535 的完整串，如 `2.7.2.0`）才能重新进入自动更新链**；跳号属于一次性例外，必须在变更记录里指名原因，禁止当成常态（常态仍是 §4-R10 的「三段号 + 10 位时间码」）。**两个连带后果（也是这条规则的存在理由）**：① 跳号之后**发行线基准就变成了 `2.7.2.0`**——下一版必须大于它（继续 `2.7.3.0` 或跳 `3.0.0`），回头发 `2.3.x` 老宿主与新宿主都收不到；② **同一个更新源目录里不得混放"两种世代"的包**——按本节 ④ 的世代兜底，带 10 位时间码的包恒大于旧编号形态，于是装完 `2.7.2.0` 的宿主再检查更新，会把目录里残留的 `2.3.0.<时间码>` 判成"有更新"而**降级安装**。本批实测踩过：`updates/` 一度同时有这两类；处置＝把不再投用的包改名移出扫描模式（`*.zip` → `*.zip.bak`），**改名不删除**。另记一条同源事实：**设置页/`/api/update/status` 的 `currentVersion` 是运行进程 exe 的 PE 字符串戳**（`UpdateChecker.GetCurrentVersion()` 读 `Environment.ProcessPath`），`versions/current` 指针改不动它——所以「目录名 ≠ exe 戳」只会以显示与比较双重形态暴露，落盘时五处同串必须一起验。
- **R11 安装根解析唯一口径（禁止版本目录逐代嵌套，2026-10-04 输入19）**：安装根 = **公共层所在层**（根启动器 `ForgeSelf.exe` + `host/`+`shared/` + `update-agent.ps1` + `versions/`）。业务层 exe 在 `versions/<ver>/` 内，**它的目录不是安装根**。规则：从起点目录起，**父目录名为 `versions` 就一路上跳两级**，直到不再满足；扁平布局（dev / 测试输出目录）原样返回。两处必须同源：宿主侧 `ForgeSelf.Api/Services/HostInstallRoot.cs`（`StagedUpdateService.ApplyStaged` 用它取 `-InstallDir`）与代理侧 `scripts/update-agent.ps1` 的 `Resolve-ForgeInstallRoot`（步骤 0 归一化 + 归一化后仍在 `versions/<ver>/` 内则**当场 throw 不动盘**）。**为什么代理侧也要做**：嵌套是在「老宿主 + 新代理」那一次升级里触发的——老宿主的 C# 改不动，只有随包分发的代理脚本能拦住它自己。踩坑实证（`:51888`，2026-10-04 只读复核）：`versions\2.2.11\versions\2.2.2026.0930\versions\2.7.2.0\ForgeSelf.exe` 三层，成因就是旧 `StagedUpdateService` 把 `Path.GetDirectoryName(Environment.ProcessPath)`（业务层目录）当安装根传给代理，于是代理把新版本落进 `versions/<ver>/versions/<new>/`、并把公共层合并进版本目录。连带后果（同一次修复一并消除）：代理步骤 6 的扁平残留清理会在**版本目录**里删 `wwwroot/appsettings.json/ForgeSelf.dll`，步骤 9 的重启目标变成版本目录里的业务层 exe（无 DOTNET_ROOT，运行时解析不到）。**自愈性**：修复后下一次升级直接落 `<真安装根>/versions/<新ver>/` 并把 `current` 指过去、重启的是根启动器 ⇒ 嵌套实例升级一次即回到正确布局，旧的多余层成为无人引用的残留（清理属不可逆面，见 TODO「混代实例收编」）。常驻判据：`HostInstallRootTests`（扁平/一层/两层/三层嵌套/大写 `Versions`/尾分隔符/非 versions 反证 + 由 exe 路径解析 + 拿不到目录即抛）＋**实跑代理脚本同名函数逐条比对归一化结果与层数**（两侧规则漂移必红）＋`更新链路必须用安装根解析而非业务层目录`（防 C# 侧退回旧写法）。反向探针已做：把代理函数改成不上跳、把宿主侧退回 `ProcessPath` 目录 → 两条守卫同时实红（失败 2 / 通过 11 / 总计 13，`logs/test-hostinstallroot-probe.log`）；还原后 13/13 绿。

---

## 5. 实施改动清单（批次1 = 输入31 已完成；批次2 = 输入34 已完成）

| 改动对象 | 内容 | 风险 |
|---|---|---|
| 宿主路径基准 | `AppBuilder.cs`、`Program.cs`（批次2 已实施）：业务层路径基准 = 入口程序集目录（扁平=BaseDirectory、QQNT=versions/<ver>/）；ContentRoot 含 appsettings.json 判定；tray SetBasePath 同步 | 高（启动链核心） | **✅ 已实施（批次2 输入34）** |
| 启动器 | 根 `ForgeSelf.exe`（公共层）拉起 `versions/<current>/` 内宿主 | 高 | **✅ 已实施（批次2 输入34）**：`ForgeSelf.Bootstrapper`（AssemblyName 避开宿主 ForgeSelf.dll），versions/current 或 --forge-version 强制；ALC 从版本目录解析业务依赖；EntryPoint 调用 Main |
| 自更新 | `update-agent.ps1` + `StagedUpdateService.cs`：备份→版本目录+指针切换；`Updates/` 应用后清理；`Backups/` 退役 | 高 | **✅ 已实施（批次2 输入34）**：update-agent QQNT 版本化应用（versions/<ver>/ + current + 根启动器重启 + 扁平迁移 + 版本保留 + staged/Backups 清理）演练 8 项全过 |
| 发布脚本 | `publish-host.ps1`（业务层 FDD 单文件：PublishSingleFile + native/content 外置）、`publish-bootstrapper.ps1`（公共层：FDD 单文件启动器 + DOTNET_ROOT 结构运行时 = 本机 dotnet host/fxr + shared 三框架拷贝 + app-local shim）、`package-release.ps1`（QQNT 组装：公共根 + versions/<ver>/ + current + Plugins 并排 + 扁平残留清理；业务层单文件 exe 保留不再删除）、`release-local.ps1`（唯一编排） | 中 | **✅ 已实施（批次2 输入34 + 输入36 部分）**：v2.2.10 批次2 产出 zip 65.3MB（旧扁平 72-78MB）；输入36 公共层 FDD 单文件 + 运行时结构 + 组装 zip 83.9MB（dummy 模拟业务层）已验证；业务层单文件 publish 待 040-B1 收口 |
| 插件备份 | `PluginVersionService.cs` / `PluginInstallerService.cs` / 038 包源流：去 `_backups`，直接 stage 到 versions/ | 中 | **✅ 已实施（输入31 批次1）** |
| 侧载/迁移脚本 | `publish-plugin.ps1` 同步去 `_backups`（`package-plugin.ps1`/`migrate-plugin-versions.ps1` 本就幂等兼容） | 中 | **✅ 已实施（输入31 批次1）** |
| 缓存策略 | `ImageRecognitionCache` TTL 30 天 | 低 | **✅ 已实施（输入31 批次1）**；`DataStoragePanel.vue`「清除缓存」接线核查 → TODO（P2） |
| 旧链路收口 | `UpdateService`(008 Windows 服务) 与 036 并存：建议冻结 008（服务模式不再维护），或统一到 036 | 中（建议决策） | **✅ 已冻结（批次2 输入34）**：类头冻结声明；托盘检查收敛 036 CheckAsync；DI 注册保留防 WindowsService 引用 |
| 文档/技能 | AGENTS.md、agent-workflow B5、035/038、Plugins/README、plugin 技能按本真源同步 | 低 | **✅ 批次1 已同步**（plugin-development/plugin-publish-verify 速查此前已加引用） |
| 目录命名统一小写（输入37） | `AppBuilder.cs`（plugins、config、update-settings 路径、日志外置 `{数据根}/log`）、`DataLocationService.cs`（data）、`ForgeConfig.cs`/`Program.cs`（config）、`IDataLocationService.PluginDataRootName=plugins`、`ImGateway` 两处（plugins）、发布/侧载/迁移脚本（package-release/publish-host/publish-plugin/migrate-plugin-versions/update-agent 注释与路径）、`build/runtime/Plugins` 仓库目录改名 plugins、测试断言（DataLocationServiceTests/ForgeConfigTests 等 8 文件） | 中（路径基准语义不变，Windows 大小写兼容存量） | **✅ 已实施（输入37 2026-09-29）**：`dotnet build` 0 errors；目录相关单测 6/6 绿（DataLocation 5 + ForgeConfig 1；XCodeConfigTests 4 个失败为既有系统 Temp 拦截环境问题，与本任务无关记 TODO）；update-agent 目录名规范化 dummy 演练含幂等通过；脚本 AST 语法 7/7 通过 |

---

## 6. 引用关系

| 文档 | 引用方式 |
|---|---|
| `AGENTS.md` | §2.3 打包流程收敛为操作要点 + 引用本文（真源说明） |
| `docs/04-standards/agent-workflow.md` | B5（插件体系与发布）、B10（CI 自动发布）顶部加引用；目录结构/备份事实不再重复定义 |
| `docs/02-features/035-plugin-versioned-layout.md` / `038-plugin-local-update-source.md` / `008-tray-service-autoupdate.md` | 关联段加引用（`_backups` 目标语义以本文 §3-T4/§4-R4 为准；036 升级链路以本文 §1.2/§3/§4 为准） |
| `.agents/skills/plugin-development` / `plugin-publish-verify` | 关键事实速查加引用（活动目录/`_backups` 描述指向本文） |
| 发布/升级脚本 | 头注释引用本文 |

---

## 变更记录

| 日期 | 变更 |
|------|------|
| 2026-09-28 | 建立本文（输入30）：盘点打包/升级/备份/缓存全部落点；确立 QQNT 式目标目录结构；确立去 `_backups`/去插件备份/更新缓存清理规则；登记实施改动清单。 |
| 2026-09-28 | 批次1 实施完成（输入31 用户拍板改代码）：插件去 `_backups`/`BackupPlugin`（PluginVersionService/PluginInstallerService/publish-plugin.ps1 直落 versions/）；update-agent.ps1 去整目录备份 + 应用后清理 Updates/<tag> + 退役 Backups 存量；`LocalFileImageRecognitionCache` TTL 30 天清理；测试 56/56 绿；宿主 QQNT versions 结构（批次2）待立项。 |
| 2026-09-28 | 批次2 实施完成（输入34 用户拍板立项）：①宿主 QQNT 目录结构——`ForgeSelf.Bootstrapper` 根启动器（AssemblyName 避开宿主 ForgeSelf.dll、versions/current 指针、ALC 版本目录解析、EntryPoint 调 Main）；AppBuilder/Program 路径基准改入口程序集目录；发布脚本 QQNT 组装（publish-host FDD + publish-bootstrapper 公共层自包含 + package-release versions/<ver>/+current+Plugins 并排），zip 65.3MB（旧扁平 72-78MB）；②冻结 008（UpdateService 类头冻结声明、托盘检查收敛 036 CheckAsync）；③插件装包更新统一版本化（UpdateFromPackage 版本必须更高、StageUploadedPackage 直落 versions/）；update-agent.ps1 QQNT 版本化应用（落 versions/<ver>/ + current + 重启根启动器 + 扁平迁移 + 版本保留 current+上一版），演练 8 项全过；过滤集 23/23 绿。 |
| 2026-09-28 | 输入36 单文件化**部分实施**：①清理 artifacts 构建产物 918MB（用户批准）；②Bootstrapper 改写为**进程拉起模式**（单文件业务层是 apphost 无法 Assembly.Load → Start 子进程 versions/<ver>/ForgeSelf.exe + DOTNET_ROOT=安装根 + WaitForExit 透传退出码）；③发布脚本单文件化：**自包含单文件弃用**（解压缓存 + BaseDirectory/ProcessPath 指向提取目录，实测），**FDD 单文件落地**（公共层 ForgeSelf.exe ~170KB managed-only bundle + DOTNET_ROOT 结构运行时 = 本机 dotnet host/fxr + shared 三框架 + 根 app-local shim；业务层 publish-host 加 PublishSingleFile=true + native/content 外置，wwwroot/appsettings/SQLite 原生保留外置）；④package-release 适配（业务层检查改 exe、不再删除业务层单文件 exe）；验证：公共层 627 文件（含 shared 框架）+ 启动器 0.16MB + 链路演练（启动器→versions/current→子进程→退出码透传 7）+ 组装 zip 83.9MB 全过；**业务层真实 publish 待 040-B1 编译收口**（AgentHub IAgentRegistry 二义，并行会话在飞区）。 |
| 2026-09-29 | 输入37 目录命名统一小写**已实施**：①代码——AppBuilder（`plugins`/`config`/update-settings 路径 + NewLife 日志外置 `NewLife.Setting.Current.LogPath={数据根}/log`，探针实测 API）、DataLocationService（`data`）、ForgeConfig/Program（`config`）、IDataLocationService（`PluginDataRootName="plugins"`）、ImGateway×2（plugins）；②脚本——package-release/publish-host/publish-plugin/migrate-plugin-versions 安装形态路径改 plugins、sanitize 改 data/log/config、update-agent 新增步骤 6.5 目录名规范化（MoveFileEx 纯大小写改名，幂等，dummy 演练通过）；③仓库 `build/runtime/Plugins` 目录改名 plugins；④测试断言 8 文件同步；⑤本真源补 R9/§1.5/§3-T8/变更记录；⑥验证——`dotnet build` 0 errors（040-B1 已收口）、目录相关单测 6/6 绿、脚本 AST 7/7 过。遗留：XCodeConfigTests 4 个失败 = 既有系统 Temp 写入拦截环境问题（记 TODO，与本任务无关）；批次2+输入36+输入37 全部改动未提交 git（等用户明确指令）。 |
| 2026-09-29 | 输入43 文件版本统一 `2.2.<yyyy.MMdd>`**已沉淀**：真源 §1.1 加版本号机制行；`publish-host.ps1` 移除 `-p:Version` 覆盖（业务层 FileVersion 此前被覆盖成发行号 2.2.12）；修复 release-local.ps1 尾部汇总对目录项取 Length 的展示 bug（-File 过滤，发布成功但 exit 1 假失败）。核验：两 exe FileVersion 均 2.2.2026.0929、签名 Valid。 |
| 2026-09-29 | 输入42 发布签名能力**已沉淀（后经输入2 2026-09-30 改默认关闭，见下）**：真源 §1.1 加签名行（sign-publish.ps1 递归签全部 exe、证书可插拔、指纹复用）；AGENTS §2.3 发布规范补「发布必带 -Sign」；plugin-publish-verify 技能一键跑加 -Sign + 铁律。修复 sign-publish.ps1 递归漏签业务层 bug；CI release.yml 加 -Sign。 |
| 2026-09-30 | 输入2 发布签名默认关闭**已沉淀**：CI 卡在自签证书生成（run 36664225915 两次 20min+ 无进展）→ 用户指令「脚本默认不加签名/不自签，传参指定时才签；流水线默认不签名」→ ① CI release.yml 去 `-Sign`（默认不签）；② 真源 §1.1 签名行改为「默认不签名，`release-local.ps1 -Sign` 显式指定才签」；③ AGENTS §2.3 发布规范改「发布默认不签名，需要时传 -Sign」；④ plugin-publish-verify 技能参数说明改「-Sign 可选，默认不签」。签名能力（sign-publish.ps1 / 自签 / 商业证书 -PfxPath）全部保留，仅默认行为变更。 |
| 2026-09-29 | 输入40 程序架构分层**已沉淀**：真源新增 §1.6（公共层/版本层/插件层/数据层职责表 + 启动与更新链路 + 职责边界铁律）；AGENTS.md §2.3 真源引用句补「程序架构分层（§1.6）」。 |
| 2026-09-29 | 输入39 版本号生成 + 文件图标 + 包信息**已实施**：①版本号生成（参照 CrazyCoder.csproj）——ForgeSelf.Api.csproj / ForgeSelf.Bootstrapper.csproj 加 `VersionPrefix 2.2` + `VersionSuffix $([System.DateTime]::Now.ToString('yyyy.MMdd'))` + `Version/FileVersion=$(Version)` + `AssemblyVersion=$(VersionPrefix).*` + `Deterministic=false`（构建日期自动版本；与发布 `-Version` tag 语义并存：程序集/文件版本=构建事实，更新语义仍以 tag 为准）；②文件图标——Bootstrapper（公共层根 ForgeSelf.exe，用户双击对象）补 `<ApplicationIcon>..\ForgeSelf.Api\Assets\ForgeSelf.ico</ApplicationIcon>`（与 Api 复用同一份），ExtractAssociatedIcon 实测两 exe 均带 32x32 图标；③包信息——Api（AssemblyTitle=铸己匣 / Description / Company=OpenForgeSelf / Product=ForgeSelf（铸己匣）/ Copyright=©2026 OpenForgeSelf）+ Bootstrapper（Title=铸己匣启动器，其余同源）；④验证——`dotnet build` 0 errors（322 既有 nullable 警告非本次引入）；⑤范围——仅两个 exe 项目，McpCenter 保持人工固定版本（插件语义），Core/Abstractions 未动（需要时再统一）；⑥未提交 git（等用户明确指令）。 |
| 2026-09-29 | 输入37 发布复验与日志外置修正：①**发布全链路成功**——`release-local.ps1 -Version v2.2.11`（TEMP 重定向 `.forgeself/test-tmp` 规避系统 Temp 拦截）真实业务层 FDD 单文件 + 全部插件 + wwwroot，zip 101.8MB + QQNT 小写布局（顶层 host/shared/plugins/versions/ForgeSelf.exe/hostfxr.dll/update-agent.ps1，**无大写残留**；zip 内 versions/2.2.11 干净无 Plugins）；②**日志外置修正**——实测 `Setting.LogPath` 不联动 XTrace（日志仍落程序目录 Log/），已改 Program.cs 顶部 + AppBuilder 直接设 `XTrace.LogPath`，开发态冒烟验证：日志落 `data/log/2026_09_29.log`、程序目录不再生成 Log/；③**运行残留认知**——业务层运行后 XCode 库探测在版本目录生成 `Plugins/`（SQLite 3 件 ~15MB：e_sqlite3.dll/System.Data.SQLite.dll/zip，宿主代码不可控），安装包不含（组装时清理），属每版本运行期增量 → 记 TODO P2（update-agent 升级后清理非当前版本运行残留候选）；④阻塞：040-B1 并行会话改动 ForgeSelf.Abstractions/Core 接口 → `AIAgent/Services/ReactLoopAgent.cs` 编译断（CS0019，非本任务文件），当前工作区全量 `dotnet build` 不可过（Api 本体 `-p:BuildProjectReferences=false` 编译 0 errors 已验证本任务代码）；⑤全部改动仍未提交 git（等用户明确指令）。 |
| 2026-10-02 | 输入9/输入10 版本号规则改为「三段号 + 时间码」**已实施**：发行串 `V = <major>.<minor>.<patch>.<yyMMddHHmm>`（10 位时间码；发行线 2.2 → 2.3），tag / `versions/<ver>/` / zip 名 / 两 exe 文件版本 / 设置页显示全部同串。① 两个 csproj（Api + Bootstrapper）：`VersionPrefix 2.3` + `VersionSuffix 0.<yyMMddHHmm>` + `FileVersion=$(Version)` + `<NoWarn>CS7035</NoWarn>` + **`<PackageVersion>$(VersionPrefix).0</PackageVersion>`**（修 NuGet 按 int 解析 10 位时间码 → NU1105 且错误只进 obj logs 的 restore 静默失败）；② 发布脚本：release-lib 新增 `Get-FullReleaseVersion`/`Test-ReleaseVersionInjectible`，publish-host 恢复版本注入（输入43 的移除决策有意回退；**通道最终定为环境变量 `FORGESELF_RELEASE_VERSION`**——命令行 `-p:Version` 会外溢到被引用项目并触发 NU1105 / NETSDK1018，两次实测失败后定案）、publish-bootstrapper 新增 `-Version` 形参、release-local 显式 `-Version` 才补时间码并贯穿全链路、新增 `new-version.ps1`（只打印）；③ `UpdateChecker` 版本比较改「世代优先」：`SemVer(long[] Core)` + `IsDateCoded = Core[3] > 65535` + `ToVersion()` 夹取 + `ParseSemVer` long 解析 + `TryParseVersion` 时间码回退，新增 7 条单测；④ 文档同步：本真源 §1.1/§4-R10/变更记录 + AGENTS.md §2.3；⑤ 显示侧零改动（`UpdatePanel.vue` 读 `/api/update/status` 的 `currentVersion` = exe FileVersion 原样串）。 |
| 2026-10-02 | 输入12 预览版 tag 支撑 **已实施并实跑**：`Get-FullReleaseVersion` 支持可选 `-<后缀>`；新增 `Get-ReleaseVersionInjectible`（纯数字串原样注入；`<数字串>-preview` 只注入数字前半段，后缀留在 tag/zip/Release 页）；两条 publish 脚本改用该函数。实测 `release-local.ps1 -Version 2.3.0.2610022017-preview`：zip `OpenForgeSelf-2.3.0.2610022017-preview-win-x64.zip` + `RELEASE-NOTES-2.3.0.2610022017-preview.md` + `versions/current=2.3.0.2610022017-preview`，exe FileVersion=`2.3.0.2610022017`（无后缀），WRAPPER-EXIT=0（见 §4-R10 ⑥）。同日提交 `c80149b` 并推送 `main` + tag `v2.3.0.2610022023-preview`；CI 在 `pnpm build`（vue-tsc）处失败 —— 报错文件 `e2e/plugins/design-system/design-system-agent.spec.ts:177`（TS2322）由 **M1 提交 `5fa914c` 引入且已在上一次远端 main 上**，与本次改动无关（本次零前端改动）。 |
| 2026-10-04（输入18） | **内置插件落位改回"随版本走" + 插件根两路合并**：现场事实＝:51888 升级到 2.7.2.0 后 `GET /api/plugin` 返回 0 个，宿主日志 `插件目录不存在: …/versions/2.7.2.0/plugins```。查证：**不是** update-agent 剪切（`robocopy /E` 只复制），而是 `package-release.ps1` 第 3 步把 publish 产出的 `plugins/` 复制到安装根后再 `Remove-Item versions/<ver>/plugins`（注释自陈「插件公共外置」），与运行期解析（业务层旁边）分叉——**正常单层 QQNT 布局也已分叉**，混代布局只是把它变成可见故障。裁定（用户）：内置插件的家是**每个版本目录里的** `plugins/`，不是安装根；另需支持数据目录插件。已实施：① 打包保留随版本 + 缺目录 throw；② `PluginManager` 两路根（内置 + `~/.forgeself/plugins`）合并扫描、同 Id 版本号裁决、日志带来源；③ 布局与两路判据各有常驻测试（反向探针已验会响）。§1.6 ③/④ 与 §1.1 同步更正；后续待办见 `TODO.md`（安装/更新写入位置应落数据根、真实布局 e2e、混代收编）。 |
| 2026-10-04 | 输入10（M3 收尾）实测确认 §4-R10 ⑦：**老宿主无世代兜底 → 新规则发行串对它不可见**。为让 `:51888` 重新进入更新链，按用户选定 B 出一版**跳号包** `2.7.2.0`（四段完整、幂等原样使用、不补时间码；`release-local.ps1 -Version 2.7.2.0 -UpdateDir <更新源>`），使 `2.7.2.0 > 2.7.1.0` 在其 `System.Version` 比较下成立。同批产物：宿主整包 `2.3.0.2610041706`（含 design-system 3.1.0）与插件单包 `design-system-3.1.0.forgeself-plugin` 均在同一更新源目录。 |
| 2026-10-04（输入19） | **新增 §4-R11 安装根解析唯一口径（禁止版本目录逐代嵌套）·已实施**：现场事实＝`:51888` 的业务层跑在 `versions\2.2.11\versions\2.2.2026.0930\versions\2.7.2.0\ForgeSelf.exe`（三层）。根因（读码定位，非推测）＝`StagedUpdateService.ApplyStaged` 把 `Path.GetDirectoryName(Environment.ProcessPath)`（业务层版本目录）当 `-InstallDir` 交给代理 ⇒ 代理把新版本落进 `versions/<ver>/versions/<new>/` 并把公共层合并进版本目录；连带：步骤 6 扁平清理误删版本目录里的 `wwwroot/appsettings.json`，步骤 9 重启的是版本目录业务层 exe（无 DOTNET_ROOT）。修法＝宿主侧新增 `ForgeSelf.Api/Services/HostInstallRoot.cs`（父目录名为 `versions` 一路上跳两级；扁平原样返回）+ 代理侧 `Resolve-ForgeInstallRoot` 同步归一化并在仍处 `versions/<ver>/` 内时**当场 throw 不动盘**——代理侧必须做，因为触发嵌套的那一次是「老宿主＋随包新代理」，老 C# 改不动。自愈：修复后下一次升级落真安装根的 `versions/<新ver>/` 并切 current、重启根启动器，嵌套实例升一次即回正（多余层为无人引用残留，清理属不可逆面另批）。判据＝`HostInstallRootTests` 13 条（含**实跑 pwsh 比对两侧归一化结果**）；反向探针：代理不上跳 + 宿主退回 ProcessPath → 两条守卫同时实红（失败 2/通过 11/总计 13），还原 13/13 绿。附带更正本文 §1.5 两处过期事实（安装根 `plugins/` 单路 → 输入18 两路；版本目录含 `plugins/`）。 |
