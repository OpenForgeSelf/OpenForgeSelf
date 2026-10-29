---
id: PC-014
title: 测试 ctor 手搓 DAL.AddConnStr 换库（DAL.Reset 换 Database + 会话残留 → 吞行/吞表/写走别处）
tags: [xcode, test-infra, shared-state]
invariant: 测试进程内 XCode 连接串只在 XCodeTestFixture 首绑注册一次；测试类 ctor 禁止 AddConnStr/换库，只用 InitConnection 补建缺表与清缓存
status: guarded
first_seen: 2026-10-29
recurrences: []
cases: [2026-10-29-test-ctor-rebind-connstr]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Api.Tests/XCodeTestFixtureIronRuleTests.cs"}
allowlist: []
guards:
  - {level: G2, path: "ForgeSelf.Api.Tests/XCodeTestFixtureIronRuleTests.cs", status: active}
lens:
  - 新写测试类是否又在 ctor 里自建临时目录并 AddConnStr 换库？
  - 全量联跑顺序下，本类依赖的表是否可能因首绑早于实体程序集加载而缺失（ctor 需 InitConnection 补建）？
---
# PC-014 测试 ctor 手搓 DAL.AddConnStr 换库

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。
> 状态机与守卫等级语义见技能 `.agents/skills/problem-class-closure/references/07-recording-spec.md`。

## 1 不变量与诱因

**不变量**：一个测试进程里，每个 XCode 连接名只允许一次注册（`XCodeTestFixture` 首绑：随机目录 + 全部连接串 + 建表 + 程序集预载）；后续所有测试类复用该目录，仅做 `ClearAllData()`（纯 DELETE）与 `EntityFactory.InitConnection(connName)` 补建。

**为什么会犯**：单类隔离跑测试时「每类自建目录 + AddConnStr」是直觉写法且单类全绿；缺陷只在**全量联跑**时暴露——`DAL.AddConnStr` 换串触发 `DAL.Reset()` 换 Database 实例（`_hasCheck` 归零 → CheckTables 重跑 → SQLite「备份-重建-回填」与并发写竞态）+ `DbBase.CreateSession` 的 ThreadLocal/AsyncLocal 会话缓存检出旧会话。单类运行永远踩不到，这是典型「隔离绿、全量红」的认知缺口。次级诱因：`ClearXCodeDataAttribute`（BeforeAfterTestAttribute）**从未在程序集级注册**——「每测试前清理+建表」的前提根本不存在，属死代码。

## 2 特征签名与扫描方法

- S1（G2 测试守卫）：`XCodeTestFixtureIronRuleTests` 扫描全部测试源码，除 `XCodeTestFixture.cs` 与豁免清单外禁止出现 `DAL.AddConnStr(`。豁免清单 = 存量同型残留（CostScope 5 / DesignSystem 18 / Sems 4 / ForgeSelf 系 10——受害连接不同，专项批迁移后逐条移除）。
- 盲区：动态拼连接名的 AddConnStr（正则按字面量扫描抓不到变量形态）；跨进程并行（本项目 `DisableTestParallelization=true` 已封）。

## 3 Canonical fix

1. 删除测试类 ctor 的自建目录 + `DAL.AddConnStr(...)` + `EntityFactory.InitConnection` 换库组合；
2. 改 `IClassFixture<XCodeTestFixture>` 注入 + ctor 调 `EntityFactory.InitConnection(connName)`（不换串 → 无 Reset；类自身引用实体类型 → JIT 已加载 → 补建必能扫到）；
3. 需要新连接名时，改 `XCodeTestFixture._connNames`（唯一入口），不改各测试类。
4. 参考：`ForgeSelf.Api.Tests/Integration/AgentRunPersistenceTests.cs`（2026-10-29 修复版 ctor 注释）。

**被否方案**：Database 实例级 `Migration=Off`——实测把 `CheckTableWhenFirstUse` 实体（WorkflowDefinition/ScriptExecution/AIChatMessage）的惰性建表一起堵死，全量出现大面积 no such table（test-full-2/3）；程序集预载 + 单点建表——预载依赖程序集简单名且不解决「为何其他类缺表」的根因，弃。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `XCodeTestFixtureIronRuleTests.TestSources_MustNotRebindConnStr_OutsideFixtureOrAllowlist` | 每次 `dotnet test`（快/中/深全档） | 动态拼连接名；豁免清单内文件（专项批清理） |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|------|------|------------|------------------------------|
| [2026-10-29-test-ctor-rebind-connstr](../cases/2026-10-29-test-ctor-rebind-connstr.md) | 2026-10-29 | PILOT-051 中档全量 74 红：goal/gates/snapshot 11 红全由五个测试类 ctor 换库引发；七轮全量迭代收敛到 52 基线红 | 否（首见即建类） |
