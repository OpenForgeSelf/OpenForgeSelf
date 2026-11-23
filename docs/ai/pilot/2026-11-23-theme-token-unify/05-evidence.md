# 05 Evidence — 统一主题修复批（随域推进填充，只记实际发生）

> 分级：Verified（亲跑真输出）/ Inferred（代码推断）/ Unknown（未验证）。本文件随每域验证即时补记，收口时定稿。

## 1. 基线与工具
- [Verified] 基线（bb59154 worktree，2026-11-23）：check-theme-tokens = 6021 违反（自定义 4392 / 硬编码 1522 / 未知 el 107），16 域分布见 `.temp/theme-baseline.log`。
- [Verified] 迁移器 dry-run：111 文件 / 2971 处可机械替换；未映射 2 处均为注释示意串（检查器不命中）。
- [Inferred] themes 供给提交版已含浅/暗 --el-* 重定义且与旧自造 token 暗值等值（workshop-forge.css 98-182 / 185-259）。

## 2. 分域验证记录（W1…W7 逐域追加）
### W1 桥接层幻影修复（已完成）
- [Verified] `node scripts/migrate-theme-tokens.mjs --apply --rules phantom`：**23 文件 / 92 处**替换（与审计期未知 el 92 处对账精确吻合），日志 `.temp/w1-phantom-apply.log`。
- [Verified] workshop-forge.css 删除 4 行幻影定义（浅 `--el-color-accent: #4A7A9B` / `--el-color-accent-teal: #5A7A5A`，暗 `#7FA8C8` / `#5A7A5A`）；grep 全仓 `var(--el-color-accent` 零命中，Tailwind `*-accent`/`*-teal` 工具类零消费 → 连桥接死别名一并删除（tailwind.css --color-accent/--color-teal 两行）。
- [Verified] 映射表外增量未知 el 15 处（--el-margin×13 / --el-padding×5 / --el-font-weight-bold×3，集中在 GithubBindPanel/GithubAccountPanel 两文件；element-tokens.json 实证 EP 2.14.3 无 margin/padding 通用 token、字重仅 --el-font-weight-primary）→ 等值内联字面量（8px/12px/16px/6px/10px/600）→ **未知 el 0**。
- [Verified] 计数：6021 → **5946**（净减 75），日志 `.temp/theme-after-w1c.log`。
- [Verified] 备份 workshop-forge.css diff 复核：备份仅多 `--canvas-*` 画布调色板块（theme-canvas-unify 在途批），与任务交待的「--el-* 供给补齐块」不符——供给块本就在 main 提交版中，不搬画板块。

## 3. AC 覆盖（收口时逐条定稿）
- AC1 全项目 check:theme 0 违反：未验证（进行中）
- AC2 check:theme:demo 32 处全捕获：未验证（进行中）
- AC3 9 自造 modal 迁 el-dialog 盘点：未验证（进行中）
- AC4 SkillsView 弹窗对比度 ≥4.5:1：未验证（进行中）
- AC5 check/test/插件 build：未验证（进行中）
- AC6 插件冒烟 e2e + DesignSystem 四层门禁 ①②③：未验证（进行中）
- AC7 中档 dotnet test + 深档 e2e 基线对表：未验证（进行中）
- AC8 check:theme 接线（check 链/pre-commit/CI）+ 亮/暗走查截图：未验证（进行中）
