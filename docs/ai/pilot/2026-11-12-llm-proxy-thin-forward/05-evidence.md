# Evidence

> 阶段：Stage 7｜只记实际跑过的命令。来源等级按次标注。
> Task ID：2026-11-12-llm-proxy-thin-forward

## Task

2026-11-12-llm-proxy-thin-forward

## Changed Files

管道与桥在 `ForgeSelf.Api/Services/AI/Proxy/`。契约在 `ForgeSelf.Abstractions/LlmProxyContracts.cs`。四个 UnifiedAI 控制器改为薄壳。插件 agent 模型回合在有管道时走 `ExecuteAsync`，并保留原始模型 id。识图类型与其测试已删。API、仓储 Update、设置页去掉三个多模态字段。当前事实文档已改到 004/011/架构总览。

## Build

Command:

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj --nologo -v q
```

Result: PASS（来源等级：Verified）

上一轮日志 `.temp/tmp/llm-proxy-build.log`：`EXIT=0`，无 `error CS`。本轮快档测试在同一解决方案上重新编译测试工程后跑通，见下。

## Unit Test

Command:

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~LlmProxy|FullyQualifiedName~OpenAISdkAdapter|FullyQualifiedName~AIModelFeature|FullyQualifiedName~UnifiedAI"
```

环境：`NO_PROXY=localhost,127.0.0.1,::1`，`TEMP`/`TMP` 指向仓库 `.temp/tmp`。

Result: PASS（来源等级：Verified）

第一次（改测试前）日志 `.temp/tmp/llm-proxy-test.log`：失败 1，通过 53，总计 54。失败用例 `LlmProxyPipelineTests.ChatToResponsesStream_ReusesStableItemId`，`ArgumentOutOfRangeException`（`ids` 为空）。原因是夹具把 Chat SSE 喂给 Responses 上游，客户端却按 Responses 事件解析。已改为 Responses 入站、Chat 上游。

第二次日志 `.temp/tmp/llm-proxy-test2.log`，`TEST_EXIT=0`：

```text
已通过! - 失败: 0，通过: 54，已跳过: 0，总计: 54，持续时间 3 s - ForgeSelf.Api.Tests.dll (net10.0)
```

日志正文是 GBK，数字以上述为准。

覆盖：AC1 同协议字段、AC2 源码守卫（过滤集含 `AIModelFeature`）、AC4 空模型、AC5 流字节、AC6 429、AC7 Chat→Responses 映射、AC8 稳定 `item_id`、AC9 Anthropic 图片。集成用例在同一过滤集内，不单列。

## Integration Test

Result: PASS（来源等级：Verified）

同一过滤集含 `UnifiedAIGatewayIntegrationTests`。第二次总计 54 通过，其中包含这些集成用例。未单独打印每个用例名（`--verbosity minimal`）。

## E2E

Result: N/A（来源等级：Verified 为「未跑」）

批准的计划写明本批不跑全量 e2e，不新起宿主。设置页夹具已去掉三字段，由 vitest 覆盖。

## Static Analysis

`pnpm run check`：`CHECK_EXIT=0`。eslint `0 errors, 165 warnings`（既有警告）。`[component-conventions] PASS`。日志 `.temp/tmp/web-check.log`。

`pnpm run design:check`：`DESIGN_EXIT=0`。`[style-tokens] PASS`，`[design-compliance] PASS`。日志 `.temp/tmp/web-design.log`。

`pnpm run test`：`TEST_EXIT=0`。`Test Files 112 passed (112)`，`Tests 1199 passed (1199)`。日志 `.temp/tmp/web-test.log`。

来源等级：Verified。对应 AC10。

AC3：`*.cs` 中 `MultimodalProcessor` / `IImageRecognitionCache` / `LocalFileImageRecognitionCache` 仅剩 `AIAgentProjectionLateResultsTests.cs` 第 30 行注释，无类型引用。

AC11：`AIProviderController.cs` 不再出现 `VisionModel` / `EnableMultimodal` / `VisionPromptTemplate`。`*.vue` / `*.ts` 不再出现 `visionModel` / `enableMultimodal` / `visionPromptTemplate` / 「多模态」。

## Screenshots

N/A。未做浏览器走查。

## Known Limitations

- 未对运行中的 `:51888` 做只读走查。源码测试不代表该旧包已更新。
- 跨协议 usage-only 分片可能不带 `finish_reason`。快档未断言这一点。
- 无冒号公开别名未在本批单测里打到 XCode。
- 新建提供方时服务层仍可能把三列默认值写入库。更新路径不覆盖。
- `OpenAISdkAdapter` 仍在，供 `AIService` 回退和旧测试使用。
