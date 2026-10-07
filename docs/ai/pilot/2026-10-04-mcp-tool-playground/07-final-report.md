# Final Report — MCP 中心 · 工具测试台（v2.3.0）

> Task ID：2026-10-04-mcp-tool-playground｜日期：2026-10-04
> 工作树：`D:/src/my-proj/OpenForgeSelf/wt-mcp-playground`（分支 `feat/mcp-tool-playground`，自 `main@d88d709`）
> 闸门状态：闸门1 ✅（用户已批准 G1–G6）｜闸门2 **待验收**｜闸门3 **未执行**（未 commit / 未 push / 未打 tag）

## 1. Repository Understanding

插件 `Plugins/McpCenter`（id `mcp-center`）v2.2.0，已具备完整的外部 MCP 客户端：`McpClientManager` + `McpClientSession` + stdio / streamable-http / http-sse 三传输，能连服务器、能拉 `tools/list`（连 `inputSchema` 都拉回来了）。**真正的缺口只有两个**：后端没有「调用某个外部工具」的端点（现有 `{id}/test` 只做握手 ping）；前端工具弹窗只把 `fullName` + `description` 平铺成两行文本，`InputSchemaJson` 拉回来了却从未被解析，也没有发起调用的入口。

## 2. Selected Task

在其上增「工具测试台」：后端加 `POST api/mcp-center/servers/{id}/tools/invoke`（落在既有带鉴权的控制器内）；前端按 `inputSchema` 动态渲染参数表单、发起调用并展示耗时/文本/原始 JSON；以 DeepWiki MCP 作为联调靶子。

## 3. Changed Files

11 改 + 5 新增，逐项落在 04-task Allowed 名单内（AC11 ✅）。明细见 `05-evidence.md`。

## 4. Validation

| 项 | 命令 | 结果 |
| --- | --- | --- |
| 后端编译 | `dotnet build ForgeSelf.Api` | **0 error**（1189 warning 为仓库既有） |
| 插件后端单测（全量） | `dotnet test --filter "FullyQualifiedName~McpCenter"` | **108/108 通过** |
| 前端纯函数单测 | `cd Plugins/McpCenter/web && pnpm run test` | **23/23 通过** |
| 插件类型检查 | `pnpm run check` | **通过** |
| 插件构建 | `pnpm run build` | **通过**（index.js 63.58 kB / style.css 117.89 kB） |
| 宿主前端门禁 | `pnpm run check && pnpm run test` | **0 error / 81 warning**；**756/756 通过** |
| 插件层 e2e | `bash node_modules/.bin/playwright test e2e/plugins/mcp-center/` | **1 passed / 1 failed / 2 did not run**（环境阻断，见 §8） |
| 端点连通性 | curl DeepWiki `initialize` / `tools/list` / `tools/call` | **全部 200，端到端连通** |

## 5. Evidence

`05-evidence.md`。关键实证：
- **真实宿主已加载新版本**：e2e 证据行 `host plugin: mcp-center v2.3.0`。
- **真实 MCP 端到端调用**：后端用例 `Invoke_RealMockServer_ReturnsOkWithTextAndElapsed` 用 node mock MCP 服务器，`add(3,4)` → `sum=7`。
- **DeepWiki 三连验证**：`initialize` 原样回声 `2025-11-25`；响应头无 `Mcp-Session-Id`（无状态）；`tools/call read_wiki_structure(facebook/react)` 返回真实目录。
- **零回归**：`CallToolAsync` 抽成 `Inspect()` 后，既有 4 条契约断言仍全绿。

## 6. Review

`06-review.md` → **Final Decision: CHANGES_REQUIRED**。

## 7. Risk

R1（L2）UI 运行时未验证（最要紧）｜R2（L2）调用无并发保护（当前无并发调用方，记录不扩大）｜R3/R4（L1）array/object 与 anyOf 的降级已文档化｜R5（L1）e2e 环境依赖需手工补齐。

## 8. Problems Found

