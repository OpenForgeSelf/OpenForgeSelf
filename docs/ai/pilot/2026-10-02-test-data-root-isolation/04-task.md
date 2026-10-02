# Agent Task

> 阶段：Stage 4｜把任务变成可直接执行的工作单元。

## Task ID

`2026-10-02-test-data-root-isolation`（轻量档，输入4）

## Objective

让 `dotnet test` 在无手工前缀时自动把数据根（连同日志路径与已知 Config 文件）隔离到仓库内目录，真实 `~/.forgeself` 零写入。

## Scope

### Allowed

- 新增 `ForgeSelf.Api.Tests/TestDataRootIsolation.cs`
- 新增 `ForgeSelf.Api.Tests/TestDataRootIsolationGuardTests.cs`
- 修改 `docs/04-standards/agent-workflow.md`（§B12 第 830 行 + Part C 变更记录）
- 删除 `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/_ProbeToCssNoDb.cs`
- 清理未入库的 bin `Config/` 残留产物
- 跑后端过滤集/守卫测试验证；更新日记/TODO/工件

### Forbidden

- 改 `ForgeSelf.Api/DataLocationService.cs` 生产回落逻辑
- 改 e2e 基建（`ForgeSelf.Web/e2e/**`）
- 停/启/杀用户运行中的宿主进程
- 未授权 `git commit/push/tag`
- 把探针脚本作为验证手段长期留存
- 顺手修宿主侧顺序缺陷（`Program.cs:38` / `AppBuilder.cs:89`）——超出本任务范围，另立 TODO

## Acceptance Criteria

- [x] `TestDataRootIsolation.cs` 落地（ModuleInitializer，尊重已设值）
- [x] 守卫测试先红后绿（TDD）
- [x] 无手工前缀跑过滤集，`~/.forgeself` 无新增写入；bin 两处 `Config/` 清零且不再生成
- [x] `DataLocationServiceTests` / `DataLocationOverrideTests` / `ForgeConfigTests` 保持绿（回归 6 红与基线一致，无新增）
- [x] `agent-workflow.md` §B12 + 变更记录同步
- [x] `_ProbeToCssNoDb.cs` 已删
- [x] 05-evidence / 06-review 产出

## Expected Files

- `ForgeSelf.Api.Tests/TestDataRootIsolation.cs`（A）
- `ForgeSelf.Api.Tests/TestDataRootIsolationGuardTests.cs`（A）
- `docs/04-standards/agent-workflow.md`（M）
- `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/_ProbeToCssNoDb.cs`（D）
- `docs/ai/pilot/2026-10-02-test-data-root-isolation/`（00-07）

## Verification Commands

```powershell
# 守卫测试：
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TestDataRootIsolationGuardTests"
# 判据：失败 0 / 通过 4

# 数据根相关回归：
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DataLocation|FullyQualifiedName~ForgeConfig|FullyQualifiedName~AppBuilder|FullyQualifiedName~ConfigUnifier"

# PILOT 工件链门禁：
powershell -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-02-test-data-root-isolation
# 判据：PASS
```