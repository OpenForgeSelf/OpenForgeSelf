# Mini Task — 浏览器走查缺陷批：记忆时间戳缺失 + Home 包旧名

> 阶段：Stage 1-4 合并（轻量）｜Task ID：`2026-11-02-maf-walkthrough-fixes`｜日期：2026-11-02
> 来源：用户指令「工作去修改了aiaagent插件，你去验一下：你控制浏览器打开页面，将你做了的功能，逐个点击验证，截图作为证据，不对就修改，改到正确再截图」

## Intent（为什么/做什么）

浏览器逐功能走查 2026-11-02 maf-first-class 批（智能体命名/审批/技能/记忆/本地模型/浏览器插件/调度 AgentRun）时发现 2 个缺陷：

1. **D1 记忆时间戳缺失**：新建/导入记忆 `createdAt=updatedAt=0001-01-01`（MemoryServiceXCode 两处 `new Memory` 均未设时间戳；UpdateAsync/DeleteAsync 也未维护 UpdatedAt）。用户可见症状：记忆页创建时间显示「1/01/01 00:00」。
2. **D2 Home 插件包旧名「AI Agent」**：宿主侧 features.ts 已改「智能体」，但 `Plugins/Home/web/src/features.ts:54` 与 `HomeView.vue:147` 仍是「AI Agent」（首页常用功能格 + 核心看板卡片）——PC-021 同类残留（该 case 已预留 TODO，走查命中即修）。

## 适用问题类

- PC-021（agent-entry-blank-or-old-name）：D2 命中同类（宿主外包内可见文案残留），执行同一 canonical fix（展示名 → 智能体）。
- 无其它命中（PC 台账 2026-11-02 版检索：无「时间戳缺失」既有类；本次新暴露，建 case 归档见下）。

## Spec（最小）

- D1：`MemoryServiceXCode.CreateAsync` / `ImportAsync` 插入时设 `CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now`；`UpdateAsync` / `DeleteAsync` 在 `Update()` 前设 `entity.UpdatedAt = DateTime.Now`（与 CorrectAsync:164 既有约定一致）。
- D2：Home 包两处展示名改「智能体」；IM 网关描述里「AI Agent」一并改「智能体」；不改路由/Id。
- 边界：不动 TodoTracker 后端留痕文案（含测试耦合，记 TODO）；不改 :51888 宿主。

## Plan（file: reason）

- `ForgeSelf.Api.Tests/Integration/MemoryTimestampGuardTests.cs`（新）— TDD 守卫：create/import 时间戳非 MinValue；update/delete 后 UpdatedAt 前进。先红后绿。
- `Plugins/MemorySystem/Services/MemoryServiceXCode.cs:82-99` — CreateAsync 补时间戳。
- `Plugins/MemorySystem/Services/MemoryServiceXCode.cs:408-425` — ImportAsync 补时间戳。
- `Plugins/MemorySystem/Services/MemoryServiceXCode.cs:120-131` — UpdateAsync 补 UpdatedAt。
- `Plugins/MemorySystem/Services/MemoryServiceXCode.cs:169-183` — DeleteAsync 补 UpdatedAt。
- `Plugins/Home/web/src/features.ts:54,382` — 展示名/描述改「智能体」。
- `Plugins/Home/web/src/HomeView.vue:147` — 看板标题改「智能体」。
- `Plugins/Home/web` — `pnpm build` 重建 dist。

## Verification Commands

```
dotnet test --filter MemoryTimestampGuardTests        # 红灯 → 修复 → 绿灯
dotnet test --filter "MemorySystem"                   # 插件层回归
cd Plugins/Home/web && pnpm run build                 # Home dist 重建
浏览器复验：/home 首页命名截图 + /memory 新建记忆时间戳截图
```

## 问题类归档

- 新 case：`docs/11-troubleshooting/cases/2026-11-02-memory-timestamp-minvalue.md`（走查发现 → TDD 守卫 → 修复）。
