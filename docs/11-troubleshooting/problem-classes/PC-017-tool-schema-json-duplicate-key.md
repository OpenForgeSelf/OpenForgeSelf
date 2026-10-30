---
id: PC-017
title: 插件工具 ParametersJsonSchema 重复键少闭合括号，该工具经网关的任何调用全挂
tags: [plugin-tools,schema,json,gateway]
invariant: 所有注册给 AI 工具面的 ParametersJsonSchema（含运行期拼接产物）必须是可解析的 JSON 对象；网关/ToolRegistry 在该工具任何调用前解析它，坏一处 = 该工具对全部客户端不可用。
status: guarded
first_seen: 2026-10-30
recurrences: []
cases: [2026-10-30-tool-schema-json-duplicate-key]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/Architecture/ToolSchemaJsonGuardTests.cs}
allowlist: []
guards:
  - {level: G2, path: ForgeSelf.Api.Tests/Architecture/ToolSchemaJsonGuardTests.cs, status: active}
lens:
  - 新增/改写工具 ParametersJsonSchema 后，是否跑了 ToolSchemaJsonGuardTests（C# 编译器不校验 verbatim 字符串内容，构建绿 ≠ schema 合法）？
  - 拼接型 schema（字符串相加/方法拼接，如 DesignSystem）是否已有实例级解析覆盖？（尚未，见悬空教训 TODO）
  - 合并/多会话修复批之后，被修文件是否重跑了全插件 schema 守卫（撞车残迹常以「重复行」形态回归）？
---
# PC-017 插件工具 ParametersJsonSchema 重复键少闭合括号

> 事件经过见 `../cases/2026-10-30-tool-schema-json-duplicate-key.md`。

## 1 不变量与诱因

**不变量**：工具参数 schema 是网关每次调用该工具前必经的契约文本（ToolRegistry 对 ParametersJsonSchema 做 `JsonDocument.Parse`）。它必须永远是可解析的 JSON 对象——坏一处，该工具对全部客户端（万能工具/dsh 桥/页面 Agent）不可用，且报错（「Expected depth to be zero at the end of the JSON payload」）完全不给工具名与文件出处，极难定位。

**诱因**：schema 以 C# verbatim 字符串（`@"..."`，引号双写）内嵌在源码里。①编译器只验字符串语法、不验 JSON 内容，**构建全绿照样带病**；②多会话并行改同一文件时，合并残迹以「重复行」形态出现——本例 `"path": {` 键连写两行，多出的 `{` 吃掉一个闭合括号，肉眼极难在 20 行 schema 里发现；③修复撞车批（2026-10-29）的验收口径是「构建绿」，没有任何测试把 schema 字符串当契约。

## 2 特征签名与扫描方法

- **S1（test，G2）**：`ToolSchemaJsonGuardTests` 三用例——①源码级枚举：上溯到 ForgeSelf.slnx，提取 Plugins/**/*.cs 全部 verbatim schema 字符串（含 `""`→`"` 还原）逐个 `JsonDocument.Parse`，阳性对照要求扫到 >40 个；②file-tools 实例级：构造 4 个工具对象直接 Parse；③write_file 五参必填契约锁。
- **盲区**：运行期拼接型 schema（字符串相加/方法调用，如 DesignSystem 的 `"{...}" + StyleAxes.SchemaProperties() + "..."`）无法源码提取，需实例级枚举扩展（记 TODO，见 case §9）。

## 3 Canonical fix

删除重复键行，使 schema 括号配平。参考实现：`Plugins/FileTools/Services/FileToolFunctions.cs:242`（修复后只剩单个 `"path": {`）。被否方案：网关侧 catch schema 解析失败后按「无 schema」放行——会把坏契约静默扩散到所有调用方，把确定性失败变成随机行为，掩盖缺陷。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `ForgeSelf.Api.Tests/Architecture/ToolSchemaJsonGuardTests.cs` | `dotnet test --filter ToolSchemaJsonGuardTests`（插件任务快档） | 运行期拼接型 schema（DesignSystem 等，待实例级扩展）；已发布二进制不在源码扫描范围 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|------|------|------------|------------------------------|
| 2026-10-30-tool-schema-json-duplicate-key | 2026-10-30 | write_file 经万能工具 3 连挂，`Expected depth to be zero…LineNumber 26`，任何参数都一样 | 否（首见，此前无守卫） |
