# M4 功能屏迁移 · 批次执行规格（冻结模式 · M4a/M4b/M4c 宿主屏共用）

> 来源：总方案 `docs/ai/pilot/2026-10-11-design-overhaul/03-plan.md` M4 节（已批准，闸门1 已过）。
> 设计稿唯一真源：`D:\Users\12504\Downloads\design_handoff_forgeself_home_plugin_system\design_files\`（功能屏看 `OtherScreens.jsx` + `kit-styles.css` + `colors_and_type.css`；说明 `../README.md`；走查对照图 `../preview-01-home.png` / `../preview-02-agent.png`）。
> 项目规范：仓库根 `DESIGN.md`（v2-target：token/组件规格/动效/Do's and Don'ts/新建页面与查询执行规范/门禁化说明）。
> 本规格由编排层冻结（2026-10-11，依据 M2/M3 产物与 SystemMonitorView 样板），**批次必须严格按此执行，不得各自发明风格**。

## 0. 流程合规（AGENTS.md §0 预飞铁律，缺一不可）

1. **动手前**先向 `.forgeself/memory/2026-10-11.md` 追加一行（本批拆解清单）；`TODO.md` 引用/新建一条 🔄 待办（P1，来源:输入1）；完成时勾除并更新日记（含验证结果）。
2. 本批属于已批准方案执行（pilot `2026-10-11-design-overhaul`），**不新建 pilot 目录、不重复闸门1**；批证据写入独立文件 `docs/ai/pilot/2026-10-11-design-overhaul/m4-batch-evidence-<批次>.md`（每批一份，避免并发写冲突；编排层最后合并进 05-evidence.md）。每批必须记录**决策四要素**（决策→背景→理由→准则）。
3. **禁 git commit/push/reset/checkout 等一切改 git 状态的操作**；禁停/启/杀用户运行中的宿主进程（含 `D:\src\tools\ForgeSelf`、`:51888` 实例、他人起的 dev-stack）；不删除/改名任何他人文件。
4. 验证**判据看日志正文**（命令重定向 `*> <log>` 后 Read 日志），**不许拿 exit code 当证据**；PowerShell 工具可能不回显 stdout，没回显 ≠ 没跑（先重定向再读，别重复执行）。

## 1. 冻结模式（每屏必须满足，参考样板 `ForgeSelf.Web/src/views/SystemMonitorView.vue`）

- **页头**：模板内页头区块替换为 `<PageHead eyebrow="<英文 · 中文>" title="<display 26px 标题>">`；原右侧操作（按钮/徽标/状态/筛选）放入 `<template #actions>`。PageHead 规格：eyebrow mono 10px 大写 0.18em 金 `--gold-400`；title display 26px/700/0.05em `--fg-primary`。
- **卡片**：内容区块容器用 `<ForgeCard :interactive="...">`（glass 玻璃卡；`padding` prop 14~20；可点击卡 interactive=true）；数据表/表单/图表内部结构保留（**功能保留 > 严格视觉拷贝**，冲突时以 hover 微交互嵌入）。
- **空态**：列表/面板无数据时用 `<EmptyState icon title sub>`（icon 52px 金底 `--gold-tint`/`--gold-400` + title serif + sub mono）+ 可选 `#action` 引导/重试；**不留白屏**（含加载中、错误态：错误需可见留痕 + 重试）。
- **样式**：scoped 样式全部 token 引用——设计稿 token 族 `--ink-*`/`--gold-*`/`--fg-*`/`--border-*`/`--paper-*`/`--bronze-*`/`--cinnabar-*`/`--font-*`/`--dur-*`/`--ease-*`（定义在 `ForgeSelf.Web/src/styles/themes/workshop-forge.css`）+ Element Plus `--el-*` 变量。**禁止**：
  - 裸 hex/rgb（含内联 `style`、`v-bind` 样式对象、SVG stroke/fill）；
  - `var(--x,#hex)` fallback（守卫拦截源；存量用正则批量移除）；
  - 新增自定义 token 族（`--surface-*`/`--forge-*`/`--app-*` 等，compliance 规则 A 棘轮）；
  - `import { ElXxx } from 'element-plus'`（type 导入除外，模板 `<ElXxx>` 自动解析）。
- **交互反馈**（DESIGN.md Motion）：hover = `translateY(-1px)` / opacity .85（**禁放大缩小**）；active = `translateY(1px)` + 阴影收紧；焦点金描边；时长取 `--dur-fast/base/slow`（160/220/360ms），缓动 `--ease-out`；锤落回弹 `--ease-forge` 仅 logo/重点出现时刻。
- **品牌语汇**：主操作每屏仅一个（金底/金描边 chip）；破坏性用 `--cinnabar-400`；状态点 on 金 / off 灰 / 青铜绿；**无 emoji 装饰、无胶囊按钮**（极小 tag/chip 例外）、**无蓝紫渐变、无可爱插画**。
- **响应式**：1440 桌面为基准；固定高度卡片 `flex-shrink-0`；日志/列表限高滚动（max-height + overflow）。
- **不动的文件**：`ForgeSelf.Web/src/components/PageHead.vue`、`ForgeCard.vue`、`EmptyState.vue`、`TopNavbar.vue`、`StatusBar.vue`、`BackgroundPanel.vue`、`src/styles/themes/workshop-forge.css`、`src/App.vue`、`src/stores/appearance.ts`（已冻结，M6 专属）；`ForgeSelf.Api/**`、`ForgeSelf.Web/src/router/**`、`src/stores/pluginManifest.ts`（M5 专属）；不新建路由、不改插件（M4c 插件批次专属）。

