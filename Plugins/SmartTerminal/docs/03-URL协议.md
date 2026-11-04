# 03 · URL 协议（通用启动器）

> **设计原则**：深链是一次「打开标签 + 可选执行步骤」的**通用启动**，用 `profile` 区分终端形态；SSH / WSL-SSH / 本地壳都是 profile，而不是为 SSH 单独发明另一套参数模型。  
> 保留既有 PM 结论：基础多标签、AskWrite 默认、URL 明文密码仅内网过渡 + 知情确认、不宣传完整 PTY。

页面路由：`/smart-terminal`。

---

## 1. 一次启动 = LaunchSpec

逻辑模型（URL 与 `POST /api/smart-terminal/launch` 共用）：

```jsonc
{
  "profile": "local",          // 见 §3；亦可 custom:<id>
  "shell": "bash",             // 可选；本地壳 Id
  "cwd": "/workspace",         // 可选
  "title": "Build",            // 可选标签名
  "env": { "FOO": "bar" },     // 可选；仅注入本会话进程环境（敏感键仍脱敏审计）
  "steps": [                   // 可选；按序执行
    { "command": "echo", "args": ["hi"] },
    { "command": "ls -la" }    // 也可整行写入 command，args 省略
  ],
  "auth": {                    // 仅当 profile 需要认证时
    "method": "interactive",   // interactive | key | sshpass
    "password": null,          // 禁止持久化；URL 带入须 ack；缺省则 UI 交互输入
    "identityFile": null,
    "host": "10.0.0.1",
    "port": 22,
    "user": "root"
  }
}
```

---

## 2. 触发与脱敏

满足任一即解析为启动：

| 条件 | 说明 |
|------|------|
| `st=1` / `st_launch=1` / `launch=1\|true\|yes` | 显式触发 |
| 或存在 `profile` / `st_profile` | 通用主键 |
| 或遗留 `type` / `st_type` ∈ local\|ssh\|wsl-ssh\|shell | **兼容旧链**，等价映射到 `profile` |

解析后立刻从地址栏 **scrub**：`password` / `pwd` / … 以及一次性触发键 `st` / `st_launch`。  
非敏感参数（`profile`、`host`、`shell`、`cwd`、`step`）可保留便于分享。

---

## 3. 公共 query 参数

| 参数 | 别名 | 说明 |
|------|------|------|
| `profile` | `st_profile`、遗留 `type` | 终端配置：`local` / `ssh` / `wsl-ssh` / `custom:<id>` |
| `shell` | `st_shell` | 壳 Id（local 为主；ssh 引导壳可选） |
| `cwd` | `st_cwd` | 工作目录 |
| `title` | `st_title` | 标签标题 |
| `env.NAME` | `env`（`K:V,K2:V2`） | 会话环境变量；可重复 `env.FOO=bar` |
| `step` | `st_step`、遗留 `cmd` | **可重复**；每条 = 一整行命令（已含参数） |
| `run` | `st_run` | URL 编码的 JSON 步骤数组（见 §4）；与 `step` 二选一优先 `run` |
| `st_url_password_mode` | | `always-ack` / `intranet` / `disabled`（仅当 URL 含密码） |

遗留兼容：`cmds=a,b` 逗号分隔 → 多条 `step`。

---

## 4. 步骤（commands + args）两种写法

### 4.1 推荐日常：`step` 重复（整行命令）

```text
/smart-terminal?st=1&profile=local&shell=bash&cwd=/workspace&title=Build&step=echo%20hi&step=ls%20-la
```

### 4.2 结构化：`run` JSON（命令与参数分离）

```text
/smart-terminal?st=1&profile=local&shell=bash&run=%5B%7B%22command%22%3A%22git%22%2C%22args%22%3A%5B%22-C%22%2C%22%2Frepo%22%2C%22status%22%5D%7D%2C%7B%22command%22%3A%22pnpm%22%2C%22args%22%3A%5B%22test%22%5D%7D%5D
```

解码后：

```json
[
  { "command": "git", "args": ["-C", "/repo", "status"] },
  { "command": "pnpm", "args": ["test"] }
]
```

拼进壳时：`command` + 空格 + 安全拼接的 `args`（实现期做引号规则；审计存脱敏后的整行）。

---

## 5. Profile 目录（可扩展）

