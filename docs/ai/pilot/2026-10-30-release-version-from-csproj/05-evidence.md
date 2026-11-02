# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。每项标注 Verified / Inferred / Unknown。

## Task

PILOT-076 版本行落 csproj + release-local 缺省读 csproj（2026-10-30 输入3，闸门1 已批，版本行 = 2.4.22）

## Changed Files

- `ForgeSelf.Api/ForgeSelf.Api.csproj` — VersionPrefix 2.3 → **2.4.22**；VersionSuffix `0.<yyMMddHHmm>` → 纯 `<yyMMddHHmm>`；头注释块重写（版本行真源 + bump 须经用户允许）
- `scripts/release/release-lib.ps1` — 新增 `Get-CsprojReleaseVersion`（RepoRoot/CsprojPath 二选一、取前三数字段、非法显式抛错、ASCII 报错文案）
- `scripts/release/release-local.ps1` — 缺省分支改三分支（显式 -Version 不变 / GITHUB_REF_NAME 原样 / 本地读 csproj 补时间码 + 来源提示行）；移除 `$versionGiven` 与全部 `0.0.0-local`；Examples 头注释更新
- `ForgeSelf.Api.Tests/RepositoryReleaseVersionTests.cs` — **新增**，9 用例守卫
- `docs/04-standards/packaging-upgrade-backup.md` — §1.1 版本号机制行、§4-R10 单点生成括注 + 落地约束①、§4-R10 新增⑧、变更记录 2026-10-30 行
- `AGENTS.md` — §2.3 版本号规则条追加 2026-10-30 缺省读 csproj + 授权制
- `scripts/README.md` — release-local 行补缺省行为
- `.agents/skills/plugin-development/SKILL.md` — 三处发布命令去 `-Version` + 版本行授权提示
- `docs/04-standards/agent-workflow/b-engineering-daily.md` — B6 BOM 条扩展（edit 工具剥 .ps1 BOM 实测补种）
- 数据操作（fs 网关）：Sems `sems_update_command` commandId=6（OpenForgeSelf）去 `-Version`

## Build

Command:

```bash
pwsh: [System.Management.Automation.Language.Parser]::ParseFile(...release-local.ps1/release-lib.ps1) + dotnet test（隐含全量构建）
```

Result: PASS（来源等级：Verified）

```text
release-lib.ps1  parseErrors=0
release-local.ps1  parseErrors=0
（中档全量 dotnet test 构建段 0 error CS；仅既有 xUnit1031/CS8618 类 warning，与基线一致）
```

## Unit Test

TDD 红→绿全程留痕：

**红灯（实现前）** Command: `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~RepositoryReleaseVersionTests"`

Result: FAIL = 预期红灯（来源等级：Verified），日志 `.temp/pilot076-red.log`

```text
失败!  - 失败: 9，通过: 0，已跳过: 0，总计: 9（全部命中预期：旧两段 prefix / 0. 后缀 / 0.0.0-local 在场 / 函数缺席 / 行为层全挂）
```

**绿灯（实现后）** 同命令，日志 `.temp/pilot076-green3.log`

Result: PASS（来源等级：Verified）

```text
已通过! - 失败: 0，通过: 9，已跳过: 0，总计: 9（exit 0）
```

