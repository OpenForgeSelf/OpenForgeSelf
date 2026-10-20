# Intent

> 阶段：Stage 1 ｜ 任务：2026-10-17-plugin-di-helper-todo-fix ｜ 闸门1 = 用户 2026-10-17「全部都做」（批准上轮两项待拍：①公共 DI 助手并入修复批 ②守卫测试本批加）

## Problem

经 McpCenter 通用网关调用 `list_todos` 返回 `{success:false, error:"No service for type 'IServiceScopeFactory' has been registered."}`（复现批已证：`docs/ai/pilot/2026-10-17-gateway-list-todos-scope-repro/`）。根因：TodoTracker 全部 8 个工具在插件 Apply 构造期 eager 解析宿主 provider，而宿主根 `IServiceProvider` 要到 Build 之后的 `ProvideHostServices` 才 seed——构造时刻恒解析不到，把插件 Context 本体存进字段，请求期 `CreateScope()` 抛费错。

## Why

1. 同类缺陷第三次出现（sems、AgentHub PILOT-055、本次 TodoTracker）——每次都是「插件工具构造期捕获服务」这一个模式，用户明确要求**写公共方法统一这一个动作，只管调用，杜绝问题**。
2. 错误文案（IServiceScopeFactory）离真因（seed 时序）极远，排障成本高；公共助手应把「未就绪」变成指名道姓的明确报错。
3. 无架构门禁——同类写法还会在任意新插件重演。

## Expected Outcome

- 插件侧「取宿主容器/建宿主作用域」收敛为一个公共入口（ForgeSelf.Abstractions），只能在**执行期**调用，未 seed 时报明确错误（说明 seed 时机与正确用法）。
- TodoTracker 8 个工具全部改惰性：构造只存原始上下文，执行期才解析；网关调 list_todos 返回 `success:true`。
- 一条架构守卫测试扫描 `Plugins/**`，任何「构造函数体内解析服务」的新写法直接翻红。

## Constraints

- 只修这一类问题：不动 McpCenter（其工具本就惰性 `ctx.Get<T>()`）、不碰下午3 stateless 批文件、不顺手修盘符基线红（§1.3）。
- Abstractions 依赖不变（仅 Core + DI.Abstractions，禁止引入 NewLife/XTrace——ContextExtensions.cs 同款约束）。
- 工具对外封套形状不变（`{success,data}` / `{success:false,error}`），MCP 契约零变化。
- 禁停/启/杀用户宿主；发布与 stateless 批协调、先报用户拍板（日记下午4 拆解⑤）。

## Success Criteria

- AC-1 `dotnet test`：翻转后的网关回归测试绿（`success:true`，fake ITodoService 数据回传）。
- AC-2 新助手单测绿：seed 前构造 + seed 后调用成功（时序无关）；未 seed 调用 → 明确错误文案（含 ProvideHostServices 指引）。
- AC-3 守卫测试绿且**变异探针**证实：在任一工具构造函数塞回 `.Resolve(`/`GetService(` 即翻红，探针用完即还原。
- AC-4 相邻域不回归：TodoTracker+UniversalToolForwarder 过滤集除 2 条在册基线红外全绿；`dotnet build` 0 error。
- AC-5 dev-stack 源码本地验收：经网关真实调用 `list_todos` 返回成功封套（截图/响应存证）。
