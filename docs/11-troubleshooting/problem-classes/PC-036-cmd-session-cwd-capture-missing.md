---
id: PC-036
title: cmd 会话执行后 cwd 采集缺失（交互式 %VAR% 解析期展开 + 响应无 cwd 字段 + 前端不回写 + 提示符轮询不消费）
tags: [smart-terminal, session-shell, cwd-tracking, cmd]
invariant: 终端会话每条命令执行后，会话 cwd 必须反映 shell 真实当前目录，且经响应字段（SubmitCommandResponse.Cwd）下行、由前端回写展示。cmd 因交互式 %VAR% 在整行解析期展开（命令执行前），同行采集不可行——必须用两行采集协议（标记行结束后再发 echo <token>::%CD%，第二行在命令完成后才被解析展开）；PowerShell/POSIX 沿用 ::cwd 单行协议
status: guarded
first_seen: 2026-11-15
recurrences: [2026-11-15-st-ux-defects]
cases: [2026-11-15-st-ux-defects]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/StStartDirAndSessionShellTests.cs}
allowlist: []
guards:
  - {level: G1, path: ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/StStartDirAndSessionShellTests.cs, status: active}
lens:
  - 新增 shell kind 时，BuildWrappedCommand/Parse 分支是否配套 cwd 采集与解析？RefreshCwd 探针命令对该 shell 是否存在（cmd 无 pwd）？
  - 新增/改动的命令执行响应 DTO 是否携带 cwd？前端命令执行与审批两条路径是否都回写会话态？
- 前端展示元素若优先消费「会话创建时固化的快照字段」（如 `s.prompt`），blocks/列表轮询是否同步消费服务端刷新后的同名字段？（快照优先 + 轮询不消费 = 显示层永久陈旧）
  - 改动 SessionShell 的解析逻辑时，是否保持标记 token 唯一性与「回显行在前、真输出行在后」的 LastIndexOf 语义？
---
# PC-036 cmd 会话执行后 cwd 采集缺失

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。

## 1 不变量与诱因

会话层 cwd 展示是用户判断「我在哪」的唯一锚点。诱因四层叠加：① 交互式 cmd 的 `%VAR%` 在**整行解析期**展开（命令执行前），包裹命令同行拿不到执行后的 `%CD%`；实测 `call echo`、`%%` 转义在交互态均不可用（batch 语义 ≠ 交互语义），原作者退而用 `!ERRORLEVEL!` 延迟形式又未开 /V:ON，exit 解析恒 0、cwd 采集从未存在；② `SubmitCommandResponse` DTO 无 cwd 字段——服务端 PowerShell 分支已更新 `s.Cwd` 但响应不下行；③ 前端 tab 徽标只在建会话时设置，命令执行/审批后不回写；④ **提示符显示链断在轮询**：前端 ps1() 优先用会话创建时固化的 `s.prompt`（服务端 `${cwd} $ ` 形态），而 blocks 轮询不消费 `pack.prompt`——即使 ②③ 都修了，`s.cwd` 回写也不上屏（服务端其实已在命令完成后重建 `s.Prompt = BuildPrompt(s)` 并随 blocks 下行，纯前端消费缺口）。任何一层修了其余仍在，表现为「有的 shell 好了、cmd 永远不更新」或「接口有 cwd、页面纹丝不动」（后者即 2026-11-15 live 实测在 ②③ 修复后仍撞到的第四层）。

## 2 特征签名与扫描方法

- 代码：`SessionShell.BuildWrappedCommand`（各 shell kind 的包裹命令与标记协议）、`SessionShell.Parse`（after 段 exit/cwd 解析）、`SubmitCommandResponse`（cwd 字段）、前端 `runCommand`/`resolveApproval` 消费点、前端 blocks 轮询（`pack.prompt` 消费）。
- 扫描：改 SessionShell/命令响应模型时，按 lens 三问逐项核对；对照测试（红灯锚）= `SessionShellCmdCwdTests.Run_AfterCd_ReportsSwitchedCwd`（真实 cmd 进程集成）。
- cmd 展开时机探针结论（实测）：交互态 `%CD%`/`%ERRORLEVEL%` 均为整行解析期展开；`%%E%` 偶合展开出 `%0%` 型残迹；两行协议是唯一可靠形态。

## 3 Canonical fix

① cmd 两行采集：phase1 标记行（沿用既有 `echo {done}!ERRORLEVEL!`，exit 恒 0 为既有独立缺陷，不在本类范围）→ 检测到标记后发 `echo {done2}::%CD%`，`ApplyCmdCwd` 取 **LastIndexOf**（cmd 回显输入行在前、真输出行在后）；② `SubmitCommandResponse.Cwd` 下行（SubmitCommand 允许路径 + ResolveApprovalV2 批准路径都带 `s.Cwd`）；③ 前端 run 与批准两处 `if (res.cwd) s.cwd = res.cwd`；④ blocks 轮询消费服务端重建的提示符：`if (pack?.prompt) s.prompt = pack.prompt`（服务端格式单一真源 `BuildPrompt`，前端不得自拼格式）；`RefreshCwd` 探针按 kind 取 `cd`（cmd）/`pwd`。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/StStartDirAndSessionShellTests.cs`（SessionShellCmdCwdTests，真实 cmd 进程） | dotnet test | cmd 链路端到端（两行协议 + LastIndexOf 解析 + Cwd 返回）；PowerShell 分支由既有智能终端测试覆盖；其他 shell kind 新增时靠 lens 人工扫查 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| [2026-11-15-st-ux-defects](../cases/2026-11-15-st-ux-defects.md) | 2026-11-15 | cmd 会话 `cd <盘>:\` 后页面当前目录徽标不更新；block JSON 无 cwd；响应不携带 | 建类前既有缺陷（功能缺口自插件始版即存在），非守卫逃逸 |

## 6 已知边界（本类范围外，防误伤）

- cmd exit 码恒 0（`!ERRORLEVEL!` 无延迟展开）为**独立既有缺陷**，修法涉及 /V:ON 开关的 `!` 吞字权衡，另行挂账，不在 cwd 采集批内顺手改。
- 纯 UTF-8 拉丁重音文本在 CJK 码页环境的编码误判属 PC-011 已知局限，不在本类。
