# Final Report — db-admin 并入 main（收口与验收）

> Task：`2026-11-05-db-admin`｜状态：**终稿**。

## 1 Repository Understanding

见 00（摘要：XCode DAL 执行链 + 插件 remote-load 前端 + 4 处宿主登记 + 7 件 db_* 工具）。

## 2 Selected Task

任务板 task-5 [T-E]：rebase + 工件 + 测试 + e2e + 走查 → merge-ready。

## 3 Changed Files

**ef89d71..HEAD 共 14 提交 = 13 个 rebase 终态提交 + 1 个 docs 终稿提交**：

- docs 垫底 5：d534079（00-06+ui brief 锁稿）/ 5867bf7（05 写回决策）/ 67454ac（03 契约）/ b69c1b5（工件链 00-07+044）/ 2a9753c（05-evidence T-C 前置回填）
- code 7（全带 Pilot trailer）：04e2582 Web UI（+CT S0-S9+S-R）/ c3ce9d6 后端 MVP / 76d875a 契约对齐 / 895ff06 Agent 工具 db_* 七件 / 246d5b0 前端对齐契约 / 9312a38 SqlGuard 修复 / eeefda1 评测用例批准规则
- e2e 1：3193c97 冒烟主链路 + 路由清单两插件（含第 5 轮 S-R 竞态防护 fixup）
- docs 终稿 1（本提交）：05/06/07 终稿 + E1..E8 全量回填

非文档全集 = 04 expected_files 76 项（pre-commit 机器校验）；基线 main 侧文件（SmartTerminal/AIAgent/scripts）零触碰。

## 4 Validation

AC1..AC8 全 ✅（05 §1 终态表）；门禁分档交代：**本批跑的是「快 + 中（定向）」**——三门禁 + 插件 web 全量 vitest + 宿主前端三项 + DbAdmin 相关 dotnet 过滤集 + 插件冒烟 e2e（6 轮）+ dev-stack 走查。宿主后端**全量** dotnet test 未在本 worktree 跑（按 §5.6 触发条件：本批未碰宿主源码 ForgeSelf.Api/** 业务码与共享测试基建以外的宿主文件；DbAdminTests 走宿主测试工程编译，其过滤集绿 + PluginLoadDependencyGuardTests 见下）；该档缺口由 qa-ci 在 main 侧中档全量兜底。

## 5 Evidence

05 §0：E1（13 提交+trailer）→ E8（工件链）全 Verified，日志路径在案。

## 6 Review

06 Final Decision = **APPROVED**（八问全过）。

## 7 Risk

- 宿主前端基线红（SmartTerminal 302 TS 错 + AIAgent 3 vitest 红）在 main 侧同样存在——**并入后 main 的前端门禁数字与基线一致，无新增红**；修复归属 st-dev/lead。
- PluginLoadDependencyGuardTests（PC-020）：DbAdminTests.csproj 独有包自查 = 无（Test.Sdk/xunit/XCode.SQLite 均宿主或测试基建已有），rebase 后实证：补 `CopyLocalLockFileAssemblies=true`（参照 SmartTerminal.Tests 同款修法）→ **guard 2/2 通过**（log-pc020-guard3.txt），fixup 已并入后端 MVP 提交。
- e2e 第 5 轮暴露的 S-R 恢复竞态已加 spec 防护；产品级树缓存刷新缺口见 §11。

## 8 Problems Found

1. verify-pilot-gate 的 feature 字段格式（纯编号）与直觉不符——按报错信息可自愈，已在 04-task 修正。
2. commit-msg hook 拒无 trailer 的 fixup 提交——第一次 fixup 被拒后补 `--trailer` 重交；提示信息已明确指引。
3. 审批弹窗破坏性行文案重复（「写 · 破坏性破坏性」）——小 UI 瑕疵，§11 提案。
4. e2e 数据根 `.temp/e2e-db/` 库文件删除失败（宿主 DAL 占用）——按预案惰性清理，无泄漏风险。

## 9 Process Evaluation

- 六轮 e2e 迭代（4 次 spec 修正 + 1 次竞态防护）零产品代码改动——e2e 写作纪律生效（先读 view 源码再写断言，错误快照 + backend.log 交叉取证）。
- rebase 两次目标更正（0600858→382441f→ef89d71）均以 `git cherry` patch-id 预检 + 完整日志判定，未发生内容意外变化。
- 门禁失败先根因再修（feature 字段格式、22 处色值全清而非 --update 掩盖）——style-tokens 较基线 -740。

## 10 问题类归档

对照 00 §4 命中 PC 复核：
- **PC-020（插件 Tests csproj 独有包 CopyLocalLockFileAssemblies）**：DbAdminTests.csproj 无独有包，天然合规；lead 提醒后在 rebase 后以 PluginLoadDependencyGuardTests 过滤集实证（不新增 PC 条目）。
- **e2e S-R 竞态（第 5 轮）**：单例时序问题，非缺陷类回归（此前无同类历史），spec 侧防护已固化（6 轮绿）；不立新 PC，教训入 §12。
- **style-tokens 硬编码色值（22 处）**：写作期引入（GitHub 暗色板 + var fallback），同一提交内闭环修复并过棘轮（-740）；未逃逸出本任务，不立新 PC。
- 无 docs/11-troubleshooting/ 新增归档（无既有 PC 复发、无逃逸）。

## 11 重构提案（不属本批范围，逐条给 TODO）

| # | 提案 | 价值 | 建议 |
|---|------|------|------|
| R1 | **schema 树缓存刷新缺口**：建表/删表后树不自动刷新（loadTables 对连接缓存短路，DbAdminView.vue:258），需手动刷新页面。走查与 e2e 第 5 轮双重实证 | 高（核心动线） | P2 TODO：写操作完成后失效该连接的 tables 缓存（或 expose 手动「刷新 schema」按钮） |
| R2 | 审批弹窗「类型：写 · 破坏性破坏性」文案重复 | 低 | P3 TODO：destructive 标注拼接去重 |
| R3 | **SmartTerminal vue 依赖声明缺失**：package.json 从未声明 vue（b938101 原始提交即无），SFC `import 'vue'` 类型解析断 → check/vue-tsc 302 错，main 干净检出必红（lead 报 0 错实跑于 feat/smart-terminal@3c540d6 工作区，非 main） | 高（门禁可信度） | 移交 st-dev：package.json 补 `"vue": "^3.5.13"` + pnpm install（一行）。另 AIAgent web 棘轮 3 红仍待基线重定 |

## 12 悬空教训清单

1. **S-R 恢复是异步竞态源**：reload 后 applyRestore 与用户交互（树点击）可能交错——e2e 对恢复后 UI 的断言要带收敛防护（8s 重试），且断言失败先看错误快照 + 后端 SQL 日志交叉取证（第 5 轮 10 分钟定位的教训）。
2. **verify-pilot-gate 的 feature 字段 = 纯编号**：不是路径；写 04-task 时直接写 `feature: 044`。
3. **fixup 提交也要带 Pilot trailer**：commit-msg hook 对所有提交校验 trailer，`git commit --fixup=<sha> --trailer 'Pilot=...'` 一次过。
4. **pwsh 长命令在工具通道易截断/损坏**（本会话第 17 行损坏实证）：批量替换拆短批（≤9 对）执行，每批验结果。
5. **门禁计数不等于行数**：style-tokens 的 22 = 11 hex + 11 rgba（rgba 也计数）——修色值时两类模式都要清。