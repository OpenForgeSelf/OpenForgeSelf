# AI-Native Pilot Result — PILOT-087 sems 停止反馈（2026-11-23）

> 任务结束强制格式。状态只报事实。

## 1. Repository Understanding

我确认了：sems 是「宿主页面内远程加载」的插件（`Plugins/Sems/` + plugin.json，前端 dist 由宿主静态供给，运行端口/令牌见 `.temp/dev-stack.json`）；命令会话与日志缓冲都在**宿主内存**（宿主/插件重启即消失，完成记录与日志由前端本地留存）；停止链路横跨 RunnerService（进程树）→ Controllers（HTTP）→ ToolExtensions（MCP F25）→ workbench/RunPanel（UI 反馈）四层；workbench 有独立 toast 体系（`.wb__toast-host`/`.toast`），与 RunPanel 的 ElMessage 并存。依据：00-repository-understanding.md + 走查实测。

## 2. Selected Task

用户输入1（缺陷报告 + 指令）：sems 运行功能点停止没反应（进程早已停止）——在需求加验收标准、加测试复现、完善功能：**点击停止命令，无论什么结果都要有反馈**。PC-024 缺陷类任务，走 problem-class-closure 增强。

## 3. Changed Files

与 04-task expected_files 一致（走查追加 workbench.css 一项，已留痕）：

- 后端：`Plugins/Sems/Services/RunnerService.cs`（StopOutcome/StopResult 三态 + KillTree 回传 (Ok, Error)）、`Controllers/ProjectCommandsController.cs` / `RunsController.cs`（200/200/409 + outcome 字段）、`ToolExtensions.cs`（failed 新文案，既有文案逐字不变）
- 前端：`web/src/workbench/workbenchStore.ts`（stopTask 四路分支 + 404+运行中一次性核对收敛）、`views/ProjectTasksView.vue`（注释 + 按钮语义）、`RunPanel.vue`（三态 ElMessage）、`types.ts`、`confirmOps.ts`（确认流兼容）、`web/src/workbench/workbench.css`（toast-host absolute→sticky，走查发现伴生缺陷）
- 测试：`ForgeSelf.Api.Tests/Plugins/Sems/*Tests.cs`（三态契约 + outcome + F25 逐字）、`Plugins/Sems/web/src/workbench/stop-feedback.spec.ts`（新建 6 例）
- 文档：`Plugins/Sems/docs/01-requirements.md`（F09 + AC-F09-1…7）、`02-test-plan.md`（T-F09-S1…S6）、`docs/11-troubleshooting/`（PC-024 更新 + 新 case）、`docs/ai/pilot/2026-11-23-sems-stop-feedback/00..07`

## 4. Validation

Build:

- ✅ 后端 `dotnet build` 0 错误（警告全为存量）— Verified
- ✅ 插件前端 `pnpm run build`（toast-host 修复后复建，style.css 54.62 kB / index.js 170.97 kB）— Verified
- ✅ `pnpm run design:check` PASS（无新增违规，棘轮 -733）— Verified
- ✅ `pwsh scripts/check-commit-hygiene.ps1` PASS（staged 新增行零违规）— Verified

Unit Test:

- ✅ 后端过滤档 `dotnet test --filter "FullyQualifiedName~Plugins.Sems"`：**152/152**（含三态 200/200/409、already-stopped、F25 逐字守卫）— Verified
- ✅ 插件前端 vitest：**53/53**（含 stop-feedback 6/6；修复前 5 红先行）— Verified

E2E:

- ✅ 冒烟定向 `e2e/plugins/sems/sems.spec.ts`：鉴权用例（含新增三端点 401 面）**1 passed** — Verified
- ⚠️ 同文件 3 例**基线红**（2 例陈旧 `.sems` 选择器、1 例 MCP 网关 SSE-vs-JSON），归属证据链完整、零写操作、非本批引入；1 例因 serial 断后未跑（Unknown）。详见 05-evidence §4。

## 5. Evidence

`05-evidence.md`：红灯 5 红先行（R1/R2）→ 绿灯门禁全表 → F25 逐字核对 → 冒烟 e2e 归属表 → 起源码本地走查逐 AC 表（AC-F09-1/2/5/7 实况 Verified + 双截图已读图；AC-F09-3/failed 单测 Verified + 实况 Unknown 如实成对标注）→ 走查发现（toast-host 首屏外）随批修复闭环 → 清测试数据记录。截图：`ForgeSelf.Web/screenshots/dev-stack/sems-walkthrough-*.png`（4 张）。

