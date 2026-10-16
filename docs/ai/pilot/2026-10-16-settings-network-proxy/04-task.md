---
task_id: 2026-10-16-settings-network-proxy
feature: 042-network-proxy-settings
risk: { level: L1, triggers: ["ForgeSelf.Api/Services/Net/ModelProxy.cs", "ForgeSelf.Api/AppBuilder.cs"], raised_by_agent: false }
gate1: { mode: human, at: 2026-10-16T10:30, ref: "用户输入3 需求 + 回复「开工」批准默认案（仅 HTTP/HTTPS、关闭=显式直连、密码三语义）" }
gate2: { mode: human, at: 2026-10-16T23:30, decision: approved, ref: "用户输入8：先提交本次修改，然后按技能流程发布本地包" }
gate3: { mode: agent, at: 2026-10-16T23:30 }
expected_files:
  - ForgeSelf.Api/Models/ForgeSetting.cs
  - ForgeSelf.Api/Services/Net/ModelProxy.cs
  - ForgeSelf.Api/AppBuilder.cs
  - ForgeSelf.Api/Controllers/SettingsController.cs
  - ForgeSelf.Api/Controllers/UnifiedAI/AgentChatController.cs
  - ForgeSelf.Web/src/components/settings/NetworkPanel.vue
  - ForgeSelf.Web/src/views/SettingsView.vue
  - ForgeSelf.Web/src/types/settings.ts
  - ForgeSelf.Web/src/services/settingsApi.ts
rollback: "git revert <本批 commit>（纯新增面板+配置字段，无 DB 迁移；ForgeSetting 新字段旧实例读取时自然缺省）"
writeback: { feature_doc: done, reason: "docs/02-features/042-network-proxy-settings.md + README 总表；规律入 agent-workflow B3" }
---

# Task（Agent 工作单元）

> 阶段：Stage 4 ｜ Task ID：2026-10-16-settings-network-proxy ｜ 闸门1 批准后方可进入 Stage 5 Implement

## Objective

按 03-plan S1→S8 实现「设置 · 网络设置（模型请求代理）」：ForgeSetting 5 字段 + ModelProxy 实时决策 + IHttpClientFactory 默认 handler 接线（M1~M5）+ AgentChatController transport（M6）+ /api/settings 契约扩展 + NetworkPanel.vue 面板 + 三层测试（后端单测/前端单测/e2e）+ dev-stack 现场探针验证 + 文档回写。

## Scope

**Allowed**
- ForgeSelf.Api：`Models/ForgeSetting.cs`、`Services/Net/ModelProxy.cs`（新）、`AppBuilder.cs`（仅代理注册行 + 可选抽静态方法）、`Controllers/SettingsController.cs`、`Controllers/UnifiedAI/AgentChatController.cs`
- ForgeSelf.Api.Tests：`Unit/ModelProxyTests.cs`（新）、`Unit/HttpClientProxyWiringTests.cs`（新）、`Controllers/SettingsControllerNetworkTests.cs`（新）
- ForgeSelf.Web：`src/types/settings.ts`、`src/services/settingsApi.ts`、`src/components/settings/NetworkPanel.vue`（新）、`src/views/SettingsView.vue`、`src/components/settings/__tests__/NetworkPanel.test.ts`（新）、`e2e/settings-network.spec.ts`（新）、`screenshots/`（证据）
- 文档：`docs/02-features/` 相应真源节、`TODO.md`（衍生项登记）、`.forgeself/memory/2026-10-16.md`

**Forbidden**
- 改任何插件（Plugins/*，含 AIAgent 休眠 HttpClient——只记 TODO 不动）
- 非模型请求出口接线（UpdateChecker/UpdateService/WorkflowEngine/McpCenter/Scheduler）
- 新增 NuGet/npm 依赖；改数据库结构；SOCKS5/IPv6 支持
- 顺带重构无关代码；发布/打 tag/停启宿主进程

## Acceptance Criteria

- [ ] ForgeSetting 5 新字段默认值=关/空/0，旧配置文件兼容加载（无迁移）
- [ ] ModelProxy：关→null；开+外网目标→`http://host:port`；回环→null；用户名→Credential；解密失败→无密码+Warn（单测覆盖）
- [ ] factory 默认 client 与 typed AIService 的 handler Proxy=ModelProxy（接线单测）
- [ ] AgentChatController OpenAIClientOptions 带过代理 Transport
- [ ] POST /api/settings：合法→落盘+回显；开启无地址/端口越界→400 不落盘；密码密文存储、任何响应不含明文
- [ ] 设置页出现「网络设置」导航（api-server 之后，图标 Rank），面板交互符合 02-spec 交互规格表（AC-UX-1~5）
- [ ] `dotnet build`+`dotnet test` 绿；`pnpm run check` 0 err + `pnpm run test` 绿；e2e settings-network 通过 + 截图读图
- [ ] dev-stack 现场：开代理→探针收到模型类请求；关→直连且探针无感；测后恢复原配置
- [ ] 文档回写：真源功能文档 + 日记进度 + TODO 衍生项（SOCKS5/IPv6/连通检测/非模型出口/休眠客户端清理）

## Expected Files

见 03-plan S1~S8 的 `file:` 清单（新增 9 文件 + 修改 8 文件，无其他）。

## Verification Commands

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj
cd ForgeSelf.Web; pnpm run check; pnpm run test
pnpm run test:e2e e2e/settings-network.spec.ts
pwsh -NoProfile -File scripts/dev-stack.ps1        # 现场验证，完毕 -Stop 停自己起的栈
```
