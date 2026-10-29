# mini-task：AIAgent MAF 路径空名工具调用毒化会话（2.4.18 / AIAgent 1.9.7 回归）

> 轻量件（≤3 文件缺陷修复）：合并 Intent / Spec / Plan / Task 四要素。日期 2026-10-29。

## 1. Intent（为什么 / 做什么 / 到什么程度）

**背景（用户输入3 · 2026-10-29 13:15「你试试下面的请求 报错了」）**：用户在 :51888（宿主 2.4.18.2610290828，今晨发布，含 AIAgent 1.9.7 MAF 聊天路径）发「查看当前项目结构」，agent 跑完两轮工具调用后报错；此后同会话每次发言（「报错了」×2）都在 1~2ms 内失败、响应长度 0。用户复制的 curl 里 message 被脱敏成 3 个空格，该形态只会命中后端空消息守卫（`AIChatController.cs:217`，设计内行为），**不是用户真实报错**。

**实例日志实证**（`D:\src\tools\OpenForgeSelf\versions\2.4.18.2610290828\Log\2026_10_29.log`）：
- 13:07:50 收到 8 字消息 → 两轮工具调用正常执行 13:08:12.948 `[MafTaskLoopAgent] MAF Run 失败: Value cannot be an empty string. (Parameter 'functionName')`
- 13:08:58 / 13:09:42 两条 3 字消息 → 均在「发送流式 Chat 请求（SDK）」后 1~2ms 同错，响应长度 0。

**数据库实证**（`C:\Users\12504\.forgeself\ForgeSelf.db` SessionEvent 表，只读探针）：会话 `mvt30ipk-quraxi` 事件 #1104 `assistant/message` 的 ToolCalls = 2 条真实调用 + **数十条 `{CallId:"",ToolName:"",ArgsJson:"{}"}` 空壳**（每个参数流式续分片一条）。

**根因链（全部代码实证）**：
1. `OpenAISdkAdapter.ChatStreamAsync`（:352-372）：OpenAI 流式工具调用只有首片带 `ToolCallId/FunctionName`，续片只带参数增量 → 逐片透传 `DeltaToolCall{Name:""}`；
2. `ForgeChatMessageMapper.ToChatResponseUpdates`（`ForgeChatClientAdapter.cs:140-149`）：**把每个分片当一条完整调用**产出 `FunctionCallContent(id, "", …)`（续片参数是 JSON 前缀，解析失败变 `{}`）——全仓唯一逐片消费点（同类扫查：`ReactLoopAgent.cs:425`、`AIAgentService.cs:498` 均有 `AccumulateToolCall` 聚合器；`OpenAIChatController` 为协议透传，正确）；
3. `ForgeModelCallLogger`（`MafTaskLoopAgent.cs:853-857`）：逐 fcc 落 `ToolCallRef` → 空壳调用持久化进会话日志；
4. 下一次请求：`DeriveMessages → ToChatMessage(:761) → adapter → OpenAISdkAdapter.BuildChatMessages(:183)` 构造 `ChatToolCall.CreateFunctionToolCall(id, "")` → OpenAI SDK 抛 `ArgumentException(functionName)` → 会话**永久毒化**。

**附带症状**：真实调用的 `ArgsJson` 落也是 `{}`（逐片解析失败），工具参数历史丢失（同根因第二症状）。

**Intent**：修复 MAF 适配层流式工具调用消费方式（分片聚合），并让已毒化会话自愈；恢复「agent 多轮工具调用后正常收尾」与「同会话可继续对话」。

**成功判据**（可验证）：
- 新增单测：分片序列 → 恰好产出每调用一条完整 `FunctionCallContent`（修复前红、修复后绿）；
- 历史空名调用出站过滤测绿；
 `ForgeSelf.Api` build 0 错误；MAF 域过滤集（MafTaskLoopAgentTests / ForgeChatClientAdapterTests / ReactLoopTaskLoopTests / ReactLoopAgentTests）全绿。

## 2. Spec（改什么 / 边界 / 交互）

- **改动 1**（治本）：`Plugins/AIAgent/MafLoop/ForgeChatClientAdapter.cs`
  - `GetStreamingResponseAsync`按 `ReactLoopAgent.AccumulateToolCall`（:917）同语义聚合分片：首片带 Id 新建调用，续片（Id 空）向末位追加 name/args；流结束时每调用产出一条完整 `FunctionCallContent`（完整 args JSON）；
  - `ToChatResponseUpdates` 移除逐片 FunctionCallContent 产出（文本/usage/finish 保留）；
  - 聚合后仍无名的畸形调用**丢弃**（宁可少一个调用，不允许空名出门）。
- **改动 2**（自愈·出站过滤）：`MafTaskLoopAgent.ToChatMessage` 跳过 `ToolName` 为空的 ToolCalls；`ForgeChatMessageMapper.ToUnifiedMessages` 跳过空名 `FunctionCallContent`（双层防护，旧毒化会话立即恢复可用）。
- **边界（不改）**：`UnifiedStreamChunk` 契约（不新增 Index 字段，平行调用分片交叉到达的极端场景与 ReactLoopAgent 同限，登记 Known Limitations）；provider / 老路径 / 投影层 / 前端一律不动。

## 3. Plan（步骤 → 预期）

1. 先写 3 条红灯测试（分片聚合 ×2 + 空名过滤 ×1）→ 跑过滤集确认红（TDD Red）。
2. 实现改动 1+2 → 过滤集绿（TDD Green）。
3. 回归：`dotnet build` + MAF 域四类过滤集全绿。
4. PC 归档 + 日记/TODO + 探针清理。

## 4. Task（Allowed / Forbidden / 验证）

-Allowed**：上列  个源文件 + `ForgeSelf.Api.Tests/Plugins/AIAgent/MafTaskLoopAgentTests.cs` + 工件/归档文档。
- **Forbidden**：不动 ForgeSelf.Abstractions 契约、不动 provider/投影/前端、不发布（发布动作另行请示用户）。
- **验证命令**：`cd ForgeSelf.Api && dotnet build`；`cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~MafTaskLoopAgentTests|FullyQualifiedName~ForgeChatClientAdapterTests|FullyQualifiedName~ReactLoopTaskLoopTests|FullyQualifiedName~ReactLoopAgentTests"`。
- **闸门1依据**：本缺陷属今晨用户预授权任务（输入1「自行调研、自行决策开发、不用问我」）交付物（PILOT-051 / 2.4.18）的当日回归闭环；发布仍须用户拍板。
