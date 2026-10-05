# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式｜Task ID：PILOT-052（2026-10-05-mcp-center-endpoint-url）｜用户输入：2026-10-05 输入1（追认 2026-09-27 输入16 欠账）
> 状态只报事实，禁止模糊表述（对齐 `AGENTS.md` §10.4/§10.5）。

## 1. Repository Understanding

我确认了：

- 插件源码在仓库根 `Plugins/<PascalCase>/`；MCP 中心自带界面为单根视图 `Plugins/McpCenter/web/src/McpCenterView.vue`，产物 `web/dist/index.js` **被 gitignore 但由 `ForgeSelf.Api.csproj` 的 `StageAllPlugins`/`StagePluginsToPublish` 拷进运行与发布目录** ⇒ 改插件前端必须本机重建 dist。
- 「地址」的真源是后端 `Services/McpGatewayConfig.cs:27` 的 `ListenUrl`，它**同时是 Kestrel 的绑定串**（`McpGatewayServer.cs:55 UseUrls`）⇒ 不能在后端给它加路径；对外端点是 `POST /mcp`，根路径 404。
- 该插件前端**无测试框架**（无 vitest / 无 `*.test.ts` / 无 test 脚本）⇒ 判据按项目 §5.0 决策表落在插件层 Playwright e2e。
- 版本号两处同串约定：`plugin.json` `Version` + `McpCenter.csproj` `Version/AssemblyVersion/FileVersion`。

## 2. Selected Task

**MCP 中心界面地址补 `/mcp` 路径（防误导）** —— 2026-09-27「输入16」用户原话「增加地址，不写路径会让人误会」，当日三条拆解全未勾选、随后被输入17 挤掉并记为「暂缓」，至今零实施（用户本轮以「我记得之前已经下过任务」追认）。

## 3. Changed Files

- `Plugins/McpCenter/web/src/McpCenterView.vue`
- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`
- `Plugins/McpCenter/plugin.json`（2.2.0 → 2.2.1）
- `Plugins/McpCenter/McpCenter.csproj`（2.2.0 → 2.2.1 / 2.2.1.0）
- `docs/02-features/034-mcp-center.md`
- 运行产物（不入库，已重建）：`Plugins/McpCenter/web/dist/*`
- 工件链：`docs/ai/pilot/2026-10-05-mcp-center-endpoint-url/00–07`

`git diff --stat` 实读：`5 files changed, 74 insertions(+), 16 deletions(-)`。

## 4. Validation

Build（插件前端）：`pnpm run build` → PASS（`dist/index.js 49.62 kB`，14.30s）
Unit Test：插件前端 N/A（该插件前端无测试框架；本批不引入依赖）；宿主 vitest `pnpm run test` → **67 files / 756 tests passed**（128s，§5.6 快档必跑项）
E2E：`playwright test e2e/plugins/mcp-center/mcp-center.spec.ts --workers=1` → **3 passed (2.1m)**，关键证据行
`地址展示：config.listenUrl=http://127.0.0.1:19483 → 界面展示=http://127.0.0.1:19483/mcp；根地址 404 / 端点 200（tools[0]=universal_tool）`
后端回归：`dotnet test --filter "FullyQualifiedName~McpCenter"` → **96/96 通过**（首次 22 红已归因到环境 `%TEMP%` 写入受限，重定向 TEMP 后复跑全绿）
静态检查：`pnpm run check` → **0 errors / 81 warnings**（既有基线量级）
视觉：`screenshots/e2e/mcp-center/{gateway-tab,tools-tab}.png` 读图核对通过

## 5. Evidence

见 `docs/ai/pilot/2026-10-05-mcp-center-endpoint-url/05-evidence.md`：所有判据均附来源等级；两处环境性失败（Proxy 502 / TEMP 拒建目录）与代码结果严格分离并给了控制实验。

## 6. Review

`06-review.md` → **Final Decision: APPROVED**（Risk **L1**；八问逐项 PASS；无 Critical/Major，4 条 Minor 已登记）。

## 7. Risk

**L1** —— 影响面＝单插件展示层；无 DB、无契约、无鉴权、无宿主改动；回滚＝`git checkout` 5 文件 + 重建 dist。
已知限制：剪贴板未做自动读回断言；`0.0.0.0` 场景照直展示；1280 宽下说明行折两行。

**发布与复验（2026-10-05 追加）**：按用户指令做本地离线整包（未打 tag）→ `D:\src\my-proj\OpenForgeSelf\updates\OpenForgeSelf-2.7.3.2610051746-win-x64.zip`（签名 `Valid`、SHA256 MATCH、L1/L2/L3 布局不变量通过、包内 mcp-center **v2.2.1**）；用户升级完成后已做**运行实例只读复验** —— `/api/plugin` 报 `mcp-center 2.2.1`（18 个内置插件）、`/api/mcp-center/config` 的 `listenUrl=http://127.0.0.1:18890`（界面即 `…:18890/mcp`）、宿主**伺服的前端产物与仓库产物 SHA256 逐字节一致**。浏览器截图那一格以产物逐字节一致作为显式替代判据（理由见 05-evidence）。

## 8. Problems Found

1. **环境**：`HTTP_PROXY/HTTPS_PROXY` 指向 `127.0.0.1:10808` 且无 `NO_PROXY` ⇒ Playwright webServer 对 `localhost` 的可用性探测恒 502、超时，**表现为「代码有问题」实际是环境**（本批靠运行时 `NO_PROXY` 绕过；已提出固化为仓库侧守卫的 TODO）。
2. **环境**：`%TEMP%` 下新建目录被拒 ⇒ 依赖 `Path.GetTempPath()` 的后端测试整片假红（22/96），易被误判为代码回归。
3. **流程**：2026-09-27 已下单的任务在同日被新输入挤掉后**没有任何回流机制**（日记里三条 `- [ ]` 一直没勾、TODO 只写「暂缓」），直到用户凭记忆在 8 天后重新提出——建议日记「下一步」字段与 TODO 暂缓项设定期回看。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS（两侧环境坎均定位到根因后拿到真绿） |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 最重要的问题

**"环境假红"会污染判责**：本轮两类失败（Playwright webServer 502、dotnet 测试 `%TEMP%` Access denied）在默认视角下都像"我改坏了"；`AGENTS.md` §5.6 只要求「基线红先对表」，但**本机这两类红连全量基线都不会列出**（因为它们是本次会话的环境，不是仓库的稳定基线）。建议在 §5.6 增补一条：**本地跑门禁前先确认 `NO_PROXY` 与 TEMP 可写，否则先排除环境再判责**。

## 11. 下一步建议

只提一个：把「本机 e2e/测试的规范化运行环境」固化下来（一条 `pnpm run e2e:mcp-center` 或 `scripts/` 下的包装，内含 `NO_PROXY=localhost,127.0.0.1` 与仓库内 `TEMP`），让下一个 agent 不再浪费三个回合重新发现这两条环境坑。
