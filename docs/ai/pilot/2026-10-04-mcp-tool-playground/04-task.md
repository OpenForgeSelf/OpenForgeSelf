# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备，**待闸门1 确认**。
> Task ID：2026-10-04-mcp-tool-playground

## Task ID

2026-10-04-mcp-tool-playground（工件目录 `docs/ai/pilot/2026-10-04-mcp-tool-playground/`）

## Objective

MCP 中心插件新增「工具测试台」：后端有 `POST api/mcp-center/servers/{id}/tools/invoke`（继承类级 `ApiKeyPolicy` 鉴权），前端「外部服务器工具」弹窗能按 `inputSchema` 动态渲染参数表单、发起调用并展示耗时/文本/原始 JSON；以 DeepWiki MCP 完成一次真实端到端调用。

## Scope

### Allowed

严格限于 03-plan「Files To Change」名单：

1. `Plugins/McpCenter/Models/McpToolInvoke.cs`（新增）
2. `Plugins/McpCenter/Models/McpExternalServerConfig.cs`（仅加 `McpExternalToolDto.ServerId`）
3. `Plugins/McpCenter/Services/McpClient/McpClientSession.cs`（抽 `ExtractOutcome` + 新增 `CallToolDetailedAsync`）
4. `Plugins/McpCenter/Services/McpClientManager.cs`（新增 `InvokeToolAsync`）
5. `Plugins/McpCenter/Controllers/McpExternalController.cs`（新增 `POST {id}/tools/invoke`）
6. `Plugins/McpCenter/plugin.json`（版本 2.3.0）
7. `Plugins/McpCenter/web/src/types/external.ts`、`api/external.ts`、`McpCenterView.vue`、`package.json`
8. `Plugins/McpCenter/web/src/playground/`（新增 schemaForm.ts / schemaForm.test.ts / ToolPlayground.vue）
9. `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpToolInvokeTests.cs`（新增）
10. `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`（仅在断言了旧弹窗 DOM 时同步更新）
11. `docs/02-features/034-mcp-center.md`
12. 本工件目录 05/06/07

### Forbidden

- ❌ 修改生产环境 / `publish/` / 停起宿主进程。
- ❌ 新增或修改 XCode 实体、`Data/Model.xml`、建表逻辑、数据库结构（本任务无持久化需求）。
- ❌ 修改鉴权策略本身、去掉或降级 `[Authorize("ApiKeyPolicy")]`。
- ❌ 新增第三方依赖（NuGet / npm 均可）。
- ❌ 改动 `CallToolAsync` 的**返回值语义**（4 条既有用例必须原样通过）。
- ❌ 改 `StreamableHttpMcpTransport` / `StdioMcpTransport` / `LegacySseMcpTransport`（`Mcp-Session-Id` 支持另立 TODO，不在本批）。
- ❌ 无关重构：`McpCenterView.vue` 只做两处最小接线，不顺手整理其余 1600 行。
- ❌ 做未要求的能力：调用历史持久化、批量调用、resources/prompts 浏览。
- ❌ 写一次性 `temp/*.cjs` 或散落脚本当验证手段（AGENTS.md §0 红线）。
- ❌ 未经闸门2/3 授权提交代码、打 tag、发布。

## Acceptance Criteria

- [ ] **AC1** `POST api/mcp-center/servers/{id}/tools/invoke` 可用，返回 `{ serverId, tool, ok, isError, text, rawJson, elapsedMs }`，`elapsedMs > 0`。
- [ ] **AC2** 无令牌访问该端点 401；`McpAdminAuthTests` 全绿。
- [ ] **AC3** 未连接 / 工具不存在 / 参数非 JSON 三类错误各有明确消息 + HTTP 400，无未处理异常。
- [ ] **AC4** `McpClientSessionTests` 既有 4 条 `CallTool_*` 用例原样通过；新增 `CallToolDetailedAsync` 覆盖 text / isError / 结构化 content / 无 content。
- [ ] **AC5** `schemaForm.test.ts` 覆盖 string/number/integer/boolean/enum/array/object、`required`、`default`、`anyOf`（DeepWiki 真实 `repoName` schema）、空 schema、非法 JSON。
- [ ] **AC6** 弹窗内按 schema 渲染表单，必填有标记、enum 下拉、default 预填。
- [ ] **AC7** 真实调用 DeepWiki `read_wiki_structure{repoName:"facebook/react"}` 成功，界面展示结果与耗时（截图留证）。
- [ ] **AC8** 「填入 DeepWiki 预设」按钮可用，建服务器后连接成功、工具数 ≥ 3。
- [ ] **AC9** `plugin.json` Version = `2.3.0`，界面徽标同步。
- [ ] **AC10** 下列验证命令全部**实际执行**并通过。
- [ ] **AC11** `git status` 改动文件 ⊆ Allowed 名单。

