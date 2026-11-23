# Evidence — sems 终态任务被选中态轮询拖住修复（输入4 2026-11-23）

> 全部为 Verified（亲自跑过、真实输出）；日志与产物落 `.temp/` 与插件构建目录（不入库，结论摘录于此）。

## 红灯（修复前）

- 新增 2 例（`stop-feedback.spec.ts`）：「任务收敛后推进 ≥4 个轮询周期日志请求不再增长（AC-T4）」「选中已完成任务只拉一针」。
- 修复前实跑：2 例红——终态选中任务 5 tick 发 **6 次日志请求**（`expected 6 to be 1`）；vitest 汇总 **2 failed / 53 passed**。
- 缺陷定位（读码 Verified）：`ensureTaskPolling` 选中态轮询面只看 `logUnavailable`（workbenchStore.ts:748/758），不看任务状态 ⇒ 终态（done）选中任务被永久轮询；终态任务日志缓冲已定（running=false 后终了），轮询产物为零。属 PC-024 同类（终态分支不收敛）**第 4 发**，守卫逃逸面 = PILOT-087 G2 未覆盖「选中态循环条件」。
- 用户实时实例佐证（:51888，只读观察）：`GET /api/commands/6/logs?after=0` 持续发出（任务早已终态）。

## 绿灯（修复后）

- `pnpm run test`：**55/55 全绿**（3.6s），含新例「收敛后恰一针补拉后停」「选中已完成只拉一针」。
- 修复内容：`tailPull` 集合 + `markTaskDone` 登记 + 选中面条件加「非终态或在补拉集」+ 补拉后清理；404 收敛路径不补拉（避免二次 404 噪声）。
- 构建：插件 web `pnpm run build` **exit=0**，`dist/index.js` 171051B（+81B = 轮询面条件 + tailPull）、`style.css` 54617B 不变。
- 真源回写：`01-requirements.md` AC-F09-8（v1.0.3）+ `02-test-plan.md` T-F09-S7 + PC-024（第 4 发复发 + lens×2 + invariant 增补）+ case `2026-11-23-sems-stop-no-feedback.md` 复发追加节。
- 机检：`check-problem-classes.ps1` OK；hygiene -Staged PASS。

## 交付

- 重新打包 `软件工程管理系统-1.3.0.2611231800.zip` 投递 `OpenForgeSelfPluginUpdates\`（SHA256 029B020D…）；dist mtime/大小探针复核通过；Sems.dll 与上包同源（后端零改动）。不动 plugin.json。

## 门禁备注（环境事实，非代码红）

- 首次全量后端在另一并发批（release-pnpm-install-hook-fail）的中档门禁里统一跑，本批改动面=插件 web，按 §5.6 快档+插件 build 收口。
