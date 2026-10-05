# Evidence

任务：`2026-10-04-host-install-root-single-source`（插件根两路 + 内置插件随版本）｜时刻：2026-10-04 20:1x｜HEAD `d88d709`（并行会话提交）。
分级口径：**Verified**＝本会话亲自跑过并留日志原文；**Inferred**＝据代码/文件推断；**Unknown**＝未验证。

## Task
消除"发布包外置插件 / 运行期只扫业务层旁边"的分叉；内置插件改为随版本落 `versions/<ver>/plugins/`；宿主支持第二路数据目录插件根；两件事各钉常驻判据。

## Changed Files（本任务净增，不含同日 M3 收尾改动）
| 文件 | 动作 |
|---|---|
| `ForgeSelf.Api/Plugins/PluginManager.cs` | 改：有序插件根 `_pluginRoots`、`AddPluginRoot(dir, source)`、只读 `PluginRoots`、`DiscoverPlugins()` 两路合并 + 同 Id 版本号裁决 + 来源日志 + 缺路不早退、`internal static ComparePluginVersions` |
| `ForgeSelf.Api/AppBuilder.cs` | 改：第二路接线（`{数据根}/plugins`，常量 `IDataLocationService.PluginDataRootName`）+ 注释重写；覆盖通道与 `PluginsDirectory` 语义未动 |
| `scripts/release/package-release.ps1` | 改：取消"外置到安装根 + 从版本目录删除"，改保留随版本 + 缺目录 `throw`；sanitize 补清 `versions/<ver>/plugins/_backups`；头部布局注释更正 |
| `ForgeSelf.Api.Tests/Plugins/PluginRootsTests.cs` | 新增：7 条实路用例 + 5 段版本比较 Theory |
| `ForgeSelf.Api.Tests/RepositoryScriptTests.cs` | 改：新增发布布局守卫一条 |
| `docs/04-standards/packaging-upgrade-backup.md` | 改：§1.6 ③/④、启动链路句、§1.1「内置插件落位」行、变更记录一行 |
| 本 pilot 00–07、`TODO.md`、`.forgeself/memory/2026-10-04.md` | 改：按实态（含被推翻的第一版方案留痕） |

## Build
```
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug            → exit 0（logs/build-hostroot.log）
dotnet build ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -c Debug → exit 0（logs/build-tests-hostroot.log；首轮因缺 using 报 1 error，见下"过程账"）
```
- 告警按文件归因：**Verified** —— `PluginManager.cs` 本次 0 新增告警；`AppBuilder.cs` 仅剩既有 CS0618（:484 Scalar `WithDarkModeToggle`，与本次无关）。

## Unit Test
- **Verified**：`dotnet test --filter "FullyQualifiedName~PluginRootsTests|FullyQualifiedName~RepositoryScriptTests"`
 → `已通过! - 失败: 0，通过: 32，已跳过: 0，总计: 32`（`logs/test-pluginroots.log`；探针还原后复跑同样 32/32，`logs/test-pluginroots-after-revert.log`）
- **Verified（反向探针，AC8 的"会响"证明）**：把被禁写法 `Remove-Item (Join-Path $versionDir 'plugins')` + `$layoutPlugins` 临时插回 `package-release.ps1` → 同过滤集 **`失败: 1，通过: 20，总计: 21`**，失败即 `RepositoryScriptTests.PackageRelease_MustKeepBundledPluginsInsideVersionDirectory`（断言消息点名"不得从 versions/<ver>/ 删除内置插件目录"）→ 还原 → `grep PROBE=0 / $layoutPlugins=0 / BOM 仍在` → 复跑绿（`logs/utf8guard-probe2.log`）。
- 过程账（我自己的两次错，原文留档）：① 新测试首次编译报 `CS0246 IPermissionChecker` —— 缺 `using ForgeSelf.Api.Plugins.Abstractions;`，补后通过；② 全量在跑时我又并发发起一次 `dotnet test`，该日志以 `MSB3026` 十次重试 → `MSB3027/MSB3021`（`testhost (87420)` 锁 `ForgeSelf.dll`）收场 ⇒ **该日志作废、不计入判据**（这条规则今天由别人踩过、我复踩了一次）。
- **Unknown / 进行中**：中档全量 `dotnet test ForgeSelf.Api.Tests`（后台 `logs/full-test-hostroot-2.log`；第一份 `full-test-hostroot.log` 自 19:48 停止增长、无汇总行 ⇒ 不作判据）。已确认的既有红：`WorkflowPlanningIntegrationTests` 6 条 **`404 NotFound`**，实测归因＝测试输出目录 `ForgeSelf.Api.Tests/bin/Debug/net10.0-windows/plugins/AIAgent/` 里**只有 `AIAgent.dll`、没有 `plugin.json`** ⇒ 插件不被发现 ⇒ 插件路由 404；**Inferred**：与本次改动无关（我未触碰任何插件清单拷贝逻辑），待全量汇总出来后按"新增红才是我的"逐条对表确认。

