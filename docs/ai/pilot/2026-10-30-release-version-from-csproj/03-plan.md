# Plan

> 阶段：Stage 3｜具体到真实文件。Task ID：PILOT-076｜日期：2026-10-30

## 实施步骤（TDD：守卫先行）

| # | 步骤 | 文件:动作 | 理由 |
| --- | --- | --- | --- |
| S1 | 新增守卫测试集（**先红**） | `ForgeSelf.Api.Tests/RepositoryReleaseVersionTests.cs` 新建：静态 4 用例（csproj VersionPrefix 三段 / VersionSuffix 纯时间码 / release-local 缺省链标记 + `0.0.0-local` 清零 + GITHUB_REF_NAME 保留 / release-lib 具备 `Get-CsprojReleaseVersion`）+ pwsh 行为 3-4 用例（真实 csproj 互证、`2.4.22.9→2.4.22`、三种错误夹具 throw） | 锁住新不变量；对现状跑出红灯作为回归锚点（AC6） |
| S2 | 跑过滤集确认红 | `dotnet test --filter FullyQualifiedName~RepositoryReleaseVersionTests` | 记录真实红灯清单（预期 ≥4 红：三段断言、后缀断言、缺省链断言、函数存在断言） |
| S3 | 实现 `Get-CsprojReleaseVersion` | `scripts/release/release-lib.ps1` 末尾追加函数（读原文 → 首个 `<VersionPrefix>` 标签对 → 前三数字段 → 校验/throw；注释声明「注释内不得含字面标签对」约定） | 解析逻辑独立成可测函数，release-local 只做分支编排 |
| S4 | 改 release-local 缺省链 | `scripts/release/release-local.ps1`：`:39-43` 缺省块改三分支（显式 / CI tag / csproj）+ `:53` 判定改 `if ($versionFromCi) { Normalized } else { Full }` + csproj 来源提示行 + 头部 Examples 注释更新；全文件清除 `0.0.0-local` | 用户需求 2 的主体；CI 与显式传参行为不动 |
| S5 | csproj 版本行落位 | `ForgeSelf.Api/ForgeSelf.Api.csproj:27-29` `VersionPrefix 2.3→2.4.22`（闸门1 拍板值）、VersionSuffix 去 `0.` 前缀；`:10-26` 注释块改写（新规则 + bump 授权制） | 用户需求 1；兜底 Version 与发行串同构 |
| S6 | 跑过滤集确认绿 | 同 S2 命令 | 红→绿证据 |
| S7 | 中档门禁 | `dotnet test ForgeSelf.Api.Tests`（全量，§5.6：碰 scripts/** ⇒ 中档）+ `dotnet build ForgeSelf.Api` | `RepositoryScriptTests` 只有全量扫得到；对表基线（1516 基线红清单）判新增红 |
| S8 | 文档同步 | ① `docs/04-standards/packaging-upgrade-backup.md`（§1.1 版本号机制行、§4-R10 增补、变更记录表新行）② `AGENTS.md` §2.3 版本号规则 bullet ③ `scripts/README.md:40` ④ `.agents/skills/plugin-development/SKILL.md` §六发布条 | 真源先行，引用点跟随；同一事实只在这四处 + 真源承载 |
| S9 | Sems 命令去 `-Version` | fs 网关 `sems_update_command {commandId:6, script:"pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\\release\\release-local.ps1 -Sign"}` → `sems_list_commands` 回读 | 用户需求 3 的执行面（数据操作） |
| S10 | BOM 复验 | pwsh 读三字节（两 .ps1 + 编辑后文件清单）+ `RepositoryScriptTests` 全量内已覆盖 | PC-006 出口检查 |
| S11 | 补 05/06/07 工件 | `docs/ai/pilot/2026-10-30-release-version-from-csproj/` | 闸门2 材料 |

## Test Plan（守卫覆盖）

新增守卫（文件 `ForgeSelf.Api.Tests/RepositoryReleaseVersionTests.cs`，命名空间 `ForgeSelf.Api.Tests`）：

| 用例 | 层 | 锁什么 | 现状预期 |
| --- | --- | --- | --- |
| ApiCsproj_VersionPrefix_MustBeFullThreeSegmentReleaseLine | 静态 | BR1 载体合法（`2.3` 2 段即红） | 红 |
| ApiCsproj_VersionSuffix_MustBePureDateCode | 静态 | 兜底 Version 恒 4 段（`0.` 前缀残留即红） | 红 |
| ReleaseLocal_MustDefaultToCsprojVersionLine | 静态 | FR3：`Get-CsprojReleaseVersion` 引用 + `0.0.0-local` 清零 + `GITHUB_REF_NAME` 保留（阳性对照=文件>1000B） | 红 |
| ReleaseLib_MustExposeCsprojReleaseVersionReader | 静态 | FR2 函数在位 | 红 |
| GetCsprojReleaseVersion_FromRealCsproj_MatchesIndependentParse | pwsh 行为 | AC2（真实 csproj ↔ 测试侧第二解析器互证；pwsh 缺失软跳过） | 红（函数不存在） |
| GetCsprojReleaseVersion_FourSegmentPrefix_ReturnsFirstThree | pwsh 行为（合成夹具） | AC3 / BR6 | 红 |
| GetCsprojReleaseVersion_InvalidInputs_Throw | pwsh 行为（三种合成夹具 Theory：2 段 / 缺标签 / 文件缺失） | AC4 / Boundary | 红 |

- 既有守卫复跑：`RepositoryScriptTests`（PC-006 BOM 等 3 条）随中档全量覆盖。
- 同源自洽对策（§5.0 教训）：AC2 的两侧解析器是**两份独立实现**（ps1 函数 vs C# 正则），并配「夹具 4 段→前三段」阳性对照证解析器真的在做解析；静态用例均带长度/存在性阳性对照防空转。

## Verification（门禁分档声明）

| 档 | 内容 | 本任务 |
| --- | --- | --- |
| 快 | 过滤集 `dotnet test --filter FullyQualifiedName~RepositoryReleaseVersionTests` + `dotnet build` | S2/S6 必跑 |
| **中（本任务档位）** | **全量 `dotnet test ForgeSelf.Api.Tests`**（碰 `scripts/**` ⇒ §5.6 强制） | S7 必跑，日志重定向读正文判读 |
| 深 | 全量 e2e | 不触发（未碰 e2e 基建/发版前批） |

- 判读姿势（§5.0/§5.6）：TEMP 重定向 `$env:TEMP=$env:TMP='<repo>\.temp\tmp'`；看日志正文 PASS/失败计数，不拿 exit code 当证据；全量红先对表基线（基线红清单 = TODO「全量 dotnet test 基线红」条）。
- **明确不跑**：真·发布链全链（发布动作须用户同意）；首次真实发布时以日志行 `release-local: Version=2.4.22 -> release=2.4.22.<tc>` 补 AC2 端到端 Verified。

## 风险与回滚

- 回滚点 = 各文件单文件 git 还原（改动前工作区该四处基线见 05-evidence「改动前状态」节；csproj/脚本均为小diff）。
- 最大风险 = CI 行为回归 → 双重护栏：release.yml 显式传 `-Version`（不进缺省链）+ 静态守卫断言 `GITHUB_REF_NAME` 分支保留；发布 yml 本身零改动。
- csproj 表达式风险 = VersionSuffix 改形后 5 段回归 → VersionSuffix 静态守卫 + `dotnet build`（S7 前置）实编译验证。

## 不改清单（NOT-to-do）

- `ForgeSelf.Bootstrapper.csproj`（兜底串装饰性过期，见 Compatibility）；`publish-host.ps1` / `publish-bootstrapper.ps1` / `package-release.ps1` / `make-release-notes.ps1` / `new-version.ps1`；`.github/workflows/release.yml`；`b-engineering-platform.md` B10 历史节与 c-changelog 历史行；`UpdateChecker` / 前端任何文件。
