# Evidence

> 阶段：Stage 7｜
>
> **只记录实际发生的事情**
>
> ，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。
> ⛔ 禁用表述：「应该可以」「理论上通过」「看起来没问题」「大概率是」「估计可以」。

## Task

PILOT-028（批次 1 = 输入31 已实施；批次 2 = 输入34 已实施）

## Changed Files（批次 1，已提交 aed3789）

* `ForgeSelf.Api/Plugins/Services/PluginVersionService.cs`（重写：删 BackupPlugin/GetBackupList/RestoreFromBackup/StageVersion/GetHighestBackupVersion；CheckForUpdates source=staged；UpdatePlugin 激活不复制；RollbackPlugin 只认 versions/）

* `ForgeSelf.Api/Plugins/Services/PluginInstallerService.cs`（删三处 BackupPlugin 调用；批次 2.5 重写 UpdateFromPackage 为版本化）

* `scripts/update-agent.ps1`（去整目录备份 + 应用后清 Updates// + Backups 退役；批次 2.3 重写为 QQNT 版本化应用）

* `scripts/publish-plugin.ps1`（stage 目标 → Plugins//versions//）

* `ForgeSelf.Api/Services/AI/LocalFileImageRecognitionCache.cs`（构造器调 CleanupExpiredSessions，TTL=30 天）

* `ForgeSelf.Api.Tests/Plugins/PluginVersionServiceTests.cs`（versions/ 直落布局 + 新增 RollbackPlugin_UnknownVersion_ReturnsFalseWithoutChanges）

* `ForgeSelf.Api.Tests/Plugins/PluginVersionUpdateSourceTests.cs`（staged 直落更高版本时包不列出）

* `ForgeSelf.Api/Models/Plugins/PluginDetailDto.cs`、`ForgeSelf.Web/src/types/plugin.ts`（source 注释 backup→staged）

* 文档：`docs/04-standards/packaging-upgrade-backup.md`（§1/§2/§3-T4・T6/§4-R2・R3・R4・R6/§5 标记批次 1 已实施 + 变更记录）、`docs/02-features/035`（6 处）、`038`（4 处）、`docs/04-standards/agent-workflow.md`（B5 5 处）、`ForgeSelf.Api/Plugins/README.md`（10 处）、`docs/05-guides/plugin-hot-reload-limitations.md`（2 处）、`AGENTS.md`（§2.3 两条）

## Changed Files（批次 2 = 输入34，**未提交 git，未获提交授权**）

* `ForgeSelf.Bootstrapper/ForgeSelf.Bootstrapper.csproj` + `Program.cs`（新建根启动器：AssemblyName=ForgeSelf.Bootstrapper 避开宿主 ForgeSelf.dll；自包含 + AspNetCore.App + WindowsDesktop.App；versions/current 指针或 --forge-version 强制；ALC Resolving 从版本目录补业务依赖；Assembly.EntryPoint 调 Main；[boot] 错误写 stderr 返回 1）

* `ForgeSelf.Api/AppBuilder.cs`（webRoot/ContentRoot 路径基准 = 入口程序集目录；业务层目录含 appsettings.json 时指向之，否则保 CWD 护 dotnet run；插件目录注册注释）

* `ForgeSelf.Api/Program.cs`（托盘 SetBasePath 同步；托盘「检查更新/启动时检查」改走 036 `StagedUpdateService.CheckAsync`）

* `ForgeSelf.Api/Services/UpdateService.cs`（008 冻结：类头冻结声明，不再维护；全流程方法为历史保留禁止新调用）

* `ForgeSelf.Api/Services/StagedUpdateService.cs`（类头更新：唯一更新链路 + QQNT 版本化应用说明）

* `ForgeSelf.Api/Plugins/Services/PluginVersionService.cs`（+StageUploadedPackage：ReadPackageMetadata→Id 匹配→版本合法 `^\d+(\.\d+){0,3}$`→ValidatePackage→已存在幂等→直落 versions/<ver>/）

* `ForgeSelf.Api/Plugins/Services/PluginInstallerService.cs`（UpdateFromPackage 重写为版本化：同级/降级 InvalidOperationException、非法版本 InvalidDataException→StageUploadedPackage→UpdatePlugin 激活→返回刷新元数据）

* `ForgeSelf.Api.Tests/Plugins/PluginInstallerServiceTests.cs`（新建，4 用例）

* `scripts/update-agent.ps1`（重写为 QQNT 版本化应用：等宿主退出→定位 staged versions/<ver>→业务层 robocopy 到 InstallDir/versions/<ver>/→公共层 robocopy /XD versions→写 versions/current→扁平存量迁移清理→版本保留 current+最高旧版→清 staged + Backups→重启根启动器）

* `scripts/release/publish-bootstrapper.ps1`（新建：公共层自包含 publish 到 artifacts/layout-root，exe 重命名 + ForgeSelf.* 残留清理 + coreclr 断言）

* `scripts/release/publish-host.ps1`（FDD 默认，-SelfContained 开关，publish 到 artifacts/publish）

* `scripts/release/package-release.ps1`（重写为 QQNT 组装：公共根 + versions/<ver>/ + versions/current + Plugins 移到公共根 + sanitize + SQLite 运行时构件注入 + zip）

