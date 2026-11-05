# 07-final-report — task-17 DevModeTests 静态态复位

## 状态
COMPLETED（验收依据 = lead 批准的定向对表集 + qa 代跑中档全量待转；其余全绿）

## 交付
- ForgeSelf.Api.Tests/Plugins/Dev/DevModeTests.cs：Dispose 复位协议（+5 行）+ ResetProtocol 守卫测试（+23 行）
- ForgeSelf.Api.Tests/Plugins/PluginFrontendFileMiddlewareTests.cs：IAsyncLifetime 防御复位（+9 行）
- 提交：81b88fa（task-17 批）+ 6cbd917（merge main 3a7ab62 对表基线）

## Validation / Review
验证门禁实际执行记录（✅=亲跑 Verified）：过滤集 22/22（E2）、ForgeSetting 系 5/5（E3）、定向对表 201=198 绿+3 红（E4，3 红归属 PC-024 他域）、同参工件校验（本节由审查反馈补齐后 PASS）。Review 八问与 Final Decision 见 06-review.md（APPROVED）。

## 验证（摘要，详 05-evidence）
- 过滤集 22/22；ForgeSetting 系 5/5；定向对表 201 = 198 绿 + 3 红（全部 PC-024 工件登记腐烂，st-dev/qa 域，非本任务引入）
- 中档全量：本机 CPU 争用下 ScriptRunner 簇挂死（环境敏感，qa #3 同代码 15 分钟收口为对照），由 qa 环境代跑，lead 已派

## 问题类归档
- PC-025（新登记建议）：xUnit 并行测试的静态态 Dispose 残留——不变量：有 private set 静态的类，其测试 Dispose 必须同时复位静态态与环境变量；特征签名：并行类偶发断言翻转、类序依赖 flake、单跑绿全量偶红；canonical fix：复位协议（清 env + Initialize 幂等）+ 受影响类 IAsyncLifetime 防御复位 + 协议守卫测试；守卫：ResetProtocol_ClearEnvPlusInitialize_IsDeterministicOff。归落点 docs/11-troubleshooting/problem-classes/PC-025-static-state-dispose-residue.md（st-dev/qa 域 PC-024 工件登记腐烂另案，非本任务范围）。

## 重构提案
- DevMode.Enabled 若增加更多 private set 静态字段，考虑提取 DevModeTestReset.ResetAll() 唯一入口（现两处手写协议在字段增多时有漂移风险；守卫测试已兜底）。
- 环境治理：取消类 async 测试（ScriptRunner 簇）补总超时参数，消除 CPU 饥饿下的无限挂（TODO 候选）。

## 悬空教训
1. 孤儿 testhost 是隐形锁：过滤集跑完 testhost 可能残挂（本例 ForgeSetting 10s 过滤集产出 680s CPU 孤儿），占 DLL 锁会诱发后续全量 XCode 自愈风暴级雪崩——跑完过滤集应确认 testhost 退出。
2. worktree 缺 gitignored 共享文件会炸 hook：wt-db 缺 TODO.md 致 check-problem-classes.ps1 L148 抛路径异常、pre-commit 误报「工件链缺件」（报文误导指向 pilot 工件）——worktree 初始化应同步 TODO.md 等活队列文件（task-14 基线同步机制候选项）。
3. pwsh 行级编辑遇单语句 foreach（无花括号）找收尾大括号会错位——字符串级锚点替换更稳（已记日记）。