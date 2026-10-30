# Repository Understanding

> 阶段：Stage 0｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> Task ID：2026-10-30-sems-b1-ui

## 项目结构

仓库根有 `ForgeSelf.slnx`、宿主 `ForgeSelf.Api/`、前端 `ForgeSelf.Web/`、测试 `ForgeSelf.Api.Tests/`、插件 `Plugins/Sems/`。Sems 后端入口 `Plugins/Sems/SemsPlugin.cs`，规格 HTTP 在 `Plugins/Sems/Controllers/SpecsController.cs`，落盘在 `Plugins/Sems/Services/SpecFileStore.cs`。插件界面已有 `Plugins/Sems/web/`，入口 `src/index.ts` 导出 `SemsView`，与 `plugin.json` 的 `frontend.views[0]` 一致。

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 插件后端 | .NET，控制器返回 `{ success, spec\|specs\|links\|coverage\|case }` | `SpecsController.cs` |
| 插件前端 | Vue 3 + Vite 6 库模式，`vue` / `element-plus` 为 external | `Plugins/Sems/web/vite.config.ts`、`package.json` |
| 插件单测 | vitest，现有 `src/confirmOps.spec.ts` | `package.json` scripts.test |
| 插件 e2e | Playwright，`ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts` | 该文件头注释 |

## 架构特点

规格不是新表。`SpecFileStore` 按项目目录追加 `v{N}.json`、`links.jsonl`、`cases.jsonl`。列表接口只返回每个规格的最新版。旧版只能在已知版本号时 `GET ?version=`。界面走插件自己的 `src/http.ts`：同源 `fetch`，令牌键 `forge_api_token`。成功体没有 `data` 时客户端保留整包。

## 测试方式

插件前端：`cd Plugins/Sems/web && pnpm test` 与 `pnpm build`。后端规格行为已有 `ForgeSelf.Api.Tests` 中 Sems 过滤集，本任务不改后端时不重写那些用例。插件冒烟 e2e 在既有 `e2e/plugins/sems/sems.spec.ts` 增加一条，不新开第二套 runner。

## 构建命令

```bash
cd Plugins/Sems/web
pnpm test
pnpm build
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/Sems/Controllers` | 规格与项目 HTTP |
| `Plugins/Sems/web/src` | 插件页，宿主远程加载 `web/dist/index.js` |
| `ForgeSelf.Web/e2e/plugins/sems` | sems 插件层 e2e |

## 代码组织方式

页面壳是 `SemsView.vue`。项目卡片 `ProjectCard.vue` 展开后挂 `CommandList.vue`。新规格区同样挂在卡片上，不新增 `plugin.json` 菜单或 view。

## 现有工程规范

插件界面用原生 HTML + CSS 与 `--el-*`，脚本里可以用 `ElMessage`。破坏性动作才要二次确认；追加规格不是删除。设计上沿用本页已有页头，不从宿主 import 组件。不手起 `dotnet` / `vite`，本地走查只用 `scripts/dev-stack.ps1`。本任务不发布、不碰 `:51888`。
