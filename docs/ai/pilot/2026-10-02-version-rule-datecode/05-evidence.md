# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。
> ⛔ 禁用表述：「应该可以」「理论上通过」「看起来没问题」「大概率是」「估计可以」。

## Task

2026-10-02-version-rule-datecode（版本规则改为「三段号 + 10 位时间码」，发行线 2.2 → 2.3）

## Changed Files

- `ForgeSelf.Api/ForgeSelf.Api.csproj`（三/四段版本四件套 + env 覆盖行 + PackageVersion + NoWarn CS7035 + 注释）
- `ForgeSelf.Bootstrapper/ForgeSelf.Bootstrapper.csproj`（同上，注释精简）
- `ForgeSelf.Api/Services/UpdateChecker.cs`（`SemVer` 记录 + `ParseSemVer` long 解析 + `CompareSemVer` 世代兜底 + `TryParseVersion` 回退）
- `ForgeSelf.Api.Tests/Services/UpdateCheckerTests.cs`（新增 7 条用例，含旧形态回归与 GitHub 夹具）
- `scripts/release/release-lib.ps1`（新增 `Get-FullReleaseVersion` / `Test-ReleaseVersionInjectible`）
- `scripts/release/publish-host.ps1`（版本注入通道 = 环境变量 `FORGESELF_RELEASE_VERSION` + try/finally 复位）
- `scripts/release/publish-bootstrapper.ps1`（同一通道）
- `scripts/release/release-local.ps1`（发行串单点生成：3 段补时间码 / 4 段幂等 / 非法抛错；`$ver` 贯穿全链）
- `scripts/release/new-version.ps1`（**新增**：只打印完整串与 `git tag` 命令，不做任何 git 写操作）
- `docs/04-standards/packaging-upgrade-backup.md`（§1.1 版本号机制行重写 + 新增 §4-R10 版本号规则 + 状态/变更记录）
- `AGENTS.md`（§2.3 新增「版本号规则」条目；修正同段两处 `<VT>ersions` 文本损坏）
- `docs/ai/pilot/2026-10-02-version-rule-datecode/{00,01,02,03,04}.md`（发行线 2.2 → 2.3 回写）
- `TODO.md`（队列项，入队/待完成）

## Build

Command:

```powershell
$env:TEMP=$env:TMP='<repo>\.forgeself\test-tmp'          # 系统 Temp 写入被拦截，必须重定向
$env:FORGESELF_RELEASE_VERSION='2.3.0.2610021730'        # 模拟发布脚本注入
dotnet build 'ForgeSelf.Api/ForgeSelf.Api.csproj' -c Release --nologo -v m
```

Result: PASS（来源等级：Verified）｜exit=0，「已成功生成」，无 `NETSDK1018` / `NU1105` / `CS7035`

```text
exit=0
已成功生成。
api FileVersion=2.3.0.2610021730 | Product=2.3.0.2610021730+8c85ea4729a5922c1dfa966376b8ec8c112ca974
core FileVersion=1.0.0.0 | Product=1.0.0+8c85ea4729a5922c1dfa966376b8ec8c112ca974
```

- 业务层 `ForgeSelf.dll` 文件版本 = 注入串（完整 4 段）→ Verified
- 被引用项目 `ForgeSelf.Core.dll` 文件版本 = 1.0.0.0（**未外溢**）→ 证明「环境变量只被两个 exe 项目读取」成立 → Verified
- 完整日志：`C:\Users\Administrator\AppData\Local\Temp\fs-verprobe\envbuild2.txt`

## Unit Test

Command:

```powershell
$env:TEMP=$env:TMP='<repo>\.forgeself\test-tmp'
dotnet test 'ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj' --filter 'FullyQualifiedName~UpdateChecker'
```

Result: PASS（来源等级：Verified）｜50/50 全绿

