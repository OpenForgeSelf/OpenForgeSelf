---
task: 2026-11-20-github-device-binding
task_kind: feature
feature: 101
risk: { level: L1, triggers: [新增鉴权端点与令牌落盘面、凭据接线跨 3 个既有服务（Provider/HostTools/ServiceCollectionExtensions）、错误透传语义（不吞 400）], raised_by_agent: false }
expected_files: [ForgeSelf.Host/Models/ForgeSetting.cs, ForgeSelf.Api/AppBuilder.cs, ForgeSelf.Api/Plugins/ServiceCollectionExtensions.cs, ForgeSelf.Api/Plugins/Services/PluginPackageProviders.cs, ForgeSelf.Api/Plugins/Services/PluginHostTools.cs, ForgeSelf.Api/Services/GitHubOAuthConfig.cs, ForgeSelf.Api/Services/GitHubBindingStore.cs, ForgeSelf.Api/Services/GitHubAccountService.cs, ForgeSelf.Api/Controllers/GitHubAccountController.cs, ForgeSelf.Api/appsettings.json]
rollback: git revert 本提交即回滚（新增文件随提交整体移除；ForgeSetting 新字段向前兼容，旧代码忽略未知字段）
---

# Agent Task

> Task ID：PILOT-2026-11-20-github-device-binding｜闸门1 已批（2026-11-20 用户输入授权）。

## Objective

按 02-spec §8 AC1–AC12 完整实现 GitHub 账号绑定域：后端设备流 + 存储 + 端点 + 插件消费面凭据接线；前端设置面板四态 UX；e2e 零外联契约验证。

## Scope

### Allowed（全集，不得扩）

| 组 | 文件 |
| --- | --- |
| A 配置/宿主（2） | `ForgeSelf.Host/Models/ForgeSetting.cs`、`ForgeSelf.Api/appsettings.json`（GitHubOAuth 节） |
| B 后端新件（4） | `Services/GitHubOAuthConfig.cs`、`Services/GitHubBindingStore.cs`、`Services/GitHubAccountService.cs`、`Controllers/GitHubAccountController.cs` |
| C 组合根/插件接线（3） | `AppBuilder.cs`、`Plugins/ServiceCollectionExtensions.cs`、`Plugins/Services/PluginPackageProviders.cs`、`Plugins/Services/PluginHostTools.cs` |
| D 后端测试（4） | `ForgeSelf.Api.Tests/Unit/GitHubAccountServiceTests.cs`、`GitHubBindingDtoTests.cs`、`GitHubOAuthDefaultsGuardTests.cs`、`PluginGitHubProviderAuthTests.cs` |
| E 前端新件（8） | `types/githubAccount.ts`、`services/githubAccountApi.ts`、`utils/clipboard.ts`(+test)、`components/github/GithubBindPanel.vue`(+test)、`components/github/GithubBindDialog.vue`、`components/settings/GithubAccountPanel.vue`(+test) |
| F 前端集成（5） | `views/SettingsView.vue`、`views/PluginStore.vue`、`styles/themes/workshop-forge.contrast.test.ts`、`e2e/global-setup.ts`、**新** `e2e/github-binding.spec.ts`、`eslint.config.mjs` |
| G 文档（3） | `docs/02-features/101-github-account.md`、`docs/README.md`（+1 行）、`docs/07-decisions/not-taken-decisions.md`（+068） |

### Forbidden

- 不改 AI Provider / API Keys 既有加密链路与控制器；不建 XCode 实体表；不改 workshop-forge.css 主题色值（暗色状态横幅欠账另批）；不引入 ClientSecret；不外联 github.com 的测试。

## 验证命令

- 后端：`dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj` + `dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~GitHub`
- 前端：`pnpm run check`（本批文件零红）+ `pnpm run test -- --run src/components/github src/components/settings/GithubAccountPanel.test.ts src/utils/__tests__/clipboard.test.ts src/styles/themes/workshop-forge.contrast.test.ts`
- e2e：`pnpm exec playwright test e2e/github-binding.spec.ts`
- 门禁：`verify-pilot-artifacts`（四目录）、`verify-pilot-feature-writeback`、`check-commit-hygiene -Staged`、`design:check`
