# Agent Task

任务 ID：`2026-10-04-host-install-root-single-source`（＝**插件根两路解析 + 内置插件随版本**）｜执行者：本会话｜状态：**代码与文档已实施，中档全量在跑，包待出**。

## Objective
消除"发布包把内置插件外置到安装根 / 运行期只扫业务层旁边"这一分叉：内置插件回到 `versions/<ver>/plugins/` 随版本发布，宿主同时把数据目录 `plugins/` 作为第二路插件根合并扫描；把两件事钉成常驻判据。

## Scope

### Allowed（本批已用）
- `scripts/release/package-release.ps1`（布局与产出校验、sanitize、头部注释）
- `ForgeSelf.Api/Plugins/PluginManager.cs`（有序插件根、`AddPluginRoot`、两路合并扫描、版本裁决、日志可见性）
- `ForgeSelf.Api/AppBuilder.cs`（第二路接线与注释；**仅**接线，不动其它宿主逻辑）
- `ForgeSelf.Api.Tests/Plugins/PluginRootsTests.cs`（新增）、`ForgeSelf.Api.Tests/RepositoryScriptTests.cs`（增一条布局守卫）
- `docs/04-standards/packaging-upgrade-backup.md`（§1.1 / §1.6 ③④ / 启动链路 / 变更记录）
- 本 pilot 目录 00–07、`TODO.md`、`.forgeself/memory/2026-10-04.md`
- 产物目录 `.temp/ds-m1/**`；本地更新源 `D:\src\my-proj\OpenForgeSelf\updates`（新包落此，供用户点更新）

### Forbidden（本批未越）
- `update-agent.ps1`、`publish-host.ps1`、`sign-publish.ps1`、版本号规则 §4-R10；发布包"公共层 + `versions/<ver>/`"大框架
- `PluginInstallerService` / `PluginVersionService` / `PluginVersionLayout` 的**写入位置语义**（应改数据根，另批做，已入 TODO U1）
- `PluginFrontendFileMiddleware`、`DevController`、`ForgeSelf.Bootstrapper/**`、`Plugins/**`（18 个插件本体一行未动）、`ForgeSelf.Web/src/**`、两个 `.csproj`、`package.json`（零新依赖）
- 任何数据库 Model.xml / 迁移；任何 `DELETE` 能力
- **搬迁/删除存量实例目录**（`D:\src\tools\ForgeSelf\**`、`~/.forgeself\**` 一律只读）；启停/杀宿主进程；`git commit` / `push` / 打 tag
- 新增"安装根解析器 / `FORGESELF_HOME`"（第一版方案，已被用户推翻，未写一行）

## Acceptance Criteria
（对应 02-spec AC1–AC9）
- AC1 两路各一插件 ⇒ 都发现，`PluginRoots` 有序、`PluginsDirectory` 仍指内置根。
- AC2 同 Id 数据根版本更高 ⇒ 只生效一份且指向数据根目录。
- AC3 同 Id 同版本 ⇒ 保留内置那份。
- AC4 数据目录里只有 `*.db` 的子目录 ⇒ 不当插件（带阳性对照）。
- AC5 内置根缺失、数据根有包 ⇒ 不抛且仍发现（现场形态防护）。
- AC6 同一根重复追加 / 传 null ⇒ 只留一路、不报错。
- AC7 版本比较：可解析按数值；`v3.1.0`、`1.0.0-beta` 一律视为相等（不夺走生效份）。
- AC8 发布布局常驻守卫存在且**被反向探针证明会响**：旧写法插回 ⇒ 红；还原 ⇒ 绿。
- AC9 档位：中档全量 `dotnet test`（碰宿主源码）+ design-system 插件层 e2e 无新增稳定红。
- G 项（闸门留痕）：G1 开工授权＝用户 19:2x「先改一下，尽快增加解析支持」＝已给；G2 版本串（默认 **`2.7.3`**，理由：包内目录结构变了，属"影响产物"的增量，且必须 > 用户已装的 `2.7.2.0`）＝**待用户认可**；G3 提交/打 tag＝**未做，等用户字眼**。

## Expected Files
见 03-plan「Files To Change（实际落地）」表；实施后由 `git status --porcelain -uall` 现测填数（**报数前必回读 `git log --oneline -3` 确认 HEAD 未被并行会话推进**）。

## Verification Commands
```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~PluginRootsTests|FullyQualifiedName~RepositoryScriptTests"
dotnet test ForgeSelf.Api.Tests                      # 中档全量（串行）
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system --workers=1
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/release-local.ps1 -Version 2.7.3 -Sign -UpdateDir D:\src\my-proj\OpenForgeSelf\updates
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-04-host-install-root-single-source
```
执行约束：TMP/TEMP 固定 `.temp/ds-m1/tmp`；**dotnet 一律串行**（并发会 MSB3021/3027，那类日志作废）；判定读日志正文（`通过/失败` 行），不看 exit code；脚本一律 `pwsh`（AGENTS §2.3）。

---

## 第二批 Allowed（本批已用）
- `ForgeSelf.Api/Services/HostInstallRoot.cs`（新增）、`ForgeSelf.Api/Services/StagedUpdateService.cs`（仅 `ApplyStaged` 取根段）、`scripts/update-agent.ps1`（新增归一化函数 + 步骤 0 + 头部注释）、`scripts/release/package-release.ps1`（仅注释）、`ForgeSelf.Api.Tests/Services/HostInstallRootTests.cs`（新增）、`docs/04-standards/packaging-upgrade-backup.md`（§1.5/§4-R11/变更记录）、`TODO.md`、当天工作日记。
- 理由：用户点名「版本目录嵌套的问题也解决」，且这是宿主更新链路缺陷，落在第一批已批准的"宿主解析层"例外内。

## 第二批 Forbidden（本批未越）
- 不改 `PluginManager`/`AppBuilder`/两个 `.csproj`/`Plugins/**` 插件本体/数据库 Model.xml。
- 不改 `update-agent.ps1` 的复制语义、版本保留、staged 清理策略（只加"入口归一化 + 硬拦"）。
- 不改 `UpdateService`（008 冻结件）、不搬迁/不删除用户机器上任何存量目录、不启停/不写 `D:\src\tools\ForgeSelf` 与 `~/.forgeself`。
- 未授权不提交、不打 tag、不推远程。

## 第二批 Acceptance Criteria（对齐 02 的 AC10–AC14）
- AC10 纯函数 8 组输入全绿｜AC11 由 exe 路径解析 + 拿不到目录即抛｜AC12 实跑 `pwsh` 比对两侧 Root/Layers｜AC13 源码接线守卫｜AC14 反向探针两条守卫同时实红后还原复绿。

## 第二批 验证命令
```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~HostInstallRootTests"
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~HostInstallRootTests|FullyQualifiedName~PluginRootsTests|FullyQualifiedName~RepositoryScriptTests|FullyQualifiedName~StagedUpdate"
dotnet test ForgeSelf.Api.Tests                      # 中档全量（串行）
```
执行约束同第一批：TMP/TEMP 固定 `.temp/ds-m1/tmp`、dotnet 串行、判定读日志正文、脚本一律 `pwsh`。
