# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。
> Task ID：PILOT-version-datecode ｜ 任务目录：docs/ai/pilot/2026-10-02-version-rule-datecode

## 项目结构

| 路径 | 说明（仓库实存） |
| --- | --- |
| `ForgeSelf.Api/` | 宿主后端（ASP.NET Core，`ForgeSelf.Api.csproj`，AssemblyName=ForgeSelf，TargetFramework=net10.0-windows） |
| `ForgeSelf.Web/` | 前端 SPA（Vue 3，pnpm），e2e 也在其下 `ForgeSelf.Web/e2e/` |
| `ForgeSelf.Bootstrapper/` | 安装根公共层启动器（`ForgeSelf.Bootstrapper.csproj`，发布时改名 ForgeSelf.exe） |
| `ForgeSelf.Core/`、`ForgeSelf.Abstractions/` | 宿主核心/契约程序集 |
| `Plugins/<X>/` | 插件源码（每个含 `plugin.json` + `web/`） |
| `scripts/release/` | 发布流水线（release-local / publish-host / publish-bootstrapper / package-release / make-release-notes / publish-release / release-lib） |
| `scripts/` | 仓库级脚本（`update-agent.ps1`、`install-git-hooks.ps1`、`verify-pilot-artifacts.ps1`、`hooks/pre-commit`） |
| `.github/workflows/` | CI（`release.yml` tag `v*` 触发；`artifact-gate.yml` 工件门禁） |
| `docs/` | `04-standards/`（规范真源）、`18-templates/ai-pilot/`（工件模板）、`ai/pilot/<date>-<task>/`（工件产物） |

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 宿主后端 | .NET 10 + ASP.NET Core + SQLite + NewLife.XCode | `ForgeSelf.Api/ForgeSelf.Api.csproj:4`（net10.0-windows）+ PackageReference 段 |
| 前端 | Vue 3 + Vite + Element Plus + pnpm + vitest | `ForgeSelf.Web/package.json`、`pnpm-lock.yaml` |
| 发布脚本 | PowerShell（需兼容 Windows PowerShell 5.1 与 pwsh） | `scripts/release/release-lib.ps1:4`（显式声明双版本兼容） |
| CI | GitHub Actions（windows-latest，pwsh） | `.github/workflows/release.yml:16-43` |
| e2e | Playwright | `ForgeSelf.Web/e2e/**`（含 `update-live-apply.spec.ts`） |

## 架构特点

- **QQNT 式安装布局**：安装根公共层（根启动器 + .NET 运行时 DOTNET_ROOT 结构 + `plugins/`）＋ 业务层 `versions/<ver>/` ＋ `versions/current` 指针；由 `scripts/update-agent.ps1` 在宿主自停后落新版本并重启启动器（依据：`package-release.ps1:2-11`、`update-agent.ps1:2-12`）。
- **更新链路**：`UpdateChecker`（provider：stardust / github / gitee / local，按 semver 选最新）→ `StagedUpdateService`（下载+校验+解压到 `%LOCALAPPDATA%\ForgeSelf\Updates\<tag>`）→ `update-agent.ps1` 应用（依据：`ForgeSelf.Api/Services/UpdateChecker.cs`、`Controllers/UpdateController.cs:11-12`）。
- **当前版本来源**：`UpdateChecker.GetCurrentVersion()` 读**进程 exe 的 FileVersion 字符串**（`UpdateChecker.cs:288-300`）→ 因此 exe 文件版本即"更新判定用的当前版本"。
- **版本号现状（本次改动对象）**：`VersionPrefix 2.2` + `VersionSuffix = DateTime.Now('yyyy.MMdd')` → `Version = FileVersion = 2.2.<yyyy>.<MMdd>`（`ForgeSelf.Api.csproj:10-17`、`ForgeSelf.Bootstrapper.csproj:16-23`）；发行号（tag/版本目录名/zip 名）由调用方 `-Version` 决定，文档明确二者"各司其职"（`docs/04-standards/packaging-upgrade-backup.md:33`）。

## 测试方式

