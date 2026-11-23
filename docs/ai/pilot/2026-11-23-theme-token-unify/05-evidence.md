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

### W4-Sems 域（2026-11-23 16:3x）
- [Verified] var 迁移：6 文件 109 处，未映射 0（`.temp/w4-sems-var-apply.log`）；硬编码 dry=0 处（176 全为自定义 token 白名单面）。
- [Verified] 计数：Sems **176→82**（硬编码 0 / 未知 el 0；残余 --gold-tint 36 / --font-mono 22 / --dur-* 6 / --glass-* 5 / --font-display|serif 5 等 = W7 登记面）；全仓 **3707→3613**。
- [Verified] 插件 build 绿（522ms，dist/index.js 169KB）。
- [Verified] 冒烟 e2e（sems.spec.ts）：渲染用例红（三态选择器 30s 不可见）+ 串行后续 4 用例未跑。**根因定性 [Verified]：spec 陈旧断言（第二例）**——commit 900a528（2026-11-19 工作台改版）后清单唯一视图 SemsView 根类=`.sems-root`（经典 UI 降级 LegacySemsView 无入口），spec 仍断言 `.sems`/`.sems__title`/`.sems__version`；实证=证据快照显示插件页**已完整渲染**（SEMS 项目工作台 v1.3.0，frontend-manifest 200 含 sems、入口 JS 200、控制台零错误）。记 TODO（P2，与 ai-agent 同类：插件 e2e spec 随 UI 改版漂移）。

### W4-Home 域（2026-11-23 16:5x）
- [Verified] var 迁移 63 处 + features.ts var 兜底剥离清理 43 处 + HomeView 阴影兜底剥离 1 处，未映射 0。计数：Home **107→45**（残余全白名单面）；全仓 3324→3294。
- [Verified] 检查器第二处假阳性修复：命名色正则命中选择器类名（`.dot.green` 的 green）——lookbehind 补 `.` 排除（全仓 -2，AIAgent 185 无连带）；第三处：box-shadow 内 var 兜底色计违规——迁移器兜底剥离正则统一泛化（自造+el token，允许阴影列表前缀）。
- [Verified] 插件 build 绿（299ms）。冒烟 e2e：首跑红=**todo-tracker dist 缺失**（Home 面板远程加载 todo-tracker 插件视图，`Failed to fetch dynamically imported module .../todo-tracker/web/dist/index.js`，§5.0 新 worktree 缺件②实证）；构建 TodoTracker/ToolBridge/McpCenter/AgentHub 四 dist 后复跑 **1 passed（44.8s）**。

### W4-FileTools 域（2026-11-23 16:5x）
- [Verified] var 迁移 6 文件 29 处，未映射 0；硬编码 dry=0。计数：FileTools **65→36**（残余全白名单面）；全仓 3294。

### W4 四小域 + 判责实验（2026-11-23 17:0x）
- [Verified] 迁移：TodoTracker 遮罩黑 alpha 2 处（24→23）、ToolBridge var 18 处（33→19）、McpCenter var 6 处（--el-font-family-mono 未知名→--font-mono，R2→R1 换类 28→28）、AgentHub 遮罩 2 处（12→10）；四域硬编码清零、未映射 0。全仓 3294→**3293**。
- [Verified] **e2e 判责对照实验（file-tools/tool-bridge/mcp-center/agent-hub）**：同源三轮足迹——①本批 HEAD 源 12 failed/12 passed；②bb59154 迁移前源 10 failed/14 passed（共同红 9：tb-paste 缺失/MCP 中心标题不可见/Runtime 双刷新钮 strict violation/agent-hub 会话主链/file-tools 400+零控制台）；③迁移前源复跑仅 1 failed（file-tools 232，又一不同用例）。**套件级 flaky 实锤**（真实磁盘扫描+串行链+路由注册时序主导）；失败均为 DOM/API 级，本批 diff 全为样式声明 token 换名（`git diff bb59154 HEAD` 逐行核对），结构性无关。记 TODO P2（插件 e2e 稳定化：串行链隔离+路由注册等待）。
- [Inferred] W1 桥接映射（2918bd8）已把 AgentHub/ToolBridge/McpCenter 的未知 el（--el-font-family-mono/--el-border-radius-lg 等）映到白名单 token/字面量；本批 var apply 是其延续，方向一致性由映射表真源保证。
- [Verified] 教训（记入日记）：域迁移完成后立即提交——对照实验的 `git checkout` 洗掉了三个未提交域的迁移（已按映射表幂等重放，计数对账一致）。