## Integration Test
N/A —— 依据：本任务只改宿主内根解析与打包布局，不涉跨插件事件/契约接缝；覆盖走单元 + 端到端（03-plan Verification 已记）。

## E2E
- **Unknown（待跑）**：`e2e/plugins/design-system --workers=1` 回归 —— 本轮改了 `PluginManager` 扫描，插件层 e2e 是真正能证"升级后插件还在"的一档；须在全量结束后串行跑（不可并发，SQLite 同库）。
- **已知缺口（下一批）**：没有一条"按真实 QQNT 布局组装 → 起真实宿主 → 断言 `/api/plugin` 非空且版本==包内清单"的常驻用例；这次缺陷能溜到用户现场，缺的就是这一条。

## Static Analysis
- 布局守卫为静态扫仓库文件型（`RepositoryScriptTests` 同族）：含**阳性对照**（脚本 >1000 B + 必须出现 `Test-Path (Join-Path $versionDir 'plugins')`），避免"扫不到文件也报绿"。
- 全库无自拼插件根的新增写法：`PluginOptions.PluginsDirectory` 的默认值仍在 `ServiceCollectionExtensions.cs:53`（`AddPluginManager` 未显式传参时的兜底，属既有路径），与本次改动不冲突 —— **Verified（grep 实读）**。

## Screenshots
本任务无新增界面，故无截图；用户现场截图（设计系统 1.2.1 / 运行路径）已按原文记入 `.forgeself/memory/2026-10-04.md` 输入14/15/18。

## Known Limitations
1. 插件**安装/更新写入位置**仍指内置根（`PluginsDirectory`）⇒ 更新会写进本应随版本不可变的目录；数据根那一路目前只读。下一批迁移，含热切换与 stage 目录。
2. 存量混代实例（外层 `D:\src\tools\ForgeSelf\plugins` 1.2.1 + 4096 B 桩、中间层 3.1.0）不在本次扫描点内；历史目录一律未删未改。
3. 首次安装的目录形态改变（`plugins/` 从安装根进版本目录），老布局的"覆盖安装"若仍有安装根 `plugins/`，不会被新逻辑读取。
4. `PluginFrontendFileMiddleware` / `DevController` 的根读数尚未跟随两路（只影响插件前端资源与 dev 面板的极端场景）。

## Unresolved Issues
1. 中档全量汇总未落地（进行中）；design-system 插件层 e2e 未跑；带修复的宿主包未出（`release-local -Version 2.7.3 -Sign -UpdateDir`，等全量绿后串行跑）。
2. 版本串 `2.7.3` 的选择待用户认可（理由：包内目录结构变化 + 必须大于用户已装的 `2.7.2.0`）。
3. 提交/打 tag 一律未做（等用户字眼）；本任务与 M3 的待提交集合需在闸门3 现测重数（HEAD 会被并行会话推进）。
4. U2：用户那台实例升级到新包后能否完整恢复，需一次真机只读复验才能定案。

---

## 第二批证据（输入19 · 版本目录逐代嵌套，21:0x）

### Verified（实测，日志正文读数）
- `dotnet test --filter "FullyQualifiedName~HostInstallRootTests"` → **失败 0 / 通过 13 / 总计 13**（`logs/test-hostinstallroot2.log`，含实跑 `pwsh` 的两侧同源比对用例，该条耗时 928ms～2s）。
- `dotnet test --filter "HostInstallRootTests|PluginRootsTests|RepositoryScriptTests|StagedUpdate"` → **失败 0 / 通过 51 / 总计 51**（`logs/test-nest-related.log`，正文行「已通过! - 失败: 0」）。
- **反向探针（AC14）**：同时破坏两处——代理 `Resolve-ForgeInstallRoot` 改成 `if ($true) { break }`（不上跳）＋宿主 `installDir` 退回 `Path.GetDirectoryName(Environment.ProcessPath)` ⇒ **失败 2 / 通过 11 / 总计 13**（`logs/test-hostinstallroot-probe.log`），红的是 `与update_agent脚本的归一化规则一致` 与 `更新链路必须用安装根解析而非业务层目录` 两条；还原后 `grep -c PROBE` = 0（两文件）并复跑绿。
- 脚本自身完好性：`scripts/update-agent.ps1` **BOM 仍在、CRLF 232 行、bare LF 0、控制字符 0**，且含 `Resolve-ForgeInstallRoot` 与 `拒绝产生逐代嵌套` 两处新件（python 字节级实测）。

