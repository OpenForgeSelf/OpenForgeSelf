# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式｜载体：markdown 正文直发群消息（规范 §5；仅超单消息上限或用户明确要求时用附件，且附正文摘要）
> 状态只报事实，禁止模糊表述（对齐 AGENTS.md §10.4/§10.5）。

## 1. Repository Understanding

我确认了：

- 版本口径散落在四处：两个 exe 的 csproj（`VersionPrefix/VersionSuffix/Version/FileVersion`）、`scripts/release/*`（`release-local.ps1` 编排 + `publish-host.ps1` / `publish-bootstrapper.ps1` 发布 + `package-release.ps1` 组 zip + `make-release-notes.ps1` 出说明）、`UpdateChecker`（更新比较 + `GetCurrentVersion()` 读 FileVersion）、以及设置页显示（`UpdateController.cs:42` → `UpdatePanel.vue:315/381`，读 `/api/update/status` 的 `currentVersion` 原样串）。
- 「发行串」与「NuGet 身份」「PE 文件版本」是三套约束：发行串可含 10 位时间码，NuGet 项目版本必须 ≤4 段且每段 int（`PackageVersion` 兜底），PE 每段 16 位（`CS7035` + 数值截断，字符串字段完整）。
- 仓库守卫 `RepositoryScriptTests` 会扫 `scripts/**`：含中文的脚本必须带 UTF-8 BOM（PS 5.1 按 GBK 解析会炸）。

## 2. Selected Task

`docs/ai/pilot/2026-10-02-version-rule-datecode/`（PILOT：版本号规则改为三段号 + 10 位时间码，发行线 2.2 → 2.3）

## 3. Changed Files

- `ForgeSelf.Api/ForgeSelf.Api.csproj`、`ForgeSelf.Bootstrapper/ForgeSelf.Bootstrapper.csproj`
- `ForgeSelf.Api/Services/UpdateChecker.cs`、`ForgeSelf.Api.Tests/Services/UpdateCheckerTests.cs`
- `scripts/release/release-lib.ps1`、`publish-host.ps1`、`publish-bootstrapper.ps1`、`release-local.ps1`、`new-version.ps1`（新增）
- `docs/04-standards/packaging-upgrade-backup.md`（§1.1 / §4-R10 / 变更记录）、`AGENTS.md`（§2.3）
- `docs/ai/pilot/2026-10-02-version-rule-datecode/{00,01,02,03,04,05,06}.md`、`TODO.md`

## 4. Validation

Build: `dotnet build ForgeSelf.Api -c Release`（注入 env `FORGESELF_RELEASE_VERSION=2.3.0.2610021730`）→ **PASS**（exit 0，无 NETSDK1018/NU1105/CS7035；`ForgeSelf.dll` FileVersion=2.3.0.2610021730，`ForgeSelf.Core.dll`=1.0.0.0 未外溢）| Verified

Unit Test: `dotnet test --filter UpdateChecker` → **50/50 绿**；后端**全量**（中档）→ 16 红 / 2137 通过 / 2153 总计，**16 红全为本任务外**（基线 7 + 环境类 8 + 并行任务 1）| Verified

前端门禁（未改前端，如实跑）：`pnpm run test` → **62 文件 / 688 用例全绿**；`pnpm run check` → 红 1（`e2e/plugins/design-system/design-system-agent.spec.ts:177 TS2322`，属并行在飞 M1/M2 文件）| Verified

E2E: 完整发布链 `release-local.ps1 -Version 2.3.0 -SkipFrontend` → **PASS**（`release=2.3.0.2610021711`；zip `OpenForgeSelf-2.3.0.2610021711-win-x64.zip`；`RELEASE-NOTES-2.3.0.2610021711.md`；ALL DONE 121s）+ 三处 exe FileVersion 同串 + `versions/current` 同串 + `new-version.ps1` 五种输入行为 | Verified。深档全量 e2e **未跑**（未触碰 e2e 共享基建，§5.6 未触发）。

## 5. Evidence

- 工件：`docs/ai/pilot/2026-10-02-version-rule-datecode/05-evidence.md`（同串核对表、四条被实测否决的注入通道、归属实验、Known Limitations）
- 关键证据原文：`C:\Users\Administrator\AppData\Local\Temp\fs-verprobe\`（`envbuild2.txt` 构建+文件版本、`test-full.txt` / `test-full2.txt` 两次全量、`test-isolated2.txt` 定点、`test-oldversion.txt` 归属实验、`release-e2e3.txt` 发布链、`frontend-gate.txt` 前端）

## 6. Review

`docs/ai/pilot/2026-10-02-version-rule-datecode/06-review.md` → **Final Decision: APPROVED**（Requirement/Scope/Test/Architecture 全 PASS，Risk L2）

## 7. Risk

L2

- 旧规则安装实例（`2.2.2026.x` / `2.2.11`）的**真机升级链路**未验证（运行实例 `:51888` 仍 `versions/current = 2.2.11`）→ 需用户升级后做只读复验（AGENTS 铁律：agent 不得停/启/杀宿主进程）。
- PE 文件版本数值字段被截断（已知、有意识；字符串字段完整，更新判定只读字符串）。
- 8 条 `UpdateServiceTests.ApplyUpdateAsync_*` 为环境类既有红，归属未定 → 建议记 TODO 交测试 owner。

## 8. Problems Found

1. **`-p:Version` 全局注入不可行**（两次实测失败）：外溢到被引用项目 → `NU1105`（错误只进 obj 日志、restore 静默失败）+ `NETSDK1018`（`GetAssemblyVersion` 校验，因 `AssemblyVersion == ''` 条件只报被引用项目）→ 定案改环境变量 `FORGESELF_RELEASE_VERSION`（只被两个 exe 项目读取）。
2. **新建脚本漏 UTF-8 BOM**：`new-version.ps1` 被仓库守卫抓住 → 已修（正是 AGENTS 预警过的历史坑）。
3. 环境噪声：火绒锁 DLL（`CS2012` / `MSB3021`）、系统 Temp 写入被拦截（须 `$env:TEMP` 重定向）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PARTIAL（Spec 原定注入通道 `-p:Version` 实测不可行，改为环境变量并留痕；属实现层修正，语义不变） |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 最重要的问题

**「发行串形态」与「构建系统约束」是两个层次，Spec 阶段只锁定前者会低估实现成本**：本轮 60% 的时间花在 NuGet/PE/GenerateAssemblyInfo 三处版本校验的边界上，其中 `NU1105` 的错误**根本不冒泡**（只写进 `obj` 日志），只能靠 `project.assets.json` / `project.nuget.cache` 反查——这类「静默失败」应当在 Spec 阶段就列为显式风险与探针项。

## 11. 下一步建议

只提出一个最值得进行的下一步实验：**在用户升级 `:51888` 运行实例到新规则版本后，做一次只读复验**（核 `versions/current`、页面「当前版本」显示串、以及一次「检查更新 → 下载 → 重启并更新」链路只读走查），用以验证「旧规则安装实例能收到新规则版本更新」这条当前为 Unknown 的路径。
