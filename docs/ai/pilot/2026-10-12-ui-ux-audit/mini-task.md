---
task_id: 2026-10-12-ui-ux-audit
feature: none
risk: { level: L1, triggers: ["ForgeSelf.Web/src/**/*.vue", ".agents/skills/ui-ux-design/**"], raised_by_agent: true }
gate1: { mode: auto, at: 2026-10-12T08:30, ref: "user goal 自动迭代" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Web/src/App.vue
  - ForgeSelf.Web/src/views/SettingsView.vue
  - ForgeSelf.Web/src/components/settings/AppearancePanel.vue
  - .agents/skills/ui-ux-design/SKILL.md
rollback: "git revert HEAD"
---

# Mini-Task: UI/UX 全站统一走查与修复

## Intent（为什么/做什么/到什么程度）

用户要求：完善 ui-ux 技能验收标准（调研行业标准补漏）→ 系统化逐页走查 → 不达标就修 → 新问题回写规范 → 循环直到全站 UI/UX 统一对齐设计稿。

## Spec（验收标准）

### 已修 P0（本批已完成）
1. **遮罩浓度变量错配** — App.vue:54 img opacity 绑旧 `backgroundOpacity`，滑块调新 `overlayOpacity`。修：img 全不透明铺满，遮罩层负责压暗。✅
2. **字体大小假设置** — AppearancePanel `fontSize=ref(15)` 未绑 store/未应用 html。修：watch → `document.documentElement.style.fontSize` + localStorage 持久化。✅
3. **PageHead 硬编码** — SettingsView PageHead 写死"通用设置"，切 tab 不变。修：`pageHeadMap[activeCategory]` 动态。✅

### 走查清单（逐页勾）

| # | 页面 | 路由 | 状态 | 发现问题 |
|---|------|------|------|---------|
| 1 | 首页 | /home | ✅ 走查 | hero/指令栏/12功能卡/5看板，玻璃卡 OK |
| 2 | AI Agent 工作台 | /ai-agent | ✅ 走查 | 三栏布局 OK |
| 3 | 设置-外观 | /settings#appearance | ✅ 走查 | PageHead 动态修复；背景12纯色+6预设图+URL+遮罩 |
| 4 | 设置-通用 | /settings#general | ✅ 走查 | PageHead 动态修复 |
| 5 | 技能管理 | /skills | ✅ 走查 | 面包屑已删，PageHead"技能工坊"正常 |
| 6 | 系统监控 | /system-monitor | ✅ 走查 | 磁盘百分比已 toFixed(1) |
| 7 | AI Agent 列表 | /agents | ✅ 走查 | PageHead"接入·调度·记忆" OK |
| 8 | 提示词 | /prompts | ✅ 走查 | 标题"提示词工坊" OK |
| 9 | 工作流 | /workflows | ✅ 走查 | "工作流·执行·调度" OK |
| 10 | 代码片段 | /code-snippets | ✅ 走查 | PageHead + 空态 OK（之前导航错 /snippets 误以为空白） |
| 11 | 待办 | /todo | ✅ 走查 | 插件 TodoTracker 页，标题非 PageHead（P1 插件侧） |
| 12 | 插件市场 | /plugin-store | ✅ 走查 | PageHead + 分类侧栏 + 卡片 OK |
| 13 | 插件管理 | /plugins | ✅ 走查 | 同插件市场 |
| 14 | 记忆 | /memory | ✅ 走查 | "记忆管理" OK |
| 15 | 开发者工具 | /dev-tools | 待走查 | |
| 16 | 文本工具 | /text-tools | 待走查 | |
| 17 | 文件工具 | /file-tools | 待走查 | |
| 18 | IM 网关 | /im-gateway | 待走查 | |
| 19 | SEMs | /sems | 待走查 | |
| 20 | 成本 | /cost-scope | 待走查（已知500） | |
| 21 | MCP 中心 | /mcp-center | 待走查 | |
| 22 | 快捷链接 | /quick-links | ✅ 走查 | "快捷链接" OK |
| 23 | 聊天记录 | /chat-records | ✅ 走查 | "聊天会话查看" OK |
| 24 | Agent Hub | /agent-hub | 待走查 | |
| D1 | 编辑 Agent 弹窗 | dialog | 待走查 | 用户报"字体背景对不上" |
| D2 | 插件设置弹窗 | dialog | 待走查 | 用户报"背景很黑" |
| D3 | 快捷链接编辑 | modal | 待走查 | |
| D4 | SEM 项目编辑 | dialog | 待走查 | |

### 已补技能检查点（ui-ux-design SKILL.md）
- A. 主题/背景切换专项（12纯色逐个点/遮罩实时生效/浅色可读）
- B. 弹窗/模态专项（dialog背景走token/插件弹窗跟主题/不黑底黑字）
- C. 设置页/页头对齐一致性
- D. 字体/对比度 WCAG AA
- E. 插件前端专项
- F. 可访问性（WCAG 2.2 AA：focus可见/aria-label/键盘/reduced-motion）
- G. 组件四态（hover/focus/active/disabled/加载/表单错误）
- H. 间距/圆角/对齐一致性
- I. 响应式窄屏

## Plan（怎么改）

1. 已修 3 个 P0（遮罩/字体/PageHead）— check 0 errors / vitest 789 全绿
2. 继续逐页走查（上表 7-24 + D1-D4）
3. 发现问题就修，新问题类型追加到技能
4. /skills 面包屑→PageHead 统一（P1）
5. /monitor 空白排查（P1）

## Task（验证命令）

- `cd ForgeSelf.Web && pnpm run check` — 0 errors
- `cd ForgeSelf.Web && pnpm run test` — 全绿
- `cd ForgeSelf.Web && pnpm run design:check` — 无新增违规
- dev-stack :7399 截图走查每页
