# 02 Spec — Credential Vault P1–P3 核心（PILOT-086）

> 阶段：Stage 2｜任务日：2026-11-23｜Task ID：PILOT-086
> 推导来源：`docs/08-security/credential-vault-design.md`（xxred 839dfc2）+ 00-repository-understanding 实测事实。与设计稿的偏差逐条在「与设计稿的偏差」节登记。

## Functional Requirements

- **FR-1 凭据实体**：`StoredSecret`——Id、RefId（`sec_` + 32 hex，唯一）、Name、SecretType、Provider、OwnerType/OwnerId、MetadataJson（禁放机密值）、FormatVersion、KeyId、WrappedDek、WrapNonce、WrapTag、Nonce、Ciphertext、Tag、RotationVersion、Status（active/revoked）、ExpiresAt、RevokedAt、CreatedAt/UpdatedAt。
- **FR-2 授权实体**：`CredentialGrant`——Id、SecretId、SubjectType/SubjectId、Operation、TargetConstraint、ValidFrom/ExpiresAt/RevokedAt（DateTime.MinValue = 不限）、CreatedBy、CreatedAt。
- **FR-3 审计实体**：`SecretAuditEvent`——Id、SecretId、GrantId、Actor、SubjectType/SubjectId、Action、Target、Result（allowed/denied/error）、ReasonCode、CorrelationId、CreatedAt。**禁止记录明文、密文、Authorization 头、完整连接串**（设计 §4）。
- **FR-4 引用契约**：`SecretRef`（ForgeSelf.Abstractions）——`{"source":"vault","id":"sec_…","version":"current|固定版本"}` 的解析/生成/校验；引用只定位不携带授权（设计 §4）。
- **FR-5 主密钥提供器**：`ISecretKeyProvider`（Host/Security）——首次使用随机生成 32 字节 KEK 并按平台保护持久化；`VaultKeyMaterial{KeyId, Key, ProtectionMode}`；Windows=DPAPI **LocalMachine**（宿主以桌面/服务两种形态运行，CurrentUser 会让「服务模式读不到桌面形态写的 KEK」——沿用 IMachineKeyProvider 排除用户 SID 的同一教训），非 Windows=0600 受保护文件 + 显式 WARN。
- **FR-6 信封加密**：AES-256-GCM（`AesGcm`，nonce 12B、tag 16B）——每条 Secret 值随机 DEK；数据用 DEK 加密（AAD 绑定 `ev1|refId|ownerType|ownerId|rotationVersion`）；DEK 用 KEK 包封（独立 wrap nonce + wrap tag）；库里只存 WrappedDek/WrapNonce/Nonce/Ciphertext/Tag/KeyId/FormatVersion（设计 §5）。
- **FR-7 存储服务**：`ISecretStore`——Create/List/Get/Update 元数据/SetSecretValue（轮换：新 DEK + RotationVersion+1）/Revoke/Delete（先吊销全部 Grant 再删行）/Grant 管理/`GetValueForHost(ref, UseContext)`（授权校验 → 解密 → 审计；**仅宿主可信代码**，不设 HTTP 端点）。
- **FR-8 授权校验**：`ICredentialBroker.Authorize`——校验 Secret 状态与有效期 + Grant 命中（Subject/Operation/Target/有效期/未吊销），未命中即拒绝并写审计 denied（设计 §6，P3 范围不含短时 Lease，见偏差表）。
- **FR-9 管理 API**：`SecretsController`（`api/secrets`，类级 `[Authorize("ApiKeyPolicy")]`）——按设计 §8 十端点：GET /api/secrets、POST /api/secrets、GET /api/secrets/{id}、PATCH /api/secrets/{id}、PUT /api/secrets/{id}/value、DELETE /api/secrets/{id}、GET|POST /api/secrets/{id}/grants、DELETE /api/secrets/{id}/grants/{grantId}、GET /api/secrets/audit。`{id}` 接受数字 Id 或 `sec_*` RefId。
- **FR-10 DI 接线**：AppBuilder 注册 ISecretKeyProvider / ISecretStore / ICredentialBroker（单例；KEK 目录 = `{数据根}/keys`）。**不向插件共享表 seed 任何 Vault 契约**。

## Input

- 管理 API：Bearer 宿主令牌（ApiKeyPolicy 既有体系）。Create 请求体：name、secretType（`ApiKey|Token|Password|PrivateKey|Custom`）、provider?、ownerType?/ownerId?（默认 host/host）、metadataJson?、value（明文，≤10000 字符）、expiresAt?；SetSecretValue 请求体：value；CreateGrant 请求体：subjectType、subjectId、operation、targetConstraint?、validFrom?、expiresAt?。
- 宿主内部使用：UseContext{Actor, SubjectType, SubjectId, Operation, Target, CorrelationId}。

## Output