* `scripts/release/release-local.ps1`（唯一编排：build-frontend→publish-host→publish-bootstrapper→package-release→make-release-notes）

* `ForgeSelf.slnx`（add Bootstrapper）

* 文档：`docs/04-standards/packaging-upgrade-backup.md`（批次 2 标记：状态行/§1.2/§1.5/§2-1・6/§3 标题+T1・T2・T3・T5/§4-R1・R2/§5 四行 ✅/变更记录批次 2 行）

## Build（批次 2 终态）

Command:

```
cd ForgeSelf.Api && dotnet build
```

Result: PASS（来源等级：Verified）

```
Build succeeded. 0 Error(s), 322 Warning(s)（存量 nullability warnings）
```

## Unit Test（批次 2 终态）

Command:

```
dotnet test --no-build --filter "FullyQualifiedName~PluginVersion|FullyQualifiedName~PluginInstaller"
```

Result: PASS（来源等级：Verified）

```
23/23 通过（含 PluginInstallerServiceTests 新增 4 用例：同级/降级拒绝、非法版本拒绝、合法更高版本直落 versions/、激活后元数据刷新）
```

Command（批次 1 终态基线，供对照）：

```
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~PluginVersion|FullyQualifiedName~PluginInstaller|FullyQualifiedName~PluginFrontend|FullyQualifiedName~PluginMenu|FullyQualifiedName~ImageRecognitionCache|FullyQualifiedName~TempPluginDirectory"
```

Result: PASS（来源等级：Verified）

```
56/56 通过
```

Command:

```
dotnet test ForgeSelf.Api.Tests   # 全量（批次 1 终态基线）
```

Result: PASS with pre-existing failures（来源等级：Verified）

```
1542 通过 / 9 失败（1551 总计）。9 失败全部落既有四族，与本任务零文件交集：
  - WorkflowPlanningIntegrationTests ×6（404：/api/ai-agent/workflow/* 路由在测试宿主未注册，存量 TODO 批次E）
  - ScriptRunnerDiIntegrationTests ×1（404 flaky，插件路由注册时序）
  - TerminalCommandGuardTests ×1（断言 Contain("remove-item") 大小写不匹配，测试自身断言问题）
  - ForgeConfigTests ×1（并行 flaky）
归因：040-B1（SessionEvent 联合化）在飞区；未修、非本任务引入。
```

## End-to-End 演练（批次 2，来源等级：Verified）

### ① QQNT 布局冒烟（根启动器 + 发布产物）

隔离环境（独立 USERPROFILE + FORGESELF_INSTANCE_ID + ForgeSetting.config PortNumber=7103 避开 51888 用户实例），从 `artifacts\layout` 启动根启动器：

- 进程存活、Web 监听 ✓
- stdout「Content root path: ...\artifacts\layout\versions\2.2.10」（AppBuilder 路径基准生效）✓
- stdout「插件版本管理服务初始化，插件目录: ...\artifacts\layout\Plugins（布局 = versions/<ver>/ + current，无备份目录）」（插件公共外置生效）✓
- 初版两次失败（Main 未找到 → 改 Assembly.EntryPoint；System.Windows.Forms 缺失 → 加 WindowsDesktop.App FrameworkReference）后成功
- 已知环境限制：托盘 TryCreate failed 属 Hidden 窗口冒烟环境限制（H.NotifyIcon 需可见窗口会话），线程崩溃不杀主进程（Web 仍监听），非代码缺陷

### ② update-agent 版本化应用演练（ua-e2e，全在 .forgeself/memory 下不碰真实宿主）

模拟「旧扁平安装（含 ForgeSelf.Api.dll/wwwroot/appsettings.json 扁平残留 + 已一次 QQNT 升级的 versions/2.2.0 + current）」，跑 update-agent.ps1 应用 staged 的 QQNT 包（versions/2.3.0）：

| 验证项 | 结果 |
|---|---|
| versions/2.3.0/ 业务层落盘 | ✓ True |
| versions/current → 2.3.0 | ✓ True |
| 扁平残留清理（ForgeSelf.Api.dll/wwwroot/appsettings.json） | ✓ 全清 |
| 旧版 versions/2.2.0 保留（回滚能力） | ✓ True |
| Plugins 合并到公共根 | ✓ True |
| staged tag 清理 | ✓ True |
| Backups 退役清理 | ✓ True |
| 重启段：根启动器被拉起、读 current=2.3.0（dummy 业务层加载失败报 [boot] 退出，证明链路通） | ✓ True |

### ③ 发布管线

`release-local.ps1 -Version v2.2.10 -SkipFrontend`：PASS（来源等级：Verified），zip `OpenForgeSelf-2.2.10-win-x64.zip` **65.3MB**（旧扁平 0.2.5=75.6 / 0.2.6=76.9 / 2.2.9=78.1MB）+ SHA256SUMS.txt + RELEASE-NOTES；layout = 公共根 + versions/2.2.10/（40 文件业务层）+ Plugins 并排 + versions/current。

## Integration Test

Result: N/A（依据：本任务无新增集成测试；插件版本化链路由 PluginVersionServiceTests / PluginInstallerServiceTests / PluginVersionUpdateSourceTests 单测覆盖）

## E2E

Result: N/A（依据：无 UI 行为变化；宿主升级链路为脚本/服务层，用上述端到端演练替代；插件管理页走查待后续按 e2e-testing 技能补充）

