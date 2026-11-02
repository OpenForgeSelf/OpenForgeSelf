# Plan — B5 旁路收敛

> 前置：task-3 已领（revision 2）；闸门1 = seq7 开工令。

## 步骤（每步可验证）

| # | 步骤 | 改什么 | 验证 |
|---|------|--------|------|
| S1 | ③ 裁决报告先行 | send_message → lead（拍板前 AgentCoordinator 零触碰） | lead 回执 |
| S2 | 共享契约迁移（AC1） | ReactLoopAgent.cs :1-131 → 新 `Services/AgentTurnRuntime.cs`（纯移动）；ReactLoopAgent 类暂留 | dotnet build + MafTaskLoopAgent 过滤集 |
| S3 | 测试换装先行（AC3/AC4 红前） | 6 个编排器系文件 mock 返回体换 MafTaskLoopAgent；ReactLoopInboxGateTests/ReactLoopExitAndSuspendTests/ReactLoopAgentTests 逐文件移植 | 移植一批跑一批（过滤集） |
| S4 | 删引擎（AC2） | 删 ReactLoopAgent 类 + 工厂三元 + ResolveLoopAgent 改指 + 注释改词 8 文件 + 删 ReactLoopTaskLoopTests | 全仓 grep 零残留 + 编译 |
| S5 | 删开关（AC5） | Agents.cs 删 2 属性；MafTaskLoopAgent 恒任务循环；AIAgentService 删 2 行赋值 | 全仓 grep + 编译 |
| S6 | 兼容读测试（AC6） | ForgeSetting 含残留键反序列化测试 | 过滤集 |
| S7 | ③ 执行（AC8，拍板后） | 按 agenthub-adjudication.md 拍板面删除（后端 + UI 死代码 + not-taken） | grep + 编译 |
| S8 | 中档全量（AC9a） | r3 OutDir 旁路全量（B4 动线：换号防自撞 + 孤儿 testhost 检查） | 对表零新增红 + 伪红隔离复跑 |
| S9 | 深档全量 e2e（AC9b） | e2e-testing 技能 SOP：4 worker + `--output` 空目录；先读技能 | e2e 报告 + 截图 |
| S10 | 05/06/07 + 回报 | AC 逐字映射；TODO/日记收口；回报 lead | lead 闸门2 |

## 风险与对策

- R1 inbox R2 MAF 缺口（U1）→ 移植时暴露即缺陷：先修 MAF 侧再删引擎（缺陷记 case）。
- R2 MAF 与 ReactLoop 帧序差异致移植断言失败 → 断言锚共享 SessionEvent 契约（B7 已统一），帧级差异逐条对 MafTaskLoopAgentTests 既有口径。
- R3 OutDir 旁路伪红污染判责（B4 实证 44 条）→ 全量红必须 --no-build 标准 bin 隔离复跑定性后才归责。
- R4 e2e 环境面（NO_PROXY/TEMP/esbuild）→ e2e-testing 技能 §环境前置三件套先做。
- R5 dev-stack DLL 锁 → 构建统一 r3+ OutDir；撞锁报 lead 开窗。

## 回滚

单 commit 面：`git checkout -- <touched>` + 新文件删除即可回滚（无数据/契约变更；AgentOptions 属性删除属编译期，回滚即恢复）。AgentCoordinator 若拍板删除：4 端点下线不可逆承诺为零（零消费者），恢复 = revert commit。

## 待定

- S7 依赖 lead 拍板（U3）；拍板=接入时 S7 改为另立任务占位。