- 所有响应 = `{ success:true, data }` / `{ success:false, error }` 外壳；**任何响应不含 value / WrappedDek / Nonce / Ciphertext / Tag**；列表与详情为元数据 DTO（含 refId 与 ref JSON 串、grantCount）。
- 审计端点返回脱敏事件列表（limit 默认 100，上限 500）。

## Business Rules

- BR-1 配置只存 SecretRef，不存明文/密文（设计 §1.2）；知道 SecretRef 不代表能使用（BR-4）。
- BR-2 使用权与明文读取权分离：明文唯一出口 = `GetValueForHost`（宿主可信代码、经 Broker 授权），无管理 API 明文端点、无「显示原文」（设计 §7）。
- BR-3 每次创建/轮换重新生成 DEK 与 Nonce；RotationVersion 单调 +1；AAD 含 RotationVersion → 旧轮值材料对新轮 AAD 校验必失败。
- BR-4 **Fail Closed**：无授权/已吊销/已过期/未生效/Subject·Operation·Target 不匹配/解密失败/KEK 不可用 → 一律拒绝 + 审计 denied；不回退明文、不用源码默认密钥（设计 §1.6/§9.9）。
- BR-5 KEK：随机 32 字节、不来自产品名/机器名/固定串；Windows 存 DPAPI LocalMachine 密文文件（应用常量熵做域分隔；桌面/服务两形态同读）、非 Windows 存 0600 文件并 WARN；KEK 不与 SQLite 同文件同目录（`{数据根}/keys/vault-kek.v1.bin`）（设计 §5）。
- BR-6 Grant 默认拒绝：插件安装不自动获得任何 Grant；撤销立即生效（无缓存）。
- BR-7 删除 Secret：先吊销全部 Grant → 删行 → 审计保留（审计行不随 Secret 删除）。
- BR-8 TargetConstraint 语义：空 或 `*` = 不限目标；否则 Target 大小写不敏感全等匹配（P1 最小语义，记录于偏差表）。

## Boundary Conditions

- value 空/超 10000 字符、secretType 非法、metadataJson 非法 JSON 或超 2000 字符 → 400（success:false 外壳）。
- idOrRef 不存在 → 404（NotFound + success:false 外壳，沿用 ApiKeysController 先例）。
- 吊销后轮换/授权/使用 → success:false（reason=secret_revoked）。
- audit limit>500 → 按 500 截断。
- KEK 文件损坏（长度非 32/解封失败）→ 抛异常（Fail Closed），服务不可用但不影响宿主其它功能（Vault 服务独立注册）。

## Error Handling

- 业务失败 → HTTP 200/404 + `{success:false,error}`（ApiKeysController 外壳惯例）；未预期异常 → HTTP 500 + 同外壳，只记 `ex.Message`，不透堆栈、不含密文材料。
- 解密路径所有失败统一 `CryptographicException`（信封层）→ 上层转为审计 denied/error，不区分泄漏失败细节。

## Compatibility

- 既有 `ISecretEncryptionService`（CBC v1/v2）、AI Provider、API 子密钥、IM 网关、代理密码 **零改动**（设计 §5「现有加密兼容」）。
- 新表仅 additive（ConnName=ForgeSelf，XCode 自动建表），不改既有表列，无迁移。
- `docs/README.md` 按 xxred PR 原样并入两行索引；两份方案文档原样入库（已核对提交卫生：无盘符绝对路径、无个人标识、外链均为公开文档）。

## Non-functional Requirements

- 加密：AES-256-GCM（BCL），nonce 每次随机；不引入第三方加密库。
- 明文驻留：解密产出为 .NET string——CLR string 无法可靠即时擦除，如实声明不承诺 ZeroMemory 语义（设计 §9.11）。
- 测试：单测全部 <1s；实体测试挂 `[Collection("XCode")]` + `XCodeTestFixture`，禁手搓连接串（XCodeTestFixtureIronRuleTests 守卫）。

## Interaction Design（交互设计 · 必写节）

**本批无用户可见 UI（P1 API-only）**——「设置 → 凭据」页面属设计 §7 的 P2 后续批。无界面交互形态：

- 经宿主 API 操作（需 `Authorization: Bearer <forge_api_token>`，前端 `request.ts` 已全局注入）：
  - 创建：`POST /api/secrets`，body `{"name":"OpenAI Key","secretType":"ApiKey","provider":"openai","value":"sk-…"}` → `{"success":true,"data":{"refId":"sec_…","ref":"{\"source\":\"vault\",\"id\":\"sec_…\",\"version\":\"current\"}",…}}`，响应**无** value。
  - 轮换：`PUT /api/secrets/{id}/value`，body `{"value":"sk-新"}` → 成功响应**不回显**任何值。
  - 错误：无令牌 → 401（策略层）；不存在的 id → 404 `{"success":false,"error":"凭据不存在"}`；参数非法 → 200 `{"success":false,"error":"…"}`（沿用宿主外壳惯例）。
- 后续 UI 批须补「点什么出现什么」全表 + 五态 + 走查（不属本批 AC）。

