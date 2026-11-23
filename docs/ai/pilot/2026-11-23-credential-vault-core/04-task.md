---
task_id: PILOT-086
feature: 100                      # docs/02-features/100-secret-encryption.md（凭据/密钥加密域）
risk: { level: L2, triggers: ["ForgeSelf.Api/**", "ForgeSelf.Host/**", "新增管理面控制器"], raised_by_agent: false }
gate1: { mode: pre-approved, at: 2026-11-23T11:46, ref: "输入17 用户直接指令「开工作树或者新分支去实现它」；PILOT-080/081 先例：用户直接指令 = 闸门1 依据；审批策略已改为免批（AGENTS §8.5）" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Host/Security/ISecretKeyProvider.cs
  - ForgeSelf.Host/Security/VaultKeyProvider.cs
  - ForgeSelf.Host/Security/EnvelopeSecretCrypto.cs
  - ForgeSelf.Abstractions/SecretRef.cs
  - ForgeSelf.Api/Entities/StoredSecret.cs
  - ForgeSelf.Api/Entities/CredentialGrant.cs
  - ForgeSelf.Api/Entities/SecretAuditEvent.cs
  - ForgeSelf.Api/Services/Vault/VaultModels.cs
  - ForgeSelf.Api/Services/Vault/ISecretStore.cs
  - ForgeSelf.Api/Services/Vault/SecretStore.cs
  - ForgeSelf.Api/Services/Vault/ICredentialBroker.cs
  - ForgeSelf.Api/Services/Vault/CredentialBroker.cs
  - ForgeSelf.Api/Controllers/SecretsController.cs
  - ForgeSelf.Api/AppBuilder.cs
  - ForgeSelf.Api.Tests/Unit/VaultKeyProviderTests.cs
  - ForgeSelf.Api.Tests/Unit/EnvelopeSecretCryptoTests.cs
  - ForgeSelf.Api.Tests/Unit/SecretRefTests.cs
  - ForgeSelf.Api.Tests/Unit/CredentialVaultStoreTests.cs
  - ForgeSelf.Api.Tests/Unit/SecretsControllerTests.cs
  - docs/README.md
  - docs/02-features/100-secret-encryption.md
  - docs/06-research/003-agent-secret-management.md
  - docs/08-security/credential-vault-design.md
  - docs/ai/pilot/2026-11-23-credential-vault-core/
rollback: "整批新增文件 + AppBuilder 3 行注册，git revert <commit> 即净退；无既有表结构改动、无数据迁移"
writeback: { feature_doc: 100-secret-encryption, reason: "完成后在 docs/02-features/100-secret-encryption.md 追加 Credential Vault 小节" }
---

# Agent Task — Credential Vault P1–P3 核心（PILOT-086）

> 阶段：Stage 4｜前序工件 00/01/02/03 已齐；闸门1 = 用户输入17 直接指令预批（见 YAML）。

## Task ID

PILOT-086

## Objective

宿主具备凭据保险库 v1：SecretRef 引用、AES-256-GCM 信封加密（DPAPI 保护的随机 KEK）、Grant 授权矩阵、脱敏审计与 `api/secrets` 管理面——全部 AC 可由真实测试命令判定，存量加密体系零改动。

## Scope

### Allowed

- 03-plan「Files To Change」列出的全部新增文件 + 3 处修改（AppBuilder 注册、docs/README.md 索引、100-secret-encryption.md 回写）。
- 测试文件挂 `[Collection("XCode")]` + `XCodeTestFixture`（禁手搓连接串）。
- 若 ProtectedData 需显式包：`ForgeSelf.Host/ForgeSelf.Host.csproj` 增加 `System.Security.Cryptography.ProtectedData`（微软官方包；Evidence 记录理由）。

### Forbidden

- 修改既有加密体系（ISecretEncryptionService/AesSecretEncryptionService/MachineKeyProvider 及其调用方）。
- 修改任何既有表/列、既有控制器/服务、鉴权策略（ApiKeyPolicy 体系本体）。
- 向插件 seed Vault 契约；新增除 ProtectedData 外的任何依赖；前端/插件目录任何改动。
- 任何响应/日志/审计中出现明文或密文材料。

## Acceptance Criteria

02-spec「Acceptance Criteria」AC-1 ~ AC-14 逐条勾验（此处不重复）。

## Expected Files

见 YAML `expected_files`（= 03-plan Files To Change 并集）。

## Verification Commands

```bash
dotnet build ForgeSelf.slnx
cd ForgeSelf.Api.Tests && dotnet test
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-commit-hygiene.ps1 -Staged
```
