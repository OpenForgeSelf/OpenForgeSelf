# 00 Repository Understanding — GitHub 账号绑定（PILOT-083）

> 生成于 2026-11-20，灾后重建复刻（2026-11-23 提交 b5916ef 丢失后按事实重建，内容锚定当日真实仓库状态）。

## 1. 技术栈与架构事实（读码确认）

- 宿主：`ForgeSelf.Api`（.NET 10 + ASP.NET Core，Controllers/Services/Entities 分层）+ `ForgeSelf.Host`（托盘/服务壳，含 `Models/ForgeSetting.cs` 全局配置）。
- ORM：NewLife.XCode（唯一 ORM）；全局配置走 `[Config("ForgeSetting")]` 的 XML 落盘（`ForgeSetting.config`），非实体表。
- 敏感数据：`ISecretEncryptionService`（ForgeSelf.Abstractions；宿主实现 AesSecretEncryptionService，AES-256-CBC，`v2:` 版本化前缀）——既有 AI Provider ApiKey / API 子密钥同构。
- 插件体系：`ForgeSelf.Api/Plugins/`，`PluginGitHubProvider`（Release 下载，`https://api.github.com`，`GetStringAsync/GetByteArrayAsync`，无凭据）与 `PluginHostToolCatalog`（git clone/pull 经 `RunAsync("git", args, ct)`，无凭据）。
- 前端：Vue 3.5 + Element Plus（`--el-*` token 红线）+ Pinia + vitest（jsdom，unplugin 自动解析组件）；`services/` 层经 `authFetch` 带真实 API token。
- 测试：xUnit + FluentAssertions（Api.Tests/Unit）；Playwright e2e（globalSetup 起 publish 宿主，零 mock 零外联）。

## 2. 与本任务相关的既有接缝

| 接缝 | 文件 | 现状（实施前） |
|------|------|----------------|
| 全局配置 | `ForgeSelf.Host/Models/ForgeSetting.cs` | 网络（代理）字段至 ProxyPasswordCipher，无 GitHub 字段 |
| 密钥加密 | `ISecretEncryptionService` | 已注册 DI（AppBuilder），供 AI Provider 使用 |
| GitHub 下载 | `PluginGitHubProvider` | 匿名请求（私有仓库 404/401） |
| git 克隆 | `PluginHostToolCatalog.CloneAsync/SyncAsync` | 匿名 clone |
| 设置页 | `SettingsView.vue` navItems/pageHeadMap | 12 分类，无 GitHub 账号 |

## 3. 约束（来自 AGENTS.md / DESIGN.md）

- 令牌永不明文落盘/落日志/落响应（§B13 提交卫生 + 密钥管理既有准则）。
- UI 一律 `--el-*` 变量；禁裸色值；新增设置面走 PageHead/面板既有骨架。
- e2e 零外联（真实 GitHub OAuth 调用不可进 CI）。
