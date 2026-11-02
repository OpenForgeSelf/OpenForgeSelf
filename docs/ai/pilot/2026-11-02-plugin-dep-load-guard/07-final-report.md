# AI-Native Pilot Result（最终汇报）

## 1. Repository Understanding

插件是类库，由独立加载上下文从自己的目录加载。宿主契约进 `IContext`，不进插件子容器。控制器由 `PluginAwareControllerActivator` 从插件子容器激活。

## 2. Selected Task

把「独有 NuGet 必须复制」和「控制器不得构造注入宿主契约」做成 G2 测试，并改掉扫到的两处构造函数。

## 3. Changed Files

见 `05-evidence.md`。

## 4. Validation

Build: 测试工程编译通过（Verified，红绿两次 `dotnet test` 都完成了编译）。

Unit Test: 红灯 1 失败；修复后守卫+成本端点 30 通过；恢复开关后守卫 与当时的台账测试 7 通过。

E2E: 未跑。本任务没有页面。

## 5. Evidence

`docs/ai/pilot/2026-11-02-plugin-dep-load-guard/05-evidence.md`

## 6. Review

`06-review.md` Final Decision：APPROVED（仅本守卫）。

## 7. Risk

L1。发布 zip 里的传递 DLL 仍靠解包看，测试不打开 zip。

## 8. Problems Found

智能体功能批的交互验收没有做。这不是本守卫的遗漏，记在当天日记，不另开一条没有编号的建议。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

### 9.5 本批自复盘

- **经验提取**：先拿掉复制开关跑红，再写回，红灯日志和绿灯日志是两份文件。同因构造函数用同一守卫扫全仓，不手点文件。
- **待改进**：10 月事故只进了 TODO。入口技能和失败测试要同一回合落地，否则下一次新 NuGet 还会让宿主起不来。这一点已由 PC-020 的第 6 问写明。

## 10. 问题类归档

PC-020（新建）。case：`docs/11-troubleshooting/cases/2026-10-09-mcpcenter-nuget-missing.md`、`docs/11-troubleshooting/cases/2026-11-02-browseruse-and-approval-di.md`。

## 11. 重构提案

R0。枚举测试能表达这两条不变量，不改插件加载器。

## 12. 悬空教训清单（必须为空）

（空）
