---
task_id: 2026-11-12-mock-llm
feature: 001                  # docs/02-features/001-ai-provider-config.md（AI 提供方配置）
risk: { level: L1, triggers: ["ForgeSelf.Api/Services/AIProviderService.cs", "ForgeSelf.Api/Services/AI/Providers/MockLlmProvider.cs"], raised_by_agent: true }
gate1: { mode: user, at: 2026-11-12, decision: approved }   # 用户原文即开工：「在新工作树里尽快做完，用来在没有真实上游时测需要 Agent 的功能」
gate2: { mode: reviewer, at: 2026-11-13, decision: approved } # 06-review.md Final Decision: APPROVED
gate3: { mode: agent, at: null }   # 随本提交入库时回写
expected_files:
  - ForgeSelf.Api/Services/AI/Providers/MockLlmProvider.cs
  - ForgeSelf.Api.Tests/Unit/MockLlmTests.cs
  - ForgeSelf.Abstractions/AIConfigModels.cs
  - ForgeSelf.Api/Controllers/AIProviderController.cs
  - ForgeSelf.Api/Services/AI/AIProviderRegistry.cs
  - ForgeSelf.Api/Services/AI/LocalModelGate.cs
  - ForgeSelf.Api/Services/AIModelService.cs
  - ForgeSelf.Api/Services/AIProviderService.cs
  - ForgeSelf.Api/Services/ConfigurationService.cs
  - ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue
  - ForgeSelf.Web/src/types/aiProvider.ts
  - config/mock-llm.sample.json
  - docs/02-features/001-ai-provider-config.md
  - docs/05-guides/add-ai-provider.md
  - docs/04-standards/agent-workflow/a-workflow-core.md
  - docs/README.md
rollback: "git revert <commit>（单批原子提交；恢复到无 Mock 提供方状态）"
writeback: { feature_doc: done, reason: "001-ai-provider-config.md §2.4 已随批回写" }
---

# Agent Task

> 轻量任务配套 04（feature/回写契约锚点；意图/规格/计划/任务见同目录 mini-task.md）。
> 前序工件：mini-task.md + 05-evidence + 06-review（APPROVED）齐备。

## Task ID

2026-11-12-mock-llm

## Objective

新增不访问网络的 Mock LLM 提供方（规则文件 key→固定回复），用于没有真实上游时跑通 Agent/对话链路：`MockLlmTests` 过滤测试通过；设置页可新增「Mock（测试）」并测试连接；功能事实落 001 §2.4。

## Scope

### Allowed

- mini-task.md Plan 表所列文件及其测试断言。
- `config/mock-llm.sample.json` 入库；本机 `config/mock-llm.json` 走 .gitignore。

### Forbidden

- 真实上游提供方（OpenAI 兼容/Ollama 等）既有语义变更。
- e2e 零 mock 体系（Mock 提供方是真实注册的提供方，非测试桩，不改变 e2e 纪律）。
- 停/启/杀用户运行中的宿主进程。

## Acceptance Criteria

- [x] AC-1 `MockLlmTests` 5 用例通过（命中 key 返回 content、未命中 fallback、大小写敏感、最长优先、usage=字符数）
- [x] AC-2 设置页类型下拉含「Mock（测试）」，空地址规范 `mock://local`、空密钥可保存
- [x] AC-3 测试连接不发网络：规则文件可读时成功文案含条数，缺失/坏 JSON 时失败文案含路径
- [x] AC-4 功能文档三处回写（001 §2.4 / add-ai-provider §1.1 / a-workflow-core「没有真实 LLM 时」）+ docs/README.md 索引行

## Verification Commands

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~MockLlmTests"
cd ForgeSelf.Web && pnpm run check
```

（实测记录与偏差见 05-evidence.md；审查结论见 06-review.md。）
