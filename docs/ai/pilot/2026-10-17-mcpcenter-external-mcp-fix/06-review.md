# Review

## 审查问题（必须回答）

变更是否满足 02-spec 的 S1-S5 与兼容面？是。S1 三传输 clientInfo（canonical 单点）、S2 initialized 通知（尽力而为不阻断）、S3 Streamable 会话头捕获/回传（无状态自然不携带）、S4 编辑全回显 + 掩码不往返（前端基线 + 后端 RejectMaskedValue 双防）、S5 兼容面（宽松服务器 A/B：mock 对通知回 -32601 仍握手成功；既有 119 项测试零回归）。门禁②追加的 `/test` data 契约缺陷同批闭环（e2e + API 实测 + G2 形状锁三证）。

## Requirement Check

- 用户报告三缺陷（连接失败/测试失败/编辑不回显）：全部修复且有用户路径级证据（e2e 主链路 + 走查）。PASS
- 复现批 Review 指定的修复方向（canonical fix 一处定义三处引用）：`McpClientProtocol.cs` 落实，G3 签名封死回退。PASS
- 掩码往返防护（复现批衍生 Major）：双向防护落地。PASS

## Scope Check

超出原始三缺陷的变更共 3 处，均有依据：① `/test` data 契约（门禁② e2e 阻塞缺陷，同用户路径，闸门1 同类修复项规则）；② `types/dsh.ts` 两字段（既有 check 错误解封，git stash 验证非本批引入，异根因最小顺手修，已记 05/TODO）；③ `new-problem-case.ps1` Double 格式崩（本任务归档步骤被阻塞的当日基建缺陷，problem-class-closure 覆盖）。未触碰无关代码。PASS

## Test Check

G1 红灯转绿（Verified，命令输出见 05）；G2 三个守卫文件 + TestEndpoint 两测；G3 pre-commit 签名扫描绿；e2e 5/5；前端 23/23 + check 0 err。版本 bump 后复跑 build/test 绿（见 05 更新记录）。PASS

## Architecture Check

- canonical 单点模式与 PC-008 不变量一致，lens 可复用于后续协议客户端；
- 通知失败仅 Warn 的「尽力而为」语义在 05 Known Limitations 披露，未静默吞错（有 Warn 日志）；
- 未引入新依赖（零 SDK 铁律保持）；csproj/plugin.json 版本一致性由发布链核对。PASS

## Risk

- 低。行为变化集中在「严格服务器握手」（修复目标本身）与「/test 响应 data 增字段」（纯增量，既有消费方只读旧字段不受影响）。回滚点：本批全部改动未提交，`git checkout` 即回。

## Findings

### Critical

无。

### Major

无（第 4 缺陷 `/test` 契约在门禁②发现、当批闭环，未逃逸到发布）。

### Minor

1. 走查确认 `v-loading` 未注册、窄视口卡片布局劣化 → 已入 TODO 条目③④（不悬空）。
2. G3 grep 签名覆盖面限传输层目录 → lens 第 4 问兜底，已写入 PC-008 §2 盲区。

## Final Decision

**APPROVED** —— S1-S5 + 契约缺陷全绿收口，Minor 项全部落 TODO/PC 无悬空，放行门禁④发布。