### 两轮全量对比（同一批插件根改动，第二批改动只在 B 轮）
| 轮 | 时间与体量 | 失败 9 的构成 |
|---|---|---|
| A（`full-test-hostroot.log`） | 19:45→20:17，**失败 9 / 通过 2334 / 已跳过 0 / 总计 2343 / 32m 2s** | `ScriptRunnerDi`(1) + `WorkflowPlanning`(6) + `DesignSystem.DesignAgentToolContractTests.ExecuteAsync_空参数_不抛`(1) + `PersistentSessionStoreTests.DeriveMessages_Roundtrip`(1，原文 `IOException …ForgeSelf.db being used by another process`) |
| B（`full-test-nesting.log`） | 20:52→进行中 | `ScriptRunnerDi`(1) + `UpdateServiceTests.ApplyUpdateAsync_*`(8，全部 `Expected result to be True, but found False`)；**A 轮的 6 条 WorkflowPlanning、DesignSystem、PersistentSessionStore 本轮全绿** |

- 读表结论（**Verified 的部分**）：两轮红的集合几乎不相交、且 B 轮 A 轮各有对方没有的红 ⇒ 这批红**不是"我改一次就多 9 条"的单调新增**；`ScriptRunnerDi` 两条轮轮都在（归因已定：测试输出目录 `plugins/AIAgent/` 缺 `plugin.json`）。
- **Inferred（待现场复现坐实，不写成结论）**：B 轮 8 条 `UpdateServiceTests` 集中在 `00:01:08–00:01:09` 一秒内、且 `%LOCALAPPDATA%\UpdateServiceTestApp\Backups` 里**今天只有 20:14/20:15 三条**（属另一会话 20:1x 那轮）⇒ 本轮这些用例在**步骤 5「建备份目录」之前**就返回 false，即失败点在 下载/校验/解压/停服务 段；本仓测试 TMP 固定在 `.temp/ds-m1/tmp`，而该目录同时被并行会话使用（其日志里 `ForgeSelfSession_*`、`test_plugins_*` 就落在这里），"外部清理把在飞的临时目录删掉"是当前最合理解释。**处置**：全量结束后单独 `--filter UpdateServiceTests` 复跑；绿则登记为"并发/共享 TMP 下的偶发红"并入 TODO（不是我的新增红，也不是可以一句"无关"带过的红）。
- **Unknown**：B 轮全量汇总（进行中）；design-system 插件层 e2e（本批未跑，第二批改动不触插件层）；带两处修复的宿主包（等全量收口后串行出）。

### 第二批判据的自陈局限
1. `HostInstallRoot` 是纯字符串归一化，**不验证上跳后的目录真是安装根**（无锚点探测）：按用户裁定"只保持两路"，不引入安装根兜底扫描，也不新增锚点判定。
2. 代理步骤 0 的 `throw` 分支在正常输入下不可达（归一化已保证收敛），它的价值是"规则被改坏时不动盘"；**未做该分支的独立行为用例**（见 02 U6），存在性由源码串守卫。
3. 存量嵌套层不会被本次修复删除或合并（不可逆面，用户在场另批处理）。

---

## 门禁④读数：带两处修复的本地宿主包（2026-10-04 22:1x–22:3x，**按包内容验真，不按脚本自述**）

**产物**：`D:\src\my-proj\OpenForgeSelf\updates\OpenForgeSelf-2.7.3.2610042215-win-x64.zip`，106.7 MB，806 条目；发布链 `release-local -Version 2.7.3 -Sign -UpdateDir …` 正文行 `release-local: ALL DONE in 273s`。

