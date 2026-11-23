# 05 Evidence — GitHub 账号绑定（PILOT-083）

> 重建批验证时间：2026-11-23。所有证据为当日真实命令输出（来源等级标注）。

## 1. 变更面（git 实测）

- 新增 20 文件：后端 4 源 + 4 测试；前端 types/services/utils 组件 8 + 3 测试；e2e 1；docs 1（本目录 8 件另计）。
- 修改 10 文件：ForgeSetting.cs、AppBuilder.cs、ServiceCollectionExtensions.cs、PluginPackageProviders.cs、PluginHostTools.cs、appsettings.json、SettingsView.vue、PluginStore.vue、workshop-forge.contrast.test.ts、e2e/global-setup.ts、eslint.config.mjs。

## 2. 验收标准对照（AC1–AC12）

| AC | 证据（来源等级：Verified） |
| --- | --- |
| AC1 | `GitHubAccountServiceTests.StartAsync_Success_ParsesDeviceCodeFields` 绿：device_code/user_code/verification_uri/expires_in/interval 五字段解析（fake handler 零外联） |
| AC2 | `StartAsync_Prefill_UsesQuestionMark_OnCleanUri` + `BuildVerificationUri_UsesAmpersand_WhenUriAlreadyHasQuery` + `_EscapesReservedCharacters` + `_EmptyUserCode_ReturnsBaseUnchanged` 四测绿 |
| AC3 | `StartAsync_NotConfigured_ThrowsWithClearReason` 绿（文案含 ClientId）；e2e `github-binding.spec.ts` 绑定入口用例断言 HTTP 400 + 错误文案含 ClientId |
| AC4 | `DescribeStartFailure_MergesErrorAndDescription`（「参数不合法 · bad_param」）/ `_TruncatesAt200Chars` / `_NonJsonBody_PassesThroughTrimmed` / `_EmptyBody_ReturnsFallback` / `_InvalidJson_FallsBackToBody` 五测绿 |
| AC5 | `PollAsync_AuthorizationPending_ReturnsPending_NeverThrows` / `_SlowDown_ReturnsSlowDown` / `_Expired_ReturnsTerminalExpiredWithReason` / `_AccessDenied_ReturnsTerminalDenied` / `_UnknownError_ReturnsFailedWithUpstreamReason` 五测绿（HTTP 400 不抛） |
| AC6 | `PollAsync_Success_BindsAccountAndStoresEncryptedToken` 绿（login=octocat）+ `GitHubBindingDtoTests.SaveToken_PersistsCipherAndAccount_NotPlaintext`（密文 v2: 前缀、不含明文）+ `Get_Projection_NeverContainsTokenMaterial`（JSON 投影无令牌）绿 |
| AC7 | `PluginGitHubProviderAuthTests.WithBoundToken_AttachesBearerHeader`（全请求 Bearer）+ `WithoutBinding_NoAuthorizationHeader` + `NullOrEmptyToken_NoAuthorizationHeader` 绿 |
| AC8 | `PluginHostToolCatalog` clone/pull 前缀接线（`GitAuthArgs()`，GetBearerToken 门控单测 `GetBearerToken_Gating_BoundAutoOnReturnsToken_OffReturnsNull` 绿）；extraheader 参数串按 04/05 记录为 Inferred（沙箱内无真实私有仓库可克隆验证，走查日补） |
| AC9 | `GithubBindPanel.test.ts` 14 测绿：准备态文案/防连点（结构性）/自动复制横幅/复制失败引导/3s 倒计时开预填页/手动复制/pending 轮询节奏/slow_down 退避/bound 发事件/expired+denied+failed 错误透传/start 失败透传；`GithubAccountPanel.test.ts` 7 测绿（解绑二次确认、开关回滚） |
| AC10 | `GetBearerToken_Gating...` 绿 + `GithubAccountPanel.test.ts` 开关两测（接口刷新/失败回滚）绿 |
| AC11 | `workshop-forge.contrast.test.ts` AC-11 用例绿（设备码块/绑定说明/次要说明 明暗 4 组合 ≥4.5）；EP 状态横幅暗色欠账已记 TODO P2 |
| AC12 | e2e `github-binding.spec.ts` 4 用例就绪（设置页面板/未配置分支透传/status 契约/DELETE 幂等）；本轮执行状态见 §3 |

## 3. 命令与输出（2026-11-23 实测，Verified）

- `dotnet test --filter FullyQualifiedName~GitHub` → **52/52 通过（失败 0）**。
- `pnpm exec vitest run`（github + settings panel + clipboard + contrast）→ **32/32 通过**（26 组件/工具 + 6 对比度）。
- `vue-tsc --noEmit -p tsconfig.app.json` → 本批文件零红；余红全部位于 `Plugins/DbAdmin/web`（全新 clone 未装插件 web 依赖的环境基线红，非本批改动面；已跑 `pnpm install` 修复中）。
- e2e：spec 与 global-setup 步骤 1.4 已就绪；本轮未整跑（深档全量 e2e 依赖 DbAdmin 等插件前端产物齐备，属环境修复后动作）——**状态：Pending（环境依赖），非跳过**。
- `Inferred`：AC8 extraheader 真实克隆（需私有仓库凭据）、AC12 e2e 整跑——两项列入「风险与未覆盖」，不冒充 Verified。

## 4. 基线红对表（非本批）

- `Plugins/DbAdmin/web/src/DbAdminView.vue`：TS2307（vue 模块解析）+ 隐式 any 数条——全新 clone 未装 `Plugins/*/web` 依赖（AGENTS §5.0 已知环境前置）；与 SmartTerminal `-b` 幻影行号同属环境性。
- 处置：已启动 DbAdmin web `pnpm install`；不影响本批过滤集绿。
