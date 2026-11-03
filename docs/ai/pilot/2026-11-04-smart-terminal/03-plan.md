# Plan

## 决策

- **D1** 持久化用 JSONL 审计 + 内存会话（对齐 ToolBridge，免 XCode/PluginDbs）
- **D2** 权限分类器可配置 JSON，默认 AskWrite
- **D3** 实时输出用轮询（对齐 ScriptHub），避免宿主 MapHub 接线
- **D4** 与 B4 PermissionProfile 互补：本插件管命令级，宿主管工具级

## 文件

- `Plugins/SmartTerminal/**`
- `ForgeSelf.slnx`、`ForgeSelf.Api/ForgeSelf.Api.csproj`、`ForgeSelf.Api.Tests/**`
- `ForgeSelf.Web/e2e/plugins/smart-terminal/smart-terminal.spec.ts`
