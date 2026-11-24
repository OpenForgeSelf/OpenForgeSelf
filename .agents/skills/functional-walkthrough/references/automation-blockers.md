# 走查自动化阻塞登记（2026-11-10 · 建单规则编号）

供下轮走查对照；根因在 **Vue 合成事件 + Element Plus**，非产品逻辑。

| 步骤 | 阻塞 | 有效绕过 | 不能证明什么 |
|------|------|----------|--------------|
| 点「新增」 | `browser_click` 只聚焦 | Vue `openCreate()` | 纯 click E2E 未测时需标待核实 |
| 点「编辑」规则 | 同上 | Vue `openEdit(rule)` | 同上 |
| 登录密码 | `browser_fill` len=0 | 原生 setter + InputEvent | — |
| 抓 POST | fetch hook 空 | XHR hook 或 UI toast+traceId | — |
| 详情 | REST Detail2 40401 | 点「查看」抽屉 | API 路径≠UI 真源 |
| 列表搜规则 | 未点 Search | CDP 改 input value | 与点 Search 路径可能不同 |
| sqlcmd 密码 | auto-review 拦截 | 不用明文密码命令；用已有 API 证据 | — |
| API 首启 | 种子早于列 | `MembershipSchemaReadyGate` | 冷启须再验证 |

更新本表：每次走查在报告 §6 追加新行，重复模式合并到本文件（经用户确认后改 SKILL 主表）。
