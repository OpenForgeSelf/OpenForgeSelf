> 产品经理请先读 [**00-目录.md**](./00-目录.md)（需求 / 交互 / URL / 权限 / 密码风险）。

# 智能终端（SmartTerminal）

可审批、可审计的**命令会话**插件（非完整交互式 PTY）。默认权限模式 **AskWrite**。

## 行业对齐

| 能力 | 对标 |
|------|------|
| 分级自动 / 询问 | [VS Code Agent Approvals](https://code.visualstudio.com/docs/copilot/agents) 的 autoApprove / 审批分层思路 |
| 审批与执行分离 | [Open Interpreter](https://docs.openinterpreter.com/) sandbox 与 approval policy 分离 |
| 破坏性命令字 / 管道到壳 | 本仓 `mcp-center` `TerminalCommandGuard` + ToolBridge `CommandGuard` |
| JSONL 只追加审计 | CC Safety Net / AI Shield 一类「一行一事、可过滤回放」审计日志 |

> 与宿主 `PermissionProfile`（工具级 allow/ask/deny）互补：本插件管**终端会话内命令分类**；全局工具策略仍走 B4 权限配置。

## 权限三级

1. **Auto**：除硬 denylist 外自动执行  
2. **AskWrite**（默认）：只读自动；写/副作用/高危询问；硬拒绝仍拦  
3. **AskHighRisk**：多数自动；仅高危询问；硬拒绝仍拦  

规则文件：`{插件数据目录}/permission-rules.json`（首次从 `Data/permission-rules.default.json` 复制，可热改后调 `ReloadRules` / 重启插件）。

## Agent 工具（toolPrefix=`st`）

- `st_list_shells` / `st_create_session` / `st_run_command` / `st_get_output`
- `st_list_sessions` / `st_set_permission_mode` / `st_query_audit` / `st_resolve_approval`

## HTTP

前缀 `/api/smart-terminal/*`，类级 `ApiKeyPolicy`。

## 本地启用

1. 构建插件：`dotnet build Plugins/SmartTerminal/SmartTerminal.csproj`
2. 构建前端：`cd Plugins/SmartTerminal/web && pnpm install && pnpm build`
3. 宿主全量构建会经 `StageAllPlugins` 暂存到输出 `Plugins/SmartTerminal/`
4. 插件管理页启用 **智能终端**，打开 `/smart-terminal`

Windows / Linux 均为一等公民。v1.2 起支持多标签与 URL 深链（local / ssh / wsl-ssh）。

## 外壳支持（v1.1）

- **Linux/macOS 一等**：`bash` / `zsh` / `sh`（及探测到的 `fish` / `dash`），默认按 `$SHELL` → bash → zsh → sh。
- **Windows 一等**：`cmd` / Windows PowerShell / `pwsh` + Windows Terminal profiles；Git Bash 若在 PATH 也会出现。
- 跨平台列表会保留对方平台壳并标记 `available=false`，便于 UI 多壳选择器对照。

## CT 画廊

```bash
cd ForgeSelf.Web
pnpm run ct:server   # http://127.0.0.1:5190/ 侧栏 SmartTerminal
```

深链示例：`/?story=SmartTerminal/PendingApproval`


## URL 深链（摘要）

详见 [03-URL协议.md](./03-URL协议.md)。示例：

```text
/smart-terminal?st=1&type=local&shell=bash&cmd=echo%20hi
/smart-terminal?st=1&type=ssh&host=10.0.0.1&user=root&auth=sshpass&password=...
```

`POST /api/smart-terminal/launch` 与上表字段对等。密码风险见 [05-已知风险-密码入URL.md](./05-已知风险-密码入URL.md)。

## 密码深链策略（前端）

见 [05-已知风险-密码入URL.md](./05-已知风险-密码入URL.md)。默认 `always-ack`；内网可设 `intranet`；公网演示可设 `disabled`。
