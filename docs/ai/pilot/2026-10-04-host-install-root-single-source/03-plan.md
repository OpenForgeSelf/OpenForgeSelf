# Plan

任务：`2026-10-04-host-install-root-single-source`｜状态：**已实施（2026-10-04 19:3x–19:5x）**，中档全量在跑，包待出。

## Files To Change（实际落地）
| 文件 | 动作 | 改了什么 |
|---|---|---|
| `scripts/release/package-release.ps1` | 修改 | 第 3 步取消"复制 `plugins/` 到安装根 + `Remove-Item versions/<ver>/plugins`"，改为**保留随版本**；缺 `versions/<ver>/plugins` 时 `throw`；sanitize 段补清版本目录内 `plugins/_backups`；头部布局注释同步更正（`plugins/` 从"install root"移到 `versions/<ver>/` 一层） |
| `ForgeSelf.Api/Plugins/PluginManager.cs` | 修改 | 新增字段 `_pluginRoots`（有序根）；`SetPluginsDirectory` 重置并填入第一路；新增 `AddPluginRoot(string?, source)`（同路径忽略大小写去重、null 安全、日志带"存在=False"）；新增只读 `PluginRoots`；`DiscoverPlugins()` 改为逐根合并扫描 + 同 Id 版本号裁决（覆盖时 WARN）+ 每条发现带来源 + 完成行汇总根数与列表；缺根由"早退"改为"WARN 后继续下一路"；新增 `internal static ComparePluginVersions`（不可解析⇒0） |
| `ForgeSelf.Api/AppBuilder.cs` | 修改 | 插件根注释重写为"两路"；`SetPluginsDirectory(pluginsPath)` 之后追加 `AddPluginRoot(Path.Combine(dataLocation.GetHostDataDirectory(), IDataLocationService.PluginDataRootName), "数据目录")`；覆盖通道与 `PluginsDirectory` 语义保持 |
| `ForgeSelf.Api.Tests/Plugins/PluginRootsTests.cs` | 新增 | AC1–AC7 全部（两路各一插件 / 数据根更高版本生效 / 相同版本保留内置 / 只有 `*.db` 的数据子目录不当插件（阳性对照）/ 内置根缺失仍可加载 / 重复追加只留一路 / 版本比较 5 段 Theory） |
| `ForgeSelf.Api.Tests/RepositoryScriptTests.cs` | 修改 | 新增 `PackageRelease_MustKeepBundledPluginsInsideVersionDirectory`：禁字面量 `Remove-Item (Join-Path $versionDir 'plugins')` 与 `$layoutPlugins`，要求出现 `Test-Path (Join-Path $versionDir 'plugins')` 与 `versions/<ver>/plugins` 文案，并带"脚本文件 >1000 B"阳性对照 |
| `docs/04-standards/packaging-upgrade-backup.md` | 修改 | §1.6 ③（插件层＝两路：内置随版本 + 数据目录）与 ④（数据根 `plugins/` 双语义，靠 `plugin.json` 区分）；启动链路那句改"两路合并扫描"；§1.1 新增「内置插件落位」行；变更记录新增一行（含"不是 update-agent 剪切"的查证） |
| `AGENTS.md` / `.agents/skills/**`（本任务相关段） | 待办 | 见 02-spec U1–U3 与 TODO：技能里"升级后插件消失"排障入口尚未写，本轮先把真源与代码落稳 |

**未改**（刻意留给下一批）：`update-agent.ps1`、`PluginInstallerService` / `PluginVersionService` 的写入位置、`PluginFrontendFileMiddleware`、`DevController`、`ForgeSelf.Bootstrapper`、`Plugins/**` 全部插件本体、任何数据库面。

