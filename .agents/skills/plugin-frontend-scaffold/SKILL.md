---
name: plugin-frontend-scaffold
description: 从一个模板插件快速搭建新的「带独立 Web 界面」的后端插件前端（spec 010 插件界面远程加载）。用于新建插件 UI、从 AIAgent 模板起步、刷新共享依赖 shim、构建插件产物并跑加载器单测。当用户说「新建插件界面」「搭插件 UI」「从模板生成插件前端」时使用。
---

# 插件前端脚手架（spec 010）

## 何时用

- 要给某个后端插件加独立 Web 界面
- 想从现有 AIAgent 插件 `web/` 模板起步，避免重复搭 Vite lib + import map external 配置

## 一键跑

```powershell
pwsh .agents/skills/plugin-frontend-scaffold/scripts/scaffold-plugin-frontend.ps1 -Plugin <PascalCase目录> [-Template AIAgent] [-Force]
```

动作：

1. 复制 `Plugins/<Template>/web/` → `Plugins/<Plugin>/web/`（排除 node_modules、dist）
2. 跑宿主 `scripts/generate-shared-shims.mjs` 刷新共享依赖 shim（宿主升级 vue/element-plus 后需要）
3. `cd Plugins/<Plugin>/web && pnpm i && pnpm run build` 产出 dist
4. 跑宿主 `pluginViewLoader` 单测（`ForgeSelf.Web` 下 `pnpm run test` 对应用例）

## 之后

- 按 `docs/05-guides/plugin-frontend-development.md` 改组件、注册 `plugin.json` 的 `frontend`
- 发布验证走 `.agents/skills/plugin-publish-verify/`

## 约束

- 新插件 `web/` 是 AIAgent 复制件，需自行改组件名 / 路由 / 样式
- 入口 DLL 改动不能热更，见 `docs/05-guides/plugin-hot-reload-limitations.md`