## Expected Files

```
Plugins/McpCenter/Models/McpToolInvoke.cs                     (new)
Plugins/McpCenter/Models/McpExternalServerConfig.cs           (edit: +ServerId)
Plugins/McpCenter/Services/McpClient/McpClientSession.cs      (edit: +CallToolDetailedAsync)
Plugins/McpCenter/Services/McpClientManager.cs                (edit: +InvokeToolAsync)
Plugins/McpCenter/Controllers/McpExternalController.cs        (edit: +invoke action)
Plugins/McpCenter/plugin.json                                 (edit: 2.3.0)
Plugins/McpCenter/web/src/types/external.ts                   (edit)
Plugins/McpCenter/web/src/api/external.ts                     (edit)
Plugins/McpCenter/web/src/McpCenterView.vue                   (edit: 2 处接线)
Plugins/McpCenter/web/src/playground/schemaForm.ts            (new)
Plugins/McpCenter/web/src/playground/schemaForm.test.ts       (new)
Plugins/McpCenter/web/src/playground/ToolPlayground.vue       (new)
Plugins/McpCenter/web/package.json                            (edit: +check/test)
ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpToolInvokeTests.cs (new)
ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts       (cond. edit)
docs/02-features/034-mcp-center.md                            (edit)
docs/ai/pilot/2026-10-04-mcp-tool-playground/05-evidence.md   (new)
docs/ai/pilot/2026-10-04-mcp-tool-playground/06-review.md     (new)
docs/ai/pilot/2026-10-04-mcp-tool-playground/07-final-report.md (new)
```

## Verification Commands

```bash
# 1. 后端编译
cd ForgeSelf.Api && dotnet build

# 2. 插件后端单测（含新增 McpToolInvokeTests + 既有 McpClientSessionTests/McpAdminAuthTests）
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter"

# 3. 插件前端纯函数单测（AC5）
cd Plugins/McpCenter/web && pnpm run test

# 4. 插件前端类型检查 + 构建
cd Plugins/McpCenter/web && pnpm run check && pnpm run build

# 5. 宿主前端门禁
cd ForgeSelf.Web && pnpm run check && pnpm run test

# 6. 插件层 e2e（隔离实例）
cd ForgeSelf.Web && bash node_modules/.bin/playwright test e2e/plugins/mcp-center/

# 7. 工件链门禁（05/06/07 产出后）
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-04-mcp-tool-playground
```

## 闸门1 待用户批准的决策点

| # | 决策 | 说明 |
| --- | --- | --- |
| G1 | 工件范围是否照此执行（00–04 已出） | 批准后方可 Implement |
| G2 | 新端点落在**既有** `McpExternalController` 内（继承鉴权）而非新建控制器 | 推荐；新建控制器需同步 `McpAdminAuthTests` |
| G3 | 前端新增 `playground/` 子组件，而非把 1698 行的 `McpCenterView.vue` 继续撑大 | 推荐 |
| G4 | 给插件 `web/package.json` 补 `check`/`test`（照抄 DesignSystem），从而让 schemaForm 纯函数有单测入口 | 推荐；不补则 AC5 只能靠 e2e 兜 |
| G5 | e2e 环境若无外网，DeepWiki 真实调用改由人工走查确认（并在 Evidence 标注 `Unknown`） | 保守假设 |
| G6 | 是否同步更新 `e2e/plugins/mcp-center/mcp-center.spec.ts`（仅在断言旧弹窗 DOM 时） | 依实施前实读结果定 |