## Implementation Steps（按实际执行顺序）
1. 读代码定位真凶：`package-release.ps1:54-65`（外置 + 删除）与 `AppBuilder.cs:319-320`（扫业务层旁边）；排除 `update-agent.ps1` 嫌疑（`robocopy /E` 只复制）。
2. 改发布布局：保留 `versions/<ver>/plugins/` + 缺目录 throw + sanitize 补漏 + 头部注释。
3. 改 `PluginManager`：有序根列表 + `AddPluginRoot` + 两路合并 + 版本号裁决 + 来源日志 + 缺路不早退。
4. 改 `AppBuilder`：接入数据目录那一路（常量用 `PluginDataRootName`，不写字面量——agent-workflow §B12 的"禁止两侧各写字面量"）。
5. 写常驻判据：`PluginRootsTests`（AC1–AC7）+ 布局守卫（AC8）。
6. **反向探针**（AC8 的"会响"证明）：把旧写法临时插回 → 守卫红；还原 → 复跑绿（读数见 05）。
7. 中档全量 `dotnet test`（串行，TMP 固定）+ design-system 插件层 e2e 回归。
8. 出包：`pwsh … release-local.ps1 -Version 2.7.3 -Sign -UpdateDir …` → **按包内容**验真（`versions/2.7.3.*/plugins/DesignSystem/plugin.json` = 3.1.0）。
9. 文档收口：真源已改；技能排障入口与 AGENTS 一句引用待补（记 TODO）。

## Test Plan
- 单测：`PluginRootsTests`（两路合并、版本裁决、数据目录安全、缺路可用）＋ `RepositoryScriptTests`（BOM、git 码页、发布布局）＋既有 `PluginManager` / `PluginFrontendFileMiddleware` / `PluginDependencyIntegrationTests` 走 `SetPluginsDirectory` 单根路径 ⇒ 必须零回归。
- 中档：后端全量 `dotnet test`（先对基线再判责）。
- 端到端：design-system 插件层 e2e 串行（判据不放宽）；"按真实 QQNT 布局起真实宿主"的常驻用例**下一批补**（本轮缺陷能溜到现场的直接原因）。

## Verification
### Build
```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug                       # exit 0
dotnet build ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -c Debug           # exit 0
```
告警按文件归因：`PluginManager.cs` 本次 0 新增告警；`AppBuilder.cs` 只剩既有 CS0618（:484 Scalar 过时 API，非本次改动）。

### Unit Test
```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~PluginRootsTests|FullyQualifiedName~RepositoryScriptTests"
dotnet test ForgeSelf.Api.Tests            # 中档全量
```
判定：过滤集 **失败 0 / 通过 32 / 总计 32**（`logs/test-pluginroots.log`、`logs/test-pluginroots-after-revert.log`）；全量以日志正文汇总为准（不看 exit code）。

### Integration Test
N/A（本任务不涉跨插件事件/契约接缝；宿主内根解析由单测 + 端到端覆盖，依据见 02-spec Compatibility）。

### E2E
```bash
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system --workers=1
```
判定：不新增稳定红；红项按原文归因（G19 撞锁等既有项照旧登记）。

### Other Checks
```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-04-host-install-root-single-source
```

## Plan 偏差记录
| 时间 | 偏差点 | 原 Plan | 修正后 |
|---|---|---|---|
| 2026-10-04 19:2x | **方案方向错**：第一版 00–04 写的是"新增 `InstallRootResolver`，从 `ProcessPath` 上溯找**安装根**锚点 + 新增 `FORGESELF_HOME`" | 我以为缺陷在"运行期猜错根" | 用户当场纠正：内置插件的家是**每个版本目录里的** `plugins/`，绝不是安装根。方案改为"发布侧随版本 + 运行侧两路合并"，**未写一行解析器代码**；00–04 已按实态重写，不留被推翻的设计当结论 |
| 2026-10-04 19:3x | 用户第三问把怀疑点引向"更新代理是否剪切" | — | 查证并写进真源：`robocopy /E` 只复制；掏空版本目录的是打包脚本第 3 步。归因从"运行时"改到"发布时" |
| 2026-10-04 19:4x | 我在并发下又发起一次 `dotnet test`（前一条全量仍在跑） | dotnet 严格串行 | 该次日志以 MSB3026 重试 → MSB3021/3027 结束 ⇒ **该日志作废、不计入判据**；等全量结束后串行重跑（同一条规则今天已因别人踩过一次，我自己又踩，记此） |
| 2026-10-04 19:5x | 中途一次把全量汇总数字读成"失败 41 / 通过 1720 / 1m32s" | 报数前必须回读日志正文 | 该读数来自 GBK 控制台错位，日志里当时根本没有汇总行 ⇒ **撤回，不作证据**；以完成后 `logs/full-test-hostroot.log` 的汇总原文为准 |

