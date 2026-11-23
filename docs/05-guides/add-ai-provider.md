# 05-guides — 操作指南（SOP）

> 状态：已实现（从代码反推，2026-08-12）
> 最后更新：2026-11-12

按场景的操作步骤。原理见 `02-features/`，规范见 `04-standards/`。

## 1. 添加 AI 提供方

1. 打开前端 → 设置 → AI 提供方；
2. 新增：填 `Name`（唯一）、`ProviderType`（OpenAI/OpenAIResponses/Anthropic/Custom/Ollama/LMStudio/Mock）、`Endpoint`（base URL）、`ApiKey`（提交即加密落库）、`TimeoutSeconds`；
3. 点"测试连接"（`POST /api/ai-providers/{id}/test`，解密后发最小探测；Mock 例外：不发网络，见下 §1.1）；
4. 点"设为默认"（`SetDefault`）或"拉取模型"（`POST .../fetch-models` → 见下）。

> 图片由上游处理。设置页不再提供视觉模型或「启用多模态」。带图请求按协议原样或标准字段转发。

### 1.1 没有真实模型时用 Mock

规则文件的字段和匹配方式见 `docs/02-features/001-ai-provider-config.md` §2.4。步骤：

1. 把 `config/mock-llm.sample.json` 复制为 `config/mock-llm.json`，按要测的功能写 `key` 和 `content`。本机这份不入库。
2. 设置 → AI 提供方 → 新增。类型选「Mock（测试）」。地址保持 `mock://local`。API 密钥可空。勾选默认后保存。
3. 对话或 Agent 使用的模型填 `mock:mock`（显示名不是 `mock` 时填 `显示名:mock`）。
4. 点「测试连接」。成功文案含规则条数，且不访问网络。改 JSON 后下一次请求即生效，不用重启。

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
