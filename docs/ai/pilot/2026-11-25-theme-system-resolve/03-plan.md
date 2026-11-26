# 03 Plan

## Implementation Steps

1. Refactor `ForgeSelf.Web/src/stores/theme.ts`
   - Split actual theme enum (`light`/`dark`) from system-follow policy.
   - Add independent follow-system storage key and migration for legacy `system`.
   - Keep root attributes aligned with actual resolved theme.
   - Start/stop matchMedia listener only while follow-system is enabled.

2. Refactor `ForgeSelf.Web/src/components/settings/AppearancePanel.vue`
   - Render only two radio buttons.
   - Render “跟随系统” as a separate switch.
   - Bind radio selection to resolved actual theme, not policy value.

3. Refactor `ForgeSelf.Web/src/components/Sidebar.vue`
   - Remove system from the three-button theme mode array.
   - Keep only shallow/dark actual choices.
   - Highlight based on resolved theme so follow-system remains visually consistent.

4. Update tests
   - Add red/green store tests for system resolution and manual override.
   - Update SettingsView tests to assert two theme buttons and follow-system switch.

## Test Plan

- Store unit tests: migration, initial follow, runtime system change, manual override.
- Settings unit test: 2 radio buttons, switch behavior, active actual mode.
- Frontend fast gate: `pnpm run check`, relevant vitest suites, `pnpm run design:check`.
- UI walkthrough: follow switch on with system dark/light and manual selection; screenshot/DOM evidence.

## Verification

- Fast: relevant theme/store tests + full frontend check/test + design guard.
- Medium: not required unless cross-host changes are introduced.
- Deep: not required unless shared E2E infrastructure changes.
