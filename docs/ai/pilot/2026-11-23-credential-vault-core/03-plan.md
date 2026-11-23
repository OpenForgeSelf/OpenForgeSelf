# 03 Plan — Credential Vault P1–P3 核心（PILOT-086）

> 阶段：Stage 3｜Task ID：PILOT-086｜具体到真实文件路径。

## Files To Change

**新增（14 件代码 + 5 件测试 + 2 件文档随批）**

- `ForgeSelf.Host/Security/ISecretKeyProvider.cs` — KEK 契约 + `VaultKeyMaterial`（KeyId/Key/ProtectionMode）。
- `ForgeSelf.Host/Security/VaultKeyProvider.cs` — 随机 32B KEK；Windows=DPAPI LocalMachine（`System.Security.Cryptography.ProtectedData`，需核实包引用；桌面/服务双形态一致，理由见 02-spec FR-5）；非 Windows=0600 文件 + WARN；文件 `{keyDir}/vault-kek.v1.bin`；Lazy 缓存；损坏即抛（Fail Closed）。
- `ForgeSelf.Host/Security/EnvelopeSecretCrypto.cs` — AES-256-GCM 信封：`Encrypt(plain, aad, kek)` → `EnvelopePayload{KeyId, WrappedDek, WrapNonce, Nonce, Ciphertext, Tag, FormatVersion}`（全 base64）；`Decrypt(payload, aad, kek)`；任何失败抛 `CryptographicException`。
- `ForgeSelf.Abstractions/SecretRef.cs` — record + TryParse/ToJson/NewId（`sec_`+Guid N）。
- `ForgeSelf.Api/Entities/StoredSecret.cs` / `CredentialGrant.cs` / `SecretAuditEvent.cs` — XCode 实体（生成态风格，对齐 `ApiKeyCredential.cs`；ConnName=ForgeSelf；索引 UX_StoredSecret_RefId、IX_StoredSecret_Name、IX_CredentialGrant_SecretId、IX_SecretAuditEvent_SecretId）。
- `ForgeSelf.Api/Services/Vault/VaultModels.cs` — DTO/请求模型：SecretMetaDto、GrantDto、AuditEventDto、CreateSecretRequest、UpdateSecretRequest、SetSecretValueRequest、CreateGrantRequest、UseContext、GrantDecision。
- `ForgeSelf.Api/Services/Vault/ISecretStore.cs` — 契约（含 GetValueForHost）。
- `ForgeSelf.Api/Services/Vault/SecretStore.cs` — 实现：CRUD + 轮换 + 吊销/删除 + Grant 管理 + 审计写入 + 授权后解密。
- `ForgeSelf.Api/Services/Vault/ICredentialBroker.cs` + `CredentialBroker.cs` — 授权矩阵（BR-4/BR-8）。
- `ForgeSelf.Api/Controllers/SecretsController.cs` — 10 端点，类级 ApiKeyPolicy，`{id}` 双形态解析，明文红线 DTO 投影。
- `ForgeSelf.Api.Tests/Unit/VaultKeyProviderTests.cs` — AC-5/6。
- `ForgeSelf.Api.Tests/Unit/EnvelopeSecretCryptoTests.cs` — AC-1/2/3/4。
- `ForgeSelf.Api.Tests/Unit/SecretRefTests.cs` — FR-4 解析/生成/拒绝面。
- `ForgeSelf.Api.Tests/Unit/CredentialVaultStoreTests.cs` — AC-7/8/10/11（XCode 夹具）。
- `ForgeSelf.Api.Tests/Unit/SecretsControllerTests.cs` — AC-9/12 + 500/404 外壳。
- `docs/ai/pilot/2026-11-23-credential-vault-core/00~04` — 本批工件（已建）。

**修改（3 件）**

- `ForgeSelf.Api/AppBuilder.cs` — `:239-251` 安全注册区后追加 3 行单例注册（ISecretKeyProvider 工厂传 `{dataRoot}/keys`、ISecretStore、ICredentialBroker）。
- `docs/README.md` — 按 xxred PR 并入 2 行索引（003 调研 + credential-vault-design 链接）。
- `docs/02-features/100-secret-encryption.md` — 追加「Credential Vault（P1）」小节：能力边界 + 端点表 + 指向设计文档（gate3 前完成）。

## Implementation Steps

1. TDD Red：先写 5 件测试（引用尚未存在的类型 → 编译红，记录红清单）。
2. 契约与加密：ISecretKeyProvider/VaultKeyProvider/EnvelopeSecretCrypto/SecretRef → 绿 Envelope/KeyProvider/SecretRef 测试。
3. 实体三件（XCode 生成态）→ CredentialVaultStoreTests 可编译。
4. 服务层 VaultModels/ISecretStore/SecretStore/ICredentialBroker/CredentialBroker → store/broker 测试绿。
5. SecretsController + AppBuilder 注册 → 控制器测试绿。
6. 文档：docs/README.md 索引 + feature 文档回写 + 工件 05/06/07。

## Test Plan

1. 新增 5 件测试文件（用例数约 30）全绿。
2. 全量 `dotnet test`（中档：碰宿主源码 ForgeSelf.Api/**）对表基线，归本批新增红 = 0。
3. 提交前：`verify-pilot-artifacts.ps1` + `check-commit-hygiene.ps1 -Staged`（提交动作本身待用户批准，本批先把两道机械校验跑绿）。

## Verification

### Build

```bash
dotnet build ForgeSelf.slnx
```

### Unit Test

```bash
cd ForgeSelf.Api.Tests && dotnet test            # 快档：新批相关
dotnet test                                       # 中档：全量（碰 ForgeSelf.Api/**）
```

### Integration Test

N/A——宿主集成测试体系即 `ForgeSelf.Api.Tests`（含 XCode 临时库），已含于上。

### E2E

N/A——本批无前端与插件改动（e2e 决策表「UI/插件变更」不触发）；管理面 HTTP 冒烟由 SecretsControllerTests 直接驱动控制器覆盖。

### Other Checks

- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1`（工件链门禁预跑）。
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-commit-hygiene.ps1 -Staged`（提交卫生，秒级）。
- 前端 `pnpm run check`/`test`、`design:check`：N/A（零前端改动，§5.6 快档触发条件不成立）。

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 12:24 | 调研文档编号冲突：xxred 分支的 `06-research/003-agent-secret-management.md` 在主线已被 `003-deepseek-harness-运行全链路.md` 占用 | 原样入库 003 | 顺延为 `008-agent-secret-management.md`（git mv），`credential-vault-design.md` 内交叉引用与 docs/README.md 索引同步更新 |
| 12:10 | TDD 红：`VaultKeyProviderTests` 缺 using；`SecretRef` 大写 hex 被接受（`Uri.IsHexDigit` 语义） | — | 补 using；SecretRef 校验收紧为仅小写 hex（红→绿，Wave1 33/33 绿） |
| 12:26 | `ForgeSelf.Host.csproj` 新增 `System.Security.Cryptography.ProtectedData` 10.0.8（04-task Allowed 预留项） | — | 需显式包（net10.0 非 Windows 目标不含 DPAPI 包装器），微软官方包 |
