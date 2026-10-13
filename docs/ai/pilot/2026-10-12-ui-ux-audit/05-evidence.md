# 05 Evidence — UI/UX 走查修复

## 验证方式
- 浏览器 dev-stack :7399 实际操作 + 截图读图
- `pnpm run check` 0 errors / 144 warnings（历史遗留）

## 已验证项
- [x] 遮罩渐变从 color-mix 改 rgba+calc 后 computed 正常渲染
- [x] 纯色切换整页背景真变（浅米/墨黑/深蓝逐个点）
- [x] 浅米色自动切浅色主题（白色卡片+深色文字）
- [x] 切回墨黑自动切暗色主题
- [x] 设置页 10 个 tab 无重复标题
- [x] TagInput chip 玻璃淡白样式（编辑 Agent 弹窗）
- [x] AgentHub eyebrow+serif 标题+玻璃卡片
- [x] 熔炉预设图切换正常
- [x] 内容区 el-card 透明，浅色下不突兀

## 来源等级
- 全部 Verified（浏览器实测截图）
