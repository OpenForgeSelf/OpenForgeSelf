# 03 Plan — 新组件 composer 升级

## 1. 总体思路

**一条分界线：组件只做「骨架 + 交互」，宿主做「数据 + 语义」。**

沿用 v1.8.0 迁移已定的 `showHeader` / `transport` / `sessionId` 范式 —— 新能力同样做成 config 注入、默认关闭。这样组件随时可以独立交付给别的宿主，AIAgent 的业务（工具列表、项目、脚本）一行都进不了 `agent-core`。

## 2. 三层改动

```
agent-core（组件资产，可独立交付）
├─ 新增 components/ComposeTrigger.vue   触发面板骨架（弹层 + 过滤 + 键盘 + 无障碍）
├─ 新增 components/ContextChips.vue     chips 行渲染（数据/回调全由 config 注入）
├─ ChatWidget.vue                       在 footer 内挂载上面两个；onKeydown 增加触发判定
├─ config.js                           新增 triggers / context 两个默认 null 的键
└─ styles/widget.css                    面板与 chips 样式（只用 --el-* 与既有 --aiw-*）

AIAgent 宿主（插件业务层）
├─ agentBridge.ts / AiAgentView.vue     组装命令分组（工具+内置+技能+脚本）、mention 解析器、chips 数据
└─ http.ts                              补 scripts / project 相关取数

后端（批次 2）
└─ ProjectController.cs                 新增 GET /api/project/list（暴露 IProjectRegistryService.GetAll()）
```

## 3. 关键设计决策

| # | 决策 | 理由 | 被否选项 |
| --- | --- | --- | --- |
| D1 | 触发判定要求「行首或前一个是空白」 | 否则 `src/api/foo`、`a@b.com` 会误弹 | 只要出现字符就弹 —— 输入路径/邮箱时疯狂打扰 |
| D2 | 工具/技能命令选中后**挂 chip，不写进文本** | 后端契约本就是 `selectedToolNames` 数组；写进文本会让模型把工具名当自然语言 | 插入 `/read_file` 文本 —— 与后端语义脱节 |
| D3 | 面板数据**打开时拉一次 + 内存过滤** | 112 个工具，每次击键都请求会打爆后端 | 每次输入请求 —— 无谓的网络与延迟 |
| D4 | 文件候选排除 `node_modules/.git/dist/bin/obj` 并限深（默认 4 层） | 大项目全量枚举会卡死 | 全量递归 —— 万级文件直接卡 UI |
| D5 | 项目选择复用既有 `browse-directories` + 新增 `list` | 后端 `IProjectRegistryService.GetAll()` 已实现，只差一个端点，不必新建"项目"实体 | 新建 Project 表 —— 与既有 registry 双份真相 |
| D6 | 不恢复 5 个常驻 popover，收敛成一个「上下文」入口 | 中栏 inline 形态不宽；5 个按钮 + 发送键会挤成一排；且会让组件越来越偏离源形态 | 原样照搬 —— 与行业形态和组件升级路径都冲突 |

## 4. 批次

| 批次 | 范围 | 后端改动 | 交付价值 |
| --- | --- | --- | --- |
| **批次 1** | FR-1 chips 行、FR-2 上下文入口（项目走「浏览目录」）、FR-3 `/` 命令面板、FR-5 `@` 文件提及、FR-6 组件契约 | **零** | 能力全部可用（除「已登记项目列表」） |
| **批次 2** | FR-4 已登记项目列表、文件搜索端点（视体验） | `GET /api/project/list`（+可选文件搜索） | 从「选目录」升级为「选项目」 |

## 5. 风险

| 风险 | 影响 | 缓解 |
| --- | --- | --- |
| 组件 footer 结构改动影响既有 e2e | 中 | 保持 `.aiw-input` / `.aiw-send` / `.aiw-footer` 类名与 textarea 位置不变，新增节点挂在其外 |
| 112 条工具列表首屏渲染卡顿 | 低 | 面板内虚拟滚动不做；先限制每组默认展示 50 条 + 搜索出全部 |
| `@` 文件树遍历慢（大项目） | 中 | 限深 4 层 + 排除目录 + 打开时预取一次；批次 2 视情况改后端搜索 |
| `--aiw-*` 变量增加触发 design 棘轮 | 低 | 新样式不新增 `--aiw-*` 定义（沿用已有桥接变量） |
