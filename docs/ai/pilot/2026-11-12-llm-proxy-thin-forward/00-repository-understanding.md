# Repository Understanding

> 阶段：Stage 0｜任务 `2026-11-12-llm-proxy-thin-forward`｜依据：仓库文件，2026-11-12

## 项目结构

解决方案 `ForgeSelf.slnx`。宿主 API 在 `ForgeSelf.Api/`，契约在 `ForgeSelf.Abstractions/`，插件 `Plugins/AIAgent/` 只引用 Core + Abstractions，不引用宿主（宿主对插件 `ReferenceOutputAssembly=false`）。前端 `ForgeSelf.Web/`。后端测试 `ForgeSelf.Api.Tests/`。

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端 | .NET 10 / ASP.NET Core | `ForgeSelf.Api/ForgeSelf.Api.csproj` |
| AI 出站传输 | `Microsoft.Agents.AI.OpenAI` 带的 OpenAI 客户端传输 | `ForgeSelf.Api/Services/AI/Providers/OpenAISdkAdapter.cs` |
| 别名 | `AIProviderRegistry.ResolveUpstreamModel` | `ForgeSelf.Api/Services/AI/AIProviderRegistry.cs` |
| 前端 | Vue 3 + Vite + Element Plus | `ForgeSelf.Web/package.json` |
| 插件契约 | `ILlmProxyPipeline` | `ForgeSelf.Abstractions/LlmProxyContracts.cs` |

## 架构特点

公开网关控制器在 `ForgeSelf.Api/Controllers/UnifiedAI/`。改前每条入站用封闭 DTO，各自调用 `ResolveUpstreamModel`，再经 `UnifiedChatRequest` 与 SDK `CompleteChatAsync`，未知字段被丢掉，并在 Chat 路径上做识图与缓存。内置 agent 另有一条 `IAIProvider.ChatStreamAsync`。

## 测试方式

后端过滤集：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~LlmProxy|..." `。前端：`ForgeSelf.Web` 下 `pnpm run check`、`pnpm run test`、`pnpm run design:check`。本批不跑全量 e2e。脚本用 `pwsh`。本机 `%TEMP%` 拒写，测试前把 `TEMP`/`TMP` 指到仓库 `.temp/tmp`。

## 构建命令

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Api/Services/AI/Proxy/` | 管道、桥、原始转发 |
| `ForgeSelf.Api/Controllers/UnifiedAI/` | HTTP 薄壳与实验 agent |
| `Plugins/AIAgent/MafLoop/` | 插件侧把统一请求收成 Chat JSON，再调管道 |
| `ForgeSelf.Web/src/components/settings/` | 提供方设置页 |

## 代码组织方式

控制器不解析模型、不选协议。`LlmProxyPipeline.ExecuteAsync` 是唯一算法。入站、出站、协议桥是策略。`ChatTurn` 记录是观察者，不改写出站 body。

## 现有工程规范

开发流程只走 `docs/04-standards/ai-native-engineering-workflow.md`。闸门 1 已在本会话批准计划后开工。不改库结构。不停 `:51888` 宿主。不提交。

## 候选低风险任务

本任务改的是公开网关主路径，不是低风险小改。风险记在 04 头部块。

## 选择该任务的原因

用户要求：不同入站共用一条逻辑；同协议原样转发；跨协议只做标准映射；去掉按提供方配置的多模态识图与缓存。
