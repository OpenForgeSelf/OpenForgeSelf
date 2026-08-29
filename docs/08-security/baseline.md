# 08-security — 安全基线

> 状态：已实现（2026-08-12 反向更新）
> 最后更新：2026-08-12

## 1. 密钥管理

- 敏感凭据（AI Provider ApiKey、API 服务器密钥）**加密落库**（AES-256-CBC），详见 [`02-features/100-secret-encryption.md`](02-features/100-secret-encryption.md)；
- 密钥解析优先级：`Encryption:Key` 配置 → `FORGESELF_ENCRYPTION_KEY` 环境变量 → 内置默认密钥（**仅开发默认，生产必须覆盖**）；
- 接口仅返回掩码（`Mask`：前 3 + 末 4，`****` 遮盖），绝不明文；
- 明文仅在解密后用于上游请求（测试连接、转发），不持久化、不日志。

## 2. 鉴权要求

- 管理类接口（`AIProviderController` 等）加 `[Authorize("ApiKeyPolicy")]`；
- 凭据即 `ApiServerKey` 明文（解密比对），首次启动由 `ApiServerController.init-token` 发放；
- 端口/令牌配置变更需重启生效（见 `05-guides/`）。

## 3. 敏感操作清单

| 操作 | 风险 | 防护 |
|------|------|------|
| 写 ApiKey | 明文泄露 | 落库即密文 + 响应掩码 |
| 重启服务 | 中断运行 | 需令牌鉴权 |
| 重新生成令牌 | 旧客户端失效 | 显式 `regenerate` 接口 |

## 4. 安全 review 清单

- [ ] 新增敏感字段是否走加密落库？
- [ ] 响应是否含明文密钥/令牌？
- [ ] 管理接口是否加 `ApiKeyPolicy`？
- [ ] 生产环境是否覆盖默认加密密钥？
