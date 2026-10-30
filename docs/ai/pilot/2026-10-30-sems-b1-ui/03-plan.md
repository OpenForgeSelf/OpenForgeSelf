# Plan

> 阶段：Stage 3｜Task ID：2026-10-30-sems-b1-ui

## Files To Change

- file: `Plugins/Sems/web/src/types.ts`
  reason: 增加规格、关联、覆盖的 camelCase 类型
- file: `Plugins/Sems/web/src/specOps.ts`
  reason: 校验、请求体、列表阶段、写队列
- file: `Plugins/Sems/web/src/specOps.spec.ts`
  reason: 锁住 AC3、AC4、AC7–AC10
- file: `Plugins/Sems/web/src/specApi.ts`
  reason: 调用现有九个规格动作
- file: `Plugins/Sems/web/src/SpecPanel.vue`
  reason: 卡片内规格区
- file: `Plugins/Sems/web/src/ProjectCard.vue`
  reason: 增加「规格」开关并挂上 SpecPanel
- file: `Plugins/Sems/web/src/SemsView.vue`
  reason: 页头说明补上规格，不改主按钮
- file: `ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`
  reason: 一条规格主链路；无令牌用例补上规格路径
- file: `Plugins/Sems/docs/requirements.md`
  reason: B1 现状补一句界面位置，仍标部分完成

## Implementation Steps

1. 先写 `specOps` 与其单测，再写 `specApi` 与 `SpecPanel`。
2. 挂到 `ProjectCard`，不改命令区按钮的 title「管理命令」。
3. 跑插件 `pnpm test` 与 `pnpm build`。
4. 跑这一条 e2e。环境允许时用 dev-stack 点同一条链路。
5. 按实跑结果写 05、06、07。不提交，除非用户另说。

## Test Plan

1. `Plugins/Sems/web` 的 vitest 覆盖请求体与三态。
2. `pnpm build` 确认新组件进入 `web/dist/index.js`。
3. Playwright 只跑规格主链路这一条，并与 GET specs / coverage 对账。
4. 后端 Sems 过滤集本任务不改 C# 时不作为新缺陷的必跑项；若 e2e 全局构建顺带编译宿主，以其日志为准。

## Verification

### Build

```bash
cd Plugins/Sems/web
pnpm build
```

### Unit Test

```bash
cd Plugins/Sems/web
pnpm test
```

### Integration Test

```bash
cd ForgeSelf.Web
pnpm exec playwright test e2e/plugins/sems/sems.spec.ts -g "规格主链路"
```

全量 sems e2e 不在本计划内。dev-stack 走查单独记录，不替代上面的命令。
