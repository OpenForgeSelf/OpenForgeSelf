# Intent

> Task ID: PILOT-079｜闸门1 依据：输入8 用户直接指令（2026-11-07 23:28「全部修复，再走一遍这个流程……测完改，改完再测，直到本工具能够自行开发完一个插件，本地运行的实例，权限可改为自动审批」）——按仓内先例「用户直接指令=闸门1 依据」执行，工件先行、指令即批。

## Problem
:51888 实例演示「宿主 Agent 自主开发 ClockBadge 插件」暴露 7 项缺陷（输入7，P1×2/P2×3/P3×2）：SmartTerminal DI 全灭（P1①）、上游空流静默 Suspended（P1②）、run_terminal_command 30s 硬上限+禁重定向（P2①）、审批 Decide 不唤醒（P2②）、调度 Once 过期不触发且无删除（P2③）、长会话上下文溢出永久 400（P3①）、多模型路由无健康降级（P3②）。这 7 项共同阻断「工具自主开发插件」的核心叙事：智能终端不可用、Agent 跑不了构建、出错不可感知不可自愈。

## Why
用户点名全部修复，并要求在**起源码本地实例**（而非 :51888）上重走插件开发全流程、测完改改完再测、直至 Agent 自主闭环，以验证修复并沉淀经验。这是把「演示暴露的缺陷」转化为「平台能力提升」的直接指令。

## Expected Outcome
1. 7 项缺陷全部修复，每项带红灯复现→绿灯测试，命中既有 PC 的按 problem-class-closure 闭环（PC-013 复发批、PC-005 同族扫查），新问题类归档。
2. 起源码本地前后端（dev-stack.ps1，隔离实例）可用，本地实例审批策略改自动审批。
3. 浏览器驱动本地实例的 Agent 完整开发一个新插件（含 Agent 自己跑构建），迭代到自主闭环成功。
4. 经验记录：case/PC 归档 + agent-workflow/技能回写。

## Constraints
- ⛔ 不停/启/杀用户运行中的 :51888 宿主；不发布、不 git 提交（闸门3 等用户）。
- 本地实例仅经 `scripts/dev-stack.ps1` 起/停；自动审批**仅限本地隔离实例**。
- 不改 DB 结构、不改鉴权核心；不新增依赖（ForgeSetting 配置扩展除外）。
- DSH 会话审批策略=never ask，sandbox_permissions 禁设；pwsh 7 only；测试判据看日志正文。
- 迭代控制：连续 3 次失败无新思路 → 停止升级给人。

## Success Criteria（可测）
- SC1 后端 `dotnet build` 0 错误（警告不超基线 474），`dotnet test` 全量对基线**零新增红**；涉及插件各自门禁绿。
- SC2 每项缺陷有红灯→绿灯证据（05-evidence Verified），同类扫查命中表齐，守卫落地（PC-013/PC-005 补强 + 新类归档）。
- SC3 `.temp/dev-stack.json` 报告的本地实例可访问，审批策略=自动审批。
- SC4 本地实例 Agent 自主完成一个新插件的后端+前端+构建+加载全流程（宿主侧 run_terminal_command 跑 dotnet/pnpm 构建成功），我全程控制浏览器，留截图与 SSE 证据。
- SC5 `pwsh scripts/check-problem-classes.ps1` 绿；07 报告三节齐（问题类归档/重构提案/悬空教训为空）。