## 6. Review

`06-review.md` 八问全 PASS、无 Critical/Major → **APPROVED**（闸门2 验收与闸门3 提交/发布按流程待用户批准，不阻塞 Review 结论）。

## 7. Risk

L2。残余风险：① already-stopped/failed 两分支实况无法命中（窗口机制性过窄 / 无法有机制造），仅单测覆盖——已在 05 §5、06 Minor 成对标注；② 3 例 e2e 基线红未修（非本批面，TODO 已记）；③ 闸门2/3 未执行（等用户），发布前用户实例不会获得修复。

## 8. Problems Found

1. 主缺陷：404+运行中永不收敛 → 任务卡「运行中」+ 停止反馈不可区分（已修，PC-024/case 归档）。
2. 伴生缺陷：workbench toast-host absolute 锚文档底 → 所有 workbench toast 首屏外（已随批修，sticky）。
3. 既有 e2e 基线红 3 例（陈旧选择器 ×2、SSE 解析 ×1）→ TODO。
4. already-stopped 实况验证需宿主测试钩子 → TODO（候选，供讨论）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS（红→绿） |
| Test → Evidence | PASS（快档 + 六步①②③；④⑤待批准） |
| Evidence → Review | PASS |

### 9.5 本批自复盘（必填）

- **经验提取（保留打法）**：
  1. **「无论什么结果」类需求先穷举终态再设计契约**——先把后端出口枚举成三态再动手，前端分支与文案从同一张表派生，避免逐态打补丁。
  2. **走查必须在真实滚动窗口量 toast 可见性**——本批走查用 rect 对 vh 实测，抓出「有 toast 但看不见」这类断言式测试测不出的缺陷；已在 case 文件沉淀为走查清单条目（toast/浮层类必查 rect∈viewport）。
  3. **远程驱动浏览器的 toast 捕获**：click+waitFor+settle+截图合成一个 `browser_run_code_unsafe` 片段，避开工具往返超过 toast 生命期；截图路径用工具相对名（落 profile 根）再拷贝入仓。
- **待改进**：
  1. 早期把「already-stopped 实况」当可现造场景规划走查，实际窗口 ~1s——机制修正：**走查设计先测时序窗口**（后端收割节奏 vs 工具往返延迟），不可现造的分支直接按「单测 + Unknown」规划，不走弯路。
  2. e2e 冒烟在基线红文件上用 serial 跑，一红断后浪费一轮才发现后续用例没跑——机制修正：定向冒烟前先 `--list` 或按 describe 分段跑，基线红用例用 `--grep-invert` 排除（本批后半段已如此执行）。

## 10. 问题类归档

- PC-024（前端轮询错误处理：吞错/不收敛类）——本批为其第 3 次复发（首次 2026-11-06 DbAdmin，二次 sems 日志轮询，本次 sems 停止链路同根因面），case：`docs/11-troubleshooting/cases/2026-11-23-sems-stop-no-feedback.md`；守卫升级：G2 `stop-feedback.spec.ts`（6 例，含 404+运行中收敛与四分支反馈）+ 后端三态契约测试。机器校验 `check-problem-classes.ps1` OK（41 PC）。
- 伴生 toast-host 定位缺陷：**不新建 PC**——一次性布局锚定错误，单点修复 + 走查清单条目（case 文件「走查发现」节）承载；若同类（容器内浮层定位错锚）再现则按二次复发立 PC 并升级 ≥G2。

## 11. 重构提案

R1（局部，07 披露即可，不另开闸门）：
1. `RunnerService` 内 `Stop`/`StopExternal` 与 `KillTree` 已收敛为「结构化结果」形态；建议后续把插件内其余「布尔 + 吞错」出口（如会话查询）按同一模式收敛，消除 404 语义过载（本批已把「无会话」从 404 移到 200 already-stopped，剩余面小）。
2. workbench toast 与 RunPanel ElMessage 双体系并存是历史产物；若后续做反馈通道统一，建议以 workbench toast-host（已修 sticky）为唯一宿主内通道，ElMessage 保留给独立整页场景——牵涉多插件，超本批面，不动。

R0 项：无其他。

## 12. 悬空教训清单（必须为空）

（空——本报告所有「发现的问题/遗留/建议」均已落成：PC-024/case 归档 ×2 项；TODO.md 条目 ×3（e2e 基线红修复、e2e serial 分段执行、already-stopped 测试钩子候选）；无未落成项。）
