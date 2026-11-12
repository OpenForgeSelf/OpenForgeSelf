# Specification

> 阶段：Stage 2｜Task ID：PILOT-plugin-ui-audit｜只出工件，不执行修复。

## Functional Requirements

**FR-1 逐插件核对表**：14 个带前端插件 × 8 项固定检查，逐项给出 `已核 / 未核 / 风险`。

| # | 检查项 | 判定标准 |
|---|---|---|
| C1 | 颜色来源 | 新增裸色值=0；存量裸色值计数（`style-token-baseline.json`） |
| C2 | 亮/暗双态可读性 | 选中行/标签/横幅/正文 对比度 ≥4.5:1（图标/大号 ≥3:1），亮暗各测一次（显式 `html.dark`） |
| C3 | 五态齐备 | 加载 / 空 / 出错 / 无权限 / 正常 各有可见表现 |
| C4 | 异步局部 loading | 点击/查询/切换 ≤100ms 出局部 loading；完成前**不显示空态**；重复点击去重；旧响应不覆盖新选择 |
| C5 | 失败可重试 | 出错态提供重试入口或明确指引，不只报错码 |
| C6 | CT 状态基线 | 关键状态有 story + 行为断言（非仅像素图） |
| C7 | e2e 冒烟 | `e2e/plugins/<id>` 至少 1 条主链路，含运行态断言 |
| C8 | 走查记录 | `Plugins/<X>/docs/` 有亮/暗截图或实测数值记录 |

**FR-2 已知数据（已核，仅 C1/C6/C7）**

| 插件 | web | 裸色值 | CT | e2e | C2–C5/C8 |
|---|---|---|---|---|---|
| DbAdmin | ✅ | 0 | ✅ 25 例 | ✅ | C2 已核（light 10.41 / dark 7.98）；C3/C4 已补（本次两批） |
| SmartTerminal | ✅ | 0 | ✅（2 例，审计过滤断言陈旧） | ✅ | 未核 |
| AIAgent | ✅ | 397 | 部分（MessageText） | ✅ | 未核 |
| Sems | ✅ | 178 | ❌ | ✅ | 未核 |
| DesignSystem | ✅ | 108 | ❌ | ✅ | 未核 |
| AgentHub | ✅ | 104 | ❌ | ✅ | 未核 |
| Home | ✅ | 61 | ❌ | ✅ | 未核 |
| ImGateway | ✅ | 46 | ❌ | ✅ | 未核 |
| CostScope | ✅ | 31 | ❌ | ❌ 缺 | 未核 |
| FileTools | ✅ | 14 | ❌ | ✅ | 未核 |
| McpCenter | ✅ | 13 | ❌ | ✅ | 未核 |
| QuickLinks | ✅ | 8 | ❌ | ✅ | 未核 |
| TodoTracker | ✅ | 7 | ❌ | ✅ | 未核 |
| ToolBridge | ✅ | 1 | ❌ | ✅ | 未核 |

**FR-3 修复清单结构**：每条 = `插件 / 检查项 / 证据（文件:行 或实测值）/ 计划动作 / 验证命令 / 判定标准`。

## Input

插件源码与样式、`style-token-baseline.json`、主题 CSS、CT/e2e 目录。

## Output

`docs/ai/pilot/2026-11-06-plugin-ui-audit/` 下 00–04 工件（本批止于此，不产代码）。

## Business Rules

- 未实际读过的项一律 `Unknown(未核)`，禁止填「应该没问题」。
- 修复优先级：可读性（C2）> 无 e2e（C7）> 状态反馈（C3/C4/C5）> 基线（C6/C8）。
- 每批改动 ≤2 个插件，便于回滚与定位。
- 任何新守卫的存量豁免必须进棘轮基线，禁止一次性全量重写。

## Boundary Conditions

- 插件无前端（如 BrowserUse/MemorySystem/Scheduler/ScriptRunner/SystemMonitor/WorkflowEngine 等）→ 不进入核对表（记 `N/A`）。
- 插件仅在暗色可见（如终端类）→ C2 仍要求亮态 ≥4.5 或给出「亮态禁用」的显式依据。
- 依赖宿主 token 的组件，修复应改 token 引用/主题，不得在插件内私有色值。

## Error Handling

- 实测不达标的项：记 `FAIL` + 实测数值，进入修复清单，不修改规范数值去「让门禁变绿」。
- 无法实测（构建失败/环境缺失）：记 `BLOCKED` + 原因，不跳过。

## Compatibility

- 不得改变插件对外契约（`plugin.json`、工具签名、HTTP 端点）。
- 修复 UI 时保持现有交互语义，仅补反馈与配色。

## Non-functional Requirements

- 单插件核对 ≤30 分钟；批内验证只跑该插件 build + CT/e2e 定向用例 + `pnpm run check`。
- 不引入新依赖。

## Interaction Design（交互设计 · 必写节）

**无界面交互（本批不产 UI 变更）**：本批只产出核对表与修复计划。交互面为审计动作：

- 打开插件页面 → 记录亮/暗两态截图与 `getComputedStyle` 实测对比度 → 填入 C2。
- 点击需异步加载的入口（连接/查询/切换数据源）→ 观察 ≤100ms 是否出现局部 loading、空态是否抢先 → 填入 C4。
- 出错后是否有重试入口 → 填入 C5。
- 完成后把数值写进 `Plugins/<X>/docs/ui-design-brief.md` 与 04-task 的执行记录表。

## Acceptance Criteria

- [ ] 14 个带前端插件全部入表，`C1/C6/C7` 列有实测数据
- [ ] `C2–C5/C8` 未核项明确标 `Unknown(未核)`，无推测
- [ ] 修复清单每条含「文件:行 / 验证命令 / 判定标准」
- [ ] 批次划分含 P1（CostScope e2e、可读性）与 P2（CT 基线、五态）
- [ ] 本批未改动任何业务文件（`git status` 仅新增本目录）

## Unknown

- 各插件 C2–C5 实际表现（需逐插件打开页面实测）——本批不执行。
- CostScope 是否本就不可达（路由/菜单未接）——待核对。
- 是否有插件依赖 Element Plus 暗色之外的第三方样式表——待核对。