```text
exit=0  Passed! - Failed: 0, Passed: 50, Skipped: 0, Total: 50
（TDD 红灯记录：首次 11 条 Temp 相关用例红 = 系统 Temp 写入被拦截；重定向 TEMP 后绿）
```

全量后端测试（中档门禁，AGENTS §5.6：本次触碰 `ForgeSelf.Api/**` 与 `scripts/**`）：

Command: `dotnet test 'ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj' --nologo`（TEMP 重定向）

Result: 首次 17 红（其中 **1 条为本任务引入，已修**）→ 修复后复跑 16 红（全为本任务外）（来源等级：Verified）

```text
首次（含本任务引入的 1 条）：
失败! - 失败: 17，通过: 2136，总计: 2153，持续时间: 13 m 9 s
  ├─ RepositoryScriptTests.ScriptsWithNonAscii_MustHaveUtf8Bom(file: …\scripts\release\new-version.ps1)
  │    Expected bytes[0] to be 0xEF … because new-version.ps1 含中文但缺 UTF-8 BOM，PS5.1 会按 GBK 解析炸掉, but found 0x23
  │    → **本任务引入**（新建脚本缺 BOM）；已补 BOM（23 20 6E → EF BB BF）
  ├─ UpdateServiceTests.ApplyUpdateAsync_* ×8（隔离复跑仍红；见下方归属实验）
  ├─ WorkflowPlanningIntegrationTests ×6 + ScriptRunnerDiIntegrationTests.GetRuntimes ×1（存量基线，TODO.md L66/73/74 登记）
  └─ SecretMigrationServiceTests.MigrateOnce_… ×1（System.IO.IOException : 无法删除要被替换的文件 = 文件占用）
```

修复后的定点复验（`--no-build`，含 BOM 守卫与 SecretMigration）：

```text
失败! - 失败: 8，通过: 47，总计: 55          # 8 条全为 UpdateServiceTests.ApplyUpdateAsync_*
→ BOM 守卫绿（本任务引入的红已消）；SecretMigrationServiceTests 隔离复跑通过（首发红 = 文件占用偶发）
```

修复后全量复跑：`dotnet test --no-build` → 结果见下方「修复后全量」

**归属实验（证明 8 条 UpdateServiceTests 与本改动无关，Verified）**：
用**旧规则版本串**重跑同一批测试（`dotnet test -c Release -p:VersionPrefix=2.2 -p:VersionSuffix=2026.1002`，即 `Version=2.2.2026.1002`）：

```text
失败! - 失败: 8，通过: 20，总计: 28
→ 旧版串下同样 8 条红 ⇒ 与本任务版本改造无因果关系
```

补充依据：被测类 `ForgeSelf.Api/Services/UpdateService.cs` 本任务未改动；测试注入 `appVersion="1.0.0.0"`（不读 FileVersion）、不调用 `CompareSemVer`/`TryParseVersion`（grep 实证），断言对象是「zip 解压/文件替换/服务启停回调」——与版本解析无交集。

### 修复后全量

Command: `dotnet test 'ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj' --nologo --no-build`

Result: 16 红 / 2137 通过 / 总计 2153（来源等级：Verified）— **16 红全部为本任务外**

```text
失败! - 失败: 16，通过: 2137，已跳过: 0，总计: 2153，持续时间: 14 m 41 s
① WorkflowPlanningIntegrationTests ×6 + ScriptRunnerDiIntegrationTests.GetRuntimes ×1  → 存量基线（TODO.md L66/73/74 登记：测试宿主插件控制器 404）
② UpdateServiceTests.ApplyUpdateAsync_* ×8                                            → 环境类既有红（已用旧规则版本串实验证明与本改动无因果，见上）
③ DesignSystemTests.DesignAgentToolContractTests.ExecuteAsync_空参数_不抛 ×1            → 并行在飞 DesignSystem M2 任务（该插件本任务零文件交集；期望 success=true 实得 false）
④ 上一轮本任务引入的 RepositoryScriptTests BOM 红 → 本轮消失（已修）
⑤ 上一轮出现的 SecretMigrationServiceTests（IOException 文件占用）→ 本轮通过（偶发，验证「文件占用」判定）
```

