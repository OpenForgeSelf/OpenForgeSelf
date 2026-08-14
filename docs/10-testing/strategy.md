# 10-testing — 测试指南

> 状态：已实现（从 AGENTS.md / working memory / 代码反推，2026-08-12）
> 最后更新：2026-08-12

## 1. 测试分层与铁律

| 层 | 工具 | 铁律 |
|----|------|------|
| 前端单测 | vitest 3.x | 新功能 MUST 写组件测试（渲染 + 关键交互）；纯配置/元数据变更可免（tasks.md 注明） |
| 前端 e2e | Playwright 1.61（`@playwright/test`） | **绝不 mock**；走真实登录 + 真实后端（见 memory e2e 铁律）；截图验证视觉对齐；**首次运行前需 `npx playwright install` 安装浏览器**，否则直接失败 |
| 后端单测/集成 | xUnit + Moq + FluentAssertions + Coverlet | 新功能后端 MUST 写单测；复现 bug 先写测试断言正确行为（红灯）再修 |
| 后端 e2e | `Backend.Tests/E2E/`（ApiClient + Tests） | 真实 API 客户端端到端 |

## 2. 运行命令

### 前端（OpenForgeSelf.Frontend/）
| 命令 | 作用 |
|------|------|
| `pnpm run check` | vue-tsc --noEmit + eslint（类型 + 规范，门禁） |
| `pnpm run test` | vitest 单测 |
| `pnpm run test:e2e` | Playwright e2e |
| `pnpm run test:e2e:published` | 对已发布产物跑 e2e（`playwright.e2e-published.config.ts`） |

> **OOM 规避**：`NODE_OPTIONS=--max-old-space-size=4096 --pool=forks --poolOptions.forks.singleFork=true`

### 后端（OpenForgeSelf.Backend/ + .Tests/）
| 命令 | 作用 |
|------|------|
| `dotnet build` | 后端构建（门禁） |
| `dotnet test` | 后端测试（xUnit） |

## 3. 关键测试文件（代码事实）

| 测试 | 覆盖点 |
|------|--------|
| `Backend.Tests/AIProviderFeatureTests` | 加密落库/掩码/往返 |
| `Backend.Tests/Integration/UnifiedAIGatewayIntegrationTests` | 多风格路由（OpenAI/Anthropic/Responses/Agent Framework）+ 流式 usage 透传 |
| `Backend.Tests/Integration/ChatRecordsControllerIntegrationTests` | 会话视图 |
| `Backend.Tests/Unit/ChatSessionServiceTests` `ChatTurnServiceTests` `ChatTurnStreamRecorderTests` | 会话化改名后逻辑 |
| `Backend.Tests/Unit/ImageRecognitionCacheTests` `MultimodalProcessorCacheTests` | 多模态缓存 |
| 前端 `ChatRecordsView` / `ChatRecordDetail` 测试 | 会话维度渲染 + WS 推送 |

## 4. 判定规则

- 构建/测试失败 → 必须修复，不允许删测试"通过"；
- 前端 `check` 失败 → 先修类型再修 lint；
- 涉及 UI → Playwright 截图对比（像素级对齐）。

## 5. 已知脆弱点

- `ChatRecordRealLLMTests` 全量跑偶发红（疑测试间状态干扰，TODO 已知）；
- `TruncatedContent.test.ts` 的 `navigator.clipboard` 只读致 1 失败（环境缺陷，与样式无关）；
- `pnpm run build` 约 31 个预存类型错误（非本轮回归，待独立修，T032）。
