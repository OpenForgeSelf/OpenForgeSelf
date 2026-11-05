# 05 Evidence（task-14）

| 验收项 | 结果 | 来源 |
|---|---|---|
| worktree 漂移验收（任务验收标准） | ✅ 新建 worktree（.temp\wt-t14，feat/t14-color-test from main）→ 人为漂移基线（total.a=9999 纵容）+ 注入 745 合法 6 位 hex 色值（实测计数 1762→2506）→ pre-commit 打印「棘轮基线按 main 同步校验（分支基线未改动；校验后恢复）」→ style-tokens FAIL 2506 > 2501（ForgeSelf.Web/src +587）→ **提交被拒**（commit-msg 之前）；漂移基线下 2506 ≤ 9999 会放行=漏拦机制精确复现并被封堵 | t14 会话输出（Verified） |
| 基线恢复 | ✅ 拒绝后 git status 只余 .vue 暂存，基线文件回到 main 版本（工作区恢复、不污染暂存区） | 同上（Verified） |
| 三目录回归 | ✅ 我的三批 pilot 目录以 hook 同参直跑全 PASS（同步段不碰工件门禁） | verify 直跑（Verified） |
| globalSetup 护栏 | ✅ 逻辑落地（首抓空→8s 重试→两空且回退 config 不存在→fail-fast 指认「初始化内置 Agent」场景+处置；config 存在→正常回退）；深档全量 e2e 门禁另记 | 代码+e2e 日志（见下） |
| 中档 #4/#5（报废记录） | ❌ 均因 harness GUI 重启杀包装进程（管道断）+ 三方并发争用（e2e 12-worker×2 + db-dev 中档）报废：#4 孤儿无摘要、#5 testhost 崩溃（1650/3711 中止）；已记工作日记，**门禁跑程一律串行** | 工作日记 13:05-13:35 节（Verified） |
| 中档全量 #6（串行重跑） | ✅ 3711 例全量恢复/15m13s/32 红——**RepositoryScriptTests+RepositoryReleaseVersionTests 0 红（脚本守卫过）**；红分解=AIAgent16+WorkflowPlanning6+ScriptRunnerDi1（稳定基线）+ PFFM×2（类序方差翻转绿）+ Multimodal×6（PC-014 潜伏方差，已按同机制锚定修复 6/6 绿，复发记录入 task-12 07）+ ProblemClassRegistry×3（lead task-18 的 PC-024 台账文件缺 signature 键，报 lead 修复，非本批范围） | .temp/tmp/tb-full-6.log（Verified） |
| 深档全量 e2e | （跑程中，另记） | .temp/tmp/t14-e2e-full.log |