## Integration Test

Result: PASS（来源等级：Verified）— 以「本地完整发布链」代 Integration（本任务无独立服务集成面）

Command:

```powershell
powershell -File scripts/release/release-local.ps1 -Version 2.3.0 -SkipFrontend
```

```text
release-local: Version=2.3.0 -> release=2.3.0.2610021711 OutputRoot=...\artifacts
publish-host: exe ok, plugin manifests staged: 18
publish-bootstrapper: common layer ok (626 files incl. shared frameworks, ForgeSelf.exe 0.38 MB, fxr=10.0.12)
==> package: zip + sha256  <== ok (20.8s)
package-release: artifact = ...\artifacts\release\OpenForgeSelf-2.3.0.2610021711-win-x64.zip
make-release-notes: wrote ...\artifacts\release\RELEASE-NOTES-2.3.0.2610021711.md
release-local: ALL DONE in 121s
WRAPPER-EXIT=0
```

端到端同串核对（来源等级：Verified）：

| 对象 | 实测值 |
|---|---|
| 发行串（三段号 2.3.0 自动补时间码） | `2.3.0.2610021711` |
| 业务层 exe `artifacts/publish/ForgeSelf.exe` FileVersion | `2.3.0.2610021711`（15.70 MB） |
| 公共层 exe `artifacts/layout-root/ForgeSelf.exe` FileVersion | `2.3.0.2610021711`（0.38 MB） |
| 布局业务层 `artifacts/layout/versions/2.3.0.2610021711/ForgeSelf.exe` FileVersion | `2.3.0.2610021711` |
| `artifacts/layout/versions/current` 内容 | `2.3.0.2610021711` |
| zip 名 | `OpenForgeSelf-2.3.0.2610021711-win-x64.zip` |
| 说明文件 | `RELEASE-NOTES-2.3.0.2610021711.md` |
| ProductVersion（三处 exe 同） | `2.3.0.2610021711+8c85ea47…` |

`new-version.ps1` 输入/输出（来源等级：Verified）：

```text
IN=2.3.0              EXIT=0  → 2.3.0.2610021714 | git tag -a v2.3.0.2610021714 -m "ForgeSelf v2.3.0.2610021714"
IN=2.3.0.2609161125   EXIT=0  → 2.3.0.2609161125（幂等原样，不重复补时间码）
IN=v2.3.0             EXIT=0  → 2.3.0.2610021714
IN=abc                EXIT=1  → invalid version 'abc': expect 3 segments (e.g. 2.3.0) or 4 segments with date code
IN=2.3                EXIT=1  → invalid version '2.3': expect 3 segments … （2 段不合法）
```

### 预览版 tag 支撑与预览串实跑（输入12）

背景：用户要求打「预览版 tag」。仓库既有机制里「预览版」= GitHub Release 的 `prerelease` 标识，而 `publish-release.ps1` 仅在版本串**含 `-` 后缀**时自动加 `--prerelease`（L55-59）；但 Spec 原口径「注入仅匹配纯数字 3/4 段」会让 `<数字串>-preview` 不注入 → exe 文件版本退化为 csproj 构建期兜底串，与 tag 不同串。故补最小支撑：

- `release-lib.ps1` 新增 `Get-ReleaseVersionInjectible`：纯数字串原样注入；`<数字串>-<后缀>` **只注入数字前半段**；其它返回 `$null`（走 csproj 兜底）。
- `Get-FullReleaseVersion` 支持后缀：`v2.3.0-preview` → `2.3.0.<yyMMddHHmm>-preview`；`2.3.0.<码>-preview` 幂等原样。
- `publish-host.ps1` / `publish-bootstrapper.ps1` 改用该函数取注入串（后缀不进 PE 文件版本），步骤日志打印实际注入串。

