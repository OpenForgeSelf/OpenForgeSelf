# 05 Evidence — OAuth App 内置 ClientId（O2）

| 项 | 证据（来源等级：Verified，2026-11-23 实测） |
| --- | --- |
| appsettings GitHubOAuth 节 | `GitHubOAuthDefaultsGuardTests` 走读仓库 appsettings.json 锚定 `ClientId=Ov23likdtDN2AlQvAO33`（4/4 绿） |
| FromConfiguration/IsConfigured | 同上（缺省空 ClientId → IsConfigured=false 分支断言绿） |
| e2e 前置 | global-setup.ts 步骤 1.4 就位（publish appsettings ClientId 正则置空，日志行输出）；spec 未配置分支整跑 Pending（主任务 05 §3 登记环境依赖） |

遗留：无（配置层批）。
