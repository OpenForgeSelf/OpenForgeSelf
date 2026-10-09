# PILOT-057｜todo 详情布局重构（057A：侧边栏 → 主区）+ AgentHub 状态同步与编码修复（057B）（mini-task 合并）

> 任务协调人：MainAgent ｜ 裁剪裁定：**轻量**（合并 057A 前端重构 + 057B 后端状态/编码修复；前端 3-5 文件 + 后端 2-4 文件，Intent/Spec/Plan/Task 合并本文件，Evidence/Review 单独产出）
> 状态：**已完成**（05-evidence / 06-review = APPROVED，见同目录）
> 证据：`.forgeself/memory/2026-10-08.md` 输入11 段 + :51888 界面截图 + AgentHub task id=2 实况 + todo 记录 #5 乱码对照

---

# 子任务 A：PILOT-057A 详情布局重构（侧边栏 → 主区内容页）
## 0. 对外方案（六段）

### 1) 目标
解决「详情信息太密集、侧边形式不好看」：把待办详情从**右侧窄边栏**改为**主区内容页**（列表左、详情右 2/3 主区或独立视图），信息按层级分组、可折叠。
成功判据：详情打开后主区呈现分层（任务信息 / 委派状态 / 执行记录三区），窄屏不破版；交互规格写入 Spec「Interaction Design」节并走查核对。

### 2) 改动（≤5 条）
| 文件:行 | 动作 |
|---|---|
| `Plugins/TodoTracker/web/src/TodoView.vue` | 布局重构：列表与详情从「列表 + 侧边栏」改为「列表 + 主区」两栏（详情区宽度 ≥ 主区 2/3），空态提示保留 |
| `Plugins/TodoTracker/web/src/components/TaskDetail.vue` | 信息分级：任务信息（标题/优先级/项目/Objective/四栏）→ 委派状态（agent/进度/结果）→ 执行记录（时间线）三区；长字段折叠（含 PILOT-056 的 resultSummary 折叠） |
| `…/web/src/store.ts` | （顺带）PILOT-056 的 404 降级若本批已批则一并；否则不动 |
| `…/web/src/components/ExecutionTimeline.vue` | 时间线视觉对齐新布局（间距/对齐符合 ui-ux-design CRAP） |

### 3) 不改
- 后端契约、数据结构、接口（纯前端展示层重构）；
- 不做「详情独立路由页」大改（本批保持单页内切换，若你更想要独立页请在拍板注明）；
- 不碰 AgentHub 状态/编码（那是 PILOT-057B）。

### 4) 验证（快档 + 走查）
- `cd Plugins/TodoTracker/web && pnpm run build`；`cd ForgeSelf.Web && pnpm run check`
- e2e 定向 `npx playwright test e2e/plugins/todo-tracker --workers=1`
- 运行实例走查：打开任务 49 详情 → 三区布局、信息不重叠、长文本折叠、窄屏截图核对（ui-ux-design 走查清单 10 项）

### 5) 代价与风险
- 纯前端重构，不改数据；回滚 = 还原 2-3 文件；
- 布局改动涉及全部详情交互（关联/委派/记录），需走查逐项回归；
- 生效需发布（打 tag/本地更新源），发布另请示。

### 6) 待你拍板
- Q1 详情形态：**主区两栏**（推荐）还是**独立详情页**（列表 → 点开整页）？
- Q2 本批是否含 PILOT-056 两个缺陷修复（404 降级 + resultSummary 折叠，同一批改完一起验证）？默认：**含**（省一次发布）。

---

## 1. Intent
用户（输入11 #1）：「整个待办任务的详情信息太密集了，采用侧边的形式很不好看」。目标 = 详情展示分层、留白合理、信息可扫读；交互规格进 Spec、走查按 ui-ux-design 清单核对。

