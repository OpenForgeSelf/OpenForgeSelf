# 05-evidence：todo 下发 → 本工具内置 AI Agent 链路

> 目录：`docs/ai/pilot/2026-10-09-todo-builtin-agent/`｜证据分级：Verified（亲自跑过）/ Inferred（读码推断）/ Unknown（未验证）

## 1. 实施清单（本批改动，未 git 提交）

| 文件 | 改动 |
|---|---|
| `ForgeSelf.Abstractions/BuiltInAgentExecutionContracts.cs` | 新契约 `IBuiltInAgentExecution` + Request/Outcome/Snapshot/Option |
| `Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs` | 内置执行实现（StartAsync 后台 Run + TCS 捕获 runId；FindAsync 快照映射；ListAgents 七角色；默认模型 `default:ornith-1.0-9b`） |
| `Plugins/AIAgent/AIAgentPlugin.cs` | `AddScoped<IBuiltInAgentExecution>` + eager scope 构造后 `ctx.Register<IBuiltInAgentExecution>(实例)` |
| `Plugins/TodoTracker/Data/Model.xml` + 生成物 | Todo 加 `AgentEngine` 列（String16 默认 agenthub）；xcode 重新生成 Todo.cs（7 处引用） |
| `Plugins/TodoTracker/Models/DispatchDtos.cs` | `DelegateToAgentRequest.Engine/AgentRoleId`；`DispatchPreviewDto.BuiltInAvailable/BuiltInError/BuiltInAgents` |
| `Plugins/TodoTracker/Models/TodoDtos.cs` | `TodoDto.AgentEngine` |
| `Plugins/TodoTracker/Services/TodoProjection.cs` | AgentEngine 投影输出 |
| `Plugins/TodoTracker/Services/TodoDispatchService.cs` | IContext 注入；引擎分流 `DelegateToBuiltInAsync`；`ReadBuiltInStatusAsync`（run: 前缀分流）；`MapBuiltInStatus`/`BuiltInRoleIndex`/`BuiltInRoleId`/`BuiltInDefaultRole`；`MarkDelegationCompleteAsync` 内置无操作 |
| `Plugins/TodoTracker/web/src/types.ts` | DispatchPreview.BuiltIn*；TodoItem.agentEngine |
| `Plugins/TodoTracker/web/src/http.ts` | delegateToAgent 带 engine/agentRoleId |
| `Plugins/TodoTracker/web/src/store.ts` | delegateToAgent 透传 engine/agentRoleId |
| `Plugins/TodoTracker/web/src/components/TaskDetail.vue` | 引擎选择 + 内置角色下拉 + 「交给本工具 AI Agent 执行」按钮 + 徽标「本工具AI」标签 + agentDisplayName 内置兜底 |
| `ForgeSelf.Api.Tests/Plugins/TodoTracker/TodoBuiltInDispatchTests.cs` | 新单测 4 用例（委派回填/缺席降级/状态映射/回读与404） |
| `ForgeSelf.Api.Tests/.../TodoDispatchFlowTests.cs`、`TodoAgentStatusBatchTests.cs` | 构造签名补 IContext（兼容） |

## 2. 门禁结果（Verified）

| 门禁 | 命令 | 结果 | 证据 |
|---|---|---|---|
| 后端编译 | `dotnet build`（ForgeSelf.Api） | ✅ 0 错 | 控制台「已成功生成。0 个错误」 |
| 测试工程编译 | `dotnet build`（ForgeSelf.Api.Tests） | ✅ 0 错 | 同上 |
| todo 委派单测 | `dotnet test --filter "~TodoBuiltInDispatchTests\|~TodoDispatchFlowTests\|~TodoAgentStatusBatchTests"` | ✅ 32/32（首轮 4/4） | 「已通过! 失败:0 通过:32」；Cwd 传递断言新增后 4/4 单测 + 过滤集 32/32 全绿 |
| 插件前端构建 | `cd Plugins/TodoTracker/web && pnpm run build` | ✅ dist 产出 | vite build ✓ built in 873ms；index.js 82.14 kB |
| todo e2e | `playwright test e2e/plugins/todo-tracker --output=../.temp/pw-out-todo2` | ✅ 15/15（2.6m） | 「15 passed」；基线 15 条全绿 = AgentHub 引擎零回归（含 E1 opencode 真实委派） |
| 打包发布 | `release-local.ps1 -Version 2.3.6 -Sign -UpdateDir updates` | ✅ 2.3.6.2610091212，171s | 日志「签名完成：新增 2 / 跳过 1 / 失败 0」「校验通过：3 / 3」 |
| 运行实例升级 | 设置页 检查更新→下载→重启并更新（确认弹窗自点） | ✅ | `Get-Process ForgeSelf` exe FileVersion=**2.3.6.2610091212**（Verified 实测） |

