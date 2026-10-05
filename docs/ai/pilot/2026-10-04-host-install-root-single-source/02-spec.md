# Specification

任务：`2026-10-04-host-install-root-single-source`（**插件根两路解析 + 内置插件随版本发布**）｜时刻：2026-10-04 19:5x。
⚠️ 本文件第一版写的是"新增 `InstallRootResolver`，从 `ProcessPath` 上溯找**安装根**锚点"——**已被用户当场推翻**（原话：「我说的不是安装根，而是根每个版本一起发布的插件目录，应该是在…版本目录里面…绝对不是安装根的插件目录」）。第二版按用户裁定重写，旧方案未实施、不留代码。

## Functional Requirements
- **FR1 内置插件随版本发布**（发布侧）：`scripts/release/package-release.ps1` 第 3 步**取消**"复制 `plugins/` 到安装根 + `Remove-Item versions/<ver>/plugins`"，改为保留 publish 原生产物；并在 `versions/<ver>/plugins` 不存在时 **throw**（发布链当场失败，不再静默出"升级后 0 插件"的包）。sanitize 段同时清理版本目录内的 `plugins/_backups` 历史残留。
- **FR2 插件根两路**（运行侧）：`PluginManager` 的插件根由单值改为**按优先级排序的根列表**——
  - 第一路（内置）= `DevMode.PluginsDirectoryOverride ?? Path.Combine(AppContext.BaseDirectory, "plugins")`（语义与今天一致，覆盖通道优先级保持最高）；
  - 第二路（数据目录）= `Path.Combine(数据根, IDataLocationService.PluginDataRootName)`，由 `AppBuilder` 在 `SetPluginsDirectory` 之后调用新增的 `AddPluginRoot(dir, source)` 接入；`source` 仅用于日志标注来源。
  - `PluginsDirectory` 兼容属性保持指第一路（内置根），使 `PluginInstallerService` / `PluginVersionService` / 前端中间件的既有语义零变化（写入位置迁移属下一批，见 Unknown/Boundary）。
- **FR3 合并与裁决**：`DiscoverPlugins()` 逐根扫描并合并，按插件 Id 去重（大小写不敏感）；同 Id 跨根时按**版本号**裁决——可解析为 `Version`（2~4 段）时数值大者生效；任一不可解析则视为相等 ⇒ 保留先扫到的（内置）那份。裁决发生覆盖时打 WARN（含两侧版本与来源目录）。
- **FR4 数据目录同树的安全性**：`{数据根}/plugins/{插件Id}/` 已是**插件数据**目录（实测 `agent-hub/ ai-agent/ design-system/ …` 12 个子目录只含 `*.db`），因此第二路只把**含 `plugin.json` 的子目录**当插件包；无清单者按今天既有逻辑 Debug 跳过（不新增报错、不新增日志噪声）。
- **FR5 可见性**：① 每条"发现插件"日志带来源目录；② 扫描完成行输出"插件根 N 个：根1 | 根2"；③ 某一路根不存在 ⇒ WARN 后**继续扫下一路**（旧代码在唯一根缺失时直接 `return`，是"静默 0 插件"的最后一环）。

### 第二批（输入19，2026-10-04 19:4x 用户追加）：版本目录逐代嵌套

- **FR6 安装根唯一解析口径**：新增 `ForgeSelf.Api/Services/HostInstallRoot.cs`——纯函数，从任意起点目录起「父目录名为 `versions` 就一路上跳两级」，扁平原样返回；同文件暴露 `VersionLayerCount`（0=扁平、1=正常 QQNT、≥2=嵌套）供日志诊断，`ResolveFromExecutable(exePath)` 供更新链路调用。
- **FR7 更新链路改用安装根**：`StagedUpdateService.ApplyStaged` 的 `-InstallDir` 由 `Path.GetDirectoryName(Environment.ProcessPath)` 改为 `HostInstallRoot.Resolve(exeDir)`；layers>1 时 WARN 点名嵌套层数与「起点 → 归一化结果」，layers==1 时 INFO 记录三处路径（安装根/业务层/层数）。`update-agent.ps1` 的回退查找路径随之一致（代理脚本本来就在公共层，旧写法在 QQNT 布局下永远找不到回退件）。
- **FR8 代理侧同源归一化 + 硬拦**：`scripts/update-agent.ps1` 新增 `Resolve-ForgeInstallRoot`（与 FR6 同一规则）+ 步骤 0：入口处归一化 `$InstallDir` 并写日志；归一化后若父目录仍名为 `versions` ⇒ **当场 throw，不碰盘**。**理由（不是冗余）**：触发嵌套的那一次升级是「**老宿主 + 随包新代理**」——老宿主的 C# 已经装在用户机器上改不动，只有随包分发的代理脚本能拦住它自己。
- **FR9 两侧规则同源可证**：常驻判据必须能抓住「只改一侧」的漂移（见 AC12）。

## Input
无对外接口变化。运行期输入 = 进程位置（`AppContext.BaseDirectory`）、CLI/env 覆盖、`{数据根}/plugins` 磁盘内容。

