# AI-Native Pilot Result（最终汇报）

## 1. Repository Understanding

用户插件目录已经由 `IDataLocationService.GetPluginDataDirectory` 定义为数据根下的 `plugins/{id}`。`DataLocationService` 的环境变量 / Production / Development 分支没有改。内置插件扫描仍是版本目录旁的 `plugins/`。宿主世代比较在 `UpdateChecker.CompareSemVer`。NuGet 不能使用 10 位修订号。

## 2. Selected Task

2026-11-05-plugin-ecosystem。闸门1：用户对本计划点 Build。

## 3. Changed Files

见 `05-evidence.md` 的 Changed Files。`AppBuilder` 在版本服务 `Initialize(pluginsPath)` 之后调用 `PluginInstallerService.Initialize(数据根/plugins)`，并注册 `PluginHostToolCatalog`。

## 4. Validation

Build: `dotnet build ForgeSelf.Api` 0 错误，已成功生成（Verified）。HelloWorld 第三次编译 0 错误（Verified）。

Unit Test: 过滤集第二次 失败 0 / 通过 57 / 总计 57（Verified）。第一次 56 通过、1 失败，已按日志修正后重跑。

中档全量：失败 36 / 通过 3687 / 总计 3723（Verified，FULL_EXIT=1）。其中 `ProcessOutputEncodingGuardTests` 指向 `PluginBuildService` 的 `BeginOutputReadLine`，已改为 `ProcessOutputText`，定向复测 1/1 通过（ENC_EXIT=0）。其余 35 条见 05。全量未在该修复后再跑。

前端: `pnpm run check` CHECK_EXIT=0；`pnpm run test` 106 files / 1154 tests passed；`pnpm run design:check` DESIGN_EXIT=0（Verified）。

E2E: 未跑。

## 5. Evidence

`docs/ai/pilot/2026-11-05-plugin-ecosystem/05-evidence.md`。来源等级按 Verified / Unknown 分开写。

## 6. Review

`06-review.md` Final Decision: APPROVED。

## 7. Risk

L2。再次安装只暂存；旧 `.forgeself-plugin` 不再被扫描。没有在运行中的宿主上复验。

## 8. Problems Found

- 过滤集首跑 1 红：测试用默认 JSON 选项读 camelCase 清单，`Version` 为空。已改为忽略大小写后 57 绿。
- HelloWorld 不能把 10 位时间码写进 `Version`/`FileVersion`（NETSDK1018）。清单与 `InformationalVersion` 保留完整发行串，`PackageVersion`/`FileVersion` 为 `1.0.0.0`。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

### 9.5 本批自复盘（必填 · 2026-10-31 用户复盘要求立）

- **经验提取（保留打法，2-4 条）**：数据根只用 `GetPluginDataDirectory`，测试用 stub，不改 `DataLocationService`。发行串进清单和目录名，NuGet/PE 版本保持 `a.b.c.0`。过滤测试先看日志正文里的失败条，再改一处重跑。
- **待改进（1-3 条）**：宿主 `dotnet build -v q` 把输出重定向到文件后，过程中日志几乎不增长，完成前看起来像卡住。下次对长编译直接等结束标记，不要中途杀进程。

## 10. 问题类归档

不适用。本次是已批准的功能整改，不是缺陷类复发。§1.8.1 已记录的安装器未 Initialize，本批在 `AppBuilder` 补上调用，不单开问题类。

## 11. 重构提案

R0。安装、激活、来源解析仍在现有 `PluginInstallerService` / `PluginVersionService` / `PluginController` 上扩展，不拆新进程、不拆仓内插件。

## 12. 悬空教训清单（必须为空）

空。
