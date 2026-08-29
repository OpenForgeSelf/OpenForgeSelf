# 05-guides — 操作指南（SOP）

> 状态：已实现（从代码反推，2026-08-12）
> 最后更新：2026-08-12

按场景的操作步骤。原理见 `02-features/`，规范见 `04-standards/`。

## 1. 添加 AI 提供方

1. 打开前端 → 设置 → AI 提供方；
2. 新增：填 `Name`（唯一）、`ProviderType`（OpenAI/Anthropic/Custom）、`Endpoint`（base URL）、`ApiKey`（提交即加密落库）、`TimeoutSeconds`；
3. 若需多模态：勾 `EnableMultimodal` 并填 `VisionModel`（支持 `provider:model_id` 前缀，如 `default:qwen/qwen3-vl-4b`）；
4. 点"测试连接"（`POST /api/ai-providers/{id}/test`，解密后发最小探测）；
5. 点"设为默认"（`SetDefault`）或"拉取模型"（`POST .../fetch-models` → 见下）。

> 注意：修改 `VisionModel` 等运行期配置后，若后端进程无法被停止，**需手动重启 exe** 才生效。

## 2. 从上游拉取模型

1. 提供方已配且测试通过；
2. 点"拉取模型" → 后端调上游 `/models` → 按 `(Provider, UpstreamModelId)` upsert 到 `AIModel`；
3. 在模型列表编辑 `Alias` / `Capabilities`（vision,stream）/ `MaxContext`；
4. 复制 `ChatModelId`（`provider:upstream_model_id`）粘贴到 LLM 客户端（如 GitHub Copilot 模型字段）。

## 3. 加密一个密钥（获取密文，运维/调试）

```bash
cd ForgeSelf.Api.Tests
dotnet test --filter "FullyQualifiedName~AesSecretEncryptionOutputTests" --logger "console;verbosity=detailed"
```

输出 `ENCRYPTED_RESULT=...` 即密文（空配置 → 内置默认密钥，与运行时一致），可直接写入库/配置。详见 `02-features/100-secret-encryption.md`。

## 4. 修改运行端口

1. 设置 → 端口配置（或 `POST /api/portconfiguration`，端口范围 1024–65535）；
2. 端口写入 `ForgeSetting` 并落库；
3. **重启服务**（`POST /api/api-server/restart` 或托盘）才监听新端口。

## 5. 获取/轮换 API 令牌

- `GET /api/api-server/init-token` 取当前令牌（前端设置页展示）；
- `POST /api/api-server/regenerate` 轮换并失效旧令牌（客户端需同步更新）。
