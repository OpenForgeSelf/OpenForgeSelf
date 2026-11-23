---
task: 2026-11-22-github-oauth-app-embed
task_kind: feature
feature: 101
risk: { level: L1, triggers: [默认配置内置第三方应用标识（公开 App 无 Secret，覆盖路径保留）], raised_by_agent: false }
expected_files: [ForgeSelf.Api/appsettings.json, ForgeSelf.Api/Services/GitHubOAuthConfig.cs, ForgeSelf.Api.Tests/Unit/GitHubOAuthDefaultsGuardTests.cs, ForgeSelf.Web/e2e/global-setup.ts]
rollback: revert 本提交即回滚（appsettings 节与守卫测试随提交移除，零数据迁移）
---

# Agent Task

## Scope

### Allowed
见 mini-task.md Plan 表（4 文件）。

### Forbidden
不落 ClientSecret；不改宿主 UI 与其他配置节；测试不外联 github.com。

## 验证
`dotnet test --filter FullyQualifiedName~GitHubOAuthDefaultsGuard`；`verify-pilot-feature-writeback`（feature: 101）。
