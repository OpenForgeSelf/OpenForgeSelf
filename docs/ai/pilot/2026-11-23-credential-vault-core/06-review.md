# PILOT-086 · 06-Review（自审 · 先审后报）

> 审查人：lead（实现者自审，2026-11-23）｜对象：Wave-1 + Wave-2 全部新增文件 + AppBuilder 注册 + 文档回写
> 审查依据：02-spec FR-1..10 / BR-1..8 / AC-1..14；方案真源 `docs/08-security/credential-vault-design.md`

## 八问

**Q1 需求覆盖：设计稿 P1+P2+P3 承诺的每条是否都有对应实现与测试？**
逐条对表：P1（实体三件/信封/KEK/引用）→ `StoredSecret`/`CredentialGrant`/`SecretAuditEvent`（+.Biz 三件）、`EnvelopeSecretCrypto`、`VaultKeyProvider`、`SecretRef`，测试 Wave-1 33 用例；P2（管理面 API 10 端点 + DI）→ `SecretsController` + AppBuilder 注册，测试 SecretsControllerTests；P3（授权执行 + 审计）→ `SecretStore.GetValueForHost`（Broker 授权 → 解密 → 审计），测试 CredentialVaultStoreTests 全矩阵。**覆盖完整，无缺口**。

**Q2 明文红线（AC-9）是否有机械防而非约定？**
三层：① 响应 DTO（`SecretMetaDto` 等 record）结构上不含 value/信封字段；② SecretsControllerTests 对创建/列表/详情/轮换/审计响应体序列化后逐串断言无明文、无 `wrappedDek/ciphertext/nonce/tag/keyId`；③ 审计写入点参数全部为脱敏字段（Action/ReasonCode/Target 约束值），`GetValueForHost` 是唯一明文出口且为内部方法、无 HTTP 端点。**红线成立**。

**Q3 Fail Closed 是否处处成立（AC-8）？**
吊销/过期（Secret 与 Grant 双层）/无授权/解密失败全部拒绝并落审计（`CredentialBroker` + `EnsureUsable` + `GetValueForHost` catch）；Grant 撤销立即生效（Broker 每次直读库，无缓存）——`Grant_撤销后立即拒绝_无缓存` 用例实证。**成立**。

**Q4 AAD 绑定是否防错位/防篡改（AC-2/AC-3）？**
数据 AAD 由**库内行字段**（refId/ownerType/ownerId/rotationVersion）现算，不信任请求侧；六字段篡改 Theory 全红；轮换换 DEK+Nonce+版本。wrap AAD 绑定 keyId。**成立**。

**Q5 与既有 `ISecretEncryptionService`（CBC v1/v2）是否零干扰？**
Vault 全部新文件新表，未触碰 `AesSecretEncryptionService`/`MachineKeyProvider`/`SecretMigrationService` 与 Model.xml；全量回归无既有红新增（见 05 §2 V3）。**零干扰**。

**Q6 测试是否真实（非同源自洽）？**
实体直查 `FindByRefId` 对表断言（明文不入任何列）、缓存同实例别名坑在测试侧显式规避（轮换前捕获标量快照，实现侧无缺陷）、控制器 500 用桩注入模拟库不可用断言不透堆栈、类级鉴权用反射断言 `ApiKeyPolicy`（PC-002 守卫延续）。**真实**。

**Q7 偏差是否记录且正当（03-plan 偏差表）？**
Renumber 12:24 / TDD 两次红修 12:10 / ProtectedData 包 12:26 均已记录；本批新增偏差：XCode 12 查询静态仅剩 Expression 重载（FindAll/FindCount 写法适配）+ 实体缓存同实例语义 → 见 05 §2。**已记录**。

**Q8 回滚成本（04-task rollback 字段兑现）？**
整批为新增文件 + AppBuilder 3 行注册 + 1 包引用 + 文档，`git revert <commit>` 即净退，无数据迁移（新表惰性建表，删表即净）。**兑现**。

## 发现与处置

| # | 级别 | 发现 | 处置 |
|---|------|------|------|
| 1 | Minor（已修） | `SetSecretValue` 对不存在目标抛 `secret_not_found`，控制器初版按业务失败回 200，违 AC-9 404 语义 | 四处 catch 补 `secret_not_found → 404` 映射，用例复绿 |
| 2 | Info | XCode 实体缓存返回同一实例：测试若持有轮换前引用会被原地改写干扰断言 | 测试侧捕获标量快照；实现侧单线程改写自持实例，语义正确，不改 |
| 3 | Info | `VaultKeyProvider` 非 Windows 分支 `File.SetUnixFileMode` 触 CA1416 警告 | 存量同类模式一致（运行时平台守卫包裹），不处理 |
| 4 | Info | 审计发起方 P1 固定 `api:admin` | 设计稿 §8 已声明；请求级身份接通时更新，注释已留 |

## Final Decision

**APPROVED**。
条件核验：V3 全量 33 红经逐条对表（主仓同 filter 复跑 + 隔离重跑 + 环境缺件消息原文）全部归因于基线既红（26）/worktree 环境缺件（5）/全量态顺序干扰（2，隔离 10/10 绿），**归本批新增红 = 0**；本批 66 条新增用例全绿。终审通过（2026-11-23 12:55）。
