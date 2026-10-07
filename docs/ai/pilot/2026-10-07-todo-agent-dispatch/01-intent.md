# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，**不提前决定具体代码实现**。
> Task ID：PILOT-054 ｜ 日期：2026-10-07

## Problem

`todo-tracker` 现在是一条「标题 + 备注 + 待处理/已完成」的便签（`Data/Model.xml:25-40`），它记的是**给人看的备忘**，不是**能交给 agent 干的工作单元**：

1. 没有「让一个 agent 拿到就能开工」的信息载体——目标、范围（允许改什么/禁止改什么）、验收判据、验证命令、正文内容全都无处放；agent 想接活只能靠人重新口头复述一遍上下文。
2. 任务不知道自己在**哪个项目**里发生。仓库已有的项目工作区真相是宿主 `IProjectRegistry`/`Project.Root`（`ForgeSelf.Abstractions/IProjectRegistry.cs:13`），但待办与它零关联；而且路径写法不统一——`D:\proj`、`D:/proj`、Git-Bash 的 `/d/proj`、WSL 的 `/mnt/d/proj` 指同一目录，宿主 `Register` 只做 `Path.GetFullPath` + 精确匹配（`HostProjectRegistry.cs:56,71`），`/d/proj` 会被解成 `C:\d\proj` 直接「目录不存在」，**同一个项目会被登记成多个或根本登记不上**。
3. 本项目每个开发任务都在 `docs/ai/pilot/YYYY-MM-DD-<task-id>/` 产出 00-07 工件（规范 §2，pre-commit 硬门禁），**这批文件本身就是写给 agent 的任务规格**，但目前没有任何入口能把它们变成一条可下发的任务——每次都要人手工复制粘贴。
4. agent 干完活**没有回写处**。现在只有 `Status=0/1` 一个开关，"做了什么操作、什么结果、改了哪些文件、风险、遗留" 全无处安放，导致任务历史不可审计，也无法判断「这条待办到底是被谁、在哪个项目、按哪份工件做完的」。
5. 顺带两处既有规范缺口挂在本插件上：控制器无 `[Authorize("ApiKeyPolicy")]`（`TodosController.cs:9-11`，违 plugin-development 铁律 17）；插件不自行建表、无 `Data/TodoTrackerTables.cs`（`TodoTrackerPlugin.cs:104-118`，违铁律 12）。

## Why

- 项目的运行方式已经是「AI Agent 按工件闭环干活」（AGENTS.md §11 / ai-native-engineering-workflow）。待办插件要成为**人与 agent 之间的工作交接面**，否则用户仍需每次在聊天里重述上下文——这是当前最主要的重复劳动。
- 路径关联是下发的**前提而非修饰**：agent 必须知道工作目录（AgentHub 的 `DelegationRequest.cwd` 且要在白名单内，`DelegationRuntime.cs:618-648`）；而"同一路径 = 同一项目"若不成立，任务就会散落在重复项目下，统计与筛选失真。
- 执行记录是**验收证据的落点**：AGENTS.md §10 要求每条结论标 Verified/Inferred/Unknown、Evidence 只记实际发生的事。没有结构化回写处，"agent 说它做完了"就只是话。
- 顺带闭合铁律 17/12 缺口：管理面鉴权是安全底线；插件不自行建表在生产库上是「表神秘缺失 → `no such table`」的已知病灶（铁律 12 记录了 AgentHub 因此功能全废的先例）。

## Expected Outcome

完成后，`todo-tracker` 从「便签」变成「**可下发的 agent 任务台账**」，用户视角得到四件事：

1. **一条任务 = 一个 agent 能直接开工的工作单元**：目标 / 正文 / 允许范围 / 禁止范围 / 验收判据 / 验证命令 / 优先级 / 下发对象，且界面上一屏能读全。
2. **任务关联到一个项目，且路径写法随便是同一个项目**：`/d/project`、`D:\project`、`D:/project/`、`/mnt/d/project` 归一到同一根并复用宿主项目档案。
3. **一条任务的内容可以直接由该项目 `docs/ai/pilot/<task-id>/` 的工件组装**：选目录 → 勾选核心文件 → 生成正文，不用人复制。
4. **agent 回报有处可回**：执行记录（谁、做了什么、结果、改了哪些文件、验证、风险、遗留、证据、状态流转、耗时、下一步）逐条累积成时间线，并可从界面/REST/AI 工具函数三个面写入。
5. **一键交给 AgentHub 真跑**：经 Abstractions 能力接缝把任务提交为委派任务，回填 `taskKey` 与状态，并可把 agent 结果同步成一条执行记录。
6. 界面归插件（`Plugins/TodoTracker/web/`），宿主 `src/` 不再承载该页面；插件层 e2e 补齐；文档与版本同步。

## Constraints

对照规范 §1 硬性约束逐条：

