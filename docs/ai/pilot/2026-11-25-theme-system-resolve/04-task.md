---
task_id: theme-system-resolve
feature: 047
risk: { level: L1, reason: "Theme state/model and UI coupling only; no backend or database change" }
expected_files: ["ForgeSelf.Web/src/stores/theme.ts","ForgeSelf.Web/src/components/settings/AppearancePanel.vue","ForgeSelf.Web/src/components/Sidebar.vue","ForgeSelf.Web/src/stores/appearance.ts","ForgeSelf.Web/src/App.vue","theme/settings/appearance tests"]
rollback: "Restore the five source/test files and remove the independent follow-system key via git diff"
---

# 04 Task

## Objective

Remove “follow system” as a third independent theme choice while preserving system-follow behavior through a separate switch and keeping shallow/dark as the only actual theme values.

## Scope

### Allowed

- `ForgeSelf.Web/src/stores/theme.ts`
- `ForgeSelf.Web/src/components/settings/AppearancePanel.vue`
- `ForgeSelf.Web/src/components/Sidebar.vue`
- `ForgeSelf.Web/src/stores/__tests__/theme.test.ts`
- `ForgeSelf.Web/src/__tests__/SettingsView.test.ts`
- This pilot directory

### Forbidden

- Backend/auth/database changes
- Appearance background, accent color, animation behavior
- Proxy “跟随系统” implementation
- Unrelated plugin or UI refactoring

## Acceptance Criteria

- [ ] 只有浅色、深色两个主题选项按钮。
- [ ] 跟随系统仍可通过独立开关启用。
- [ ] 开关开启时自动将选中态设置为系统解析出的浅色或深色。
- [ ] 系统偏好变化时同步实际主题；手动选择后不响应覆盖。
- [ ] 根节点实际 theme 属性不再暴露 `system`。
- [ ] Legacy storage migration passes.

## Verification Commands

```bash
cd ForgeSelf.Web
pnpm exec vitest run src/stores/__tests__/theme.test.ts src/__tests__/SettingsView.test.ts
pnpm run check
pnpm exec vitest run
pnpm run design:check
```
