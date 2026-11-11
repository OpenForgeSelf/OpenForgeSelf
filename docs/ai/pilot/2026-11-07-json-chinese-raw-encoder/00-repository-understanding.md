# 00 Repository Understanding — 2026-11-07-json-chinese-raw-encoder

## 项目基线（实测确认）
- 技术栈：`ForgeSelf.Api`（.NET 10 + ASP.NET Core + NewLife.XCode，net10.0-windows）+ `ForgeSelf.Core` / `ForgeSelf.Abstractions`（共享层）+ `Plugins/*`（22 插件，net10.0，仅 ProjectReference Core+Abstractions）+ `ForgeSelf.Web`（Vue3，本轮不动）。
- 测试：`ForgeSelf.Api.Tests`（xUnit + FluentAssertions），仓库级守卫先例 `Architecture/ToolSchemaJsonGuardTests.cs`（源码枚举 + 阳性对照 + StripComments 规矩，见 plugin-development 坑 B）。
- 构建：`cd ForgeSelf.Api && dotnet build`（项目图覆盖全部插件，csproj ProjectReference 实测）；全量 `dotnet test`（中档，~15 min）。

## 问题现场（实测）
- System.Text.Json 默认 Encoder（`JavaScriptEncoder.Default`）把**所有非 ASCII**转义成 `\uXXXX`。
- 仓内静态 `*.json` 无 `\u` 残留（grep 实测 0 命中）⇒ 转义全部产生于**运行时序列化**。
- 生产 `JsonSerializer.Serialize(x)`（ options 单参）大量：宿主85 处 grep 命中（ToolRegistry 工具结果 / SessionEventJsonConverter 持久化 / ChatController SSE / UnifiedAI 三个协议控制器 / UsageStats / WorkflowEngine / McpCenter MCP JSON-RPC / MemorySystem / TodoTracker / DevTools JSON 格式化器（Utf8JsonWriter 无 Encoder）…）。
- 部分服务已用 `UnsafeRelaxedJsonEscaping`（SkillsService / ToolBridge / BrowserUse / DesignSystem.ReleaseService 等）⇒ 项目已有先例与共识，本批是把它变成**全仓不变量**。
- MVC 入口：`AppBuilder.cs:158 AddControllers()` 未配 JsonOptions ⇒ API 响应同样转义（浏览器 JSON.parse 后不可见，外部客户端/回显原文场景可见）。

## 用户可见症状
工具调用结果回显、MCP 客户端、AIAgent 执行记录、落盘配置文件中的中文显示为 `\u76EE\u5F55\u4E0A\u6EAF`（=「目录上溯」）形态。

 候选任务与选择理由- 选择：仓统一「写出路径不转义非 ASCII」（G2 类级守卫 + G1 单测 + 全量回归）。理由：用户直接指令、单点改不动（STJ 无全局默认覆盖钩子）、跨宿主+插件的同一根因。
- 不选：仅修个别服务（留死角，同类问题必复发）；改用 Newtonsoft（换库，违反铁律 8/4）。

## 适用问题类（tags 检索结论）
- 命中 lens：PC-023（双 JSON / 提取面）——本批只改转义行为、不改 JSON 结构与嵌套层数，`ExtractJsonPayload` 类裸正则提取面不受影响；不动 PC-023 守卫。
- 其余 PC 未命中（无鉴权/DB/进程/前端类触碰）。本次新立 **PC-027**（序列化默认转义非 ASCII）。
