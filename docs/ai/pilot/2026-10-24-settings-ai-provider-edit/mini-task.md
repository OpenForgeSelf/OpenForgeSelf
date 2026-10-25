# Mini Task：设置页 AI 提供方编辑浮层显示不完整

> Task ID：2026-10-24-settings-ai-provider-edit｜task_kind: defect｜日期：2026-10-24｜轻量缺陷修复（以本文件合并 Intent / Spec / Plan / Task）
> 闸门1：用户已指定采用 Element Plus `el-dialog` 编辑并关注透明度；按该交互方向实施。

## Repository Understanding

`SettingsView.vue` 在设置页挂载 `AiProvidersPanel.vue`。既有提供方点击编辑后由该组件的 `openEdit` 填充表单，并显示 `el-drawer`（约第 565 行）。用户澄清实际问题是编辑内容留在设置容器内显示，未成为独立弹窗，表单内容看不全；并明确要求改用 Element Plus `el-dialog`。现有 `AiProvidersPanel.test.ts` 没有编辑弹窗隔离/完整展示的回归用例。

## Intent / Problem

修复设置页 AI 提供方编辑入口：点击编辑后使用 Element Plus `el-dialog` 独立显示，不受设置容器尺寸约束，用户能看全并操作表单。

## Spec

### Functional Requirements

- 点击既有提供方的编辑按钮，使用 Element Plus `el-dialog` 显示独立于设置容器的编辑弹窗，表单可完整查看和操作。
- 保持现有字段回填、API Key 留空代表保留原密钥、取消不保存、保存更新既有提供方的行为。
- 同组件内其他同类编辑浮层按同根因扫查；只修复确认同一挂载问题的命中项。

### Input / Output and Boundaries

- 输入：用户点击提供方卡片的编辑按钮。
- 输出：编辑表单以 `append-to-body` 的 `el-dialog` 出现；保存成功关闭并刷新列表，取消关闭且不发更新请求。
- 不改 API/数据库/鉴权/密钥处理契约、不重构无关设置功能、不触碰工作区现有其他任务改动。
- 透明度：复用 `bg-image-mode.css` 现有 `body.app-bg-image-mode .el-dialog` 主题规则；背景图模式保持半透明并验证正文、输入控件可读，普通主题沿 Element Plus 主题实底显示，不新增硬编码颜色。
- 未知：截图未显示视口完整尺寸及其他字段行为；实施时以组件回归和真实设置页截图确认弹窗相对视口定位、表单无裁切。

### Failure Modes / 问题类

- `docs/11-troubleshooting/problem-classes/README.md` 的 tags 未覆盖此 UI 浮层挂载问题；PC-001–PC-011 暂无直接匹配。按 `problem-class-closure` 建新问题类和 case，并做同类扫查。
- 根因假设：抽屉未传送到 body，受设置页容器的布局/层叠上下文限制。必须由红灯用例和实际页面截图验证；若不成立，先修订方案。

### Interaction Design

| 交互点 | 触发 | 结果 | 反馈/状态 | 边界 |
|---|---|---|---|---|
| 编辑提供方 | 点击编辑图标 | `el-dialog` 弹窗打开并回填既有字段 | 保存沿用成功/错误提示；取消关闭 | `append-to-body`、视口内滚动；背景图模式半透明且内容可读；API Key 初始留空且保留语义不变 |

### Acceptance Criteria

- [x] 编辑既有提供方时使用 Element Plus `el-dialog` 并传送至 body；组件测试确认弹窗位于 `.app-layout` 外。
- [ ] 背景图模式沿用现有弹窗透明度样式，标题/表单/按钮可读；普通模式保持主题默认背景。
- [x] 组件回归测试覆盖 body 级挂载、字段回填、取消不请求、保存调用 update、API Key 留空语义。
- [x] 本组件及同域设置弹窗扫查完成；命中表、PC/case 和索引已归档。
- [ ] `pnpm run check`、相关 Vitest、`pnpm run build` 通过；真实设置页截图确认表单可见且可操作。
- [x] PC/case 索引、05 Evidence、06 Review 与 07 Final Report 已生成；Review 因浏览器验证缺失为 BLOCKED。

## Plan

1. 在 `AiProvidersPanel.test.ts` 先增加弹窗独立性和编辑语义用例；尝试在修复前运行，但本机 pnpm/Vitest 解析路径失败，修复前红灯未取得。
2. 将提供方编辑抽屉改为 `el-dialog` + `append-to-body`；表单区域限高滚动，保持窄视口可用。
3. 检查背景图模式全局透明度规则，使用现有 token/规则而不另写固定色值；以截图验证透明度和文字对比。
4. 重跑相关测试与前端门禁；按项目 UI 走查要求在源码页面截图验证完整展示。
5. 按问题类闭环完成同类扫查、守卫、case/PC 归档和索引更新。

## Files To Change (planned)

- `ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue`：提供方编辑区改为 Element Plus `el-dialog`。
- `ForgeSelf.Web/src/__tests__/AiProvidersPanel.test.ts`：红灯→绿灯回归守卫。
- `ForgeSelf.Web/e2e/ai-providers.spec.ts`：更新 provider 编辑/新增/校验场景选择器并验证弹窗透明度。
- `docs/02-features/001-ai-provider-config.md`：同步编辑 UI 事实。
- `docs/11-troubleshooting/cases/2026-10-24-settings-provider-edit-container-clipping.md`：问题证据与同类扫查。
- `docs/11-troubleshooting/problem-classes/PC-012-settings-provider-edit-container-clipping.md`：问题类不变量和签名。
- `docs/11-troubleshooting/cases/README.md`、`docs/11-troubleshooting/problem-classes/README.md`、`docs/11-troubleshooting/index.md`：问题类索引。
- `.forgeself/memory/2026-10-24.md`、`TODO.md`：完成时归档。

## Verification Plan

```powershell
cd ForgeSelf.Web; pnpm run check
cd ForgeSelf.Web; pnpm run test -- AiProvidersPanel
cd ForgeSelf.Web; pnpm run build
pwsh scripts/check-problem-classes.ps1
```

之后按 `ui-ux-design` 走查清单在源码运行页面检查编辑浮层完整可见，并保存截图到 pilot 证据目录。

## 当前执行状态

- 实现：已将 provider 新增/编辑表单换为 `el-dialog append-to-body`，设置视口宽度上限，内容区限高滚动；透明度复用现有背景图主题 CSS。
- 组件回归：10/10 通过；PC-012 单项及全量问题类机器守卫通过。
- 全仓 `pnpm run check` / `pnpm run build` 被既有 `e2e/plugins/todo-tracker/todo-tracker.spec.ts` 未使用 `existsSync` 导入挡住；Vite bundle 曾通过一次，最后复跑在 `components.d.ts` 写入失败。
- Playwright 浏览器复验与截图未完成。首次启动遇到测试变量重名，已修；后续重跑被自动审核拒绝（首次启动已处理残留宿主 PID）。内置浏览器工具也无法初始化本机 Node kernel。
- **状态：BLOCKED，不能视为完成**。待恢复安全的隔离 E2E 环境后补跑 T021、透明度截图、完整 check/build 并更新证据。

## Rollback

仅回退本任务列出的文件；保留任务开始前已有的工作区改动。