| profile | 含义 | 认证 |
|---------|------|------|
| `local` | 本机壳（bash/zsh/sh/pwsh/…） | 无 |
| `ssh` | 远程 SSH（Linux/macOS 原生 ssh；Windows 无密码时亦可用 OpenSSH） | `interactive`（默认）/ `key` / `sshpass` |
| `wsl-ssh` | Windows 经 WSL 的 ssh/sshpass | 同上 |
| `custom:<id>` | 插件或宿主注册的命名配置（**协议位已预留；本里程碑不实现**；实现期查表合并默认 shell/cwd/env/steps） | 由注册表声明 |

> 新增场景优先 **加 profile 或 custom 配置**，而不是再发明平行 query 方言。  
> **本里程碑**：仅实现 `local` / `ssh` / `wsl-ssh`。遇到 `custom:<id>` 应返回可读错误（不静默降级为 local）；UI 不提供 custom 选择器。

### 5.1 local 示例

```text
/smart-terminal?st=1&profile=local&shell=zsh&cwd=/tmp&title=Tmp&env.LANG=C&step=pwd&step=date
```

### 5.2 ssh · 无密码 → **交互式输入**（推荐默认）

```text
/smart-terminal?st=1&profile=ssh&host=10.0.0.1&port=22&user=root&step=uname%20-a
```

等价显式：`&auth=interactive`。  
行为（定稿）：**先**弹出遮罩密码框（见 02 / 06）→ 用户确认后**再**创建标签并连接 → 内存注入 → 执行 `step`。取消则不建标签。  
密码**不得**写入 URL 历史回写、JSONL、命令历史、sessionStorage / localStorage 原文。

### 5.3 ssh · 密钥

```text
/smart-terminal?st=1&profile=ssh&host=10.0.0.1&user=ubuntu&auth=key&identityFile=~%2F.ssh%2Fid_ed25519&step=hostname
```

### 5.4 ssh · URL 带密码（内网过渡，非推荐）

```text
/smart-terminal?st=1&profile=ssh&host=10.0.0.1&user=root&auth=sshpass&password=SECRET&st_url_password_mode=intranet
```

须走「我已知晓风险」确认（05）；Windows 可升为 `wsl-ssh`。

### 5.5 wsl-ssh

```text
/smart-terminal?st=1&profile=wsl-ssh&host=10.0.0.1&user=ops&auth=interactive
```

---

## 6. 认证字段（挂在需要认证的 profile 上）

| 参数 | 别名 | 说明 |
|------|------|------|
| `host` | `st_host` | 必填（ssh / wsl-ssh） |
| `port` | `st_port` | 默认 22 |
| `user` | `st_user` | 必填 |
| `auth` | `st_auth` | `interactive`（缺省）/ `key` / `sshpass` |
| `password` | `pwd` 等 | **不推荐**；见 05 |
| `identityFile` | `identity` / `key` 路径 | `auth=key` |

决策树：

1. `auth=key` → 不弹密码框  
2. URL 含 password → scrub → 风险 ack → sshpass  
3. 否则 → **`auth=interactive`**（含未写 auth）→ UI 遮罩输入  

---

## 7. API 对等

`POST /api/smart-terminal/launch`

```jsonc
{
  "profile": "ssh",          // 亦接受遗留字段 type
  "shellId": "bash",
  "cwd": "/workspace",
  "title": "SSH · root@host",
  "env": { "LANG": "C" },
  "steps": [
    { "command": "uname", "args": ["-a"] }
  ],
  // 扁平兼容：commands: ["uname -a"]
  "host": "10.0.0.1",
  "port": 22,
  "user": "root",
  "auth": "interactive",
  "password": null,
  "identityFile": null
}
```

响应：`session`、`commandResults`、`launchType`/`profile`、`passwordProvided`、`warning`、`needsInteractivePassword`（可选提示）；**永不回显密码**。

---

## 8. 安全摘要

| 能力 | 规则 |
|------|------|
| URL 明文密码 | 仅内网过渡 + ack；默认策略 `always-ack` |
| 交互密码 | 遮罩输入；**先框后标签**；内存；关标签即焚；不进 URL/JSONL/历史/浏览器存储原文 |
| env 疑似密钥 | 审计脱敏（键名或值形态像密钥 → `***`） |
| custom profile | 协议位保留；**本里程碑不实现** |
| 推荐 | `auth=key` 或后续 vault |

详见 [05-已知风险-密码入URL.md](./05-已知风险-密码入URL.md)。