## 2. Spec（要点；Unknown 标注）
- S1 布局：TodoView 改为 `列表(约1/3) + 详情主区(约2/3)`；未选中时详情主区显示空态引导（沿用现文案）。
- S2 分区：TaskDetail 顶部任务信息卡（标题/优先级/项目/Objective/四栏，四栏可折叠「展开全部」）→ 委派状态卡（agent 名/徽标/结果摘要，PILOT-056 折叠并入）→ 执行记录时间线（每条 actor/action/result 摘要 + 展开）。卡片间距统一 `--el-*` token。
- S3 反馈：关联/委派/保存操作反馈保持现状（toast/记录追加），不因布局变化丢失。
- Unknown：窄屏断点行为（<900px 时详情是否降为抽屉/堆叠）——按 Element Plus 断点给默认（≤768px 详情堆叠列表下方），走查截图核对。
- 验收：三区可见、无重叠、长文本折叠可用、窄屏不破版（走查 + 截图）。

## 3. Plan
1. Read `TodoView.vue` / `TaskDetail.vue` / `ExecutionTimeline.vue` 现状（布局结构、class、宽度约束）；
2. 改 TodoView 两栏布局（grid/flex，详情列宽 2/3）；3. TaskDetail 三区卡片化 + 折叠；4. 时间线对齐；5. 门禁 + e2e + 运行实例走查截图；6. Evidence/Review + §10 汇报。

## 4. Task
**Allowed**：改上述 web 源文件；跑门禁/e2e；运行实例只读走查（截图）。
**Forbidden**：改后端/AgentHub；停/启/杀宿主；git 写操作；发布（未授权）；一次性 temp 脚本代替门禁。

**验证命令**：
```powershell
cd Plugins/TodoTracker/web && pnpm run build
cd ForgeSelf.Web && pnpm run check
cd ForgeSelf.Web && npx playwright test e2e/plugins/todo-tracker --workers=1
```

---

# 子任务 B：PILOT-057B AgentHub 状态同步 + 编码修复
## 0. 对外方案（六段）

### 1) 目标
消除「任务已回报成功（stage=Review）但详情委派状态显示 Failed · exit -1」的**双系统不同步**，并止住两处中文乱码（AgentHub 存库 mojibake + opencode 回报 GBK→`?`）。
成功判据：任务 49 同类场景复现（回报成功）后，AgentHub 任务状态 = **Completed（或按回报终止）**而非 Failed(timeout)；新回报记录中文不再出现 `?`/mojibake。

### 2) 改动（≤5 条）
| 文件:行 | 动作 |
|---|---|
| `Plugins/AgentHub/Services/DelegationRuntime.cs` | ① 新增「回报成功即收尾」：todo 侧回报 Review 时回调 AgentHub 把任务标记 Completed/取消超时计时（进程若仍存活则自然回收）；② 超时判定加「已回报」豁免 |
| `Plugins/AgentHub/Services/CliTransport.cs` | 字符串输出/持久化统一 UTF-8（排查 GBK 来源：进程输出重定向/DB 写入路径） |
| `Plugins/TodoTracker/Controllers/TodoDispatchController.cs` 或 `TodoDispatchService.cs` | POST records（stageTo=Review）时调 AgentHub 完成标记（新增/复用接口） |
| `Plugins/AgentHub/Controllers/*` | 暴露「按 taskKey 标记完成」只读端点供 todo 回调（若现有无等价能力） |
| `Plugins/TodoTracker/web/src/components/ExecutionTimeline.vue` | （若含）乱码检测提示：记录含 U+FFFD 替换符时显示「（编码异常，原文本可能含非 UTF-8 字符）」 |

### 3) 不改
- 已损坏的历史数据（记录 #5 的 `?` 不可逆；AgentHub resultText 可 GBK 反向修复但价值低——**如需修复历史数据另行拍板**）；
- opencode/模型/提示词模板语义；todo 侧记录 #1-4 正常部分；
- 不碰 UI 布局（那是 PILOT-057A）。

### 4) 验证（中档）
- 后端 `dotnet build` + 全量 `dotnet test`（碰宿主插件源码 → 中档）
- 插件前端 build/check + e2e 定向（记录渲染回归）
- 运行实例复验：委派新任务（或重放 49 场景）→ 回报成功后 AgentHub 状态 Completed、详情不再 Failed；回报含中文 → 记录无 `?`

