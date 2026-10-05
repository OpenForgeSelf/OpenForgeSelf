# Repository Understanding

任务：`2026-10-04-host-install-root-single-source`（现名与内容按用户裁定改为**插件根两路**；本目录 00–04 的第一版写的是"上溯找安装根锚点"，**已被用户当场推翻**，重写留痕见 03-plan 偏差记录与本文末节）。时刻：2026-10-04 19:5x，HEAD `d88d709`。

## 项目结构
- `ForgeSelf.Api/`：.NET 10 + ASP.NET Core 业务层宿主（Kestrel + wwwroot + 插件 ALC 加载 + XCode/SQLite）。
- `ForgeSelf.Bootstrapper/`：根启动器薄壳，读 `versions/current` 拉起 `versions/<ver>/ForgeSelf.exe`。
- `ForgeSelf.Web/`：Vue 3 SPA + `e2e/`（应用层与插件层同一套 Playwright）。
- `ForgeSelf.Api.Tests/`：xUnit（含仓库级静态守卫 `RepositoryScriptTests.cs`）。
- `Plugins/<X>/`：18 个插件源码工程（dll + plugin.json + web/dist）。
- 结构事实真源：`docs/04-standards/packaging-upgrade-backup.md`（§1.1 发布链、§1.6 程序分层、§4-R10 版本号与世代）。

## 技术栈
.NET 10 / ASP.NET Core / NewLife.XCode（唯一 ORM）/ SQLite；Vue 3.5 + Vite + pnpm；`dotnet test` + vitest + Playwright；发布链 `scripts/release/*.ps1`（执行一律 `pwsh` → AGENTS §2.3）。

## 架构特点（与本次缺陷直接相关的四条，均带出处）
1. **布局规范（真源 §1.6）**：公共层在安装根（根启动器 + `host/` + `shared/` + `update-agent.ps1`）；业务层在 `versions/<ver>/`；数据层随人走（`~/.forgeself`，可被 `FORGESELF_DATA_ROOT` 重定向）。
2. **运行期插件根取自业务层自己的位置**：`ForgeSelf.Api/AppBuilder.cs:319-320` → `DevMode.PluginsDirectoryOverride ?? Path.Combine(AppContext.BaseDirectory, "plugins")`；覆盖通道 CLI `--plugins-dir` ＞ env `FORGESELF_PLUGINS_DIR`（`Plugins/Dev/DevMode.cs:34/38/94/118`，不受 dev 总闸限制）。
3. **publish 阶段插件本来就落在版本目录里**：`scripts/release/publish-host.ps1:82`（`$pluginDir = Join-Path $OutputDir 'plugins'`，实测 `plugins/AIAgent/AIAgent.dll` 579,072 B 随产物走）。
4. **但打包组装把它们掏空了**：`scripts/release/package-release.ps1:54-65`（旧版）把 publish 的 `plugins/` 复制进**安装根**，随后 `Remove-Item (Join-Path $versionDir 'plugins')`，注释自陈「业务层不再携带插件目录（插件公共外置，避免每版本复制）」。
   ⇒ 2 与 4 合起来＝**发布包与运行期解析分叉**：业务层旁边没有 `plugins/`，`PluginManager.DiscoverPlugins()` 早退（旧代码在目录不存在时 `return`），现场读数 `GET /api/plugin` → `data:[]`。
5. **更新代理不背这个锅（实测）**：`scripts/update-agent.ps1:85/89` 只做 `robocopy /E`（复制）——`versions/<newVer>` 落版本层，再把公共层合并进安装根（`/XD versions`）；全程无剪切。
6. **既有"根解析"分散**：插件根（`BaseDirectory/plugins`）、数据根（`DataLocationService.cs:34-42/66-70`）、日志根（`AppBuilder.cs:87-88` 由数据根派生）各算各的；`IDataLocationService.PluginDataRootName = "plugins"` 已经占用数据根下的同名目录放**插件数据**（`~/.forgeself/plugins/{插件Id}/{连接名}.db`，实测 12 个子目录里只有 `*.db`，无 `plugin.json`）。

