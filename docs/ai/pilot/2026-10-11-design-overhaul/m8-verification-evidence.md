# M8 收口 · 最终验收统一复跑证据

> 批次：M8（设计稿全面重构 M4-M8 收口·最终验收统一复跑）｜执行：SubAgent o_000EeDllURG｜日期：2026-10-11
> 范围（专属）：只做验证 / 最小修复 / 定性三类事。本机低负载、全批已交付；dev-stack 复用（后端 :7301 / 前端 :7399），未停/启/杀任何宿主。
> 环境前置（每条命令重设）：`NO_PROXY=no_proxy=localhost,127.0.0.1,::1`；`TEMP=TMP=<repo>\.temp\tmp`；`E2E_SIGN_EXE=false`。判据看日志正文（`*> <log>` 后 Read），不看 exit code。

## 0. 结论速览

- **全量守卫（快档三连）全绿**：style-tokens 1470、compliance a209/b0/c0/d0、check 0 errors/145 warnings、vitest 70 files/792 tests——均在棘轮内且无新增回退。
- **此前受阻 e2e 复跑拿稳信号**：ai-agent 7/7、todo-tracker 14/14、home 1/1、menu-route-consistency 4/4 全绿，阻塞解除；prompts-skills 7/7 全过（**证实 /prompts 侧栏 ForgeCard role=button 无 strict 冲突，M4a-1 「侧栏改 .glass 裸 div」最小修复不需要执行**）。
- **未发现 M4-M8 迁移引入的真实回归**。失败项逐条定性为后端 flake / 后端协议 500 / live 变体配置错配 / 既有数据依赖时序，均非前端迁移触碰面。
- **本批零前端视觉代码改动**（最小修复边界内无迁移回归需修）。
- **CostScope DI 已定性**：真实架构缺口（Cordis 子容器不转发宿主契约），非陈旧二进制、非迁移回归，重启 dev-stack 无效；记 TODO。

## 1. 全量守卫（快档三连）

| 门禁 | 棘轮 / 基线 | 实测 | 判定 | 证据 |
|------|------------|------|------|------|
| design:check（style-tokens 硬编码色值） | ≤2484（基线 2501） | **1470**（-1031） | ✅ Verified | `.temp/m8-design-check.log` |
| design:check（compliance） | a≤244 b=0 c=0 d≤1（基线 a244 d1） | **a=209 / b=0 / c=0 / d=0** | ✅ Verified | 同上 |
| pnpm run check（vue-tsc + eslint） | 0 errors / 既有 warnings 不新增 | **0 errors / 145 warnings**（全为既有 SVG 属性格式警告） | ✅ Verified | `.temp/m8-check.log` |
| vitest run | 70 files / 792 不回退 | **70 files / 792 tests 全绿**（Duration 127.71s；`--maxWorkers=2`，堆大小走 NODE_OPTIONS 而非 CLI） | ✅ Verified | `.temp/m8-vitest.log` |

## 2. 受阻 e2e 统一复跑（`--workers=1`，日志 `*.log` 重定向）

命令统一：`cd ForgeSelf.Web; node node_modules/@playwright/test/cli.js test --config=playwright.config.ts --workers=1 --output=<空目录> --reporter=list <spec...>`。

### 2.1 插件批（batch1-plugin，34 tests：24 passed / 7 failed / 3 did not run，4.6m）

| spec | 结果 | 定性 |
|------|------|------|
| ai-agent/ai-agent.spec.ts | **7/7 全过** | ✅ 阻塞解除（此前隔离宿主前端未起致入口 JS 404）。远程加载+版本徽标 v1.7.3+三栏+SSE 聊天+composer+Agent 增删改+执行模式+计划驱动+关联工作流区块，全链路绿。 |
| home/home.spec.ts | **1/1 过** | ✅ 重定向 /home 四区块+导航桥+无溢出。 |
| todo-tracker/todo-tracker.spec.ts | **14/14 全过** | ✅ 阻塞解除（此前 page-setup 超时）。A1/B1/B2/C1/C2/D1/D2/D3/V1/E1（真实委派 opencode）/U1-U6 全绿。 |
| design-system:211（全链路大例） | 1 failed | ⚠️ **后端 SQLite 写锁 flake**：error-context `alert: 500 code=Busy(5) System.Data.SQLite.SQLiteException: database is locked`，失败在导入写入步（spec:603）。非前端迁移触碰面；属已知 DB lock 竞争 flake。 |
| mcp-center:106 | 1 failed | ⚠️ **后端 MCP 协议端点**：health 探活过，但 MCP `initialize` JSON-RPC `POST /mcp` 返回 HTTP 500（spec:56 mcpCall→:143）。C# 后端，非前端迁移触碰面。 |
| mcp-center:254 / :331 / :461 | 3 did not run | 🔁 describe serial 模式，:106 失败后级联跳过（非独立失败）。 |
| quick-links ×5（:68/:101/:121/:187/:238） | 5 failed | ⚠️ **live 变体 spec 配置错配**：spec 文件头明示对接长期运行实例 :51888、须走 playwright.live.config.ts、不走 globalSetup；本批在隔离 harness 下跑属配置错配，统一失败在 `openQuickLinks`（spec:53 `getByRole('heading',{name:'快捷链接'})`）。**已核实 `QuickLinksView.vue:235` 仍为 `<h1 class="view-title">快捷链接</h1>`——标题未漂移，非迁移回归**。不应在隔离 harness 下强行修绿。 |

