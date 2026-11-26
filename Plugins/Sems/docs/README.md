# SEMS 文档

本目录是软件工程管理系统（插件 id：`sems`，路由：`/sems`）的产品文档。

## 本轮有效文档

- [01-requirements.md](./01-requirements.md)：需求与验收标准 v1.0.5（2026-11-26，改 I06 状态栏和 I07 确认范围；v1.0.4 起「运行」页为一屏）。测试、前后端以本文为准。
- [02-test-plan.md](./02-test-plan.md)：测试计划 v1.0.5（2026-11-26，已与需求 v1.0.5 对齐）。覆盖 F01–F26、I01–I07 共 33 项；先不锁门禁。

## 仅作对照，不再作为需求来源

- `requirements.md`：2026-10-26 的「AI 原生开发管理平台」草案，范围已超出本产品定位，不纳入本轮。
- `refactor-notes.md`、`ui-design-brief.md`：1.3.0 工作台交接实现说明。其中把「插件」屏做成 22 个 `sems_*` 工具清单、把「依赖」屏映射为运行命令，已被 v1.0 纠正。

实现对照基线：`main` [`900a528`](https://github.com/OpenForgeSelf/OpenForgeSelf/commit/900a528)。
