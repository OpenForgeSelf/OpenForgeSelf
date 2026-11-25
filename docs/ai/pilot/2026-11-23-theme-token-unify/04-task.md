---
task_kind: refactor
risk: { level: L2, triggers: ["ForgeSelf.Web/src/**（115+ 文件机械替换）", "Plugins/*/web/src/**", "scripts/**", "9 自造 modal 迁 el-dialog"], raised_by_agent: false }
gate1: { mode: auto, at: 2026-11-23T15:00, ref: "会话审批策略=never ask（自动迭代，AGENTS §8.5）；push/发布仍禁" }
expected_files:
  - ForgeSelf.Web/src/**
  - Plugins/AIAgent/web/src/**
  - Plugins/DesignSystem/web/src/**
  - Plugins/Sems/web/src/**
  - Plugins/ImGateway/web/src/**
  - Plugins/SmartTerminal/web/src/**
  - Plugins/Home/web/src/**
  - Plugins/FileTools/web/src/**
  - Plugins/ToolBridge/web/src/**
  - Plugins/McpCenter/web/src/**
  - Plugins/TodoTracker/web/src/**
  - Plugins/AgentHub/web/src/**
  - Plugins/QuickLinks/web/src/**
  - scripts/check-theme-tokens.mjs
  - scripts/custom-tokens-allow.json
  - scripts/token-migration-map.json
  - scripts/migrate-theme-tokens.mjs
  - scripts/element-tokens.json
  - scripts/gen-element-tokens.mjs
  - scripts/fixtures/theme-tokens-anti-patterns.vue
  - scripts/install-git-hooks.ps1
  - scripts/hooks/pre-commit
  - package.json
  - .github/workflows/ci.yml
  - docs/ai/pilot/2026-11-23-theme-token-unify/**
feature: none
rollback: 每域独立 commit，机械替换可单域 git revert；themes 供给先行保证任一时刻新旧 token 可用
---

# 04 Task — 统一主题修复批（el-token 全项目收敛 + 弹窗标准化）

> 闸门1：用户已将会话审批策略设为「never ask the user for approval」（自动迭代授权，AGENTS §8.5）；本批按 03-plan 执行，push 与发布动作仍禁。

## Allowed
1. 按映射表机械替换全部域的 token 引用/定义；映射表增量与豁免册定稿。
2. 9 个自造 modal 迁 `<el-dialog>`（视觉 token 引用保留）。
3. `scripts/` 新增/修改：迁移器两件入库、检查器 fileExemptions 小扩展、check 链接线、pre-commit/CI 接线；`scripts/install-git-hooks.ps1` Windows 断言短路修复（同类扫查命中：阻断本批 hook 验证的既有缺口）。
4. `package.json` 增加 check:theme / check:theme:gen / check:theme:demo 别名并接入 check 链。
5. pilot 工件 00-07 与走查截图（`ForgeSelf.Web/screenshots/`）。

## Forbidden
- push / tag / 本地目录发布（须用户明示；DesignSystem 四层门禁第④层留痕待授权）。
- 整文件覆盖备份双改文件（57 个双改文件只走映射器重放 + 手工合并）。
- 搬备份 `--canvas-*` 画布调色板块（属 theme-canvas-unify 在途批）。
- 触主检出在途件（DESIGN.md/package.json 修改、审计批 pilot 目录内容）；升级 Element Plus；改主题视觉基调/品牌色值（等值迁移除外）。
- 删测试以求绿；改 e2e/global-setup/playwright 配置（深档触发件保持不动）。

## 验证命令
```bash
node scripts/check-theme-tokens.mjs                                    # 每域：计数只许降，收口 0
node scripts/check-theme-tokens.mjs scripts/fixtures/theme-tokens-anti-patterns.vue  # 32 处全捕获
node scripts/migrate-theme-tokens.mjs --dry-run [--scope <域>]         # --apply 前必跑
pnpm run check && pnpm run test                                        # 快档
cd Plugins/<X>/web && pnpm run build                                   # 涉改插件
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/<id>  # 冒烟
dotnet test  # 中档全量（W7）
pnpm exec playwright test --config=playwright.config.ts --workers=4 --output=.temp/e2e-out  # 深档（W7）
```
判据一律看日志正文（发现 N 处问题 / PASS / 测试通过数），不拿 exit code 当证据。