| 检查 | 实测读数 | 等级 |
|---|---|---|
| 串一致（zip 名 / `versions/` 目录名 / `versions/current` / 两个 exe 的 FileVersion+ProductVersion） | 全部 `2.7.3.2610042215`；ProductVersion 另带 commit `+d88d709…` | **Verified**（python 读 zip 条目 + `VersionInfo`） |
| 包内公共层形态 | 顶层只有 `ForgeSelf.exe / hostfxr.dll / host / shared / update-agent.ps1 / versions`，**顶层没有 `plugins/`**（＝「只保持两路」在产物侧成立） | **Verified** |
| 内置插件随版本（L1） | `versions/2.7.3.2610042215/Plugins/` 下 18 个插件，`…/Plugins/DesignSystem/plugin.json` 读出 `Id=design-system`、`Version=3.1.0`，DLL 与 `web/dist/index.js+style.css` 均在 | **Verified**（大小写见下方缺陷） |
| 不嵌套（L2） | 匹配 `versions/<ver>/versions/` 的条目数 = **0** | **Verified** |
| 随包代理确实带修复 | 从 zip 里读出 `update-agent.ps1`，含 `Resolve-ForgeInstallRoot` = True、`拒绝产生逐代嵌套` = True | **Verified** |
| 校验和 | `SHA256SUMS.txt` 行 `32c8fee1…d4ea` 与实测 zip 的 sha256 **逐字 MATCH** | **Verified** |
| Authenticode | 两个 `ForgeSelf.exe` 解出后 `Get-AuthenticodeSignature` → `STATUS=Valid / TYPE=Authenticode`，签名者 `CN=OpenForgeSelf 铸己匣` | **Verified** |
| 「升级一次即回正」的落点推算 | 现场只读盘点：根 `versions/current` = `2.2.11`；根**无** `wwwroot/appsettings.json`（步骤 6 无东西可误删）；根有 `ForgeSelf.staticwebassets.endpoints.json`（会被当扁平残留清掉，符合设计）；真安装根 = `D:\src\tools\ForgeSelf`，从三层嵌套 exe 起算 layers=3 | **Inferred**（读盘 + 同一套解析规则，未实跑升级；升级由用户点，我不碰进程） |

**一条我自己引入、当场逮住的产物缺陷（未修完，如实挂着）**：包内插件目录名是 **`Plugins/`（大写 P）**，而运行布局与真源 §4-R9 要求小写 `plugins/`。机制＝publish 产出沿用源码 csproj 的 `Plugins\**` 目录名，我取消"外置到安装根"之后**失去了原来显式创建小写 `plugins` 的那一步**，于是大小写随 publish 走。Windows 大小写不敏感 ⇒ 功能不受影响（宿主按 `<exeDir>/plugins` 扫，实测同一目录可读到），但**名实不符**且违反文档化不变量。已修：`package-release.ps1` 组装步骤 3 增加两步改名（`Plugins → plugins.tmp-casefix → plugins`，PS 不能做纯大小写改名）+ 校验改成**大小写敏感**的 `Where-Object { $_.Name -ceq 'plugins' }`（`Test-Path` 在 Windows 上不敏感，会放行大写）；判据同步进 `RepositoryScriptTests.PackageRelease_MustKeepBundledPluginsInsideVersionDirectory`（新增 `-ceq 'plugins'` / `-ceq 'Plugins'` / `Plugins → plugins` 三条断言），过滤集复跑 **失败 0 / 通过 32 / 总计 32**（`logs/guard-casefix.log`）。**该修复最终拿到了带它的包（终态见下一节）**：过程中连跑 4 轮 `release-local`——两轮被前端段的 `components.d.ts` 打开失败打断、一轮被 DigiCert 时间戳打断（见下方阻塞项），第 4 轮全绿 ⇒ 交付串＝`2.7.3.2610042326`；`2610042215` 那包（目录名仍为 `Plugins/`）降为历史，不作为交付物。

**阻塞项（非本批改动面，实测 3 次红 / 2 次同命令绿）**：`pnpm build (host-web)` 在 `unplugin-vue-components.writeDeclaration` 报 `UNKNOWN errno -4094, open '…\ForgeSelf.Web\components.d.ts'`；把该生成件改名移开→同命令 `✓ built in 21.56s`；但下一次构建又在同一处红，且该文件在失败后又被外部进程以 **7884 字节 / 22:32** 的形态重新出现（＝有外部编辑器/索引进程持句柄并回写）。本批**未改任何前端配置**，处置口径与待查项已入 `TODO.md`（P2）与当天日记。

---

## 终态读数（2026-10-04 22:5x→23:3x，输入20「那就一个个来」逐项清完我能自做的）

### 交付包 `OpenForgeSelf-2.7.3.2610042326-win-x64.zip`（106.7 MB，806 条目）——按包内容逐条验真

