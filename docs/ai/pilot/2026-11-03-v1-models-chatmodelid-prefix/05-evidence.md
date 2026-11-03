# 05 Evidence — v1/models ChatModelId 前缀

## Changed Files

| 文件 | 变更 |
|------|------|
| `ForgeSelf.Api/Entities/AIModel.Biz.cs` | 新增 `GetGatewayModelListId` |
| `ForgeSelf.Api/Controllers/UnifiedAI/ModelsController.cs` | 列表/单查 `id` 统一投影 |
| `ForgeSelf.Api/Services/AI/AIProviderRegistry.cs` | `GetModelById` 支持 `provider:upstream`；别名分支 `ModelInfo.Id` 改为 upstream |
| `ForgeSelf.Api.Tests/AIModelFeatureTests.cs` | 新用例 + 别名用例内部 Id 断言对齐 |
| `docs/02-features/004-provider-models-integration.md` | §2.6 更新 |

## Validation（Verified）

| 命令 | 结果 |
|------|------|
| `dotnet test --filter FullyQualifiedName~AIModelFeatureTests -p:OutDir=.temp/bin-v1models-prefix3/` | **20/20 通过**（日志 `.temp/tmp/aimodel-feature-tests4.log`） |

## Known Limitations

- 本地默认 `bin\Debug` 被 dev-stack 宿主（pid 82240）锁文件，验证使用旁路 OutDir（与 AGENTS §5.6 旁路说明一致）；未停用户运行实例。

## Unresolved Issues

- 无。
