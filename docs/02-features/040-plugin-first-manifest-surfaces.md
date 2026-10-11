# 040 — Plugin-First Manifest Surfaces（宿主消费契约）

> 状态：2026-10-11 M5 落地。真源：`DESIGN.md`「Plugin-First 架构约定」。
> 范围：宿主侧 manifest 类型 + 校验 + 前端消费位；不改既有插件 UI。

## 1. 四类 surface

| surface | 消费位 | 典型声明 |
|---|---|---|
| `tab` | 顶栏 TabBar / 独立页面路由 | `route` + `views[0]` + `entry` |
| `home-widget` | 首页（`@forge/home`）面板 slot | 视图组件由 Home 面板按 slot 名挂载 |
| `quick-action` | 全局 composer / 命令面板入口 | 插件经工具/API 注册动作 |
| `status-badge` | StatusBar 状态徽标 | 插件经 API 上报状态点 |

## 2. manifest 字段（plugin.json → `frontend.surfaces`）

```jsonc
{
  "id": "home",
  "frontend": {
    "views": ["HomeView"],
    "menu": "首页",
    "route": "/home",
    "icon": "fa-house",
    "entry": "web/dist/index.js",
    "surfaces": ["tab", "home-widget"]
  }
}
```

- 类型真源：后端 `ForgeSelf.Abstractions/PluginSurfaces.cs`（常量 + `Normalize`/`NormalizeAll`）；前端 `ForgeSelf.Web/src/utils/pluginSurfaces.ts`。
- **向后兼容**：旧插件未声明 `surfaces`，但带 `route` + 非空 `views`，前端 `resolveSurfaces` 自动按 `['tab']` 处理——既有插件零回归。
- **未知值**：后端在 `PluginManager.LoadPluginManifest` 清洗（`NormalizeAll` 丢弃并 `XTrace.Log.Warn`），不阻断加载；前端 `normalizeSurfaces` 二次防御性过滤。

## 3. 宿主消费点

| 消费位 | 代码 | 说明 |
|---|---|---|
| Tab 路由注册 | `ForgeSelf.Web/src/router/dynamicPlugins.ts` | `hasSurface(frontend,'tab')` 才 `addRoute`；route meta 带 `surfaces` |
| 菜单/功能列表 | `stores/pluginManifest.ts` `menus` | 既有，不变 |
| 首页 slot | `stores/pluginManifest.ts` `homeWidgets` | 已暴露 getter，待 Home 面板按 slot 名消费（M3 未声明 widget，本步只铺管道） |
| 快捷动作 | `stores/pluginManifest.ts` `quickActions` | 已暴露 getter |
| 状态栏徽标 | `stores/pluginManifest.ts` `statusBadges` | 已暴露 getter |
| Tab 精确过滤 | `stores/pluginManifest.ts` `tabRoutes` | 与 `menus` 互补 |

## 4. 校验

- 后端：`ForgeSelf.Api.Tests/Plugins/PluginFrontendManifestTests.cs`
  - `DiscoverPlugins_Surfaces_AreDeserialized_AndNormalized`
  - `DiscoverPlugins_UnknownSurfaces_AreDropped_NotBlockLoading`
  - `GetFrontendManifest_Surfaces_FlowThrough`
  - `PluginSurfaces_Normalize_HandlesKnownAndUnknown`
- 前端：`src/utils/__tests__/pluginSurfaces.test.ts`、`stores/__tests__/pluginManifest.test.ts`、`router/__tests__/dynamicPlugins.test.ts`。

## 5. 不在本批范围

- 不改既有插件 `plugin.json` 的 surfaces 声明（Home 等仍走默认 `tab`；显式声明留给后续插件版本化发布时一并补）。
- 不新建首页 slot 渲染组件 / 命令面板 / StatusBar 徽标 UI（属 M4/M8 视图改造）。
- 不做发布与运行实例复验（本批为宿主类型/管道，未改插件产物）。
