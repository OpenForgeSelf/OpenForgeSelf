# M4c P5 插件前端迁移证据（AgentHub / CostScope）

- 日期：2026-10-11
- 范围：两插件 `web/src/**` 冻结模式迁移（token 化移除 fallback + CostScope 补版本徽标 + 去 ⚠ 装饰）
- 模式：插件冻结——禁 import 宿主组件；`--el-*` token 直接引用；保留全部功能/类名/可访问名
- 环境：复用既有 dev-stack（后端 :7301 pid 97516 / 前端 :7399 pid 79692），未起停宿主；e2e 走 globalSetup 隔离实例（:7002），E2E_SIGN_EXE=false

## 1. 改动清单（绝对路径）

### AgentHub（仅样式 fallback 剥离；版本徽标/三态本就具备）
- `Plugins/AgentHub/web/src/AgentHubView.vue`
  - 移除全部 `var(--el-x, #hex)` 颜色 fallback（67 处 hex → 1，再改注释）；结构 fallback `var(--el-font-family, system-ui, sans-serif)` 保留
  - 注释「禁用裸 #fff」改写为「禁用裸白色值」（去掉注释里残留 hex）
  - 既有版本徽标 `<span class="ok-version">v{{ pluginVersion }}</span>`（`fetchPluginVersion('agent-hub')`）保留未动
- `Plugins/AgentHub/web/src/AgentHubAgentCard.vue`：15 处 `, #hex)` fallback → 空（健康灯/能力矩阵/交互口着色全走 `--el-*`）；✓/✕ 功能字形保留
- `Plugins/AgentHub/web/src/AgentHubAgentForm.vue`：4 处 fallback 剥离
- `Plugins/AgentHub/web/src/AgentHubEventStream.vue`：18 处 fallback 剥离（事件语义着色 border-left/background）

### CostScope（fallback 剥离 + 补版本徽标 + 去 ⚠）
- `Plugins/CostScope/web/src/CostScopeView.vue`
  - 4 处 `, #hex)` fallback 剥离
  - **新增版本徽标**：`<h2>成本观测 <span v-if="version" class="cs-version">v{{ version }}</span></h2>`；script 加 `onMounted` 拉 `fetchPluginVersion('cost-scope')`；新增 `.cs-version` 样式（`--el-fill-color-light`/`--el-text-color-secondary`）
- `Plugins/CostScope/web/src/http.ts`：新增 `fetchPluginVersion(pluginId)`——裸 fetch `/api/plugin`（剥信封取 data 数组），失败/异常返回空串；**不撞** `CostScopeWebAssetTests` 守卫（该守卫仅禁 `api/usage*`/`api/chat-records`，不禁 `/api/plugin`；且仍保留 `COST_SCOPE_PREFIX='/api/cost-scope'`）
- `Plugins/CostScope/web/src/components/DashboardPanel.vue`：10 处 fallback 剥离；去覆盖度横幅装饰字符 `⚠`（保留 `mainChatObservable`/`costIsLowerBound` 守卫要求的字符串与 `.dash__warn` 着色）
- `Plugins/CostScope/web/src/components/PricePanel.vue`：3 处 fallback 剥离
- `Plugins/CostScope/web/src/components/BudgetPanel.vue`：6 处 fallback 剥离（保留 `shouldAlert` 横幅逻辑，未加 Notification/ElMessage）
- `Plugins/CostScope/web/src/components/TracePanel.vue`：8 处 fallback 剥离（保留 `correlationNote` 守卫要求字符串）

未动：`web/dist/`、后端 `.cs`、路由/后端、其他插件、宿主三组件、workshop-forge.css。

## 2. 验证结果

