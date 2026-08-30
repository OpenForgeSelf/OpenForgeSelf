# 插件前端运行时架构总览（spec 010，2026-08-30）

> 功能编号：010
> 状态：已实现（与代码对齐，2026-08-30）
> 最后更新：2026-08-30

本文档从代码反推「插件独立 Web 界面远程加载」的整体架构：分层、模块关系、核心数据流。
开发 how-to 见 [`../05-guides/plugin-frontend-development.md`](../05-guides/plugin-frontend-development.md)，
发布验证见 `.agents/skills/plugin-publish-verify/`，规格/设计见 `specs/010-plugin-frontend-runtime/`。

---

## 1. 目标

让后端插件（`Plugins/<Dir>/`）拥有独立 Web 界面，由宿主前端**远程加载**、**共享宿主 Vue/Element Plus 单实例**，
宿主不再内置插件页静态路由（`AgentView.vue` 已迁出，改为清单驱动远程加载）。

---

## 2. 分层与组件关系

```
插件 (Plugins/<Dir>/web/)
  ├─ vite build（lib 模式：vue/element-plus/pinia/vue-router 设为 external）
  └─ 产物 dist/index.js + dist/style.css
        │  远程加载（浏览器 fetch + import()）
        ▼
宿主前端 (ForgeSelf.Web)
  ├─ main.ts 顶部：exposeSharedDeps() → window.__FORGE_SHARED__ = { vue, element-plus, pinia, vue-router }
  ├─ index.html：<script type="importmap"> 把裸模块名映射到 /shared/*.js
  ├─ public/shared/{vue,vue-router,pinia,element-plus}.js：从全局具名再导出（generate-shared-shims.mjs 生成）
  ├─ utils/pluginViewLoader.ts：按清单 entry 远程 import + 同目录注入 style.css（幂等去重）
  └─ router/dynamicPlugins.ts：清单驱动注册路由（route 直接路径，冲突回退 /plugin-view/<id>）
        │  资源请求 GET /plugins/<id>/frontend/**
        ▼
宿主后端 (ForgeSelf.Api)
  ├─ Plugins/Services/PluginFrontendFileMiddleware：只读 + 拒 .. 穿越 + .js→text/javascript + ?v= 发 immutable
  └─ PluginController.GetFrontendManifest → /api/plugin/frontend-manifest（含 Version）
```

---

## 3. 核心数据流

- **启动**：宿主 `main.ts` 暴露共享依赖 → 前端拉取 `/api/plugin/frontend-manifest` →
  `registerManifestRoutes` 按清单 `entry` 远程 `import()` `index.js` → 浏览器经 import map 解析到宿主同一 Vue 实例（单实例，响应式有效）。
- **版本**：清单 `PluginFrontendManifestDto.Version` 由前端拼 `?v={version}` 作缓存标识；中间件对静态资源发 `immutable`。
- **热更新**：插件 `web/dist` 被覆盖 → 宿主加载器下次按新 version 拉取（或 watcher 触发重载），**无需重启宿主**。
  （C# 入口 DLL 改动不能热更，见 [`../05-guides/plugin-hot-reload-limitations.md`](../05-guides/plugin-hot-reload-limitations.md)）

---

## 4. 不变量（铁律）

- 插件 bundle **不得打包自己的 Vue**（双实例陷阱：响应式 / 组件通信全部失效）。
- 插件模板**禁用 `<ElXxx>`**（宿主 `unplugin-vue-components` 不处理插件预编译产物，运行期必解析失败）→ 一律原生 HTML + CSS，用 `--el-*` 变量保视觉一致。
- 共享依赖 shim 由 `scripts/generate-shared-shims.mjs` **脚本生成**，禁止手改（漏导出只有运行期才报 "does not provide an export named X"）。
- 活动插件目录只放插件自身程序集（`<Dir>.dll` + `plugin.json` [+ `web/dist`]），禁止混入宿主共享 DLL。

---

## 5. 相关

- 开发 SOP：[`../05-guides/plugin-frontend-development.md`](../05-guides/plugin-frontend-development.md)
- 热更新局限 SOP：[`../05-guides/plugin-hot-reload-limitations.md`](../05-guides/plugin-hot-reload-limitations.md)
- 发布验证技能：`.agents/skills/plugin-publish-verify/`
- 脚手架技能：`.agents/skills/plugin-frontend-scaffold/`
- 规格 / 设计 / 契约：`specs/010-plugin-frontend-runtime/`
