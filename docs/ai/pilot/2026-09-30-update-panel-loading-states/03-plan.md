# Plan

> 阶段：Stage 3｜必须具体到真实文件路径。
> Task ID：PILOT-051

## Files To Change

- file: `ForgeSelf.Web/src/components/settings/UpdatePanel.vue`（模板 317 / 334-341 行附近）
  reason: 「检查更新」按钮 `:loading="checking || busy()"` → `:loading="checking" :disabled="busy()"`；「下载更新」按钮 `:loading="downloading || busy()" :disabled="isReady()"` → `:loading="downloading" :disabled="busy() || isReady()"`。修复共用 loading 缺陷。
- file: `ForgeSelf.Web/src/__tests__/UpdatePanel.test.ts`（新增）
  reason: 按项目既有面板测试模式（AppearancePanel.test.ts：mount + vi.mock('element-plus')）新增回归测试，覆盖 stage=applying 时三按钮 loading/disabled 断言。

## Implementation Steps

1. 编辑 `UpdatePanel.vue` 模板两处 el-button 绑定（见上）。
2. 新建 `ForgeSelf.Web/src/__tests__/UpdatePanel.test.ts`：
   - `vi.hoisted` 定义 updateApi mock（getStatus/getConfig/check/download/getProgress/saveConfig/apply）。
   - `vi.mock('@/services/updateApi')` 与 `vi.mock('element-plus')`（保留实际组件，仅覆写 ElMessage/ElMessageBox）。
   - mount UpdatePanel；mock `getStatus` 返回 `state.status='ready'` 且含 check（hasUpdate=true），使「下载更新」「重启并更新」按钮均渲染。
   - `ElMessageBox.confirm` mock 返回 resolved；`apply` mock 返回 `{ status:'applying', progress:0 }`。
   - 点击「重启并更新」，flushPromises 后断言：重启按钮 loading=true；检查/下载按钮 loading=false 且 disabled=true。
3. 运行 `pnpm run check` + `pnpm run test` 验证。

## Test Plan

1. 新增用例：重启确认后（stage=applying）仅「重启并更新」转圈，「检查更新」「下载更新」不转圈（disabled）。
2. 存量测试全量回归（`pnpm run test`）。

## Verification

### Build

```bash
cd ForgeSelf.Web
pnpm run check
```

### Unit Test

```bash
cd ForgeSelf.Web
pnpm run test
```

### Integration Test

N/A（前端组件单测范围，无后端/集成入口需要）

### E2E

N/A（本次为设置页组件加载状态缺陷，未触发 §5.6 深档条件：未改 e2e 基建/未发版；既有 e2e 与本改动无关联）

### Other Checks

- eslint 随 `pnpm run check` 覆盖。

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
