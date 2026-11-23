# 03 Plan — GitHub 账号绑定（PILOT-083）

> 步骤按「改完可立即验证」切分；实施记录见 05-evidence。

## 步骤 1：后端配置与服务层

- 改 `ForgeSelf.Host/Models/ForgeSetting.cs`：`#endregion`（网络）后新增 GitHub 账号区 5 字段（AccountLogin/TokenCipher/Scopes/BoundAt/AutoAttachToken，`[Category("GitHub账号")]`）。
- 新建 `ForgeSelf.Api/Services/GitHubOAuthConfig.cs`（ClientId/Scopes/端点/ApiBase/IsConfigured/FromConfiguration）。
- 新建 `ForgeSelf.Api/Services/GitHubBindingStore.cs`（Get/SaveToken/GetTokenAsync/Unbind/SetAutoAttach；令牌仅密文）。
- 新建 `ForgeSelf.Api/Services/GitHubAccountService.cs`（StartAsync/PollAsync/DescribeStartFailure/BuildVerificationUri/GetBearerToken 门控/GetStatus/Unbind/SetAutoAttach/FetchAccountLoginAsync）。
- 验证：`dotnet build ForgeSelf.Api`。

## 步骤 2：后端控制器与插件接线

- 新建 `ForgeSelf.Api/Controllers/GitHubAccountController.cs`（status/start/poll/unbind/auto-attach，`[Authorize("ApiKeyPolicy")]`，信封 `{ success, data|error }`）。
- 改 `AppBuilder.cs`：ISecretEncryptionService 注册后追加 GitHub 三件套 DI。
- 改 `Plugins/ServiceCollectionExtensions.cs`：PluginGitHubProvider 改工厂注册，桥接 `GitHubAccountService.GetBearerToken`。
- 改 `Plugins/Services/PluginPackageProviders.cs`：可选令牌源参数 + 请求前附加 Bearer。
- 改 `Plugins/Services/PluginHostTools.cs`：可选服务注入 + clone/pull 前缀 `-c http.extraheader=...`。
- 验证：`dotnet build` + 过滤测试。

## 步骤 3：后端测试（TDD 先行的锚定面）

- 新建 `ForgeSelf.Api.Tests/Unit/GitHubAccountServiceTests.cs`（设备流解析/预填/失败透传/协议等待态/绑定门控）。
- 新建 `Unit/GitHubBindingDtoTests.cs`（投影形状/校验/密文落盘/解绑/损坏密文容错）。
- 新建 `Unit/GitHubOAuthDefaultsGuardTests.cs`（默认端点/作用域 + appsettings 内置 ClientId 防丢）。
- 新建 `Unit/PluginGitHubProviderAuthTests.cs`（Bearer 附着/缺省不附着/SHA256 校验/Parse 守卫）。
- 验证：`dotnet test --filter FullyQualifiedName~GitHub`。

## 步骤 4：前端组件与集成

- 新建 `src/types/githubAccount.ts`、`src/services/githubAccountApi.ts`（authFetch）、`src/utils/clipboard.ts`（+测试）。
- 新建 `src/components/github/GithubBindPanel.vue`（四态）+ `GithubBindDialog.vue`。
- 新建 `src/components/settings/GithubAccountPanel.vue`（绑定/解绑/开关）。
- 改 `SettingsView.vue`（分类/导航/挂载/pageHeadMap）；改 `PluginStore.vue`（前置检查 + 武装备份 + 续装 + 对话框）。
- 新建 `src/components/github/GithubBindPanel.test.ts`、`src/components/settings/GithubAccountPanel.test.ts`。
- 改 `src/styles/themes/workshop-forge.contrast.test.ts`（+AC-11 对比度守卫）。
- 验证：`pnpm run check`（我方文件净）+ `pnpm run test`（过滤集）。

## 步骤 5：e2e 与全局设置

- 改 `e2e/global-setup.ts`：步骤 1.4 置空 publish 宿主 `GitHubOAuth:ClientId`（e2e 零外联）。
- 新建 `e2e/github-binding.spec.ts`（设置页面板四态 + 未配置分支错误透传 + API 契约）。
- 验证：`pnpm exec playwright test e2e/github-binding.spec.ts`（深档另行全量）。

## 步骤 6：文档与工件

- 新建 `docs/02-features/101-github-account.md`；`docs/README.md` 功能表 +1；`not-taken-decisions.md` +068。
- 本目录 05/06/07。

## 风险与回滚

- 回滚点：全部为新增文件 + 5 个既有文件小改（ForgeSetting/AppBuilder/ServiceCollectionExtensions/PluginPackageProviders/PluginHostTools/SettingsView/PluginStore/contrast test/global-setup/eslint），`git revert` 单提交可退。
- 最高风险：轮询误把 400 当异常（已用 AC5 锁）；令牌泄漏面（AC6/AC7/投影序列化守卫锁）。
