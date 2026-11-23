# Mini Task — GitHub OAuth App 内置 ClientId（O2）

> Task ID：PILOT-2026-11-22-github-oauth-app-embed｜轻量形态（mini-task 合并 Intent/Spec/Plan/Task）｜主任务：`2026-11-20-github-device-binding`（feature: 101）。

## Intent（为什么）

设备流需要 OAuth App ClientId；用户已创建公开应用（无 Secret 需求）。把 ClientId 内置进默认配置，用户开箱即用，同时允许 appsettings 覆盖（自带 App 的用户）。

## Spec（做什么·到什么程度）

- `ForgeSelf.Api/appsettings.json` 新增 `GitHubOAuth` 节：`ClientId: Ov23likdtDN2AlQvAO33`、`Scopes: repo workflow`。
- `GitHubOAuthConfig.FromConfiguration` 读该节；缺省/空 → IsConfigured=false（绑定入口报「未配置 ClientId」明确错误）。
- 守卫测试锚定默认值防丢（`GitHubOAuthDefaultsGuardTests` 4 测）。
- e2e 前置：global-setup 步骤 1.4 把 publish 宿主的 ClientId 置空（e2e 零外联，走未配置分支）。

## Interaction Design

**无界面交互**：本批纯配置层，无 UI 面。经 HTTP 暴露的行为 = `POST /api/github-account/bind/start` 在 ClientId 空时返回 HTTP 400 `{ success:false, error:"GitHub OAuth ClientId 未配置……" }`（含 ClientId 字样，e2e 锚定）；配置在 `ForgeSelf.Api/appsettings.json`，入参示例（覆盖自带 App）：`"GitHubOAuth": { "ClientId": "Ov23lixxxx", "Scopes": "repo" }`。

## Plan / Allowed

| 文件 | 动作 |
| --- | --- |
| `ForgeSelf.Api/appsettings.json` | +GitHubOAuth 节（5 行） |
| `ForgeSelf.Api/Services/GitHubOAuthConfig.cs` | FromConfiguration/IsConfigured（主任务同批新件） |
| `ForgeSelf.Api.Tests/Unit/GitHubOAuthDefaultsGuardTests.cs` | 新（4 测） |
| `ForgeSelf.Web/e2e/global-setup.ts` | +步骤 1.4（ClientId 置空） |

**Forbidden**：不改宿主 UI；不落 ClientSecret；不改其他配置节。

## 验证

`dotnet test --filter FullyQualifiedName~GitHubOAuthDefaultsGuard` → 4/4 绿；e2e spec 未配置分支用例（Pending 见主任务 05）。
