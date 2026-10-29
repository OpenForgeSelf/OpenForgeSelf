# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，不提前锁定具体代码实现。
> Task ID：PILOT-051 ｜ 日期：2026-10-29

## Problem

1. **聊天 Agent 停得太早**：用户在 AIAgent 对话页发一个需求，Agent 调用工具、回一段文本就结束回合；不像 Claude Code / Devin / Manus 等同类产品那样持续工作直到任务真正完成（多轮工具调用 → 阶段汇报 → 继续 → 显式宣告完成）。
2. **没有目标驱动执行**：用户无法「设定一个目标 + 验收标准，让 Agent 一直做直到完成目标或满足目标要求」；现有计划驱动 Run 做完预设步骤即 Completed，没有「目标达成度评估 → 不达标自动补步骤/续轮」的闭环。

## Why

- 这两条是个人 Agent 平台（铸己匣）的核心体验差距：同类行业标准方案已收敛为「agentic loop + 显式完成信号（finish/complete 工具）+ 目标/验收评估环」，本项目只有前一半（工具循环）且终止判据错了（用「模型不再调工具」当「任务完成」）。
- 用户明文指令：参考行业标准方案，自行调研、自行决策开发、不用问。

**行业标准调研结论（已核实）**：
- Microsoft Agent Framework（MAF，本仓库已引用 1.6.1）：`ChatClientAgent`/`AIAgent.RunAsync` 提供 function-calling 自动循环；循环终止由「模型不再请求工具调用」或 `FunctionInvocationContext.Terminate` 决定；`ChatToolMode.RequireAny/RequireSpecific` 可强制工具调用；`AgentResponseUpdate` 流式翻译已有参考实现（`AgentStreamTranslator`）。
- 「做完才停」的通用模式（OpenAI Agents SDK / LangGraph / Claude Code Stop-hook / smolagents 同型）：挂载**完成出口工具**（task_complete），约定「未调用它之前不许停」；模型只出文本不调工具时注入续跑指令并 `ToolMode=required` 强制其继续行动，步数上限兜底。
- 目标驱动执行通用模式（evaluator-optimizer / plan-and-execute + verify，LangGraph/AutoGPT/Devin 同型）：目标 + 验收标准 → 执行 → **评估器判定达成度** → 未达标生成补救步骤继续，预算（最大轮数）兜底，评估不过则升级给人。

## Expected Outcome

1. AIAgent 对话（FreeLoop，每个会话 Agent）：发需求后 Agent 持续多步工作；只要本回合用过工具且模型只出文本，就自动续跑（注入续跑指令 + 强制工具调用），直到模型调用 `task_complete` 出口工具或触达步数上限。纯问答（整个回合零工具调用）行为不变。
2. 循环引擎按用户要求建立在 MAF 的 Agent 之上：聊天路径的回合由 MAF `ChatClientAgent`（自动 function-invocation 管线）驱动，宿主工具注册表经 AIFunction 适配接入；同时既有 `ReactLoopAgent` 获得同一套任务循环语义作为后备引擎（配置开关可切回）。
3. 目标模式：计划驱动执行（/api/ai-agent/runs）支持「目标 + 验收标准 + 最大轮数」；步骤全部完成后由 MAF 评估 Agent 按验收标准判定；未达标自动追加补救步骤并续跑；预算用尽仍未达标 → Run 进 Stuck（带未达成报告，可人工介入/恢复）。
4. 前端最小可用：新建执行表单支持目标模式（验收标准多行 + 最大轮数），运行记录面板展示目标轮次/评估结论事件。

## Constraints

- 规范 §1 硬约束逐条对照：不改生产、**不改 DB 结构**（目标配置全部进既有 `AgentPlan` JSON，不新增实体/列）、不碰鉴权/权限/支付、**不新增大规模依赖**（MAF 1.6.1 + M.E.AI 10.5.1 已在宿主 NuGet 图内，插件引用同版本为既有依赖的接线）、不做无关重构（ReactLoopAgent 只做任务循环语义必需的最小修改）。
- 保持 00-artifact 列出的 5 条运行时不变量（日志唯一写路径、派生纯函数、出口工具声明即出口、步数上限、看门狗）。
- 兼容：未开启任务循环/目标模式的行为与现状逐字节兼容（存量会话、存量 Run、e2e 基线不回归）。
- 遗留面 L3/L5 不动（B7 合法保留），仅记入不做事决策。

## Success Criteria（可验证）

- SC1 后端单测：脚本化 provider 驱动 MAF 运行时，验证「工具→纯文本→续跑→task_complete 收束」全链路帧序/日志序；纯问答路径行为不变；`ReactLoopAgent` 后备引擎同样语义。
- SC2 后端单测：目标模式「步骤完成→评估未达标→补步骤→再评估→达标→终局合成」状态流转 + 预算耗尽 → Stuck。
- SC3 `dotnet build` 绿 + `dotnet test` 过滤集绿（改宿主源码 → 中档全量对表无新增红）。
- SC4 插件前端 `pnpm run build` 绿；新建执行表单与事件渲染可用（e2e 冒烟 1 条主链路）。
- SC5 闸门2 齐备：05-evidence/06-review 落盘，AC 逐条有 Verified 证据。

## 闸门1 裁剪记录

用户输入原文明文授权「自行调研、自行决策开发，不用问我」→ 本任务按规范 §1.1「仅当用户明文授权免闸门时方可裁剪」处理：**闸门1 以该授权视为已批**（工件 00-04 仍完整产出，作为决策依据留痕）；闸门2 仍按规范执行（用户验收前不提交 git）。