## 测试方式
`dotnet test ForgeSelf.Api.Tests [--filter …]`（跑前 TMP/TEMP 固定 `.temp/ds-m1/tmp`，**dotnet 严格串行**——本轮亲测并发会 MSB3021/3027 锁死，日志作废）；`cd ForgeSelf.Web && pnpm run check && pnpm run test`；e2e 走 `e2e-testing`（globalSetup 自建 publish 宿主、零 mock）；仓库级静态守卫先例 = `RepositoryScriptTests.cs`。

## 构建命令
```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~PluginRootsTests|FullyQualifiedName~RepositoryScriptTests"
dotnet test ForgeSelf.Api.Tests                      # 中档全量（碰宿主源码必跑）
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system --workers=1
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/release-local.ps1 -Version 2.7.3 -Sign -UpdateDir D:\src\my-proj\OpenForgeSelf\updates
```

## 主要目录职责
`artifacts/publish`（业务层 publish，含 `plugins/`）→ `artifacts/layout`（QQNT 组装）→ `artifacts/release/*.zip` + `SHA256SUMS.txt` + `RELEASE-NOTES-*.md`；本地更新源 `D:\src\my-proj\OpenForgeSelf\updates`。

## 代码组织方式
`AppBuilder`（静态引导、根接线）→ `Plugins/PluginManager`（扫描/加载/生命周期）→ `Plugins/PluginVersionLayout`（插件侧 `versions/<ver>/ + current`，已是正确范式）→ `Plugins/Services/PluginVersionService`/`PluginInstallerService`（安装与热切换，写入位置仍以 `PluginManager.PluginsDirectory` 为准，**本批未改**，见 02-spec 待办项）。

## 现有工程规范
真源 §1.6（一层一个程序）、§4-R10 ⑦（世代比较/混代包会判成"有更新"而**降级**）、AGENTS §2.3（`pwsh` 口径）、§5.6（**碰宿主源码＝中档＝后端全量**）、agent-workflow §B6（PS 码页与转义坑）。

## 现场事实（本任务立项依据，全部实测）
- 用户实例 `:51888` 已升到 `2.7.2.0`（PID 47228 根启动器 + PID 84140 业务层，路径 `…\versions\2.2.11\versions\2.2.2026.0930\versions\2.7.2.0\ForgeSelf.exe`）。
- 该实例 `GET /api/plugin` → `{"data":[]}`（0 个）；`/api/plugin/detail/design-system` → 404；宿主日志 17:32:56 原文 `插件目录设置为: …\versions\2.7.2.0\plugins` → `插件目录不存在`。
- 用户截图页面显示"已安装 1 个 / 显示版本 1.2.1"＝**重启前旧进程**从外层旧扁平根 `D:\src\tools\ForgeSelf\plugins\DesignSystem`（1.2.1 + 4096 B 桩 DLL）读到的；中间层 `…\2.2.2026.0930\plugins\DesignSystem` 已被 17:32 的更新写成 3.1.0 / 1,043,456 B（说明"包里有插件"一直是事实）。
- 包内容实测：806 条目，`plugins/` 在安装根一层、`versions/2.7.2.0/` 内**无 plugins**；`plugins/DesignSystem/plugin.json` = design-system 3.1.0。

## 候选低风险任务
临时绕法（常驻设 `FORGESELF_PLUGINS_DIR` 指向中间层 `plugins`）已由**用户明确否决**（「我不要临时解决」），只作为历史读数解释保留，不作为交付路径。

## 选择该任务的原因
一处"发布布局与运行期解析的分叉"会让每次版本升级都可能复现"整台实例插件消失"，且当前所有门禁都在扁平/dev 形态下跑（所以从来没抓到）。修它 = 消除一类缺陷 + 补上按真实布局的常驻判据，而不是只让那一台实例好看。
