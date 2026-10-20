# 07-final-report：通用网关 list_todos IServiceScopeFactory 复现（2026-10-17，轻量批）

> 级别：轻量（mini-task.md + 05/06/07）。本批只复现不修复，修复见 `docs/ai/pilot/2026-10-17-plugin-di-helper-todo-fix/`。

## Selected Task

按用户指令「先复现一下问题，增加一条单元测试」：把「MCP 网关 → ToolRegistry → TodoTracker `list_todos`」链路上 `{success:false, error:"No service for type 'IServiceScopeFactory' has been registered."}` 封套锁成可重复 xUnit 测试（红线：禁一次性脚本作验证）。

## Validation

- `dotnet build ForgeSelf.Api.Tests`：**0 错误**（Verified，见 05）
- `dotnet test --filter UniversalGatewayListTodosScopeReproTests`：**1/1 PASS**——测试断言 buggy 封套本身，PASS = 复现成功；错误串与用户观察逐字一致（FluentAssertions `.Be(ObservedError)` 锚定）
- 既有回归：`~TodoTracker|~UniversalToolForwarder` 160 通过 / 2 失败——失败为**既有基线红**（盘符大小写敏感，非本批引入，已记 TODO P2），单独 `--filter ~TodoDispatchFlowTests` 复核确认与本测试无关

## Evidence

`05-evidence.md`——全 Verified；时序复刻真实件（Context/ToolRegistry/UniversalToolForwarder）零 mock 核心；「测试证明的是时序必然产生该封套」与「真实宿主恰好如此」的边界已如实标注（后者由用户原始报错+日志文案佐证）。

## Review

`06-review.md`——九问无 Major/Critical，**APPROVED**，Risk L1；Minor-1（ObservedError 前缀静默容忍 .NET 文案变化）登记在案。

## Risk / Rollback

L1：仅新增 1 个测试文件 + 本 pilot 目录；git 未提交天然可还原（删文件即回滚）。

## 下一步

修复批（plugin-di-helper-todo-fix）已把本复现测试翻写为成功路径回归 `UniversalGatewayListTodosScopeRegressionTests`（本文件计划内退役，头注释已声明改写方式）；本 07 留档复现证据链起点。

## 问题类归档 / 悬空教训（机制 v1.2.0 前工件，自愿补记）

- 问题类归档：根因归 **PC-005**（插件构造期 eager 解析宿主服务，`docs/11-troubleshooting/problem-classes/PC-005-plugin-eager-provider-resolve.md`）；本 case 档案 `docs/11-troubleshooting/cases/2026-10-17-gateway-list-todos-scope.md`。
- 悬空教训清单：（空）——复现方法沉淀进修复批守卫（PluginToolConstructorDiGuardTests + 变异探针），无未落守卫的发现。
