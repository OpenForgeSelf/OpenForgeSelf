# Evidence

> 阶段：Stage 7｜只记录**实际发生的事情**。
> 来源等级：**Verified**（亲自跑过，拿到真实输出）/ **Inferred**（凭代码推断）/ **Unknown**（未验证）——三者禁止混用。
> Task ID：2026-10-04-mcp-tool-playground｜工作树 `wt-mcp-playground`（分支 `feat/mcp-tool-playground`）

## Changed Files

| 文件 | 类型 | 说明 |
| --- | --- | --- |
| `Plugins/McpCenter/Models/McpToolInvoke.cs` | 新增 | `McpToolInvokeRequest` / `McpToolCallOutcome` / `McpToolInvokeResult` |
| `Plugins/McpCenter/Models/McpExternalServerConfig.cs` | 改 | `McpExternalToolDto` 增加 `ServerId` |
| `Plugins/McpCenter/Services/McpClient/McpClientSession.cs` | 改 | 抽 `Inspect()` 统一提取；新增 `CallToolDetailedAsync`；`CallToolAsync` 改为委托（语义不变） |
| `Plugins/McpCenter/Services/McpClientManager.cs` | 改 | 新增 `InvokeToolAsync`（前置校验 + 计时委托） |
| `Plugins/McpCenter/Controllers/McpExternalController.cs` | 改 | 新增 `POST {id}/tools/invoke`（继承类级 `[Authorize("ApiKeyPolicy")]`） |
| `Plugins/McpCenter/plugin.json` | 改 | Version `2.2.0 → 2.3.0` + Description 补测试台说明 |
| `Plugins/McpCenter/web/src/types/external.ts` | 改 | `inputSchema` → `inputSchemaJson`（对齐后端真名）；新增 invoke 请求/结果类型 |
| `Plugins/McpCenter/web/src/api/external.ts` | 改 | 新增 `invokeExternalTool` |
| `Plugins/McpCenter/web/src/McpCenterView.vue` | 改 | 工具弹窗换成 `<ToolPlayground>`（宽度 560→860，标题带服务器名）；新增 DeepWiki 预设按钮；修 3 处存量类型问题 |
| `Plugins/McpCenter/web/src/playground/schemaForm.ts` | 新增 | schema→表单纯函数 |
| `Plugins/McpCenter/web/src/playground/schemaForm.test.ts` | 新增 | 23 条单测 |
| `Plugins/McpCenter/web/src/playground/ToolPlayground.vue` | 新增 | 工具清单 + 动态参数表单 + JSON 模式 + 结果面板 |
| `Plugins/McpCenter/web/tsconfig.check.json` | 新增 | 类型检查配置（照抄 DesignSystem 插件） |
| `Plugins/McpCenter/web/package.json` | 改 | 补 `check` / `test` 脚本 |
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpToolInvokeTests.cs` | 新增 | 12 条后端单测（含真实 node mock 端到端调用） |
| `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts` | 改 | `startMock` 提到模块级；新增工具测试台用例 |
| `docs/02-features/034-mcp-center.md` | 改 | 补 invoke 端点契约 + 工具测试台用法 + schema 解析规则 |

**越界自审（AC11）**：`git status --short` 实测 11 改 + 5 新增，与 04-task Allowed 名单**逐项一致**。
过程中出现过的越界项已处理：① `ForgeSelf.Web/components.d.ts` 被工具链改写（仅行尾 CRLF 差异，无内容 diff）→ 已 `git checkout --` 还原；② 临时 `vite.e2e-tmp.config.mts`（环境规避用，无效）→ 已删除。

---

## Verification

### 1. 后端编译 — **Verified**

```bash
cd ForgeSelf.Api && dotnet build
```
> `已用时间 00:00:25.84` — **0 个错误**（1189 个警告，均为仓库既有警告，非本批引入）

### 2. 插件后端单测 — **Verified**

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpToolInvokeTests"
```
> `已通过! - 失败: 0，通过: 12，已跳过: 0，总计: 12，持续时间: 2 s`

