# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> Task ID：PILOT-076（自编号）｜日期：2026-10-30｜任务：版本行落 csproj + release-local 缺省读 csproj + 文档同步

## 项目结构

- 单仓多项目：`ForgeSelf.Api/`（.NET 10 宿主）、`ForgeSelf.Web/`（Vue 3 前端）、`Plugins/<PascalCase>/`（19 个插件）、`scripts/`（仓内脚本，发布链在 `scripts/release/`）、`ForgeSelf.Api.Tests/`（xUnit）。
- 本任务触碰面：`ForgeSelf.Api/ForgeSelf.Api.csproj`（版本属性）、`scripts/release/release-local.ps1` + `release-lib.ps1`（发行串生成）、`ForgeSelf.Api.Tests/`（新增仓库级守卫）、4 份文档、Sems 插件一条运行命令（经 fs 网关的数据操作）。

## 技术栈（本任务相关层）

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 版本真源 | MSBuild 属性（csproj） | `ForgeSelf.Api/ForgeSelf.Api.csproj:27-34` |
| 发行串生成 | PowerShell 7 脚本链 | `scripts/release/release-local.ps1` + `release-lib.ps1` |
| 版本注入通道 | 环境变量 `FORGESELF_RELEASE_VERSION` | `scripts/release/publish-host.ps1:41-53`、csproj:30-31 |
| 守卫 | xUnit 静态/行为测试 | `ForgeSelf.Api.Tests/RepositoryScriptTests.cs` |

## 版本链现状（逐条带证据，全部实读）

1. **csproj 版本块**（`ForgeSelf.Api/ForgeSelf.Api.csproj:27-34`）：
   - `<VersionPrefix>2.3</VersionPrefix>`（**已过期**：现行发行线是 2.4.x，最新已发行 = 2.4.22.2610301421，更新源实测 2026-10-30 15:0x）；
   - `<VersionSuffix>0.$([System.DateTime]::Now.ToString('yyMMddHHmm'))</VersionSuffix>` → 开发者本地兜底 `Version = 2.3.0.<时间码>`；
   - `:31` `FORGESELF_RELEASE_VERSION` 非空时覆盖 `<Version>`（发布链注入，优先级正确）；
   - `PackageVersion = $(VersionPrefix).0`（NuGet 身份层，三段兜底）、`FileVersion = $(Version)`、`AssemblyVersion = $(VersionPrefix).*`。
2. **发行串单点生成**（`scripts/release/release-local.ps1:39-53`）：
   - `-Version` 未传时：`GITHUB_REF_NAME` 有值 → 用 tag；否则 `0.0.0-local`（本地 dry-run 缺省串，不补时间码）；
   - `$ver = $versionGiven ? Get-FullReleaseVersion($Version) : Get-NormalizedVersion($Version)`——即**只有显式传参才补时间码**，CI tag / 0.0.0-local 原样（防产物版本 ≠ tag，R10 ⑤）。
3. **发行串规则**（`scripts/release/release-lib.ps1:42-91`）：`Get-FullReleaseVersion` 3 段补 `yyMMddHHmm` / 4 段幂等 / `-preview` 后缀剥离注入；`Test-ReleaseVersionInjectible` 只对纯数字 3/4 段注入。
4. **注入通道**（`publish-host.ps1:36-53`）：`-Version` 经 `Get-ReleaseVersionInjectible` 判定后设 `env:FORGESELF_RELEASE_VERSION`，csproj 主动读取；`0.0.0-local` 之类不注入、走 csproj 兜底。publish-bootstrapper 同构。
5. **消费方**：`UpdateChecker.GetCurrentVersion` 读业务层 exe `FileVersion`（csproj:20-21 注释）；更新判定 = 世代比较（第 4 段 >65535 为时间码世代）。
6. **测试现状**：`RepositoryScriptTests`（3 个仓库级静态守卫：BOM / git log 码页 / 布局契约）；**无任何测试断言 `0.0.0-local`**（全 `.cs` grep 实证 0 命中）→ 改缺省行为无测试包袱。
7. **文档提及点（现行指导类）**：AGENTS.md §2.3 版本号规则 bullet；`docs/04-standards/packaging-upgrade-backup.md` §1.1 版本号机制行 + §4-R10 + 变更记录表；`scripts/README.md:40`；`.agents/skills/plugin-development/SKILL.md` §六发布条。`b-engineering-platform.md` B10 节是 2026-09-26/27 的**历史决策记录**（其 `-Version v0.1.0` 早已随 10-02 规则过期但按惯例不回写历史节），本次不改。
8. **Sems 侧现状**：本会话 15:0x 经 fs 网关登记的 commandId=6（projectId=1 OpenForgeSelf）script = `pwsh … release-local.ps1 -Version 2.4.23 -Sign`（版本写死，正是本次要去掉的漂移源）。
9. **编码现状（PC-006 lens 输入）**：`release-local.ps1` / `release-lib.ps1` 均已带 UTF-8 BOM（字节实测 EF BB BF）；`ForgeSelf.Api.csproj` 无 BOM（BOM 铁律只约束 .ps1，csproj 无此要求，维持现状）。

