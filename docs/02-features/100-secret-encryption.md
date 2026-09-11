# 密钥加密存储（Secret Encryption）— 功能需求与设计

> 功能编号：N/A（核心安全基础设施）
> 状态：已实现（2026-09-10 起升级为机器派生密钥 + 密文版本化，见 `030-api-keys.md`）
> 关联：AI Provider 配置数据库化（specs/001-ai-provider-config-db）、ApiServerKey 实体、`030-api-keys.md`
> 最后更新：2026-09-11

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
| 密文格式 | **v2**：`v2:` + `base64(IV[16字节] + 密文)`；**v1（旧）**：无前缀 `base64(IV + 密文)`，只读兼容 | 前缀区分新旧，v1 永久可读、v2 只用当前密钥解 |
| 密钥 | SHA256 派生固定 32 字节 | 支持任意长度密钥源，统一为 AES-256 密钥 |

### 2.2 密钥解析优先级（2026-09-10 起）

```
Encryption:Key 配置（appsettings / 环境配置）        ← 逃生舱：跨机器迁移用
  ↓ 未设置
FORGESELF_ENCRYPTION_KEY 环境变量                    ← 逃生舱：跨机器迁移用
  ↓ 未设置
机器派生密钥（MachineKeyProvider）                    ← 默认：出本机即不可解
    熵源：注册表 MachineGuid → /etc/machine-id → /var/lib/dbus/machine-id → 计算机名(兜底+WARN)
    派生：PBKDF2-HMAC-SHA256("ForgeSelf|<熵源>", "ForgeSelf.SecretEncryption.v2.MachineBound", 210000, 32)
```

- **硬编码串 `ForgeSelf-AIProvider-Default-Encryption-Key` 已降级为「仅解 v1 旧密文的兼容键」**，永不再用于加密新数据——这是升级前落库数据仍可读的唯一机制，不得删除。
- v1 密文读取走候选钥匙链（当前密钥 → 旧兼容键），且必须叠加**明文合理性过滤**（错误密钥约 1/256 概率恰好通过 PKCS7 填充校验，详见 `030-api-keys.md` §4.1）。
- 存量 v1 密文由 `SecretMigrationService` 在启动期自动迁移为 v2（幂等，见 030）。

### 2.3 实现位置

| 文件 | 职责 |
|------|------|
| `ForgeSelf.Api/Security/ISecretEncryptionService.cs` | 接口：`Encrypt` / `Decrypt` / `TryDecrypt` / `Mask` |
| `ForgeSelf.Api/Security/AesSecretEncryptionService.cs` | AES-256-CBC 实现 + 密钥解析 + v1/v2 版本化 + 候选钥匙链 |
| `ForgeSelf.Api/Security/MachineKeyProvider.cs` (+`IMachineKeyProvider`) | 机器熵源采集 + PBKDF2 派生 + 指纹 |
| `ForgeSelf.Api/Services/SecretMigrationService.cs` | 启动期 v1→v2 迁移（幂等 + 写回双闸） |
| `ForgeSelf.Api/AppBuilder.cs` | DI 注册：`ISecretEncryptionService` / `IMachineKeyProvider`，并触发迁移 |
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
2. **密钥变更即密文失效**：更换 `Encryption:Key` / 环境变量后，旧密文无法再解密。机器派生密钥场景下迁移到新机器同理；跨机器迁移走逃生舱（见 `030-api-keys.md` §3.3）。
3. **不要用错误密钥解密**：CBC 解密不校验密钥正确性，错误密钥**约 1/256 概率恰好通过 PKCS7 填充校验并返回乱码**（不是必然抛异常！）。业务层不得以「不抛异常」判定密钥正确，必须叠加明文合理性校验（本条已在 030 的候选钥匙链中落实，并修掉过一个由此导致的「迁移静默销毁密钥」缺陷）。
4. **测试密钥与生产密钥隔离**：既有功能测试（AIProviderFeatureTests 等）使用独立测试密钥 `test-encryption-key-0123456789`，与生产默认密钥无关；专用输出测试刻意使用空配置以对齐后端运行时。

## 5. 测试覆盖

| 测试 | 覆盖点 |
|------|--------|
| `Unit/AesSecretEncryptionOutputTests.cs` | 指定明文加密 + 往返解密断言 + 密文输出 |
| `AIProviderFeatureTests.cs`（T018/T019/T021） | 加密落库、响应掩码、往返+掩码格式 |
| `Unit/HashServiceTests.cs` | DevTools 插件 AES 加解密（独立实现，密钥来源不同） |
