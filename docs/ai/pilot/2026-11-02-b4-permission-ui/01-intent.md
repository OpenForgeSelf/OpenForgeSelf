# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」
> Task ID：PILOT-B4-UI ｜ 日期：2026-11-02

## Problem

权限策略目前不可见、不可配置：`HighRiskToolPolicy` 的七工具 ask 名单硬编码在后端，用户在 UI 上看不到「哪个 Agent 会因什么规则被审批/拒绝」，也无法按 Agent 调整；审计流水（策略决策 + 人工审批）没有查询入口。

## Why

B4 权限可配置化的用户面：后端（verify-dev 链）已把 PermissionProfile 数据化并提供 CRUD 与审计端点，但没有 UI 这些能力对用户等于不存在。设置页需要补「权限策略」分类，让策略矩阵、白名单、审计可查可改——这是该批功能交付的必要闭环（lead 开工令 seq6）。

## Expected Outcome

- 设置页新增「权限策略」面板：策略卡片（agentId 身份）+ 规则矩阵（Allow/Ask/Deny）+ 规则级白名单编辑 + 显式保存 + 审计日志双视图。
- `features.ts` 入口 + `?cat=` 深链直达。
- 零配置行为不变（兼容红线）：UI 以「内置默认」只读说明卡如实呈现，不合成策略数据。
- 快档三门禁绿 + 真实走查截图。

## Constraints

- 契约先行：仅对接 verify-dev 冻结契约（camelCase JSON；实际线上枚举为数值，见 02-spec U2 对表记录）。
- 不改后端（`ForgeSelf.Api/**` 非 UI 写面；`PermissionsController` 只读对表）。
- 不动既有设置分类行为；深链为纯增量。
- 门禁档位：快档（本批不触宿主源码/仓库脚本，不跑中档全量 dotnet test）。
- dev-stack 停/启只走 `scripts/dev-stack.ps1`；撞锁报 lead。

## Success Criteria

- AC1..AC10 全绿（见 02-spec，05-evidence 逐字对应 + 走查截图实证）。
- `pnpm run check` / `test` / `design:check` 三门禁 0 错误、棘轮不涨。
- 走查截图 ≥5 张落 `ForgeSelf.Web/screenshots/dev/b4-permission-20261102/` 并读图核对。
