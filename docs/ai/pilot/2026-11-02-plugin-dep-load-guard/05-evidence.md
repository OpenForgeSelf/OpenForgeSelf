# Evidence

## Task

2026-11-02-plugin-dep-load-guard

## Changed Files

- `ForgeSelf.Api.Tests/Architecture/PluginLoadDependencyGuardTests.cs`（新建）
- `Plugins/AIAgent/Controllers/ProjectController.cs`
- `Plugins/CostScope/Controllers/CostController.cs`
- `ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostControllerTests.cs`
- `docs/11-troubleshooting/problem-classes/PC-020-plugin-dependency-unresolved.md` 与两份 case、三处索引

## Build

命令：`dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter FullyQualifiedName~PluginLoadDependencyGuardTests|FullyQualifiedName~CostControllerEndpointTests`

Result: PASS（Verified）

```text
已通过! - 失败: 0，通过: 30，总计: 30，持续时间: 11 s
日志：.temp/tmp/plugin-load-guard.log
```

## Unit Test

### AC1 红灯

临时去掉 `Plugins/BrowserUse/BrowserUse.csproj` 的 `CopyLocalLockFileAssemblies`，只跑「插件独有第三方包必须复制到输出目录」。

Result: FAIL（Verified）

```text
失败 1，通过 0
Plugins/BrowserUse/BrowserUse.csproj 独有包 Microsoft.Playwright 但没有 CopyLocalLockFileAssemblies=true
日志：.temp/tmp/plugin-load-guard-red.log
```

随后已把该属性写回 csproj。

### AC2 绿灯

恢复属性后重跑守卫，并加上台账测试（当时 PC 文件尚未写入，这一跑只证明守卫本身是绿的）。

Result: PASS（Verified）

```text
已通过! - 失败: 0，通过: 7
日志：.temp/tmp/plugin-load-guard-green.log
```

AC3 的台账测试在写入 PC-020 之后另跑，结果补在本文件末尾「台账复跑」。

## Known Limitations

守卫不打开发布 zip，也不逐个核对 deps.json 的传递依赖文件是否在插件目录。`IServiceProvider` 构造注入不在本守卫里。

## Unresolved Issues

智能体本批功能的页面交互验收仍未做完。本工件只覆盖依赖解析这一类。

## 台账复跑

写入 PC-020 之后（Verified）：

```text
ProblemClassRegistryTests：已通过，失败 0，通过 5。日志 .temp/tmp/pc020-registry.log
check-problem-classes.ps1：check-problem-classes OK：20 个 PC 签名/allowlist/guards 全部通过。日志 .temp/tmp/pc020-check.log
verify-pilot-artifacts.ps1 -TaskId 2026-11-02-plugin-dep-load-guard -EnforceInteraction：PASS
```
