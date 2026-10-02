# Plan

> 阶段：Stage 3｜具体到真实文件；偏差须先记录再修正。

## 步骤

1. **新增** `ForgeSelf.Api.Tests/TestDataRootIsolation.cs`：`internal static class` + `[ModuleInitializer] internal static void Initialize()`；含 `FindRepoRoot()`（复刻 `DesignSystemAuthTests` 的 `ForgeSelf.slnx` 向上查找写法）；`RedirectLogAndConfig()` 按「先 `ConfigUnifier` → 再 `XTrace.LogPath` → 再 `Setting.LogPath`+`Save`」顺序执行。
2. **新增** `ForgeSelf.Api.Tests/TestDataRootIsolationGuardTests.cs`：`[Collection("EnvVarIsolation")]` 的 4 条守卫（红/绿判据）。
3. **修改** `docs/04-standards/agent-workflow.md` §B12 第 830 行：表述由「跑前手工赋值 `FORGESELF_DATA_ROOT`」改为「测试进程已由 `[ModuleInitializer]` 自动隔离，手工仅用于覆盖」；Part C 变更记录补 2026-10-02 一行。
4. **删除** `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/_ProbeToCssNoDb.cs`：M2 临时探针（M2 已暂缓，按红线「用完即删、不进版本控制」清理）。
5. **清理** 未入库的 `ForgeSelf.Api.Tests/bin/**/Config/`、`ForgeSelf.Api/bin/**/Config/` 污染残留（Core.config / XCode.config）。
6. **产出** `05-evidence.md` / `06-review.md`（+ 本目录 00-04/07，满足 §0 工件链门禁）。

## 不碰

- `ForgeSelf.Api/DataLocationService.cs`（生产语义冻结）
- `ForgeSelf.Web/e2e/global-setup.ts`（e2e 隔离已就绪）
- `ForgeSelf.Core.Tests` / `ForgeSelf.Abstractions.Tests`（不引用 `Program`/`DataLocationService`）

## Plan 偏差记录

- 原 Plan 第 1 步仅设想「设 `FORGESELF_DATA_ROOT`」即可；实现期实证**单设数据根不足**（程序目录残留 Core.config 绕过隔离），故扩展为「数据根 + 日志路径 + 全部已知 Config FileName」三件套，并修正内部执行顺序（先 ConfigUnifier 后 XTrace）。
- 原 Plan 未列出「清理 bin 残留产物」独立步骤，实际作为必要前置纳入第 5 步。