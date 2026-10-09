# 05-evidence — todo 委派终态自动回写 + 详情回显（2026-10-09）

## Changed Files（3 + 发布伴随）
| 文件 | 改动 |
| --- | --- |
| `Plugins/TodoTracker/Services/TodoDispatchService.cs` | 新增静态 `AutoAdvanceLocks`（todoId 粒度）；新增 `TryAutoAdvanceAsync`（终态 Succeeded 且 Stage==Running → 推进 Review + 补 CompletedAt/UpdatedAt + UpdateAsync）+ `AppendAdvanceRecordAsync`（锁外 await 留痕「agent 执行回写：{Status}」，actor=agent 名）；`ReadStatusAsync` 外部分支与 `ReadBuiltInStatusAsync` 成功路径均接入自动推进 |
| `Plugins/TodoTracker/web/src/components/TaskDetail.vue` | 新增 `syncDelegation(t)`：按 `task.agentEngine`/`task.agentId` 回填引擎/角色/agent 下拉（初始化 + watch id）；`doDelegate` 成功后回填 `form.assignee=result.agentName` 并 commit（点即保存）；「下发对象」input placeholder 在有委派时显示 `agentDisplayName` |
| `ForgeSelf.Api.Tests/Plugins/TodoTracker/TodoBuiltInDispatchTests.cs` | 新增 3 用例：成功终态自动推进待验收并留痕 / 自动推进幂等不重复写 / 失败终态不推进保持由人判 |
| `Plugins/TodoTracker/plugin.json` | Version 1.1.2 → 1.1.3 |

## Validation（Verified，全部亲自跑过）
| 项 | 命令 | 结果 |
| --- | --- | --- |
| 后端编译 | `dotnet build ForgeSelf.Api.csproj` | ✅ 0 错误 |
| 后端单测 | `dotnet test --filter FullyQualifiedName~TodoBuiltInDispatchTests` | ✅ 7/7（含新增 3） |
| 插件前端构建 | `cd Plugins/TodoTracker/web && pnpm run build` | ✅ vue-tsc + vite 通过（两次） |
| 插件 e2e | `e2e/plugins/todo-tracker --workers=1` | ✅ 16/16（两次，改前后各一次） |
| 发布 | `release-local.ps1 -Version 2.3.9 -Sign -UpdateDir updates` | ✅ ALL DONE，签名 3/3 校验通过，包 `OpenForgeSelf-2.3.9.2610092019-win-x64.zip` |
| 运行实例升级 | 页面「检查更新→下载→重启并更新」+ 确认弹窗自点 | ✅ current → 2.3.9.2610092019，进程 20:23:51 自新版本目录启动 |
| 运行实例复验（只读） | Playwright 直连 :51888 + token 注入 | ✅ 见下 |

## 运行实例复验（AC6 逐条）
1. **任务 50 状态自动翻转**：列表徽章由「执行中」→「**待验收**」（升级后首次读取即自动推进，未点任何按钮）；委派徽标「已成功·程序员 #run:6」，记录 7（原 6 + 自动回写 1）。
2. **自动回写记录落库并显示**：执行记录 #7 = 「agent 执行回写：Succeeded / 程序员 / Running → Review」。
3. **详情回显（用户输入9 三处痛点）**：
   - 委派状态行：「本工具AI agent 程序员 / 已成功 / Succeeded」；
   - 角色下拉：**程序员**（不再恒为「默认（程序员）」占位）；引擎下拉：本工具 AI Agent；
   - 下发对象 input：placeholder=**程序员**（旧任务兜底回显，详情第一眼可见）。
4. **截图存档（3 张，已读图核对）**：`ForgeSelf.Web/screenshots/live-51888/`：
   - `todo-2.3.9-状态自动翻转+详情回显.png`（列表：待验收 + 记录7 + v1.1.3）
   - `todo-2.3.9-详情委派区回显.png`（委派区 + 角色下拉 + 回写记录 #7）
   - `todo-2.3.9b-下发对象回显.png`（下发对象=程序员）

## Known Limitations / Unresolved
- 失败/取消终态仍不自动推进（由人判语义，未改）；列表失败任务如停在「执行中」需人工点「记为执行记录」或手动流转——与设计一致，非缺陷。
- 全量后端测试未重跑（改动仅在 todo-tracker 插件服务 + 测试文件，快档过滤集 + 插件 e2e 覆盖；全量基线 13 红与本批零交集）。
- 本轮未 git 提交（用户未授权提交操作）。
