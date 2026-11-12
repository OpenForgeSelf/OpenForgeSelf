> 产品经理请先读 [**00-目录.md**](./00-目录.md)。

# 智能终端（Smart Terminal）v2

宿主插件里的 **可审批、可审计的命令会话**。Id `smart-terminal` · 建议版本 **2.0.0** · toolPrefix `st` · ConnName `SmartTerminal` · EntryType 沿用 `ForgeSelf.Api.Plugins.SmartTerminal.SmartTerminalPlugin`（实现落点是宿主仓 `Plugins/SmartTerminal`）。

对外一句话：**在宿主里打开本机或 SSH 命令会话——提示符里回车执行，写操作先确认，右侧是复用宿主模型的助手对话。**

## 产品口径

以本目录 **01–06（已锁定 / 原型 v0.2 · 2026-11-12）** 为准。

本套 **v2 文档**是当前真源。旧 UX（独立命令框 +「运行」按钮）已废弃，旧文档只作领域对照，**不得**再当验收真源。

## HTTP

前缀 `/api/smart-terminal/*`，类级 `ApiKeyPolicy`，详见 [03](./03-架构与接口.md)。

原型里曾写 `/api/plugins/smart-terminal/*`，只是示意，**实现必须以 `/api/smart-terminal` 为准**（与 `super-lab` / `db-admin` 等兄弟插件一致）。

## 交互真源

确认原型：[智能终端原型 v0.2](./smart-terminal-prototype.html)。变更说明：[CHANGELOG-v0.2.md](./CHANGELOG-v0.2.md)。竞品底稿：[competitor-brief.md](./competitor-brief.md)。

## 工程约束

- 前端：Vue 3 + 原生 HTML/CSS，**禁止**模板使用 Element Plus 组件；token 与三禁见 02 / 06。
- 复用宿主能力：凭据走宿主凭据库 / `ISecretEncryptionService`；助手走宿主 AI 网关（`ILlmRuntime` / `IChatCompletion` / `IChatModelResolver`）。取宿主服务一律执行期 `CreateHostScope()` / `ctx.Get<T>()`。
- **不要**把密码、私钥明文写入插件库、URL、日志或浏览器存储。

## 本地启用

1. 按 01 / 03 在宿主中启用插件 `smart-terminal`
2. `cd web && pnpm install && pnpm build`（`web/dist` 不入库，挂宿主前再构建）
3. 打开 `/smart-terminal`
