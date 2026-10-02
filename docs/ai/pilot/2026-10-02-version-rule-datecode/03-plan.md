# Plan

> 阶段：Stage 3｜**必须具体到真实文件路径**，禁止只写「修改 Service、增加测试」。
> Task ID：PILOT-version-datecode ｜ 任务目录：docs/ai/pilot/2026-10-02-version-rule-datecode

## Files To Change

- file: `scripts/release/release-local.ps1`
  reason: 版本串单点生成——`-Version` 为 3 段时补 `yyMMddHHmm`、4 段时直接用；把完整串传给 `publish-host.ps1` 与 `publish-bootstrapper.ps1`（现状只传 publish-host，且两处都不注入版本）
- file: `scripts/release/publish-host.ps1`
  reason: 恢复 `-p:Version=$ver` 注入（输入43 曾是"移除"以修不一致；本次因"发行串 = 文件版本"而**有意**注入），并改写 L38-40 注释
- file: `scripts/release/publish-bootstrapper.ps1`
  reason: 新增 `-Version` 形参并在 launcher publish 传 `-p:Version`，使公共层启动器 exe 与业务层同串（现状无该形参 → 启动器版本与业务层不一致）
- file: `scripts/release/new-version.ps1`（新增）
  reason: 辅助入口——输入三段发行号，输出完整发行串与可直接执行的 `git tag` 命令（避免人工算 10 位时间码）
- file: `ForgeSelf.Api/ForgeSelf.Api.csproj`
  reason: `VersionSuffix` 由 `yyyy.MMdd` 改 `0.yyMMddHHmm`（本地构建兜底形态 `2.3.0.<时间码>`）；`NoWarn` 追加 `CS7035`（16 位上限的已知代价，配注释）
- file: `ForgeSelf.Bootstrapper/ForgeSelf.Bootstrapper.csproj`
  reason: 同上一项（两个 exe 同源兜底规则）
- file: `ForgeSelf.Api/Services/UpdateChecker.cs`
  reason: `SemVer.Core` 由 `int[]` 改 `long[]`；`ParseSemVer` 逐段 `long.TryParse`；`CompareSemVer` 增加世代规则（BR4）；`ToVersion()` 做前 3 段的 int 保护
- file: `ForgeSelf.Api.Tests/Services/UpdateCheckerTests.cs`
  reason: 补 AC4/AC5/AC6 用例（10 位时间码解析与比较、世代兜底、非法值 null）
- file: `docs/04-standards/packaging-upgrade-backup.md`
  reason: §1.1「版本号机制」行改写为新规则（含 16 位代价与世代兜底）；文末变更记录追加本次一行
- file: `AGENTS.md`
  reason: §2.3 发布规范 / 打包升级备份两条 bullet 同步新版本规则与 `new-version.ps1` 用法
- file: `docs/ai/pilot/2026-10-02-version-rule-datecode/05-evidence.md`（后续）
  reason: Stage 7 证据（本 Plan 阶段只落 00-04）

> 待 Implement 时按 `grep -n "yyyy\.MMdd\|VersionSuffix\|2\.2\.2026" 全仓` 收口，若发现上表之外的残留描述，记入下方「Plan 偏差记录」后追加文件。

## Implementation Steps

1. **先实测 SDK 路径（Unknown 第 1、2 项）**：`scripts/release/publish-host.ps1 -Version 2.3.0.2609161125 -OutputDir <临时目录>` → 读 `ForgeSelf.exe` 的 `FileVersion`/`ProductVersion`/`AssemblyVersion`，确认 SDK 只出 CS7035 警告、不报错、字符串字段完整。若与裸 `csc` 探针不一致 → 停手，按 02-spec Unknown 的保守分支修订 Plan 并回报用户。
2. `ForgeSelf.Api.csproj` / `ForgeSelf.Bootstrapper.csproj`：改 `VersionSuffix` → `0.$([System.DateTime]::Now.ToString('yyMMddHHmm'))`；`<NoWarn>$(NoWarn);CS7035</NoWarn>` + 注释说明 PE 16 位上限的取舍。
3. `publish-bootstrapper.ps1`：新增 `[string]$Version = ''` 形参；`$ver = Get-NormalizedVersion $Version`；仅当非空时向 `dotnet publish` 追加 `-p:Version=$ver`。
4. `publish-host.ps1`：改为向 `dotnet publish` 追加 `-p:Version=$ver`；注释改写为「版本串由 release-local 单点生成，两 exe 同源（本注入是有意的，替代输入43 的移除决策）」。
5. `release-local.ps1`：新增完整串解析（3 段 → 补 `(Get-Date).ToString('yyMMddHHmm')`；4 段 → 直接用；其它 → 抛错）；把完整串传给 publish-host 与 publish-bootstrapper；package-release / make-release-notes / `-UpdateDir` 拷贝继续用 `$ver`。
6. 新增 `scripts/release/new-version.ps1`：`-Version 2.3.0` → 打印 `2.3.0.<stamp>` 与 `git tag -a v2.3.0.<stamp> -m "..."` 两行（只打印，不做任何 git 写操作）。
7. `UpdateChecker.cs`：`SemVer(long[] Core, string? Prerelease)`；`ParseSemVer` 用 `long.TryParse`；新增 `IsDateCoded(SemVer) => Core[3] > 65535`；`CompareSemVer` 先比世代再逐段；`ToVersion()` 用前 3 段并夹取到 `int` 范围。
8. `UpdateCheckerTests.cs`：补 `ParseSemVer_10DigitDateCode_IsParsed`、`CompareSemVer_NewRuleBeatsLegacy`（`2.2.2026.1002`、`2.2.11` vs `2.3.0.2609161125`）、`CompareSemVer_PatchIncrement`、`ParseSemVer_Overflow_ReturnsNull`、`ParseSemVer_FiveSegments_ReturnsNull`（TDD：先红后绿）。
9. 文档同步：真源 §1.1 + 变更记录；`AGENTS.md` §2.3；全仓 grep 收口旧描述。
10. 端到端核验：`release-local.ps1 -Version 2.3.0.2609161125 -SkipFrontend`（或分步 publish）→ 核 `versions/<V>/`、`versions/current`、zip 名、两 exe FileVersion。

