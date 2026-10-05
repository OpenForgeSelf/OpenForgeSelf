# Plan

> 阶段：Stage 3｜Task ID：PILOT-052（2026-10-05-mcp-center-endpoint-url）
> 必须具体到真实文件与真实行号；偏差在 Implement 期先记录再修正。

## Files To Change

| # | file | reason |
| --- | --- | --- |
| F1 | `Plugins/McpCenter/web/src/McpCenterView.vue` | 唯一实现文件：新增 `mcpEndpointUrl` computed（script 区，紧邻 `loadGatewayConfig`）；`copyListenUrl()`（`:181-191`）改取端点地址（函数可改名 `copyMcpUrl`，模板同步）；template `:552-553` chip 文本/title 改端点；`:651-653` 卡值改端点并加 `data-mcp-url`；卡片内加 FR4 说明行（复用既有 `.card-sub` 样式 `:1361-1364`） |
| F2 | `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts` | 把 `:228` 的弱断言（chip 含 `127.0.0.1`）升级为端点等值断言（AC2/AC3/AC4）：同一用例内 `fetch(`${BACKEND_URL}/api/mcp-center/config`, { headers: AUTH_HEADERS })` 取 `listenUrl`，断言 `chip 文本 === listenUrl + '/mcp'` 且 `[data-mcp-url] === 同值`；保留状态卡数量=3 与「监听 …」断言；补根路径 404 反向腿（AC4） |
| F3 | `Plugins/McpCenter/plugin.json` | `Version` `2.2.0 → 2.2.1`（BR6；版本徽标与更新链路的真源） |
| F4 | `Plugins/McpCenter/McpCenter.csproj` | `Version/AssemblyVersion/FileVersion` 同步 `2.2.1`（034 既有「版本统一」约定；漏改会导致徽标与程序集版本不一致） |
| F5 | `docs/02-features/034-mcp-center.md` | 「前端界面（/mcp-center）」节（`:206-212`）描述地址展示口径；新增「验证记录（v2.2.1）」节，填真实读数 |

> 运行产物（不入库、但必须重建）：`Plugins/McpCenter/web/dist/{index.js,style.css}` —— 由 F1 经 `pnpm run build` 生成（`.gitignore` 忽略；`ForgeSelf.Api.csproj` 的 `StageAllPlugins`/`StagePluginsToPublish` 依赖它进运行与发布目录）。

## Files Explicitly Not Changed

- `Plugins/McpCenter/Services/**`、`Models/**`、`Controllers/**`、`McpCenterPlugin.cs`（后端零改动；`ListenUrl` 仍是绑定串 —— BR2）。
- `ForgeSelf.Api/**`、`Plugins/` 下其它 17 个插件、`ForgeSelf.Web/src/**`、`e2e/global-setup.ts`、`playwright*.config.ts`、`scripts/**`。
- `Plugins/DesignSystem/**`（先例只读引用，不动）。

## Implementation Steps

1. **F1**：在 script 区加
   `const mcpEndpointUrl = computed(() => { const base = (gatewayConfig.value?.listenUrl ?? '').trim().replace(/\/+$/, ''); return base ? `${base}/mcp` : '' })`；
   - chip：文本与 `:title` 都用 `mcpEndpointUrl`（空串时 chip 不渲染，沿用 `v-if="gatewayConfig"`）；
   - 卡值：`{{ gatewayConfig ? (mcpEndpointUrl || '（未运行，暂无地址）') : '加载中...' }}`，元素加 `data-mcp-url`；
   - 说明行：`<div class="card-sub">客户端须使用 /mcp 路径（根路径 404）</div>`；
   - 复制：`copyListenUrl` 改名为 `copyMcpUrl`，取 `mcpEndpointUrl`，模板 `@click` 同步；其余（失败提示、2s 提示复位）不变。
2. **构建**：`cd Plugins/McpCenter/web && pnpm run build`（若命中 U2，按 `plugin-development` §3.2 出树构建兜底；无论哪种方式都要在 05-evidence 写明实际用的方式）。
3. **F2**：改造 `mcp-center.spec.ts` 的 `/mcp-center` UI 用例（`:219-253`）：新增 config 取数 + 两处等值断言 + `mark()` 证据行；新增/内联根路径反向腿（对 `listenUrl` 直发 `POST /mcp` 期望 200 + 对 `listenUrl + '/'` 期望非 200，二者共用同一个 `mcpCall` 风格实现，避免新增 mock）。
4. **F3/F4**：两处版本号同升 `2.2.1`（用 PowerShell 实读回显核对同串，不靠肉眼）。
5. **F5**：文档同步（界面口径 + v2.2.1 验证记录，读数从步骤 6 的真实输出拷贝）。
6. **验证**：按下节命令逐条跑，真实记录；失败按 §6 迭代（同一问题第 3 次失败升级给人）。
7. **收口**：写 `05-evidence.md`（Changed Files / 命令+结果 / 截图路径 / Known Limitations / Unresolved Issues）→ `06-review.md`（八问 + Final Decision）→ `07-final-report.md` → 交闸门2。