| 约束 | 本任务的立场 |
| --- | --- |
| 1 不改生产环境 | 遵守。只在开发 worktree 改代码；发布动作到闸门3 才请示 |
| 2 不改数据库结构 | **例外，已升级到人**：插件自有库 `TodoTracker.db` 的 `Todo` 扩列 + 新增 `TaskExecution` 表（用户 2026-10-07 拍板"原地扩 Todo + 新增 TaskExecution"）。宿主库 `Project`/`RunCommand` **结构零改动**（宿主实体无 Model.xml，重建不可行 —— plugin-development §E 实测结论） |
| 3 不改鉴权/权限核心逻辑 | 只做**加法**：给本插件控制器补类级 `[Authorize("ApiKeyPolicy")]`（铁律 17 要求的既有缺口闭合），不动宿主策略与权限模型 |
| 4 不新增大规模依赖 | 遵守。零新 NuGet 包、零新 npm 依赖（插件 `web/` 依赖组合照抄 `Plugins/QuickLinks/web/`） |
| 5/6 不无关重构、不改无关文件 | 遵守。宿主侧只做"迁出本插件页面"必需的删除；`Home` 插件对 `/api/todos` 的消费保持兼容，不改 Home |
| 7 不为展示能力扩大范围 | 遵守。**不做**：agent 自动重试/编排、成本统计、跨插件事件、任务依赖图（登记 not-taken-decisions） |
| 8 必须真跑验证 | 遵守。门禁分档：因碰 `ForgeSelf.Abstractions` + `Plugins/AgentHub` ⇒ **中档**全量 `dotnet test`；插件层 e2e + 宿主前端 `check/test/vue-tsc -b` |
| 9/10 不猜、不伪造 | 遵守。不确定点写 `Unknown`；验证标 Verified/Inferred/Unknown |

流程约束：闸门1（本四件工件经用户批准）前不改业务代码；闸门2（Evidence+Review 齐备）前不提交；插件任务五步闭环（门禁 → 插件层 e2e → 发布 → 隔离实例走查 → 运行实例只读复验）缺一不报完成；禁止 agent 停/启/杀用户宿主。

## Success Criteria

以下每条都可被实际命令输出判定（细则与命令见 02-spec.md §Acceptance Criteria、04-task.md §Verification Commands）：

1. **数据**：`xcode Model.xml` 生成后 `Todo` 含全部下发字段、`TaskExecution` 表存在；插件启动自行建表成功；`BindColumn` 字段集对账无漂移；`dotnet test --filter ~TodoTracker` 全绿且**新增用例数被计入**。
2. **路径归一**：同一目录的 4 种写法（`/d/p`、`D:\p`、`D:/p/`、`/mnt/d/p`）解析出**同一 canonical key**，且只对应**一个**宿主项目 Id；对账测试用金样表逐条断言（含反例：`/d/p` 与 `/d/p2` 必须不同）。
3. **工件下发**：给定真实 pilot 目录（用本任务自身目录作为夹具），勾选文件后 `Content` 等于组装结果且**长度 > 8000 字符不被截断**（写入 12,000 字符读回等长）；越界路径 / 非 `.md` / 目录名带 `..` 一律 400。
4. **执行记录**：三面（REST/AI 工具/界面）写入同一条记录形状一致；列表按任务内 `Seq` 递增可回放；`Status=1` 与 `Stage=Done` 一致。
5. **一键执行**：`todo-tracker` 经 `ctx.Get<IAgentDelegation>()` 提交，AgentHub 返回 `taskKey` 并回填；接缝缺席时明确 503 + 文案，不静默成功；全程**没有**插件间直连 HTTP。
6. **界面归位**：`Plugins/TodoTracker/web/dist/index.js` + `style.css` 存在，导出名 == `plugin.json.views[0]`，`grep 'from "vue-router"'` 证明 external；宿主 `src/**` 无 todo 页面残留；`check-features.mjs` 门禁绿；标题旁版本徽标可见。
7. **安全**：`api/todos` 全部控制器类级 `[Authorize("ApiKeyPolicy")]`，反射守卫用例在场（对照 `McpAdminAuthTests` 形状）；无 token 请求返回 401。
8. **兼容**：`Plugins/Home/web` 零改动仍可创建/完成/列出待办（e2e 断言首页面板）。
9. **e2e**：`e2e/plugins/todo-tracker/todo-tracker.spec.ts` 在隔离宿主上真实跑绿（零 mock，含截图读图 Level 3 清单），且 `pnpm exec playwright test` 深档结果按基线对账无新增红。
10. **流程**：00-07 八件工件齐备（`pwsh scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-07-todo-agent-dispatch` PASS）、功能文档 `005-todo-tracker.md` 与 ADR/not-taken-decisions 同步、日记与 TODO 收口。
