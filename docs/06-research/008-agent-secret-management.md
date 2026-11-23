# 多 Agent 项目的机密管理方案调研

> 文档类型：跨项目技术调研  
> 调研日期：2026-11-22  
> 状态：已完成  
> 范围：OpenClaw、Hermes Agent、Goose、OpenHands 的凭据存储、引用、运行时交付及隔离方式  
> 结论：以 OpenClaw 的 SecretRef/运行时模型为主要参考，组合 Hermes 的外部密钥源与浏览器凭据库、Goose 的 OS Keyring、OpenHands 的平台管理与按需注入；不直接照搬任何单一项目。

## 1. 调研结论

Agent 机密管理不是单纯的“把 API Key 加密后放进数据库”。完整能力至少包含五部分：

1. **Secret Store**：安全保存 API Key、Token、数据库密码、SSH 私钥、网站账号等。
2. **Secret Reference**：业务配置只保存凭据引用，不保存机密明文。
3. **Credential Resolver**：按照引用和来源取得凭据，并检查有效性。
4. **Credential Broker / Adapter**：验证 Agent、Plugin、Task 的使用权限，在真正调用 LLM、HTTP、数据库、SSH 或浏览器时交付凭据。
5. **Audit & Lifecycle**：写入、轮换、吊销、过期、授权、访问审计和明文残留检查。

关键安全边界：

- “密文落库”解决的是数据库文件泄露风险，不代表运行时安全。
- “不在 Prompt 中”不代表 Agent 无法读取；只要明文出现在 Agent 可执行的 Shell 环境、可读文件、工具结果或日志中，Agent 就可能取到它。
- 环境变量只是兼容既有工具的交付形式，不是不可读取的秘密通道。
- 同进程插件共享宿主进程权限。仅靠插件 ID 做授权属于逻辑隔离，不能防御恶意或被攻陷的进程内插件。
- 要实现“Agent 可以使用、但模型看不到明文”，必须把凭据解析与注入放在宿主可信边界，由专用 Adapter 或出站代理完成。

## 2. 方案横向对比

| 维度 | OpenClaw | Hermes Agent | Goose | OpenHands |
|---|---|---|---|---|
| 主要定位 | Gateway 级机密引用与运行时管理 | 本地 Agent 配置及外部 Secret Source | 桌面/本地 Agent 的凭据保存 | 平台 Settings 与 Agent Runtime |
| 本地存储 | 共享 SQLite Secret Store；文件权限保护 | `.env`、加密浏览器凭据库 | OS Keyring；不可用时回退文件 | 平台 Secret API，密文持久化 |
| 外部来源 | env、file、exec、store；可接 Vault 等 CLI | Bitwarden、1Password、命令行 Helper；第三方可扩展 | Provider 环境变量；OS Keyring | Settings API、LookupSecret 来源 |
| 引用机制 | SecretRef | 环境变量名、Secret Source 配置及内部凭据句柄 | Provider 配置读取 Keyring/环境变量 | LookupSecret / SecretRegistry |
| 运行时交付 | 内存 Snapshot、部分模型请求在出站边界替换 Sentinel | 受控子进程环境变量白名单；浏览器直接填充 | Provider/扩展读取凭据，按配置传递 | 按命令解析引用并注入环境变量；部分不透明子进程需整体解析 |
| 用户录入 | 受保护的 Secrets 工具输入 | CLI/Desktop 设置、浏览器密码录入 | `goose configure` / Settings | Settings → Secrets |
| 轮换/审计 | audit、configure、apply、reload | 外部 Vault 集中轮换；本地 Vault CLI 管理 | Keyring/文件重新配置 | 删除重建或 API 更新；有 Secret API |
| 主要限制 | SecretRef 本身不是进程隔离；明文残留仍可被可读文件访问 | 普通 `.env` 不是加密保险库；权限依赖进程隔离和传递白名单 | 无桌面 Keyring 的环境可能落入明文 `secrets.yaml` | 密钥进入 Runtime 后仍可能被 Agent/子进程读取；不同 Runtime 路径注入范围不同 |

## 3. OpenClaw：SecretRef + Gateway Secret Store

### 3.1 存储来源与引用

OpenClaw 为支持机密的配置字段提供 SecretRef，来源包括：

- `env`：宿主环境变量。
- `file`：从指定文件读取。
- `exec`：调用配置过的命令/CLI 获取。
- `store`：从 Gateway 共享 Secret Store 读取。

配置保存引用而不是 API Key 明文，例如：

```json
{
  "apiKey": {
    "source": "store",
    "provider": "default",
    "id": "OPENAI_API_KEY"
  }
}
```

共享 Store 以本地 SQLite 文件保存团队范围的 Secret/环境值。官方文档说明文件权限为 `0600`；它是本地受权限保护的存储，不应将其等同于 HSM 或独立 KMS。若要求更高等级的密钥托管，应使用外部 Vault Provider。

### 3.2 Agent 安全录入

OpenClaw 提供 `secrets` 工具。Agent 只提出凭据名称、用途和必要的目标主机；用户在受保护的输入界面录入，Gateway 直接写入 Store。

