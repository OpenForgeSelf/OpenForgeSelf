# 00 · Repository Understanding

## 与本任务相关的仓库事实（读码/清单实测）

- 插件 20 个（`Plugins/` 目录 + plugin.json 实测）：agent-hub 1.0.11 / ai-agent 1.9.6 / cost-scope 0.1.0 / design-system 3.1.0 / dev-tools 1.0.0 / file-tools 1.1.2 / home 1.0.0 / im-gateway 2.0.1 / mcp-center 2.7.0 / memory-system 1.0.0 / quick-links 1.0.1 / sample 1.0.0 / scheduler 1.0.1 / script-runner 1.0.1 / sems 1.2.0 / system-monitor 1.0.0 / text-tools 1.0.0 / todo-tracker 1.2.0 / tool-bridge 1.0.4 / workflow-engine 1.0.1。
- 工具注册 127 个、17 插件贡献（`docs/02-features/021-ai-agent.md:206-227` 唯一真源清单）；ADR-004 已立命名空间规则（前缀 + 11 保留名 + mcp-center 整插件豁免）。
- 插件间互通：契约入 `ForgeSelf.Abstractions`、消费走 `ctx.Get<T>()`（architecture-design 技能铁律 2/4；调研 `docs/06-research/001` §5.6/§6 裁决 A-F）。
- 能力供给三层模型：L1 契约 / L2 事件 / L3 拦截（`docs/01-architecture/host-capability-seams.md`）。
- 历史反例（用户点名）：AIAgent 曾实现文件三件套 + 万能工具 + 工作流三工具（均已迁出，ADR-004 / pilot 2026-10-28-tool-namespace-ownership）。
- 既有标准落点惯例：`docs/04-standards/`（packaging-upgrade-backup / frontend-component-development / ai-native-engineering-workflow 等并列），新标准须登记 `docs/README.md`。

## 技术约束（本标准必须兼容的既有裁决）

- ADR-004：工具名 = `<toolPrefix>_<语义名>`；mcp-center 整插件豁免（2026-10-28 17:29 用户拍板）。
- not-to-do 台账（`docs/07-decisions/not-taken-decisions.md` 035/044/045 等）已定的不做项不得被本标准推翻。
- 禁止隐式耦合（共享文件/静态类/直连 HTTP 当契约）；持久化是 Provider 内部细节。
