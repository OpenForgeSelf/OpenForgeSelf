# 07 Final Report

## 结论

- 状态：**COMPLETED**（验证关键项全部 Verified；e2e 与真实模型走查明示 Unknown 未跑，不构成阻塞——判据函数为单点纯函数，单测层已钉死）
- 任务：测试连接（选定模型）探针判据放宽——回复非空白即成功，不校验是否含 ok；空回复仍判失败（PC-013 保留）。

## 主要变更

- `AIProviderService`：`SaysOk` → `HasUsableReply`（`!IsNullOrWhiteSpace`）；成功消息「连接成功 · 模型回复「{前 100 字}」」；删除「模型未回复 ok」失败分支。
- 控制器 doc ×2、接口 doc、真源 001 §3、e2e 标题/注释同步；守卫测试改名 `AIProviderTestConnectionReplyGuardTests`（9 例矩阵，G1）。
- 问题类归档：PC-034 新建（首见）+ case + 三处索引；`check-problem-classes.ps1` 34/34 OK。

## Validation / Review（验证摘要，明细见 05-evidence；审查结论见 06-review）

红 3 失败/5 过 → 绿 9/9；build 0 error；中档全量 3778/3753，25 失败逐族对表归本批 0；PC 机器校验 + Registry 8/8 绿。

## 问题类归档（缺陷类门禁节）

- PC-034 `docs/11-troubleshooting/problem-classes/PC-034-probe-keyword-false-negative.md`（status: guarded，G1 active）；case `cases/2026-11-14-provider-probe-ok-keyword-false-negative.md`；索引三处已更新（problem-classes/README、cases/README、index.md 速查）。

## 重构提案

- **R0，无**。判据为单点纯函数，G1 已把不变量钉死；无跨模块契约面、无复发历史。若未来出现「同类内容判据」二次复发，按 PC-034 lens 直接升 G2 枚举式守卫（扫全部探活/测试类入口）。

## 悬空教训（必须为空）

- 空——本批无 Minor/遗留项。e2e 与真实模型走查为「未跑明示」，非教训挂账；深档触发条件未满足，不留 TODO。

## 代价与收益（运行经济学）

- 代价：净 -31 行（+16/-47，含测试重写）；1 次全量测试 16 分钟。
- 收益：消除「模型正常但测试失败」的假阴性整类；测试连接结果与模型真实可用性对齐；判据不变量首次成文（PC-034）。

## NOT-to-do

- 不改探针 prompt 与 MaxTokens=128（e2e wire 断言依赖 + 与判定无关）。
- 不引入新的内容启发式校验（长度/语言等 = 同类隐患，PC-034 lens）。
- 不动 PC-013 空响应失败分支与其守卫。

## 风险

- 低。唯一行为变化即本批目标（不含 ok 的正常回复 → 成功）；若某处 UI 文案依赖「含 ok」语义（已查：仅测试标题/注释，已同步）。
- 本 worktree 为会话工作树，改动未提交 git（提交需用户同意）。
