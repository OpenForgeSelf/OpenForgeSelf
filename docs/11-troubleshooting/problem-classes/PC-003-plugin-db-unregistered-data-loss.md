---
id: PC-003
title: 插件库连接名未登记 XCodeConfig.PluginDbs，数据落版本目录、宿主升级即丢
tags: [plugin-data, xcode, packaging]
invariant: 插件使用的每个 XCode 连接名（BindTable/const ConnName/DAL.Create 字面量）必须登记在 ForgeSelf.Api/Data/XCodeConfig.cs 的 PluginDbs；未登记 → 连接串缺省派生 {程序基目录}/Data/{conn}.db，发布态=versions/<ver>/Data/，升级换目录后旧库不再被读
status: guarded
first_seen: 2026-10-09
recurrences: [2026-10-09-agenthub-db-unregistered-data-loss]
cases: [2026-10-09-agenthub-db-unregistered-data-loss]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs}
allowlist: []
guards:
  - {level: G2, path: ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs, status: active}
lens:
  - 新插件带持久库吗？连接名进 PluginDbs 了吗（key=PascalCase 连接名，value=kebab Id，与 plugin.json 一致）？
  - 实体 ConnName / 建表脚本 DAL.Create 的字面量与登记 key 逐字一致吗（大小写敏感）？
  - 存量迁移走了修复指引吗（旧版本目录库拷回数据根 + 登记 + 重启）？
---
# PC-003 插件库未登记 PluginDbs → 升级丢数据

> 唯一真源补充：目录结构/生命周期规则本体在 `docs/04-standards/packaging-upgrade-backup.md`；`ForgeSelf.Api/Plugins/README.md` §三 是数据落盘操作指引。本文件承载「这类缺陷」的不变量与守卫，事件经过见 `../cases/2026-10-09-agenthub-db-unregistered-data-loss.md`。

## 1 不变量与诱因

`PluginDbs` 是插件库落数据根（`~/.forgeself/plugins/{id}/{conn}.db`）的**唯一登记入口**（`XCodeConfig.AddXCode` 按 `DbFiles` 派生连接串，README §三 2026-10-09 实证段）。诱因：插件侧自带 `Data/<X>Tables.cs` 建表 + `DAL.Create(ConnName)` 就能跑通开发态（Debug 基目录下 Data/ 看起来"正常"），登记这一步散落在 README，没有任何机器检查——AgentHub/ImGateway 两个插件同时漏登记，直到用户实例升级后「agent 登记数据全丢」才被现场逮住（2.3.2/2.3.3 库 512/385KB → 2.3.4 起每版 57KB 空库）。

## 2 特征签名与扫描方法

G2 枚举测试 `ProblemClassEnumerationTests.PluginDalConnNames_MustBeRegisteredInPluginDbs`：扫两棵插件树全部 `.cs` 的 `ConnName = "X"`（const 与 `[BindTable(... ConnName = "X")]`）与 `DAL.Create("X")` 字面量，断言集合 ⊆ `XCodeConfig.PluginDbs.Keys`；阳性对照 = 命中数 ≥10（防空转）。盲区：字符串拼接出的连接名（现存无此写法，出现即人工核）。

## 3 Canonical fix

`XCodeConfig.cs` `PluginDbs` 加一行 `["连接名"] = "kebab-id"`；存量迁移 = 把有数据的旧版本目录 `Data/{conn}.db` 拷到 `~/.forgeself/plugins/{id}/` + 登记 + 重启宿主。参考实现：2026-10-09 修复（AgentHub/ImGateway 两行 + 注释记录实证）。被否方案：插件运行时自查自注册连接串（绕过单点真源，制造第二套登记面）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `ProblemClassEnumerationTests.cs` | dotnet test 全量 / CI | 拼接连接名（见 §2） |
| — | `XCodeConfigTests.cs`（既有，只断条目形态） | dotnet test | 不校验插件侧引用关系——正是本类的缺口所在 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-10-09-agenthub-db-unregistered-data-loss | 2026-10-09 | 升级 2.3.4 后 AgentHub 登记列表空（库留在旧版本目录） | 否（首见；README §三 有文字规则但无守卫） |