1. **【环境阻断 · 非代码缺陷】全新 worktree 里 vite dev server 必崩**：运行期触发依赖重新预构建时，esbuild 无法写入 vite 预先创建的 `node_modules/.vite/deps_temp_*`（`Access is denied`，14 项错误）→ 进程退出 → 所有 UI 用例 `ERR_CONNECTION_REFUSED`。
   已验证的边界：esbuild 写**自己创建**的 `deps_temp_*` 成功；写**任何进程预创建**的 `deps_temp_*` 一律失败。vite 的固定流程（先建目录再让 esbuild 写）在本环境必然踩中。已尝试并否决的规避：复用主工作树缓存（仍会在发现新依赖时崩）、`optimizeDeps.noDiscovery`（不崩但裸导入无法解析 → 页面空白）、非沙箱运行（同样崩，故不是沙箱权限问题）。
   **影响**：本批新增的 UI e2e 用例未执行；既有 UI 用例同失败 ⇒ 可证明与本批改动无关。
2. **【存量】** `McpCenterView.vue` 3 处类型问题（未使用 import、两处隐式 any）—— 被本批新增的 `check` 门禁暴露，已最小修复。
3. **【存量 · 未改】** `api/external.ts` 的 `testExternalServer` 返回类型声明为 `{success,message,durationMs}`，与后端实际返回 `{id,ok}` 不符。不阻塞本批，已记录。
4. **【环境】** e2e `global-setup.ts:250` 需要 SQLite provider，全新 worktree 无 `publish/` 时必失败；已从主工作树复制两个 DLL 到 `publish/Plugins/`（gitignored）。

## 9. Process Evaluation

- **做对的**：闸门1 前先做可行性实测（DeepWiki 三连 curl），把「能不能调通」的风险前置消灭；两个 `Unknown` 在实施前实读代码闭合（e2e 未断言旧弹窗 DOM、`McpCenterRuntime` 不管外部会话）；G5 决策落地时发现仓库已有 `mock-mcp-server.js`（带 `echo`/`add` 两个带 schema 的工具），**零新增测试基础设施**。
- **卡点**：全新 worktree 的 e2e 环境依赖（node_modules / SQLite DLL / vite 预构建 / safe-delete 阈值）四道坎，花掉大量时间且最终只打通一部分。**规律**：新开 worktree 跑 e2e 前应有一条前置检查清单。
- **未做的猜测**：没有因为"看起来对"就把 UI 验收标成通过——AC6/AC7/AC8 如实标 Inferred/Unknown。

## 10. 最重要的问题

**工具测试台的界面行为一次都没有在浏览器里跑过。** 后端与纯逻辑已被 108+23 条测试钉住，DeepWiki 端点也已 curl 验证，但「schema 渲染出表单 → 填参 → 点调用 → 出结果」这条用户可见链路只有类型检查与构建背书。这不是偷懒的结论，是环境阻断的客观结果（既有 UI 用例同样红），但它仍然意味着：**在补跑之前，本功能不能宣称可用。**

## 11. 下一步建议

1. **（最高优先）补一次 UI 运行时验证**，二选一：
   - 在 vite 依赖预构建可用的环境（如主工作树）跑 `bash node_modules/.bin/playwright test e2e/plugins/mcp-center/ --workers=1`；
   - 或在隔离宿主实例人工走查：新增服务器 → 点「填入 DeepWiki 预设」→ 保存 → 连接 → 「工具」→ 选 `read_wiki_structure` → 填 `facebook/react` → 发起调用 → 截图存 `ForgeSelf.Web/screenshots/`，回填 `05-evidence.md` 并把 `06-review.md` 的 Final Decision 改为 APPROVED。
2. **沉淀环境规律**：把「新开 worktree 跑 e2e 的前置检查清单」写进 `docs/04-standards/agent-workflow.md` 与 `e2e-testing` 技能（node_modules 安装 / SQLite DLL / test-results 需 mv 而非删 / vite 预构建崩溃的判据与规避结论）。
3. **另立 TODO**：`StreamableHttpMcpTransport` 补 `Mcp-Session-Id`（接需要会话亲和的服务器时才需要）；DeepWiki 预设是否做成内置样例服务器（需用户拍板，避免默认写入用户配置）。
4. 闸门2 通过后，按 `plugin-development` §四 走发布：打 tag 自动发布 或 `release-local.ps1 -UpdateDir`，再走查 + 运行实例只读复验。**本批未 commit / 未 push / 未打 tag / 未启停任何宿主进程。**
