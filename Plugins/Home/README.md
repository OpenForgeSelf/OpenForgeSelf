# 首页插件（Home Plugin）

> 铸己匣首页仪表盘。view-only 全量插件化：Hero / 常用功能 / 核心看板 / 待办与活动 / 系统监控。

## 定位

- 把原宿主 `ForgeSelf.Web/src/views/HomeView.vue` + `stores/home.ts` 整体迁移为独立插件，宿主只保留加载与路由桥。
- 无独立后端：`HomePlugin` 仅注册前端视图，不提供 API（见 `plugin.json` 的 `Provides: []`）。

## 插件化契约（spec 010）

前端产物约定（宿主按此加载）：

- 构建入口 `web/dist/index.js`，样式 `web/dist/style.css`。
- `web/src/index.ts` 默认导出 `views: ['HomeView']`，与 `plugin.json.frontend.views` 对齐。
- `web/vite.config.ts` 的 `rollupOptions.external` 必须含 `vue` / `vue-router` / `pinia` / `element-plus`，由宿主 import map 解析到**同一实例**，防止 Vue 双实例。
- `web/dist/` 由 `pnpm build` 生成，已被 `web/.gitignore` 忽略，**不入库**；源码在 `web/src/`，发布时宿主构建。

## 导航桥契约

插件界面打开宿主页面走四级降级链：

1. `inject('forgeOpenPage')` —— 组件级主路（宿主 `app.provide`）。
2. `window.__FORGE_OPEN_PAGE__` —— 跨实例硬兜底。
3. 自带 `router.push` —— 插件内兜底。
4. `location.assign` —— 终极兜底。

宿主桥体必须包 `app.runWithContext()`（根因：插件组件调用栈里 `inject(router / pinia)` 取不到宿主 provide，会导致 `router.push` 抛 `undefined`；用 `runWithContext` 切回宿主上下文）。详见 `ForgeSelf.Web/src/main.ts` 注释。

## 已知坑：Hero 文字被裁半截（2026-09-20）

**症状**：走查首页时 Hero 区问候语 / 副标题 / 提问条 / 推荐动作整段被吃，只露半截。

**根因**：`.home-content` 是「flex 列 + `overflow-y:auto`」滚动容器，子区块默认 `flex-shrink:1`；当内容总高 > 容器（视口较矮，如 Playwright 默认 1280×720）时区块被 flex 压扁，叠加 `.hero-section { overflow:hidden }` 直接裁掉内部文字。

**修法**：在 `HomeView.vue` 的 `<style scoped>` 末尾加

```css
/* 关键约束：home-content 是「flex 列 + overflow-y:auto」的滚动容器，
   其子区块必须保持自然高度，绝不能被 flex 压缩。
   用「直接子元素」选择器而非逐个类名，保证后续新增区块自动继承该不可压缩约束。 */
.home-content > * {
  flex-shrink: 0;
}
```

**验证**：4 视口（1280×720 / 1920×1080 / 1366×768 / 1536×864）Hero 稳定 220px；e2e 加回归守卫（Hero 底部须落在 Hero 区块内）；chromium 4/4 绿；运行宿主（51888）静态服务 `cache-control: no-cache` 直读磁盘新 `style.css`，字节级确认跑的是修复版。

## 发布部署

- 构建：`cd web && pnpm build` → 产物 `web/dist/`。
- 部署：拷贝到运行宿主 `Plugins/Home/web/dist/`。宿主静态服务直读磁盘、`no-cache`，**无需重启即时生效**。
- 版本：改 `plugin.json.Version`；`frontend` 块 `entry` 固定 `web/dist/index.js`。

## 验证状态（2026-09-20）

- e2e（chromium 矩阵）4/4 通过，含 Hero 回归守卫。
- 4 视口几何校验 Hero 不被裁切。
- 51888 运行实例已部署修复版并字节级确认。
