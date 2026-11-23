# Mini-Task — sems 终态任务被选中态轮询拖住（输入4 2026-11-23）

> 轻量工件（≤3 文件缺陷修复：Intent/Spec/Plan/Task 合并；规范 §11 裁剪档）。
> 来源：输入4 2026-11-23 17:47 原文「那样吗一直发出上面的两个请求怎么整，运行命令都停止了，异常退出了，还在轮询，这两个请求有什么用」。

## Interaction Design（交互设计）

**无界面交互**——本批只收窄轮询面逻辑（store 条件 + tailPull 集合），不新增/修改任何用户可见 UI 元素；用户可感知行为变化仅为「终态任务不再持续发日志请求」，异常提示复用既有「! 读取日志失败」一行通道，无新增交互状态。

## Intent（为什么/做什么/到什么程度）

用户观察到：命令已停止/异常退出（任务已终态）后，页面仍在持续发出两个轮询请求。经读码定位：

1. `GET /api/commands/{id}/logs?after=N`（1.2s 间隔）——sems 工作台任务日志轮询。**缺陷**：`ensureTaskPolling` 的「选中任务轮询面」只看 `logUnavailable`，不看任务状态（workbenchStore.ts:748/758）⇒ 已终态（done）的选中任务被永久轮询；而终态任务日志缓冲已定（running=false 后缓冲终了），轮询产物为零。属 PC-024 同类（终态分支不收敛）第 4 发、**守卫逃逸面**：PILOT-087 的 G2 守卫覆盖了「404+运行中」与停止四路，未覆盖「选中态循环条件」。
2. `GET /api/smart-terminal/inbound`（2s 间隔）——**宿主主壳**智能终端收件箱（`ForgeSelf.Web/src/composables/useTerminalInbox.ts:36`，全页面常驻）：协议唤起/Agent `st_create_session` 投递「开终端标签」请求，主壳取走并打开。空=正常。设计如此（真源 `Plugins/SmartTerminal/docs/03-架构与接口.md`），**非 sems 范围、非缺陷**；优化项（空闲退避/WS）记 TODO。

做到什么程度：sems 侧——终态任务退出轮询面；收敛后**最多补拉一针**（抓后端尾部行如「进程已退出（PID …）」，保留 AC-F09-2 实测过的停止后退出留痕行为），拉完即停；404 收敛路径不补拉（避免二次 404 噪声）。

## Spec（交互与不变量）

- 不变量（PC-024）：**任务进入终态后，该命令不得再出现在常驻轮询集合**；唯一例外 = 收敛后恰一针补拉（tailPull），完成即清。
- 选中已完成任务：仅选中时一次性拉取（现有 :770 行为保留），不进循环。
- 运行中任务轮询语义不变；404+运行中收敛语义不变（PILOT-087）。
- AC-T4（新增，随 01-requirements AC-F09-8）：任务收敛后推进 ≥4 个轮询周期，该命令日志请求数最多 +1 且不再增长。

## Plan（具体到真实文件）

1. `Plugins/Sems/web/src/workbench/stop-feedback.spec.ts`：新增 2 红例（收敛后恰一针补拉后停；选中已完成任务只拉一针）。
2. `Plugins/Sems/web/src/workbench/workbenchStore.ts`：`tailPull` 集合 + `markTaskDone` 登记 + `ensureTaskPolling` 选中面条件加「非终态或在补拉集」+ 补拉后清理。
3. 真源回写：`01-requirements.md`（AC-F09-8）+ `02-test-plan.md`（T-F09-S7）+ PC-024（第 4 发复发与逃逸分析）+ case 文件。

## Task（Allowed/Forbidden + 门禁）

- Allowed：上列 4 处；Forbidden：不动后端（本轮纯前端轮询面）、不动宿主 useTerminalInbox（另立 TODO）、不动 F25 工具语义。
- Gate1：auto-granted（§8.5 自动迭代 + 输入4 直接指令「怎么整」= 修复指令 + 本会话 never-ask 审批策略；决策留痕同 PILOT-087）。
- 门禁：快档（vitest 全绿 + 插件 build + design:check + hygiene -Staged）；后端未触碰不重跑（改动面=插件 web）。
- 交付：重新打包 sems（新时间码版本）投递 OpenForgeSelfPluginUpdates + 包内容探针复核。

## Evidence / Review

**Evidence（2026-11-23 17:53-17:56，全 Verified）**：
- 红灯（修复前）：新例 2 红——终态选中任务 5 tick 发 6 次日志请求（`expected 6 to be 1`，vitest 2 failed / 53 passed）；其余 53 例不受影响（守卫未伤）。
- 绿灯（修复后）：`pnpm run test` **55/55**（3.6s），含新例「收敛后恰补拉一针后停」「选中已完成只拉一针」。
- 构建：插件 web `pnpm run build` exit=0，`dist/index.js` 171051B @17:55:52（+81B = 轮询面条件 + tailPull）、`style.css` 54617B 不变。
- 真源回写：AC-F09-8（v1.0.3）/ T-F09-S7 / PC-024（复发+lens×2+invariant 增补）/ case 复发追加节。
- 重新打包：见下「交付」。

**Review（八问速审）**：
1. 需求满足？✅ AC-F09-8 红→绿。2. 改动面？✅ 恰 4 文件（store/spec/requirements/test-plan）+ 归档文档，无越界。3. 测试真实？✅ vitest 实跑。4. 是否破坏既有？✅ 53 例全绿；404+运行中收敛语义、停止四路反馈、运行中轮询语义均保持（既有守卫未动且全过）。5. 同类扫查？✅ PC-024 lens 扫过 sems 全部轮询点（唯一常驻轮询面=ensureTaskPolling，已收口；selectTask 一次性拉取非轮询）；宿主 useTerminalInbox=异根因设计内心跳，记 TODO 不扩范围。6. 归档？✅ PC-024 + case 已记。7. 风险？⚠️ 补拉一针若恰逢会话收割完成会吃一次 404（catch 落「! 读取日志失败」一行，去重，可接受——比永续轮询好两个数量级）。8. 结论：**APPROVED**（自审）。

**交付（输入4 重打包）**：`软件工程管理系统-1.3.0.<新时间码>.zip` → OpenForgeSelfPluginUpdates + 包内容探针（dist 新字节 + index.js 含 tailPull 逻辑产物无法直接字符串探测，改验 dist mtime/大小 + 插件 DLL 探针不变重跑）；Sems.dll 与上包同源（后端零改动）。
