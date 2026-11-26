# 03 Plan — 统一主题修复批（分域执行，每域独立 commit + 门禁）

> 闸门1：本会话审批策略 = 用户设置的「never ask the user for approval」（自动迭代模式，§8.5），视为预授权执行；push 仍禁。偏差先记录再修正。

## 1. 域序（按 03-plan 域序；每域验收 = 域计数 0 + check/test 绿 + 亮/暗走查）
| 域 | 内容 | 主要文件 | 验证 |
|----|------|----------|------|
| W1 桥接层 | 2 处幻影 token 引用 + 删主题内 4 行幻影定义 | ForgeSelf.Web/src/styles/tailwind.css、themes/workshop-forge.css | check:theme 未知 el 归零部分 + check |
| W2 供给核验 | 核对映射目标 token 在 themes 供给齐备（提交版已含浅/暗块）；不搬备份 --canvas-* 块 | themes/workshop-forge.css | 读码核对 + 走查 |
| W3 宿主 | 迁移器 --apply --scope ForgeSelf.Web/src + 硬编码映射收尾 | views/components 96 文件（大户 ProfileView/MemoryView/SkillsView/WorkflowLibrary/ScriptLibrary/PluginImportExport/ChatView） | check:theme 宿主 0 + check/test + dev-stack 亮/暗截图 |
| W4 非 DesignSystem 插件 | AIAgent 839（浮窗主题走查）、Sems、ImGateway、SmartTerminal、Home、FileTools、ToolBridge、McpCenter、TodoTracker、AgentHub、QuickLinks | Plugins/*/web/src | 各插件 build + 1 冒烟 e2e + 走查 |
| W5 DesignSystem | --ds-* 1769 全迁：映射表增量（--ds-* 豁免条目 → 字面量/el 组合）→ --apply → 删孤儿定义 | Plugins/DesignSystem/web/src（tokens.css 等） | 四层门禁 ①②③ + 假能力自查表 + 走查 |
| W6 弹窗 | 9 自造 modal 迁 el-dialog（PluginDetailModal 参考备份改法）+ SkillsView 对比度 | 宿主 5 + FileTools 2 + QuickLinks 2 | 每弹窗所在视图冒烟 e2e + 读图对比度 |
| W7 收口 | 豁免册定稿 + 检查器 fileExemptions + check 链/pre-commit/CI 接线 + demo 32 捕获 + 中档 dotnet test + 深档 e2e | scripts/、package.json、.github/workflows、scripts/hooks | AC1/AC2/AC7/AC8 |

## 2. 映射表增量（本批补）
- `--ds-*`：tokens.css 定义值为字面量真源——刻度/字体/行高/字重/圆角/间距 → literal；surface-0/surface-bg → `transparent`；gradient-brand(.soft) → el 主色组合（linear-gradient(var(--el-color-primary-light-3), var(--el-color-primary-dark-2)) 族）；其余颜色沿用 rename 表。allowTokens 中 --ds-* 条目移除（不走豁免）。
- 未知 el 107 − 92 = 15 增量：--apply 后重跑检查器逐条入 phantomEl。
- 硬编码 1522：公共 hex → el 语义映射（--el-color-success/danger/warning/info 族 + border-brand/glass 参数）；展示型工具 5 文件走 fileExemptions 登记；UuidTool/XmlTool/YamlTool/JsonTool 等零散映射。

## 3. 验证分档（每域）
- 快档：check:theme 计数只许降 + `pnpm run check` + `pnpm run test` + design:check（pre-commit 自动）。
- 中档（W7）：全量 dotnet test（scripts/** 已动）。深档（W7）：全量 e2e 4 worker + --output 空目录。
- 走查：dev-stack 起源码前后端，亮/暗两态截图（Playwright 浏览器交互，落 screenshots/walk-<域>/）。

## 4. 风险与回滚点
- 每域一 commit，token 替换纯机械可单域 `git revert`。
- themes 供给先于使用点替换（W1/W2 先行）；任一时刻新旧 token 同时可用。
- e2e 环境前置：NO_PROXY/TEMP 设定 + SQLite provider 拷贝 + 插件 dist 预构建（§5.0）。
- 并行会话在途件（DESIGN.md/package.json/AIAgent 后端、审计批 pilot 目录）：不碰不提交不还原。