覆盖：`CallToolDetailedAsync` 四种返回形态（text / isError / 结构化 content / 无 content）、`CallToolAsync` 既有契约回归、Manager 未连接与空工具名校验、控制器 4 类错误（空工具名 / 坏 JSON / 未连接 / 工具不在清单）、**成功路径走真实 node mock MCP 服务器（stdio）→ `add(3,4)` → `sum=7`**。

### 3. 既有 McpCenter 测试全量回归 — **Verified**

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter"
```
> `已通过! - 失败: 0，通过: 108，已跳过: 0，总计: 108，持续时间: 6 s`

即：新增 12 条 + 既有 96 条**全绿**（含 `McpClientSessionTests` 4 条 `CallTool_*` 契约断言与 `McpAdminAuthTests` 鉴权反射断言）⇒ `CallToolAsync` 重构**零回归**、鉴权未被削弱。

### 4. 插件前端纯函数单测 — **Verified**

```bash
cd Plugins/McpCenter/web && pnpm run test
```
> `Test Files 1 passed (1)` / `Tests 23 passed (23)` / `Duration 3.12s`

含 DeepWiki 真实 `ask_wiki_question.inputSchema` 作输入的 `anyOf` 用例。

### 5. 插件前端类型检查 — **Verified**

```bash
cd Plugins/McpCenter/web && pnpm run check
```
> 通过（无输出即 0 error）。
> 注：首次跑暴露 3 处**存量**问题（非本批引入）——`testMcpServer` 未使用 import、两处 `@update:model-value` 参数隐式 any——已最小修复（详见 03-plan 偏差记录）。未放宽任何编译选项。

### 6. 插件前端构建 — **Verified**

```bash
cd Plugins/McpCenter/web && pnpm run build
```
> `✓ 50 modules transformed` / `dist/style.css 117.89 kB` / `dist/index.js 63.58 kB` / `✓ built in 1.46s`

### 7. 宿主前端门禁 — **Verified**

```bash
cd ForgeSelf.Web && pnpm run check   # 0 errors, 81 warnings（与仓库既有基线一致）
cd ForgeSelf.Web && pnpm run test    # Test Files 66 passed / Tests 756 passed
```

### 8. 插件层 e2e — **部分 Verified / 部分 BLOCKED**

```bash
cd ForgeSelf.Web && bash node_modules/.bin/playwright test e2e/plugins/mcp-center/ --workers=1
```
> **4 条用例：1 passed / 1 failed / 2 did not run**（serial 模式，第 2 条失败后后续跳过）

- ✅ 用例 1「宿主加载 mcp-center + MCP 端口全链路 + 网关配置 API」**通过**，证据行含 `host plugin: mcp-center v2.3.0`（**Verified**：真实宿主实例已加载新版本插件）。
- ❌ 用例 2「插件自带界面 /mcp-center 渲染」失败：`page.goto: net::ERR_CONNECTION_REFUSED at http://localhost:7002/mcp-center`。
  **根因（Verified，已定位到具体命令与报错）**：vite dev server 在运行期触发依赖重新预构建时崩溃——
  `Failed to write to output file: open ...\node_modules\.vite\deps_temp_<hash>\vue-router.js: Access is denied`（14 项错误，进程退出）。
- ❌ 用例 3、4（含本批新增的工具测试台用例）**未执行**。

**根因复现与边界（Verified）**：

| 场景 | 结果 |
| --- | --- |
| esbuild 写入**自己创建**的 `deps_temp_*` 目录 | ✅ 成功 |
| esbuild 写入**任何进程预先创建**的 `deps_temp_*` 目录（node `mkdirSync` / bash `mkdir` 都一样） | ❌ Access is denied |
| esbuild 写入非 `deps_temp_*` 的同级目录（如 `ebtest`） | ✅ 成功 |

