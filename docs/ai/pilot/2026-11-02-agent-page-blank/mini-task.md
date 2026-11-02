---
task_kind: defect
risk:
  level: L1
expected_files:
  - ForgeSelf.Web/src/stores/tabs.ts
  - ForgeSelf.Web/src/views/RouteMissingView.vue
  - ForgeSelf.Web/src/router/dynamicPlugins.ts
feature: 智能体页能打开且顶栏叫智能体
rollback: 还原上述三个文件以及设置页、聊天页里的名称字符串
---

# 智能体页空白与顶栏旧名

## Intent

本地开发栈打开 `/ai-agent` 时，顶栏写着改名前的「AI Agent」，主区域是空的。本任务让顶栏和设置里的名称变成「智能体」，并让带访问密钥时插件页真正挂上。

## Spec

- 宿主默认标签、助手气泡、设置分类、旧聊天标题使用「智能体」。
- 没有匹配路由时主区域显示说明，不再是空注释。
- 这条兜底路由不得把插件声明的 `/ai-agent` 判成冲突。
- 智能体插件的 `web/dist/index.js` 要存在，远程界面才加载得到。

## Plan

- 改 `tabs.ts`、`App.vue`、设置与聊天标题。
- 增加 `RouteMissingView`，并在 `isPathTaken` 里跳过兜底路由。
- 在 `Plugins/AIAgent/web` 执行 `pnpm run build`。
- 用本地开发栈打开页面核对。

## Task

Allowed：宿主前端上述文件、对应单测、PC-021 与 case、本目录工件、日记和 TODO。

Forbidden：停 :51888、改插件 C# 或插件 Vue 源码、发布、提交 git。

## Interaction Design（交互设计）

点顶栏「智能体」或直接打开 `/ai-agent`：

- 加载：清单成功且界面产物存在时，主区域出现会话栏、输入框和「你好，我是铸己匣智能体」。
- 空：无密钥或清单没有这条路由时，主区域是「这个页面没有加载出来」，不是空白。
- 出错：路由已注册但入口脚本失败时，仍走原有「插件界面加载失败」（本任务不改那段）。
- 无权限：清单 401 时不注册动态路由，落到上面的空态说明。
- 正常：顶栏当前项与页内标题都是「智能体」，版本徽标可见。

验收：浏览器里顶栏为「智能体」，主区域有输入框与问候语；设置分类里有「智能体」。

## Acceptance Criteria

- AC1：`tabs` 单测断言默认标签为「智能体」
- AC2：带兜底路由时，动态注册仍把插件放在 `/ai-agent`
- AC3：本地开发栈打开 `/ai-agent`，顶栏是「智能体」，主区域是插件页而不是空注释
