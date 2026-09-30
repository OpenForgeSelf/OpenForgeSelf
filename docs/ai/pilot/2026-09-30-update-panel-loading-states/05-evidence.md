# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。

## Task

PILOT-051（2026-09-30-update-panel-loading-states）

## Changed Files

- `ForgeSelf.Web/src/components/settings/UpdatePanel.vue`（修改，git diff 确认仅 6 行模板绑定）
- `ForgeSelf.Web/src/__tests__/UpdatePanel.test.ts`（新增，3 条用例）
- `docs/ai/pilot/2026-09-30-update-panel-loading-states/`（00-07 工件，本任务新增）

## Build（类型检查）

Command（`ForgeSelf.Web/`）:

```bash
pnpm run check   # vue-tsc -b && eslint .
```

Result: 部分 PASS（来源等级：Verified）

- `vue-tsc -b` 通过（`&&` 短路成立，eslint 已执行）；本次改动文件 **0 error / 0 warning**（对 check 输出按 `UpdatePanel|__tests__` 过滤零命中，Verified）。
- 命令整体 exit 1：唯一 error 为 `e2e/global-setup.ts:183:5  preserve-caught-error`（catch 内 `throw new Error(...)` 未挂 `cause`），**存量基线红**：该文件由 PILOT-050 提交 288819c（今日重写 global-setup.ts）带入并已入库，本任务未触碰该文件（git status 空）。已按 AGENTS.md §5.6「基线红先对表再判责」记入 TODO（P2，归属 PILOT-050），本次不顺手修。
- 既有 81 warning 与 2026-09-28 基线一致（memory 2026-09-28: 「前端 check 0 error（81 既有 warning）」）。

## Unit Test

Command（`ForgeSelf.Web/`）:

```bash
pnpm run test   # vitest
```

Result: PASS（来源等级：Verified）

```text
Test Files  47 passed (47)
     Tests  493 passed (493)
   Duration  71.82s
```

- 新增 `src/__tests__/UpdatePanel.test.ts` 3/3 绿：
  1. 弹窗确认重启后（stage=applying）仅「重启并更新」转圈，检查/下载按钮只禁用不转圈（回归用户上报缺陷）
  2. 下载阶段（stage=downloading）检查/下载按钮均为禁用态且不转圈
  3. 检查更新进行中只有自身按钮转圈，完成后恢复
- 存量用例无回归（493 全绿；09-28 基线 477、09-29 B9 489，数量递增与新增一致）。

## Integration Test

Result: N/A（前端组件单测范围，无后端/集成入口涉及）

## E2E

Result: N/A（未触发 §5.6 深档条件：未改 e2e 基建、未发版/tag、未改插件；既有 update e2e 与本改动无关联）

## Static Analysis

Result: PASS（本任务文件 0 error 0 warning，Verified；整体 exit 1 归因存量 global-setup.ts，见 Build）

## Screenshots

N/A（无 UI 截图条件；模板绑定差异以 git diff 为准，见下）

```diff
-        <el-button :loading="checking || busy()" @click="onCheck">检查更新</el-button>
+        <el-button :loading="checking" :disabled="busy()" @click="onCheck">检查更新</el-button>
-            :loading="downloading || busy()"
-            :disabled="isReady()"
+            :loading="downloading"
+            :disabled="busy() || isReady()"
```

## Known Limitations

- 未做浏览器级人工点验（未启动宿主实例；交互语义由组件单测覆盖：loading/disabled 属性断言）。
- 无 UpdatePanel 既有组件测试可参照的渲染断言，本测试聚焦缺陷回归面（三按钮 loading/disabled），未覆盖轮询/重启等待等既有流程（非本任务范围）。

## Unresolved Issues

- 存量：`e2e/global-setup.ts:183` eslint error（PILOT-050 引入，已记 TODO P2）。
