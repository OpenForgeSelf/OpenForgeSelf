# 05 Evidence — AIAgent MAF 空名工具调用毒化会话（PC-015 / 2.4.18 回归闭环）

> 日期 2026-10-29。证据分级：全部 Verified（亲自执行、逐字记录关键输出）。本文件是 case/PC 引用的证据落点（探针目录 `.temp/probe-chat-stream/` 按 §5.0 红线用后即删，原始输出关键行已逐字收录于此）。

## 1 验证执行清单

| # | 验证项 | 命令 | 结果 | 等级 |
|---|--------|------|------|------|
| V1 | TDD 红灯（修复前） | `dotnet test --filter FullyQualifiedName~ForgeChatClientAdapterTests`（ForgeSelf.Api.Tests） | 失败: 3，通过: 3，总计: 6 | Verified |
| V2 | TDD 绿灯（修复后，MAF 域四类过滤集） | `dotnet test --filter 'FullyQualifiedName~MafTaskLoopAgentTests\|FullyQualifiedName~ForgeChatClientAdapterTests\|FullyQualifiedName~ReactLoopTaskLoopTests\|FullyQualifiedName~ReactLoopAgentTests'` | 失败: 0，通过: 28，总计: 28（5 s） | Verified |
| V3 | 后端构建 | `dotnet build`（ForgeSelf.Api） | 0 个错误（337 警告 = 既有基线水位） | Verified |
| V4 | 问题类签名扫描 | `pwsh -File scripts/check-problem-classes.ps1` | OK：15 个 PC 签名/allowlist/guards 全部通过（含新增 PC-015 S1） | Verified |
| V5 | 台账完整性（守卫的守卫） | `dotnet test --filter FullyQualifiedName~ProblemClassRegistryTests` | 失败: 0，通过: 5 | Verified |

## 2 红灯关键输出（V1，tdd-red 阶段 14:20）

```
[xUnit.net 00:00:01.09] ForgeSelf.Api.Tests.Plugins.AIAgent.ForgeChatClientAdapterTests.StreamingToolCallFragments_MergeIntoSingleCompleteCall [FAIL]
[xUnit.net 00:00:01.09] ForgeSelf.Api.Tests.Plugins.AIAgent.ForgeChatClientAdapterTests.StreamingToolCallFragments_ParallelCallsMergedInOrder [FAIL]
[xUnit.net 00:00:01.10] ForgeSelf.Api.Tests.Plugins.AIAgent.ForgeChatClientAdapterTests.MapsRequest_SkipsEmptyNameToolCallsFromHistory [FAIL]
失败! - 失败: 3，通过: 3，总计: 6
```

三测分别断言：分片序列（首片 id+name+参数前缀 / 续片仅参数增量）→ 恰好 1 条完整 FunctionCallContent 且 args 拼接正确；平行调用 c1/c2 交错分片 → 按序 2 条完整调用；含空名调用的历史消息 → 出站请求 ToolCalls 仅剩有名调用。红灯期间行为 = 逐片产出 3/4 条（续片为空名 `{}` 调用）/ 空名调用照常出门，与生产缺陷形态一致。

## 3 绿灯关键输出（V2）

```
已通过! - 失败: 0，通过: 28，总计: 28，持续时间: 5 s - ForgeSelf.Api.Tests.dll (net10.0)
```

过滤集覆盖：MafTaskLoopAgentTests（含既有 task-loop 用例，确认聚合未破坏 logger/出口路径）+ ForgeChatClientAdapterTests（6）+ ReactLoopTaskLoopTests + ReactLoopAgentTests（老路径回归，确认 AccumulateToolCall 语义对齐无互扰）。

## 4 生产证据（现象侧，诊断阶段采集）

- 实例日志 `D:\src\tools\OpenForgeSelf\versions\2.4.18.2610290828\Log\2026_10_29.log`：13:07:50 收到 8 字消息；13:08:12.948 `[MafTaskLoopAgent] MAF Run 失败 SessionId=mvt30ipk-quraxi: Value cannot be an empty string. (Parameter 'functionName')`；13:08:58 / 13:09:42 两条 3 字消息均在上游请求日志后 1~2ms 同错、响应长度 0。
- 会话库 `C:\Users\12504\.forgeself\ForgeSelf.db`（只读探针）：SessionEvent #1098 ToolCalls=2 条真实 `list_files` 调用；#1104 ToolCalls=2 条真实 + 数十条空壳（`CallId:""`、`ToolName:""`、`ArgsJson:"{}"`，数量=参数流式续分片数）；#1099-#1110 为真实 tool/call+result 配对；#1120/#1128 为失败 run 记录。
- 用户 curl 中的 3 空格 message 是脱敏产物：实测该形态命中 `Plugins/AIAgent/Controllers/AIChatController.cs:217` 空消息守卫（HTTP 200 SSE `{"type":"error","content":"消息不能为空"}`），非用户真实报错。

## 5 覆盖声明（§5.6 档位）

- 本批跑的是**快档**（相关过滤集 + build + 台账机检）。触发分析：改动面 = `Plugins/AIAgent/MafLoop/*` + 测试文件 + 问题类文档；未碰 `ForgeSelf.Api/**` 宿主源码、`scripts/**`（check-problem-classes.ps1 只执行未修改）、共享测试基建 ⇒ 不触发中/深档。
- `Plugins/FileTools` / `Plugins/McpCenter` 本批只有**语法级踩踏修复**（恢复并行会话 cwd 必填化改造中被坏合并破坏的结构：补 `{`/`}`、去重属性/语句），随 V2/V3 一起编译通过；该域功能级 QA 归并行任务承担（已在 TODO.md 协作交接登记）。
- e2e：无 UI 变更，不适用。

## 6 环境前置（§5.0）

TEMP/TMP 已指 `<repo>\.temp\tmp`；测试判定一律看日志正文（Out-File 落盘后读），不拿 exit code 当证据。一次 MSB3027（残留 testhost 60560 锁 DLL）已终止该进程后复跑通过——属本机已知散例（`docs/11-troubleshooting/index.md` CS2012 行），非回归。