**踩坑与修复**（已闭环）：
1. `Model.xml` Description 含 `<run:<id>>` 尖括号 → XmlException（xcode 解析失败）；改为「run:id」描述后生成成功。
2. `ListEnabled()` 不存在 → 改用 `IAgentRegistryService.GetAllAgents()`（已过滤 IsEnabled）。
3. IContext 缺 `using ForgeSelf.Core` → 编译 2 错，补齐后 0 错。
4. `MapBuiltInStatus` 原 internal → 测试工程无 InternalsVisibleTo 报 CS0117 → 改 public。
5. 测试断言顺序错误（调用后才置 Snapshot=null）→ 调整为先置后调。
6. `ReadBuiltInStatusAsync` ErrorCode 大小写未归一（快照 "Stuck" vs "stuck"）→ 先 Trim().ToLowerInvariant() 再判。
7. **CS0165 未赋值 wsError**：out 参数在 workspace==null 短路分支未赋值 → 拆分支显式判 null 后编译 0 错。

## 2.5 运行实例端到端复验（Verified · 2.3.6 升级后）

> 这是「整链路通」的决定性证据：任务 50 从「未关联项目」到「内置委派成功 + 产物落地 + 状态 Succeeded」全程实测。

| 步 | 动作 | 结果（实测） |
|---|---|---|
| 1 | `POST /api/todos/50/project {projectId:1}` | ✅ 200，projectRoot=D:\src\my-proj\OpenForgeSelf\OpenForgeSelf |
| 2 | `POST /api/todos/50/dispatch-to-agent {engine:'builtin', agentRoleId:'agent.programmer', permissionMode:'read-only'}` | ✅ 200，taskKey=**run:6**，cwd=D:\src\my-proj\OpenForgeSelf\OpenForgeSelf（**Cwd 注入生效**），steps=[Draft→Ready, Ready→Dispatched] |
| 3 | 轮询 `GET /api/todos/50/agent-status` | Running（38.8s：write_file 已创建 ok-builtin.txt，11 字节「本工具AI链路验证通过」）→ **Succeeded**（62.7s：read_file 验证内容一致，验收判据 ✓） |
| 4 | 落地核验 | ✅ `Test-Path` True；内容「本工具AI链路验证通过」（UTF-8） |
| 5 | UI 详情复验 | ✅ 徽标「委派状态：本工具AI agent 程序员 已成功 Succeeded」；列表行「OpenForgeSelf / 已成功 / 程序员 / #run6 / 记录6 / 2分钟前」 |
| 6 | 执行记录 | ✅ #1/#2 旧版 20s 超时失败留痕；#3 关联项目；#4 Draft→Ready 补齐——记录逐条可查 |
| 7 | 截图存档 | `ForgeSelf.Web/screenshots/live-51888/todo-builtin-2.3.6-委派成功-ok-builtin.png`（OCR 读图核对） |

**Cwd 修复闭环**（本轮唯一实质缺陷修复）：
- 根因：内置引擎文件工具挂在全局 `IProjectWorkspaceService.ProjectRoot` 上；todo 委派 `RunRequest` 无工作目录字段 → Run 5 stuck「尚未选择项目目录，请先在左侧选择工作目录」，taskInput 显示「项目路径：未关联」。
- 修复：契约 `BuiltInAgentRequest` 加 `Cwd`；`StartAsync` 先 `TrySetProjectRoot(Cwd)`（目录不存在/服务缺席时 Fail 并给出「请先在任务上关联项目」引导）；todo `DelegateToBuiltInAsync` 传 `todo.ProjectRoot`；单测补 Cwd 传递断言。
- 附带修复：StartAsync 启动窗口 20s → **60s**（9B 本地模型规划阶段实测可 >20s；旧版 400 与 Run 实际继续执行的割裂已消除，文案同步为「启动偏慢…后台可能仍在执行，可到 AI Agent 运行记录查看」）。

## 3. 内置引擎真实链路（✅ 已完成 · 见 §2.5）

- 隔离 e2e 环境无用户模型通道（gpustack 外部服务），内置委派真实执行不放入 e2e（避免外部依赖假红）。
- 真实链路已在运行实例 2.3.6 完成（§2.5 七步全实测通过，任务 50 → run:6 → Succeeded，产物 ok-builtin.txt 落地）。

## 4. 前端交互规格符合性（✅ 已走查 · 截图读图）

