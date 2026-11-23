# Review — PILOT-087 sems 停止反馈（2026-11-23）

> Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。结论只报事实。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 满足。Intent（01）要求「点停止无论什么结果都有反馈 + 需求加验收标准 + 测试复现」。三点均落地：① 三态 `StopResult`（Stopped/AlreadyStopped/Failed）+ 前端四路分支（stopped/already-stopped/failed/not-running）每路 toast + 日志留痕 + 状态收敛；② `01-requirements.md` F09 增 AC-F09-1…7；③ 红灯先行（stop-feedback.spec.ts 修复前 5 红）。用户原始场景（进程早已停止、点停止无反应）两条路径均有反馈：会话还在 → already-stopped toast；会话已收 → not-running 提示且不发请求；且卡「运行中」的根因（404+运行中永不收敛）已修为一次性核对收敛。
2. **实现是否符合 Spec？** 符合。02-spec 交互设计（行 60：详情区「停止」→ toast + 日志行 + 状态收敛；行 68：沿用 WorkbenchView `.toast` 与 ElMessage 双体系）逐项兑现；RunPanel 路径走 ElMessage（success/info/error 三分支），任务详情路径走 workbench toast。无 spec/impl 分叉。
3. **是否超出了 Scope？** 一处受控扩展，已走闸门1 追加留痕：`workbench.css` toast-host 定位修复（absolute→sticky）。判定依据：02-spec 承诺 toast 为任务详情路径反馈通道，而该通道自 ca1ccc3（2026-11-19）起对所有 workbench toast 都落在首屏之外（走查实测 y=936 > vh=716）——不修则 AC-F09-1「可见反馈」不成立。属「完成已批交互规格所依赖的通道」，非无关重构；根因（布局锚定）与主缺陷（终态不收敛）不同根因，已在 case 文件分节记录并同步 04-task expected_files。除此之外无超范围文件。
4. **是否修改了不应该修改的文件？** 否。`git status` 实测变更集与 04-task expected_files 完全一致（含追加的 workbench.css）；未触碰 F25 工具名/语义、e2e 基建、宿主内核、鉴权、依赖版本；未执行任何发布动作；未触碰用户 :51888 实例（PID 75416 只读 awareness，e2e 端口推导 19000+ 保证隔离）。
5. **测试是否覆盖 Acceptance Criteria？** 覆盖，映射见 `02-test-plan.md` T-F09-S1…S6 与 05-evidence §5 走查表：AC-F09-1（404 一次性 + 空闲可点停，实况 console error 恒 1）、AC-F09-2（三次实测 + 双截图）、AC-F09-3（单测三态 + 实况窗口过窄如实标 Unknown）、AC-F09-5（无请求 + toast，网络面板佐证 + 截图）、AC-F09-7（外部杀 → 留痕行 + 自动收敛 + 截图；404 变体单测覆盖）、failed（单测逐字 + 实况无法制造如实标 Unknown）。
6. **是否存在明显回归风险？** 低。后端仅插件内部契约（`IRunnerService.Stop/StopExternal` 调用方仅本插件 controllers/tools/测试）；F25 工具既有文案逐字守卫（SemsToolExtensionTests）确保 MCP 消费方零感知；前端新增分支不改变既有成功路径；toast-host 改 sticky 与 statusbar 同机制（statusbar 已在该机制下长期运行）；无 DB/依赖/构建配置变更。
7. **是否存在架构不一致？** 无。三态枚举与既有「结构化结果优于布尔」方向一致（对齐 KillTree 已有的 (bool, string?) 形态的收敛）；HTTP 语义 200/200/409 符合「请求成功但无可停对象 vs 请求本身失败」的 REST 惯例；前端沿用既有 toast 体系未新造反馈通道。
8. **Evidence 是否足以证明任务完成？** 足以证明「已实现且关键路径已验证」（05-evidence 全 Verified 或 单测Verified+实况Unknown 成对如实标注，无臆断）。不足以证明「已交付」——闸门2/3（用户验收 + 发布）按流程待用户批准，属流程状态而非证据缺口。

## Requirement Check

PASS — 用户三项要求（验收标准 / 测试复现 / 无论什么结果都有反馈）逐条有产物有证据。

## Scope Check

PASS — 变更集 = expected_files（含走查追加项，追加决策留痕于 04/05/case）。

## Test Check

PASS — 快档门禁全绿：后端过滤 152/152、vitest 53/53（含 6 新例）、插件构建、design:check（toast-host 修复后复跑 PASS）、check-problem-classes OK（41 PC）、hygiene PASS、冒烟 e2e 鉴权用例 1/1（3 例基线红已归属 + 真实报错 + TODO，1 例未跑如实标 Unknown）。

## Architecture Check

PASS — 插件内契约升级，无跨层泄漏；错误语义「可区分 + 可收敛」与 PC-024 类修复方向一致。

## Risk

L2（插件行为面 + 工具语义周边，未触宿主内核/DB/依赖；发布动作未执行故无线上暴露面）

## Findings

### Critical

（无）

### Major

（无）

### Minor

1. already-stopped 实况窗口极窄（后端日志流 EOF ~1s 内收割死进程会话），远程走查无法命中——单测已覆盖该分支，实况标 Unknown；若未来要实况验证，需在宿主内注入可控假死会话（测试钩子），记 TODO 供讨论。
2. 3 例既有 e2e 基线红（2 例陈旧选择器 + 1 例 MCP SSE 解析）非本批引入但持续污染 serial 档冒烟——已记 TODO（补选择器/改 SSE 解析），不属本批修复面。

## Final Decision

APPROVED

<!-- 依据：八问全 PASS、无 Critical/Major findings；Minor 两条均已落 TODO 并在 07 §12 对照清空。
     闸门2（用户验收）与闸门3（提交/发布）按流程待用户批准，不阻塞本 Review 结论。 -->