凭据值不会进入聊天内容、会话转录、工具结果或模型上下文。Agent 得到的是元数据和 SecretRef。该工具当前面向主 Agent 会话，不向 Subagent/ACP Worker 开放。

### 3.3 运行时交付

- SecretRef 在激活时解析为进程内运行时快照；请求热路径读取快照。
- 对支持的模型 Provider 出站路径，使用进程内 Sentinel 代替明文，直到受控网络出站边界才替换成真实凭据。
- 未识别的 Sentinel 会阻止请求，避免将未解析占位符发送给上游。
- `secrets reload` 原子发布新快照；失败可按已知 Owner 隔离为不可用或保留最后一次有效值（取决于 Owner 类型和配置变化）。
- `secrets audit` 检查明文残留、未解析引用和优先级冲突；`configure/apply` 用于迁移与清理。

### 3.4 安全边界

SecretRef 只减少配置文件中的明文，不自动隔离 Agent。官方明确指出：如果明文仍在 Agent 可读的 `openclaw.json`、`.env`、生成的模型配置或其他文件中，Agent 仍可经文件或 Shell 读取。

Sentinel 也不是进程隔离：最终出站适配器仍会在进程内短暂接触明文。未使用 SecretRef 的普通环境变量不受 Sentinel 保护。生产部署仍需结合文件权限、容器/进程隔离、网络出口控制和外部 Vault。

**值得借鉴：** SecretRef 统一协议、受保护录入、共享 Store、出站边界注入、原子刷新、明文残留审计。

## 4. Hermes Agent：本地 Secret Source + 外部 Vault + 浏览器凭据库

### 4.1 LLM 与工具凭据

Hermes 默认可从 `~/.hermes/.env` 加载 API Key，也能在进程启动时从外部 Secret Source 获取变量。官方内置来源包括：

- Bitwarden Secrets Manager（`bws` CLI）。
- 1Password（`op://` 引用和官方 CLI）。
- Command Helper：对接 `keepassxc-cli`、`secret-tool`、`pass` 或自定义 CLI。

多个 Secret Source 可以组合；环境变量存在冲突时按明确的优先级和 `override_existing` 策略处理。第三方 Secret Source 可作为独立插件注册，解析优先级、冲突、超时和来源标记由编排层负责。

### 4.2 子进程环境隔离

Terminal、代码沙箱和部分无人值守任务使用经过清理的环境。Hermes 管理的凭据默认不全部透传，只有显式列在 `terminal.env_passthrough` 或 Skill 的 `required_environment_variables` 中的变量才会交给子进程；多 Profile 场景还会按 Profile 选取来源，避免一个 Profile 的凭据进入另一个 Profile 的子进程。

这比“把全部环境变量复制给每个工具”安全，但被允许接收变量的命令仍可能读取、打印或外传该变量。

### 4.3 Passwords & Logins

Hermes 另有浏览器登录凭据库，和普通 LLM `.env` 不是同一套存储：

- 登录密码、卡片等值加密后保存在 Profile 的本地 Vault 文件中。
- 默认密钥位于 `~/.hermes/vault/vault.key`，使用受限文件权限；也可把密钥放在 macOS Keychain、Windows Credential Manager、Linux Secret Service/KWallet。
- 主密码通过受控的非交互通道交给密码管理器 CLI，Agent 不接触主密码、会话 Token 和登录密码。
- 浏览器凭据按保存的 Origin 使用，密码直接填入页面，不作为普通工具结果返回。

### 4.4 安全边界

Hermes 的 `.env` 本身不是加密 Vault；本地文件权限是基础防线。外部 Secret Source 的引导凭据仍需安全保存。被显式透传的环境变量仍可能被子进程读取。浏览器 Vault 的“不暴露给 Agent”能力是专用浏览器登录流程，不等于数据库/SSH 通用凭据代理。

**值得借鉴：** 外部 Secret Source 插件化、Profile 级隔离、子进程变量白名单、专用浏览器凭据库。

## 5. Goose：优先 OS Keyring，失败时回退文件

Goose 将 Provider API Key 与普通 `config.yaml` 分离：

- 默认通过操作系统 Keyring/Keychain 保存凭据。
- Provider 环境变量也可提供凭据，并按文档规定的优先级覆盖存储值。
- `GOOSE_DISABLE_KEYRING` 可禁用系统 Keyring。
- 无桌面 Keyring 的容器、CI、无头服务器，或 Keyring 不可用时，Goose 可能回退到独立的 `secrets.yaml`。

当前官方文档明确说明：文件回退中的 `secrets.yaml` 是明文文件，路径为 macOS/Linux 的 `~/.config/goose/secrets.yaml`，Windows 的 `%APPDATA%/Block/goose/config/secrets.yaml`。因此无头服务环境必须特别注意文件 ACL、备份与运行账户权限，不能把“默认 Keyring”误认为所有部署形态都使用 Keyring。

部分扩展配置还会声明进程环境变量或 HTTP Header；应把这些扩展配置一并纳入敏感配置审计，不能只检查 Provider API Key。

