---
id: PC-022
title: XCode 实体直插漏赋时间戳字段，落库为 0001-01-01 缺省值
tags: [xcode, sqlite, timestamp, memory]
invariant: 含 CreatedAt/UpdatedAt 字段的 XCode 实体在代码直接 new/Update 时必须显式赋时间戳；真库回读不得出现 0001-01-01。
status: guarded
first_seen: 2026-11-02
recurrences: [2026-11-02-memory-timestamp-minvalue]
cases: [2026-11-02-memory-timestamp-minvalue]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Api.Tests/Integration/MemoryTimestampGuardTests.cs"}
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Api.Tests/Integration/MemoryTimestampGuardTests.cs", status: active}
lens:
  - 新的实体直插路径（new X{...}）是否漏赋 CreatedAt/UpdatedAt？
  - 服务实现层是否只有 mock 测试、没有真库守卫（时间戳缺陷 mock 测不出来）？
---

# PC-022 XCode 实体时间戳缺省 0001-01-01

## 1 不变量与诱因

XCode 实体不会自动填充时间戳字段：代码 `new X{...}` 直插时漏赋 `CreatedAt/UpdatedAt`，SQLite 落库即 `0001-01-01T00:00:00`；`Update()`/软删前不设 `UpdatedAt`，审计时间也不推进。诱因是「同文件里已有一处正确写法（CorrectAsync），其余写入点没有对齐」——同类遗漏未扫查。

## 2 特征签名与扫描方法

按「实体是否含 CreatedAt 且由代码赋值」扫插件服务层所有实体直插点；真库守卫断言新建/导入回读时间非 MinValue、更新推进 UpdatedAt。盲区：mock 了 IMemoryService 的集成测试完全测不出本类缺陷（MemorySystemIntegrationTests 即全 mock）。

## 3 Canonical fix

每个写入点显式赋值：新建/导入补 `CreatedAt = UpdatedAt = DateTime.Now`；更新/软删在 `Update()` 前补 `UpdatedAt = DateTime.Now`。长期：XCode 直插收敛到统一写入入口（实体基类或服务层 SetTimestamps），见重构提案。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `MemoryTimestampGuardTests.cs`（[Collection("XCode")] 真库） | 后端测试 | 仅覆盖 Memory 实体；其他插件实体的插入路径待同类巡检（TODO P3） |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-11-02-memory-timestamp-minvalue | 2026-11-02 | 记忆卡片「创建时间 1/01/01 00:00」，API 回读 createdAt/updatedAt 全为 MinValue | 否（走查首次发现并建档） |

## 6 重构提案

统一时间戳写入入口（实体基类 OnInsert 钩子或服务层 SetTimestamps 助手），随「插件插入路径时间戳巡检」（TODO P3）评估落地。

## 7 悬空教训

- 服务实现层零真库覆盖时，持久化细节缺陷只能靠真实走查暴露——新增关键写路径必须配 [Collection("XCode")] 真库守卫。