⇒ vite 固定「先建 `deps_temp_` 目录、再让 esbuild 写入」，在本环境必然失败 ⇒ **任何需要重新预构建的 dev server 都会崩**。
已尝试的规避及结果：① 复用主工作树已构建的 `.vite/deps` 缓存 → 首屏能起（curl 200），一旦运行期发现新依赖仍崩；② `optimizeDeps.noDiscovery` → 不崩但裸导入无法解析、页面空白；③ 非沙箱模式运行 → 同样崩（**不是沙箱权限问题**）。

### 9. DeepWiki 真实端点 — **Verified**（HTTP 层，非界面）

```bash
curl -X POST https://mcp.deepwiki.com/mcp -H 'Accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25",...}}'
curl -X POST https://mcp.deepwiki.com/mcp -d '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":null}'
curl -X POST https://mcp.deepwiki.com/mcp -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"read_wiki_structure","arguments":{"repoName":"facebook/react"}}}'
```
实测结果：
- `initialize`：200 / SSE，请求 `2025-11-25` **原样回声**（与插件 `SupportedProtocolVersions[0]` 一致），`serverInfo=DeepWiki 2.14.3`；响应头**无 `Mcp-Session-Id`** ⇒ 无状态，插件现有「每次请求独立」写法可直连。
- `tools/list`：公开 3 个工具 `ask_wiki_question` / `read_wiki_contents` / `read_wiki_structure`；`repoName` 的 `anyOf[string, array<string>]` 已作为单测 fixture 固化。
- `tools/call`：`read_wiki_structure{repoName:"facebook/react"}` 返回 200 + 真实目录文本。

---

## Known Limitations

1. **array / object 参数在表单模式下是多行文本框**（填 JSON），嵌套结构建议用 JSON 模式；不做递归表单渲染（刻意不扩大范围）。
2. **调用结果不落盘**：无调用历史、无回放。工具测试台是一次性调用，刷新即失。
3. **`anyOf` 只取首个带 `type` 的分支**：`repoName` 因此渲染为 string（无法在表单里切到数组模式，需走 JSON 模式）。
4. **不补 `Mcp-Session-Id`**：DeepWiki 这类无状态服务器没问题；若将来接需要会话亲和的服务器，需另立任务改 `StreamableHttpMcpTransport`。
5. 工具测试台**未做** resources / prompts 浏览、批量调用、并发保护（`McpClientSession` 无锁，详见 Review Risk R2）。

## Unresolved Issues

| # | 问题 | 状态 | 说明 |
| --- | --- | --- | --- |
| 1 | ~~未跑全量 `dotnet test --filter McpCenter`~~ | **已闭合** | 已补跑：**108/108 通过**（Evidence §3） |
| 2 | 插件层 e2e 的**全部 UI 用例**在本工作树跑不了 | **BLOCKED（环境问题）** | vite dev server 依赖预构建必崩（见 Verification §8）。API 层用例已通过并确认 `mcp-center v2.3.0` 生效。**本批新增的 UI 用例（工具测试台）未获得运行时验证** |
| 3 | DeepWiki 真实调用**未经界面**验证 | **Unknown** | HTTP 层已 Verified（§9）；界面层因 #2 未跑。需在有可用 dev server 的环境补一次走查 |
| 4 | 主工作树与全新 worktree 的 e2e 可用性差异 | **Inferred** | 主工作树已有可用的 `.vite/deps` 缓存，可能可以跑；本批未在主工作树验证（不随意动用户工作区） |

## 环境备注（非代码缺陷，供后续复用）

- 全新 worktree 缺 `node_modules` → 需先 `pnpm install`（宿主 + 插件各一次）。
- e2e `global-setup` 需要 SQLite provider，全新 worktree 无 `publish/` → 已从主工作树 `publish/Plugins/` 复制 `System.Data.SQLite.dll` + `e_sqlite3.dll` 到本工作树 `publish/Plugins/`（该路径被 `.gitignore` 忽略，不入库）。
- e2e 每轮会清理 `test-results/`（167 个文件）→ 触发环境 safe-delete 批量保护（阈值 50）→ 需先 `mv` 走而不是让 playwright 删。
