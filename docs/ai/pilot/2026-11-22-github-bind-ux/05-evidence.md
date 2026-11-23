# 05 Evidence — 绑定 UX 一轮

| 项 | 证据（来源等级：Verified，2026-11-23 实测） |
| --- | --- |
| 四态流转 | `GithubBindPanel.test.ts` 14/14 绿：preparing 文案与防连点（结构性断言按钮消失）/自动复制横幅/复制失败引导/3s 倒计时自动开预填页（stubGlobal open 记录断言）/手动复制/pending 节奏/slow_down 退避/bound 事件/expired+denied+failed 透传/start 失败透传 |
| 剪贴板工具 | `clipboard.test.ts` 5/5 绿（secure-context / execCommand 兜底 / 失败 false） |
| 设置面板 | `GithubAccountPanel.test.ts` 7/7 绿：未绑定入口/已绑定行/解绑二次确认/开关两态刷新/失败回滚 |
| SettingsView 集成 | vue-tsc app 配置本批文件零红（pageHeadMap 补 `github-account` 项） |
| 类型净 | `pnpm run check` 过滤口径：本批文件零类型/lint 红（插件基线红对表见主任务 05 §4） |

遗留：走查截图与 e2e 整跑 Pending（主任务 05 §3 登记）。