实跑（来源等级：Verified，`-Version 2.3.0.2610022017-preview -SkipFrontend`）：

```text
release-local: Version=2.3.0.2610022017-preview -> release=2.3.0.2610022017-preview
  ==> host: dotnet publish (… Version=2.3.0.2610022017) ok (84.9s)     ← 注入串 = 数字前半段
  ==> boot: launcher publish (… Version=2.3.0.2610022017) ok (7.6s)
package-release: OpenForgeSelf-2.3.0.2610022017-preview-win-x64.zip   ← zip 名带后缀
make-release-notes: RELEASE-NOTES-2.3.0.2610022017-preview.md          ← 说明文件带后缀
release-local: ALL DONE in 139s / WRAPPER-EXIT=0
versions/current = 2.3.0.2610022017-preview                            ← 目录名带后缀
artifacts/publish|layout-root|layout/versions/<V>/ForgeSelf.exe
  FileVersion   = 2.3.0.2610022017                                     ← 无后缀（PE 段必须纯数字）
  ProductVersion= 2.3.0.2610022017+8c85ea4729a5922c1dfa966376b8ec8c112ca974
```

结论：预览版 tag 下「tag / versions 目录 / zip 名 / Release 页」同串，exe 文件版本 = 该串的数字前半段（`-preview` 后缀无法进入 `AssemblyFileVersion`）；`ParseSemVer` 能解析带后缀串并把 `preview` 作为 prerelease 参与比较 → 更新通道按频道过滤（`channel != stable` 才收 prerelease，`UpdateChecker.cs:336/L351`）符合预期（Verified：解析/比较逻辑由单测 `ParseSemVer_*` / `CompareSemVer_*` 覆盖）。

### git / push / tag / CI（输入12）

```text
git add（仅本次 19 个文件）→ 预提交 hook: PASS: 全部 PILOT 工件链齐全（00-07 八件 + 关键节）
commit  c80149b  feat(release): 版本号规则改为「三段号 + 10 位时间码」，发行线升到 2.3（19 files changed, 1189 insertions(+), 49 deletions(-)）
git push github main           → 远端 refs/heads/main = c80149b（含既有未推送 8c85ea4）
git tag -a v2.3.0.2610022023-preview -m "ForgeSelf v2.3.0.2610022023-preview"  → 指向 c80149b
git push github <tag>          → 远端 refs/tags/v2.3.0.2610022023-preview 存在（tag obj e3f70b6 → commit c80149b）
```

CI（`gh run watch 37006395435`）：**失败**，卡在 `release-local.ps1` 的前端构建步：

```text
> npm run clean && vue-tsc -b && vite build
##[error]e2e/plugins/design-system/design-system-agent.spec.ts(177,5): error TS2322: Type 'string | null' is not assignable to type 'string'.
     pnpm build (host-web) failed with exit code 2
```

**归属（Verified，非本任务引入）**：

- 报错文件 `ForgeSelf.Web/e2e/plugins/design-system/design-system-agent.spec.ts` 本次提交**从未触碰**（`git status` 对它是 clean、`git diff --cached` 无它）；
- 该文件最后一次修改 = `5fa914c feat(ds) DesignSystem v2.8.0 Agent 工具层（M1）…`，而 `5fa914c` **正是本次推送前的远端 main**（`git merge-base --is-ancestor 5fa914c 8c85ea4^` → True）⇒ 该类型错误**在本次推送前就已在远端**，任何 tag 触发的 release 都会在此步失败；
- 本任务零前端改动（本轮把 `release-local.ps1` 的版本注入改到环境变量后，`-SkipFrontend` 与含前端两条路径的差异只在前端构建本身）；
- 报错位置：spec 第 177 行 `surfaceBgHex = surface!.colorHex;`（`string | null` → `string`）。