## Failure Modes（v1.2.0 必写）

| 命中问题类 | 本批如何满足 / 为何不适用 |
| --- | --- |
| PC-002 宿主管理面控制器漏鉴权（AIProviderController 先例） | 新控制器类级 `[Authorize("ApiKeyPolicy")]` + 反射守卫断言进 SecretsControllerTests |
| PC-034 插件侧构造注入 IServiceProvider 导致宿主契约恒 null | 本批不向插件 seed/注册任何 Vault 契约，插件无消费面；后续 Adapter 批落地时按 PC-034 用 `ctx.Get<T>()` 教案处理（记入 07 悬空教训） |
| 明文/密文外泄类（设计 §9 红线） | 响应投影 DTO 无 cipher 字段；测试断言「创建/列表/轮换/审计」响应序列化串不含 value 与密文子串；审计字段白名单 |

## Acceptance Criteria

- [ ] AC-1 信封往返：Encrypt→Decrypt 还原明文（GCM，AAD 一致）。
- [ ] AC-2 篡改拒绝：Ciphertext/Nonce/Tag/WrappedDek/WrapNonce/AAD 任一被改 → 解密抛 CryptographicException。
- [ ] AC-3 同一明文两次加密 Nonce 与 Ciphertext 均不同（设计验收 §11.1）。
- [ ] AC-4 错误 KEK 解密失败（设计验收 §11.3 之「错误 Key 失败」）。
- [ ] AC-5 KEK 稳定与隔离：同 keyDir 两实例密钥/KeyId 一致；不同 keyDir 密钥不同；默认落 `{keyDir}/vault-kek.v1.bin` 且不进 SQLite 所在目录。
- [ ] AC-6 KEK 文件保护：Windows 下文件字节 ≠ KEK 明文（DPAPI）；非 Windows 下文件 mode 0600（条件断言，非 Windows 才跑）。
- [ ] AC-7 Grant 矩阵：无 Grant/已吊销/已过期/未生效/Subject 不匹配/Operation 不匹配/Target 不匹配 → 均拒绝；全匹配 → 允许（设计验收 §11.5）。
- [ ] AC-8 Secret 已吊销/已过期 → GetValueForHost 拒绝 + 审计 denied。
- [ ] AC-9 明文红线：创建/列表/详情/轮换/审计的响应体均不含 value 与任何密文材料字段（序列化串断言）。
- [ ] AC-10 审计落行：create/update_value/use_allowed/use_denied/grant_revoke 均有事件且事件字段无明文。
- [ ] AC-11 删除：Grant 全清、行消失、审计仍可查。
- [ ] AC-12 SecretsController 类级 `[Authorize("ApiKeyPolicy")]`（反射断言）。
- [ ] AC-13 `dotnet build ForgeSelf.slnx` 绿；全量 `dotnet test` 无归本批的新增红（对表基线）。
- [ ] AC-14 改动面 = 03-plan 文件清单（无越界 diff）。

## Unknown

| 不确定点 | 影响 | 处理方式（询问/搁置/保守假设并标注） |
| --- | --- | --- |
| 非 Windows 生产 KEK 正式通道（Secret Service / systemd credentials） | Linux 部署的 KEK 保护强度弱于 DPAPI | 保守假设：0600 文件 + 启动 WARN 日志；设计 §12 本就列为未决风险，后续批接 Secret Service（记 07 悬空教训） |
| `ProtectedData` 在 net10.0 是否需显式 PackageReference | 若需要 = 新增 1 个微软官方包 | 实现时核实：需要则引用 `System.Security.Cryptography.ProtectedData`（微软 BCL 配套包，不属「大规模依赖」），Evidence 记录 |
| 并发轮换竞态（两人同时 PUT value） | RotationVersion 可能交错 | 保守假设：桌面单用户场景，不做乐观锁（XCode 单行 Save 原子）；07 记录为已知限制 |
| Grant 的 Target 语义（前缀/通配/端口） | 授权粒度 | P1 最小语义（全等 或 `*`），后续 P4 接 HTTP Adapter 时按需扩展 |

## 与设计稿的偏差（实现范围裁决）

| 设计稿条目 | 本批处置 | 理由 |
| --- | --- | --- |
| P3「签发短时 Lease」 | **缓建**：Broker 只做校验+审计，无 Lease | 本批无任何 Adapter 消费端，Lease 属无消费方的投机接口（YAGNI）；P4 接 LLM/HTTP Adapter 时随首个消费端落地 |
| P2「管理 UI」 | 缓建（本批 API-only） | 设计 §12 建议 P0–P3 先行；UI 属下一批，届时补 Interaction Design 全表 |
| §8 表中「测试连接」语义 | 本批无（无 Adapter） | 同上，P4 范围 |
| §5「主密钥轮换只重新包装 DEK」 | 缓建 KEK 轮换 API | 无 UI 与运维入口前无可触发场景；KeyId 机制已预留 |
