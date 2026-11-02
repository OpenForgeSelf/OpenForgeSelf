# Intent — B5 旁路收敛

> 闸门1：lead seq7 开工令（2026-11-02 23:42，视为已批）

## 为什么做

架构真源 `maf-centric-architecture.md` 已定性：ReactLoopAgent = Legacy Loop（废弃）、UseMafRuntime/TaskLoopMode 开关 = 收敛后删除、AgentCoordinator 启发式旁路 = 非主路径「要么接入要么删除」。三处旁路并存造成：①双引擎语义漂移风险（每次工具管线/审批/收件箱改动都要双写）②死开关误导回滚想象（UseMafRuntime 生产恒 true）③Coordinator 旁路把带工具的任务送进 ad-hoc 无日志循环（无会话日志、无审批泵——零配置下高风险工具免审批执行，F16 安全缺口）。

## 做什么（到什么程度）

1. **删 ReactLoopAgent**（绿场直接删，无迁移承诺）：类本体 + 工厂分支 + 注册表模式匹配 + 全部类型引用；共享契约（AgentTurnRuntime、PreStep*/AgentRequest*/TurnStopping）迁独立文件保留；引擎语义门禁测试迁 MafTaskLoopAgent 驱动。
2. **删双开关**：AgentOptions.UseMafRuntime 移除（CreateAgent 恒 MafTaskLoopAgent）；TaskLoopMode 收敛默认常开（MAF 路径恒任务循环语义）；ForgeSetting.config 旧键残留不得崩（未知键忽略天然成立，加兼容读测试佐证）；全仓 grep 零残留。
3. **AgentHub→实为 AgentCoordinator 启发式旁路裁决**：读码 + 调用链已毕（00 §1.3），出「接入 MAF / 删除」推荐 + 理由，**报 lead 拍板后才动**（拍板前 AgentCoordinator 代码零触碰）。
4. 不做：IM 审批推送（B5 后置）、IChatCompletion 旧面（B7 定性合法例外）、人设注册表（RunOrchestrator 在用）、Scheduler（P2 路线图）。

## 完成判据

- 全仓 grep `ReactLoopAgent|UseMafRuntime` 生产代码零命中（文档历史记载除外）；
- 既有引擎语义门禁全数由 MafTaskLoopAgent 驱动承载（迁移后全绿）；
- 中档全量 dotnet test 对表零新增红 + 深档全量 e2e（4 worker 空输出目录）绿；
- AC1..ACn（02-spec §9）全过；AgentCoordinator 裁决留痕（推荐 + lead 拍板原文）。