- 后端单测：`ForgeSelf.Api.Tests`（xUnit；版本解析既有测试文件 `ForgeSelf.Api.Tests/Services/UpdateCheckerTests.cs`）。
- 前端单测：`ForgeSelf.Web` → `pnpm run test`（vitest）；类型/lint：`pnpm run check`。
- e2e：`ForgeSelf.Web/e2e`（Playwright），与本次强相关的是 `e2e/update-live-apply.spec.ts`（真跑"检查→下载→重启并更新"，第 91 行断言更新后 `currentVersion` 以目标 tag（去 v）开头）。
- 工件链门禁：`scripts/hooks/pre-commit` → `scripts/verify-pilot-artifacts.ps1`（提交触碰 `docs/ai/pilot/<task>/` 时校验 00-07 八件 + 关键节）。

## 构建命令

```powershell
# 后端构建 / 测试
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj

# 发布（本地与 CI 同一命令）
scripts/release/release-local.ps1 -Version v2.3.0 -SkipFrontend
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Api/Services/` | 宿主服务（含 `UpdateChecker.cs`、`StagedUpdateService.cs`、`UpdateService.cs`[冻结]） |
| `ForgeSelf.Api/Controllers/` | REST 端点（含 `UpdateController.cs`） |
| `scripts/release/` | 版本号 → 目录名 → zip 名 → tag 的全链路组装 |
| `docs/04-standards/packaging-upgrade-backup.md` | 打包/升级/备份/版本号机制的**唯一真源** |
| `docs/ai/pilot/<date>-<task>/` | AI-Native 闭环工件（00-07） |

## 代码组织方式

宿主按 Controllers / Services / Models / Entities 分层；配置与清单走 `plugin.json`；发布侧为「脚本编排 + csproj 生成版本属性」两段式：csproj 负责生成版本属性，脚本负责把它写进目录名/zip 名/current 指针。

## 现有工程规范（对本任务有约束力）

- `AGENTS.md` §0（预飞铁律 + 出口清单）、§2.3（发布规范、打包/升级/备份真源引用）、§11（AI-Native 九阶段 + 三道闸门）。
- `docs/04-standards/ai-native-engineering-workflow.md` v1.1.0：闸门1=Intent/Spec/Plan/Task 经用户确认；闸门2=Evidence+Review 交付验收；闸门3=验收后提交。
- `docs/04-standards/packaging-upgrade-backup.md` §1.1「版本号机制」行 = 本次要改写的规则真源。
- 铁律：禁止 agent 停/启/杀用户运行中的宿主进程（本次只做只读核对，不触碰运行实例）。

## 本次任务的现状事实（2026-10-02 代码查证）

