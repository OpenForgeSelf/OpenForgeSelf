# Review

> 阶段：Stage 8｜Reviewer 视角重查八问，输出 PASS/FAIL、风险分级、发现项与最终结论。
> Task ID：2026-10-04-mcp-tool-playground

## Requirement Check（是否真正满足 Intent）

| 验收项 | 结论 | 依据 |
| --- | --- | --- |
| AC1 invoke 端点返回 `{ok,isError,text,rawJson,elapsedMs}` | **PASS** | `Invoke_RealMockServer_ReturnsOkWithTextAndElapsed`（真实 node mock，`add(3,4)`→`sum=7`） |
| AC2 无令牌 401；`McpAdminAuthTests` 绿 | **PASS** | 新动作落在既有控制器内继承类级策略；全量 108/108 通过（含 `McpAdminAuthTests`） |
| AC3 三类错误各有明确消息 + 400 | **PASS** | 单测覆盖空工具名 / 坏 JSON / 未连接 / 工具不在清单 |
| AC4 `CallToolAsync` 既有 4 条断言不变 + 新增 Detailed 4 形态 | **PASS** | 全量回归 108/108；新增回归用例 `CallTool_LegacyContract_StillConcatenatesText` |
| AC5 schemaForm 单测覆盖各分支 | **PASS** | 23/23；含 DeepWiki 真实 `anyOf` schema |
| AC6 弹窗按 schema 渲染表单（必填标记/enum/default） | **Inferred** | 组件已实现（必填标记、enum 下拉、default 预填、类型标签）；**界面运行时未验证**（见 Risk R1） |
| AC7 真实调用 DeepWiki 并展示结果耗时 | **Unknown** | HTTP 层 Verified；界面层因 e2e 环境阻断未验证 |
| AC8 DeepWiki 预设按钮 | **Inferred** | 已实现并挂 `data-testid="fill-deepwiki-preset"`；未运行时验证 |
| AC9 `plugin.json` 2.3.0 + 徽标 | **PASS** | e2e 证据行 `host plugin: mcp-center v2.3.0`（真实宿主读取） |
| AC10 验证命令全绿 | **PARTIAL** | build / 后端 108 / 前端 23 / check / build / 宿主 check+756 均 Verified；**插件层 e2e 未跑通** |
| AC11 零越界 | **PASS** | `git status --short` 11 改 + 5 新增，逐项落在 Allowed 名单 |

## Scope Check（是否超出 Scope）

- **PASS**：未改实体/建表/DB；未改鉴权策略本身；未新增 NuGet 或 npm 依赖；未改动三个传输层；未做调用历史/批量/资源浏览。
- **边界内但有争议的两处**（已在 03-plan 偏差记录留痕）：
  1. 修了 `McpCenterView.vue` 3 处**存量**类型问题（未使用 import、两处隐式 any）。理由＝本批引入的 `check` 门禁必须绿；修复为零风险字面修改，未放宽编译选项。
  2. 类型 `inputSchema` → `inputSchemaJson` 重命名。理由＝对齐后端真名（原名字段从未被赋值也从未被读取，属既有不一致），非新增破坏。

## Test Check（是否覆盖 Acceptance Criteria）

- **PASS（后端）**：12 条新增 + 96 条既有全绿；成功路径用真实 mock MCP 服务器而非纯 mock，判据强度足够。
- **PASS（纯逻辑）**：schema→表单 23 条，分支齐全（含 `anyOf`、type 数组、enum 优先、空 schema、非法 JSON、必填判定边界）。
- **FAIL（UI 运行时）**：新增的工具测试台 e2e 用例**未执行**（环境阻断）。这是本次交付的最大缺口。

## Architecture Check（是否存在架构不一致）

- **PASS**：调用链路复用既有 `McpClientSession` + `McpClientManager`，未新建平行通道；结果提取抽为单一 `Inspect()`，两个入口共用，无逻辑分叉。
- **PASS**：前端沿用插件既有 `http.ts` / `types` / `api` 分层；新组件只依赖已在用的 EP 组件集合；`schemaForm.ts` 零 UI 依赖，可单测。
- **PASS**：`InputSchemaJson` 保持字符串不变，解析放前端 —— 避免跨前后端破坏性契约变更。

## Risk

| 级别 | 项 | 说明 |
| --- | --- | --- |
| **R1（L2 · Major）** | UI 运行时未验证 | 工具测试台组件只经过了类型检查与构建，**没有一次真实浏览器渲染/点击**。e2e 因本工作树 vite 依赖预构建必崩而跑不了。合入前必须在可用环境补跑 |
| **R2（L2 · Major）** | `McpClientSession.CallToolAsync` 无并发保护 | 本批未引入并发（运行时无心跳触碰会话，已核实 `McpCenterRuntime` 不管外部会话），但测试台开放了用户手动并发调用的可能；`_toolsLock` 只保护工具清单，不保护调用。当前风险可控（无已知并发调用方），记录而非扩大改动 |
| **R3（L1 · Minor）** | `array`/`object` 参数表单是 JSON 文本框 | 用户填错格式会得到远端报错而非前端拦截。已通过 `placeholderFor` 给出样例，并在文档标注降级 |
| **R4（L1 · Minor）** | `anyOf` 只取首个分支 | DeepWiki `repoName` 因此恒为 string，想传数组需切 JSON 模式。文档已标注 |
| **R5（L1 · Minor）** | e2e 环境依赖被手工补齐 | 本工作树 `publish/Plugins/*.dll` 由主工作树复制而来（gitignored）。其他全新 worktree 跑 e2e 会同样卡在 `global-setup.ts:250` |

## Findings

### Critical
无。

### Major
1. **UI 运行时验证缺失**（R1）：工具测试台是本批的核心交付，却只有静态验证。虽然根因是环境问题（与代码无关，既有 UI 用例同样失败），但不能据此宣称完成。建议：合入前在主工作树或修好 vite 预构建的环境补跑 `e2e/plugins/mcp-center/`，并对 DeepWiki 预设做一次真实调用 + 截图。

### Minor
2. `types/external.ts` 中 `McpExternalToolDto.inputSchema` 曾与后端字段名不一致（存量），本批改名修好；若别处还有引用会编译报错——已跑 `check` 确认无残留。
3. `api/external.ts` 里 `testExternalServer` 的返回类型声明为 `{success,message,durationMs}`，与后端实际返回 `{id,ok}` 不符（**存量问题，未在本批修改**）。不影响本批功能，记录备查。

## Final Decision

**CHANGES_REQUIRED**

理由：代码、契约、后端与纯逻辑测试均已达标（108/108、23/23、类型检查与构建全绿，真实宿主已加载 v2.3.0），但 **AC6/AC7/AC8 三项界面验收项只有 Inferred/Unknown 级别的证据**——核心功能的浏览器运行时行为一次都没有被验证过。按「Evidence 不足以证明任务完成即不得交付」的判据，须在可用环境补跑插件层 e2e 后转为 APPROVED。

**转为 APPROVED 的前置条件（二选一）**：
- (a) 在 vite 依赖预构建可用的环境跑通 `e2e/plugins/mcp-center/`（含新增工具测试台用例）；或
- (b) 在隔离宿主实例上人工走查一次：新增 DeepWiki 服务器 → 连接 → 工具 ≥3 → 调 `read_wiki_structure` → 截图留证，并回填本文件与 05-evidence。
