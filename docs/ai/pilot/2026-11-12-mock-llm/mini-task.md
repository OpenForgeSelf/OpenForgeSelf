# mini-task：Mock LLM（关键字 → 固定回复）

轻量任务。用户原文即开工（闸门1）：在新工作树里尽快做完，用来在没有真实上游时测需要 Agent 的功能。

## Intent

几乎每个功能都要 AI Agent，而 Agent 依赖真实 LLM。增加一种不访问网络的提供方：规则文件里写关键字和返回内容；把该提供方设为默认；请求文本命中 key 就返回对应 content。

完成判据：

- `MockLlmTests` 过滤测试通过。
- 设置页类型里能选「Mock（测试）」，空地址与空密钥可以保存。
- 功能事实在 `docs/02-features/001-ai-provider-config.md` §2.4，步骤在 `docs/05-guides/add-ai-provider.md` §1.1，开发流程在 `docs/04-standards/agent-workflow/a-workflow-core.md`「没有真实 LLM 时」。

## Spec

- 新枚举值 `AIProviderType.Mock`。
- 规则文件默认 `config/mock-llm.json`，样例 `config/mock-llm.sample.json` 入库，本机规则文件 gitignore。环境变量 `FORGESELF_MOCK_LLM` 可改路径。
- 每次请求重读文件。空 key 忽略。最长子串优先，长度相同保留文件顺序。未命中用 `fallback`。匹配范围是系统提示和消息文本。区分大小写。
- 空 Endpoint 规范成 `mock://local`，空模型列表规范成 `mock`。API 密钥可空。部署标记为本地。
- 聊天与流式都只返回助手文本，不发工具调用，不创建 HttpClient。usage 三个数字是字符数。
- 测试连接不发网络，也不使用「回复含 ok」判据。文件缺失或 JSON 无法解析则失败，并写出路径。拉取模型不发网络，新行默认启用。
- 显式模型 id 为 `提供方名:mock`。提供方名是 `mock` 时写作 `mock:mock`。

## Interaction Design

设置 → AI 提供方 → 新增：

- 类型下拉增加「Mock（测试）」。选中后接入地址填入 `mock://local`，地址下方出现一行说明：不访问网络，命中 `config/mock-llm.json` 的 key 就返回 content，样例在 `config/mock-llm.sample.json`。
- API 密钥旁提示「本地运行时可留空」。留空仍可保存。
- 列表类型标签用既有 `info` 样式。
- 保存并设为默认后，对话或 Agent 使用模型 `mock:mock`。请求文本包含某条 key 时，助手回复是该条 content；都不包含时回复 fallback。
- 点「测试连接」且规则文件可读：成功文案含规则条数，并写明未访问网络。文件不存在或 JSON 坏掉：失败文案含路径。改 JSON 后下一次请求即用新内容，页面不必重启。
- 空态：还没复制样例时，测试连接失败，文案告诉用户复制样例或设置环境变量。
- 本批不改对话页布局。运行中的 `:51888` 宿主不包含本分支，不能在那个实例上看到新下拉项。

## Plan

| 文件 | 动作 |
|------|------|
| `ForgeSelf.Abstractions/AIConfigModels.cs` | 枚举加 `Mock` |
| `ForgeSelf.Api/Services/AI/Providers/MockLlmProvider.cs` | 读规则、匹配、聊天与流式 |
| `ForgeSelf.Api/Services/AI/AIProviderRegistry.cs` | Mock 不建 HTTP 客户端 |
| `ForgeSelf.Api/Services/AIProviderService.cs` | 规范化、测试连接短路 |
| `ForgeSelf.Api/Services/AIModelService.cs` | 拉取模型不走网络，新行启用 |
| `ForgeSelf.Api/Services/AI/LocalModelGate.cs` | 部署标记本地 |
| `ForgeSelf.Api/Controllers/AIProviderController.cs` | 空密钥允许 |
| `ForgeSelf.Api/Services/ConfigurationService.cs` | appsettings 里的 Mock 也规范化 |
| `ForgeSelf.Web/src/types/aiProvider.ts` | 类型联合加 `Mock` |
| `ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue` | 下拉、地址、提示 |
| `config/mock-llm.sample.json`、`.gitignore` | 样例入库，本机文件忽略 |
| `docs/02-features/001-ai-provider-config.md`、`docs/05-guides/add-ai-provider.md`、`docs/04-standards/agent-workflow/a-workflow-core.md`、`docs/README.md` | 事实、步骤、流程、索引 |

不改：用户正在跑的宿主、主工作区未入库的 LLM 代理管道、真实上游协议、`ForgeSetting.DefaultModel` 的自动改写。

## Task

Allowed：上表文件，以及本目录工件、`ForgeSelf.Api.Tests/Unit/MockLlmTests.cs`。

Forbidden：停启 `:51888`；把主工作区未跟踪的 `Services/AI/Proxy` 拷进本分支；改用户 `~/.grok/config.toml`；用参数无关的 `JsonSerializer.Serialize` 写业务 JSON；提交 git。

验证：

```text
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter FullyQualifiedName~MockLlmTests -m:1
cd ForgeSelf.Web && pnpm run check && pnpm run design:check
pwsh -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-12-mock-llm -EnforceHeader -EnforceAcCoverage -EnforceInteraction
```

轻量目录没有单独的 02/04，后两个开关只对存在的编号文件生效。Interaction Design 写在本文件。
