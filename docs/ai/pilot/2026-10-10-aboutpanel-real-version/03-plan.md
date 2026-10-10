# Plan

> Task ID: PILOT-2026-1010-ABOUTVER

## Files To Change
- file: `ForgeSelf.Web/src/components/settings/AboutPanel.vue`
  reason: 替换硬编码 `v0.1.0`；新增 `<script setup>` 挂载取数（import updateApi、ref version、onMounted try/catch）。
- file: `ForgeSelf.Web/src/components/settings/__tests__/AboutPanel.test.ts`（新增）
  reason: 单测覆盖加载态/成功态/失败态三条路径（fetch mock，参照 ApiKeysPanel.test.ts 模式）。

## Implementation Steps
1. 基线门禁（改动前 `pnpm run check` 确认绿）。
2. AboutPanel.vue：script 块实现 + 模板 `v0.1.0` 行改为 `v{{ version ?? '…' }}`。
3. 新增 `__tests__/AboutPanel.test.ts`：三态断言。
4. `pnpm run check` + `pnpm vitest run` 全量。
5. 收尾 05-07 工件 + TODO/日记勾选。

## Test Plan
1. 单测（新）：成功→`v2.4.0.2610101148` 样例值；失败→`v未知`；挂载瞬间→`v…`。
2. 全量 vitest run：确认无回归。
3. 类型+Lint：`pnpm run check`（vue-tsc -b && eslint）。

## Verification

### Build
```bash
cd ForgeSelf.Web && pnpm run check   # vue-tsc -b 覆盖类型构建检查
```

### Unit Test
```bash
cd ForgeSelf.Web && pnpm vitest run
```

### Integration Test
N/A：纯前端展示位，无集成面新增。

### E2E
N/A（记录为证据缺口而非掩盖）：改动未编译进 51888 运行实例（publish/ 禁改 + 禁启停进程），live 走查不可行；e2e 需构造 dev 栈，用户禁止新增进程。以单测+真实调用链同源（UpdatePanel 已 live 验证过同一接口的同字段展示）作 Inferred 证据。

### Other Checks
- ESLint 0 新增 error（check 内含）。
- diff 审查：仅目标文件+新测试文件。

## Plan 偏差记录

| 时间 | 偏差内容 | 原 Plan | 处理方式 |
| --- | --- | --- | --- |
| 2026-10-10 实现 | check 输出中 AboutPanel.vue 38/39 行报 6 条 max-attributes-per-line warning | 预期新增 0 warning | 核对 diff：38/39 行（SVG 属性）未触碰，属基线既有 warning（基线亦列 AboutPanel 同类告警），行位移使其在新计数中归入本文件；全文件 warning 总数稳定 76（改前=改后），0 error，非本次引入。已留档。 |