## 测试方式

- 后端：`cd ForgeSelf.Api && dotnet build`；`dotnet test`（全量）；过滤集 `--filter "FullyQualifiedName~<X>"`。
- 仓库级脚本守卫在 `ForgeSelf.Api.Tests`（`RepositoryScriptTests` 形态 = 静态断言 + 阳性对照）；本次新增守卫同仓同型。

## 构建命令

```bash
pwsh -NoProfile -Command ". scripts/release/release-lib.ps1; Get-FullReleaseVersion '2.4.22'"   # 函数级验证（不触发发布）
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~RepositoryReleaseVersionTests"      # 新守卫（红→绿）
dotnet test ForgeSelf.Api.Tests                                                                  # 中档全量（scripts/** 触发）
```

## 主要目录职责（本任务相关）

| 目录/文件 | 职责 |
| --- | --- |
| `scripts/release/release-local.ps1` | 唯一发布编排（本地与 CI 同一条命令）；`-Version` 缺省逻辑在本文件 |
| `scripts/release/release-lib.ps1` | 发布脚本共享函数（无副作用 dot-source）——新解析函数落点 |
| `ForgeSelf.Api/ForgeSelf.Api.csproj` | 宿主业务层工程文件；版本属性块 = 本次「版本行真源」落点 |
| `docs/04-standards/packaging-upgrade-backup.md` | 打包/升级/版本号规则**唯一真源**（§1.1 / §4-R10） |
| `scripts/README.md` / `AGENTS.md` §2.3 / plugin-development SKILL | 发布命令与版本规则的现行指导引用点 |

## 代码组织方式

发布链 = `release-local.ps1`（编排+发行串单点生成）→ `build-frontend` → `publish-host` / `publish-bootstrapper`（env 注入）→ `package-release`（组包+签名）→ `make-release-notes` →（`-UpdateDir` 拷贝更新源）。版本串只在 release-local 生成一次，下游只透传——本任务不改变该拓扑，只改「缺省来源」。

## 现有工程规范（对本任务有约束力）

- AGENTS §5.6 门禁分档：**碰 `scripts/**` ⇒ 中档 = 全量 `dotnet test`**（`RepositoryScriptTests` 只有全量才扫得到）。
- §2.3：脚本一律 UTF-8 BOM（PC-006 / RepositoryScriptTests 守卫）；PowerShell 一律 pwsh 7。
- 版本规则真源 `packaging-upgrade-backup.md` §1.1 / §4-R10：CI tag 绝不改写；`-p:Version` 禁用（NU1105/NETSDK1015 实测教训）；NuGet 身份层必须合法三段。
- 本任务为**非缺陷类**（规则演进/新功能），不触发 problem-class-closure 全闭环；但按 Stage 0 要求做了 PC tags 检索。

## 适用问题类

按 `docs/11-troubleshooting/problem-classes/README.md` tags 表检索：触碰「写/改 *.ps1 脚本、发布链路」⇒ **PC-006**（PowerShell 5.1 编码/BOM/码页，guarded，RepositoryScriptTests active）。本任务满足方式：两份 .ps1 编辑保持既有 UTF-8 BOM（编辑前已实测在位，编辑后复验 + 守卫复跑）；不新开 PC（无缺陷，属规则演进）。其余 PC（001-005/007-018）触碰区域与本任务不交集。

## 候选低风险任务

本任务即用户直接指令（输入3，2026-10-30 15:13），非自主候选。

## 选择该任务的原因

用户直接指令（最高优先级任务来源）；改动面集中（2 个脚本/1 个 csproj/4 份文档 + 1 条 Sems 命令），可用「函数级验证 + 仓库级守卫测试 + 中档全量」闭环，不涉生产/DB/权限/架构。
