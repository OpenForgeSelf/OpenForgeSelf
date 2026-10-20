# Repository Understanding

> 任务：2026-10-16-settings-proxy-mode-bypass（网络设置·代理三模式 + 旁路条目 + 测试代理）
> 本批为 `2026-10-16-settings-network-proxy`（同日均已完成验收、未提交）的**需求演进**，仓库现状以该批落地后的工作树为准。

## 项目结构（复核确认）

- 后端 `ForgeSelf.Api/`（.NET 10 + XML 落盘配置 `ForgeSetting.config`，非数据库）；前端 `ForgeSelf.Web/`（Vue 3.5 + Element Plus + pnpm）；测试 `ForgeSelf.Api.Tests/`（xUnit）；e2e `ForgeSelf.Web/e2e/`（Playwright 真实前后端）。

## 现状事实（本会话实测/实读，均 Verified）

| 位置 | 现状 |
|------|------|
| `ForgeSelf.Api/Models/ForgeSetting.cs` L44-69 | 网络字段五枚：`ProxyEnabled`(bool)/`ProxyHost`/`ProxyPort`/`ProxyUsername`/`ProxyPasswordCipher`，二值语义（开=手动代理，关=显式直连屏蔽系统代理） |
| `ForgeSelf.Api/Services/Net/ModelProxy.cs`（91 行全文读） | `IWebProxy` 实时决策器：`GetProxy` 直读 `ForgeSetting.Current`；`IsBypassed`=仅回环；**无模式概念、无旁路列表**；`DynamicProxyCredentials` 407 实时凭据 |
| `ForgeSelf.Api/AppBuilder.cs` | IHttpClientFactory 默认 handler 挂 ModelProxy（覆盖 M1~M5）；`AgentChatController` 同 client（M6） |
| `ForgeSelf.Api/Controllers/SettingsController.cs` L21-95 | GET/POST 代理五字段 + BR-5 校验（Regex 域名/IPv4、端口 1-65535）+ 密码三语义 |
| `ForgeSelf.Web/src/components/settings/NetworkPanel.vue` | 开关（ElSwitch）→ 开启出表单（地址/端口/用户名/密码）+ 保存；错误行常驻 h-4 占位 |
| 用户机器环境（输入6 实测） | WinINET 系统代理 = `127.0.0.1:10808` 且带 ProxyOverride 旁路列表（含 `10.*`、`*.local`、具体子域等）；`.NET HttpClientHandler` 旧默认（UseProxy=true + Proxy=null）= 跟随该系统代理**含旁路** |

## 验证入口（本仓库当前可用）

- 后端：`dotnet build ForgeSelf.Api -p:OutputPath=bin\BuildCheck\`；`dotnet test ForgeSelf.Api.Tests --filter …`
- 前端：`cd ForgeSelf.Web; pnpm run check` / `pnpm run test`
- e2e：`pnpm exec playwright test e2e/settings-network.spec.ts`（真实前后端）
- 现场：`pwsh -NoProfile -File scripts/dev-stack.ps1`（当前 :7301/:7399 在跑，代理配置=手动 127.0.0.1:10808）

## 重要目录职责 / 既有规范

- 功能真源：`docs/02-features/042-network-proxy-settings.md`（本批完成后更新）；样式走 `--el-*` + Tailwind；行内错误必须常驻等高占位（Part B B3）；破坏性/写操作成败可见；点即保存落盘。
- 上一批测试资产：`ModelProxyTests`(12)/`SettingsControllerNetworkTests`(9)/`HttpClientProxyWiringTests`(5)/`NetworkPanel.test.ts`(11)/`e2e/settings-network.spec.ts`(3)——本批需同步扩展，不允许留旧语义断言假绿。

## 候选任务与选择理由

用户输入7 明示两点需求（三模式选择器+测试按钮；旁路条目列表），即在上述真实现状上的增量演进；范围可控（后端 4 文件 + 前端 3 文件 + 测试），属**全量级**（契约/字段变更 + 新端点），产出 00~06 七件。
