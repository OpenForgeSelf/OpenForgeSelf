# 设计方案：铸己匣统一机密与凭据管理（Credential Vault）

> 状态：Proposal，待架构评审；不代表代码已实现。  
> 日期：2026-11-22  
> 调研依据：`../06-research/008-agent-secret-management.md`  
> 技术约束：C# + SQLite + NewLife.XCode，不引入 EF Core。

## 1. 目标与原则

建设宿主级 Credential Vault，为 LLM API Key、HTTP Token、数据库密码、SSH 私钥、OAuth Token、网站登录凭据和证书提供统一录入、加密存储、引用、授权、运行时使用和审计。

1. 凭据归宿主管理，插件不得自行持久化宿主级凭据。
2. 配置只保存 SecretRef，不保存明文。
3. 使用权与明文读取权分离；默认授予 Use，不授予 ReadValue。
4. 宿主通过协议 Adapter 代为使用凭据。
5. 按 User / Agent / Plugin / Task / Runtime、操作、目标和有效期授权。
6. 过期、吊销、解析失败或权限不匹配时 Fail Closed。
7. 保留现有 ISecretEncryptionService 和存量密文兼容能力；新 Vault 使用独立认证加密格式。

## 2. 现状与缺口

项目已有 ISecretEncryptionService、AES-256-CBC 密文存储、版本前缀、旧密文兼容读取/迁移，以及 API Key 掩码、停用、轮换、删除和过期等能力。这解决部分敏感字段不以明文落库，但不是通用凭据保险库。

缺口：通用凭据实体与管理入口、SecretRef、Agent/Plugin/Task 级授权、Broker/Adapter、Terminal/MCP/子进程传递规则、访问审计和主密钥托管/恢复设计。

非目标：不替换现有 API Server Key 认证/哈希索引；不宣称防御 Root/管理员或已攻陷宿主进程；不向任意插件开放导出全部明文；首期不实现企业级 KMS、多租户密钥托管和自动证书签发。

## 3. 总体架构

```text
Host UI / API
  └─ Credential Control Plane
       ├─ SecretRef Registry
       ├─ Grant Policy
       └─ Audit
            └─ Credential Broker
                 ├─ Secret Store（密文）
                 ├─ Secret Key Provider（主密钥）
                 └─ Credential Adapters
                      ├─ LLM / HTTP / MCP
                      ├─ Database
                      ├─ SSH
                      └─ Browser
```

Control Plane 管元数据、引用、授权和生命周期；Store 管密文；Key Provider 保护主密钥；Broker 校验主体、操作、目标和有效期；Adapter 在协议边界代为使用凭据。

## 4. 数据模型

### StoredSecret

Id、Name、SecretType、Provider、OwnerType/OwnerId、MetadataJson（禁止放机密值）、CipherVersion、KeyId、WrappedDek、Nonce、Ciphertext、Tag、CreatedAt、UpdatedAt、ExpiresAt、RevokedAt、RotationVersion。

### CredentialGrant

Id、SecretId、SubjectType/SubjectId（User/Agent/Plugin/Task/Runtime）、Operations（HttpRequest/DbConnect/SshConnect/BrowserFill）、ConstraintsJson（目标 Host、数据库、SSH Host、HTTP 方法等）、ValidFrom、ExpiresAt、RevokedAt、CreatedBy、CreatedAt。

Grant 表示使用权，不表示明文读取权。

### SecretAuditEvent

记录 SecretId、GrantId、Actor、AgentId、PluginId、TaskId、SessionId、Action、Target、Result、ReasonCode、Timestamp、CorrelationId。禁止记录明文、密文、Authorization Header、私钥、完整连接字符串或敏感请求体。

### SecretRef

配置只保存稳定引用：

```json
{ "source": "vault", "id": "sec_01J...", "version": "current" }
```

引用只定位，不携带授权。知道 SecretRef 不代表能使用凭据。支持 current 和固定版本。

## 5. 加密与主密钥

新 Vault 使用 AES-256-GCM + Envelope Encryption：
1. 每个 Secret 生成随机 256-bit DEK。
2. DEK + 随机 96-bit Nonce 加密 Secret。
3. AAD 绑定 SecretId、OwnerId、CipherVersion 和数据版本。
4. KEK 加密 DEK。
5. 数据库存 WrappedDEK、Nonce、Ciphertext、Tag、KeyId 和格式版本。
6. 解密前先校验 Grant，再解包 DEK 并校验 GCM Tag。

