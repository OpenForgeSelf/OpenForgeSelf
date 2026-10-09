---
feature_key: F028b
feature_no: 028
status: draft
last_updated: 2026-10-06
aliases: ["028-wxt-dual-mode"]
---

# 028 · 前端双形态：浏览器插件（WXT）+ 独立 Web

> 状态：方案已定，待实施（Phase 0 起）
> 来源：用户输入「研究 WXT，把前端做成浏览器插件，也可 web 单独打开」
> 关联：`.forgeself/memory/2026-08-18.md`、TODO.md「调研 WXT 并把前端做成双形态」

---

## 1. 背景与目标

OpenForgeSelf 前端是 Vue 3.5 + Vite 6 + TS 5.7 + Element Plus + Pinia + Tailwind 4 的纯 SPA（ForgeSelf.Web/），当前仅以 Web 形态运行（dev :7002 / 构建产物进后端 wwwroot）。

目标：同一份源码，既能**打包成浏览器扩展**（Chrome/Firefox/Edge），也能**单独以 Web 应用打开部署**。前端本质是纯 SPA 调后端 API，扩展仅作承载外壳。

## 2. 已确认决策（2026-08-18 与用户对齐）

| 维度 | 决策 | 理由 |
|---|---|---|
| 框架 | **WXT**（`@wxt-dev/module-vue`） | 基于 Vite，文件式入口点自动生成 manifest，MV3 跨浏览器，HMR/TS/自动导入；不改变写 Vue 的方式。 |
| 入口形态 | **多入口并存（2026-08-18 补充 newtab + sidepanel）**：`newtab`（浏览器新标签页→工具主页，完整 SPA 宿主）+ `sidepanel`（侧边栏→agent 聊天）+ `popup`（快捷操作）+ `options`（设置）+ `app`（unlisted 完整页，newtab 覆盖关闭时的兜底入口，可选） | newtab 覆盖浏览器新标签、默认打开工具主页；sidepanel 打开 agent 聊天；popup 提供快捷入口；options 承载设置；app 作为 newtab 覆盖关闭时的完整页兜底入口。 |
| 独立 Web 策略 | **方案 A · 同源双构建** | 根 `index.html` 走普通 `vite build` 出纯 Web SPA；`entrypoints/*` 走 WXT 出扩展；共用 `src/`。改动最小、双形态最干净。 |
| 扩展开销 | **纯外壳**：无 background / content script | 前端是纯 SPA 调后端 API，不需要后台脚本或网页注入，权限与体积最小。 |

## 3. 目标架构

```
ForgeSelf.Web/
├── index.html              # 方案A：纯 Web SPA 入口（普通 vite build 用，WXT 忽略）
├── vite.config.ts          # 现有配置（需与 WXT 融合，见 §6 风险）
├── wxt.config.ts           # WXT 配置（manifest / 权限 / module-vue）
├── entrypoints/            # 仅 WXT 读取，普通 vite 忽略
│   ├── newtab/index.html   # 浏览器新标签页覆盖 → 完整 SPA 主页（hash 路由覆盖全部视图）
│   ├── sidepanel/index.html # 侧边栏 → agent 聊天视图（独立实例）
│   ├── popup/index.html    # 快捷操作轻量壳
│   ├── options/index.html  # 设置页（复用 app 设置视图）
│   └── app/index.html      # 完整 SPA（unlisted，newtab 覆盖关闭时的兜底入口，可选）
├── src/                    # 共享核心
│   ├── main-web.ts         # 纯 Web 挂载（根 index.html 引用）
│   ├── main-ext.ts         # 扩展挂载（entrypoints 共用，按入口传初始视图）
│   ├── lib/env.ts          # isExtension 判断 + 环境常量
│   ├── lib/storage.ts      # chrome.storage.local ↔ localStorage 双端封装
│   └── router/index.ts     # 改用 hash 模式
```

核心约束：**五个入口是独立 app 实例**（各自独立 Pinia）。跨入口的状态同步**不能靠内存**，必须统一走 `src/lib/storage.ts`。尤其 agent 聊天会话需在 `newtab` 与 `sidepanel` 间共享（同一会话在两侧打开应一致），必须持久化到 storage。

## 4. 分阶段实施

### Phase 0 · 跑通双构建（验证可行性，不动业务）
- 依赖：`wxt`、`@wxt-dev/module-vue`（devDeps）。
- 新增 `wxt.config.ts`：
  ```ts
  import { defineConfig } from 'wxt';
  export default defineConfig({
    modules: ['@wxt-dev/module-vue'],
    browser: 'chrome',
    manifest: {
      name: '铸己匣 OpenForgeSelf',
      permissions: ['storage'],
      host_permissions: ['http://localhost:7102/*', 'http://localhost:7300/*'],
    },
  });
  ```
  > host_permissions 后续按真实后端域调整（dev/prod 区分）。
- 加 `entrypoints/newtab/index.html` + 薄 `main.ts`，挂载现有 root app（先只 newtab，不动 sidepanel/popup/options/app）。WXT 会据此自动写入 `chrome_url_overrides.newtab`。
- 保留根 `index.html`，原 `vite build` 产出纯 Web 不变。
- `package.json` 脚本：`dev`/`build`/`zip` 指向 wxt；新增 `dev:web`/`build:web` 指向原 vite；`check`/`test` 不变。
- **验证**：`wxt build` 出 `.output/` + `vite build` 出纯 Web，两套均成功。