**值得借鉴：** 有 OS Keyring 就优先使用；区分配置和凭据；对 Keyring 不可用的退化路径显式告警，而不是隐藏地假设所有环境都具备系统密钥环。

## 6. OpenHands：平台 Secret API + LookupSecret + Runtime 注入

### 6.1 管理与存储

OpenHands 提供 Settings → Secrets 页面，也提供 Secret API：

- 用户保存具名 Secret，列表接口只返回名称和说明，不返回值。
- 密钥静态保存时使用 `OH_SECRET_KEY` 保护。
- 修改已有 Secret 时不能再次读取旧值；需要删除并重新创建或通过支持的更新 API 轮换。
- Secrets 与 LLM Provider 配置是相关但不同的设置面。

### 6.2 Runtime 解析

OpenHands SDK 有 `LookupSecret` 和 `SecretRegistry`：

- 对可分析的 Bash 命令，Registry 可以扫描命令引用，只把该命令需要的 Secret 解析为环境变量。
- 对无法逐条扫描的透明度较低的子进程/ACP 场景，需要在启动时解析更大范围的 Registry 并注入；SDK 源码也指出整体 Registry 注入存在最小权限范围仍需加强的问题。
- 因此，不能将“OpenHands 所有凭据始终只按需注入”概括为全路径保证。

### 6.3 安全边界

凭据进入 Runtime 环境后，终端命令本身可能读取并输出环境变量。加密静态存储也依赖 `OH_SECRET_KEY` 的独立保护。不同 Backend 的设置/Secret 是隔离的，不应假设一个 Backend 下保存的 Secret 自动适用于其他 Backend。

**值得借鉴：** 平台集中管理 UI、值只写不读、Secret API、延迟解析、执行命令时按引用注入。

## 7. 共性、差异与可借鉴结论

### 7.1 共性

1. 配置和机密分离，常规配置只保存名字/引用。
2. 管理界面不提供“再次查看完整机密”的入口。
3. 运行时按需要提供凭据，避免进入 Prompt、Session、Memory 和普通日志。
4. 环境变量是常见兼容层，但不能替代权限隔离。
5. 外部 Secret Manager 对多机器、团队共享、轮换更有价值。
6. 必须显式处理无头服务、容器、子进程、MCP/ACP 等运行环境差异。

### 7.2 差异

- OpenClaw 最完整地把引用、解析、运行时快照、出站注入和审计串成一条链路。
- Hermes 的优势是多来源组合、Profile 子进程边界，以及针对浏览器登录的独立加密 Vault。
- Goose 的方案最轻：OS Keyring 优先，Keyring 不可用时回退文件；部署形态决定实际保护等级。
- OpenHands 的优势是平台级 UI/API 和 Runtime 引用；其不同执行路径的注入范围和隔离等级需要逐条核对。

### 7.3 对铸己匣的选择

| 借鉴对象 | 采纳内容 | 不直接照搬的部分 |
|---|---|---|
| OpenClaw | SecretRef、Broker、受保护录入、出站注入、审计/迁移/刷新流程 | 不把本地 SQLite + 0600 文件权限当作最高安全等级；不把 Sentinel 当作进程沙箱 |
| Hermes | 外部 Secret Source 插件、子进程变量白名单、浏览器 Origin 绑定填充 | 普通 `.env` 不能当通用加密库；浏览器 Vault 不代替通用凭据授权 |
| Goose | OS Keyring 优先、明确的不可用退化与告警 | 不允许生产无声退化到明文文件 |
| OpenHands | 平台统一管理、只写不读、引用和按需注入 | 不向不透明子进程无差别注入整个 Secret Registry |

## 8. 参考资料

以下为官方文档或官方仓库源码。调研基准日：2026-11-22。

### OpenClaw

- [Secrets Management](https://docs.openclaw.ai/gateway/secrets)
- [Secrets Runtime Model](https://docs.openclaw.ai/gateway/secrets/runtime-model)
- [Secrets CLI](https://docs.openclaw.ai/cli/secrets)
- [Secrets Tool（受保护凭据录入）](https://docs.openclaw.ai/tools/secrets)

### Hermes Agent

- [Secrets / Secret Sources](https://hermes-agent.nousresearch.com/docs/user-guide/secrets)
- [Passwords & Logins / Credential Vault](https://hermes-agent.nousresearch.com/docs/user-guide/features/credential-vault)

### Goose

- [Configuration Files / Security Considerations](https://goose-docs.ai/docs/guides/config-files/)
- [Known Issues / Keyring Fallback](https://goose-docs.ai/docs/troubleshooting/known-issues/)

### OpenHands

- [Secrets Management](https://docs.all-hands.dev/openhands/usage/settings/secrets-settings)
- [Settings and Secrets API Example](https://github.com/OpenHands/software-agent-sdk/blob/main/examples/02_remote_agent_server/12_settings_and_secrets_api.py)
- [SecretRegistry SDK Implementation](https://github.com/OpenHands/software-agent-sdk/blob/main/openhands-sdk/openhands/sdk/conversation/secret_registry.py)

