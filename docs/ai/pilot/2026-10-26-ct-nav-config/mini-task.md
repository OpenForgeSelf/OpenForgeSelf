# Mini Task：组件画廊侧边栏导航配置（分组标题/顺序 + 深链保留 + CT 基线干净）

> Task ID：2026-10-26-ct-nav-config｜task_kind: feature｜日期：2026-10-26｜轻量（本文件合并 Intent / Spec / Plan / Task）
> 闸门1：用户输入15 明文指令「请帮我配置 Storybook 的侧边栏导航」视为批准。
> 对象澄清：**本项目没有 Storybook**，组件调试使用自建画廊 `ForgeSelf.Web/ct/`（输入12 建）；本任务配置的就是该画廊的侧栏，不新增 Storybook 依赖。

## Repository Understanding

- 画廊真源：`ForgeSelf.Web/ct/main.ts`（注册表 / 挂载契约 / 侧栏 / 深链）、`ct/stories.ts`（唯一注册点）、`ct/index.html`（`#gallery-nav` 样式）。
- 现状（改前）：分组 key = story id 的 `/` 前缀，标题即英文前缀，顺序 = `stories` 注册顺序；`>25` 默认折叠；深链 `?story=` 完全不生成侧栏。
- CT 链路：`playwright-ct.config.ts`（5190；`CT_FRESH=1` → 5193）、`src/components/ct-support.ts::openStory`（开页 + 摘 `#gallery-nav`）。
- 壳契约：`src/components/Gallery.ct.spec.ts` 3 条用例（侧栏点击 / 深链无侧栏 / 未知 id）。

## Intent

让侧边栏像 Storybook 一样**集中可配**（分组显示名与顺序），并让人类浏览深链时也能看到导航、知道当前在看哪一条；同时保证 CT 截图基线不受侧栏影响。

## Spec

- FR-1 `ct/stories.ts` 导出 `navConfig`（`title` / `groups[{key,title}]` / `collapseWhenMoreThan`），为侧栏唯一配置点；未配置的分组按 id 前缀追加在末尾。
- FR-2 分组标题显示 `title (count)`；点击条目切换故事、`history.replaceState` 同步 `?story=`，刷新后停留同一故事。
- FR-3 `?story=<id>` 人类浏览保留侧栏并高亮当前条目；`&chrome=0` 不生成侧栏、无 250px 左侧留白。
- FR-4 `openStory` 统一带 `chrome=0`；`Gallery.ct.spec.ts` 覆盖「配置标题生效 / 深链 active / chrome=0 无侧栏」。
- 非目标：不引入 Storybook；不改任何产品组件；不做搜索/收藏/二级嵌套。

### Acceptance Criteria

- AC1 `navConfig` 生效：四组标题与顺序为 `ForgeCard · 玻璃卡片 / PageHead · 页头 / EmptyState · 空状态 / AIAgent · 聊天助手`，均带计数。
- AC2 深链 `?story=` 保留侧栏且当前条目 active；点击其他条目切换并同步地址栏。
- AC3 `&chrome=0` 不渲染侧栏、`body` 无 `has-nav`、组件无左侧留白。
- AC4 既有 12 条 CT 用例不回归（含 4 张像素基线）。

## Plan

1. `ct/stories.ts`：新增 `NavGroupConfig` / `NavConfig` 接口与 `navConfig` 常量。
2. `ct/main.ts`：`buildNavGroups` / `groupTitle` / `setActiveStory` / `previewStory` / `expandGroupOf` / `renderUnknown`；入口按 `chrome` 决定是否渲染侧栏，深链高亮。
3. `src/components/ct-support.ts`：`openStory` 的 URL 追加 `chrome=0`。
4. `src/components/Gallery.ct.spec.ts`：改深链用例、新增 chrome=0 用例、补配置标题断言。
5. `ct/README.md`：更新深链语法与 `navConfig` 说明。

## Files To Change (planned)

- `ForgeSelf.Web/ct/stories.ts`
- `ForgeSelf.Web/ct/main.ts`
- `ForgeSelf.Web/src/components/ct-support.ts`
- `ForgeSelf.Web/src/components/Gallery.ct.spec.ts`
- `ForgeSelf.Web/ct/README.md`
- 本工件目录（mini-task / 05 / 06 / 07）+ 当天日记 / TODO

## Allowed / Forbidden

- Allowed：上述 5 个源/文档文件与工件目录。
- Forbidden：新增依赖；改产品组件；改 `playwright-ct.config.ts` 端口策略；git 提交（闸门3 待用户）。

## Verification Plan

```powershell
cd ForgeSelf.Web
pnpm run type-check
node scripts/check-component-conventions.mjs
npx eslint ct/main.ts ct/stories.ts src/components/ct-support.ts src/components/Gallery.ct.spec.ts
$env:CT_FRESH='1'; pnpm run test:ct   # 5190 被画廊 dev server 占用时换 5193
pnpm run design:check
```

## Rollback

回退上述 5 个文件即可；无数据、配置或依赖副作用。
