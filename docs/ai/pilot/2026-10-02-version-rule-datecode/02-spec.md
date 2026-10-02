# Specification

> 阶段：Stage 2｜必须从真实 Repository Understanding 与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`，不得自行假定。
> Task ID：PILOT-version-datecode ｜ 任务目录：docs/ai/pilot/2026-10-02-version-rule-datecode

## Functional Requirements

| ID | 需求 | 说明（依据） |
| --- | --- | --- |
| FR1 | **版本串形态统一** | 发行串 `V = <major>.<minor>.<patch>.<yyMMddHHmm>`，示例 `2.3.0.2609161125`；第 4 段 = 生成时刻的 10 位时间编码（用户 2026-10-02 澄清结论） |
| FR2 | **一处生成、全程复用** | 同一发布中：git tag `v<V>`、`versions/<V>/`、`versions/current` 内容、zip 名 `OpenForgeSelf-<V>-win-x64.zip`、`RELEASE-NOTES-<V>.md`、两个 exe（公共层启动器 + 业务层）的 `FileVersion`/`ProductVersion`、设置页「当前版本」全部等于同一个 `V` |
| FR3 | **时间编码生成点** | 由发布脚本（`release-local.ps1`）计算并注入；`-Version` 支持两种输入：4 段（tag 形态，CI 主路径）直接使用；3 段（如 `2.3.0`）自动补当前 `yyMMddHHmm` |
| FR4 | **tag 与产物同源** | 用户打 `git tag -a v<V>`；CI 以 `GITHUB_REF_NAME` 作为 `-Version`（现状不变），据此保证 tag 与产物串一致 |
| FR5 | **辅助生成入口** | 新增 `scripts/release/new-version.ps1 -Version 2.3.0`：打印完整发行串与可直接执行的 tag 命令，免去人工算时间码 |
| FR6 | **解析器支持 10 位时间编码** | `UpdateChecker` 的 `SemVer`/`ParseSemVer`/`CompareSemVer` 能解析并比较 4 段长整数（现状 `int` 会溢出） |
| FR7 | **规则换代不破更新链** | 旧规则版本（`2.2.2026.1002`、`2.2.11`）与新规则版本（`2.3.0.<时间码>`）之间比较，结论必须为"新规则版本更新" |
| FR8 | **文档同步** | 真源 `docs/04-standards/packaging-upgrade-backup.md` §1.1「版本号机制」行 + 变更记录；`AGENTS.md` §2.3 发布/打包两条 bullet；仓库内旧规则描述（`2.2.<yyyy.MMdd>`、`yyyy.MMdd`、`publish-host 不传 -p:Version`）全部改写 |
| FR9 | **开发者本地构建兜底** | 不走发布脚本的本地 `dotnet build`，两个 csproj 仍自动生成同形态串 `2.3.0.<yyMMddHHmm>`（patch=0 表示非发行构建，保证本地构建永远"旧于"任何发行版） |

## Input

| 输入 | 形态 | 来源 |
| --- | --- | --- |
| `-Version`（发布脚本） | `v2.3.0.2609161125` / `2.3.0.2609161125` / `2.3.0` | 人工（本地）/ `GITHUB_REF_NAME`（CI，`.github/workflows/release.yml:43`） |
| 系统时间 | 本机时间（`Get-Date`） | `release-local.ps1` 计算 `yyMMddHHmm` |
| 当前安装版本 | exe 的 `FileVersion` 字符串（如 `2.2.11`） | `UpdateChecker.GetCurrentVersion()`（`UpdateChecker.cs:288-300`） |
| 远端/本地候选版本 | tag 名或 zip 文件名中的版本 | GitHub/Gitee Releases API、本地目录扫描（`UpdateChecker.cs:801-821`） |

## Output

| 产物 | 形态 |
| --- | --- |
| git tag / Release | `v2.3.0.2609161125` |
| 安装包 | `artifacts/release/OpenForgeSelf-2.3.0.2609161125-win-x64.zip` + `SHA256SUMS.txt` + `RELEASE-NOTES-2.3.0.2609161125.md` |
| 布局 | `artifacts/layout/versions/2.3.0.2609161125/`（业务层）+ `versions/current` = 同一串 |
| exe 属性 | 公共层 `ForgeSelf.exe`（启动器）与 `versions/<V>/ForgeSelf.exe`（业务层）的 `FileVersion`/`ProductVersion` 均为 `2.3.0.2609161125` |
| 设置页 | 「当前版本」显示 `v2.3.0.2609161125`（`UpdatePanel.vue:315`，前端无需改动） |
| 解析能力 | `UpdateChecker` 可解析并正确比较 4 段长整数版本 |

## Business Rules

| ID | 规则 |
| --- | --- |
| BR1 | 版本串仅由 4 段十进制数构成（`major.minor.patch.yyMMddHHmm`），不带预发布后缀；除 tag 前缀 `v` 外无其它字符 |
| BR2 | 时间编码 `yyMMddHHmm` 取自**发布时刻**（脚本单点生成），一旦生成即冻结进 tag；重跑发布不改变已打 tag 的串 |
| BR3 | 第 3 段（patch）由人工在 `-Version` 上递增（与现状 `v2.2.11` → `v2.2.12` 语义一致），脚本不自动 +1 |
| BR4 | 比较规则：先将版本分两"世代"——**世代2 = 第 4 段 > 65535**（日期编码，PE 16 位上限之上）；**世代1 = 其余**（旧规则 `2.2.<yyyy>.<MMdd>`、`2.2.11` 等）。世代2 恒大于世代1；同世代按段逐位数值比较，缺失段按 0 |
| BR5 | PE 文件版本每段 16 位（>65535 触发 `CS7035`，且 `VS_FIXEDFILEINFO` 数值字段被截断为低 16 位；**字符串字段保留完整串**）→ 允许并显式 `NoWarn` 抑制 CS7035，同时在真源登记该代价 |
| BR6 | 生成点唯一：`release-local.ps1` 计算完整串后向 `publish-host.ps1` / `publish-bootstrapper.ps1` 传入 `-Version`，由两者 `-p:Version` 覆盖两个 csproj；csproj 内的日期机制仅供开发者本地构建 |
| BR7 | 不改动 `versions/<ver>/` 目录寻址方式（`update-agent.ps1` 全程按目录名字符串操作，实测不解析版本数值） |

## Boundary Conditions

| 场景 | 期望 |
| --- | --- |
| `-Version 2.3.0`（3 段，本地干跑） | 自动补 `yyMMddHHmm` → `2.3.0.2609161125`，后续全链路用该串 |
| `-Version v2.3.0.2609161125`（4 段带 v） | 去 `v` 后直接使用，不二次追加时间码（幂等） |
| `-Version` 缺省 | CI：`GITHUB_REF_NAME`；本地：`0.0.0-local`（现状语义保留，不再追加时间码，避免本地干跑与既有文档不一致） |
| 时间码跨分钟重跑 | 同一次发布只生成一次；不同次发布自然不同（10 位含分精度） |
| 旧规则候选（`v2.2.2026.0930`）与新规则候选同时存在 | 选新规则为 latest（BR4） |
| 当前安装版本 = `2.2.11`（本机运行实例实测值） | `2.3.0.<时间码>` 判定为有更新（第 2 段 3 > 2） |
| 当前安装版本 = `2.2.2026.1002` | 判定为有更新（世代兜底，BR4） |
| 版本串第 4 段超过 `long.MaxValue` / 含非数字 | `ParseSemVer` 返回 null，该候选被忽略（不抛异常） |

## Error Handling

| 情形 | 处理 |
| --- | --- |
| `-Version` 段数不合法（如 5 段） | `release-local.ps1` 显式抛错并给出允许形态，不静默拼接 |
| 时间码生成失败（系统时间异常） | 脚本抛错终止（不产出版本串即不发布） |
| `publish-host` / `publish-bootstrapper` 未收到版本 | 保持 csproj 兜底（`2.3.0.<yyMMddHHmm>`），不产出空版本 |
| 解析失败的候选版本 | 跳过该候选（现状行为，保持） |
| CS7035 | `NoWarn` 抑制 + 真源登记（BR5），**不允许**留成常驻可见警告 |

## Compatibility

| 维度 | 结论 |
| --- | --- |
| 已安装旧规则实例 | 必须能收到新版本更新（BR4 世代兜底）；本机运行实例 `D:\src\tools\ForgeSelf` 的 `versions/current = 2.2.11`（本次只读核对） |
| 目录/指针格式 | 不变（`versions/<ver>/` + `current`），`update-agent.ps1` 无需改动 |
| CI | `.github/workflows/release.yml` 无需改动（`-Version` 取 tag 名，脚本内部兼容 4 段输入） |
| 前端 | `UpdatePanel.vue` 直接显示版本串，无需改动；既有 vitest 断言（`UpdatePanel.test.ts` 用 `v0.0.0`）不受影响 |
| e2e | `e2e/update-live-apply.spec.ts:91` 断言 `currentVersion` 以目标 tag（去 v）开头 → 新规则下依然成立 |
| 其它消费者 | `scripts/update-agent.ps1`、`scripts/sign-publish.ps1`、`scripts/release/make-release-notes.ps1` 均按字符串处理，不受影响 |

## Non-functional Requirements

| 项 | 要求 |
| --- | --- |
| 单点真源 | 版本串只在 `release-local.ps1` 生成一次，下游只做透传 |
| 可核验性 | 「tag = 目录名 = zip 名 = 两 exe FileVersion = 设置页显示」可由脚本/命令一条条核出（见 04-task Verification） |
| 幂等 | 同 `-Version` 重复跑发布产出稳定（时间码只在 3 段输入时才生成） |
| 向后兼容 | 旧规则 tag / 目录 / 安装实例不被破坏 |
| 无新增依赖 | 仅改既有脚本、csproj、`UpdateChecker` 与文档 |

## Acceptance Criteria

| ID | 验收点 | 判定手段 |
| --- | --- | --- |
| AC1 | 4 段输入透传：`-Version v2.3.0.2609161125` → 全链路串一致（tag/目录/zip/current） | 脚本干跑 + 读 `versions/current` 与产物名 |
| AC2 | 3 段输入自动补时间码：`-Version 2.3.0` → 产出 `2.3.0.<10 位>` | `new-version.ps1` 输出 + 脚本日志 |
| AC3 | 两个 exe 的 `FileVersion` 均为完整发行串 | `FileVersionInfo` 读 `artifacts/publish/ForgeSelf.exe` 与 `artifacts/layout-root/ForgeSelf.exe` |
| AC4 | 10 位时间码可解析、可比较（含 `2.3.1.<ts>` > `2.3.0.<ts>`） | 后端单测（`UpdateCheckerTests`） |
| AC5 | 世代兜底：`2.2.2026.1002`、`2.2.11` 均判定小于 `2.3.0.2609161125` | 后端单测 |
| AC6 | 非法版本（>long、非数字、5 段）返回 null 且不抛异常 | 后端单测 |
| AC7 | 本地 `dotnet build` 兜底串为 `2.3.0.<yyMMddHHmm>` | 构建输出 exe 的 FileVersion |
| AC8 | 文档同步：真源 §1.1 + 变更记录 + `AGENTS.md` §2.3 已改写，全仓 grep 无残留旧描述 | `grep` 收口 |
| AC9 | 无新增构建错误；`NoWarn` 抑制后 CS7035 不再出现在构建输出 | `dotnet build` 输出 |

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| SDK（`dotnet publish -p:Version=2.3.0.2609161125`）是否与裸 `csc` 探针表现一致（仅 CS7035 警告、不报错） | 若 SDK 报错 → 方案退到「FileVersion 用 16 位安全值」分支 | Implement 阶段第一步实测（`publish-host.ps1 -Version ...` 后读 exe 属性） |
| `AssemblyVersion` 通配 `2.3.*` 与 `-p:Version` 覆盖是否冲突 | 可能影响程序集身份/绑定 | 同上实测；如冲突则显式给 `AssemblyVersion` 固定值 |
| 是否有其它文档/技能残留旧版本规则描述 | 文档不一致 | `grep` 全仓 `yyyy.MMdd` / `2.2.2026` / `VersionSuffix` 收口（FR8） |
| `e2e/update-live-apply.spec.ts` 是否可在本机跑通（需发布产物 + 更新源夹具） | 端到端证据完整性 | 先按既有技能判定档位；跑不通则在 05-evidence 记录缺口，不伪造 |
