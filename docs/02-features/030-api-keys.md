---
feature_key: F030
feature_no: 030
status: implemented
last_updated: 2026-10-06
aliases: ["030-api-keys"]
---

# 030 认证体系升级（机器派生密钥 + API 密钥分发）— 功能说明与架构流程

> 功能编号：030（specs/030-authentication-upgrade）
> 状态：已实现并发布（2026-09-11 发布到 51888 实例）
> 关联：`02-features/100-secret-encryption.md`（密钥加密存储）、`009-web-port-token-security.md`（端口与令牌）
> 最后更新：2026-09-11

## 1. 功能说明

### 1.1 背景与解决的问题

| 问题 | 升级前 | 升级后 |
|------|--------|--------|
| 源码泄露即密钥泄露 | 加密密钥硬编码在 `AesSecretEncryptionService`，拿到源码 + 配置文件即可解密全部密文 | 密钥由**本机信息派生**，出本机即不可解 |
| 换浏览器/清缓存 = 全站 401 | `init-token` 仅首次有效，之后无自助恢复手段 | 托盘「打开主界面」带 `#token=`，前端自动消费并清除 |
| 无法给第三方客户端分发密钥 | 全站共用一个主密钥，无法单独吊销 | 主密钥 + N 个子密钥，可单独创建/停用/轮换/删除 |

### 1.2 三条主链路

1. **机器派生密钥**：`MachineKeyProvider` 按优先级取机器熵源（Windows 注册表 `MachineGuid` → `/etc/machine-id` → `/var/lib/dbus/machine-id` → 计算机名兜底并 WARN），经 **PBKDF2-HMAC-SHA256（210_000 迭代，固定盐 `ForgeSelf.SecretEncryption.v2.MachineBound`）** 派生 32 字节 AES 密钥。启动时计算一次并缓存，日志输出指纹（`机器派生密钥已就绪（指纹=xxxxxxxx）`）。
2. **密文版本化 + 启动期迁移**：新密文一律带 `v2:` 前缀；识别到无前缀的旧密文（v1）时，`SecretMigrationService` 在启动期用旧密钥解密 → 新密钥重加密 → 回写，并把 `ForgeSetting.SecretMigrationVersion` 置 2（幂等标记）。迁移范围 = `ForgeSetting.ApiToken` + `AIProvider` 表全部 `ApiKey`。
3. **API 密钥分层**：主密钥（`ForgeSetting.ApiToken`，不可删/不可停用，只能重新生成）+ 子密钥（`ApiKeyCredential` 表，可创建/重命名/启停/轮换/删除/设过期）。

### 1.3 用户入口

- **设置页 → API 密钥**：顶部只读主密钥卡片（掩码 + 重新生成）；下方子密钥表格（掩码展示，明文仅在创建/轮换当次弹窗出现一次，提供一键复制）。
- **托盘 → 打开主界面**：URL 形如 `http://localhost:{port}/#token=<主密钥明文>`。

## 2. 架构流程

### 2.1 认证链路（`Authorization: Bearer <token>`）

```
ApiKeyAuthenticationHandler（策略 ApiKeyPolicy）
  └─ ApiKeyService.ResolveByToken(token)
       ├─ ① 子密钥：FindByKeyHash(SHA256(token))  ← O(1) 摘要定位，绝不逐条解密
       │     ├─ 未启用 → 401（不回退主密钥）
       │     ├─ 已过期 → 401（不回退主密钥）
       │     └─ 命中   → 200，返回 Claims(KeyId/KeyName/AuthMethod)，60s 节流写 LastUsedAt
       └─ ② 回退主密钥：解密 ForgeSetting.ApiToken → FixedTimeEquals 定长比较
             └─ 表缺失/库异常 → 记 WARN 后仍走主密钥判定（认证绝不 500）
```

- 主密钥与子密钥走**同一条** Bearer 校验链路，`/v1/*`（LLM 代理）与 `/api/*` 一视同仁。
- 子密钥按摘要定位意味着**不校验前缀**：`sk-`、`cs-sk-`、`gpu…` 等任意令牌格式均可作为子密钥。

### 2.2 密文读写规则

| 场景 | 行为 |
|------|------|
| 写（Encrypt） | 永远产出 `v2:` + `base64(IV[16]+密文)`，用当前密钥（配置 > 环境变量 > 机器派生） |
| 读 v2 | 只用当前密钥解，失败即抛（不存在"其它机器"的可能） |
| 读 v1（无前缀） | 遍历全部候选密钥（当前密钥 → 旧硬编码兼容键）→ **明文合理性过滤**（非空/无 `U+FFFD`/无控制字符）→ 恰好 1 个采信；多个候选都合理则**拒猜抛错**；0 个抛「无法在本机解密」 |
| 迁移写回 | 双闸：明文合理性 + `Decrypt(Encrypt(plain)) == plain` 往返一致；任一不过 → 保留原值不写回 |

### 2.3 托盘免登录（`#token=` 消费）

```
托盘 OnOpenMainPage → 实时取主密钥明文 → Uri.EscapeDataString
  → 打开 http://localhost:{port}/#token=<明文>
     ├─ 冷加载：main.ts 首屏同步 consumeTokenFromHash() → 写 localStorage(forge_api_token)
     │          → history.replaceState 清空 fragment → initAuthToken 因已有 token 跳过
     └─ 同页已打开：浏览器只改 fragment 不重载 → hashchange 监听兜底
                → 消费成功即整页刷新（consumeTokenFromHash 内部用 replaceState，无死循环）
```