### 2.2 宿主批（batch2-host，35 tests：30 passed / 5 failed）

| spec | 结果 | 定性 |
|------|------|------|
| agents.spec.ts | 3/3 过 | ✅ |
| all-features-plugins.spec.ts | 5/5 过 | ✅ |
| code-snippets.spec.ts | 3/3 过 | ✅ |
| menu-route-consistency.spec.ts | **4/4 全过** | ✅ 阻塞解除（此前 globalSetup 清理 flake）。双源对账 diff=0 + manifest 逐项真实导航 + 路由表解析 + /quicklinks 清零/补发回归，全绿。 |
| prompts-skills.spec.ts | **7/7 全过**（/prompts 3/3 + /skills 4/4） | ✅ **关键结论**：/prompts 页面加载/切换/保存 全绿——侧栏 `ForgeCard role="button"`（ForgeCard.vue:25 interactive=false 也带 role）**未造成 strict-mode 冲突**。故 M4a-1 沉淀的「侧栏改 .glass 裸 div」最小修复**不需要执行**，PromptsView.vue:137/182 两处 ForgeCard 保持不动。 |
| update-panel.spec.ts | 3/3 过 | ✅ |
| workflows.spec.ts | 3/3 过 | ✅ |
| chat-records.spec.ts | 2/7 过（:20 页头根容器、:25 筛选栏过；:34 表格渲染、:43 分页、:54/:69/:81 详情面板 5 失败） | ⚠️ **既有数据依赖/时序，非迁移回归**（M4c-host 批次即已知「数据加载时序失败」）。见 §3 定性。 |

## 3. chat-records 5 失败定性（排除迁移回归）

- **error-context 页面快照**：页头 eyebrow「Chat Records · 聊天记录」+ h1「聊天会话查看」、连接状态「未连接（仅手动查询）」、筛选栏（来源/客户端/会话键/Style/起止日期/搜索/重置）、表头十列（ID/会话键/来源/Style/Model/首条消息/轮次/消息数/更新时间/操作）**全部渲染**；唯表格体停在「加载中…」，`.record-row`/`.empty-row`/`.pagination` 未在窗口内出现。
- **类名核对（非漂移）**：读 `ForgeSelf.Web/src/components/chatrecords/ChatRecordsList.vue`——组件完整保留 spec 期望的全部钩子：`.chat-records-list`(:54)、`.records-table`(:60)、`.record-row`(:76)、`.detail-btn`(:99)、`.empty-row`(:105，`records.length===0 && !loading`)、`.pagination`(:110，`total>0`)。`ChatRecordsView.vue` fetchSessions(:125-148) 逻辑正常（API 回来即清 loading 渲染行）。
- **根因**：失败在隔离 e2e 宿主（全新 DB）聊天会话表无数据——`.pagination` 仅 total>0 才渲染、`.record-row` 无数据不出现、`.empty-row` 需 `!loading` 后才渲染；测试 :34 同步计数（非 auto-wait）在 loading 未清时求值，故 hasRows=hasEmpty=false。详情面板三例（:54/:69/:81）依赖 `.record-row` 先出现（15s timeout），无数据则永不出现。
- **判定**：**数据依赖/时序问题，非 M4-M8 迁移引入回归**（选择器与类名均完好）。本批范围不强行改 spec 造绿（那会掩盖真实数据依赖）；如实记录，建议后续将该 spec 改为「先播种会话再断言」或走 live 配置——已超 M8 收口范围。

