# 01 Intent — 2026-11-07-json-chinese-raw-encoder

## Problem
用户输入6：项目 JSON 输出里中文被序列化成 `\u76EE\u5F55\u4E0A\u6EAF` 这种转义形态，人读不可读（工具结果回显、MCP 客户端、执行记录、落盘配置）。

## Why
- System.Text.Json 默认 Encoder 转义一切非 ASCII；仓内大量 `JsonSerializer.Serialize(x)` 无 options 调用走了该默认。
- 工具结果 JSON 直接喂给 LLM 与回显 UI，转义形态损害可读性与模型理解。

## Expected Outcome
全仓（宿主 + 22 插件 + 共享层）**JSON 写出路径**中文等非 ASCII 字符保持原文直出；落盘给人读的配置维持缩进；接口/DB 载荷不加缩进（体积与兼容不变）。

## Constraints
- 只改**写出转义行为**：命名策略、结构、字段、缩进（除既有的）一律不动 ⇒ 零契约漂移。
- 不引入新依赖（`System.Text.Encodings.Web` 属共享框架）。
- 不改反序列化路径行为（`\uXXXX` 与原文两种形态 STJ 均可读，双向兼容）。
- 不动 `ForgeSelf.Web/**`、e2e、`plugin.json` 版本号、`Data/Model.xml`。

## Success Criteria
守卫（G2 源码枚举）修复前红、修复后绿；`ForgeJson` G1 单测绿；`dotnet build` 0 error；全量 `dotnet test` 新增红 = 0；工件链 00-07 齐 + PC-027 归档。

## 闸门1 依据
用户直接指令原文即规格（「设置不要序列化成 \uXXXX 这种，直接保持中文原文」）；本会话审批策略 never-ask，无法逐工件请批，以该指令为批准依据留痕，验收走闸门2。