### Phase 1 · 多入口
- 拆 `entrypoints/sidepanel/`（agent 聊天视图）、`entrypoints/popup/`（快捷操作轻量壳）、`entrypoints/options/`（设置视图），复用 `src/` 组件与 stores；`entrypoints/app/`（unlisted 完整页，可选）作为 newtab 覆盖关闭时的兜底入口。
- sidepanel 默认开启：在 `entrypoints/sidepanel/index.html` 加 `<meta name="manifest.open_at_install" content="true" />`（Chrome 安装即开侧栏；Firefox 走 `sidebar_action` 语义略有差异）。
- 抽出 `main-ext.ts`：接收「入口类型」参数，决定挂载完整 app 还是某视图（newtab 挂完整 SPA 主页、sidepanel 挂 agent 聊天、popup 挂轻量壳、options 挂设置视图、app 挂完整 SPA）。
- 落地 `src/lib/storage.ts` 双端封装，替换现有 `localStorage` 直调（跨入口/跨会话状态统一走它；agent 聊天会话在 newtab↔sidepanel 共享）。

### Phase 2 · 路由 + 环境抽象
- `src/router/index.ts` 改 `createWebHashHistory()`（扩展内静态 HTML 必须 hash；纯 Web 用 hash 兼容）。
- `src/lib/env.ts`：`isExtension = !!chrome?.runtime?.id`；导出 API base（扩展走绝对 URL + host_permissions，Web 走相对/代理）。
- API 层（`src/services/*Api.ts`）base 按 env 切换，确保扩展内 fetch 后端可用（配合 host_permissions + 后端 CORS）。

### Phase 3 · 兼容与门禁
- 验证 Element Plus 自动导入（`unplugin-vue-components` + `@element-plus/resolver`）在 WXT Vite 下仍自动生成 `components.d.ts`；Tailwind v4（`@tailwindcss/vite`）接入 WXT 管线。
- 融合现有 `vite.config.ts`（含 safe-delete shim 等）与 WXT 的 vite 配置，避免冲突。
- **门禁**：`wxt build` + `vite build` + `pnpm run check` + `pnpm run test` 全绿；`wxt dev` 手动核对 newtab/sidepanel/popup/options/app 五入口。

## 5. 关键风险

| 风险 | 影响 | 缓解 |
|---|---|---|
| 跨入口状态不共享 | newtab/sidepanel/popup/options/app 五实例 Pinia 独立，内存态互不可见 | 统一 `storage.ts`，所有持久/跨入口状态走它；agent 聊天会话在 newtab↔sidepanel 必须按会话 key 持久化共享 |
| hash 路由 | 纯 Web URL 带 `#`，体验略降 | 可接受；如介意可在 Web 构建用 history（按 env 切换 history/hash） |
| vite.config 冲突 | WXT 用自有 Vite 管线，与现有配置（safe-delete shim 等）可能并存冲突 | Phase 3 融合，必要时把 WXT 排除原 shim 逻辑 |
| 构建脚本改写 | 原 `dev`/`build` 含义变化 | 明确 `dev:web`/`build:web` 保留纯 Web 路径，`dev`/`build`/`zip` 归 WXT |
| 包管理器 | 项目前端 pnpm 环境受限（memory：pnpm.mjs 缺失，改用 managed node 调 `.bin`） | wxt 经 `node_modules/.bin/wxt` 用 managed node 调用，不依赖 pnpm 全局 |

## 6. 验证门禁（完成定义）

- [ ] `wxt build` 产出扩展（含 manifest.json / newtab.html / sidepanel.html / popup.html / options.html / app.html）于 `.output/`
- [ ] `wxt zip` 产出可上架 zip
- [ ] `vite build`（或 `build:web`）产出纯 Web SPA
- [ ] `pnpm run check`（vue-tsc + eslint）0 error
- [ ] `pnpm run test`（vitest）全绿
- [ ] `wxt dev` 手动核对 newtab / sidepanel / popup / options / app 五入口可正常打开与交互
- [ ] 浏览器开新标签默认进入工具主页（newtab 覆盖生效）
- [ ] 侧栏默认打开 agent 聊天页（sidepanel open_at_install 生效）
- [ ] 跨入口状态同步（设置项、agent 聊天会话）经 `storage.ts` 生效（newtab↔sidepanel 同一会话一致）

## 7. 待确认 / 遗留

- 真实后端域名与 CORS：prod 环境 `host_permissions` 与后端 CORS 需对齐（dev 先用 localhost）。
- `wxt prepare` 生成的 `wxt/browser` 类型定义纳入 CI/类型检查。
- 是否需要把扩展构建产物也自动化进发布流程（publish/ 目录当前放后端产物）。
- 纯 Web 是否接受 hash 路由；若否，Web 构建切 history、扩展构建切 hash 的双模式路由需在 `router/index.ts` 按 env 分支。
- **newtab 覆盖是否提供开关**：强制覆盖浏览器新标签可能侵扰用户，是否加设置项允许用户关闭覆盖（关闭后回退默认新标签，完整 SPA 仍可由 popup/app 入口打开）。
- **`app`(unlisted) 是否保留**：newtab 已承载完整 SPA，app 仅作 newtab 覆盖关闭时的兜底入口；若确认始终覆盖 newtab，可去掉 app 入口。
- **sidepanel 跨浏览器差异**：Chrome 用 `side_panel`（open_at_install 支持），Firefox 用 `sidebar_action`（默认开启语义不同，需 `manifest.sidebar_action.default_panel`）；Safari 对侧栏支持有限，需评估降级方案。
