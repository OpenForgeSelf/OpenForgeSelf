---
task_kind: defect
risk:
  level: L1
expected_files:
  - ForgeSelf.Api.Tests/Architecture/PluginLoadDependencyGuardTests.cs
  - Plugins/AIAgent/Controllers/ProjectController.cs
  - Plugins/CostScope/Controllers/CostController.cs
feature: 插件依赖加载守卫
rollback: 删掉守卫测试并恢复两个控制器的构造参数
---

# 插件依赖加载守卫

## Intent

插件独有 NuGet 不进输出目录、控制器构造注入宿主契约，已经让宿主起不来或接口 500，而且 2026-10-09 记过待办后 2026-11-02 又发生。本任务把这两条变成会失败的测试，并改掉扫出来的同因构造函数。

## Spec

- 插件 csproj 里、宿主没有的 PackageReference，必须带 `CopyLocalLockFileAssemblies=true`。
- `Plugins/**/Controllers` 的构造参数不得是 `HostProvidedServiceContracts` 里的类型。`IServiceProvider` 除外。
- 同因命中：`ProjectController` 的 `IApprovalService`、`CostController` 的 `ITurnTelemetryQuery`，改为 `IContext.Get`。

## Plan

- 测试：`ForgeSelf.Api.Tests/Architecture/PluginLoadDependencyGuardTests.cs`
- 改两个控制器和成本单测的构造
- 归档 PC-020 与两个 case

## Task

Allowed：上述测试、两个控制器、成本单测、问题类档案、本目录工件、技能与工程日课里指向该测试的句子。

Forbidden：停 :51888、改发布脚本、改插件加载器的排除名单、给本任务没有扫到的控制器改构造。

## 无界面交互

本任务不改页面。判定是 `dotnet test` 的失败与通过计数，以及问题类台账能被 `ProblemClassRegistryTests` 和 `scripts/check-problem-classes.ps1` 读过。

## Acceptance Criteria

- AC1：去掉复制开关时，守卫测试失败并点名该 csproj
- AC2：开关恢复且控制器改完后，守卫测试通过
- AC3：PC-020 在三处索引里，台账测试通过
