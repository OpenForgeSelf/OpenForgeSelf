# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式｜载体：markdown 正文直发群消息（规范 §5）。
> 状态只报事实，禁止模糊表述（对齐 AGENTS.md §10.4/§10.5）。

## 1. Repository Understanding

我确认了：seed 时序缺口实锤于 `AppBuilder.cs`（L364 RegisterAllServices 触发插件 Apply → L402 才 ProvideHostServices）；`IContext : IServiceProvider` 只查本地值+root 共享表；AgentHubDi 已是惰性先例；`RecordUsageAsync` 内部 CreateScope+全吞；守卫三规矩（剥注释/指针推进/探针）有既有先例可抄。

## 2. Selected Task

PILOT-062：通用网关调 `list_todos` 报 `IServiceScopeFactory` 未注册（复现批 2026-10-17-gateway-list-todos-scope-repro 定根因）→ 本批按用户指令「全部都做」执行修复 + 公共 DI 助手 + 架构守卫，杜绝同类问题。

## 3. Changed Files

新增 3（`ForgeSelf.Abstractions/PluginContextDiExtensions.cs` + 助手单测 + 网关成功回归）/ 修改 4（TodoTracker 三文件 + 守卫测试新增计 8 件）/ 移入 `.trash/` 2（旧 TodoToolProviderTests、旧复现测试）。明细见 05-evidence「Changed Files」。

## 4. Validation

Build: `dotnet build ForgeSelf.Api -p:OutputPath=bin\BuildCheck\` → **0 error**（用户 dev 宿主锁 Debug 目录，绕锁先例，如实标注）。
Unit Test: 定向 **7/7** 绿 + 相邻 **158/158** 绿；守卫变异探针**红→绿**往返实证。
E2E: todo-tracker 冒烟 A1 **1 passed (43.5s)**（globalSetup 全新宿主零 mock）。
现场: 真实宿主进程经 MCP 网关真调 `list_todos`/`create_todo` → 全部 `success:true` 成功封套，缺陷错误串不再出现；测试数据已清理、自起实例已停止。

## 5. Evidence

`05-evidence.md`——全部命令与输出原文、来源等级 Verified；dev-stack 等效替代的偏差理由（脚本内部固定 Debug build 必撞锁）与进程隔离细节均已实录。

## 6. Review

`06-review.md` Final Decision：**APPROVED**（Critical/Major 零；Minor 2 条备查）。

## 7. Risk

L2 → 已消解：改动原子化、对外契约零变化、守卫把缺陷写法变成 CI 判据。

## 8. Problems Found

- 用户运行中的 dev 宿主锁死 Debug 产物目录：本批全程（build/test/现场）都需绕行——这是共享开发机上「验证 vs 用户实例」的结构性摩擦；
- 首次现场验证误打了用户实例的网关端口（18895 被用户 dev 宿主占用而不自知），靠核对监听 PID 归属才识破——把「验收前先核端口归属」变成规则。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS（首跑两条编译二义如实记录并当场纠正，未掩盖） |
| Test → Evidence | PASS（含失败记录与等效替代偏差标注） |
| Evidence → Review | PASS |

## 10. 最重要的问题

**多实例并存时"打到谁的端口"不可想当然**：现场验收第一次的 initialize 响应其实来自用户 dev 宿主（有状态旧网关），若未核对 OwningProcess 就会把用户实例的报错当成本批验证结论。规则已拟入 agent-workflow Part B：起验前用 `Get-NetTCPConnection` 核对目标端口 PID 归属自己拉起的实例。

## 11. 下一步建议

单一实验：把守卫的 ForbiddenResolutionTokens 扩到「控制器/服务构造含 IContext 参数」一层（覆盖非工具类插件服务），在 CI 上跑一轮观察存量违规量，再决定是否收紧。