## 4. CostScope 后端 DI 定性

- **活实例实测**：`GET http://localhost:7301/api/cost-scope/overview`（Bearer 真实 token）返回 500；后端日志（`.temp/dev-stack-backend.log`）真实异常：`System.InvalidOperationException: Unable to resolve service for type 'ForgeSelf.Abstractions.ITurnTelemetryQuery' while attempting to activate 'ForgeSelf.Api.Plugins.CostScope.Controllers.CostController'`，栈经 `PluginServiceRegistry.Resolve` → `PluginAwareControllerActivator.Create`。
- **注册核对**：`ForgeSelf.Api/AppBuilder.cs:195` 已 `AddScoped<ITurnTelemetryQuery, TurnTelemetryQueryService>()`（文件 mtime 10/08，早于 dev-stack 10:03 启动）——**非陈旧二进制**。
- **根因（Cordis 模式架构缺口）**：`CostController`（`Plugins/CostScope/Controllers/CostController.cs:58-59`）构造函数直接注入宿主契约 `ITurnTelemetryQuery`；但 `PluginServiceRegistry.BuildPluginProvider` 构建插件子容器时**按设计不转发宿主契约**（该类注释明示「彻底 Cordis 模式，本注册表不再向子容器转发宿主契约描述符」）；宿主→插件契约种子清单 `HostProvidedServiceContracts`（`PluginManager.cs:248-271`）**不含 ITurnTelemetryQuery**。故子容器解析不到该契约 → 构造激活 500。
- **决策四要素**：
  - 决策：定性为「插件控制器构造注入未登记进 HostProvidedServiceContracts 的宿主契约」的真实缺口。
  - 背景：M5 已在 AppBuilder 注册 ITurnTelemetryQuery，但插件控制器走独立子容器，宿主注册≠插件子容器可见。
  - 理由：重启 dev-stack 无效（子容器本就不含该契约，与二进制新旧无关）；正确做法是 CostController 取 `IContext` 经 `ctx.Get<ITurnTelemetryQuery>()` 运行期解析，而非构造注入，或将该契约补入 HostProvidedServiceContracts。
  - 准则：插件控制器构造注入的宿主契约必须同时落在 `HostProvidedServiceContracts` 清单里，否则 Cordis 子容器激活即 500。
- **处置**：本批范围是「定性」，**不修后端 .cs**；已登记 TODO（来源:输入1 M8）。

## 5. 截图读图视觉核对（Level 3）

| 截图 | 核对项 | 结果 |
|------|--------|------|
| `ai-agent/ai-agent.png` | 三栏布局、版本徽标 v1.7.3、左侧空态（暂无画像数据/暂无 Agent）、composer chips、状态栏（匠人 Lv.1·REST+WS 已就绪·AI 离线）、无白屏无溢出 | ✅ |
| `todo-tracker/v1-list-default.png` | 版本徽标 v1.1.3、状态胶囊组（全部/草稿/就绪/已下发…）、空态（还没有任务/未选中任务）、共0条+刷新、无溢出 | ✅ |
| `home/walkthrough.png` | 页头三件套（logo/导航/右上图标）、问候 hero、指令栏+橙发送、常用功能网格、核心看板各面板空态/离线降级、玻璃卡、无溢出无白屏 | ✅ |

截图均落 `ForgeSelf.Web/screenshots/e2e/<spec名>/`（2026-10-11 13:39-13:43 新鲜产物）。

## 6. 本批改动明细

- **前端视觉代码：零改动**。最小修复边界内无迁移引入回归需修：
  - PromptsView.vue 侧栏 ForgeCard（M4a-1 预案）：因 prompts-skills 7/7 绿，**未改**（:137/:182 保持）。
  - e2e spec 选择器：chat-records 类名经核对未漂移，**未强行改 spec 造绿**。
- 仅产出本证据文件 + 日记回写 + TODO 登记（CostScope DI）。

## 7. 风险与未做

- ⚠️ design-system:211 / mcp-center:106 / quick-links×5 / chat-records×5 失败均非 M8 迁移回归，但各自属后端 flake / 后端协议 / live 配置错配 / 数据依赖——**本批未修**（超出收口验证范围）。
- ⚠️ CostScope `/api/cost-scope/overview` 仍 500（真实架构缺口），待后续按 §4 准则修后端。
- 未做深档全量 e2e（本次为受阻 spec 定向复跑，按快/中档门禁；globalSetup 与 playwright.config 本批未碰，无需深档）。