| 判据 | 实测 | 等级 |
|---|---|---|
| 大小写规范化（本批后补的那条） | 构建日志原文 `package-release: normalized bundled plugin dir case Plugins → plugins under …\versions\2.7.3.2610042326`；包内按**大小写敏感**统计：`versions/<ver>/plugins/<Id>/` 命中 **18**、`Plugins/`（大写）命中 **0** | **Verified** |
| 内置插件版本 | `versions/2.7.3.2610042326/plugins/DesignSystem/plugin.json` → `Id=design-system`、`Version=3.1.0`（直接从 zip 条目读出） | **Verified** |
| 不嵌套（输入19） | 匹配 `versions/<ver>/versions/` 的条目 **0** | **Verified** |
| 扫描点只有两路 | 顶层条目集合＝`ForgeSelf.exe / hostfxr.dll / host / shared / update-agent.ps1 / versions`，**顶层无 `plugins/`** | **Verified** |
| 随包代理带修复 | zip 内 `update-agent.ps1` 含 `Resolve-ForgeInstallRoot` 与 `拒绝产生逐代嵌套` | **Verified** |
| 串一致 | zip 名 / 版本目录名 / `versions/current` / 两 exe `FileVersion` 全 = `2.7.3.2610042326` | **Verified** |
| 校验和 | 实测 sha256 `a1c7078c…bba9b3` 与 `SHA256SUMS.txt` 行**逐字 MATCH** | **Verified** |
| 签名与时间戳 | `signtool verify /pa /v` 两 exe：`The signature is timestamped: Sun Oct 04 23:29:24 / 23:29:27 2026`，时间戳由 `DigiCert Assured ID Root CA` 验证，签名者 `CN=OpenForgeSelf 铸己匣`（有效期 2029-09-26） | **Verified** |

**我自己的一个错读（原文留档）**：先用 `Get-AuthenticodeSignature` 读 `$s.TimeSigner` 得到 `HasCounterSig=False / TSA=` 空，据此险些上报"这包没带时间戳"。改用 **signtool verify /pa /v** 复核 ⇒ 时间戳确实在且可验。教训＝**PS 的 `TimeSigner` 属性不能作为"有无 RFC3161 计数器签名"的判据**，签名相关结论一律以 `signtool verify /v` 的输出为准（同族老规律：判定要看工具正文，别拿某个属性的空值当证据）。

### 门禁清单（本批终态）

| 门禁 | 读数 |
|---|---|
| 中档全量 `dotnet test`（碰宿主源码必须） | **失败 15 / 通过 2341 / 总计 2356 / 19.8477 分**（`logs/full-test-nesting.log`）；总数比上轮 +13 ＝ 本批新增判据条数；15 条构成与逐条归因见上「两轮全量对比」 |
| 本批判据集合（宿主 + 发布脚本 + 大小写守卫） | `HostInstallRootTests` **14/14**、`+PluginRoots+RepositoryScript+StagedUpdate` **51/51**、大小写守卫后 `RepositoryScript+PluginRoots` **32/32**；DesignSystem+本批四集合并跑 **585/585（17m13s）** |
| 反向探针 | 两次都实红后还原复绿：①代理不上跳＋宿主退回 ProcessPath → **失败 2/总计 13**；②只破代理归一化 → **失败 2/总计 14** |
| 插件层 e2e（真实宿主） | 全目录 **32 passed / 1 failed（22.4m）**；红的那条长链路**单跑 1 passed（2.4m）** ⇒ 判为 M3 已登记的偶发（500/BUSY 同形态），**不判通过**，如实挂着（`logs/e2e-ds-nesting.log`、`logs/e2e-ds-solo.log`） |
| A 轮那条 DesignSystem 契约红 | 隔离复跑 **失败 0 / 通过 4 / 总计 4** ⇒ 非本批引入 |
| 工件门禁 | `verify-pilot-artifacts.ps1 -TaskId 2026-10-04-host-install-root-single-source` → **PASS**（00–07 八件 + 关键节齐） |
| 出包重试次数 | `release-local -Sign` 共 4 轮：2 轮红在前端段（`components.d.ts` 外部句柄）、1 轮红在 DigiCert 时间戳间歇失败、第 4 轮 `ALL DONE in 215s` 全绿。**半签状态的包一律没往更新源投**（脚本在签名步当场 throw，zip 段根本没跑） |
| 本批路径（现测，HEAD `d88d709` 未被推进） | **26** 条（12 改 + 11 新 + 3 条属 M3 的技能文件混在同一路径集里）；扣除 M3 的 3 条技能＝**23** 条属这两条宿主修复 + 同日文档 |

**仍待用户**：闸门2 验收 → 闸门3 提交授权（逐路径 add，不用 `-A`）→ 在 `:51888` 点「检查更新」升 `2.7.3.2610042326` → 我做**只读**复验（读 `GET /api/plugin` 非空 + 运行路径回到 `D:\src\tools\ForgeSelf\versions\2.7.3.2610042326\ForgeSelf.exe`）。M3 的 G1–G5 逐项批准、以及"是否允许改脚本默认 TSA"两条仍挂着。