**中档门禁（scripts/** + ForgeSelf.Api/** 触发）** Command: `dotnet test ForgeSelf.Api.Tests --nologo`，日志 `.temp/pilot076-full3.log`

Result: PASS（来源等级：Verified）——对表基线（10-29 03:54 `test-full-7.log`，52 红/3203 绿/3255 总）

```text
本次：失败 65，通过 3259，总计 3324（总 +69 = 本批 9 守卫 + 并行批新增测试），FULL-SUITE EXIT=1
65 红全部对表归因（0 条归本批）：
  · 基线既有红 50 条（TodoTracker 32 / WorkflowPlanningIntegrationTests 6 / TodosControllerTests 5 /
    ImGateway.DbConfigStore 4 / ScriptRunnerDiIntegration 1 / backfill-feature-ids.ps1 BOM 1——该 BOM 红
    在 test-full-7 基线同在，脚本 30b076f 提交，非本批文件；本批两 .ps1 BOM=True 过守卫）
  · AIAgentTests.ProjectWorkspaceDefaultRootTests 15 条：测试文件来自他会话提交 68df669（10-30 00:03），
    基线（10-29 03:54）中不存在该批测试——属该批断言/实现不一致，与本批（csproj 版本属性/发布脚本/新测试文件）零交集
  · Unit.ScriptRunnerTests.CancelAsync 1 条：flake（全量中 Status=Timeout ≠ Cancelled，5s 竞速）；
    单跑复验 1/1 绿（pilot076-cancel-recheck.log，EXIT=0）——非回归
本批新增红 = 0；本批 9 守卫用例全绿。
```

## Integration Test

N/A（本批不新增运行时行为面；csproj 兜底 Version 形态变化由单测与构建验证覆盖）

## E2E

N/A（零前端/零 UI 改动；02-spec「无界面交互」节已申报——本批交互面 = pwsh 命令行 + Sems 面板既有按钮）

## Static Analysis

- 两 .ps1 AST 解析 0 错（Verified，见 Build）
- BOM 字节复核：release-local.ps1 / release-lib.ps1 均 BOM=True（Verified；edit 工具曾剥 BOM → 已补回，事故与处置见「Known Limitations」前的事故记录）
- 函数级冒烟：`Get-CsprojReleaseVersion -RepoRoot <repo>` → `2.4.22`（Verified，真实输出）

## 发布链端到端

**已实跑（2026-10-30 19:08-19:14，用户输入7 授权「测试一次发布」）**：`release-local.ps1 -Sign`（= Sems 命令 6 同参，**不传 -Version = 新缺省链首跑**），发行串 **2.4.22.2610301914**（Verified，`.temp/release-local-test-default-20261030-r2.log`）：

```text
release-local: version line from ForgeSelf.Api.csproj (VersionPrefix) = 2.4.22 (bump requires user approval)
release-local: Version=2.4.22 -> release=2.4.22.2610301914 OutputRoot=...\artifacts
（build-frontend all done → publish Release win-x64 → Authenticode 签名 → 打包 → 更新源落盘）
```

验包（全部 Verified）：zip `OpenForgeSelf-2.4.22.2610301914-win-x64.zip`（161.8MB，1186 项）在 `D:\src\my-proj\OpenForgeSelf\updates`；SHA256SUMS 行 vs 实算 **MATCH**；根布局（launcher/host/shared/versions/update-agent.ps1）+ `versions/2.4.22.2610301914/plugins/` 19 插件齐；**根 ForgeSelf.exe 与版本层 ForgeSelf.exe 的 FileVersion 均 = 2.4.22.2610301914**（= zip 名 = versions 目录名，单点贯穿）；两 exe Authenticode **Valid + 时间戳有**。

首跑事故：`dotnet publish` 的 StaticWebAssets 压缩段 MSB6006（子 dotnet 进程退出码 0x1000000A）——`dotnet build-server shutdown` 后同命令重跑即过（瞬态，未改任何代码；缺省链两行日志在首跑日志中即已出现）。

## AC 覆盖对照（对 02-spec AC1-AC10）

| AC | 判定 | 依据（来源等级） |
|---|---|---|
| AC1 csproj VersionPrefix=2.4.22 | ✅ | 守卫①绿 + 文件实文（Verified） |
| AC2 release-local 不传 -Version 读 csproj 补时间码 | ✅ | **端到端实跑 Verified**（19:14 发布：缺省链两行日志 + 2.4.22.2610301914 贯穿 zip/目录/两 exe FileVersion），见上节 |
| AC3 解析固定部分（四段取前三） | ✅ | 守卫⑥ fixture 2.4.22.9 → 2.4.22（Verified） |
| AC4 非法输入显式抛错 | ✅ | 守卫⑦ Theory（两段/缺标签/缺文件，ASCII 关键词）（Verified） |
| AC5 CI tag 行为不变 | ✅（代码级 Verified；CI 实跑 Unknown→由下次打 tag 验证） | GITHUB_REF_NAME 分支逐字保留；release.yml 本就显式传 -Version（双保险） |
| AC6 0.0.0-local 废止 | ✅ | 守卫③ NotContain 绿 + Select-String 残留检查 0 命中（Verified） |
| AC7 版本行 bump 须用户允许（文档） | ✅ | 5 处文档回读确认新句在位（Verified） |
| AC8 Sems 命令去 -Version | ✅ | sems_list_commands 回读（Verified） |
| AC9 BOM/编码不变 | ✅ | 字节复核 True + RepositoryScriptTests 26/26 全绿（含 19:10 代修 backfill-feature-ids.ps1 后守卫复跑，pilot076-bom-guard.log）（Verified） |
| AC10 中档门禁对表 | ✅ | full3 日志对表基线（Verified，见 Unit Test 节更新） |

## 事故与处置记录（真实发生）

1. **edit 工具剥 .ps1 BOM**：S10 复核发现两 .ps1 BOM=False（此前 True）→ `WriteAllText + UTF8Encoding($true)` 补回、复核 True；BOM 污染态的全量首跑作废（job pwsh-cd396fe232b74df5 主动 kill）。规则已扩展进 b-engineering-daily.md B6。
2. **job_kill 留孤儿 testhost 锁 DLL**：第二跑 MSB3021/MSB3027（ForgeSelf.dll 被 testhost(52372) 锁）→ 清孤儿进程后重跑第三跑；第二跑 job 报 exit 0 为假象（job 命令末语句是字符串字面量），以日志正文为准——再次验证 §5.0「不许拿 exit code 当证据」。
3. **规格微调（记偏差）**：`Get-CsprojReleaseVersion` 原设计 `-RepoRoot` 必填，红灯揭出夹具用例只传 `-CsprojPath` 时参数绑定直接失败 → 放宽为「二者至少给一，CsprojPath 优先」，测试先行暴露设计缺陷 = TDD 有效性的实证。
4. **发布链首跑 MSB6006（19:08）**：StaticWebAssets 压缩段子 dotnet 崩（exit 0x1000000A），缺省链日志行已正常出现 → `dotnet build-server shutdown` + 同命令重跑即过（19:14 EXIT=0，零代码改动，按瞬态处置；复现再深查）。

## Known Limitations

- ForgeSelf.Bootstrapper.csproj 兜底串仍为旧形（2.3 + `0.` 前缀）——按 03-plan 有意不动（发布态经注入；独立 dev 构建才走兜底，且 bootstrapper 无版本显示面）。已记入 07 §11 重构提案（R1，未排期）。
- ~~完整发布链未实跑~~ **已于 19:14 真跑闭环（AC2 回填 Verified），此限清零**。
- CI 打 tag 实跑仍未验证（AC5）：本次走的是本地链；CI 通道由 release.yml 显式传 -Version 双保险，下次打 tag 时自然覆盖。

## Unresolved Issues

无（无失败遗留；待用户拍板项见 07「待拍板」）。
