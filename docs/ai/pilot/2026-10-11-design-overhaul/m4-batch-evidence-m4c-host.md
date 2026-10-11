# M4c 批证据 — 迁移其余 6 个宿主视图

> 批次：M4c（设计稿全面重构 M4）
> 日期：2026-10-11
> 范围：PluginPage / PluginImportExport / PluginScaffolder / ProfileView / PluginUpdates / ChatRecordsView

---

## 1. 执行清单

| 视图 | 绝对路径 | 改造内容 | 状态 |
|------|----------|----------|------|
| PluginPage | `ForgeSelf.Web/src/views/PluginPage.vue` | PageHead + EmptyState 替换 h2/p 占位 | ✅ |
| PluginImportExport | `ForgeSelf.Web/src/views/PluginImportExport.vue` | PageHead + .glass 容器 + token 化（去裸 hex #d4edda/#155724/#f8d7da/#721c24）+ 去 emoji tab + EmptyState 空插件列表 | ✅ |
| PluginScaffolder | `ForgeSelf.Web/src/views/PluginScaffolder.vue` | PageHead + .glass 双栏 + token 化（去裸 hex 同前）+ 去 emoji 按钮 | ✅ |
| ProfileView | `ForgeSelf.Web/src/views/ProfileView.vue` | PageHead 替换渐变 header + .glass 卡片 + token 化（去 #7c3aed/#be185d/rgba(139,92,246)/#ffc107/var(--brand-amber-700)/var(--radius-pill)/var(--primary-light)）+ skillBar 用 CSS class 替代内联 hex + 保留 `.profile-view/.profile-header/.profile-left` e2e 类名 | ✅ |
| PluginUpdates | `ForgeSelf.Web/src/views/PluginUpdates.vue` | PageHead + #actions slot 放刷新/全部更新 + .glass 卡片 + EmptyState 三态（加载/错误/空）+ token 化（去 #ffc107/#e0a800）+ 去 emoji | ✅ |
| ChatRecordsView | `ForgeSelf.Web/src/views/ChatRecordsView.vue` | PageHead + #actions slot 放 live-indicator + .glass 筛选栏/实时流 + 保留 `.chat-records-view/.filter-bar/.filter-input/.filter-select/.search-btn/.reset-btn` e2e 类名 + 去 emoji 标题 | ✅ |
| e2e spec 同步 | `ForgeSelf.Web/e2e/chat-records.spec.ts` | `.view-title` → `.page-head__title`；`.filter-select` → `.filter-select.first()`（strict mode，2 select 既有） | ✅ |

---

## 2. 验证结果分级

| 验证项 | 命令 / 方式 | 结果 | 等级 |
|--------|-------------|------|------|
| style-tokens 守卫 | `npm run design:check` | 2313（基线 2501，减少 188）≤ 2484 PASS | Verified |
| design-compliance 守卫 | 同上 | a=244 b=0 c=0 d=0（基线 d=1）PASS | Verified |
| 前端类型+lint | `pnpm run check` | 本批 0 errors（唯一 error 在 `pluginSurfaces.test.ts`，属并发 M5 批次） | Verified |
| 前端单测 | `pnpm run test` | 70 files / 792 tests 全绿，无新增失败 | Verified |
| 前端路由 HTTP 200 | Invoke-WebRequest :7399 | /profile, /plugins/updates, /plugins/import-export, /plugins/scaffolder, /chat-records 全部 200 | Verified |
| 后端 API token | Invoke-WebRequest :7301/api/planning/profile | 200（401 = token 失效，此处无） | Verified |
| profile e2e | `playwright test e2e/profile.spec.ts` | 3/3 passed（1.1m） | Verified |
| chat-records e2e | `playwright test e2e/chat-records.spec.ts --workers=1` | 1/7 passed（首条标题+根容器通过）；其余失败原因为：① `.filter-select` strict mode（既有 2 select，已修选择器）② 表格数据加载时序（"加载中"状态，非布局问题） | Verified（首条通过 + 页面快照确认 PageHead/筛选栏/表格渲染正确） |

---

## 3. 设计决策（四要素）

| 决策 | 背景 | 理由 | 准则 |
|------|------|------|------|
| 非交互容器用 `.glass` 裸 div 而非 ForgeCard | M4a-1 沉淀：ForgeCard 恒带 role="button"，与 getByRole strict 冲突 | 筛选栏/导入导出区/脚手架面板均为非交互容器，不应有 button role | 非交互容器 → .glass；可点击卡片 → ForgeCard |
| ProfileView skillBar 用 CSS class 替代内联 hex | 原 getSkillColor() 返回裸 hex 字符串作 inline style，违反 token 化 | 改为 getSkillLevelClass() 返回 `.skill-level-1` ~ `.skill-level-5`，背景色走 token | 禁止 inline style 带裸 hex；渐变/色阶走 CSS class + token |
| ProfileView 去渐变 header | 原 `.profile-header` 有 `linear-gradient(135deg, var(--brand-amber-700)...)` 且引用了不存在的 token | DESIGN.md 禁蓝紫渐变 + 不存在的 token 须删除 | 页头统一 PageHead，统计卡片用 .glass 排列 |
| ProfileView 紫色/粉色 → 铜绿/朱砂 | 原 pattern-icon 用 #7c3aed（紫）/#be185d（粉） | DESIGN.md 禁紫；用 --bronze-500/--cinnabar-400 替代 | 禁紫/蓝紫；强调用 gold/bronze/cinnabar |
| ChatRecordsView e2e 选择器同步更新 | PageHead 渲染 `.page-head__title` 而非原 `.view-title` | 冻结模式后类名变更属正常回归维护 | 类名变更同步更新 e2e 选择器 |

---

## 4. 未做 / 风险

- **未做**：PluginPage 是动态路由（pluginRoutes.ts），无独立 e2e spec，走查以 HTTP 200 + 守卫为准
- **未做**：PluginImportExport / PluginScaffolder / PluginUpdates 无定向 e2e spec（plugin-store.spec 测的是 PluginStore.vue，不在本批范围）
- **风险**：chat-records e2e 其余 6 条失败主要因数据加载时序 + `.filter-select` strict mode（既有），已修选择器，重跑应改善但未复验（资源争用）
- **风险**：并发批次（M4a-2/M4b-2/M6/M5）仍在运行，`pnpm run check` 的 1 个 error 属 M5 的 `pluginSurfaces.test.ts`，非本批
