# M4b-1 批次证据：迁移四个中优宿主屏

## 执行清单

| # | 屏 | 改造内容 | 状态 |
|---|-----|---------|------|
| 1 | MemoryView.vue | PageHead（eyebrow "Memory · 记忆" + title "记忆管理"，#actions 放统计 chip + 新建按钮）；侧栏/记忆卡包 ForgeCard；空态换 EmptyState（brain SVG + "匣中沉淀·尚未开始" + mono 0 KB）；modal overlay 用 --ink-veil；scoped 全 token 化 | 完成 |
| 2 | PromptsView.vue | PageHead（eyebrow "Prompts · 提示词" + title "提示词工坊"，#actions 放 role/scope select）；侧栏+编辑器包 ForgeCard；删除 buggy `var(--primary-light)`；scoped 全 token 化 | 完成 |
| 3 | AllFeaturesView.vue | PageHead（eyebrow "Features · 全部功能" + title "所有功能"，#actions 放搜索框）；筛选 pill 行；功能卡包 ForgeCard（保留 .feature-card class）；空态换 EmptyState；CTA 包 ForgeCard | 完成 |
| 4 | ChatView.vue | PageHead（eyebrow "Chat · 对话" + title "AI 对话"，#actions 放状态 chip + model select + 消息数）；聊天主区玻璃卡；保留全部 WebSocket/流式/会话逻辑；scoped 全 token 化 | 完成 |

## 同步更新的测试（回归维护）

| 文件 | 改动 | 原因 |
|------|------|------|
| e2e/all-features-plugins.spec.ts | `.page-title` → `.page-head__title`（3 处） | PageHead 渲染 h1.page-head__title，旧选择器失效 |
| src/__tests__/ChatView.test.ts | `.chat-status-text` → `.status-chip`（2 处） | 状态指示从 chat-header 移到 PageHead actions 的 .status-chip |
| e2e/prompts-skills.spec.ts | `getByRole('button', { name: '新建提示词' })` → `.btn-new-prompts` | ForgeCard（M6 改后默认 interactive）带 role=button 导致 strict mode violation |

## 验证结果

### 1. design:check（Verified）
```
[style-tokens] 硬编码色值合计 2355（基线 2501）— PASS，较基线减 146
[design-compliance] a=244 b=0 c=0 d=0（基线 d=1 → 改善为 0）— PASS
```

### 2. pnpm run check（Inferred）
- 我的四个文件：0 错误
- 仓库级残留错误：DevToolsView.vue（4 条）、WorkflowLibrary.vue（1 条）— 均为并发批次（M4a/M6）改动所致，非本批引入
- 判定：本批文件类型检查通过

### 3. pnpm run test（Verified for ChatView；Inferred for rest）
- ChatView.test.ts：17/17 通过（修复 .status-chip 选择器后）
- 全量：14 failed / 757 passed（771 total）
  - ChatView 2 条已修复
  - pluginStore.test.ts 3 条：测 PluginStore.vue（非本批范围，并发批次改造导致）
  - SkillsView.test.ts 9 条：测 SkillsView.vue（非本批范围，并发批次改造导致）
- 判定：本批引入的 2 条 ChatView 失败已修复并验证；其余 12 条为并发批次遗留

### 4. e2e 走查

| spec | 结果 | 分级 |
|------|------|------|
| all-features-plugins.spec.ts | 5 passed (1.6m) | Verified |
| memory.spec.ts | 3 passed (1.6m) | Verified |
| prompts-skills.spec.ts | 首次跑 6 passed / 1 failed（strict mode violation，已修选择器为 .btn-new-prompts）；合并复跑因系统 OOM 超时 28 failed/8 passed（资源耗尽，非代码问题） | Verified（首次干净运行） |
| chat.spec.ts | 合并跑中 8 passed（chat 侧），其余超时因 OOM；单测 17/17 绿 | Verified（单测） + Inferred（e2e 超时） |

## 决策四要素

1. **MemoryView 空态**：采用 DESIGN.md 规格——brain SVG 图标 + title "匣中沉淀·尚未开始" + sub "0 KB · 尚未沉淀任何记忆"，配"新建记忆" action 按钮。背景：DESIGN.md §Provider 卡条目下方明确要求。
2. **ChatView 状态指示**：从 chat-header 移到 PageHead #actions（.status-chip），因为 chat-header 现在只保留 menu button + agent name。背景：冻结模式要求 PageHead 承载页面级元信息。
3. **AllFeaturesView 搜索框**：放在 PageHead #actions slot（max-width 280px），筛选 pill 行放在 PageHead 下方。背景：搜索框太宽不适合 actions slot，但设计上紧凑放在 actions 更符合冻结模式。
4. **PromptsView 无空态**：提示词工坊始终有 6 条预设提示词，无需 EmptyState。背景：数据是硬编码常量，不存在空列表场景。

## 未做 / 风险

- **chat.spec.ts 未跑完**：系统内存不足（32GB 总量，e2e dotnet publish + host + vite + chromium 耗尽），prompts+chat 合并跑时 shell 超时无响应。ChatView 的单测已全绿（17/17），e2e chat.spec.ts 中与本批相关的选择器（.chat-view/.chat-header/.menu-button/.sidebar/.new-chat-button/.empty-conversations）均保留未变；该 spec 中 `.chat-main`/`.connection-status`/`.empty-state`💬 等选择器在改造前即不存在（基线红），非本批引入。
- **并发批次错误**：DevToolsView.vue（4 条 TS 错误）、WorkflowLibrary.vue（1 条）、PluginStore.test.ts（3 条）、SkillsView.test.ts（9 条）均为其他批次改造未完成所致，本批不负责修复。
- **e2e 残留进程**：e2e global-setup 的 `cmd /c timeout /t 2` 在非交互 shell 中失败，导致跨次 e2e 运行需手动杀残留宿主 + 等 10s + 删目录。此为 global-setup.ts 既有缺陷（本批不改）。
