# Agent Task

> 阶段：Stage 4｜Task ID：PILOT-052（2026-10-05-mcp-center-endpoint-url）
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备且经**闸门1 确认**后才允许 Implement。

## Task ID

PILOT-052（2026-10-05-mcp-center-endpoint-url）

## Objective

MCP 中心自带界面的地址展示从裸 `http://host:port` 改为**可直接连的端点** `http://host:port/mcp`（顶部 chip、服务地址卡、复制地址三处一致），卡片补一行「客户端须使用 /mcp 路径」说明；真实绑定地址仍留在运行状态卡；插件版本升 `2.2.1`；以插件层 e2e 把「界面地址 == 后端 `listenUrl` + `/mcp`」钉成常驻判据。

## Scope

### Allowed

- `Plugins/McpCenter/web/src/McpCenterView.vue`（03-plan F1）
- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`（03-plan F2）
- `Plugins/McpCenter/plugin.json`、`Plugins/McpCenter/McpCenter.csproj`（03-plan F3/F4，仅版本号）
- `docs/02-features/034-mcp-center.md`（03-plan F5）
- `Plugins/McpCenter/web/dist/**` 重建（gitignored 运行产物，非入库文件）
- 本任务工件链 `docs/ai/pilot/2026-10-05-mcp-center-endpoint-url/**`、`.forgeself/memory/2026-10-05.md`、根 `TODO.md`

### Forbidden

- 后端任何文件：`Plugins/McpCenter/Services/**`、`Models/**`、`Controllers/**`、`McpCenterPlugin.cs`（尤其**不得**给 `McpGatewayConfig.ListenUrl` 加 `/mcp`——它是 Kestrel 绑定串）。
- 宿主：`ForgeSelf.Api/**`、`ForgeSelf.Web/src/**`、`e2e/global-setup.ts`、`e2e/fixtures/**`、`playwright*.config.ts`、`scripts/**`。
- 其它插件与其它 e2e spec；鉴权/权限逻辑；DB 结构与数据。
- 新增依赖（含为该插件引入 vitest）；新增网络请求。
- **生产动作**：`git commit` / `git push` / `git tag` / 停启杀任何宿主进程 / 在任何运行实例上做写操作（本轮全部禁止，等用户明确指示）。
- 借本任务顺手做无关重构或修其它 TODO 项。

## Acceptance Criteria

- [ ] AC1 插件前端 `pnpm run build` 成功，`dist/index.js` 重新生成
- [ ] AC2 插件层 e2e 断言 chip 文本 == config `listenUrl` + `/mcp`，`[data-mcp-url]` == 同值（含同瞬间证据行）
- [ ] AC3 既有断言保留：状态卡数量 3、运行状态卡仍显示「监听 host:port」（绑定事实未隐藏）
- [ ] AC4 真连验证：端点 `POST /mcp` → 200/`universal_tool`；根地址同法 → 非 200
- [ ] AC5 `pnpm run check` 0 error；`dotnet test --filter "FullyQualifiedName~McpCenter"` 全绿
- [ ] AC6 `plugin.json` 与 `McpCenter.csproj` 版本同串 `2.2.1`；`docs/02-features/034-mcp-center.md` 已同步
- [ ] AC7 视觉核对：chip 加长后无截断/溢出，卡片换行正常（截图读图）
- [ ] AC8 变更文件集合 == Allowed 列表（无越界），且全部为本地改动（无 commit/push/tag/宿主操作）

## Expected Files

- `Plugins/McpCenter/web/src/McpCenterView.vue`
- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`
- `Plugins/McpCenter/plugin.json`
- `Plugins/McpCenter/McpCenter.csproj`
- `docs/02-features/034-mcp-center.md`

## Verification Commands

```bash
cd Plugins/McpCenter/web && pnpm run build
cd ForgeSelf.Web && pnpm run check
cd ForgeSelf.Web && pnpm exec playwright test e2e/plugins/mcp-center/mcp-center.spec.ts --workers=1
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~McpCenter"
Select-String -Path Plugins/McpCenter/plugin.json,Plugins/McpCenter/McpCenter.csproj -Pattern '2\.2\.1'
```

门禁档位：**快档**（`AGENTS.md` §5.6；不触宿主源码/脚本/共享 e2e 基建）。
