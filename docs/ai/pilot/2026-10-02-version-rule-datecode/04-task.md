# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备且经闸门1 确认。

## Task ID

PILOT-version-datecode（目录：`docs/ai/pilot/2026-10-02-version-rule-datecode/`）

## Objective

把版本规则统一为 `<三段版本号>.<yyMMddHHmm>`：同一发布的 tag / `versions/<ver>/` / zip 名 / 两个 exe 的 FileVersion / 设置页显示串完全一致，且旧规则安装实例仍可从新规则版本收到更新（`UpdateChecker` 版本解析与比较同步升级）。

## Scope

### Allowed

- `scripts/release/release-local.ps1`、`scripts/release/publish-host.ps1`、`scripts/release/publish-bootstrapper.ps1`
- `scripts/release/new-version.ps1`（新增）
- `ForgeSelf.Api/ForgeSelf.Api.csproj`、`ForgeSelf.Bootstrapper/ForgeSelf.Bootstrapper.csproj`
- `ForgeSelf.Api/Services/UpdateChecker.cs`
- `ForgeSelf.Api.Tests/Services/UpdateCheckerTests.cs`
- `docs/04-standards/packaging-upgrade-backup.md`、`AGENTS.md`
- 本任务工件目录 `docs/ai/pilot/2026-10-02-version-rule-datecode/`（05/06/07 在对应阶段补）
- 依 `03-plan.md` 步骤9 grep 结果追加的**纯文档**同步（须先记 03-plan 偏差记录）

### Forbidden

- 生产环境、数据库结构、鉴权/权限/支付/安全核心逻辑
- 新增第三方依赖；升级/降级 NuGet 包版本
- 停/启/杀用户运行中的宿主进程（含 `D:\src\tools\ForgeSelf`、`:51888` 实例）；不修改其安装目录
- 改动 `scripts/update-agent.ps1`、`scripts/sign-publish.ps1`、`scripts/release/package-release.ps1`、`make-release-notes.ps1`、`publish-release.ps1`（按字符串透传即可，无需改）
- 改动前端源码与 e2e 用例（若发现确需改动，先记偏差并回报）
- 与 PILOT-version-datecode 无关的重构；顺手解决 `TODO.md` 中其它条目
- 未获用户明确指令执行 `git commit` / `git push` / 打 tag

## Acceptance Criteria

- [ ] AC1 4 段输入全链路一致：`-Version v2.3.0.2609161125` → `versions/<V>/`、`versions/current`、zip 名、RELEASE-NOTES 名、tag 全是同一串
- [ ] AC2 3 段输入自动补时间码：`-Version 2.3.0` → `2.3.0.<10 位 yyMMddHHmm>`；`new-version.ps1` 可直接输出该串与 tag 命令
- [ ] AC3 两个 exe（业务层 `artifacts/publish/ForgeSelf.exe`、公共层 `artifacts/layout-root/ForgeSelf.exe`）`FileVersion` 均为完整发行串
- [ ] AC4 10 位时间码可解析与比较（`2.3.1.<ts>` > `2.3.0.<ts>`）
- [ ] AC5 世代兜底：`2.2.2026.1002` 与 `2.2.11` 均判定小于 `2.3.0.2609161125`
- [ ] AC6 非法版本串（>long.MaxValue、非数字、5 段）→ `ParseSemVer` 返回 null 且不抛异常
- [ ] AC7 本地 `dotnet build` 兜底串为 `2.3.0.<yyMMddHHmm>`
- [ ] AC8 文档同步且全仓 grep 无残留旧规则描述（历史变更记录中标注"当时事实/已被取代"者除外）
- [ ] AC9 两个 csproj 构建 0 error，构建输出中无 `CS7035`

## Expected Files

- `scripts/release/release-local.ps1`（改）
- `scripts/release/publish-host.ps1`（改）
- `scripts/release/publish-bootstrapper.ps1`（改）
- `scripts/release/new-version.ps1`（新增）
- `ForgeSelf.Api/ForgeSelf.Api.csproj`（改）
- `ForgeSelf.Bootstrapper/ForgeSelf.Bootstrapper.csproj`（改）
- `ForgeSelf.Api/Services/UpdateChecker.cs`（改）
- `ForgeSelf.Api.Tests/Services/UpdateCheckerTests.cs`（改）
- `docs/04-standards/packaging-upgrade-backup.md`（改）
- `AGENTS.md`（改）

## Verification Commands

```powershell
# 构建（0 error + 无 CS7035 可见输出）
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Release
dotnet build ForgeSelf.Bootstrapper/ForgeSelf.Bootstrapper.csproj -c Release

# 版本解析单测（TDD）
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~UpdateChecker"

# 中档门禁（改动覆盖 ForgeSelf.Api/** 与 scripts/**）
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj

# 版本串透传 + 两 exe 属性
scripts/release/release-local.ps1 -Version 2.3.0.2609161125 -SkipFrontend
Get-Content artifacts/layout/versions/current
[Diagnostics.FileVersionInfo]::GetVersionInfo("artifacts/publish/ForgeSelf.exe").FileVersion
[Diagnostics.FileVersionInfo]::GetVersionInfo("artifacts/layout-root/ForgeSelf.exe").FileVersion

# 辅助脚本 + 文档收口
scripts/release/new-version.ps1 -Version 2.3.0
grep -rn "yyyy\.MMdd\|VersionSuffix\|2\.2\.2026" AGENTS.md docs/04-standards docs/02-features scripts/release

# 前端门禁（不改前端，仍如实跑）
cd ForgeSelf.Web; pnpm run check; pnpm run test
```