---

## 第二批 Files To Change（实际落地）

| 文件 | 动作 | 说明 |
|---|---|---|
| `ForgeSelf.Api/Services/HostInstallRoot.cs` | **新增** | 安装根唯一解析口径（`Resolve` / `VersionLayerCount` / `ResolveFromExecutable`），纯函数、无 IO |
| `ForgeSelf.Api/Services/StagedUpdateService.cs` | 改 `ApplyStaged` 取根段 | `-InstallDir` 改传归一化安装根；layers>1 WARN 点名嵌套层数，否则 INFO 记三处路径；代理脚本回退查找路径随之落到公共层（旧写法在 QQNT 布局下永远找不到回退件） |
| `scripts/update-agent.ps1` | **新增** `Resolve-ForgeInstallRoot` + 步骤 0 | 入口归一化 `$InstallDir` 并写日志；归一化后父目录仍为 `versions` ⇒ `throw` 在任何写盘动作之前；头部布局注释更正（内置插件在 `versions/<ver>/plugins/`） |
| `scripts/release/package-release.ps1` | 改注释 | 「安装根 plugins 只作为历史布局兜底读取」是错的（用户裁定只保持两路），改为"两路 + 安装根不再扫描" |
| `ForgeSelf.Api.Tests/Services/HostInstallRootTests.cs` | **新增** 13 条 | 纯函数 8 组输入 + exe 路径解析 + null/空即抛 + **实跑 pwsh 比对两侧** + 源码接线守卫 |
| `docs/04-standards/packaging-upgrade-backup.md` | 改 §1.5 两处过期事实 + 新增 §4-R11 + 变更记录一行 | §1.5 原文仍写"插件目录 = 安装根 /plugins 单路"，与输入18 裁定冲突，一并更正 |

## 第二批 Implementation Steps
1. 读码定因（`StagedUpdateService.cs:226` → `update-agent.ps1:83-89`）后先写纯函数与其 8 组输入判据。
2. 宿主接线（`ApplyStaged` 取根 + 日志）。
3. 代理侧同源实现 + 硬拦（顺序很重要：代理是"老宿主升级那一次"唯一能生效的位置）。
4. 两侧同源判据（抽出脚本函数体、`pwsh` 实跑比对）＋源码接线守卫。
5. 反向探针（两处同时破坏 → 两条守卫实红 → 还原复绿）。
6. 文档与真源收口（§1.5 过期事实、§4-R11、变更记录）。

## 第二批 偏差记录
- **偏差 1（判据自身先红一轮）**：`与update_agent脚本的归一化规则一致` 首跑红——原因在**我的提取逻辑**不是实现：`update-agent.ps1` 是 CRLF，找 `"\n}\n"` 取不到函数体。归一换行后复绿（`logs/test-hostinstallroot2.log`，13/13）。教训已体现在测试内注释：随包脚本要求 CRLF+BOM，按 LF 切分前必须先归一。
- **偏差 2（写文档时重演了转义吃字符老错）**：给真源追加变更记录行用 python 非 raw 串写 `versions\2.2.11\versions\…`，`\2`→STX、`\v`→VT 把路径烤成 `versions.2.11ersions…`；靠"写完必扫控制字符"逮住，用 `chr(92)` 逐段 join 重写，全文复扫 0 控制字符。
- **偏差 3（Edit 锚点吃标题）**：给真源 §4 加 R11 时把下一节标题 `## 5. 实施改动清单` 一起换掉了（old_string 圈了下一节标题）。已补回并复核 `^## ` 节数（前后均 8）。这两条都是既有铁律（`feedback-edit-anchor-minimal` / `feedback-cjk-quotes-break-scripting`）的再犯，不是新规律，故不另开文档、只在此登记。
- **未做（刻意）**：`UpdateService._appDir`（008 已冻结、无生产调用者）不动；存量嵌套目录清理不动（不可逆，需用户在场）。