结论：预览版 Release **未发布成功**（CI 在打包步失败，`Create GitHub Release` 步被跳过）；tag 已在远端但无对应 Release（对更新端无影响：`UpdateChecker` 读 releases API，无 release 即不可见）。

### 解封（用户授权「授权我修这一行」后）

- 修复：`design-system-agent.spec.ts:177` `surface!.colorHex` → `surface!.colorHex ?? ''`（声明处为 `let surfaceBgHex = ''`，第 176 行已断言 truthy，语义不变）→ commit `31ef0f9` 推送。
- 本地验证口径修正：上一次本地发布链用了 `-SkipFrontend`（**恰好跳过 CI 失败的前端段**）⇒ 复现 CI 必须同参数。改跑**含前端**的完整链 `release-local.ps1 -Version 2.3.0.2610022037-preview`：frontend host web 147.3s + plugin webs 235.5s + host publish 136.1s + package 全绿，`ALL DONE in 583s`、`WRAPPER-EXIT=0`；exe FileVersion=`2.3.0.2610022037`，`versions/current`/zip/notes = `2.3.0.2610022037-preview`；`pnpm run check` → 0 error（81 存量 warning）。
- 重打 tag：`v2.3.0.2610022049-preview`（指向 `31ef0f9`）→ CI run 37009025753 **成功**（`release in 3m54s`，Build + package ✓ / Create GitHub Release ✓）。
- 发布结果（`gh release view`）：`isPrerelease=true`、`isDraft=false`，资产 `OpenForgeSelf-2.3.0.2610022049-preview-win-x64.zip`（103.6 MB）+ `SHA256SUMS.txt`；URL `https://github.com/OpenForgeSelf/OpenForgeSelf/releases/tag/v2.3.0.2610022049-preview`（来源等级：Verified）。
- 首次失败 tag `v2.3.0.2610022023-preview` 仍在远端（无对应 Release，未被更新端使用；如需清理可 `git push github :refs/tags/...`）。

## E2E

Result: N/A（依据：本任务未触碰 `e2e/**` 共享基建（`global-setup.ts` / `playwright.*.config.ts` / `fixtures/**`），按 AGENTS §5.6 未触发「深档全量 e2e」；更新链的端到端行为由既有 e2e 用例 `e2e/update-live-apply.spec.ts:91` 的断言约束，该断言用 `startsWith(targetTag.replace(/^v/,''))`，新规则串仍成立 → Inferred）

## Static Analysis

前端类型检查 / 单测（本次未改前端，作门禁如实跑）：

```powershell
cd ForgeSelf.Web ; pnpm run check ; pnpm run test
```

Result: `pnpm run test` PASS（来源等级：Verified）／`pnpm run check` FAIL×1（**非本任务文件**，来源等级：Verified）

```text
pnpm run test → Test Files 62 passed (62) / Tests 688 passed (688) / TEST-EXIT=0

pnpm run check → CHECK-EXIT=2
  e2e/plugins/design-system/design-system-agent.spec.ts(177,5): error TS2322:
  Type 'string | null' is not assignable to type 'string'.
```

归属：本任务**零前端文件改动**（`git status` 中 `ForgeSelf.Web/**` 的改动全部来自并行在飞的 DesignSystem M2 任务：`design-system.spec.ts` 等已修改 7 件 + 未跟踪 9 件），该 TS 报错文件本任务从未触碰 → 非本任务引入；已在 06-review 记为 Major（门禁噪音，归属并行任务）。

后端 Release 配置可行性（本任务附带）：`dotnet test -c Release -p:VersionPrefix=2.2 -p:VersionSuffix=2026.1002 --filter UpdateServiceTests` → 构建成功、20/28 绿（来源等级：Verified）

## Rejected Approaches（实测否决的通道，供后人省时）

