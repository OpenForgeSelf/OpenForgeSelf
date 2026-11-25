# 07 Final Report — exe 轨更新代理宿主未拉起修复

## 1 Repository Understanding
见 00：QQNT 式分层（公共层内置运行时 + 版本层快照 + agent FDD 单文件）；staged 目录兼具「待装包解压根」与「agent 运行时根」双重身份，是本缺陷关键。

## 2 Selected Task
修复 exe 轨首次真机更新宿主未拉起（方案 A，用户「可」授权）：交换 UpdateAgentRunner 步骤 8/9，先重启宿主再清理 staged。

## 3 Changed Files
| 文件 | 变更 |
|------|------|
| `ForgeSelf.UpdateAgent/UpdateAgentRunner.cs` | 交换步骤 8/9 + 顶部注释含 PC-050「不可交换」理由 |
| `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs` | 新增 G2 守卫 `Pc050_…` |
| `docs/11-troubleshooting/problem-classes/PC-050-….md`（新） | 问题类真源 |
| `docs/11-troubleshooting/cases/2026-11-24-update-agent-exe-….md`（新） | 事件档案 |
| 台账 `README.md`×2 + `index.md` | 三处索引 |
| `docs/ai/pilot/2026-11-24-update-agent-restart-before-cleanup/`（新） | 00–07 八件工件 |

未碰：发布脚本、shim、宿主 csproj、SDK/global.json、并行会话在途文件。

## 4 Validation
- G2 守卫 `Pc050_…` Passed（1/1）。
- agent 单文件 publish EXIT=0，195193 B（体积不变符合预期）。
- check-problem-classes 40/40；Registry+Pc050 6/6；pilot 工件门禁（全开关）见下。
- 真机完整更新闭环未实测（沙箱 MSB4166 环境约束，见 §8）。

## 5 Evidence
见 05-evidence（AC1–AC4 逐条 Verified + 出处）。

## 6 Review
见 06-review：修复针对根因、最小改动；用户 SDK11/签名/我改脚本三个假设均经实测推翻；台账自洽。

## 7 Risk
L1：改动仅 2 行顺序，ps1 轨与其它模块不受影响；JIT 延迟加载的其它 BCL 理论上仍可能在重启后触发，但 `AgentLog` 在步骤 0 已加载，风险低。

## 8 Problems Found
1. **宿主未拉起**（本任务修复）：exe 轨先删 staged（自身运行时）再重启，JIT 加载 System.Collections.Specialized 失败 → PC-050。
2. **MSB4166**（环境）：本沙箱并行 MSBuild 节点崩溃，完整发布链 3 次失败；`-m:1` 单组件 publish 成功，整包重打包需正常终端；非仓库 bug，未归档为 PC。
3. **回滚/健康检查缺失**（用户诉求）：current 无回滚、无启动探活；另批。

## 9 Process Evaluation
9.1 有效：坚持第一性原理，从异常倒推 + 实测对照（SDK10/11 体积、签名启动、历史轨型），推翻了三个错误假设才锁定真因；先理解设计用意（staged 双重身份）再决策。
9.2 教训：JIT 延迟加载 + 双重身份目录 + 多轨并存，三者叠加让缺陷只在「exe 轨真机」这一狭窄条件暴露； ps1 单测的成功不能外推到 exe 真机。

### 9.5 本批自复盘
- **做对了**：用户质疑时没有坚持旧结论，重新实测；闸门1 等到明确「可」才动手；守卫带阳性对照防假绿。
- **可改进**：前两轮打包用默认并行参数遇 MSB4166，本沙箱跑 dotnet 直接带 `-m:1` 可省两轮；function 包装 dotnet 注入 `-m:1` 触发 PowerShell 参数歧义，属无效尝试。

## 10 问题类归档
PC-050（guarded，G2 active + G3 签名）+ case 2026-11-24-update-agent-exe-cleanup-before-restart + 三处索引。check-problem-classes 40/40、Registry 6/6。

## 11 重构提案
R0 不重构：2 行顺序交换 + 守卫已足够。方案 C（运行时独立到 agent-run/runtime）作为「运行时升级/回滚」大批的候选，非本止血批范围。

## 12 悬空教训清单
（空）
