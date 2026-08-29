# 密钥加密存储（Secret Encryption）— 功能需求与设计

> 功能编号：N/A（核心安全基础设施）
> 状态：已实现
> 关联：AI Provider 配置数据库化（specs/001-ai-provider-config-db）、ApiServerKey 实体
> 最后更新：2026-08-06

## 1. 功能需求

### 1.1 背景

AI Provider 的 API Key、API 服务器密钥等敏感凭据需要持久化存储。若以明文写入 SQLite 数据库：

- 数据库文件泄露即等于凭据泄露；
- 日志、备份、异常快照可能顺带带走明文；
- 无法满足"落库即密文"的安全基线。

### 1.2 目标

1. 敏感凭据（API Key 等）以**密文**形式落库，明文不出后端服务边界；
2. 加解密对业务代码透明：Service/Repository 层拿到的是可解密的密文，仅展示/响应时脱敏；
3. 加密方案可离线复现（同一实现可被测试、运维脚本复用），且可验证（往返断言）。

### 1.3 非目标

- 不负责传输层安全（由 HTTPS 承担）；
- 不提供密钥轮换自动化（当前为单密钥，轮换需人工/运维流程）；
- 不加密整个数据库，仅加密敏感字段。

## 2. 设计

### 2.1 算法选型

| 项 | 选择 | 理由 |
|----|------|------|
| 算法 | AES-256-CBC | .NET 内置，满足对称加密需求，无额外依赖 |
| 填充 | PKCS7 | AES 标准填充 |
| IV | 每次加密随机生成（16 字节） | 同一明文多次加密产出不同密文，防模式分析/重放（语义安全） |
| 密文格式 | `base64(IV[16字节] + 密文)` | IV 随密文存储，解密时自足还原，无需额外传参 |
| 密钥 | SHA256 派生固定 32 字节 | 支持任意长度密钥源，统一为 AES-256 密钥 |

### 2.2 密钥解析优先级

```
Encryption:Key 配置（appsettings / 环境配置）
  ↓ 未设置
FORGESELF_ENCRYPTION_KEY 环境变量
  ↓ 未设置
内置默认密钥 "ForgeSelf-AIProvider-Default-Encryption-Key"（仅开发默认）
```

> **生产要求**：必须通过 `Encryption:Key` 或 `FORGESELF_ENCRYPTION_KEY` 覆盖默认密钥，避免密钥硬编码泄露。当前仓库所有 appsettings 均未配置，开发环境实际生效的是内置默认密钥。

### 2.3 实现位置

| 文件 | 职责 |
|------|------|
| `ForgeSelf.Api/Security/ISecretEncryptionService.cs` | 接口：`Encrypt` / `Decrypt` / `Mask` |
| `ForgeSelf.Api/Security/AesSecretEncryptionService.cs` | AES-256-CBC 实现 + 密钥解析 |
| `ForgeSelf.Api/AppBuilder.cs` | DI 注册：`AddSingleton<ISecretEncryptionService, AesSecretEncryptionService>()` |
| `ForgeSelf.Api/Data/Model.xml` | `ApiServerKey.KeyCipher`（AES-256-CBC 加密存储） |

### 2.4 脱敏展示

`Mask(plainText)`：保留前 3 位与末尾 4 位，中间以 `****` 遮盖（如 `sk-****3456`）。API 响应中绝不明文返回 ApiKey。

## 3. 使用指南

### 3.1 加密一个密钥（获取密文）

新增密钥密文可通过专用单元测试输出（与后端运行时行为完全一致，空配置 → 内置默认密钥）：

```bash
cd ForgeSelf.Api.Tests
dotnet test --filter "FullyQualifiedName~AesSecretEncryptionOutputTests" --logger "console;verbosity=detailed"
```

测试输出 `ENCRYPTED_RESULT=...` 即密文，可直接写入数据库/配置。

- 参考实现：`ForgeSelf.Api.Tests/Unit/AesSecretEncryptionOutputTests.cs`
- 测试内置往返断言（`Decrypt(cipher) == 原文`），保证输出的密文可解密还原。

### 3.2 代码中加密/解密

```csharp
var encryption = serviceProvider.GetRequiredService<ISecretEncryptionService>();

var cipher  = encryption.Encrypt("sk-xxx");   // 落库
var plain   = encryption.Decrypt(cipher);     // 还原
var masked  = encryption.Mask("sk-xxx");      // 展示：sk-****xxx
```

## 4. 注意事项（已踩坑 / 已知约束）

1. **密文非确定性**：随机 IV 导致同一明文每次加密结果不同——这是特性而非缺陷。每份密文都携带自己的 IV，均能被同一密钥解密还原。
2. **密钥变更即密文失效**：更换 `Encryption:Key` / 环境变量后，旧密文无法再解密，需重新加密存量数据（当前无自动轮换）。
3. **不要用错误密钥解密**：CBC 解密不校验密钥正确性，错误密钥可能返回乱码或抛填充异常，业务层需自行校验（如解密后格式检查）。
4. **测试密钥与生产密钥隔离**：既有功能测试（AIProviderFeatureTests 等）使用独立测试密钥 `test-encryption-key-0123456789`，与生产默认密钥无关；专用输出测试刻意使用空配置以对齐后端运行时。

## 5. 测试覆盖

| 测试 | 覆盖点 |
|------|--------|
| `Unit/AesSecretEncryptionOutputTests.cs` | 指定明文加密 + 往返解密断言 + 密文输出 |
| `AIProviderFeatureTests.cs`（T018/T019/T021） | 加密落库、响应掩码、往返+掩码格式 |
| `Unit/HashServiceTests.cs` | DevTools 插件 AES 加解密（独立实现，密钥来源不同） |