### 5) 代价与风险
- 后端状态机变更（AgentHub 完成语义 = 进程退出 → 回报为准），影响委派链路终态判定，需回归历史场景（48 超时 Failed 语义变化：**已回报成功的即使进程未退也不再标 Failed**）；
- 编码修复在已损数据上不可逆（只能止新增）；
- 生效需发布，发布另请示。

### 6) 待你拍板
- Q1 完成语义：**回报成功（stageTo=Review）即标完成**（推荐，用户视角正确）还是保留进程退出为准（现状）？
- Q2 历史数据是否反向修复（AgentHub resultText GBK→UTF-8 脚本恢复，记录 #5 无法恢复只能标注）？默认：**不修历史**，只止新增。

---

## 1. Intent
用户（输入11 #2/#3）：详情委派状态 Failed 与执行记录成功矛盾要解决；记录中文乱码要解决（问谁提交的→opencode，宿主编码缺陷）。目标 = 完成语义对齐 + 编码止损。

## 2. Spec（要点；Unknown 标注）
- S1 完成语义：todo POST records 且 `stageTo=Review` 时，宿主回调 AgentHub 将该 taskKey 标记为**完成**（status=Completed、exitCode=0、errorCode=null，记录 endTime=当前）；AgentHub 超时定时器对「已标记完成」的任务**不再覆盖**为 Failed（幂等：先到先得，已完成不被超时改写）。
- S2 超时豁免：DelegationRuntime 超时回调前检查任务状态，已 Completed 则跳过（防 20:41 场景：回报 20:41:03 → 超时 20:41:24 被覆盖）。
- S3 编码：AgentHub 全链路 UTF-8——进程 stdout 捕获按 UTF-8 解码（当前疑似 GBK 码页）；DB 写入前字符串保证 UTF-8（XCode 连接/列类型确认）；**禁止再经 Console 默认编码往返**。
- S4 回报编码：opencode 回报的 GBK→`?` 在发送侧已损、后端不可恢复——止损手段：委派提示词回报 curl 示例改为**不含中文的 ASCII 模板**（actor/action/result 字段名与占位说明用英文，中文值由 agent 自填仍可能 GBK——故同时给后端宽容提示）；**后端检测**：入参含 U+FFFD 时记 warning 日志 + 记录标注。
- Unknown：① AgentHub 现有接口是否有「按 key 标完成」（需读代码确认，无则新增）；② opencode 环境变量能否强制 UTF-8（LANG/LC_ALL）——需在 opencode 侧实验，本批以宿主侧处理为主。
- 验收：重放 49 场景 AgentHub=Completed；新回报中文无 `?`（实测 opencode 再回报一次含中文验证）；全量后端测试绿。

## 3. Plan
1. Read `DelegationRuntime.cs`（超时/完成状态机、AppendEvent）、`CliTransport.cs`（进程输出编码）、`TodoDispatchService.cs`/`TodoDispatchController.cs`（records 写入）、AgentHub 控制器（现有端点）；
2. 实现 S1/S2（完成标记 + 超时豁免 + todo 回调）；
3. 实现 S3（UTF-8 规范化，定位 GBK 来源：先实测进程输出码页）；
4. 实现 S4（提示词模板 ASCII 化 + 后端 U+FFFD 检测标注）；
5. 门禁（后端 build + 全量 test）+ e2e + 运行实例复验（新委派含中文回报）；6. Evidence/Review + 汇报。

## 4. Task
**Allowed**：改上述后端/前端文件；跑门禁/e2e；运行实例只读复验（委派新任务属用户已授权的验收动作）。
**Forbidden**：改 UI 布局（057A 范围）；停/启/杀宿主；git 写操作；发布（未授权）；修历史数据（未拍板）。

**验证命令**：
```powershell
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test            # 中档全量
cd Plugins/TodoTracker/web && pnpm run build
cd ForgeSelf.Web && npx playwright test e2e/plugins/todo-tracker --workers=1
```