1. **命令行 `-p:Version` 全局注入**：10 位时间码经全局属性外溢到被引用项目（`ForgeSelf.Core` / `ForgeSelf.Abstractions` / `Plugins/*`），触发两处失败 → Verified 否决。
   - `NU1105`「'2.3.0.2610021706' 不是有效的版本字符串」：只写进被引用项目 `obj/project.nuget.cache`、不冒泡，对外表现为 restore 静默失败（`MSB4181`）或 `NU1201 …不支持任何目标框架`。
   - `NETSDK1018`：`Microsoft.NET.GenerateAssemblyInfo.targets(226,5)` 的 `GetAssemblyVersion` 以 `NuGetVersion="$(Version)"` 校验（该 task 的 `Condition` 是 `AssemblyVersion == ''`，故显式设了 `AssemblyVersion` 的两个 exe 项目反而不报错、只报被引用项目）。
2. **连带注入 `-p:PackageVersion` 兜底**：restore 可过（探针 exit 0），但构建仍被上述 `NETSDK1018` 拦 → 通道作废。
3. **清 `project.nuget.cache` / `dotnet build-server shutdown`**：不解决该 restore 失败。
4. **`--no-restore`**：坏资产文件下报 `NETSDK1005`，不能作为验证通道。
5. **最终通道（Verified 通过）**：环境变量 `FORGESELF_RELEASE_VERSION`，由两个 exe 项目 csproj 主动读取；发布脚本设值 + `try/finally` 复位。

## Screenshots

N/A（本次无 UI 变更：设置页「当前版本」经 `/api/update/status` 原样回传版本串（`UpdateController.cs:42`），前端 `UpdatePanel.vue` 零改动）

## Known Limitations

- PE 文件版本每段为 16 位，10 位时间码必然溢出 → `CS7035` 已显式抑制；`FileVersionInfo.FileVersion` 字符串字段完整保留，但**数值字段被截断**（按数值读版本的第三方工具会看到截断值）。更新判定读的是字符串字段 → 不受影响（Verified）。
- 本地 Release 构建偶发 `CS2012 无法打开 …ForgeSelf.dll 以写入`，被 `Huorong Internet Security Daemon` 锁定（实测 1 次，重跑即过）——环境瞬时问题，非本改动引入。
- 运行实例 `D:\src\tools\ForgeSelf`（`:51888`）当前仍是 `versions/current = 2.2.11`；新规则的「页面显示/自动更新」端到端效果需用户先升级该实例，之后再做只读复验（AGENTS 铁律：agent 不得停/启/杀宿主进程）。
- 头部 `版本升一个 → 2.3` 的生效前提：调用方显式传三段号时的第 3 段（patch）由人工递增，脚本不自动 +1（BR3）。

## Unresolved Issues

- ~~预览版 Release 未发布（CI 红，非本任务引入）~~ **已解封（2026-10-02，用户授权修那一行）**：修复 `31ef0f9` + 新 tag `v2.3.0.2610022049-preview` → CI run 37009025753 成功、Release 已发布且 `isPrerelease=true`。
- 清理与重发（2026-10-02 输入13「删了，从新提交修改，推新的预览tag」）：失败 tag `v2.3.0.2610022023-preview` 本地 + 远端已删除；新预览 tag `v2.3.0.2610022106-preview` 打在最新提交 `ebceb5e`（含全部修改）→ CI run 37010782602 成功、Release `isPrerelease=true`（资产 zip 103.66 MB + `SHA256SUMS.txt`）。上一枚成功预览 `v2.3.0.2610022049-preview`（指向 `31ef0f9`）已于 2026-10-03 按用户指令删除（`gh release delete --cleanup-tag` 删 Release + 远端 tag，本地 tag 一并删除）⇒ 预览通道只保留 `v2.3.0.2610022106-preview` 一枚。
- 8 条 `UpdateServiceTests.ApplyUpdateAsync_*` 为环境类既有红（已用旧规则版本串实验证明与本改动无因果），归属未定 → 建议记 TODO 交测试 owner。