新增 ISecretKeyProvider，按平台实现 Windows DPAPI/Credential Manager、macOS Keychain、Linux Secret Service/KWallet、systemd credentials/受保护密钥文件，后续可接外部 Vault。

首次安装生成随机主密钥，不得从产品名、机器名或固定字符串构造；主密钥不能与 SQLite 放在同一个未保护文件。机器 ID 不能作为唯一高熵秘密。生产环境无法建立安全主密钥时 Fail Closed，不回退明文或源码默认密钥。数据库备份与主密钥备份分离，并提供恢复演练。主密钥轮换只重新包装 DEK。

### 现有加密兼容

- 现有 ISecretEncryptionService 继续读取历史 CBC v1/v2。
- 新 Vault 独立使用 GCM/Envelope，不覆盖旧格式。
- 第一阶段不迁移 AI Provider/API Token/IM Gateway 存量字段。
- 后续 AI Provider ApiKey 改为 ApiKeyRef：双读 → 新增引用 → 验证请求 → 清理明文残留 → 关闭旧写入。
- 迁移支持备份、Dry Run、往返校验、失败保留旧值、可重复执行和审计。

## 6. SecretRef、Broker 与 Adapter

每次使用请求必须包含可信上下文：Actor/ActorId、TaskId、SessionId、RuntimeId、Operation、Target（scheme/host/port/resource）、RequestedAt、CorrelationId。身份由宿主运行上下文提供，不信任 Prompt 或工具参数自报身份。

服务抽象：
- ISecretStore：创建、更新、查询元数据、吊销；不向管理 UI 提供明文 API。
- ISecretKeyProvider：主密钥保护、解封和轮换。
- ISecretResolver：解析引用为宿主内部受保护材料。
- ICredentialBroker：校验授权、签发短时 Lease、审计。
- ICredentialAdapter<T>：执行协议操作，尽可能不向插件返回明文。

禁止向普通插件提供无上下文的 GetSecretValue(id)。

| 场景 | 推荐方式 | 明文边界 |
|---|---|---|
| LLM | Provider Adapter / HTTP Transport 出站注入 | 宿主请求发送边界 |
| HTTP/MCP | 受控 HttpClient 按目标 Host 注入 Header | 不进入 Prompt/工具参数 |
| DB | DbConnectionFactory 建立授权连接 | 返回受限连接对象 |
| SSH | SshSessionFactory / 受控 ssh-agent | 不返回私钥文本 |
| Browser | 按 Origin 填充 | 不进入 DOM 快照/工具结果 |
| Shell | 默认不透传；显式 Consumer 白名单 | 子进程可读取透传变量 |

**进程边界：** 同进程 .NET 插件可检查宿主内存和进程权限范围内文件。Broker 是能力治理，不是恶意插件沙箱。不可信插件必须运行在独立进程/容器，通过 IPC Broker 使用受限操作。

## 7. 授权与管理 UI

授权维度：所有者、消费者、操作、目标和有效期。默认拒绝；插件安装不自动获得凭据。插件升级若权限扩大，重新请求授权；卸载撤销 Grant。

Agent 请求凭据时只提交需求元数据；Host 通过可信 UI 遮罩输入，API 直接写 Store；Agent 只得到 SecretRef 和授权结果，不返回值、密文或可逆掩码。禁止通过普通聊天消息收集密码。

设置 → 凭据：
- 列表显示名称、类型、Provider、所有者、授权数量、状态、更新时间和过期时间。
- 新建/轮换使用受保护输入。
- 详情只读元数据，不提供“显示原文”。
- 授权页面查看和撤销 Grant。
- 测试连接由 Host Adapter 发起，只返回脱敏结果。
- 删除先吊销授权，再删除活动值；保留必要审计元数据。

## 8. 管理 API 草案