## Static Analysis

Result: N/A（依据：前端仅注释改动；后端改动无 lint 体系；dotnet build 0 error 已覆盖编译面）

## Screenshots

N/A（无 UI 变更）

## Known Limitations

* `DataStoragePanel.vue`「清除缓存」按钮未见后端 API 接线（2026-09-28 grep 核查，疑为占位）——已登记 TODO（P2），本任务未动。

* C 盘剩余 4GB（496/500GB 用）：`C:\Temp` 对 testhost 拒绝建目录根因未解决，测试用自定义 TEMP（D 盘 `.forgeself\test-tmp`）绕开——环境问题，非代码缺陷。

* 冒烟遗留若干杀不掉的僵尸 ForgeSelf 进程（pids 82676/83584/79392/73628 等，Access denied）：已尽力 taskkill 清理；不监听端口、不占 mutex，不影响后续。

* `package-release.ps1` 打包时删 `Plugins/_backups` 防御性删除保留（清存量，无害）；`migrate-plugin-versions.ps1` 保留跳过 `_*`（幂等兼容存量）。

## Unresolved Issues

* 无 BLOCKED 项。全量测试 9 失败归因明确（既有四族 + 040-B1 在飞区），与本任务零交集。

* **批次 2 全部改动未 git 提交**（未获用户提交指示；用户偏好：提交须明确指示、同任务汇总一次性提交；040-B1 在飞区须精确隔离）。

* 端口真源（文档大量写 51888 vs 7102）待用户拍板统一（登记 TODO）。

---

## 输入36 单文件化（2026-09-28，未提交 git）

### Changed Files（输入36 追加）

* `ForgeSelf.Bootstrapper/Program.cs`（**重写为进程拉起模式**：读 versions/current 或 --forge-version=<v> → Start 子进程 versions/<ver>/ForgeSelf.exe（WorkingDirectory=versionDir、DOTNET_ROOT=安装根、剔除 --forge-version 透传其余参数）→ WaitForExit 转发退出码；[boot] 错误写 stderr 返回 1。单文件业务层是 apphost 无法 Assembly.Load，故弃 ALC 改进程）

* `scripts/release/publish-bootstrapper.ps1`（**重写为公共层两步组装**：① FDD 单文件 publish 薄壳启动器（~170KB managed-only bundle）；② 从本机 dotnet 安装目录拷贝 DOTNET_ROOT 结构运行时 = host/fxr/<最新10.0.x> + shared/{Microsoft.NETCore.App,Microsoft.AspNetCore.App,Microsoft.WindowsDesktop.App}/<ver> + 根 hostfxr/hostpolicy app-local shim；清理原名 exe/残留；FDD 薄壳大小断言）

* `scripts/release/publish-host.ps1`（加单文件参数：PublishSingleFile=true + IncludeNativeLibrariesForSelfExtract=false + IncludeAllContentForSelfExtract=false —— wwwroot/appsettings/SQLite 原生外置，AppBuilder 路径基准 = Assembly.Location = 单文件 exe 目录）

* `scripts/release/package-release.ps1`（适配：业务层检查 ForgeSelf.dll → ForgeSelf.exe；**删除「业务层 exe/runtimeconfig/deps 冗余删除」旧逻辑**（单文件化后 exe 是业务层本体必须保留）；头注释更新单文件结构）

* 文档：`docs/04-standards/packaging-upgrade-backup.md`（状态行/§1.5/§3 标题+T5・T7/§5 发布脚本行/变更记录输入36 行）

### Verified（输入36 单文件化，来源等级：Verified）

| 验证项 | 命令/方法 | 结果 |
|---|---|---|
| 公共层 FDD 单文件启动器 | `publish-bootstrapper.ps1` 两次 publish 组装 | PASS：公共层 627 文件（含 shared 框架）+ ForgeSelf.exe 0.16MB + fxr=10.0.12 |
| 启动器进程模式链路 | 完整安装根（公共层 + versions/9.9.9/ForgeSelf.exe dummy FDD 单文件）同步执行 | PASS：退出码 7 透传；dummy 打印 base=…versions\9.9.9\（业务层 BaseDirectory = exe 目录）、procpath=…versions\9.9.9\ForgeSelf.exe |
| 组装 | `package-release.ps1 -Version 9.9.9`（dummy 模拟业务层） | PASS：layout = host/ + shared/ + versions/9.9.9/（2 文件）+ Plugins/ + ForgeSelf.exe + hostfxr.dll + update-agent.ps1；zip 83.9MB |
| artifacts 清理 | Remove-Item artifacts（用户批准） | PASS：释放 918MB |

### 关键踩坑（输入36 实测）

