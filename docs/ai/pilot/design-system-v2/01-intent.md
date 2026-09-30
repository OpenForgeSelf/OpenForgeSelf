# Intent — DesignSystem 插件 v2

> 阶段：Stage 1｜只讲「为什么做 / 做什么 / 做到什么程度」，不预先决定实现细节。
> Task ID：PILOT-ds-v2｜日期：2026-09-28｜依据：`00-repository-understanding.md`、`research.md`

## Problem
`Plugins/DesignSystem` 现在是一个**前端演示件**：后端只有 20 行 no-op（`DesignSystemPlugin.cs:15-19`），零实体、零控制器、零服务端测试；"持久化"是浏览器 `localStorage` 且历史上限 10 条（`storage.ts:15-19`）。生成逻辑是关键词打分 + 一条写死的 10 阶 L/S 曲线（`generate.ts:90-218`、`colorScale.ts:29-49`），而 spacing/radius/shadow/motion/排版全是**逐字节相同的常量**（`generate.ts:23-61` ≡ `presets.ts:68-93`）——换任何 brief 都得到同一套非色令牌。`selfCheck` 里"对比度可达性 ≥4.5:1"是**从未被计算的散文**（`:358`）。5 个界面 section 里只有 DesignStudio 是真的，其余（ComponentGallery / console×5 / marketing）是硬编码 fixture 与无处理器按钮。导出 6 格式里没有一个是业界标准（`tokens.json` 不是 DTCG，`tailwind.tokens.js` 是字符串拼的 v3 时代 config）。

## Why
1. 用户指令：把该插件做成"**拥有完整设计功能的插件，而不是玩具**"，并明确指定对标 `D:\Downloads\Stardust`。
2. Stardust 已经证明可行的那一半，恰好是我们缺的那一半：它把设计系统落成了 **XCode EntityModel（10 表 88 列，oklch 三元组拆列、阴影一层一行、mode 作行维度）**，并且 `api/index.json:4` 白纸黑字写着"静态文件代替后端接口……换成真实 API 时只需替换 url" —— 那是给我们留的插座。
3. 行业 2025–2026 的格局（`research.md` §B8）显示"持久化、可校验、可版本化的**设计系统数据库 + 标准合规投影**"目前是**空白定位**：AI codegen（v0 / Stitch / Figma Make）都是一次性输出，没有分层、没有别名图、没有模式、没有库。我们继续做"一次性生成器"必然被碾压；做底座才有不可替代性。
4. 宿主自身需要一个设计语言系统级底座：全仓界面（宿主 + 18 插件）都该消费同一套语义令牌，而现在的"设计系统"页跟真实渲染链路毫无关系（`--ds-*` 只活在演示容器里）。

## Expected Outcome
DesignSystem v2.0.0 = **设计系统的系统级底座**，七件事成立：
1. **有库**：插件自建 `ConnName=DesignSystem` 库，11 张表承载项目 / 主题 / 三层令牌 / 阴影分层 / 组件变体矩阵 / 图标 / 页面 / 字体资产 / 审计结果 / 版本快照；关掉浏览器换台机器数据仍在。
2. **有真算法**：从一个种子色确定性生成感知均匀色阶（tone 目标 + hue-cycling 恒感知彩度），语义角色按**所需对比度反查 tone**（Leonardo 式定向选取，不是事后审计）；排版有模块化比例与 fluid `clamp()`；动效含 `prefers-reduced-motion` 派生；spacing/radius/shadow/motion 随项目参数真实变化。
3. **有分层与模式**：primitive → semantic → component 三层 + 别名图（带环检测与失效报错）；light/dark/高对比 主题 × 密度，令牌行按 `ThemeId` 维度存储。
4. **有校验**：WCAG 2.2 对比度（4.5 / 3:1 非文本）、focus-visible、reduced-motion、跨层违规（component 直连 primitive）、孤儿令牌 —— 全部作为 `DesignAudit` 行落库并可导出报告，**不达标即红**。
5. **有版本**：DS 自身的 release 快照 + 令牌级 diff（回归报告），生成参数（seed / algorithm / version）随行持久化 → 同一 brief 可复现。
6. **有标准投影**：DTCG `tokens.json`、CSS 自定义属性（保留 alias 与 `color-mix` 派生）、Tailwind v4 `@theme`、SCSS/LESS/TS 带类型、Tokens Studio/Figma Variables + `$themes`、DESIGN.md、shadcn 风格 `registry.json`、以及 **Stardust 兼容的 SQL 种子 + `api/<entity>.json`**；一键打包含多文件的发布包。
7. **有真界面**：10+ 个 section 全部**由库驱动**（组件库不再渲染写死的 SRE fixture），含令牌编辑器、色彩实验室、运行时主题微调面板（Stardust tweaks 的全令牌升级版）、审计台、导出台；清除一切硬编码色值，让 `--ds-*` 成为组件唯一来源。

## Constraints
- 产物契约不变：`web/dist/index.js` + `style.css`，导出名 == `plugin.json.views[0]`（`DesignSystemView`）；禁 import 宿主模块 / `@/` 别名 / 显式 `ElXxx`。
- 控制器必须类级 `[Authorize("ApiKeyPolicy")]`（铁律 17）；插件必须自行建表（铁律 12）；数据落 `ctx.EnsurePluginDataDirectory()`（铁律 §六）。
- **禁止任何删库/清目录动作**（铁律 10）；唯一性校验直查 DB（铁律 11）。
- 不引入新 NuGet/npm 依赖（oklch + WCAG 数学自实现并单测）；如确需，先升级给人。
- 不动宿主 `ForgeSelf.Web/src` 的既有渲染链路（本任务的界面范围是 `Plugins/DesignSystem/web/`）；与宿主 `--el-*` 体系的对接只以"可导出/可套用"为界，不改宿主主题文件。
- 禁 agent 停/启/杀用户宿主进程；发布走打 tag 或本地目录更新源（AGENTS.md §0 / §2.3）。
- 四步门禁（构建+测试 / 插件层 e2e / 发布 / 走查）与 §11 工件链不得跳。
- 数据库结构变更属高风险：须经闸门1 用户确认后才动 `Model.xml`。

## Success Criteria
见 `02-spec.md` 的 Acceptance Criteria 全表（逐条可测）。硬判据摘要：
- `dotnet test` 中存在并通过：色彩数学（黄金值）、别名环检测、对比度门禁、DTCG/各投影 golden-file、控制器鉴权反射断言。
- `cd Plugins/DesignSystem/web && pnpm run build && pnpm run check && pnpm run test` 全绿（`check/test` 脚本当前**不存在**，属本次交付物）。
- 插件层 e2e 零 mock 跑通「建项目 → 生成 → 改令牌 → 审计 → 导出下载 → 换会话刷新仍在」全链路，并断言 `plugin.json` 版本。
- 全仓 `Plugins/DesignSystem/web/src` 内不再有本应走令牌的硬编码色值（`grep` 判据见 spec AC-19）。
- 生成的 `tokens.json` 通过 DTCG 结构校验；对比度审计 0 项 Critical 失败。