## Test Plan

1. **TDD 优先（后端）**：先写 `UpdateCheckerTests` 新用例（红）→ 改 `UpdateChecker`（绿）。
2. **脚本层**：`publish-host.ps1` / `publish-bootstrapper.ps1` 各跑一次带 `-Version` 的 publish，读产物 exe 版本属性；`new-version.ps1` 跑 3 段输入核输出格式。
3. **链路层**：`release-local.ps1 -Version <完整串> -SkipFrontend`（复用既有 wwwroot，省前端构建），核目录名/current/zip 名/notes 名一致。
4. **回归**：`dotnet build` 两个 exe 项目 0 error 且无 CS7035 可见警告；后端全量 `dotnet test`（中档门禁，碰了 `ForgeSelf.Api/**` 与 `scripts/**`）。
5. **前端不回归**：`pnpm run check` + `pnpm run test`（本次不改前端，作为门禁如实跑）。

## Verification

### Build

```powershell
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Release
dotnet build ForgeSelf.Bootstrapper/ForgeSelf.Bootstrapper.csproj -c Release
```

判定：0 error；输出中不出现 `CS7035`（已 NoWarn）。

### Unit Test

```powershell
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~UpdateChecker"
```

判定：新增用例全绿，既有 UpdateChecker 用例无回归。

### Integration Test

```powershell
# 中档门禁：改动了 ForgeSelf.Api/** 与 scripts/**（AGENTS §5.6），跑后端全量
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj
```

判定：新增红为 0；既有基线红按 `TODO.md` 基线清单对表归属（非本次引入者记 TODO）。

### E2E

```powershell
# 强相关：更新链路端到端（判定为可选，取决于本机夹具可用性）
cd ForgeSelf.Web && pnpm exec playwright test e2e/update-live-apply.spec.ts
```

判定：跑通则作为 AC1/AC3 的端到端证据；跑不通（夹具/发布产物缺失）→ 在 05-evidence 记 `Unknown/未执行`，不得以单测冒充。

### Other Checks

```powershell
# 1) 版本串透传（脚本干跑）
scripts/release/release-local.ps1 -Version 2.3.0.2609161125 -SkipFrontend
Get-Content artifacts/layout/versions/current
Get-ChildItem artifacts/release

# 2) 两个 exe 的文件版本
[Diagnostics.FileVersionInfo]::GetVersionInfo("artifacts/publish/ForgeSelf.exe").FileVersion
[Diagnostics.FileVersionInfo]::GetVersionInfo("artifacts/layout-root/ForgeSelf.exe").FileVersion

# 3) 辅助脚本
scripts/release/new-version.ps1 -Version 2.3.0

# 4) 文档收口
grep -rn "yyyy\.MMdd\|VersionSuffix\|2\.2\.2026" AGENTS.md docs/04-standards docs/02-features scripts/release
```

判定：① `versions/current` = zip 名内版本 = tag = 两 exe FileVersion；② `new-version.ps1` 输出 4 段 10 位时间码；③ grep 无残留旧描述（历史变更记录里的"当时事实"可保留，但必须标注已被新规则取代）。

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-02 | 步骤1 前置实测结果（SDK 是否报错、CS7035 是否可抑制、AssemblyVersion 通配是否冲突） | 假设与裸 csc 探针一致 | 实测一致：SDK 只出 `CS7035` 告警、不报错，`<NoWarn>CS7035</NoWarn>` 可压制；`AssemblyVersion=$(VersionPrefix).*` 无冲突（Build 节） |
| 2026-10-02 | 步骤3/4 版本注入通道（步骤 3/4 原写 `-p:Version`） | 命令行 `-p:Version=$ver` 注入两个 exe | **通道作废**：`-p:` 是全局属性，外溢到被引用项目 → `NU1105`（错误只进 obj 日志、restore 静默失败）+ `NETSDK1018`；改为**环境变量 `FORGESELF_RELEASE_VERSION`**（只被两个 exe 项目读取，同串且不外溢），见 05-evidence「Rejected Approaches」 |
| 2026-10-02 | 步骤9 grep 收口范围 | 已知 3 处（真源 §1.1 / AGENTS §2.3 / publish-host 注释） | 实收口：真源 §1.1 + **新增 §4-R10** + 变更记录、`AGENTS.md` §2.3、`publish-host.ps1` / `publish-bootstrapper.ps1` / `release-lib.ps1` 注释（+ 修 `AGENTS.md` 两处 `<VT>ersions` 文本损坏） |
| 2026-10-02（输入12） | 预览版 tag 的注入口径（Spec 原写「仅匹配 `^\d+\.\d+\.\d+(\.\d+)?$` 才注入」） | 带 `-preview` 后缀的 tag 一律不注入 → 文件版本退化为 csproj 构建期兜底串，与 tag 不同串 | 新增 `Get-ReleaseVersionInjectible`：纯数字串原样注入；`<数字串>-<后缀>` 只注入**数字前半段**（PE 文件版本不能带后缀），后缀保留在 tag / zip 名 / Release 页，预览版语义由 GitHub `prerelease` 承载；`Get-FullReleaseVersion` 同步支持 `2.3.0-preview` → `2.3.0.<码>-preview` |