## 2. 每屏步骤

1. Read 目标视图当前内容 + 设计稿对应原型（OtherScreens.jsx 对应块 / kit-styles.css 对应节 / DESIGN.md「Components-功能屏」对应行）。
2. 按 §1 改造模板与 scoped 样式；**保留全部既有功能与交互**（数据取数、事件、props、路由跳转、Element Plus 组件用法不改）。
3. 全文件扫裸色值与 `var(--x,#hex)` fallback 并 token 化。
4. 该视图如有 vitest 单测，跑之并同步断言（类名变更需同步）；如无则视情况补 1-2 例关键断言（可选，不硬性）。

## 3. 验证（快档，顺序执行；命令全部从仓库根 `D:\src\my-proj\OpenForgeSelf\OpenForgeSelf` 用 PowerShell 工具跑）

环境前置（AGENTS.md §5.0，每次测试前设）：
```
$env:NO_PROXY='localhost,127.0.0.1,::1'; $env:no_proxy='localhost,127.0.0.1,::1'; $env:TEMP=$env:TMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\tmp'
```
1. **守卫**：`cd ForgeSelf.Web; npm run design:check` → 双守卫 PASS：style-tokens 合计 **≤ 基线 2484**（禁上涨）；compliance **a≤244 b=0 c=0 d≤1**（与基线一致）。日志重定向 `*> .temp\<批次>-design.log` 后 Read。
2. **类型+lint**：`pnpm run check` → 0 errors（76 条既有 warnings 不新增）。日志 `.temp\<批次>-check.log`。
3. **单测**：`pnpm run test` → 67 files / 771 tests 全绿（含新增）。日志 `.temp\<批次>-test.log`。若并发 `vue-tsc -b` 因 tsbuildinfo 竞争出现与改动无关的异常报错，重跑一次确认（vue-tsc -b 会重建增量缓存）。
4. **dev-stack 起源码验收（③）**：**dev-stack 已在运行**（`.temp/dev-stack.json`：后端 `http://localhost:7301` pid 97516、前端 `http://localhost:7399` pid 79692，2026-10-11 10:03 起，vite 对视图改动 HMR 实时生效）。**直接复用，不重复起、不停止**（他人实例）。对每屏路由发 HTTP 请求确认 200（前端 `http://localhost:7399/<路由>`，后端 `http://localhost:7301/<api>` 探一条需鉴权的端点验 token 有效，401 立即上报）；按 DESIGN.md「新建页面与查询执行规范」验收三步逐屏记录验收结论（功能保留 + 三组件落地 + 状态三件套）。
5. **走查证据（隔离实例全页截图）**：运行覆盖本批视图的既有宿主 e2e spec（命令：`cd ForgeSelf.Web; node node_modules/@playwright/test/cli.js test --config=playwright.config.ts e2e/<spec>.spec.ts --output=<空目录>`，spec 见批次清单）；spec 未截图则补 `page.screenshot()`（落 `ForgeSelf.Web/screenshots/e2e/<spec名>/`）；**改造导致的选择器/断言失效同步更新（回归维护，非新增用例）**；完成后 **Read 截图读图**：页头三件套渲染、无横向溢出、无白屏、与设计稿布局一致、0 守卫命中（无裸色值残留/无违规样式）。桌面 GUI 截图被 Windows 锁屏（CU_SESSION_LOCKED）时不要反复尝试，以隔离实例截图为准。
6. **冒烟原则**：本批**不新增**插件级 e2e、不跑全量 e2e（每插件 1 冒烟 / 宿主回归 spec 只维护不扩增）。

## 4. 证据与汇报

- 批证据文件 `docs/ai/pilot/2026-10-11-design-overhaul/m4-batch-evidence-<批次>.md`：执行清单表（# / 里程碑 / 动作 / 文件 / 证据等级）+ 验证结果（真实命令输出，Verified/Inferred/Unknown 分级）+ 决策四要素 + 未做/风险如实声明。
- 回复编排层：每屏完成清单（视图绝对路径）、验证结果摘要（守卫三连 + dev-stack 验收 + e2e 截图路径与读图结论）、风险与未做项。**六段式**（目标/改动/不改/验证/代价风险/待拍板），≤半屏，中文，无 emoji。