| 项 | 结果 | 等级 |
|----|------|------|
| AgentHub `pnpm run build` | PASS，540ms，dist/index.js 50.84 kB / style.css 14.76 kB（14 modules） | Verified |
| CostScope `pnpm run build` | PASS，462ms，dist/index.js 28.19 kB / style.css 4.55 kB（18 modules） | Verified |
| design:check style-tokens | 合计 1470（基线 2501，较基线 -1031），≤2484，PASS | Verified |
| design:check compliance | a=209(≤244) b=0 c=0 d=0(基线 d=1→0)，PASS 无增量 | Verified |
| `pnpm run check` | exit=0，0 errors；145 warnings 全部位于其他插件 SVG（`Select-String AgentHub\|CostScope` 零命中），本批 0 新增 | Verified |
| `npx vitest run --maxWorkers=2` | 70 files / 792 tests 全绿，0 failed（129s） | Verified |
| agent-hub e2e（隔离实例） | 5 passed（42s）；dist/index.js 200 text/javascript；`.ok-root`/`.ok-title`(/^Agent 中枢)/`.ok-stats`/`.ok-scan-dirs`/`.ok-alert--ok` 断言全过；唯一 400=重复 vendor 唯一约束预期断言 | Verified |
| agent-hub 截图读图 | `screenshots/e2e/agent-hub/agent-hub-visual.png`：页头「Agent 中枢 v1.0.10」版本徽标在、统计栏、三页签、金底主按钮、附加扫描目录、虚线空态「还没有登记任何 agent」；无横向溢出、无白屏、宿主米色/金主题已级联 | Verified |
| CostScope 路由 200 | 前端 `http://localhost:7399/cost-scope` → 200；插件产物 `http://localhost:7301/plugins/cost-scope/web/dist/index.js` → 200 text/javascript | Verified |
| CostScope 数据 API | `GET /api/cost-scope/overview` → 500，后端日志根因 `Unable to resolve service for type 'ITurnTelemetryQuery' ... CostController`——宿主后端 DI 未注册该依赖，**后端装配问题，与本批前端改动无关**（未碰任何 .cs）；此时面板走 `.dash__error` 错误态 | Inferred（后端）/ Unknown（数据渲染视觉） |

## 3. 决策（四要素）

- 决策：迁移深度 = 「机械剥离 `var(--el-x, #hex)` 颜色 fallback + CostScope 补版本徽标 + 去 ⚠ 装饰」，不重排既有布局、不重写原生 table/表单。
  - 背景：两插件已大量使用 `--el-*` 变量，主要缺口是遍布的 `, #hex` fallback（被 style-tokens 守卫计数）；CostScope 缺版本徽标（铁律13）；DashboardPanel 有一个 ⚠ 装饰。
  - 理由：功能保留 > 严格视觉拷贝（Main 验收条）；agent-hub e2e 选择器契约密集（`.ok-*` 类名），重排布局风险远大于收益；宿主 :root 必然级联 `--el-*`（截图已证），fallback 删除不影响渲染。
  - 准则：正则 `,\s*#hex` → 空 仅剥颜色 fallback（结构 fallback `, 6px`/`, monospace`/`, system-ui, sans-serif` 保留）；类名/可访问名/守卫要求字符串（`mainChatObservable`/`costIsLowerBound`/`correlationNote`/`shouldAlert`）一律保留。

## 4. 未做 / 风险

- 未重排布局、未把 CostScope 原生 `<table>/<button>/<input>` 改成玻璃卡组件（功能保留优先；CostScope 无 e2e，视觉改造收益低、回归风险高）。
- 风险1：CostScope 数据 API 在 dev-stack 隔离实例下 500（后端 DI 缺 `ITurnTelemetryQuery` 注册），**非本批前端回归**（未碰 .cs）；故 CostScope 面板的数据态视觉未在运行实例取证，仅以 build + 路由/产物 200 + 守卫 + 单测为据（任务书对 CostScope 即以此为准）。该 DI 缺注册疑与并行 M5/宿主接线批次相关，需后端批次对表。
- 风险2：agent-hub e2e 全部通过（UI 渲染/写往返/设置持久化/视觉溢出），无后端 RPC 阻塞。
- 未停/启/杀任何用户宿主进程（dev-stack 97516/79692、:51888 均未动）；未做任何 git 状态操作。