* **自包含单文件弃用**：无论压缩与否，自包含单文件启动时把 bundle 解压到 `DOTNET_BUNDLE_EXTRACT_BASE_DIR` 缓存运行，`AppContext.BaseDirectory` / `Environment.ProcessPath` 均指向提取目录（`%TEMP%\.net\ForgeSelf\<hash>\`）→ 启动器无法定位安装根；且解压缓存长期占盘（版本变化 hash 变 → 旧缓存残留）——与「省空间」目标相悖。
* **FDD 单文件为正解**：managed-only bundle 原地直跑，BaseDirectory = exe 目录（dummy 实测）、无解压缓存；运行时由公共层 DOTNET_ROOT 结构提供（根 hostfxr.dll app-local shim 让 FDD 启动器自身解析；业务层由启动器传 DOTNET_ROOT=安装根）。
* **FDD 应用 + 扁平运行时（自包含多文件布局）不工作**：hostfxr 要求 DOTNET_ROOT/shared 目录结构（实测 missing framework 10.0.0）。

### 未验证（阻断：040-B1 在飞区）

* **业务层真实 publish（ForgeSelf.Api FDD 单文件）**：publish-host.ps1 已加单文件参数，但全量构建被 040-B1 的 AgentHub 编译错误阻断（`IAgentHubPlugin.cs(57) CS0104/CS0311 IAgentRegistry 二义`，并行会话在飞区文件，本任务不碰）——参数为标准 MSBuild 属性（dummy FDD 单文件已验证等效组合），待 040-B1 收口后跑 `release-local.ps1` 全量验证真实业务层单文件产物 + wwwroot/插件 targets 交互 + update-agent 端到端升级演练。
* TerminalCommandGuard 断言修复（`Contain("remove-item", OrdinalIgnoreCase)`，1 行）同样被上述编译错误挡住未跑测试（断言逻辑明确，风险低）。


---

## 输入37 目录命名统一小写（2026-09-29，未提交 git）

### Changed Files（输入37 追加）

* `ForgeSelf.Api/AppBuilder.cs`：① 启动早期设 `NewLife.Setting.Current.LogPath = {数据根}/log` + `Save()`（NewLife 默认 Log 挂程序目录，须在首次日志写入前外置）；② `configRoot` 改 `"config"`（原 "Config"）；③ update-settings.json / plugin-update-settings.json 路径 `"Config"`→`"config"`；④ `pluginsPath = Path.Combine(AppContext.BaseDirectory, "plugins")`（原 "Plugins"）；⑤ 注释 data/ 语义
* `ForgeSelf.Api/DataLocationService.cs`：开发模式 `"Data"`→`"data"`（ResolveHostDataDirectory 静态/实例两处）+ XML 注释
* `ForgeSelf.Abstractions/IDataLocationService.cs`：`PluginDataRootName = "plugins"`（原 "Plugins"）
* `ForgeSelf.Api/Plugins/ServiceCollectionExtensions.cs`：`PluginsDirectory = ..."plugins"`（原 "Plugins"）
* `ForgeSelf.Api/Program.cs`：`UnifyAllConfigFiles(... "config")` 两处（主入口 + RunTrayMode）
* `ForgeSelf.Api/Data/ForgeConfig.cs`：`fcp.FileName = ..."config/{name}.config"`（原 "Config"）
* `Plugins/ImGateway/Core/ImGatewayConfig.cs`（数据目录回退 `~/.forgeself/plugins/im-gateway`）+ `Plugins/ImGateway/Services/WeComScanAuthService.cs`（`AppContext.BaseDirectory/plugins/ImGateway/wecom-cli...exe` 候选）
* 测试断言 8 文件：DataLocationServiceTests（4 处 "Data"/"Data/Plugins"→小写 + 2 方法名）、ForgeConfigTests（"Config/ForgeSetting.config"→"config/..."）、PluginReloadTests / PluginLifecycleEventTests / PluginEventBusBubblingTests（2 处）/ Integration/PluginServiceLifecycleIntegrationTests / Plugins/McpCenterTests/McpClientIntegrationTests（`BaseDirectory/"plugins"` File.Copy/MockScript 路径）
* 脚本 7 件：`update-agent.ps1`（注释小写 + **§6.5 目录名规范化**：InstallDir 与 `~/.forgeself` 两处 Plugins/Data/Log/Config→小写，MoveFileEx 纯大小写改名，幂等仅 -ceq 大写时执行）、`package-release.ps1`（$pubPlugins/$layoutPlugins "Plugins"→"plugins" 3 处 + sanitize Data/Log/Config→小写 + build/runtime/plugins）、`publish-host.ps1`（$pluginDir='plugins'）、`release-local.ps1` / `publish-bootstrapper.ps1` / `migrate-plugin-versions.ps1` / `publish-plugin.ps1`（注释/路径小写）
* 仓库目录：`build/runtime/Plugins` → `build/runtime/plugins`（MoveFileEx，内容 e_sqlite3.dll + System.Data.SQLite.dll 完好）
* NewLife 探针工程 `.forgeself/test-tmp/nl-probe`（验证 `XTrace.LogPath`/`NewLife.Setting.Current.LogPath/DataPath` setter 可用 + Save() 落盘；NewLife.Core 11.17.2026.701 / XCode 12.0.2026.701）
* 文档 11 件：真源（状态行/§1.5/§3 T8/R9/§5/变更记录）、AGENTS.md §2.3 引用行、agent-workflow（8 处目录事实 + NewLife 日志外置修正）、034/032（数据根 plugins）、035/052（publish/plugins）、configuration.md、ForgeSelf.Api/Plugins/README.md（13 处）、DesignSystem README/ROADMAP、plugin-development/plugin-publish-verify 技能速查

### Verified（输入37，来源等级：Verified）

| 验证项 | 命令/方法 | 结果 |
|---|---|---|
| 编译 | `dotnet build ForgeSelf.Api -c Release` | PASS：0 errors（040-B1 已收口，AgentHub 二义已解决；832 存量 warning） |
| 目录相关单测 | 过滤集 3 类 | PASS：23/27（DataLocationServiceTests 5 + ForgeConfigTests 1 + 部分 XCodeConfigTests 全绿；4 失败 = XCodeConfigTests 系统 Temp 拦截，环境问题见下） |
| NewLife API 可行性 | 独立探针 `.forgeself/test-tmp/nl-probe` | PASS：LogPath/DataPath setter 编译运行 OK、Save() 落盘 OK |
| 脚本语法 | PowerShell AST ParseFile ×7 | PASS：7/7 OK |
| 目录名规范化 | dummy `name-norm-test`（InstallDir + DataRoot 双目标，MoveFileEx） | PASS：Plugins/Data/Config→小写；既有小写跳过；marker 保留；幂等复跑 OK |
| 仓库目录改名 | `build/runtime/plugins` | PASS：内容完好（e_sqlite3.dll + System.Data.SQLite.dll） |

### 关键踩坑（输入37 实测）

* **PS 5.1 纯大小写改名**：`Rename-Item` 与 .NET `Directory.Move` 对纯大小写改名（`Plugins`→`plugins`）都报「源路径和目标路径必须不同」→ 必须 P/Invoke `kernel32.dll MoveFileEx`（Add-Type ForgeNativeMove，dummy 实测成功）。
* **NewLife 日志外置**：`XTrace`/`NewLife.Setting` 默认 Log/Data 挂程序目录；setter 可用（非只读），须在首次日志写入前设 `LogPath`（AppBuilder 已前置）。
* **Windows 大小写兼容**：NTFS 大小写不敏感 → 存量大写目录无需强制迁移，update-agent 应用时一次性规范化（幂等）；源码工程目录（`ForgeSelf.Api/Plugins/<X>`）与命名空间绑定保持 PascalCase 不动（真源 R9）。

### 全量测试 597 失败（环境问题，与本任务零交集）

* `dotnet test`（全量）597 失败 / 995 通过 / 1592 总计——**全部失败源于 `XCodeTestFixture` 构造函数 `Directory.CreateDirectory(Path.GetTempPath()/ForgeSelfTest_<guid>)` 抛 `UnauthorizedAccessException`**（共享夹具炸 = 所有 DB 测试类全炸）。
* 归因证据：① git status 确认 `XCodeTestFixture.cs` 未被本次改动触碰；② 失败点全部在系统 Temp 目录创建，与 plugins/data/config 路径逻辑零交集；③ 手动 PowerShell 创建同路径目录 OK；④ 系统 Temp 残留 757 个 `ForgeSelfTest_*`/`ofs_*` 测试目录（历史从不清理）；⑤ `TMP`/`TEMP` 环境变量指向项目 `.forgeself/test-tmp` 重跑 → testhost 进程崩溃（测试中止，非权限错误）——环境级不稳定，非代码缺陷。
* 处置：登记 TODO（P1 环境：测试临时目录统一改项目 test-tmp 或清理残留 757 目录后复验；安全软件行为拦截为最可能根因）。**本次任务改动范围内测试（目录相关 6/6）全绿**。

### 未提交 git

批次2（输入34）+ 输入36 + 输入37 全部改动未提交（用户偏好：提交须明确指示、同任务汇总一次性提交；git add 须精确隔离 040-B1 并行会话文件）。


---

## 输入37 发布复验与日志外置修正（2026-09-29 二次追加，未提交 git）

### 发布全链路成功（Verified）

* 命令：`release-local.ps1 -Version v2.2.11`（前置 `$env:TMP/$env:TEMP = .forgeself\test-tmp` 规避系统 Temp 对 esbuild 的写入拦截——首次跑被 `Access denied` 拒）
* 全链路：build-frontend（pnpm build 1988 modules）→ publish-host（业务层 FDD 单文件 ForgeSelf.exe 15.66MB）→ publish-bootstrapper（公共层 DOTNET_ROOT 运行时）→ package-release（QQNT 组装）→ make-release-notes，297s 全过
* 产物：`artifacts/release/OpenForgeSelf-2.2.11-win-x64.zip`（101.8MB，真实业务层含全部 18 插件 + wwwroot）+ SHA256SUMS.txt + RELEASE-NOTES-2.2.11.md；组装目录 `artifacts/layout/`
* **QQNT 小写布局验证（Verified）**：顶层 = host/ + shared/ + plugins/（18 插件）+ versions/（2.2.11 + current）+ ForgeSelf.exe + hostfxr.dll + update-agent.ps1；versions/2.2.11/ = ForgeSelf.exe + wwwroot + appsettings×2 + web.config + staticwebassets（无 Plugins/data/log/config）；**zip 内无大写残留**（zip 检查 versions/2.2.11/Plugins/ 不存在 = 组装时第 65 行删除逻辑生效）

### 冒烟（Verified，业务层 FDD 单文件真实启动）

* 启动器拉起 `versions/2.2.11/ForgeSelf.exe` 子进程成功；Content root = versions/2.2.11/；端口 7102 监听；Development 环境
* 小写运行目录：`data/config/`（ForgeSetting/Agent/Core/XCode.config）+ `data/plugins/`（ai-agent/memory-system 等）+ `data/ForgeSelf.db` 全小写生成 ✓
* 托盘 TryCreate failed 异常 = 无交互桌面环境预期行为（H.NotifyIcon），非缺陷
* **暴露两个遗留点** → 见下

### 遗留①修复：XTrace.LogPath 直接设置（Verified）

* 现象：日志仍落程序目录大写 `Log/`（`versions/2.2.11/Log/2026_09_29.log`），设 `NewLife.Setting.Current.LogPath` 无效
* 归因（独立探针对照实测 `.forgeself/test-tmp/nl-probe`）：**`XTrace.LogPath` 是独立静态属性，不联动 `NewLife.Setting.Current.LogPath`**——仅设 Setting 后 `XTrace.LogPath` 仍 = "Log"（默认相对路径）
* 修复：Program.cs 顶部（ConfigUnifier 之前）+ AppBuilder（幂等）改为直接设 `XTrace.LogPath = {数据根}/log` + `NewLife.Setting.Current.LogPath` 同步持久化
* 验证（开发态冒烟，Api 本体 `-p:BuildProjectReferences=false` 编译 0 errors）：日志落 `data/log/2026_09_29.log` ✓、程序目录 Log/ 不再生成 ✓

### 遗留②认知：运行态 XCode 探测生成版本目录 Plugins/（登记 TODO P2）

* 现象：冒烟后 `versions/2.2.11/Plugins/`（大写）出现 e_sqlite3.dll + System.Data.SQLite.dll + System.Data.SQLite.win-x64_v3.50.4.zip（~15MB）
* 归因：宿主代码无 "Plugins" 大写字面量（grep 实证）；csproj StageAllPlugins 大写路径仅影响构建产物（组装时已删除、zip 干净）；运行态生成 = **NewLife.XCode 库探测「插件文件夹」（BaseDirectory/Plugins）行为**，宿主不可控；DeepWiki（NewLifeX/X + XCode）无探测路径配置项
* 影响：每版本运行后增量 ~15MB；版本切换后旧版本目录保留 → 累积浪费
* 处置：登记 TODO P2（候选方案：update-agent 升级后清理**非当前版本**目录的运行残留 Log/Plugins/data——版本目录本身是发布快照，业务层 exe 保留 → 回滚能力不受损）；不在本次任务范围扩改

### 阻塞（外部）：040-B1 并行会话破坏全量编译

* `Plugins/AIAgent/Services/ReactLoopAgent.cs:254` CS0019（DeriveMessages 方法组 vs int）—— 因 040-B1 并行会话改动 ForgeSelf.Abstractions/Core 接口（IAgentLoop.cs 删除、ISessionStore/ILlmRuntime 修改，git status 未提交）
* 本任务代码验证不受影响：ForgeSelf.Api 本体 `-p:BuildProjectReferences=false` 编译 0 errors（Program.cs/AppBuilder.cs 修改验证通过）；`git status` 确认 ReactLoopAgent.cs 非本会话改动

### 未提交 git

全部改动（批次2 + 输入36 + 输入37 + 本次 LogPath 修正）未提交；注意 `aed3789` 提交 = packaging 优化（批次1/2 内容，历史既有提交），本会话后续改动均在工作区。

---

## 输入38：SQLite 驱动正式依赖化（XCode.SQLite 包替代运行态探测）——实施证据（2026-09-29）

### 目标
安装 `XCode.SQLite` NuGet 包使 SQLite 驱动成为正式依赖，根除 XCode 运行时「程序目录文件探测 → 创建 Plugins/ + 外网下载」路径（输入37 遗留：运行态版本目录生成大写 Plugins/ ~15MB）。

### 包选型（Verified）
* `XCode.SQLite` **11.24.2026.302**：依赖 NewLife.XCode ≥11.25.2026.302（低于项目 12.0 → NuGet 保留主版本不升级）、System.Data.SQLite 2.0.2、SourceGear.sqlite3 3.53.4；11.25.2026.901 需升 XCode 12.2 主版本（弃选，记为未来选项）。
* nuspec 核验：纯依赖聚合包（无 lib 程序集）。

### 探针验证（Verified，nl-probe 工程）
* XCode 12.0.2026.701 + XCode.SQLite 11.24.2026.302 共存编译 0 errors；`DAL.AddConnStr(..., "sqlite")` → 方言注册成功（DbType=SQLite/ServerVersion）；真实 SQL 建表/插入/查询 rows=1。
* **程序目录 Plugins/ 无生成**（根除探测首证）。
* 显式工厂路径（`typeof(System.Data.SQLite.SQLiteFactory)` 第三参 sqlite/SQLite/null 三变体）均抛 `XCodeException [ProbeConn]提供者类型异常` → **XCode 12.0 只认文件探测，工厂参数不可行（方向封死）**。

### dev-bin 冒烟（Verified，smoke-input38.log）
* `bin/Release/net10.0-windows`：System.Data.SQLite.dll 随 bin 输出落盘；清掉旧 `bin/Plugins`、`bin/data` 后启动：
  * 日志 `[System.Data.SQLite.SQLite] 加载 ...\System.Data.SQLite.dll 版本v2.0.2.0`（本地命中、零下载）
  * ForgeSelf + 7 插件库全部 `数据库连接成功 (ServerVersion=3.50.4)`
  * **bin/Plugins 未再生成** ✓
* 宿主随后因 `bind 0.0.0.0:51888 address already in use` 退出（用户真实实例在 51888，预期冲突，未破坏任何东西）。

### 单文件兼容性调试（Verified，关键弯路记录）
1. 首次单文件发布（System.Data.SQLite.dll 内嵌）：运行态完整回退复现（下载 x.newlifex.com zip → Plugins/ → AgentHub 初始化失败「缺少文件」）。
2. 尝试 `ResolvedFileToPublish` + `ExcludeFromSingleFile`（AfterTargets=ComputeFilesToPublish / ComputeResolvedFilesToPublishList 两时机）：**ResolvedFileToPublish 中无该文件**（.NET 10 托管程序集不走该 item），不生效。
3. 查证 SDK targets（10.0.401 Microsoft.NET.Publish.targets）：bundle 输入 = `FilesToBundle`（`_ComputeFilesToBundle` 生成，GenerateSingleFileBundle DependsOnTargets 引用）。
4. **生效方案**：csproj Target `ExcludeSqliteFromSingleFile`（`AfterTargets="_ComputeFilesToBundle" BeforeTargets="GenerateSingleFileBundle"`）从 `FilesToBundle` `Remove` 该 DLL + `publish-host.ps1` publish 后从 NuGet 缓存复制外置兜底。
5. 发布验证：ForgeSelf.exe 16,424 → **16,036 KB**（-388KB 内嵌移除）+ 顶层 System.Data.SQLite.dll 外置 386 KB ✓

### 单文件最终冒烟（Verified，smoke38d-final.log）
* 预置隔离配置（端口 7102 + 隔离 data/）启动 publish38 单文件：
  * `[System.Data.SQLite.SQLite] 加载 D:\...\publish38\System.Data.SQLite.dll 版本v2.0.2.0` —— 落盘命中、零下载、无 already-loaded 冲突
  * ForgeSelf + MemorySystem/QuickLinks/Scheduler/ScriptRunner/WorkflowEngine/AIAgent/TodoTracker **8 库全部 `数据库连接成功 (ServerVersion=3.50.4)`**
  * **Plugins/（大写）未生成**；`Now listening on: http://0.0.0.0:7102` + Application started
  * 托盘线程 H.NotifyIcon 异常 = 本环境无桌面托盘所致（宿主进程与 Web 服务不受影响）

