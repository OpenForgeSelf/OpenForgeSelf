---
task_id: 2026-10-17-plugin-di-helper-todo-fix
feature: none              # 缺陷修复 + 架构门禁；主题回写 034-mcp-center.md 与 agent-workflow/README.md
risk: { level: L2, triggers: ["ForgeSelf.Abstractions 新增 public 扩展方法", "Plugins/TodoTracker 8 工具执行路径"], raised_by_agent: false }
gate1: { mode: user, at: 2026-10-17T(下午4), ref: "用户指令「全部都做」（批准①公共助手并入修复批②守卫测试）" }
gate2: { mode: user, at: null, decision: null }   # git 提交与发布④⑤待拍
gate3: { mode: agent, at: 2026-10-17T(下午5), decision: APPROVED }  # 06-review 全链自审通过
expected_files:
  - ForgeSelf.Abstractions/PluginContextDiExtensions.cs
  - Plugins/TodoTracker/ToolExtensions.cs
  - Plugins/TodoTracker/AgentToolFunctions.cs
  - Plugins/TodoTracker/TodoTrackerPlugin.cs
  - ForgeSelf.Api.Tests/PluginContextDiExtensionsTests.cs
  - ForgeSelf.Api.Tests/Plugins/TodoTracker/UniversalGatewayListTodosScopeRegressionTests.cs
  - ForgeSelf.Api.Tests/Architecture/PluginToolConstructorDiGuardTests.cs
rollback: "git 未提交批次：直接还原工作区（复现批测试文件自 .trash/ 取回即可恢复特征化状态）"
writeback: { feature_doc: done, reason: "034 网关故障段 + agent-workflow §B2/§B4 新规则已回写（2026-10-17）；另新增 plugin-development 铁律 26" }
---

# Task（Agent 工作单元）

> 阶段：Stage 4 ｜ PILOT-062

## Objective

插件工具「解析宿主 DI」收敛为单一公共入口且只能执行期调用；TodoTracker 8 工具时序免疫（seed 前构造、seed 后调用成功）；架构守卫把构造期解析写法变成 CI 红。

## Scope

### Allowed

- 03-plan「Files To Change」全部所列文件
- `.trash/` 归档被删测试文件（TodoToolProviderTests.cs、旧复现测试文件）

### Forbidden

- McpCenter / AgentHub / 其他插件代码（守卫首跑命中他插件时只登记不顺手修，§1.3）
- 下午3 stateless 批文件（McpGatewayConfig/网关控制器/web）——并行会话在改，避免混批
- 盘符大小写基线红 2 条在册测试；DB 结构；MCP 对外契约（工具名/schema/封套形状）
- 停/启/杀用户宿主；发布（④⑤与 stateless 批协调，待用户拍板）；git 提交（待闸门2）

## Acceptance Criteria

- [x] AC-1 网关回归测试绿：seed 前构造工具 + 调用返回 `success:true`（fake ITodoService 数据）
- [x] AC-2 助手单测绿：三分支 + 未 seed 明确文案（含「ProvideHostServices」「ExecuteAsync」字样）+ null 输入
- [x] AC-3 守卫测试绿 + 变异探针翻红实证（记录后还原，探针不进提交）
- [x] AC-4 `dotnet build ForgeSelf.Api` 0 error；相邻回归除 2 条在册基线红全绿（锁下绕行 `-p:OutputPath=bin\BuildCheck\` 已标注；本次相邻 filter 158/158 零命中基线红）
- [x] AC-5 dev-stack 源码本地验收：真实宿主进程内经 MCP 网关调 list_todos 返回成功封套（响应原文存证；dev-stack 因 Debug 锁改用 e2e 全新产物等效替代，偏差理由见 05）
- [x] AC-6 冒烟 e2e：todo-tracker 既有冒烟 1 条跑绿（A1，1 passed 43.5s）
- [x] AC-7 文档回写：034 + agent-workflow/README.md + TODO + 日记 + 05/06/07 工件齐备（另新增 plugin-development 铁律 26）

## Expected Files

见 frontmatter `expected_files`（新增 3 / 修改 4 / 移入 .trash 2）。

## Verification Commands

```powershell
dotnet build ForgeSelf.Api
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~PluginContextDi|FullyQualifiedName~UniversalGatewayListTodos|FullyQualifiedName~PluginToolConstructorDiGuard"
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TodoTracker|FullyQualifiedName~UniversalToolForwarder"
pwsh scripts/dev-stack.ps1   # 验收后 -Stop
```
