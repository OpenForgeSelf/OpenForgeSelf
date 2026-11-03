# 05 Evidence · 网关流式上游错误透传

| 项 | 命令 | 结果 | 等级 |
|----|------|------|------|
| 过滤集 | `dotnet test … --filter UnifiedAIGatewayIntegrationTests\|UnifiedUpstreamErrorsTests`（OutDir `bin/upstream-passthrough/`） | **26/26 通过** | Verified |

覆盖：`UnifiedUpstreamErrorsTests` Theory 400/401/403/404/408/422/429/500/502/503 + InnerException 解包；网关 429 流式/非流式集成测。
