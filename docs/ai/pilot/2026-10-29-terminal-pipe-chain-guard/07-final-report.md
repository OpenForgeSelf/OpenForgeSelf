# AI-Native Pilot Result（最终汇报）

> 任务：执行命令工具放开管道与链式（PILOT-075）。状态：代码闭环+验证全绿，**闸门2 待用户验收；验收通过且用户同意前不 git 提交、不发布**。

## 1. Repository Understanding

我确认了：命令拦截为双实现（McpCenter `TerminalCommandGuard` 348 行 + ToolBridge `CommandGuard` 318 行同构对账，不可合并因共享层无 NewLife 依赖）；执行层单段直启 Process（PC-011 原始字节解码）；文档真源 021-ai-agent.md / 039-tool-bridge.md；测试体系 xUnit+FluentAssertions（快档过滤集）+ Playwright 插件层 e2e（零 mock）。

## 2. Selected Task

输入5（2026-10-29 15:31）：「关于执行命令工具，关于命令的拦截，禁止管道和链式不合理，放开支持，每一个管道或者链式，应都走同样的拦截校验」→ 闸门1 经用户 16:12 批准「批准，按方案开工」。

## 3. Changed Files

16 项见 04-task front-matter：两守卫 + 两执行器 + 文案四文件 + 两 plugin.json（2.9.0/1.1.0）+ 五个测试文件 + 新测试类 + 两特性文档 + mcp-center e2e 冒烟用例。

## 4. Validation

Build:

- 四工程 `dotnet build` 全部 0 error（Verified；中间 CS1628/CS0022 两轮编译红已修）。

Unit Test:

- 快档过滤集（TerminalCommandGuard|ToolBridge|RunTerminalCommand|McpCenterTests|Parity）：**431/431 通过，0 失败**（Verified，.temp/test-p075-r3.log）。首轮 35 红为我引入的 FlushSegment 丢 `result.Add(seg)` 自伤 bug（分段恒空→逐段循环零次→一切放行），修复并修正 2 处测试期望（E3 独立消息；段内门序=白名单先于红线）后全绿。

E2E:

- `mcp-center.spec.ts` PILOT-075 冒烟：**1 passed (55.6s)**，真实 publish 宿主零 mock——链式放行（stdout 实含 `git version 2.53.0.windows.2`+`v24.16.0`）与段级拒绝（`segment 2/2: ... allowlist: python`）双证据（Verified，.temp/e2e-p075.log）。

## 5. Evidence

docs/ai/pilot/2026-10-29-terminal-pipe-chain-guard/05-evidence.md——AC1-AC11 逐条对照（含两处计划偏差披露）；截图 `ForgeSelf.Web/screenshots/dev-walkthrough/2026-10-29-p075/`（v2.9.0 徽标 + 新描述 + 网关 18895 真实双链日志）。

## 6. Review

06-review.md：八问全过、四 Check PASS、Minor×2（既有陈旧常量记 TODO；门序原因词披露）、**Final Decision: APPROVED**。

## 7. Risk

L1（对外语义放宽=用户明确指令；安全强度未降：逐段同一门 + 51 行金样表对账 + 拒绝路径零子进程探针反证）。

## 8. Problems Found

- 自伤 bug（分段器丢段）——同轮测试即抓（35 红实证），修复后金样表+阳性对照钉死。
- `git status; del *` 原因词为 allowlist（段内门序）：披露于 06-review Minor，行为语义无损失。
- McpCenter 网关 serverInfo.version 常量 2.7.0 陈旧 → TODO.md（P3）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS（双实现定位一次到位，纠正了「AIAgent 持有守卫」的文档陈旧认知） |
| Intent → Spec | PASS |
| Spec → Plan | PARTIAL（AC2 原因词、AC11 测试台手段两处预判失准，实现期如实披露并走等效证据） |
| Plan → Code | PASS（含 2 轮编译红自修） |
| Code → Test | PASS（自伤 bug 当轮被抓即修，零逃逸） |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 问题类归档

无 + 理由：本批为功能语义变更（非缺陷/回归/返工类）；实现期自伤 bug 在同一验证轮内即被既有门禁抓获并修复，未形成逃逸或返工，无可归档的问题类（不满足 PC 立类条件：非跨任务复发的机制性缺陷）。

## 11. 重构提案

R0 + 理由：两守卫不合并是既有架构裁决（D1，共享层不引 NewLife；合并=依赖结构变更须另立 ADR），本批以同构镜像+金样表对账维持反漂移；执行层双路径（单段直启/多段 pwsh）为最小侵入实现，无跨模块结构收益支撑一次重构。

## 12. 悬空教训清单（必须为空）

（空——Minor 两项：serverInfo.version 常量已落 TODO.md P3 条目；门序原因词已在 06-review/05-evidence 披露为已知语义无待办动作。）
