# 05 Evidence

## 红灯（Verified）

修复前 `ChatCompletions_SlowSessionUpsert_DoesNotHoldResponse`：会话写入拖 3 秒，响应耗时 3.05 秒。断言要求小于 800ms。日志 `.temp/tmp/llm-audit-red.log`。

## 绿灯（Verified）

`dotnet test --filter FullyQualifiedName~LlmTurnAuditIsolationTests|FullyQualifiedName~UnifiedAIGatewayIntegrationTests`

结果：失败 0，通过 9，跳过 0，209 ms。日志 `.temp/tmp/llm-audit-green.log`。

覆盖：慢保存不挡住响应且后台确实开始保存；入队抛错仍回上游正文；保存失败后下一条仍执行；热路径源码不再调用轮次保存。

## 构建（Verified）

上述测试编译并运行了 `ForgeSelf.Api`。无编译错误。

## 全量（Verified）

`dotnet test ForgeSelf.Api.Tests\ForgeSelf.Api.Tests.csproj`，14 分 41 秒。失败 26，通过 3717，总计 3743。日志 `.temp/tmp/llm-audit-full.log`。

26 条都在既有基线族：ProjectWorkspaceDefaultRootTests ×15、WorkflowPlanningIntegrationTests ×6、ScriptRunnerDi ×1、DesignAgentToolContractTests.空参数 ×1、OpenAIResponsesOutboundSchemaTests ×1、JsonNonAsciiEscapingGuardTests ×1（只剩 AnthropicProtocolBridge.cs:151）、MafApprovalLoopTests ×1（已知瞬时态）。没有一条落在本批文件。归本批 = 0。

## 未跑

无界面改动。未起 dev-stack，未做浏览器走查。未提交 git。
