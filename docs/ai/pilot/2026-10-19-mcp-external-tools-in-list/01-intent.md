# Intent

> 阶段：Stage 1 ｜ Task ID：PILOT-072 ｜ 日期：2026-10-19

## Problem

接入外部 MCP 服务器（如 deepwiki）后，其工具**在枚举侧和内置消费侧都不可见/不可用**：
- `list_tools`（万能工具的「发现工具」入口）只枚举宿主注册表，外部工具不在返回里——调用方无从知道有什么外部工具、叫什么名；
- 内置数字助手（AIAgent）的 `universal_tool` 没有 `mcp.` 前缀路由，对话中直传 `mcp.deepwiki.ask_wiki_question` 落「未知工具」——只有网关侧（外部 MCP 客户端）能直调。

用户原话：「不然注册了外部 mcp，在工具内外都用不了那添加来干嘛」。

## Why

外部 MCP 接入功能的价值闭环 = 「添加 → 发现 → 调用」。现状断在「发现」（list_tools 不含）与「内置调用」（AIAgent 无路由）两处，功能形同半成；2026-10-18/19 两次用户困惑均源于此（list_tools 查无 → 追问怎么直调）。

## Expected Outcome

1. `list_tools` 返回 = 宿主注册表全部工具 + **已连接外部 MCP 服务器的全部工具**（名字即 `mcp.<服务器id>.<工具名>` 全名，含描述；`includeSchema` 时含参数 schema）；关键字过滤对两类条目同规则生效。
2. 内置 AIAgent 的 `universal_tool` 能直接调用 `mcp.*` 工具（与网关同语义：转发外部会话、失败返回错误封套）；AIAgent 经注册表转发到的 `list_tools` 自动含外部工具（同一份实现）。
3. 网关侧行为不回归（转发、错误封套、既有 e2e 全绿）。

## Constraints

- 规范 §1 逐条适用：不改数据库、不动鉴权、不扩无关重构。
- **不推翻 D10 的注册表隔离**：外部工具仍然**不进** `IToolRegistry`（避免宿主工具装配被插件生命周期污染、避免同名冲突与使用统计误算）——本批改的是「枚举出口合并 + 调用路由」，注册表边界保持。跨插件能力供给按既有裁决（Abstractions 契约 + `ctx.Register`/`ctx.Get` 软依赖，可降级）。
- McpCenter 未启用/未连接任何外部服务器时，一切行为与现状一致（纯增量，零破坏）。
- 与未发布的 v2.5.2（超时可配置）批协调：同插件改动，版本合并或顺延一拍，发布时合并发版。

## Success Criteria

- 单测：连上外部 mock 后 `list_tools` 含 `mcp.<id>.<tool>` 条目（名/描述/按 keyword 过滤/includeSchema）；无网关能力时不报错、结果与现状一致。
- 单测：AIAgent `universal_tool {tool:"mcp.<id>.<tool>"}` 走外部路由（假契约验证转调与错误封套）；契约缺失时报明确中文错误，不落「未知工具」。
- e2e：mock 服务器连接 → 经网关 `universal_tool {tool:"list_tools"}` 断言含 `mcp.<id>.*` 全名 → `mcp.<id>.echo` 实调成功。
- `dotnet build` 0 错 + McpCenter/AIAgent 相关单测全绿 + mcp-center 冒烟 e2e 绿。
