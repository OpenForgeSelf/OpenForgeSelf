# 06-review：McpCenter 网关 Stateless 会话模式做成配置项（2026-10-17，v2.4.0）

> Reviewer 八问（规范 §Stage 8）。审查对象 = 本批净改动（McpCenter 插件后端 4 文件 + 前端 2 文件 + 版本 2 文件 + 测试 3 用例 + e2e spec 修复 + 034 契约回写 + 流程工件）。

## 八问结论
| # | 问题 | Check | 说明 |
| --- | --- | --- | --- |
| 1 | 是否真正满足 Intent？ | **PASS** | Intent=「Stateless 入配置、改完重启网关生效、以后不改代码发版」；V5a 实测 PUT 双向热切换 + env 覆盖 + 老配置缺键自动转无状态，目标逐条有证据。 |
| 2 | 是否符合 Spec？ | **PASS** | S1-S6 全部落地且与拍板一致（默认 true、三级优先序、PUT 可空部分更新、复用 ApplyUpdateAsync 热重启、前端开关+常驻文案、2.4.0）；边界未越（未动鉴权/universal_tool/dsh 面板）。 |
| 3 | 是否超出 Scope？ | **PASS（有说明）** | e2e spec 的 clientInfo.version / SSE 解析 / tab 步骤、serverInfo 契约回归、toast 文案笔误——均为**本批门禁过线或本批产物自身的必要修复**；发现的无关既有缺陷（dsh vue-tsc 3 错、v-loading、listChanged）只登记 TODO 不顺手修（§1.3）。 |
| 4 | 是否改了不该改的文件？ | **PASS** | 宿主零改动；`McpGatewayServer.cs` 的 ServerInfo 配置属 034 既有契约的回归修复（全仓 grep 证实 SDK 化后无任何 ServerInfo 配置点）。 |
| 5 | 测试覆盖 Acceptance Criteria？ | **PASS** | 新增 3 条单测锁配置链（默认/显式 false/env）与热重启往返；e2e 在无状态默认下全绿锁协议面；V5a A/B 实测锁运行期行为。mini-task V1-V5 在 05 均有 Verified 记录。 |
| 6 | 明显回归风险？ | **PASS（低）** | 唯一行为变更 = 升级后默认转无状态——恰为用户拍板的根治目标，且页面可一键切回有状态（行为回退不需 revert）；apply 失败回滚快照已测。老客户端（带 session header 打无状态网关）：无状态忽略 session 直接应答，无破坏。 |
| 7 | 架构不一致？ | **Minor** | listChanged 断言放宽为「tools 存在」而非严格契约——受 SDK 能力所迫，034 已按实况改写并登记 TODO P3，不是静默妥协。 |
| 8 | Evidence 足以证明任务完成？ | **PASS（截图除外）** | 命令+输出+计数实测；**走查截图两轮均因浏览器窗口不可见失败**，以 e2e 截图+DOM 核对+dist 实证替代并如实标注——发布后第⑤步（运行实例只读复验）仍须补视觉证据。 |

## Findings
- **Minor-1**：toast 文案笔误（「客户端重启宿主后无需重连」主语写反）在首轮走查中被子代理发现，属本批引入——已补修并 dist 重建实证（旧串 0 命中）。教训：本批新增用户文案应逐句复读，不能只对功能。
- **Minor-2**：`FORGESELF_MCP_GATEWAY_PORT=18895` 是 dev-stack 验证的临时端口选择（避开用户 :51888 实例占用 18890），非配置变更；e2e 默认 19000+ 动态端口约定不受影响。
- **Major-0 / Critical-0**：无。

## Risk
**L2（中低）**：插件内配置链 + 传输层开关，无数据库/宿主/契约破坏；版本 2.4.0 走插件更新通道（不重启宿主即热重启网关）。回滚点 = git revert 本批提交；运行态回退 = 页面切回「有状态」。

## Final Decision
**APPROVED**（待闸门2 用户验收；git 提交与 V6 发布未获授权前不执行；plugin-development ④⑤步依赖发布后在 :51888 实例复验）。