| # | 承载点 | 现状事实 |
| --- | --- | --- |
| 1 | `ForgeSelf.Api/ForgeSelf.Api.csproj:10-17` | `VersionPrefix 2.3` + `VersionSuffix $([System.DateTime]::Now.ToString('yyyy.MMdd'))` + `Version/FileVersion=$(Version)` + `AssemblyVersion 2.3.*` + `Deterministic=false` |
| 2 | `ForgeSelf.Bootstrapper/ForgeSelf.Bootstrapper.csproj:16-23` | 与宿主同款日期版本机制（输入39 起"与业务层同源"） |
| 3 | `scripts/release/publish-host.ps1:33-47` | 输入43 起**不再传** `-p:Version`；注释声明"日期机制生效，与根启动器统一；版本目录名仍由 -Version 决定" |
| 4 | `scripts/release/publish-bootstrapper.ps1:17-42` | **无 `-Version` 形参**，launcher publish 完全用 csproj 自带版本 |
| 5 | `scripts/release/release-local.ps1:26-31,49,55,59` | `-Version`（缺省 CI 的 `GITHUB_REF_NAME`，本地 `0.0.0-local`）→ `$ver = Get-NormalizedVersion`（去 v）→ 传 publish-host / package-release / make-release-notes |
| 6 | `scripts/release/package-release.ps1:37,41,99,109` | `$ver` → `versions/<ver>/`、`versions/current` 内容、`OpenForgeSelf-<ver>-win-x64.zip` |
| 7 | `scripts/release/publish-release.ps1:21-22` | `$ver` → `$tag = "v$ver"` → `gh release create $tag` |
| 8 | `.github/workflows/release.yml:43,49` | CI 用 `GITHUB_REF_NAME`（即 tag）作为 `-Version` |
| 9 | `ForgeSelf.Api/Services/UpdateChecker.cs:917-976` | `SemVer(int[] Core, string? Prerelease)`；`ParseSemVer` 允许 1~4 段、逐段 `int.TryParse`；`CompareSemVer` 逐段数值比较（缺失段=0）；`ToVersion()` 只取前 3 段 |
| 10 | `UpdateChecker.cs:288-300,340-380,764-767,801-821` | 当前版本读 FileVersion 字符串；GitHub/本地目录两路都先 `ParseSemVer(tag/文件名)`，解析失败即 `continue`/跳过 |
| 11 | `ForgeSelf.Api/Controllers/UpdateController.cs:42` | `status.currentVersion = UpdateChecker.GetCurrentVersion()` 原样回传 |
| 12 | `ForgeSelf.Web/src/components/settings/UpdatePanel.vue:315,327,381` | 设置页直接显示 `v{{ currentVersion }}` / `latestVersionTag`（无需前端改动即随新格式变化） |
| 13 | `scripts/update-agent.ps1:74-144` | 全程按目录名字符串操作（`versions/<ver>`、`current`），**不解析版本数值** |
| 14 | `docs/04-standards/packaging-upgrade-backup.md:33,207,211` | 版本号机制真源行 + 输入43/输入39 变更记录（本次需同步改写/追加） |

## 探针实测（Verified，2026-10-02，探针位于系统 Temp，未污染仓库）

| 检查项 | 方法 | 实测结果 |
| --- | --- | --- |
| PE 文件版本 16 位上限 | Roslyn`csc` 编译 `[assembly: AssemblyFileVersion("2.3.0.2609161125")]` | **编译通过但 warning CS7035**（版本串不符合 major.minor.build.revision）；`2.3.0.65535` 无警告，`2.3.0.65536` 起触发 |
| PE 内实际写入值 | `FileVersionInfo.GetVersionInfo(probe.dll)` | `FileVersion`/`ProductVersion` **字符串字段保留完整** `2.3.0.2609161125`；**数值字段被截断** `FilePrivatePart = 41893`（2609161125 & 0xFFFF） |
| 仓库是否把警告升级为错误 | `grep TreatWarningsAsErrors/WarningsAsErrors/NoWarn` 全仓 | 0 命中 → CS7035 不会中断构建，但会成为**常驻构建警告** |
| 10 位时间编码能否被当前解析器接受 | `UpdateChecker.cs:947-957` 逐段 `int.TryParse` | `2609161125` > `int.MaxValue(2 147 483 647)` → `ParseSemVer` 返回 **null** → GitHub/local 两条链路都会**忽略**该版本（本地目录按 zip 名扫描时静默跳过） |
| 规则换代后旧实例能否收到更新 | `CompareSemVer` 逐段比较（:`963-976`） | 旧装 `2.2.2026.1002`（第 3 段 2026）**恒大于**新 `2.3.0.<时间码>`（第 3 段 0）→ 已安装实例判定"已是最新"，更新链断 |

## 候选低风险任务

1. **版本规则改造（本次，已由用户直接指令指定）**：2 个 csproj + 5 个发布脚本 + `UpdateChecker` 版本解析 + 文档真源同步。
2. 宿主侧顺序缺陷（`Program.cs:38`/`AppBuilder.cs:89` 的 `Save()` 早于 `ConfigUnifier`）——已在 `TODO.md` 登记，本次不碰。

## 选择该任务的原因

用户直接指令（附图片给出目标格式）；改动面集中在版本生成/消费两端，可用「单测 + `dotnet build` + `release-local` 干跑 + 现有 e2e」验证；两条平台/代码硬约束（PE 16 位字段、`int` 解析溢出）已通过探针取得真实证据，属于必须在闸门1 拍板的方案分叉，而非实现细节。