## Test Plan

| 层 | 测什么 | 落点 |
| --- | --- | --- |
| 构建 | 插件前端能出产物，产物内含端点派生逻辑 | `pnpm run build` + 产物检索 |
| 插件层 e2e（**主判据**） | 界面展示的地址 == 后端真源 `listenUrl + '/mcp'`；绑定地址仍在；根路径确为 404 | `e2e/plugins/mcp-center/mcp-center.spec.ts` |
| 后端回归 | 插件后端行为与版本变更后无回归 | `dotnet test --filter "FullyQualifiedName~McpCenter"` |
| 宿主前端静态检查 | e2e spec 改动不影响宿主类型/lint 门禁 | `ForgeSelf.Web && pnpm run check` |
| 视觉 | chip 加长后不截断/不溢出、卡片换行正常 | e2e 既有截图（`screenshots/e2e/mcp-center/{tools-tab,gateway-tab}.png`）读图 |

> 不写 vitest：该插件 `web/package.json` 无测试脚本、无 vitest 依赖、全插件无 `*.test.ts`（实测）；为 4 行字符串派生引入测试框架违反 §1 硬性约束 4「不新增大规模依赖」，且 §5.0 决策表把"UI 行为"落在 Playwright e2e。

## Verification

```bash
# V1 插件前端构建（必跑；dist 驱动 e2e 与发布）
cd Plugins/McpCenter/web && pnpm run build

# V2 宿主前端静态门禁（e2e spec 在宿主树内）
cd ForgeSelf.Web && pnpm run check

# V3 插件层 e2e（零 mock；globalSetup 自动 publish 宿主 + 起宿主 + 解密 token）
cd ForgeSelf.Web && pnpm exec playwright test e2e/plugins/mcp-center/mcp-center.spec.ts --workers=1

# V4 后端回归（本批未改后端，作为版本号/构建回归）
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~McpCenter"

# V5 版本同串实读
Select-String -Path Plugins/McpCenter/plugin.json,Plugins/McpCenter/McpCenter.csproj -Pattern '2\.2\.1'
```

**门禁档位（`AGENTS.md` §5.6）**：本批改动面 = 插件前端 + 该插件 e2e + 该插件清单/文档，**不触**宿主源码、`scripts/**`、共享 e2e 基建（`global-setup.ts`/`fixtures/**`/`playwright.*.config.ts`）⇒ 走**快档**（V1–V4），不跑全量 `dotnet test` 与全量 e2e。汇报必须写明走的哪档。

## Risks

| 风险 | 概率 | 处置 |
| --- | --- | --- |
| e2e UI 用例在**全新 worktree** 因 vite 预构建崩而跑不了（已登记环境坎，本 worktree 非全新、`web/dist` 已存在） | 低 | 命中则如实记 `05-evidence` 的 Unresolved，不改判据、不伪造绿；必要时改用「发布产物读内容 + curl 真连」作为补充证据并标注等级 |
| 剪贴板读回断言不稳（U1） | 中 | 降级为文本等值 + 共用 computed 的静态事实，写进 Known Limitations |
| 版本号与并行 worktree `wt-mcp-playground`（2.3.0）冲突 | 中 | 合并时取高者并复跑插件门禁；本批在 03/05 显式登记 |
| chip 加长后 `max-width:260px` 截断 | 低 | 视觉读图核对；必要时把 `title` 设为全串（已含），**不**为此放宽为无界宽度 |
| 改了展示但用户仍按老习惯填根地址 | 低 | FR4 说明行 + 文档同步 |

## Rollback

- 代码：`git checkout -- Plugins/McpCenter/web/src/McpCenterView.vue ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts Plugins/McpCenter/plugin.json Plugins/McpCenter/McpCenter.csproj docs/02-features/034-mcp-center.md`，随后 `pnpm run build` 重建 `dist` 即可回到旧界面。
- 无 DB 变更、无数据迁移、无契约破坏、无发布动作 ⇒ 回滚零残留。

## Deviation Log

（Implement 期发现 Plan 与仓库不符时，在此追加一行并说明修正，不得直接绕过。）
