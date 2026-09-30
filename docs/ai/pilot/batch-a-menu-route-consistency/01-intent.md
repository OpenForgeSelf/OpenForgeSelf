# 意图与处理规则（Intent & Rules）

> 本文记录批次A「菜单/路由真源一致性」任务的意图与执行规则。本文为 2026-09-30 回溯建档，依据同目录 05/06 与岗位流水记录。

## 1. 本次任务意图

1. **消除菜单/路由双轨漂移**：修复 00 号文档记录的 D1（quicklinks 菜单悬空）、D2（manifest 插件菜单不补发）、D3（纯后端插件声明界面菜单）三类缺陷。
2. **确立单真源派生**：`GET /api/plugin/menu-items` 改为「插件代码声明 + frontend-manifest」合并派生，禁用插件不发、已声明不双发。
3. **固化防漂移门禁**：新增 e2e 一致性 spec + `GetMenuItems` 合并单测 + 铁律19 条款，使任何 route 改名/增删在开发期与 CI 被自动暴露。
4. **岗位流水闭环**：验收包（seq95）→ 任务书（seq99）→ 实现 → 交付回报（seq102）→ 测审派单（seq105）→ 独立核验 + 终审。

## 2. 处理规则（沿用项目既定铁律）

- **测试审查只读**：测审岗不改业务代码（篡改取证须当场还原）、不删/跳测试、不动配置真源。
- **e2e 禁 mock**：走真实宿主（globalSetup 全新起宿主），非 mock 通道。
- **发布走正规通道**：按 plugin-publish-verify 主路径（构建 → publish 宿主 → health → API 面实测 + DLL 探针）。
- **提交纪律**：绝对禁止自动 commit/push；生产代码仅任务书指定文件（T1 = `PluginController.cs`），超范围一律不留。

## 3. 不入库载体（重要背景）

以下路径被 `.gitignore` 忽略，**不随提交入库**，任务上下文不能依赖它们：
- `specs/`（整目录）、`/TODO.md`（根级）
- `.forgeself/memory/`（工作日志）
- `.tmp/`、`.tmp-tests/`、`.tmp-env/`、`.userprofile/`（沙箱绕法产物）

→ 因此本任务全部上下文收敛于 `docs/ai/pilot/batch-a-menu-route-consistency/`（00–07 八件）。

## 4. 文档治理（用户 2026-09-29 拍板规则，回溯适用）

1. `docs/` 其他文档是项目事实标准，可按代码实现更新，与 `docs/ai/pilot` 不冲突；
2. `docs/ai/pilot/<task-id>/` 是一次会话任务全部上下文的唯一入库载体（00–07 八件齐全，由 pre-commit hook 与 CI 强制）；
3. 本目录为批次A 回溯补建：05/06 为任务当时原始产出，00–04/07 为 2026-09-30 依证据建档。
