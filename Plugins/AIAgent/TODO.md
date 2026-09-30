# AIAgent 插件 TODO（插件级遗留）

> 条目格式：`- [ ] <事项>（P<优先级>，来源）`；完成即移除，不留 ✅ 堆积（AGENTS §7.5）。

## ⬜ 待办

- [ ] **目录浏览弹层调用不存在的后端端点（P2，来源:输入1 附带发现；输入13 迁移自根 TODO.md）**：`Plugins/AIAgent/web/src/components/DirectoryPickerDialog.vue:67` 调 `/api/project/browse-directories`，全仓 grep 零命中（无后端实现）→ 弹层 404。跨插件（涉及宿主 IProjectRegistry 面或 sems 同类目录能力），待排期实现或下线该入口。
