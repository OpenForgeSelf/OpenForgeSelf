# 05 Evidence — 绑定 UX 二轮

| 项 | 证据（来源等级：Verified，2026-11-23 实测） |
| --- | --- |
| 前置检查/armed/续装 | `PluginStore.vue` 集成编译过（vue-tsc app 配置本批零红）；组件行为走查记录见主任务 05 §2 AC12（e2e 整跑 Pending） |
| AC-11 守卫 | `workshop-forge.contrast.test.ts` 6/6 绿（含 AC-11 三组合 × 明暗 = 6 断言组） |
| 错误透传文案 | `GithubBindPanel.test.ts` start 失败/终态三测绿（合并文案直呈断言） |
| 主题欠账分流 | EP 状态横幅暗色 <4.5 → 已记 TODO P2（另批修，未混入本批） |

来源等级：Verified（过滤集测试）；Inferred（armed-resume 的 e2e 整跑）。
