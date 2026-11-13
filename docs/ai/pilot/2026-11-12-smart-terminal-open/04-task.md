---
task_id: 2026-11-12-smart-terminal-open
feature: smart-terminal
risk: { level: L2, triggers: ["ForgeSelf.Api/Program.cs", "Plugins/AIAgent/Services/AIAgentService.cs"], raised_by_agent: true }
gate1: { mode: user-direct, at: 2026-11-12, ref: "输入5 完成开发并授权方案" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Abstractions/TerminalOpenInbox.cs
  - ForgeSelf.Api/Services/Terminal/TerminalOpenParser.cs
  - ForgeSelf.Api/Services/Terminal/TerminalLaunchBootstrap.cs
  - ForgeSelf.Api/Program.cs
  - Plugins/SmartTerminal/Controllers/SmartTerminalController.cs
  - Plugins/SmartTerminal/web/src/SmartTerminalView.vue
  - ForgeSelf.Web/src/composables/useTerminalInbox.ts
rollback: "git checkout -- 上述文件；新文件移入 .trash 后由用户决定是否删除"
writeback: { feature_doc: Plugins/SmartTerminal/docs/03-架构与接口.md, reason: "补上 §6.1 打开标签契约" }
---

# Agent Task

## Objective

用同一套不含密码的 query，让页面、协议和已运行实例都能打开智能终端标签，并让 `st_*` 工具真正可被助手调用。

## Scope

### Allowed

03 列出的文件，加上对应测试与 `Plugins/SmartTerminal/docs` 里 URL / 工具两处口径。

### Forbidden

不改 RDP/SFTP，不改用户 `:51888`，不轮换 API 密钥，不提交 git，不把无关 LLM 代理改动混进本任务。

## Acceptance Criteria

- [x] AC1 页面深链开标签并跑 step
- [x] AC2 第二个进程转交，已运行页面新开标签
- [x] AC3 带密码的 inbound 返回 400
- [x] AC4 工具走 CreateHostScope，作用域含 smart-terminal
- [x] AC5 wsl-ssh 拒绝

## Expected Files

见头部 `expected_files`。

## Verification Commands

`dotnet test --filter TerminalOpenParserTests|AIAgentToolScopeTests`

`pnpm test`（插件 web）与宿主 `vue-tsc --noEmit`
