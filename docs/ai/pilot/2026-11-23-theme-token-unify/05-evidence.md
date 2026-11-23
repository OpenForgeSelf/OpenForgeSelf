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

### W3 宿主迁移（已完成，提交 6a7f7ba）
- [Verified] W3 全量证据见提交信息与日记 15:43 块（vue-tsc 0 错 / vitest 1266 全绿 / 计数 5946→4357）。

### W4-AIAgent 域（2026-11-23 16:0x）
- [Verified] var 迁移 `--apply --prune-defs --scope=Plugins/AIAgent/web/src`：首轮 5 文件 378 处 + 孤儿定义 47 行；映射表补漏（--aiw-radius/--aiw-radius-xl/--aiw-fs-xs/--aiw-fs-sm）后增量 3 文件 29 处 + 定义 6 行，未映射 0。日志 `.temp/w4-ai-var-apply.log`、`.temp/w4-ai-hex-apply.log`。
- [Verified] 硬编码迁移 `--root Plugins/AIAgent/web/src`：apply 222+1 处，未映射 0（dry 五轮校准记录 `.temp/w4-ai-unmapped-full.log`）。
- [Verified] 检查器假阳性修复：check-theme-tokens.mjs 跨行块注释状态机——AIAgent 13 处「硬编码」10 处为注释史料，全仓再削 16 处（3765→3733）。
- [Verified] 计数：AIAgent **809→185**（硬编码 0，残余全白名单面）；全仓 **4357→3707**。日志 `.temp/theme-after-w4-ai4.log`。
- [Verified] 插件 build 绿（354ms，dist/index.js 67.7KB + style.css 13.1KB）。
- [Verified] vitest：**15 红/153 = 精确基线**（http.session 7 + http 5 + sessionArchive 3 契约漂移 + 10 套件 `Failed to load url vue` 别名缺失；TODO 已挂账 P2 非本批）。修复过程中把 widget-contract 契约测试 4 红改锁新不变量后 **19/19 绿**（原断言要求保留 --aiw-* token 族，与本批并入 EP 体系决策冲突，按 W3 守卫先例改写）。
- [Verified] 冒烟 e2e（ai-agent.spec.ts）：**7/7 红，stash 对照（HEAD=6a7f7ba 无本批改动）同 7/7 红同报错** → 非本批引入。根因定性 [Verified]：commit 64c0cbe（2026-11-18）把 `AiAgentView` 导出合入新助手页（`AgentAssistantPage`，aa-* 类族），spec 断言仍停留在 v1.8 三栏 UI（`.agent`/`.sess/.chat/.ctx`/版本徽标）——**spec 陈旧断言债**，属全量 e2e 82 红池；已记 TODO（P2，spec 重写对齐助手页）。
- [Inferred] 残余 185 全为白名单登记面（--font-mono 87 / --aiw-space-* 34 / --aiw-primary-alpha 档定义+引用 / --radius-lg 等），待 W7 统一登记。

## 3. AC 覆盖（收口时逐条定稿）
- AC1 全项目 check:theme 0 违反：未验证（进行中）
- AC2 check:theme:demo 32 处全捕获：未验证（进行中）
- AC3 9 自造 modal 迁 el-dialog 盘点：未验证（进行中）
- AC4 SkillsView 弹窗对比度 ≥4.5:1：未验证（进行中）
- AC5 check/test/插件 build：未验证（进行中）
- AC6 插件冒烟 e2e + DesignSystem 四层门禁 ①②③：未验证（进行中）
- AC7 中档 dotnet test + 深档 e2e 基线对表：未验证（进行中）
- AC8 check:theme 接线（check 链/pre-commit/CI）+ 亮/暗走查截图：未验证（进行中）
