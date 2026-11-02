---
id: PC-023
title: 执行记录 filesChanged 断链——自动留痕无参数 + 双编码 JSON 裸正则永不命中
tags: [todo-tracker, fileschanged, persistence, regex, xcode]
invariant: 执行记录（手动 AppendAsync 与系统 AppendSystemAsync）必须携带结构化 FilesChanged；凡从落库 JSON 提取结构化字段必须先反序列化再取值，禁止裸正则扫双编码串。
status: guarded
first_seen: 2026-11-02
recurrences: [2026-11-02-fileschanged-persistence-loss]
cases: [2026-11-02-fileschanged-persistence-loss]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Api.Tests/Plugins/TodoTracker/BuiltInFilesChangedPersistenceTests.cs"}
  - {id: S2, kind: grep, glob: "Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs", regex: "new Regex", expect: no-match}
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Api.Tests/Plugins/TodoTracker/BuiltInFilesChangedPersistenceTests.cs", status: active}
lens:
  - 新增留痕/回写路径是否把结构化字段（FilesChanged 等）透传到落库，还是止步于 DTO？
  - 从 ToolCallsJson/ArgumentsSummary 这类「序列化再套字符串」字段提取数据，走的是结构化反序列化还是裸正则？
  - 接口参数面（ITaskExecutionService）与实现是否同步演进——实现多出的可选参数即潜在的静默丢弃点？
---

# PC-023 执行记录 filesChanged 断链

## 1 不变量与诱因

执行记录是「agent 说它做完了」的唯一台账：结构化 `FilesChanged`（JSON 数组）必须与记录同批落库，手动路径与系统路径同一不变量。诱因有二，叠加后缺陷静默：① `AppendSystemAsync` 最初就是「无请求体的轻量留痕」签名，没带 `filesChanged`，后来状态快照有了结构化清单也没有人回头对齐参数面（接口演化不同步 = 静默丢弃点）；② `ToolCallsJson` 的落库形态是「轨迹列表序列化后再套一层 JSON 字符串」，双编码后 `path":` 变成 `path\\\":`，裸正则永不命中且**不报错**——提取失败被当成「本来就没有文件」。

## 2 特征签名与扫描方法

按「调用 AppendSystemAsync 的点是否携带结构化数据」+「谁在读 ToolCallsJson 提取字段」两个面扫：前者全点过（19 个调用点，仅自动回写 1 点携带文件数据）；后者全库过 Regex（其余为纯文本解析非双编码形态）。机器签名 S2 = 快照提取文件内不得再出现 `new Regex`（取证一律走 ToolCallFileExtractor 单点）。盲区：mock 测不出（丢字段是持久化行为），必须 [Collection("XCode")] 真库回读断言非空。

## 3 Canonical fix

① 结构化提取单点化：`ToolCallFileExtractor`（反序列化轨迹 → 逐条解析 ArgumentsSummary → 反转义正则仅兜底），快照/收据/验收共用一个真源；② 接口与实现对齐：`ITaskExecutionService.AppendSystemAsync` 补 `filesChanged` 参数，`FileChangeList.ToStorage` 归一；③ 调用点透传（`AppendAdvanceRecordAsync` 传 `status.FilesChanged`）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `BuiltInFilesChangedPersistenceTests.cs`（[Collection("XCode")] 真库，快照提取 + 自动回写落库 2 用例） | 快档过滤集 + pre-commit 全量 | 仅覆盖内置 Agent 链路；外部 AgentHub 委派回写的 filesChanged 语义（Hub 侧是否带清单）未覆盖 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-11-02-fileschanged-persistence-loss | 2026-11-02 | 5 条「agent 执行回写」记录 filesChanged 全空，快照提取段裸正则打不中双编码 JSON + 系统留痕段无参数可传 | 否（用户 TODO 回报后建档，B2① 先红后绿修复） |

## 6 重构提案

「轨迹取证」已有单点（ToolCallFileExtractor），下一步把 `AgentStepRun.ToolCallsJson` 的写入收敛为统一的轨迹序列化入口（带 schema 版本号），让「双编码」从落库形态上不可表达（G4 方向）；同时评估 `ArgumentsSummary` 改存结构化 JSON（非转义字符串），消除反转义兜底。

## 7 悬空教训

- 「提取失败静默返回空」与「本来就没有文件」在数据上不可区分——取证类代码要么带告警日志+结构化降级标记，要么有守卫测试钉住非空形态，二者必居其一。
- 接口演化的静默面：实现类新增可选参数而接口未同步（本次反向：接口先行会 CS，实现先行不会）——凡「手动路径有、自动路径没有」的成对 API，改任何一个必须同名扫另一个。