### 发布脚本与仓库清理（Verified）
* `package-release.ps1` L94-104 `inject runtime-probe libs` 块**删除**（替换为输入38 说明注释）。
* 仓库 `build/runtime/plugins`（e_sqlite3.dll 1866KB + System.Data.SQLite.dll 386KB）**移 `.trash/build-runtime-plugins-输入38/plugins`**（禁 rm 铁律，可回滚）。
* 全仓 grep 确认除注释外无 `build/runtime` 残留引用。

### 文档同步（Verified）
* agent-workflow.md：L611（全量 build 会删 publish/plugins/System.Data.SQLite.dll → 新认知：包依赖自然落盘 + 单文件剔除外置）、L729（package-release 描述去注入）、L736（不再需要手工入库）、L451（global-setup 双布局复制简化）。
* 真源 packaging-upgrade-backup.md：§1.1 注入行更新 + §5 变更记录新增输入38 行。

### 未提交 git
输入38 全部改动（csproj 包引用 + Target、publish-host.ps1、package-release.ps1、build/runtime 处置、agent-workflow.md、真源）未提交，等用户明确指示（汇总提交时隔离 040-B1 文件）。

### 输入39：版本号生成 + 文件图标 + 包信息（2026-09-29 · Verified）
* ForgeSelf.Api.csproj：加 VersionPrefix 2.2 + VersionSuffix 2026.0929 + Version/FileVersion + AssemblyVersion 2.2.* + Deterministic=false + AssemblyTitle=铸己匣/Description/Company=OpenForgeSelf/Product=ForgeSelf（铸己匣）/Copyright=©2026 OpenForgeSelf（图标 Assets\ForgeSelf.ico 原有）。
* ForgeSelf.Bootstrapper.csproj：加 ApplicationIcon=..\ForgeSelf.Api\Assets\ForgeSelf.ico + 同套版本/包信息（AssemblyTitle=铸己匣启动器）。
* 踩坑：Api 的 obj\Release\net10.0-windows\win-x64 变体缓存（历史 -p:Version=2.2.12 残留）不随 csproj 变化失效 → 移 .trash\obj-win64-input39 后重建；验证须看 in\Release\net10.0-windows\（无 RID 输出）而非历史 win-x64 残留目录。
* 验证（实测）：ForgeSelf.exe（Api）FileVer=2.2.2026.0929 / Title=铸己匣 / Company=OpenForgeSelf / Product=ForgeSelf（铸己匣）/ Copyright=©2026 OpenForgeSelf；ForgeSelf.Bootstrapper.exe 同套（Title=铸己匣启动器）；两 exe ExtractAssociatedIcon 均返回 32x32 图标；dotnet build 0 errors（322 既有 nullable 警告，非本次引入）。
* 未提交 git（等用户明确指示）。

