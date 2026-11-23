# 01 Intent — Credential Vault P1–P3 核心（PILOT-086）

> 阶段：Stage 1｜任务日：2026-11-23｜Task ID：PILOT-086
> 用户输入17 原文：「xxred对应本项目有pr关于存储机密的方案，你开工作树或者新分支去实现它」
> —— 闸门1 依据 = 用户直接指令预批（PILOT-080/081 先例：直接指令 = 闸门1 依据；审批策略已改为免批，按 AGENTS §8.5 自主迭代）。

## Problem

宿主目前只有「敏感字段加密落库」能力（`ISecretEncryptionService`，AES-256-CBC，机器派生密钥），被 AI Provider ApiKey、API 子密钥、IM 网关 Secret、代理密码各自内嵌使用。它解决的是「数据库文件泄露时字段不落明文」，但不是通用凭据保险库：

1. 没有**通用凭据实体**——每种凭据各自建列/建表，新增一种凭据就要改一处 schema；
2. 配置里存的是**密文本身**而不是**引用**，凭据无法在「不入 Prompt/日志/配置」的前提下被多处安全复用；
3. 没有**使用权与读取权分离**——能碰到配置的人/代码就能解密；
4. 没有**授权（Grant）、审计、吊销**与「Fail Closed」语义。

以上 = xxred 设计提案（`docs/08-security/credential-vault-design.md`）§2「现状与缺口」的结论，本批按其建议先落 P0–P3。

## Why

- 铸己匣的定位是「个人可扩展、可长期演进的智能体平台」，Agent/插件将越来越多地需要用 API Key、Token、数据库密码等凭据；没有宿主级保险库，每个新集成都被迫「把密文再抄一份」或把明文塞进配置。
- 方案已完成调研（OpenClaw/Hermes/Goose/OpenHands 四方对比）与设计评审稿，用户点名实现——属既有规范覆盖内的迭代，按 AGENTS §8.5 自主执行。

## Expected Outcome

- 宿主出现**凭据保险库 v1**：StoredSecret（AES-256-GCM 信封加密，随机 DEK + KEK 包封）、CredentialGrant（授权）、SecretAuditEvent（脱敏审计）三张新表 + SecretRef 引用契约 + KEK 提供器（Windows DPAPI 优先）+ 管理面 API（`api/secrets` 系 10 端点，类级 ApiKeyPolicy）。
- **任何 API 响应、日志、审计都不出现明文与密文材料**；使用明文只有宿主可信代码经授权校验后的内部通道（本批无 UI、无 Adapter 消费端）。
- 存量加密体系零改动、零迁移；xxred 两份方案文档随本批入库并登记 docs/README.md。

## Constraints（规范 §1 逐条对照）

1. 不修改生产环境——只改仓库代码 ✅
2. 不修改数据库结构——**本批只新增 3 张表**（ConnName 沿用 `ForgeSelf`，additive），不改任何既有表/列，无数据迁移 ✅
3. 不修改鉴权/权限/支付/安全核心——`ApiKeyPolicy` 体系不动，新控制器**沿用**该策略 ✅
4. 不新增大规模依赖——AES-GCM 用 BCL `System.Security.Cryptography.AesGcm`，DPAPI 用 BCL `ProtectedData`（Windows 內建，无需新包，net10.0 BCL 已含）✅（实现时若确认需显式包引用再按升级规则上报）
5. 不无关重构——全部为新增文件 + AppBuilder 3 行注册 ✅
6. 不改无关文件——仅 Plan 列出的文件 ✅
7. 最终可运行实际测试验证 ✅（中档门禁：全量 `dotnet test`）

## Success Criteria（可验证）

1. `dotnet build ForgeSelf.slnx` 绿。
2. 新增单测全绿（信封往返/篡改拒绝/Nonce 唯一、KEK 稳定与落库隔离、Grant 授权矩阵、Store 生命周期、控制器外壳与明文红线）。
3. 全量 `dotnet test` 无「归本批的新增红」（对表基线）。
4. 改动面 = Plan 文件清单，无越界 diff。
5. 回复完成时 05/06/07 齐备，通过 `verify-pilot-artifacts.ps1` 机械校验。