> fragment 不会发往服务器，token 不出现在访问日志中。

### 2.4 实现位置

| 文件 | 职责 |
|------|------|
| `Security/MachineKeyProvider.cs` (+`IMachineKeyProvider`) | 机器熵源采集 + PBKDF2 派生 + 指纹 |
| `Security/AesSecretEncryptionService.cs` | v1/v2 版本化加解密 + 候选钥匙链 + `IsPlausiblePlaintext` |
| `Services/SecretMigrationService.cs` | 启动期 v1→v2 迁移（幂等 + 写回双闸） |
| `Services/ApiKeyService.cs` | 子密钥 CRUD/轮换 + `ResolveByToken` 认证判定 |
| `Entities/ApiKeyCredential.cs` (+`.Biz.cs`) | 子密钥实体（`ConnName=ForgeSelf`，`KeyHash` 唯一索引） |
| `Controllers/ApiKeysController.cs` | `api/api-keys` 管理端点（全部 `[Authorize("ApiKeyPolicy")]`） |
| `Services/TrayIconManager.cs` | 托盘 URL 拼接 `#token=` |
| 前端 `services/authInit.ts` | `consumeTokenFromHash` / `installTokenHashWatcher` |
| 前端 `components/settings/ApiKeysPanel.vue` | 「API 密钥」面板 |
| 前端 `services/apiKeysApi.ts` / `types/apiKey.ts` | API 封装与类型 |

## 3. 使用指南

### 3.1 创建 / 分发子密钥

设置页 → API 密钥 → 新建密钥 → 弹窗内**仅此一次**展示明文与现成的 `Authorization` 头 → 复制交给第三方客户端。该客户端即可访问 `/v1/*`（当 LLM 代理用）与 `/api/*`。停用/删除后**立即 401**。

### 3.2 补录一把「升级前就在用」的既有密钥

子密钥由系统生成明文，不支持直接输入自定义值；如需把既有令牌纳入管理（避免使用方报错），按下面两步（本机执行）：

1. 经系统 API 建行（让 XCode 自己写好日期等字段）：

```bash
curl -X POST -H "Authorization: Bearer <主密钥>" -H "Content-Type: application/json" \
  -d '{"name":"个人密钥","remark":"既有密钥补录"}' http://localhost:51888/api/api-keys
```

2. 只覆写两个字符串列（`KeyCipher` 用当前密钥加密、`KeyHash` 为明文 SHA-256 小写 hex）。派生密钥的独立复现配方：`PBKDF2-HMAC-SHA256("ForgeSelf|<MachineGuid>", "ForgeSelf.SecretEncryption.v2.MachineBound", 210000, 32)`，AES-256-CBC，密文 = `v2:` + `base64(IV[16]+密文)`。

3. 验证：`Authorization: Bearer <既有令牌>` 访问 `/api/api-server/status` 应 200；列表中该行掩码变为既有令牌的首尾（如 `cs-****2ca8`）。

### 3.3 跨机器迁移（逃生舱）

在**旧机器**设置 `FORGESELF_ENCRYPTION_KEY`（或 `Encryption:Key`）再启动一次，存量 v1/v2 密文会以该密钥重新落盘；在**新机器**配置同一个值启动即可解密。逃生舱优先级始终高于机器派生密钥。

## 4. 注意事项（已踩坑 / 关键取舍）

1. **padding 校验通过 ≠ 密钥正确**：AES-CBC + PKCS7 在错误密钥下约 **1/256** 概率恰好通过填充校验并解出乱码。绝不可把「解密不抛异常」当作候选密钥的判别器——必须叠加明文合理性过滤（本次已因此修掉一个「迁移静默销毁密钥」的 HIGH 缺陷）。
2. **明文合理性校验不得假定格式**：`AIProvider.ApiKey` 是用户填的第三方密钥（`sk-…`、`AIza…`、`gpu…` 均有可能），只判「像不像文本」（无 `U+FFFD`、无控制字符），不能要求 `sk-` 前缀。
3. **明文红线**：列表接口永不返回明文/密文/摘要；创建与轮换的明文只在当次响应出现；异常日志只记 `ex.Message`，绝不记请求体。
4. **主密钥重新生成不影响子密钥**；停用子密钥后该密钥立即 401 且**不回退**主密钥。
5. **子密钥数量软上限 50**（`ApiKeyService.MaxKeyCount`）。

## 5. 测试覆盖

| 测试 | 覆盖点 |
|------|--------|
| `Unit/MachineKeyProviderTests.cs` | 熵源降级链、派生稳定性、指纹 |
| `Unit/AesSecretEncryptionV2Tests.cs` | v2 加解密、v1 回退链、**padding 碰撞下仍返回真实明文**、无合理明文即抛 |
| `Unit/SecretMigrationServiceTests.cs` | 迁移幂等、可疑结果**保留原值不写回**、往返一致 |
| `Unit/ApiKeyServiceTests.cs` | 子密钥 CRUD/轮换/启停/过期、认证判定、库异常回退主密钥 |
| `Unit/ApiKeysControllerTests.cs` | 端点 500 外壳（`{success:false}`，不泄堆栈）与 404 外壳 |
| `Services/TrayIconManagerTests.cs` | `#token=` URL 拼接与退化 |
| 前端 `services/__tests__/authInit.test.ts` | hash 消费、同页 hashchange 兜底、旧 token 被覆盖 |
| 前端 `components/settings/__tests__/ApiKeysPanel.test.ts` | 面板渲染与交互 |