## Output
- 插件发现结果（两路合并、每 Id 一份生效）＋上述日志；
- 不新增端点、不新增配置项、不改 `GET /api/plugin` 响应结构。

## Business Rules
- BR1 覆盖优先级：`--plugins-dir` ＞ `FORGESELF_PLUGINS_DIR` ＞ 内置默认根；数据目录根**恒追加**（除与已有根同路径，忽略大小写去重）。
- BR2 内置根在前＝扫描顺序在前；但**生效与否由版本号决定**（用户装的更高版本必须能压过随版本的旧内置，否则"更新无效"）。
- BR3 版本畸形（如 `v3.1.0`、`1.0.0-beta`）一律视为相等 ⇒ 保留内置，防止一份坏清单把生产插件换掉（有 Theory 用例逐条钉）。
- BR4 数据目录只读扫描，不改写、不清理、不搬迁任何既有目录（搬迁属下一批，需用户在场）。

## Boundary Conditions
- 两路根都不存在 ⇒ 空列表 + 两条 WARN（不再有"早退导致第二路根本没扫"的形态）。
- 内置根缺失、数据根有包 ⇒ 仍能加载（现场修复路径，有单测）。
- 数据根下既有 `design-system/`（插件数据，无清单）又有 `DesignSystem/`（含清单的包）⇒ 只认后者；两棵并存不误报（实测 `~/.forgeself/plugins` 目前只有数据子目录）。
- 同 Id 同版本两路都有 ⇒ 保留内置（顺序 + 相等不覆盖），日志记"跳过"原因。
- 单根模式下（只 `SetPluginsDirectory`，未追加第二路）行为与改动前逐字等价（既有 4 个测试类仍用该入口，未改动即通过）。
- **（第二批）扁平形态输入**：dev / 测试输出目录 / 旧扁平安装（`D:\x\ForgeSelf.exe`）⇒ 归一化必须**原样返回**、层数 0，不得上跳出"父目录恰名为 versions 但不是版本层"的目录之外（反证用例：`…/backup/2.7.2.0`）。
- **（第二批）三层嵌套输入**（现场形态 `…\versions\2.2.11\versions\2.2.2026.0930\versions\2.7.2.0`）⇒ 回到真安装根、层数 3；即**升级一次即回正**，多余层成为无人引用残留（清理不在本批）。
- **（第二批）根/盘符边界**：起点即盘根（`D:\`）或 `versions` 直接位于盘根（`D:\versions\1.0.0`）⇒ 不得死循环、不得抛空引用；尾部分隔符与大写 `Versions` 都要与另一侧一致。

## Error Handling
- 单个插件目录扫描异常仍按现有 `catch` 记录 Error 并继续下一个目录（不改语义）。
- 打包脚本的产出校验失败 ⇒ throw，让 `release-local.ps1` 当场非零退出（判定看日志正文，不靠 exit code 猜测）。
- **（第二批）宿主侧拿不到程序目录** ⇒ `HostInstallRoot.ResolveFromExecutable` 抛 `InvalidOperationException`（沿用旧 `ApplyStaged` 的"无法确定应用程序目录"语义，不猜路径）。
- **（第二批）代理侧归一化后仍在 `versions/<ver>/` 内** ⇒ `throw "安装根仍位于 versions/<ver>/ 内，拒绝产生逐代嵌套: …"`，发生在**任何写盘动作之前**（步骤 0，早于 robocopy）；宿主已退出 ⇒ 走脚本既有 catch：不动 `current` 指针、尽力重启根启动器，用户仍能用旧版本。

## Compatibility
- **零破坏**：不改插件清单格式、不改包内公共层、不改 DI 注册与 `PluginOptions.PluginsDirectory` 默认值。（第二批更正：`update-agent.ps1` **改了**——第一批的"不改代理脚本"只对插件根那一路成立；嵌套修复必须落在代理侧，理由见 FR8。）
- 行为差异（第一批三处 + 第二批一处）：① 包里 `plugins/` 从"安装根一层"回到"`versions/<ver>/` 一层"（= **首次安装的目录结构变化**，dev/e2e 不受影响）；② 数据目录 `plugins/` 现在会被扫（此前完全不扫）；③ 缺一路根不再中断扫描；④ 更新链路交给代理的 `-InstallDir` 由"业务层目录"改为"安装根"（dev/扁平形态两者相同 ⇒ 零变化；QQNT 形态下嵌套实例升级一次即回正）。
- 老布局兼容：已经存在的安装根 `plugins/`（用户那台中间层里 17:32 落盘的 3.1.0）在新逻辑下不再是扫描点 ⇒ 靠"升级后版本目录自带插件"或"数据目录那一路"生效；历史目录不删（见 Unresolved）。

## Non-functional Requirements
- 启动开销：多扫一路 = 一次 `Directory.Exists` + 一次 `GetDirectories`（数据根通常不存在 ⇒ 只 1 次存在性判断），不引入磁盘遍历放大。
- 可诊断性：任何一份生效插件都能从日志一眼指出来源目录（这是本缺陷原本缺的东西）。
- 测试性：新增判据一律常驻（单测 + 仓库级守卫），不依赖手工现场观察。

## Acceptance Criteria
- AC1 两路各有一插件 ⇒ 两个都发现，且 `PluginRoots` 顺序为 [内置, 数据目录]，`PluginsDirectory` 仍 == 内置根。
- AC2 同 Id 数据目录版本更高 ⇒ 只生效一份、版本取高、`PluginDirectory` 指向数据目录那份。
- AC3 同 Id 版本相同 ⇒ 保留内置那份。
- AC4 数据目录里只有 `*.db` 的子目录 ⇒ 不被当插件（带"该目录确实存在于扫描路径下"的阳性对照）。
- AC5 内置根缺失 + 数据根有包 ⇒ 不抛、仍发现（现场形态回归防护）。
- AC6 重复追加同一根 ⇒ 只留一路（`AddPluginRoot(null)` 也不报错）。
- AC7 版本比较 Theory：`3.1.0>3.0.0`、`1.0<1.0.1`、`2.7.2.0==2.7.2.0`、`v3.1.0` 与 `1.0.0-beta` 一律 0（相等）。
- AC8 发布布局常驻守卫：`package-release.ps1` 不得再出现"外置到安装根 / 从版本目录删除"的写法，且必须带 `Test-Path` 产出校验；**反向探针已做**（把旧写法插回 ⇒ 守卫实红：失败 1 / 总计 21；还原 ⇒ 32/32 绿）。
- AC9 档位：中档后端全量 `dotnet test`（碰宿主源码）+ design-system 插件层 e2e 不新增稳定红。
- **AC10（第二批）归一化纯函数**：扁平（dev/测试输出/旧扁平安装）原样返回且层数 0；一层 QQNT → 真根、层数 1；两层、三层（现场形态）→ 真根、层数 2/3；大写 `Versions` 与尾部分隔符同结果；父目录不是 `versions` 的同形状路径（`…/backup/2.7.2.0`）不上跳（反证）。
- **AC11（第二批）由 exe 路径解析**：`ResolveFromExecutable(<…>/versions/a/versions/b/ForgeSelf.exe)` == 真安装根；拿不到程序目录（null/空）⇒ 抛 `InvalidOperationException`，不猜。
- **AC12（第二批）两侧同源实跑判据**：测试从 `scripts/update-agent.ps1` 抽出 `Resolve-ForgeInstallRoot` 函数体，用 `pwsh` 对**同一批输入**逐条执行，比对 Root 与 Layers 与 C# 结果一致（只改一侧规则必红）。
- **AC13（第二批）接线守卫**：`StagedUpdateService.cs` 必须出现 `HostInstallRoot.Resolve(`，且不得再出现"把 `ProcessPath` 目录直接当 installDir"的写法。
- **AC14（第二批）反向探针**：把代理函数改成不上跳 + 把宿主侧退回 `ProcessPath` 目录 ⇒ AC12/AC13 同时实红；还原后复绿（读数见 05）。

## Unknown
- U1 **安装/更新写入位置**：`PluginInstallerService` / `PluginVersionService` 仍以 `PluginsDirectory`（内置根）为目标 ⇒ 更新会写进"应随版本不可变"的版本目录。本批刻意不改（改面涉及热切换与 stage 目录布局），**已入 TODO**，下一批连同 e2e 真实布局用例一起做。
- U2 用户那台三代混代实例能否只靠"升级到新包"完全恢复（其外层/中间层历史 `plugins/` 残留是否会造成困惑）：需一次真机只读复验，不预判。
- U3 `PluginFrontendFileMiddleware`、`DevController` 的根读数是否要跟随两路（当前只读展示与前端资源路径，未验证两路场景）：记 TODO。
- U4（第二批）**已存在的嵌套目录**：修复只保证"不再产生新层"，用户那台的三层残留要靠一次真升级回正（下一次 `ApplyStaged` 起即用真安装根）；多余层内历史 `plugins/`（1.2.1 + 4096 B 桩）的清理属不可逆面 ⇒ 不在本批，需用户在场，已入 TODO「混代实例收编」。
- U5（第二批）**008 `UpdateService` 的 `_appDir` 仍是 `ProcessPath` 目录**（`UpdateService.cs:60`，替换/备份目标）：该类已在批次2 冻结、无生产调用者（`ApplyUpdateAsync` 只有测试调用），故本批不动；若将来复活该路径，它会把公共层文件覆写进版本目录 ⇒ 记 TODO（同源缺陷的另一张脸）。
- U6（第二批）**代理侧硬拦的可达性**：归一化循环本身保证收敛后父目录不名为 `versions`，故 throw 分支在正常输入下不可达（它是"规则被改坏时不动盘"的保险，不是常规错误路径）。它的**存在**由源码守卫钉（`拒绝产生逐代嵌套` 字面串在脚本内），它的**行为**未做独立用例（需要 mock 掉归一化才有红，收益低于成本）——如实标 Unknown，不写成"已验证拦截生效"。
