---
feature: im-gateway-chat-list
risk: { level: L1, triggers: ["Plugins/ImGateway/**"], raised_by_agent: false }
gate1: { mode: user, at: "2026-10-30 22:2x", ref: "TODO 协作区·输入N+1 用户指令" }
expected_files:
  - Plugins/ImGateway/DataModel.xml
  - Plugins/ImGateway/Data/Entities/
  - PluginsImGateway/Services/
  - Plugins/ImGateway/Controllers/
  - Plugins/ImGateway/Core/
  - Plugins/ImGateway/web/src/
  - Plugins/ImGateway/plugin.json
  - ForgeSelf.Api.Tests/Plugins/ImGatewayTests/
  - ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts
rollback: git revert 本批提交（单插件批，ImGateway 2.1.1 → 2.0.x 前版本可回退）
---

# Task

> Task ID：PILOT-078｜团队：lead（文档/审/验收）+ im-dev（实现）+ im-test（测试）

## Allowed（写范围白名单）

- **im-dev**：`Plugins/ImGateway/Data/Model.xml`、`Plugins/ImGateway/Data/Entities/**`（xcode 生成 + .Biz.cs）、`Plugins/ImGateway/Data/ImGatewayTables.cs`（如需登记）、`Plugins/ImGateway/Services/**`、`Plugins/ImGateway/Controllers/**`、`Plugins/ImGateway/Core/**`（如需 DTO）、`Plugins/ImGateway/web/src/**`、`Plugins/ImGateway/plugin.json`
- **im-test**：`ForgeSelf.Api.Tests/Plugins/ImGatewayTests/**`、`ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts`、`.temp/tmp/**`（取证日志）
- **lead**：`docs/ai/pilot/2026-10-30-imgateway-chat-list/**`、`Plugins/ImGateway/README.md`、`Plugins/ImGateway/docs/**`、`ForgeSelf.Web/screenshots/dev-stack/**`、TODO.md、`.forgeself/memory/**`
- dev-stack 停/启：仅 lead（TODO 协作区登记）

## Forbidden

- ❌ 修改 `Plugins/AIAgent/**`（404 修复批未提交，勿踩）、`ForgeSelf.Api/**` 宿主源码树、`docs/04-standards/**`、其他插件
- ❌ 手改 `Data/Entities/*.cs` 生成件（铁律 9：只改 Model.xml 再 xcode）
- ❌ 前端 `<ElXxx>` / import 宿主模块 / 裸色值（铁律 4 + DESIGN.md 三禁）
- ❌ 引入删除/修改消息的破坏性端点；前端不做主动发消息
- ❌ 未经 lead 在协作区登记，任何成员停/启 dev-stack
- ❌ git 操作（闸门3 等用户）

## 验证命令（成员各自门禁）

```bash
# dev（后端）
cd ForgeSelf.Api && dotnet build
# dev（前端）
cd Plugins/ImGateway/web && pnpm install && pnpm run build
# test
dotnet test --filter "FullyQualifiedName~ImGatewayTests"
cd ForgeSelf.Web && pnpm run test:e2e -- e2e/plugins/im-gateway/im-gateway.spec.ts
```

## 完成定义（DoD）

- 02 spec AC-B1..B12 全绿；插件六步闭环 ①②③ 完成（④⑤⑥ 属发布后动作，须用户同意发布后进行）
- 05-evidence（Verified 分级）+ 06-review（八问+Decision）齐；闸门2 交用户
