# 03 · URL 协议（深链启动）

页面路由：`/smart-terminal`（宿主菜单「智能终端」）。

> 密码深链为**内网过渡**；须知情确认。推荐 `auth=key`。策略见 [05](./05-已知风险-密码入URL.md) / [02](./02-交互说明.md)。

## 1. 触发条件

满足任一即可解析为一次启动：

- `st=1` 或 `st_launch=1` 或 `launch=1|true|yes`
- 或 `type` / `st_type` ∈ `local|ssh|wsl-ssh|shell`

解析成功后会从地址栏移除：`password` / `pwd` / … 以及 `st` / `st_launch`（防刷新重复启动）。  
**非敏感参数**（host、user、shell、cwd、cmd）可保留便于分享。

## 2. 公共参数

| 参数 | 别名 | 说明 |
|------|------|------|
| `st_type` | `type` | `local`（默认）/ `ssh` / `wsl-ssh` |
| `st_shell` | `shell` | 壳 Id：`bash` / `zsh` / `sh` / `pwsh` / … |
| `st_cwd` | `cwd` | 工作目录 |
| `st_title` | `title` | 标签标题 |
| `cmd` | `st_cmd` | 可重复；初始命令 |
| `cmds` | `st_cmds` | 逗号分隔命令列表 |

## 3. 类型 A · local

打开本地壳标签，并按序执行命令。

```text
/smart-terminal?st=1&type=local&shell=bash&cwd=/workspace&title=Build&cmd=echo%20hi&cmd=ls
```

等价：

```text
...&cmds=echo%20hi,ls
```

## 4. 类型 B · ssh（Linux/macOS 原生；Windows 有密码时自动升为 wsl-ssh）

```text
/smart-terminal?st=1&type=ssh&host=10.0.0.1&port=22&user=root&auth=sshpass&password=SECRET&cmd=uname%20-a
```

| 参数 | 别名 | 说明 |
|------|------|------|
| `st_host` | `host` | 必填 |
| `st_port` | `port` | 默认 22 |
| `st_user` | `user` | 必填 |
| `st_auth` | `auth` | `sshpass` / `key` / `interactive` |
| `st_password` | `password`/`pwd` | **仅内存**；见文档 05 |
| `st_identityFile` | `identityFile`/`key` | `auth=key` 时私钥路径 |

实现要点：

- `auth=sshpass`：命令为 `sshpass -e ssh ...`，密码进进程环境 `SSHPASS`，**不进命令字符串/JSONL**  
- Windows 上若带密码，服务端将 `type` 提升为 `wsl-ssh`

## 5. 类型 C · wsl-ssh（Windows 明确走 WSL）

```text
/smart-terminal?st=1&type=wsl-ssh&host=10.0.0.1&user=ops&auth=sshpass&password=SECRET
```

实际引导近似：

```text
wsl.exe -e sshpass -e ssh -o StrictHostKeyChecking=accept-new -p 22 ops@10.0.0.1
```

并通过 `WSLENV=SSHPASS/w` 把环境变量传入 WSL。  
要求：Windows 已安装 WSL，且发行版内有 `sshpass` 与 `ssh`。

## 6. API 对等

`POST /api/smart-terminal/launch`  
Body 字段与上表同名（camelCase）：`type/shellId/cwd/title/commands/host/port/user/password/auth/identityFile`。

响应含 `passwordProvided`、`warning`（有密码时给出产品警告文案），**永不回显密码**。

## 7. 安全相关行为（摘要）

详见 [05-已知风险-密码入URL.md](./05-已知风险-密码入URL.md)。


## 8. 密码策略参数

| 参数 | 说明 |
|------|------|
| `st_url_password_mode` | 当次覆盖：`always-ack`（默认）/ `intranet` / `disabled` |

含密码时前端会先弹「我已知晓风险」；`disabled` 下丢弃密码并提示改用密钥。
