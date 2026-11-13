# 00 · Repository Understanding

- 仓库：OpenForgeSelf。宿主 `ForgeSelf.Api`（.NET 10）+ 前端 `ForgeSelf.Web`（Vue 3 / pnpm）+ 插件 `Plugins/SmartTerminal`。
- 2026-11-12 将 `github-origin/main` 快进到 `8f07e8f`（智能终端 v2，17 个提交）。本地未提交的 LLM 代理改动与这 17 个提交无文件重叠，保留。
- v2 已有：提示符会话、`POST /api/smart-terminal/sessions`、右侧助手 `SendChatAsync`、`launchFromQuery.ts`、`st_*` 工具。
- 缺口：页面 `loadLive` 先擦掉深链、从不调用解析器；工具执行期用 `CreateScope()`（插件上下文没有 `IServiceScopeFactory`）；宿主互斥量被占用时直接退出，不转交参数；没有 `forgeself:` 登记。
- 适用问题类：插件工具 DI 与 PILOT-062 / `PluginContextDiExtensions` 同类。本次只改执行期调用，不新开问题类。
