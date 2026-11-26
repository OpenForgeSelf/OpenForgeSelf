# 05 Evidence

> Stage 7 | 只记录实际发生的事情

## Changed Files

- `ForgeSelf.Web/src/stores/theme.ts:4` — actual theme enum split from follow-system policy
- `ForgeSelf.Web/src/components/settings/AppearancePanel.vue:44` — two theme radios + independent follow-system switch
- `ForgeSelf.Web/src/components/Sidebar.vue:23` — sidebar exposes only shallow/dark choices
- `ForgeSelf.Web/src/stores/appearance.ts:137` — solid background sync with resolved theme
- `ForgeSelf.Web/src/App.vue:35` — app-level resolved-theme to background sync
- `ForgeSelf.Web/src/stores/__tests__/theme.test.ts:111` — follow-system/migration/manual override tests
- `ForgeSelf.Web/src/__tests__/SettingsView.test.ts:74` — appearance control tests
- `ForgeSelf.Web/src/__tests__/appearance-store.test.ts:103` — solid/image background sync test

## Red

Command:

```bash
cd ForgeSelf.Web
pnpm exec vitest run src/stores/__tests__/theme.test.ts src/__tests__/SettingsView.test.ts
```

Result: FAIL（Verified）

Two failures reproduced the old behavior:

- store root mode remained `system` instead of resolved `dark/light`
- Settings appearance rendered 3 theme buttons instead of 2

## Green

| Check | Result | Evidence |
|---|---|---|
| Focused theme tests | PASS 43/43 | theme + settings + appearance store suites |
| Frontend check | PASS | vue-tsc + eslint; only pre-existing lint warnings |
| Full frontend unit suite | PASS 123 files / 1281 tests | Verified |
| Design guard | PASS | style-tokens and design-compliance pass |
| VS Code diagnostics | PASS | no errors in changed source files |

## Live UI

Verified via source dev stack:

- Frontend: `http://localhost:7399`
- Backend: `http://localhost:7301`
- Dark follow state:
  - `data-theme=dark`
  - `data-theme-mode=dark`
  - html `.dark=true`
  - `follow=true`
  - shallow/dark radio: shallow unchecked, dark checked
  - solid background: `rgb(11, 9, 8)`
- Light follow state:
  - `data-theme=light`
  - `data-theme-mode=light`
  - html `.dark=false`
  - `follow=true`
  - shallow/dark radio: shallow checked, dark unchecked
  - solid background: `rgb(244, 238, 223)`
- Manual shallow state while system media is dark:
  - `data-theme=light`
  - `follow=false`
  - storage mode `light`

Screenshots:

- `ForgeSelf.Web/screenshots/dev/theme-system-follow-dark.png`
- `ForgeSelf.Web/screenshots/dev/theme-system-follow-light.png`

## Acceptance Coverage

| AC | Evidence |
|---|---|
| AC1 | Settings/Sidebar unit assertions plus live Appearance panel snapshot show only shallow/dark radios |
| AC2 | Live dark/light follow states show resolved radio and html state |
| AC3 | Live emulated system light/dark change updates resolved radio and root theme |
| AC4 | Live manual shallow state under dark system media stayed light with follow=false |
| AC5 | theme.test legacy system migration test passed |
| AC6 | focused 43/43, full frontend 1281 tests, check/design guard passed |

## Known Limitations

- No new Playwright spec was required for this shell-level unit/live walk; live browser verification was done directly against the source dev stack.
- No git commit or push was performed.
