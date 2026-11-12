# 045 · Frontend Check 工作流的 pnpm 安装（frontend-check-pnpm）

> 状态：随 2026-11-05-frontend-check-pnpm 轻量任务入库（CI 债修复）。

## 功能点登记（与 pilot mini-task/04 一致）
- INFRA-1 pnpm 安装顺序契约：CI workflow 中包管理器安装步（pnpm/action-setup@v4，版本对齐 ForgeSelf.Web/package.json 的 packageManager）必须位于 setup-node（cache: "pnpm"）**之前**；禁止依赖 corepack enable 作为 pnpm 安装手段（新 runner 不可靠）。当前适用：.github/workflows/frontend-check.yml（release.yml 已同参）。

## 用户动线
无用户界面；效果 = push 后 GitHub Actions「Frontend Check」稳定通过 pnpm 安装与缓存步骤。

## 验收基准
pilot 2026-11-05-frontend-check-pnpm AC1/AC2（本地 frozen-lockfile + pnpm run check 绿；CI 运行时随下次 push 收口）。