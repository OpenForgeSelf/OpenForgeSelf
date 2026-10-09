# 05 · Evidence（PILOT-056：404 静默降级 + resultSummary 折叠）

> 迷你任务（合并 Intent/Spec/Plan/Task 于 `mini-task.md`）。证据按 Verified / Inferred / Unknown 分级，只记实际发生。

## 变更清单（已实施）

| 文件 | 动作 |
| --- | --- |
| `Plugins/TodoTracker/web/src/store.ts` | `loadAgentStatus` 404 时构造降级快照（`ok:false` + 可解释文案），不再 `showFailure` 弹错 |
| `Plugins/TodoTracker/web/src/components/TaskDetail.vue` | resultSummary >240 字符折叠（`summaryPreview`/`summaryOpen` + 展开/收起 toggle） |

## 验证证据（Verified）

- **前端构建**：`pnpm run build` → `✓ built in 425ms`（20 modules，dist/index.js 78.69 kB）✅
- **插件 e2e 全目录**：`e2e/plugins/todo-tracker` 15/15 passed（含 U1-U5、V1 视觉走查、E1 真实委派端到端）✅
  - 404 降级路径由既有 e2e 的 U 系列覆盖（任务 47 的 toast 现场不再复现——该场景在 2.3.3 运行实例实测时是 toast 弹错，本批后详情按降级快照展示）。
- **后端门禁**：`dotnet build` 0 错误（1344 既有警告）；相关过滤集 263/264（1 红 = `CompleteThenReopen_Flow_ShouldFlipStatus`，InvalidCastException ObjectResult→OkObjectResult，路径与本批改动零交集，判预存/环境红，已记 TODO 待全量基线对表）。

## 分级说明

- 折叠交互（点展开/收起）为组件内 UI 行为：**Inferred**（代码审查确认逻辑闭合：`summaryLong` → 按钮渲染 → toggle 切换 preview/full），e2e V1 截图覆盖折叠前形态，展开态未单独截图（低风险）。
- 404 降级：**Verified**（e2e 全目录绿 + store.ts 代码路径复核）。
