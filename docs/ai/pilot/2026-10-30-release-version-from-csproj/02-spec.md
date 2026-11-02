# Specification

> 阶段：Stage 2｜从真实 Repository Understanding 与 Intent 推导。
> Task ID：PILOT-076｜日期：2026-10-30

## Functional Requirements

| # | 需求 | 内容 |
| --- | --- | --- |
| FR1 | csproj 版本行落位 | `ForgeSelf.Api.csproj`：`<VersionPrefix>` 由 `2.3` 改为**三段发行线**（默认 `2.4.22`，闸门1 可改拍 `2.4.23`）；`<VersionSuffix>` 由 `0.<yyMMddHHmm>` 改为**纯时间码** `<yyMMddHHmm>`（否则 Version 变 5 段）；开发者本地兜底 `Version = 2.4.22.<时间码>` 形态与发行串同构；`PackageVersion = $(VersionPrefix).0`、`FileVersion = $(Version)`、`AssemblyVersion = $(VersionPrefix).*` 表达式不变（求值仍合法）；头部注释块同步改写（含「版本行 bump 须经用户允许」） |
| FR2 | release-lib 新增解析函数 | `Get-CsprojReleaseVersion`：入参 `-RepoRoot`（必填）、`-CsprojPath`（可选，缺省 `<RepoRoot>/ForgeSelf.Api/ForgeSelf.Api.csproj`）；读文件原文 → 取首个 `<VersionPrefix>…</VersionPrefix>` 字面值 → **解析固定部分 = 前三个数字段**（`2.4.22.9` 也取 `2.4.22`，落实用户「解析固定的部分」原话）→ 返回三段串；文件缺失 / 无该标签 / 不足三段 → 显式 throw（含文件路径与期望形态，不静默兜底） |
| FR3 | release-local 缺省读 csproj | 不传 `-Version` 且无 `GITHUB_REF_NAME` → `$Version = Get-CsprojReleaseVersion -RepoRoot $repoRoot`，走 `Get-FullReleaseVersion` 补时间码，并打印一行来源提示（`version line from ForgeSelf.Api.csproj …`）；**三分支判定**：CI tag → `Get-NormalizedVersion`（原样，行为不变）；用户显式传参 → `Get-FullReleaseVersion`（行为不变）；csproj 缺省 → 补时间码（新行为）。`0.0.0-local` 缺省串废止（文件内所有出现点含注释一并清理）；头部 Examples 注释同步 |
| FR4 | 守卫测试 | 新增 `ForgeSelf.Api.Tests/RepositoryReleaseVersionTests.cs`（约 7 用例，静态 + pwsh 行为两层，见 Test Plan） |
| FR5 | 文档同步 | ① 真源 `packaging-upgrade-backup.md`：§1.1 版本号机制行补 2026-10-30 规则、§4-R10 增补缺省来源与版本行授权制、变更记录表加一行；② `AGENTS.md` §2.3 版本号规则 bullet 更新生成入口描述；③ `scripts/README.md` release-local 行用法补「缺省读 csproj」；④ `plugin-development/SKILL.md` §六发布条更新命令示例 |
| FR6 | Sems 命令更新 | 经 fs 网关 `sems_update_command` 把 commandId=6 的 script 改为 `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\release\release-local.ps1 -Sign`（去 `-Version`），`sems_list_commands` 回读核验（数据操作，非代码） |

## Input

- 显式 `-Version`（可选，行为不变）；环境 `GITHUB_REF_NAME`（CI）；`ForgeSelf.Api.csproj` 的 `<VersionPrefix>` 字面值（新缺省来源）。
- `Get-CsprojReleaseVersion -CsprojPath` 可指向合成夹具（测试用），生产路径恒为真实 csproj。

## Output

- 函数返回三段号字符串；release-local 日志首行 `release-local: Version=<三段> -> release=<三段>.<时间码> OutputRoot=…`（格式不变）+ csproj 来源提示行；csproj 本地构建 `Version = <三段>.<10 位时间码>`。

## Business Rules

| # | 规则 |
| --- | --- |
| BR1 | **版本行真源 = ForgeSelf.Api.csproj `<VersionPrefix>`（三段固定部分）**；bump（含换预览线）必须经用户明确允许，agent 不得自行修改（2026-10-30 输入3 用户立） |
| BR2 | CI tag 原样不改写（产物版本必须 = tag，R10 ⑤ 既有禁令，回归保护） |
| BR3 | 发行串仍单点生成于 release-local（补时间码动作不变）；下游 publish-* 透传，零改动 |
| BR4 | 同一版本行重复发布 → 生成 `<三段>.<新时间码>`（世代比较下单调新于旧包），属预期；换发行线 = 用户授权的 csproj bump |
| BR5 | `0.0.0-local` 本地 dry-run 缺省废止；「不出包的试跑」不再由缺省串承担（要验证走函数级/守卫层，不跑全链） |
| BR6 | 解析固定部分时对超出三段的输入（如 `2.4.22.9`）取前三段，不抛错；不足三段才抛错 |

## Boundary Conditions

