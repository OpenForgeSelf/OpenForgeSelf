# Intent

> 阶段：Stage 1｜Task ID：PILOT-design-gate-contrast

## Problem

1. DbAdmin 暗色下连接名对比度 1.10:1（已修 token ramp），但**门禁没人拦**：静态守卫只看「是不是裸色值 / 是不是白名单 token 族」，不看「这组前景/背景能不能读」。
2. 对比度守卫只写在 vitest 里，`pre-commit` 与 CI 都不跑 vitest ⇒ 提交链上不存在该门禁。
3. `AGENTS.md` §8 承载了过多细节（双态验证、异步 loading 的具体阈值与做法），与「AGENTS 只放每次必守规则、细节落对应文件」的分层原则冲突。

## Why

用户判断「统一 token 就不会看不见」，实际反例是 DbAdmin：token 化 100%，却因暗色 token 取值与语义配对错误导致不可读。⇒ 门禁必须判「配对后的可读性」，而不是判「有没有硬编码」。

## Expected Outcome

- 提交（pre-commit）与 CI 都会因「主题对比度不足」或「把背景级 token 当文字色」而失败，与是否有人记得跑 vitest 无关。
- 新插件/旧插件改样式时，同一套门禁自动生效，无需额外约定。
- AGENTS §8 只留三步门禁与引用；细则在 `DESIGN.md` §Colors 与 `ui-ux-design` 清单。

## Constraints

- 不改任何业务 UI（本轮只动门禁脚本、CI/pre-commit、规范文档）。
- 不新增依赖；不改主题取值（token ramp 已在上一批修好）。
- 不引入「全量 vitest 进 pre-commit」（太慢）；对比度校验做成独立快脚本。
- 基线棘轮只允许下降，不允许抬高。

## Success Criteria

| 判据 | 目标 |
|---|---|
| 故意把 `#FDE68A` 写进暗色 `light-8` | `design:check` 退出码非 0，日志明确报对比度不足 |
| 故意写 `color: var(--el-color-primary-light-7)` | `design:check` 退出码非 0，报「背景级 token 不得当文字色」 |
| 现状代码 | `pnpm run design:check` 一次 PASS（规则 E 命中 0） |
| CI | `frontend-check.yml` 含 `pnpm run design:check` 步骤 |
| AGENTS §8 | 三步门禁每步 ≤1 行 + 指向真源文件 |
