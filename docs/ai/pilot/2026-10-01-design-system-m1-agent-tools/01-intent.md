# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，不提前决定具体代码实现。
> Task ID：PILOT-ds-m1-agent-tools ｜ 日期：2026-10-01

## Problem

设计插件（design-system v2.7.1）的后端很完整，但**只有人经浏览器能用**：

1. 没有任何 agent / MCP 工具——外部 agent（经 McpCenter 网关）和本工具内置 agent 都拿不到设计系统，"后续项目开发，UI/UX 一切依据该插件产出的设计系统"这条链路根本没有入口。
2. 没有「开发后审查」能力：令牌级审计只盯设计系统自己的数据（对比度/别名/命名…），不能回答"这段业务代码有没有偏离设计系统"。
3. 没有面向不懂设计的人/agent 的入口：创建设计系统要手填 hue/chroma/typeRatio 等专业参数，也没有"给一句需求就推荐风格"的能力。
4. 内置 agent 的工具白名单只含 `ai-agent` + `memory-system`，设计工具即使注册也默认够不着。

## Why

用户目标（2026-09-30 原话）：设计插件要能"提供 mcp 工具，给外部或者本工具 agent 使用"，让专业设计、不懂设计的程序员、完全外行都能产出令人满意的设计系统；开发完页面的"审查也可作为依据"，设计系统是 UI/UX 的"唯一真源"。M1 先补上这条**机器可用**的底座，M2（向导/展厅/接入界面）与 M3（风格轴/UX 规范）都复用它。

## Expected Outcome

- 设计插件向宿主工具注册表暴露 8 个 `design_*` 工具：外部 MCP 客户端可经 `universal_tool` 枚举并调用；内置 AIAgent 默认可见。
- agent 能：读设计说明书（唯一真源）、查令牌/组件/最近令牌/导出文本、审查一段业务代码是否合规、跑令牌级审计、按预设/参数创建并生成设计系统、改令牌/重新生成/发布。
- 界面能做的、REST 能做的、工具能做的**同源同结果**（不是三套实现）；写类能力可一键关闭（只读模式）。
- 新增两种导出格式（面向 AI 的紧凑设计说明书 `brief`、可粘贴进 AGENTS.md 的 `agent-rules`），进 bundle。
- 一份给 agent 的使用技能 `design-system-consume`，已登记 AGENTS.md §2.4。

## Constraints

对照规范 §1 硬性约束逐条：

1. 不改生产环境；agent 不停/启用户宿主（AGENTS §0 发布规范）。
2. **不改数据库结构**（M1 不动 `Model.xml`；`DesignGuideline` 表属 M3，须在 M3 闸门1 单独批）。
3. 不改鉴权/权限/支付/安全核心逻辑：新增 REST 端点复用控制器类级 `ApiKeyPolicy`；审查只吃**内联内容、绝不读服务器磁盘**；写类工具默认允许但可关闭。
4. 不新增大规模依赖（不加 NuGet/npm）。
5. 不做无关重构：仅为"工具与 REST 同源"抽出 `GenerationService`（控制器 `Generate` 改调它，响应形状不变）。
6. 不改与本任务无关的文件；插件前端（`web/`）M1 不改。
7. 不为展示能力扩范围：展厅/向导/规范/风格轴属 M2/M3。
8. 必须真实跑测试/构建验证；失败不伪造。
9. 数据安全铁律 10：工具不提供任何删除；归档=软删；不删库文件。
10. 内置 agent 白名单只加 `design-system` 一个插件 id（用户已选 A），不恢复 `universal_tool`（权限扩大，另评估）。

## Success Criteria

（详见 02-spec.md 的 AC 全表；摘要）

- `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem"`：新增用例全绿、存量 0 回归，报告总数 == `--list-tests` 发现数。
- 工具集精确等于 8 个 `design_*`；经 `McpJsonRpcHandler` + 真实 `ToolRegistry` + `UniversalToolForwarder` 往返可调用；`list_tools keyword=design` 枚举 8 个。
- `design_context` 中的语义色 hex == `tokens/effective` 真值（同源）；`design_review` 对生成产物自己的消费样例 0 error，对每条规则的反例必响；`design_create apply=false` 不落库、`apply=true` 落库且审计无 critical。
- 写开关关闭后，所有写类动作被拒且给出可执行原因。
- AIAgent 白名单含 `design-system`，其余插件工具仍不可见；`ToolAllowlist` 仍可裁剪。
- 新 e2e（真实宿主）直连 MCP 网关验证上述关键判据并截图/日志留证；插件层现有 e2e 无新增红。
