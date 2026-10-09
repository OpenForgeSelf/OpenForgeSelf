# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，不提前决定具体代码实现。
> Task ID：2026-10-08-ux-close ｜ 日期：2026-10-08

## Problem

1. **UX 三缺陷（todo-tracker 插件）**：
   - ①「项目选择」：前端没有真正的项目选择器（只有手动路径输入与过滤下拉）；选了项目也不自动带出项目地址；新建任务不能预选项目自动关联。
   - ②「交给 AgentHub 执行」按钮：能力缺席 / 四栏不齐 / 无可用 agent 时**恒禁用且页面无可见原因**（只有 hover title），无下一步引导。
   - ③ 任务下发给 agent 后：只有 taskKey 编号，列表无委派状态、详情无自动刷新，执行记录入口不显眼 → 用户「只能默默等待」。
2. **流程缺陷**：从需求到最终提交的工件链（Intent/Spec/Plan）**没有交互设计环节**；交互设计统一要求虽写在 plugin-development §3.4，但未与工件链和 DoD 绑定 → 功能能做出来，好不好用没人把关。

## Why

用户原话：「工件里面没有提到交互相关的，从需求开始到最终提交，都没人管 ui ux，导致现在体验极差……以上这些问题，需要完善开发、验证流程的，不仅要能做出来，还要好用」。

## Expected Outcome

- todo-tracker 三个 UX 缺陷修复：①可从宿主项目清单选择项目、选中自动带出并保存项目地址、新建任务可预选项目创建即关联；②委派按钮禁用时页面可见原因文案，agents 为空给出「去登记 agent」的下一步引导；③列表可见委派状态徽标、详情委派状态自动轮询直至终态、执行记录区成为明确的进度入口。
- 流程完善：交互设计成为开发工件（Spec）的**必写节**，验证阶段（DoD/e2e/走查）必验交互清单；本批工件即示范（含交互设计节）。

## Constraints

- 不改 AgentHub 插件自身 UI（本次只做 todo 插件视角的进度可见；AgentHub 侧「任务列表页」不在本批）。
- 不新增批量委派状态接口（列表徽标用既有 stage/agentTaskKey 推断；实时状态以详情 agent-status 为准）。
- 不新增前端测试基建（todo-tracker web 无 vitest 配置；交互逻辑断言并入 e2e + 走查截图）。
- 契约变更（AgentStatusDto 加字段）必须同步契约测试字段清单（该用例会红，属正确提醒，要一起改）。
- 零 mock e2e（e2e-testing 铁律）；不碰用户运行实例（:51888）。
- 禁止 git commit/push（用户未授权）；禁止停/启/杀用户宿主进程。
- 流程文档更新（规范/模板/技能/AGENTS.md）随本批落盘，不另开任务。

## Success Criteria（可验证）

1. 新建任务可预选项目（下拉来自 `/api/todos/projects`），创建后任务已关联（列表显示项目名/地址；`GET /api/todos/{id}` 的 projectId>0）。
2. 详情页项目区提供「选择项目」入口，选中后自动关联并显示项目名 + 完整地址（root）；「输入路径」模式保留。
3. 委派按钮禁用时，按钮下方**可见**原因文案（四态 + agents 空态），非仅 hover；agents 空态含「去 Agent 中枢登记」引导。
4. 列表行对已委派任务显示委派状态徽标（按 stage 推断：Dispatched/Running=执行中、Review=待验收、Done=完成）与 taskKey 短显。
5. 详情委派状态区：显示 agent 名（AgentStatusDto.AgentName）；Running/Dispatched 时每 15s 自动刷新，终态自动停止并提示结果。
6. 「记为执行记录」成功后提示并引导到执行记录区（锚点/滚动）。
7. e2e 新增交互用例全绿（新建选项目 / 委派禁用可见文案 / agents 空态引导 / 列表徽标 / 轮询刷新），走查截图读图通过。
8. 流程落盘：Spec 模板加「交互设计」节；ai-native-engineering-workflow.md / plugin-development §3.4 / AGENTS.md §11 同步声明交互设计为必写环节；本批 02-spec 含交互设计节（示范）。
