# Repository Understanding

> Task ID：`2026-11-19-oar-runtime-gap-close`

## 要点（来自仓库实读）

- 审计 `/opt/cursor/artifacts/oar-delivery-audit.md` @ `a9f7bfe`：Overall **PARTIAL**。P0–P4 主路径代码+过滤测近 FULL；缺口集中在：
  1. §7.2 双层事件：`AgentHubSession` 已有 `Generation`/`AgentHubSequence`，**无** `OarSequence`；**无** `AgentRuntimeRecord` 表；任务层仍用 `DelegationEvent.Seq`。
  2. 架构 §9 示意 API：`runtimes/{id}/probe|models`、`sessions/.../abort|records|stream` **未实现**（P3 Spec FR5 只写了最小面；设计 FR-R5 / 架构表仍要求）。
  3. 插件 e2e：`e2e/plugins/agent-hub/agent-hub.spec.ts` 未覆盖 Session 页签主链路。
- Worker 协议已具备 `runtime.installation|auth|models`、`session.control|records`（`RuntimeWorker/src/handlers.ts` + smoke 12 步）；`OarWorkerClient` 尚未封装这些 RPC。
- 控制器风格：`AgentHubRuntimesController` / `AgentHubSessionsController` 无类级 `ApiKeyPolicy`（032 §5 待核对项）；Settings 已带。
- Linux WindowStyle 属 PR#7，本批不碰 `dev-stack.ps1`。

## 测试 / 构建入口

```bash
dotnet build Plugins/AgentHub/AgentHub.csproj -p:EnableWindowsTargeting=true
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -p:EnableWindowsTargeting=true \
  --filter "FullyQualifiedName~AgentHub|FullyQualifiedName~OarWorker|FullyQualifiedName~AgentRuntime|FullyQualifiedName~TransportRouting"
cd Plugins/AgentHub/web && pnpm run build
# 冒烟 e2e（NO_PROXY/TEMP 按 AGENTS §5.0）
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/agent-hub
```

## 适用问题类

无（补齐交付缺口，非缺陷复发类）。
