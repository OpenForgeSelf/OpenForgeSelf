---
规范定位: 打包·升级·备份·缓存 目录结构与生命周期规则——**唯一真源**
状态: 真源建立（2026-09-28，输入30）；插件去 _backups / 更新缓存应用后清理 / Backups 退役 / 图片缓存 TTL 已实施（输入31，批次1）；宿主 QQNT 式 versions 结构**未实施**（批次2，待立项）
最后更新: 2026-09-28
关联: AGENTS.md §0/§2.3；docs/04-standards/agent-workflow.md B4/B5/B10；docs/02-features/035-plugin-versioned-layout.md、036-github-release-auto-update.md、038-plugin-local-update-source.md；.agents/skills/plugin-development、plugin-publish-verify；scripts/release/*、update-agent.ps1、package-plugin.ps1、publish-plugin.ps1、migrate-plugin-versions.ps1、build.ps1；ForgeSelf.Api（StagedUpdateService/UpdateService/UpdateChecker/PluginVersionService/PluginInstallerService/PluginVersionLayout/AppBuilder/DataLocationService）
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
| 打包含清洁 | `scripts/release/package-release.ps1` | 注入 `build/runtime/Plugins` SQLite 运行时构件 → 清理 `Data/Log/Config`、`Plugins/_backups`、pdb → zip + `SHA256SUMS.txt` |
| 发版/发布 | `make-release-notes.ps1` / `publish-release.ps1` | tag 注解→RELEASE-NOTES；`gh release create` |
| CI | `.github/workflows/release.yml` | tag `v*` 触发，只调 `release-local.ps1` + `publish-release.ps1` |
| 插件打包 | `scripts/package-plugin.ps1`（输入27/038） | 产 `<id>-<ver>.forgeself-plugin`（plugin.json + 入口 DLL + web/dist；排除宿主共享 DLL）→ `artifacts/plugin-packages` |
| 插件侧载 | `scripts/publish-plugin.ps1` / `publish-plugin-full.ps1` | 直落 stage 到 `Plugins/<id>/versions/<ver>/`（2026-09-28 输入31 去 _backups） |
| 存量迁移 | `scripts/migrate-plugin-versions.ps1` | 扁平插件 → `versions/<ver>/` + current（幂等；跳过 `_*` 目录） |
| 全量构建 | `scripts/build.ps1` | legacy 全量构建覆盖 `publish/`（宿主二进制被锁会静默漏更，B5 有坑） |

### 1.2 宿主升级与备份（**两套并存**，都向 `%LOCALAPPDATA%\ForgeSelf` 写数据且**无清理策略**）

| 链路 | 位置 | 备份/缓存行为 |
|---|---|---|
| 分阶段更新（036，现行） | `ForgeSelf.Api/Services/StagedUpdateService.cs` | 下载+校验+解压到 `%LOCALAPPDATA%\ForgeSelf\Updates\<tag>\`（`update.zip` + `extracted/`）→ 拉起 `update-agent.ps1` → 宿主自停。**staged 目录应用成功后不清理** |
| 自更新代理 | `scripts/update-agent.ps1` | 等宿主退出 → **整目录备份**（robocopy 到 `%LOCALAPPDATA%\ForgeSelf\Backups\<ts>`，仅排除 Data/Log/Config/_backups）→ 覆盖 staged 文件 → 重启。**备份永不清除**；`Updates\agent-<ts>.log` 累积 |
| Windows 服务更新（008，旧） | `ForgeSelf.Api/Services/UpdateService.cs` | 同样整目录备份到 `%LOCALAPPDATA%\ForgeSelf\Backups\<ts>` + 覆盖 + 失败回滚；`GetBackupRoot()`/`GetBackups()` 暴露备份列表 |
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
| `%LOCALAPPDATA%\ForgeSelf\Backups\<ts>` | 整目录备份**已退役**（update-agent.ps1 不再写入，更新成功后清理存量，输入31 批次1；008 UpdateService 仍写，待收口决策） |
| `{数据根}/Data/ImageRecognitionCache/<会话键>/<sha256>.json` | 图片识别缓存（`AppBuilder.cs:246-248`）；**TTL 清理已实施**（`LocalFileImageRecognitionCache` 初始化清理超 30 天会话目录，输入31 批次1 R6） |
| `publish/wwwroot` 旧 `.br/.gz` | 发布前需删除旧预压缩文件（agent-workflow B3 既有规则） |
| `ForgeSelf.Web DataStoragePanel.vue`「清除缓存」 | 前端按钮存在；**未见后端 API 接线**（2026-09-28 grep 核查，疑为占位，待核） |
| Entity `Meta.Cache` | 进程内缓存（AsyncLocal），非磁盘，不算空间浪费 |

### 1.5 安装目录结构现状（zip 扁平布局，代码事实）

```
安装根/（publish 目录 / 解压 zip 后）
├── ForgeSelf.exe + 全部宿主 DLL + update-agent.ps1 + wwwroot/   ← 扁平，每版全量覆盖
├── Plugins/<id>/
│   ├── plugin.json / current / versions/<ver>/**                 ← 插件版本化布局（已有）
│   └── （扁平兜底文件，兼容回退）

├── Data/ Log/ Config/                                            ← 运行时生成（zip 时清理）
```
路径基准 = `AppContext.BaseDirectory`（exe 目录）：webRoot 候选（`AppBuilder.cs:45/540-564`）、`pluginsPath = BaseDirectory/Plugins`（`AppBuilder.cs:256`）、开发态数据根 `BaseDirectory/Data`（`DataLocationService.cs`；生产态 `~/.forgeself` 已外置）。

---

## 2. 空间浪费点（优化目标清单）

1. **宿主升级整目录备份**：每次更新把安装目录全量拷贝到 `Backups\<ts>`（含 Plugins、wwwroot、全部 DLL，单次可达数百 MB），无保留/清理策略 → 多次升级累积数 GB。
2. **更新暂存缓存不清理**：~~每次更新下载 ~74MB zip + 解压 ~200MB 到 `Updates\<tag>\`，应用后留存~~ **已实施清理**（输入31 批次1）。
3. **插件 `_backups` 三重冗余**：~~同一插件内容同时存在于 `versions/`、`_backups/<id>/`、活动扁平目录~~ **已消除**（输入31 批次1：`_backups`/`BackupPlugin` 全删，仅 versions/ 单轨）。
4. **图片识别缓存无清理**：~~`ImageRecognitionCache` 只增不减~~ **已实施 TTL 30 天清理**（输入31 批次1）。
5. **本地构建产物堆积**：`artifacts/publish`、`artifacts/release`、`artifacts/plugin-packages` 每版 ~300MB+，不入库但占磁盘。
6. **双更新链路并存**：`UpdateService`(008) 与 `StagedUpdateService`(036) 都写 Backups/Updates，语义重叠。

---

## 3. 目标目录结构（QQNT 式 · 用户拍板方向 · **部分实施**：批次1 = 插件去 _backups + 更新缓存清理 + Backups 退役 + 图片缓存 TTL 已落地；批次2 = 宿主 versions 结构，**未实施**，待立项）

对标 `C:\Program Files\Tencent\QQNT`：**公共/稳定的放外面，每次要更新的放 `versions/`，插件目录与 `versions/` 并排**。

```
安装根/
├── ForgeSelf.exe               # 公共/启动层（稳定，不随版本变；类似 QQ.exe，负责拉起当前版本）
├── update-agent.ps1            # 自更新代理（随包分发）
├── versions/                   # 宿主版本目录：每次更新要动的全落这里
│   └── <semver>/
│       ├── ForgeSelf.dll + 全部宿主 DLL + wwwroot/**   # 不可变版本快照
│       └── current(或根级 current 指针，原子切换)        # 当前生效版本指针
├── plugins/                    # 与 versions 并排；宿主升级不触碰
│   └── <id>/{plugin.json, current, versions/<ver>/**}   # 插件自身版本化（现状已如此）
├── Data/ Log/ Config/          # 运行时生成（生产态走数据根 ~/.forgeself，已外置）
```

设计要点（对应现有机制，逐条可落）：

| # | 要点 | 与现状差异 |
|---|---|---|
| T1 | 宿主每版内容不可变快照入 `versions/<ver>/`，`current` 指针原子切换 | 现为扁平覆盖式安装 |
| T2 | **升级不再整目录备份**：旧版本目录天然保留（当前 + 上一版），回滚 = 切指针 | 现为整目录备份到 `%LOCALAPPDATA%\ForgeSelf\Backups\` |
| T3 | `plugins/` 与 `versions/` 并排，宿主升级只增版本目录，不碰插件目录 | 现为插件目录在安装目录内随全量覆盖 |
| T4 | **插件目录无 `_backups`、无插件备份**：新版本（包源/侧载）直接 stage 到 `versions/<ver>/`（未切 current 即惰性，side-by-side 安全）；回滚走 `versions/` 内保留版本 | **已实施（输入31 批次1）**：`_backups`/`BackupPlugin` 全删，直落 versions/ |
| T5 | 公共/启动层只放稳定件（exe、代理脚本），路径基准改为「安装根」而非 `AppContext.BaseDirectory` | 现为 BaseDirectory 全量基准（AppBuilder/DataLocationService/PluginManager） |
| T6 | 更新暂存（`Updates/<tag>\`）应用成功后即清理；`Backups\` 退役并清理存量 | **已实施（输入31 批次1）**：update-agent.ps1 应用后清理 staged tag + 退役 Backups（008 侧待收口） |

**可行性结论**：结构可行。宿主 .NET self-contained 应用的本体（DLL+wwwroot）全部随版本变化，公共层只有启动器 exe 与代理脚本，与 QQNT 的「QQ.exe + versions/」同构。落地需改动路径基准（AppBuilder.cs、DataLocationService、PluginManager 的 BaseDirectory 引用）、update-agent.ps1（备份覆盖→版本目录+指针）、StagedUpdateService/UpdateService、发布脚本产物布局（publish-host/package-release）、PluginVersionService/PluginInstallerService/PluginController（去 _backups）。**属宿主/CI 级架构变更，实施前须 architecture-design 出方案并经用户拍板**（见 §5）。

---

## 4. 生命周期规则（真源 · 实施后生效）

> 以下为**目标规则**。**已实施项标注（输入31 批次1）**；未实施项现状行为见 §1。

- **R1 宿主版本保留**：`versions/` 保留**当前 + 上一版**（对齐插件 `PruneVersions` 的 MaxRetainedVersions=2）；更旧版本在版本切换后延迟删除（沿用 `PluginAssemblyUnloader.TryDeleteDirectory` 模式：被占用则跳过下轮重试，绝不阻塞）。
- **R2 升级不做整目录备份**：升级 = 新增版本快照 + 原子切 `current`；回滚 = 切指针到保留版本。**已实施（输入31 批次1，036 侧）**：update-agent.ps1 备份步骤已删、Backups 退役清理；008 UpdateService 侧待收口决策。
- **R3 更新缓存清理**：`%LOCALAPPDATA%\ForgeSelf\Updates\<tag>\` 在**应用成功后**删除该 tag 目录（保留日志 `agent-*.log` 供排查）。**已实施（输入31 批次1）**：update-agent.ps1 应用成功后删除 Updates/<tag> 并清理 Backups 存量。
- **R4 插件无备份**：**已实施（输入31 批次1）**：`BackupPlugin`/`GetBackupList`/`RestoreFromBackup` 全删；安装/更新/卸载无备份调用；新版本（包源/侧载）直落 `versions/<ver>/`；CheckForUpdates/GetPluginVersions/EnsureStagedFromPackageSource 全部直读写 `versions/`；publish-plugin.ps1 stage 目标同步。
- **R5 插件版本保留**：沿用 `PruneVersions`（当前 + 上一版，更旧延迟删除）。
- **R6 图片识别缓存**：**已实施（输入31 批次1）**：`LocalFileImageRecognitionCache` 初始化清理 LastWriteTimeUtc 超 **30 天**的会话目录（TTL 常量 `CacheMaxAge`；失败占位结果不写缓存的既有行为保留）。
- **R7 构建产物**：`artifacts/publish|release|plugin-packages` 属可再生物，定期手动清理（不入库，`.gitignore` 已覆盖）。
- **R8 禁止事项**：① 禁止在升级/更新链路中自动化「整目录备份+覆盖」；② 禁止把宿主共享 DLL（`XCode.dll`/`NewLife.*.dll`/`ForgeSelf.*.dll`/`Stardust.dll` 等）拷入插件目录或版本快照（类型分裂，宿主启动即崩，plugin-development 铁律）；③ 禁止 agent 停/启/杀用户运行中的宿主进程（B10/AGENTS §0）；④ 禁止自动化删除数据目录（plugin-development 铁律 10）。

---

## 5. 实施改动清单（批次1 已完成 = 输入31；批次2 待用户拍板立项）

| 改动对象 | 内容 | 风险 |
|---|---|---|
| 宿主路径基准 | `AppBuilder.cs:45/256/540-564`、`DataLocationService.cs`、`PluginManager.SetPluginsDirectory`：BaseDirectory → 安装根（versions 的父目录）；webRoot/plugins/data 解析改造 | 高（启动链核心） |
| 启动器 | 根 `ForgeSelf.exe`（公共层）拉起 `versions/<current>/` 内宿主 | 高 |
| 自更新 | `update-agent.ps1` + `StagedUpdateService.cs`：备份→版本目录+指针切换；`Updates/` 应用后清理；`Backups/` 退役 | 高 |
| 发布脚本 | `publish-host.ps1`（产物入 versions/）、`package-release.ps1`（清洁规则、_backups 删除）、`release-local.ps1`、CI 契约 | 中 |
| 插件备份 | `PluginVersionService.cs` / `PluginInstallerService.cs` / 038 包源流：去 `_backups`，直接 stage 到 versions/ | 中 | **✅ 已实施（输入31 批次1）** |
| 侧载/迁移脚本 | `publish-plugin.ps1` 同步去 `_backups`（`package-plugin.ps1`/`migrate-plugin-versions.ps1` 本就幂等兼容） | 中 | **✅ 已实施（输入31 批次1）** |
| 缓存策略 | `ImageRecognitionCache` TTL 30 天 | 低 | **✅ 已实施（输入31 批次1）**；`DataStoragePanel.vue`「清除缓存」接线核查 → TODO（P2） |
| 旧链路收口 | `UpdateService`(008 Windows 服务) 与 036 并存：建议冻结 008（服务模式不再维护），或统一到 036 | 中（建议决策） |
| 文档/技能 | AGENTS.md、agent-workflow B5、035/038、Plugins/README、plugin 技能按本真源同步 | 低 | **✅ 批次1 已同步**（plugin-development/plugin-publish-verify 速查此前已加引用） |

---

## 6. 引用关系

| 文档 | 引用方式 |
|---|---|
| `AGENTS.md` | §2.3 打包流程收敛为操作要点 + 引用本文（真源说明） |
| `docs/04-standards/agent-workflow.md` | B5（插件体系与发布）、B10（CI 自动发布）顶部加引用；目录结构/备份事实不再重复定义 |
| `docs/02-features/035-plugin-versioned-layout.md` / `038-plugin-local-update-source.md` | 关联段加引用（`_backups` 目标语义以本文 §3-T4/§4-R4 为准） |
| `.agents/skills/plugin-development` / `plugin-publish-verify` | 关键事实速查加引用（活动目录/`_backups` 描述指向本文） |
| 发布/升级脚本 | 头注释引用本文 |

---

## 变更记录

| 日期 | 变更 |
|------|------|
| 2026-09-28 | 建立本文（输入30）：盘点打包/升级/备份/缓存全部落点；确立 QQNT 式目标目录结构；确立去 `_backups`/去插件备份/更新缓存清理规则；登记实施改动清单。 |\n| 2026-09-28 | 批次1 实施完成（输入31 用户拍板改代码）：插件去 `_backups`/`BackupPlugin`（PluginVersionService/PluginInstallerService/publish-plugin.ps1 直落 versions/）；update-agent.ps1 去整目录备份 + 应用后清理 Updates/<tag> + 退役 Backups 存量；`LocalFileImageRecognitionCache` TTL 30 天清理；测试 56/56 绿；宿主 QQNT versions 结构（批次2）待立项。 |
