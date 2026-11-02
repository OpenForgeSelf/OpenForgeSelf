---
feature: release-version-from-csproj
risk: { level: L2, triggers: ["scripts/release/**"], raised_by_agent: false }
gate1: { mode: user, at: "2026-10-30 15:45", ref: "TODO 协作区·PILOT-076 闸门1 已批 2.4.22" }
expected_files:
  - ForgeSelf.Api.Tests/RepositoryReleaseVersionTests.cs
  - scripts/release/release-lib.ps1
  - scripts/release/release-local.ps1
  - ForgeSelf.Api/ForgeSelf.Api.csproj
  - docs/04-standards/packaging-upgrade-backup.md
  - AGENTS.md
  - scripts/README.md
  - .agents/skills/plugin-development/SKILL.md
rollback: 还原 csproj 版本属性块与 release-lib/release-local 增量即可（守卫测试文件一并还原）
---

# Task

> 阶段：Stage 4｜Agent 可直接执行的工作单元。Task ID：PILOT-076｜日期：2026-10-30

## Objective

版本行真源落 `ForgeSelf.Api.csproj`（默认 2.4.22，闸门1 可改拍）；`release-local.ps1` 本地缺省读 csproj 固定三段 + 补时间码，CI/显式传参行为不变；文档与 Sems 命令同步；仓库级守卫红→绿 + 中档全量门禁。

## Scope

**Allowed（仅限以下文件/操作）**

- `ForgeSelf.Api.Tests/RepositoryReleaseVersionTests.cs`（新建）
- `scripts/release/release-lib.ps1`（追加 `Get-CsprojReleaseVersion`）
- `scripts/release/release-local.ps1`（缺省链三分支 + 注释）
- `ForgeSelf.Api/ForgeSelf.Api.csproj`（版本属性块 + 注释块）
- `docs/04-standards/packaging-upgrade-backup.md`（§1.1 / §4-R10 / 变更记录）
- `AGENTS.md`（§2.3 版本号规则 bullet 一处）
- `scripts/README.md`（release-local 行一处）
- `.agents/skills/plugin-development/SKILL.md`（§六发布条一处）
- fs 网关 `sems_update_command`（commandId=6，数据操作）
- 工件与日记/TODO（流程文件）

**Forbidden**

- 上表之外的任何业务/脚本/前端/插件文件；`publish-*` / `package-release` / `new-version` / release.yml；`ForgeSelf.Bootstrapper.csproj`；git 提交与推送（闸门3 前）；任何发布执行（`release-local` 全链、`sems_run_command`）；停/启/杀任何宿主进程；修改 csproj 版本行**取值**超出闸门1 批准值。

## Acceptance Criteria

- [ ] AC1 csproj 版本行 = 批准三段线，Suffix 纯时间码，兜底 Version 求值合法（三段+时间码 4 段）
- [ ] AC2 真实 csproj 经 `Get-CsprojReleaseVersion` 返回与 `<VersionPrefix>` 一致（行为测试 + 独立解析器互证）
- [ ] AC3 夹具 `2.4.22.9` → `2.4.22`（固定部分解析）
- [ ] AC4 错误路径 ×3 throw（2 段 / 缺标签 / 文件缺失）
- [ ] AC5 release-local 三分支落位：`0.0.0-local` 清零、`Get-CsprojReleaseVersion` 在链、`GITHUB_REF_NAME` 通道保留；显式传参与 CI 行为不变（代码走查 + 函数未变）
- [ ] AC6 守卫集改动前红（≥4 红，留证据）→ 改动后绿（0 红）
- [ ] AC7 中档全量 `dotnet test` 无新增红；`RepositoryScriptTests` 复跑绿（PC-006）
- [ ] AC8 四份文档更新到位
- [ ] AC9 Sems commandId=6 回读无 `-Version`，其余字段不变
- [ ] AC10 两份 .ps1 编辑后 BOM 仍在（字节复验）

## Expected Files（改动后清单）

```
ForgeSelf.Api.Tests/RepositoryReleaseVersionTests.cs   （新增）
scripts/release/release-lib.ps1                         （+~30 行）
scripts/release/release-local.ps1                       （~12 行 diff）
ForgeSelf.Api/ForgeSelf.Api.csproj                      （属性块 + 注释块 diff）
docs/04-standards/packaging-upgrade-backup.md           （3 处）
AGENTS.md / scripts/README.md / .agents/skills/plugin-development/SKILL.md （各 1 处）
docs/ai/pilot/2026-10-30-release-version-from-csproj/00..07 （工件）
```

## Verification Commands

```powershell
# 快档：新守卫红→绿
$env:TEMP='<repo>\.temp\tmp'; $env:TMP=$env:TEMP
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~RepositoryReleaseVersionTests"

# 函数级验证（不触发发布）
pwsh -NoProfile -Command ". scripts/release/release-lib.ps1; Get-CsprojReleaseVersion -RepoRoot (Get-Location).Path"

# 中档（scripts/** 触发，§5.6）：全量
dotnet test ForgeSelf.Api.Tests   # 日志重定向读正文，对表基线

# BOM / Sems 回读
pwsh -NoProfile -Command "(Get-Content scripts/release/release-local.ps1 -Encoding Byte -TotalCount 3) -join ','"
# → fs 网关 sems_list_commands {projectId:1}
```

## 执行顺序

S1 红灯测试 → S2 记录红 → S3/S4/S5 实现 → S6 绿 → S7 中档全量 → S8 文档 → S9 Sems 命令 → S10 BOM 复验 → S11 补 05/06/07 → 闸门2 交用户验收（通过前不提交）。
