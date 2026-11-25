# 00 Repository Understanding

- date: 2026-11-25
- task-id: `theme-system-resolve`
- repository: `OpenForgeSelf/OpenForgeSelf`

## Repository facts

- Frontend: Vue 3.5 + TypeScript + Pinia + Element Plus
- Theme state: `ForgeSelf.Web/src/stores/theme.ts`
- Appearance control: `ForgeSelf.Web/src/components/settings/AppearancePanel.vue`
- Sidebar control: `ForgeSelf.Web/src/components/Sidebar.vue`
- Unit tests: `ForgeSelf.Web/src/stores/__tests__/theme.test.ts` and `ForgeSelf.Web/src/__tests__/SettingsView.test.ts`
- Root theme application: `data-theme`, `data-theme-mode`, `html.dark`, `colorScheme`

## Applicable problem classes

- PC-025: UI theme/dark contrast
- 触发原因：主题控制把“跟随系统”暴露为与浅色、深色并列的第三值，破坏主题状态模型的可枚举语义。
- No existing case is known for this exact theme-mode presentation bug.
