# PILOT-056｜todo-tracker 走查缺陷修复：委派状态 404 降级 + resultSummary 折叠（mini-task）

> 任务协调人：MainAgent（本会话）｜ 裁剪裁定：**轻量**（源文件 2，规范 §4 允许 ≤3 文件缺陷修复合并为 mini-task）
> 状态：**闸门1 待用户批准**（批准前不改任何业务代码）
> 基线：运行实例 :51888 = 2.3.3.2610081746（todo-tracker v1.1.2）；本轮走查（输入9）在真实 UI 复现全部缺陷，证据见 `.forgeself/memory/2026-10-08.md` 输入9 段。

---

## 0. 对外方案（六段 · AGENTS.md §3.1 硬约束）

### 1) 目标
修掉用户在真实 UI 发现的两个体验缺陷，**成功判据 = 浏览器点开旧委派任务不再弹「委派任务不存在」错误提示；详情页不再平铺大段 JSON**：

- 点击待办条目（其 AgentHub 委派任务已不存在，如升级清库的旧任务 47）→ 详情轮询 `GET /api/todos/{id}/agent-status` 返回 404「委派任务不存在」→ 前端 toast 报错（用户原话「会提示委派任务不存在」）。改为**静默降级**：委派状态区显示「委派任务不存在（可能已被清理）」，不弹 toast；
- 详情页委派状态区平铺 opencode 会话原始 JSON（resultSummary 最长 60 万字符，如任务 48）→ 改为**超长折叠**（>240 字符截断 + 「展开全文/收起」）。

### 2) 改动（源文件 2，≤5 条）

| 文件:行 | 动作 |
|---|---|
| `Plugins/TodoTracker/web/src/store.ts:401-412` | `loadAgentStatus` catch 分支：`e instanceof ApiError && e.status === 404` 时静默降级 `state.agent = { ok:false, error:'委派任务不存在（可能已被清理）', statusCode:404, taskKey, agentName:null, status:'', terminal:false }`，**不** `showFailure`；其余错误照旧 |
| `Plugins/TodoTracker/web/src/components/TaskDetail.vue:453` | resultSummary 折叠：>240 字符显示前 240 + 「…展开全文」toggle（局部 ref `summaryOpen`），`:title` 保留全量；≤240 原样显示 |

### 3) 不改
- 后端 `TodoDispatchController.cs:194` 404 语义、AgentHub、宿主代码、委派链契约——全部不动（404 降级只在前端消费侧处理）；
- 列表批量徽标逻辑（已静默）、既有 e2e 语义（只增不删）；
- **不 push / 不 commit / 不发布**（待授权项）；不碰 :51888 运行实例配置。

### 4) 验证（快档 §5.6）
- 插件前端：`cd Plugins/TodoTracker/web && pnpm run build`（判据：构建日志 0 error）
- 宿主门禁：`cd ForgeSelf.Web && pnpm run check`（vue-tsc + eslint，改的是插件 web 但归同一前端体系）
- e2e：`cd ForgeSelf.Web && npx playwright test e2e/plugins/todo-tracker --workers=1`（判据：日志正文 PASS 计数，不回归）
- 运行实例走查：:51888 点开任务 47 → **无 toast**，委派状态区显示「委派任务不存在（可能已被清理）」；点开任务 48 → resultSummary 折叠显示 + 可展开

### 5) 代价与风险
- 纯前端消费侧修复，不碰后端契约，回滚 = 还原 2 文件（git 可回溯）；
- 404 静默后旧任务不再有任何委派状态提示风险（以「委派任务不存在」文案兜底，信息不丢）；
- 运行实例验证依赖宿主加载新 dist：前端产物需经发布链路（打 tag/本地更新源）才在 :51888 生效——本批只改码+门禁，**生效与发布待用户授权**。

### 6) 待你拍板
- **Q1** 是否按本方案实施？默认：**是**
- **Q2** 本批是否顺带发布（本地更新源 + 页面自动更新，同 2.3.3 流程）？默认：**先不发布**，验证完另行请示（发布属独立授权项）

---

## 1. Intent（为什么 / 做什么 / 到什么程度）