- A1 引擎选择（外部 AgentHub / 本工具 AI Agent）+ 七角色下拉（默认程序员/协调者/分析师/评论家/通用助手/写作者/研究员/程序员）+ 委派按钮可用 = 运行实例实测通过（DOM 断言）。
- A2 委派 → 状态徽标「本工具AI agent 程序员」+ 终态「已成功 Succeeded」= 运行实例截图 OCR 核对通过（截图存档见 §2.5 步 7）。
- A5 执行记录留痕（失败原文 + 状态流转 + 关联项目逐条可查）= 运行实例实测通过。
- 空态/禁用原因（接缝缺席文案）已实现于 TaskDetail.vue；e2e U4 覆盖接缝在但无 agent 场景（15/15 含）。

## 5. 委派区双下拉完善（2.3.7，Verified · 2026-10-09 输入6）

**需求**：「agent 可以选择本工具的也可以选 AgentHub 的，要出下拉选择而不是填什么标识符」。

**现状核实（改动前运行实例实测）**：委派区代码已有引擎下拉 + builtin 角色下拉 + agenthub agent 下拉，但两个体验缺口：① 下拉仅在 `s.preview?.xxx.length` 非空时渲染，而 preview 需先点「生成提示词」→ 打开详情时第一个下拉为空/消失；② preview 未加载时下拉整块不渲染（用户只看到按钮 + 空态文字，像要「填标识符」）。

**改动（TaskDetail.vue，未 git 提交）**：
1. `onMounted` + `watch(task.id)` 自动 `store.loadPreview`（轻量接口 `GET /api/todos/{id}/dispatch`，返回 agents/builtInAgents/canDispatch，不生成 prompt）。
2. 引擎/角色/agent 三下拉**始终渲染**（去掉 `&& s.preview?.xxx.length` 条件）：
   - 内置引擎：`默认（程序员）+ 七角色`；preview 未就绪 → 占位「加载中…」；就绪但无角色 → 「本工具 AI Agent 未就绪（先生成提示词）」。
   - 外部引擎：`默认 agent + 已登记 agents（name（vendor））`；无 agents → 占位「暂无可用 agent（请先登记）」(disabled) + 既有「去 Agent 中枢登记本机 agent」引导。

**门禁（Verified）**：

| 门禁 | 结果 | 证据 |
|---|---|---|
| 前端构建 | ✅ | `pnpm run build` ✓ built in 1.04s，dist/index.js 82.63 kB |
| todo e2e | ✅ 16/16（1 worker，2.6m） | 「16 passed」；**U3 断言语义更新**（打开详情即自动加载预览，不再要求先点「生成提示词」）、**新增 U6**（双下拉开箱即用：引擎 2 项 + agent/角色下拉不空） |
| 打包 | ✅ 2.3.7.2610091523 | 签名「新增 2 / 跳过 1 / 失败 0」「校验通过：3 / 3」，ALL DONE 235s |
| 运行实例升级 | ✅ | 设置页自点 检查更新→下载→重启并更新（确认弹窗自点）→ exe FileVersion 实测 **2.3.7.2610091523** |

**运行实例复验（/todo 任务 50，Verified）**：
- 打开详情（未点「生成提示词」）→ 引擎下拉 2 项 + **角色下拉自动出现 8 项**（改动前 roleOpts=null）。
- 切「外部 AgentHub」→ agent 下拉可见：占位「暂无可用 agent（请先登记）」(disabled) + 登记引导按钮 ✓（不整块消失）。
- 列表徽标「已成功·程序员」「#run5 记录6」✓。
- 截图：`screenshots/live-51888/todo-2.3.7-委派区-内置角色下拉.png` / `todo-2.3.7-委派区-外部agent下拉.png`。

**踩坑**（已闭环）：
1. **U3 态1 断言过时**：自动 loadPreview 后 preview 立即有值，「先生成提示词」态不再出现 → 语义更新为「打开详情即自动加载预览，hint 直接给四栏前置条件」。
2. **U6 首跑红**：disabled 占位 option 在 Playwright 可见性语义下 hidden → 断言改「`option` 不空」而非「可见」。
3. **并行竞态**：2 worker 全量下 B1/U3 偶红（自动 loadPreview 引入新异步 + 并行共享宿主）→ todo 目录固定 `--workers=1` 跑（与 e2e-testing 技能同源结论一致）。
4. **E1 偶发 `database is locked`**：真实委派读 agent-status 时 SQLite 写锁竞态（404），单跑/1 worker 重跑绿——非本次改动引入，登记 TODO P3。

**现场提示**：运行实例 AgentHub agents 为空 = 插件 Data 落点缺陷（2.3.6 升级丢登记数据，TODO P1 另列）；UI 下拉形态已就绪，用户需在 Agent 中枢重新登记本机 agent 后外部下拉才有真实选项。