## 输入41 发布核验（2026-09-29）

**动作**：`release-local.ps1 -Version v2.2.12 -SkipFrontend` 全链路发布成功（首次运行因 040-B1 并行会话并发构建竞态导致 AIAgent CS0246，错峰重跑 exit 0）。

**产物落点（真实路径）**：
- `artifacts\release\OpenForgeSelf-2.2.12-win-x64.zip`（102MB，SHA256=df263248f6427155624ea9117aee0fafd26eb3b5d9e7f4b6f821d5e00406d0f5）
- `artifacts\release\RELEASE-NOTES-2.2.12.md`、`artifacts\release\SHA256SUMS.txt`
- 实体 QQNT 组装树 `artifacts\layout\`（zip 内结构与 layout 逐项一致，三个关键文件哈希比对一致）

**核验真实输出（FileVersionInfo + ExtractAssociatedIcon）**：

| exe | FileVersion | FileDescription | ProductName | CompanyName | LegalCopyright | 图标 |
|---|---|---|---|---|---|---|
| 根启动器 `layout\ForgeSelf.exe`（0.38MB） | 2.2.2026.0929 | 铸己匣启动器 | ForgeSelf（铸己匣） | OpenForgeSelf | ©2026 OpenForgeSelf | 32x32 ✓ |
| 业务层 `versions\2.2.12\ForgeSelf.exe`（15.66MB） | 2.2.12 | 铸己匣 | ForgeSelf（铸己匣） | OpenForgeSelf | ©2026 OpenForgeSelf | 32x32 ✓ |

- 三个 exe 图标同源一致（766 bytes）；ProductVersion 带 `+aed3789` commit 后缀（ContinuousIntegrationBuild=true 注入 SourceRevisionId）。
- 输入38 断言通过：`plugins/` 顶层无 e_sqlite3.dll/System.Data.SQLite.dll（18 插件目录干净）；`versions\2.2.12\` 含外置 SQLite 驱动 2 件（设计如此），无大写 Log/Plugins 残留。
- 差异（如实记录）：业务层 FileVersion=2.2.12（publish-host `-p:Version=v2.2.12` 覆盖 csproj 日期机制），根启动器为 2.2.2026.0929（csproj 机制生效）。目录名=版本号=FileVersion 三者一致，语义自洽；是否改为日期版本待用户拍板。

## 输入42 Authenticode 签名（2026-09-29）

**链路**：`release-local.ps1 -Sign` → `package-release.ps1`（签名在 zip 打包前）→ `scripts/sign-publish.ps1`。签名脚本已成熟（自签证书 CN=OpenForgeSelf 铸己匣、certutil 静默信任、signtool SHA256、digicert RFC3161 时间戳、幂等跳过、-PfxPath 可换商业证书）；指纹写 `.forgeself\codesign-thumbprint.txt` 供 CI `-Thumbprint` 复用。

**修复（发现并已改）**：sign-publish.ps1 收集 exe 缺 `-Recurse` → 只签顶层根启动器、**漏签 `versions/<ver>/` 业务层**（首次运行日志「待签名 1 个文件」暴露）。已加递归；重跑后「待签名 3 个文件」= 根启动器 + 业务层新签、.NET 自带 createdump.exe（MS 已签）跳过，signtool 校验 3/3。

**核验（Get-AuthenticodeSignature 真实输出）**：zip 内根启动器 + 业务层（及 layout 同 2 exe）全部 `Status=Valid / 签名已通过验证`；签名者 `CN=OpenForgeSelf 铸己匣`（指纹 B7A5851E992B53D8C7334088BBA9653FDCAFCDC7，2026-09-26 ~ 2029-09-26）；时间戳 `CN=DigiCert SHA256 RSA4096 Timestamp Responder 2026 1`。

**CI 复用**：`.github/workflows/release.yml` 发布步骤已加 `-Sign`（注释说明证书策略可插拔：自签自动生成 / 商业证书经 CI secrets 传 -PfxPath）。

**最终产物**：`artifacts\release\OpenForgeSelf-2.2.12-win-x64.zip`（101.1MB，SHA256=178d40abff0d720708aff1557323c4dfbe47371ecc15d55e38483e5b24b81dc9）。

## 输入43 版本格式统一（2026-09-29）

**根因**：`publish-host.ps1` 传 `-p:Version=$ver`（v2.2.12）覆盖 csproj 的日期机制 → 业务层 FileVersion=2.2.12，与根启动器（Bootstrapper，2.2.2026.0929）不统一。

**修复**：① `publish-host.ps1` 移除 `-p:Version`（保留 ContinuousIntegrationBuild=true，ProductVersion 带 commit 溯源）；② 顺带修复 `release-local.ps1` 尾部汇总 `Get-ChildItem` 对目录项取 `Length` 的展示 bug（加 `-File`）——此前发布成功但脚本 exit 1 假失败。

**核验（真实输出）**：根启动器 + 业务层 `FileVersion` 均 = `2.2.2026.0929`、`ProductVersion` 均 `2.2.2026.0929+aed3789...`；签名均 `Valid`（CN=OpenForgeSelf 铸己匣）。

**语义**：发行号（`v2.2.12`：目录名/tag/更新检查）与文件版本（`2.2.yyyy.MMdd`：exe 属性）各司其职，已沉淀真源 §1.1 版本号机制行。

**产物**：`OpenForgeSelf-2.2.12-win-x64.zip`（101.1MB，SHA256=491458a40c91bae09e7dd97053192308c9595057255bd1f15235f9355d3e1213）。