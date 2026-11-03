# 智能终端（SmartTerminal）

会话化、可审批、可审计的本机终端插件。默认权限模式 **AskWrite**。

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

Windows 多壳（cmd / Windows PowerShell / pwsh + WT profiles）在 Windows 宿主上可用；Linux/macOS 构建与加载不炸，降级为 bash/sh/pwsh。