### W5-DesignSystem 域·进行中（2026-11-23 17:3x）
- [Verified] **架构定性（判据前提）**：DesignSystem 的 --ds-* 命名空间含两类——①外壳别名层（tokens.css→宿主主题，工作台 chrome 消费，检查器计 R1 的主体）②**用户设计系统运行时契约**（后端 export?format=css 产出 --ds-semantic-*/--ds-size-*/--ds-space-* 等注入 .ds-skin 容器/:root，mannequin 样板间/ComponentGallery/TokenShowcase 消费，skin.ts 为外壳名↔令牌路径别名表）。②是产品功能（「预览=交付同源」），字面量化会杀死展厅对比/密度演示——**不可迁**，与 5 展示工具同类（内容=产品数据）。
- [Verified] 据此 W5 范围=**chrome 面 19 文件迁移**（1018 处）+ tokens.css 值收敛（34+4 处，值→el/主题组合）+ 真孤儿定义清理（40 条：仅 chrome 引用、全仓无消费点）；预览契约面（showroom/* 485 + ComponentGallery 111 + TokenShowcase 30 + tokens.css 42 = 668）归 **W7 fileExemptions**（值所有者文件，与 themes/*.css 同哲学）；chrome 残余 176 全为 allowTokens 已豁免面（--border-subtle/--font-display/--glass-*/--dur-*/--radius-*）。
- [Verified] 计数：DesignSystem 1807→**844**；全仓 3293→**2330**。映射表增量 33 条（--ds-space/lh/fw/fs→字面量、soft→color-mix EP 组合、gradient→EP 主色组合、radius→var(--radius-*) 保单一真源）；allowTokens 移除 --ds-* 条目 63 条（计划字面口径）。
- [Verified] **迁移器缺陷修复（migrate-theme-tokens.mjs --prune-defs）**：原实现引用计数只扫 scope 内文件→scope 外消费点（预览面对外壳 token 的引用）不计→仍被引用的定义被当孤儿误删（W5 实证 75 定义全删、预览回退链差点断）。修复=引用计数全仓统计（宿主+全插件），scope 内按改名后文本、scope 外按原文；实测 tokens.css scope 剪 40 留 35（预览引用面存活）。
- [Verified] 门禁②（chrome 迁移后）：vue-tsc check 0 error；**vitest 262/262**（含 skin.test.ts 令牌契约测试）；build 绿（dist 445.75 kB）。门禁③（五 spec e2e）跑中。门禁①（dotnet test DesignSystem filter）本批未触后端，待收口快档核对；④ 发布不在本批（须用户明示）。

- [Verified] **判责闭环（三轮对照实证）**：基线（W5 前源码 ae0a4b0，隔离重跑 showroom+agent 两 spec）2 红=MCP list_tools（后端网关 SSE 流被 spec 当 JSON 解析=真产品缺陷，非本批）+ C2（flaky：run1 过、run2/基线红）。W5 迁移引入的确定性红=base.css `.ds-outfit` 两行——`--ds-semantic-text-1/--ds-semantic-surface-bg` 是**用户系统契约名**（注入的每件衣服投影变量），W2 映射误改为宿主色 → A1/B2/D1/V1 画布断言红。修复=映射移除全部 --ds-semantic-* 条目（14 条）+ base.css 两行还原 + dist 重建。复绿：run3 主链/A1/B2/V1 全过（30 passed），隔离重跑 showroom 17/18（唯一红=A1 向导流 `[data-wizard-done]` 30s 超时，非画布断言、时态 flake；画布断言同用例 run3 已绿）。
- [Verified] **残余红归档（非本批引入）**：①MCP list_tools + G7——后端 MCP 网关对 list_tools 返回 SSE 流而 spec 按 JSON 解析（`SyntaxError: Unexpected token 'e', "event: mes"...`）——产品缺陷，挂 TODO（P1，插件任务需修网关或 spec 协议）；②C2/D1/C6/A1-wizard-done——页面态/超时 flake（C2 基线同红；D1 `[data-showroom]` 30s 不可见、C6 展厅 tab 120s 超时均为元素时态，非样式断言），归插件 e2e 稳定化（TODO P2 已挂）。
- [Verified] 门禁③结论：**W5 迁移零确定性红**；DS 计数 858（=chrome 授权白名单面 + skin-canvas 契约 17 + 预览契约面）。映射 literal 双字段教训、prune 全仓计数修复、契约名三类（--ds-bg/--ds-fg-1/--ds-semantic-*）已入库迁移器与映射表。

## 3. AC 覆盖（收口时逐条定稿）
- AC1 全项目 check:theme 0 违反：未验证（进行中）
- AC2 check:theme:demo 32 处全捕获：未验证（进行中）
- AC3 9 自造 modal 迁 el-dialog 盘点：未验证（进行中）
- AC4 SkillsView 弹窗对比度 ≥4.5:1：未验证（进行中）
- AC5 check/test/插件 build：未验证（进行中）
- AC6 插件冒烟 e2e + DesignSystem 四层门禁 ①②③：未验证（进行中）
- AC7 中档 dotnet test + 深档 e2e 基线对表：未验证（进行中）
- AC8 check:theme 接线（check 链/pre-commit/CI）+ 亮/暗走查截图：未验证（进行中）
