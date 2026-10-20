# AI-Native Pilot Result（闭环汇报）

## 1. Repository Understanding

外部 MCP 客户端 = McpCenter 插件内零 SDK 手写三传输（Stdio/StreamableHttp/LegacySse）→ McpClientSession → McpClientManager → McpExternalController（`api/mcp-center/servers` CRUD + connect/disconnect/tools/test）；前端 McpCenterView.vue 网关配置 tab 消费 `http.ts` apiPost（只解包 data）。复现批已定位：initialize 载荷手搓缺规范必填 clientInfo、openEditExternal 硬编码空值、StateDto 类型丢字段。详见 00。

## 2. Selected Task

PC-008 修复批（闸门1 = 用户「开工」2026-10-17）：S1 三传输 clientInfo canonical、S2 notifications/initialized、S3 会话头回传、S4 编辑全回显 + 掩码不往返、S5 兼容面零回归；执行中按 problem-class-closure 同类扫查追加 `/test` data 契约修复（门禁② e2e 阻塞缺陷）。

## 3. Changed Files

见 05-evidence「Changed Files」表（生产 8 + 测试 5 + e2e 1 + 脚手架 1 + 文档/版本 6）。

## 4. Validation

- 后端：`dotnet test ~McpCenterTests` **121/121**；`~ProblemClass` **8/8**；`check-problem-classes.ps1` **8 PC 全过**；版本 bump 后复跑 build+test 绿。
- 插件前端：build 绿 / check 0 err / test **23/23**。
- e2e：mcp-center **5/5**（含新「外部 MCP UI 主链路 v2.5.0」7 证据）。
- dev-stack 走查：API 实测 `/test` data 含 success/durationMs + UI 5 步全 PASS + 截图读图核验（screenshots/g3-devstack-20261017/01-05）；测试数据已清、栈已停。

## 5. Evidence

05-evidence.md（全节 Verified；红灯→绿灯对照在复现批 05 与本批 G1 条目）。

## 6. Review

06-review.md：**APPROVED**（Critical/Major 无；Minor 2 条全部落 TODO/PC）。

## 7. Risk

低：改动集中于握手合规（修复目标）与 /test data 纯增量字段；未提交状态 `git checkout` 即回滚；宽松服务器兼容有 A/B 证据。

## 8. Problems Found

1. 第 4 缺陷（本批新发现，Verified）：`/test` 成功时 data 只有 `{id,ok}`，信封 message 被 `http.ts` 解包丢弃 ⇒ 握手成功仍误报「测试失败」；且 ElMessage 3s 自动消失造成 e2e 快照「无 message」假象，排障耗 4 轮。
2. 基建缺陷：`new-problem-case.ps1` 对 int 序列 `Measure-Object -Maximum` 返回 Double，`{0:D3}` 必崩 ⇒ PC 创建段不可用（已修，实测绿）。
3. 既有债确认（非本批引入）：DshMcpConfigDto 前端类型缺字段（本批最小解封）、v-loading 未注册、窄视口卡片劣化——均已在 TODO。

## 9. Process Evaluation

- Loop Engineering 闭环有效：复现批红灯 → 修复批 G1 转绿 → G2/G3 守卫 → PC 归档一次贯通；e2e「主链路用户路径」比单测更早逮到契约脱节（门禁②的价值实证）。
- 缺口登记：e2e 断言瞬时 UI（ElMessage）需按「渲染生命周期」写窗口（3s 内断言/截图），此类经验已入 case §9；重复教训「mock 宽松度掩盖必填项」上浮为 PC-008 lens 第 1/4 问。

## 10. 问题类归档

**PC-008**（`docs/11-troubleshooting/problem-classes/PC-008-mcp-external-client-protocol.md`，status=guarded）+ case **2026-10-17-mcp-external-client-protocol**；三索引（problem-classes/README 检索表+总表、cases/README、index.md 速查 -32602 入口）齐；机器校验 = `check-problem-classes.ps1` 绿 + Registry 测试 8/8。守卫：G1 红灯回归 + G2×3（协议形状/会话头/端点契约）+ G3（pre-commit 源码签名）。

## 11. 重构提案

R1（任务内已做）：initialize 载荷收敛 `McpClientProtocol` 单点，三传输去重。**R2+ 无提案**——理由：客户端侧已 canonical 化、无跨模块重复；「客户端侧改用官方 C# MCP SDK」属架构级 R3，需另行 `architecture-design` 立项评估（本批不做，非悬空：不属遗留问题，是明确否决的路线选择，见 case §6）。

## 12. 悬空教训清单（必须为空）

| 教训/发现 | 落点 |
|-----------|------|
| v-loading 未注册（走查复现） | TODO「McpCenter 插件前端视觉/指令缺陷」③ |
| 窄视口外部卡片布局劣化 | 同条目④（含证据截图路径） |
| dsh 视图旧链路迁移（types 已解封，链路未迁） | TODO「McpCenterView dsh 面板 vue-tsc 类型残留」既有条目 |
| ElMessage 瞬时 toast 断言生命周期 | PC-008 case §9 基建改进（e2e 排障经验） |
| mock 宽松度掩盖规范必填项 | PC-008 lens 第 1/4 问（Spec 阶段必答） |

**悬空 = 0。**