| 方法 | 路由 | 用途 |
|---|---|---|
| GET | /api/secrets | 列表元数据，不返回值/密文 |
| POST | /api/secrets | 创建，响应只返回 Ref/元数据 |
| GET | /api/secrets/{id} | 元数据和授权摘要 |
| PATCH | /api/secrets/{id} | 修改非机密元数据 |
| PUT | /api/secrets/{id}/value | 轮换值，不回显 |
| DELETE | /api/secrets/{id} | 吊销并删除活动值 |
| GET | /api/secrets/{id}/grants | 查询授权 |
| POST | /api/secrets/{id}/grants | 创建授权 |
| DELETE | /api/secrets/{id}/grants/{grantId} | 撤销授权 |
| GET | /api/secrets/audit | 查询脱敏审计 |

接口复用现有 ApiKeyPolicy 和统一错误响应。Secret 值不得进入 URL、QueryString、日志、异常或调试响应。

## 9. 运行时安全要求

1. Prompt/Memory 只允许 SecretRef 和非机密元数据。
2. Session/Checkpoint 只存引用和授权 ID，不存解析值或 Lease。
3. Tool Result、日志、Trace、异常必须脱敏。
4. Terminal 默认不继承 Vault Secret；禁止放入 argv、命令历史或工作目录文件。
5. MCP 每个 Server 只获自己的授权，不传宿主全部环境变量。
6. 子进程只构造最小环境；白名单变量仍可被子进程读取。
7. 缓存只在内存短 TTL，轮换/吊销后失效。
8. HTTP 凭据绑定目标 Host；重定向到未授权 Host 时不携带认证头。
9. 缺失、过期、吊销、解密失败、Key Provider 不可用、权限拒绝均 Fail Closed。
10. DB 备份与 KEK 备份分离，执行恢复演练。
11. 尽量使用可释放 byte buffer，并在生命周期结束调用 CryptographicOperations.ZeroMemory；不宣称 CLR 字符串可可靠即时擦除。

## 10. 实施路线

| 阶段 | 内容 | 验收 |
|---|---|---|
| P0 | 盘点密钥字段、读取点、配置、插件/MCP/Terminal 传递路径 | Secret Inventory 完整 |
| P1 | StoredSecret、Key Provider、AES-GCM Envelope、SecretRef | 往返、篡改拒绝、错误 Key 失败、主密钥不落库 |
| P2 | 管理 API/UI、轮换、吊销、审计 | UI/API 不返回明文或密文 |
| P3 | Grant、Broker、短时 Lease | 越权/过期/吊销/目标不匹配均拒绝 |
| P4 | LLM Provider、HTTP/MCP 出站注入 | Prompt/日志无 Key，轮换有效 |
| P5 | DB、SSH、Browser Adapter | 只交付授权连接/会话 |
| P6 | Terminal 白名单、MCP 隔离、独立进程 Broker IPC | 默认零 Secret 透传 |
| P7 | Bitwarden、1Password、Vault Provider | 统一引用；外部不可用时明确失败 |

## 11. 验收清单

- [ ] 同一明文多次加密产生不同 Nonce。
- [ ] 篡改 Ciphertext、Nonce、Tag、AAD 均解密失败。
- [ ] DB 不存主密钥；仅复制 DB 无法解密。
- [ ] 无 KEK 时生产启动 Fail Closed。
- [ ] 无 Grant、过期、吊销、目标不匹配均拒绝。
- [ ] Secret 不进入 Prompt、Memory、Session、Checkpoint、Tool Result、日志和 Trace。
- [ ] Terminal 默认读取不到 Vault Secret；MCP A 不能访问 MCP B 凭据。
- [ ] HTTP 跨域重定向不携带原凭据。
- [ ] 旧 CBC v1/v2 继续可读；迁移中断可重试且不覆盖旧值。
- [ ] DB/KEK 分离恢复演练通过。

## 12. 未决风险与决策

- 同进程插件是否定义为可信代码；不可信插件必须先做进程隔离。
- Linux 无头环境需实测 systemd credentials、Secret Service 和受保护文件。
- 主密钥丢失会导致凭据不可恢复，需产品级恢复包/外部 Vault 策略。
- 不同 LLM SDK 的认证注入点需逐 Provider 验证。
- Terminal/MCP 若获得环境变量，就不能承诺其命令看不到值。

**建议先完成 P0–P3**，建立通用引用、授权和 Broker，再接入 LLM/HTTP/MCP；之后分批接入 DB、SSH、Browser 和外部 Vault。