- `<VersionPrefix>` 为 2 段（如历史值 `2.3`）→ `Get-CsprojReleaseVersion` throw（期望形态写进报错）；守卫测试同时从静态层拦截该形态回归。
- csproj 文件缺失 / 标签缺失 → throw（带路径）。
- csproj 注释里出现 `<VersionPrefix>` 字样 → 解析取**首个标签对**；本任务改写注释时保证注释内不含该字面标签对（实现约定，写入函数注释）。
- `-Version ''`（显式空串）等价未传 → 走缺省链。
- 时间码同分钟重跑 → 生成同串（与现状一致的幂等边界，包覆盖同路径，不额外处理）。

## Error Handling

- 解析失败一律显式 throw（含文件路径、实际值、期望形态「3 段，如 2.4.22」），让发布链在起跑前失败——不产出无法解释的版本串（对齐 `Get-FullReleaseVersion` 既有风格）。
- 测试夹具错误路径用「pwsh 退出码非 0 + stderr 含关键词」断言。

## Compatibility

- **CI（`.github/workflows/release.yml`）零改动**：其调 `release-local.ps1 -Version "${env:GITHUB_REF_NAME}"`（显式传参），且运行环境本就带 GITHUB_REF_NAME——双保险下行为逐字节不变。
- `publish-host.ps1` / `publish-bootstrapper.ps1` / `package-release.ps1` / `make-release-notes.ps1` / `new-version.ps1` **零改动**（new-version.ps1 仍是 tag 准备辅助，显式 -Version 打印）。
- `ForgeSelf.Bootstrapper.csproj` 的过期 `VersionPrefix 2.3` 兜底**本次不动**：发布态它经注入获得发行串，兜底串只影响独立 dev 构建（装饰性过期），列入不改清单（避免扩面；如需对齐另开任务）。
- 更新判定 / 设置页显示链路零改动（读 exe FileVersion，注入串不变）。

## Non-functional Requirements

- `.ps1` 保持 UTF-8 BOM、pwsh 7 语法（PC-006）；脚本注释与报错文案中文无乱码。
- 新增 pwsh 行为测试单次调用 ≤3s；守卫对 CI windows runner 兼容（pwsh 缺失时软跳过并留痕，本机与 CI 均有 pwsh）。

## Interaction Design（交互设计 · 必写节）

**无界面交互**（本任务无任何 UI 改动）：用户可见面 = ① 命令行/CI 日志（新增一行 csproj 来源提示，格式见 Output）；② Sems 插件面板（页面刷新后 commandId=6 的脚本列显示更新后的无 `-Version` 命令串，名称「发布本地 zip」不变——纯数据更新，无界面代码改动）；③ 未来发版动线 = Sems 面板点「运行」或 `sems_run_command {commandId:6}`，无需任何参数；错误提示 = 版本行非法时发布链起跑前 throw，错误原文含 csproj 路径与期望形态。五态不适用（无新页面/无新端点）。

## Acceptance Criteria

- [ ] AC1 `ForgeSelf.Api.csproj`：`<VersionPrefix>` = 闸门1 批准的三段线（默认 2.4.22）；`<VersionSuffix>` 为纯时间码表达式；本地兜底 `Version` 求值 = `<三段>.<10 位时间码>`；注释块与实现一致。
- [ ] AC2 `Get-CsprojReleaseVersion` 对真实仓库返回与 csproj `<VersionPrefix>` 一致的三段号（pwsh 行为测试 + 测试侧独立第二解析器互证）。
- [ ] AC3 固定部分解析：夹具 `<VersionPrefix>2.4.22.9</VersionPrefix>` → 返回 `2.4.22`（用户原话「解析固定的部分」的直接验证）。
- [ ] AC4 错误路径：2 段 prefix / 缺标签 / 文件缺失三种夹具均 throw（pwsh 退出非 0 + 关键词）。
- [ ] AC5 release-local 三分支：静态守卫证明「`0.0.0-local` 清零 + 引用 `Get-CsprojReleaseVersion` + `GITHUB_REF_NAME` 通道保留」；CI tag 原样与显式传参行为经代码走查 + `Get-FullReleaseVersion`/`Get-NormalizedVersion` 函数未变更证实（Inferred：不跑真发布）。
- [ ] AC6 守卫测试集对**改动前**代码为红（至少 AC2/AC3 对应断言红）、对**改动后**为绿（红→绿证据留 05-evidence）。
- [ ] AC7 中档全量 `dotnet test` 无新增红（对表基线；`RepositoryScriptTests` 既有 3 守卫复跑绿 = PC-006 BOM 复验）。
- [ ] AC8 四份文档更新到位（真源两节 + 变更记录行 + AGENTS §2.3 + scripts/README + plugin-development SKILL）。
- [ ] AC9 Sems commandId=6 回读 script 不含 `-Version`，其余字段不变。

## Unknown

| 不确定点 | 影响 | 处理方式（询问/搁置/保守假设并标注） |
| --- | --- | --- |
| csproj 版本行取 2.4.22 还是 2.4.23 | 决定下一包版本线 | **询问**（闸门1 待拍板项，默认推荐 2.4.22 = 用户原话「最新版本号」） |
| 真·全链 `release-local`（无 -Version）实跑验证 | AC2 端到端置信 | 搁置——发布动作须用户单独同意；以函数级 + 行为测试 + 代码走查覆盖，05-evidence 标 Inferred；首次真实发布时由日志行补 Verified |