- **为什么**：用户真实 UI 走查发现「点击待办条目提示委派任务不存在」（旧任务 AgentHub 委派数据随 2.3.3 升级清库，详情轮询 404 必现，前端 toast 报错）；且详情页平铺 opencode 会话原始 JSON（resultSummary），观感差、信息噪声。
- **做什么**：前端 2 处缺陷修复——详情委派状态 404 静默降级（不弹 toast，显示友好文案）；resultSummary 超长折叠（可展开）。
- **到什么程度**：代码改动收敛在 todo-tracker 插件 web 2 个文件；门禁（build + check + e2e）绿；运行实例走查确认两场景不再复现。发布/推送不做（待授权）。

## 2. Spec（要点；不确定点标 Unknown）

- **S1 404 降级**：`loadAgentStatus` 对 `ApiError.status === 404`（后端 message 含「委派任务不存在」）静默降级为 `state.agent = { ok:false, error:'委派任务不存在（可能已被清理）', statusCode:404, taskKey:<当前任务 agentTaskKey 或 ''>, agentName:null, status:'', terminal:false }`——满足 `AgentStatus` 接口（ok/statusCode/taskKey/status/terminal 必填，error/agentName 可选）。`TaskDetail.vue:452` 的 error 行（`.td-missing`）自然显示该文案；`:454`「读取中…」因 `!s.agent?.ok` 且 error 存在而不再显示。**非 404 错误仍 `showFailure`**（如网络断/500）。
- **S2 折叠**：resultSummary 长度 >240 时显示 `summary.slice(0,240) + '…'` 与「展开全文」按钮；点击 toggle 显示全量 + 「收起」；`:title` 恒为全量（悬停可读）；≤240 原样。折叠态为组件局部 ref，不持久化（刷新复位，可接受）。
- **验收**：e2e 新增 1 条（可选，用隔离实例可造 404？——**Unknown**：隔离实例中旧任务 404 难构造（AgentHub 任务需先存在再删），故 e2e 覆盖折叠（构造超长 resultSummary 需 mock 接口——e2e 零 mock 原则冲突）→ **折叠与 404 降级均以运行实例走查 + DOM 断言为证据**，e2e 不强造（如实记录不覆盖理由）；既有 e2e 不回归为准。
- **Unknown**：① 404 构造在隔离 e2e 的可行性（不强求）；② `taskKey` 降级对象的取值（`state.selected.agentTaskKey` 可能为空串，`statusCode` 用 404 常量即可）。

## 3. Plan（分步，具体到文件）

1. 已实读：`store.ts:395-434`（loadAgentStatus/loadAgentStatuses，404 抛 ApiError → `showFailure('读取委派状态', e)`）、`TaskDetail.vue:443-455`（委派状态区，resultSummary 平铺在 :453）、`http.ts:28-36/203`（ApiError.status、agentStatus 端点）、`types.ts:188-196`（AgentStatus 接口）。
2. 改 `store.ts`：catch 分支判 `ApiError && status===404` → 构造降级对象赋值 `state.agent`；否则原逻辑。
3. 改 `TaskDetail.vue`：加 `summaryOpen` ref + `summaryPreview` computed（截断逻辑）+ 模板 453 行改折叠渲染与「展开全文/收起」按钮。
4. 门禁：插件 `pnpm run build`；宿主 `pnpm run check`；todo e2e 定向（--workers=1）。
5. 运行实例走查（只读）：点任务 47 → 无 toast + 文案；点任务 48 → 折叠 + 展开。
6. 补 `05-evidence.md` / `06-review.md`；按 §10 汇报；发布/推送列为待授权项。

## 4. Task（Allowed / Forbidden + 验证命令）

**Allowed**：改上述 2 个 web 源文件；跑插件 build / 宿主 check / todo e2e（按 §5.0 设 NO_PROXY/TEMP）；运行实例只读走查（点开任务、读 DOM、截图尝试）；记日志/TODO。

**Forbidden**：碰后端/AgentHub/宿主代码与契约；停/启/杀用户实例或改其配置；`git commit`/`push`/发布；删任何数据目录/文件；用一次性 temp 脚本代替门禁；把猜测当证据写进工件。

**验证命令**：
```powershell
cd Plugins/TodoTracker/web && pnpm run build      # 判据：0 error
cd ForgeSelf.Web && pnpm run check                # 判据：vue-tsc 0 error + eslint 无新增 error
cd ForgeSelf.Web && npx playwright test e2e/plugins/todo-tracker --workers=1   # 判据：日志 PASS 计数，不回归
```
