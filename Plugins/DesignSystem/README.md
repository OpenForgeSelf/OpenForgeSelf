# 设计插件 DesignSystem · 通用设计系统生成器

> 铸己匣（ForgeSelf）的**设计系统生成器**插件。
> 插件 ID `design-system`，挂载路由 `/design-system`，界面由插件自带（`web/dist`），宿主运行时远程加载。
>
> 本质：**输入一段需求描述，输出一套像 Stardust 一样完整的设计系统**（品牌 / 颜色 / 类型 / 间距 / 组件 / 应用示例 / 自检），并提供文件清单、实时预览与导出。
> Stardust 只是内置的「参考示例」预设，不是插件的身份。

---

## 一、三角色定位（解释「产出机器」）

本插件同时扮演三个角色，缺一个就不是完整产品：

| 角色 | 含义 | 在本插件中的体现 |
|------|------|------------------|
| **设计系统（System）** | 定义一套可被工程消费的视觉语言 | 通用 token 模型（颜色 / 类型 / 间距 / 组件 / 品牌）+ 展示页（令牌 / 组件库 / 控制台 / 官网） |
| **设计工作台（Studio）** | 接收需求，以专业设计师视角产出完整设计 | 需求输入 → 行业画像识别 → 生成 DesignSystem → 7 个结果分区浏览 |
| **产出机器（Artifacts）** | 把设计落成下游可直接带走、可直接用的文件 | 6 类产出文件：Markdown 规格、自包含 HTML 预览、结构化 JSON、tokens(JSON/CSS/Tailwind)，全部可下载/复制/新窗口预览 |

**「产出机器」具体指什么？** 不只是「看看就好」，而是：
- **清单**：明确告诉用户这次生成了哪些文件、各是什么、给谁用。
- **预览**：不用下载也能立刻看到效果（工作台内「实时预览」+ 独立 `preview.html`）。
- **导出**：逐文件或全部下载，拿到手里就能继续用。

---

## 二、本质需求目标

用户（产品/设计/前端）想解决的核心问题：

> "我要做一个 XX 产品，需要一套统一的设计系统来规范它的样子。"

本插件的目标响应：

1. **零门槛启动**：输入一句话需求即可得到完整设计系统。
2. **通用而不空洞**：不是给一套固定色卡，而是据行业/颜色词推导品牌与组件集合。
3. **对标 Stardust 完备度**：输出的设计系统覆盖品牌、颜色、类型、间距、组件、应用示例、自检七大维度。
4. **可消费**：产出文件覆盖「人读 / 机读 / 工程可用」三层。
5. **可预览**：工作台内实时换肤预览 + 可离线打开的 preview.html。

### 非目标（明确不做）

- **不做像素级视觉稿**：输出的是设计规格与线框，不是 Figma 替代品。
- **不做设计资产管理（后端）**：当前产物落在浏览器本地，不跨设备共享。
- **不接 LLM**：生成引擎是确定性的，保证可复现、可回归、可离线。

---

## 三、快速上手（为后来者准备）

### 3.1 30 秒看懂结构

```
Plugins/DesignSystem/
├── plugin.json                 # 插件清单（Version / frontend.route / entry）
├── DesignSystem.csproj         # 后端项目（本插件无业务逻辑，仅挂载）
├── DesignSystemPlugin.cs       # IPlugin 实现（空 Apply）
├── README.md                   # 本文档
├── ROADMAP.md                  # 路线图（下一步做什么）
└── web/                        # 自带界面（Vite lib 模式）
    ├── package.json
    ├── vite.config.ts
    ├── pnpm-workspace.yaml
    ├── dist/                   # 构建产物（发布时带上）
    ├── scripts/
    │   └── gen-tokens-css.ts   # 由 design/tokens.ts 生成 styles/tokens.css
    └── src/
        ├── index.ts            # 入口：导出 DesignSystemView
        ├── DesignSystemView.vue# 根视图（5 个标签页 + activeDs 换肤容器）
        ├── styles/
        │   ├── tokens.css      # 插件外壳中性主题（工具界面，非任何品牌）
        │   └── base.css        # reset + 布局工具类
        ├── design/             # 设计系统生成器内核
        │   ├── schema.ts       # DesignSystem 通用数据模型
        │   ├── colorScale.ts   # 通用色阶推导（任意色相 → 50–900）
        │   ├── presets.ts      # 内置参考示例 Stardust + 外壳主题 SHELL
        │   ├── generate.ts     # 生成引擎（行业识别 + 颜色词 + 模板）
        │   ├── tokensToCss.ts  # 设计系统 → CSS 变量（换肤核心）
        │   ├── exporters.ts    # 6 类产出文件序列化 + 下载/预览
        │   └── storage.ts      # localStorage 持久化（工作区 + 历史）
        ├── components/         # 通用组件（全部消费 --ds-* 槽位）
        └── sections/
            ├── DesignStudio.vue      # 设计工作台（输入 → 生成 → 浏览 → 导出）
            ├── TokenShowcase.vue     # token 可视化字典
            ├── ComponentGallery.vue  # 组件活体演示
            ├── console/              # 控制台 UI Kit（应用示例）
            └── marketing/            # 官网设计稿（应用示例）
```

### 3.2 本地构建

```bash
cd Plugins/DesignSystem/web
pnpm install
pnpm build          # → dist/index.js + dist/style.css
```

后端（无界面逻辑，回归确认即可）：

```bash
dotnet build Plugins/DesignSystem/DesignSystem.csproj
```

### 3.3 运行/预览

- 启动宿主：`cd publish && ForgeSelf.exe`（端口默认 51888）或按 `plugin-publish-verify` 技能一键发布。
- 浏览器访问 `http://localhost:51888/design-system`。
- 在「设计工作台」输入需求，点「生成设计系统」，然后：
  - 切「设计令牌 / 组件库 / 控制台 UI Kit / 官网设计稿」看换肤效果；
  - 切「实时预览」看内联预览；
  - 切「产出文件」下载 6 类文件。

### 3.4 关键约定

- **组件只消费 `--ds-*` CSS 变量，不写死色值** —— 这是「换肤」能成立的根本原因。
- **外壳使用中性主题** —— `tokens.css` 来自 `SHELL` 预设，让插件看起来是工具，不是某个品牌展台。
- **生成器输出 DesignSystem 对象** —— `tokensToStyleAttr(ds)` 把它变成容器内联样式，所有子组件自动换肤。
- **Stardust 是参考示例** —— 启动时默认 `activeDs = STARDUST`，让首次用户立刻看到一套完整系统长什么样。

---

## 四、设计体系通用模型

一份设计系统由以下结构构成（见 `web/src/design/schema.ts`）：

```
DesignSystem
├── meta
│   ├── name          # 设计系统名（从需求提取）
│   ├── brief         # 触发本次生成的需求原文
│   ├── source        # 'preset' | 'generated'
│   └── seed          # { hue, accentHue, industry, fontKey }（复现关键）
├── brand             # 品牌名 / Logo 概念 / 渐变 / 图标系统 / 装饰母题
├── tokens
│   ├── color         # brand / accent / neutral / semantic / surface / text
│   ├── typography    # fontSans / fontMono / scale / weights / lineHeights
│   ├── spacing       # 8pt 刻度
│   ├── radius
│   ├── elevation     # shadow-sm/md/lg
│   ├── border        # 半透明描边
│   └── motion        # easing + duration
├── components[]      # 组件规格（name / purpose / variants / anatomy）
├── applications[]    # 应用示例（name / desc / sections）
└── selfCheck[]       # 设计自检清单
```

### 颜色推导

- 用户**明确说颜色**（如"蓝色调"）→ 取该色相。
- 用户**没说颜色** → 按行业画像取默认色相（devops 紫、金融蓝、医疗青绿、电商橙、内容洋红、教育绿、企业 SaaS 靛蓝）。
- 辅助色 = 品牌色相 ± 约 75°（在 `accentHueOf` 中按视觉互补微调）。
- 中性色 / 语义色 / 间距 / 圆角 / 阴影 / 动效为**稳定导轨**，跨品牌不变 —— 避免换肤后观感变脏或可达性下降。

---

## 五、生成引擎

见 `web/src/design/generate.ts`。

**设计取向：确定性 + 可解释**，不接 LLM：
- 同一段需求永远产出同一份设计系统（可复现、可 diff、可回归、可离线）。
- 规则透明：关键词打分 → 行业 → 色相 / 字体 / 组件 / 应用示例模板。
- 颜色词优先于行业默认。

当前支持 7 个行业画像：devops、finance、health、commerce、content、edu、enterprise。无命中回落 enterprise。

---

## 六、产出文件清单

| 文件 | 类型 | 给谁用 | 说明 |
|------|------|--------|------|
| `design-system.md` | Markdown | 人读 / 评审 / 归档 | 完整规格（品牌 / 颜色 / 类型 / 间距 / 组件 / 应用示例 / 设计自检） |
| `preview.html` | HTML | 任何人，双击即开 | **自包含单文件预览**，内联 token + 示例 UI，离线可用 |
| `design-system.json` | JSON | 回导 / diff / 二次加工 | 设计系统本体结构化 |
| `tokens.json` | JSON | Figma 插件 / 主题包 / 构建管线 | 结构化 token（含 `$schema`） |
| `tokens.css` | CSS | 任意 Web 项目 | `:root` CSS 变量 `--ds-*` |
| `tailwind.tokens.js` | JS | Tailwind 项目 | `theme.extend` 扩展 |

---

## 七、当前实现是否符合需求

| 需求 | 状态 | 说明 |
|------|------|------|
| 输入需求 → 输出像 Stardust 一样完整的设计系统 | ✅ | 通用模型覆盖 7 大维度，Stardust 降为参考示例 |
| 插件本身是通用的，不绑死 Stardust | ✅ | 外壳中性主题；生成器据需求推导品牌；Stardust 仅作为默认预设 |
| 产出文件清单 + 导出 | ✅ | 6 类文件，可逐一下载 / 全部下载 / 复制 |
| 提供预览 | ✅ | 工作台「实时预览」+ 独立 preview.html |
| e2e 验证 | ✅ | `ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts` 1 passed |
| 生产级（持久化 / 健壮 / a11y） | ✅ | localStorage 工作区+历史、校验、错误态、toast、tab 语义 |

### 已知缺口（诚实）

| # | 缺口 | 影响 | 优先级 |
|---|------|------|--------|
| G1 | 生成引擎不理解语义（模板+关键词） | 奇异/跨品类需求只能得通用骨架 | P2 |
| G2 | 产物不落后端 | 无法跨设备共享 / 版本化 | P1（需升级给人确认，见 ROADMAP） |
| G3 | 产出是规格+线框，非像素稿 | 不能直接「照着切」 | 明确不做 |
| G4 | 无自动对比度 / 截图回归校验 | 自检靠人读 | P2 |
| G5 | pluginViewLoader 缓存键 = 版本号 | 同版本前端文案改动需硬刷 | P2 |
| G6 | 未接 Figma 双向同步 | token 变更需手工同步 | P3 |

---

## 八、为后来者准备的下一步

如果你接手这个插件，下面是**最有价值、按优先级排序**的下一步（详细方案与触发条件见 `ROADMAP.md`）：

### P1 · 建议优先做

1. **后端持久化设计产物**
   - 为什么：当前 localStorage 刷新/换浏览器即失，无法形成真正的「设计资产」。
   - 做什么：新增 `DesignRecord` 实体 + API 控制器 + 前端历史切换为后端拉取。
   - 风险：涉及数据库迁移与鉴权，按 AGENTS §6.4 需升级给人确认 schema。
   - 见 `ROADMAP.md §P1.1`。

2. **修复宿主体质性插件版本/更新 API**
   - 为什么：`PluginVersionService.Initialize(pluginsPath)` 从未调用，导致 `/api/plugin/updates|update|versions|rollback` 死代码；新插件 install 把文件写到 exe 根而非 `publish/plugins/<id>/`。
   - 做什么：在宿主启动时 Initialize PluginVersionService；install 路径改为 `AppContext.BaseDirectory/plugins`。
   - 见 `ROADMAP.md §P1.2`。

3. **pluginViewLoader 缓存键改为 version + content-hash**
   - 为什么：现在同版本号的前端改动用户必须硬刷才能看到。
   - 做什么：构建时计算 dist/index.js 的 hash，清单 URL 用 `?v=1.2.0&h=<hash>`。
   - 见 `ROADMAP.md §P1.3`。

### P2 · 体验增强

4. **LLM 增强模式（可选开关）**
   - 触发：用户明确要求「更聪明 / 读懂复杂需求」。
   - 做法：保留确定性引擎作为 fallback 与回归基线，复杂需求走 LLM，简单/重复需求走确定性引擎。
   - 见 `ROADMAP.md §P2.1`。

5. **自动对比度 / 截图回归校验**
   - 触发：自检第 2 条（对比度）需要自动化；展示页换肤后需防止回归。
   - 做法：Playwright 截图基线 + WCAG 对比度脚本。
   - 见 `ROADMAP.md §P2.2`。

6. **把「真宿主发布 + 浏览器走查」沉淀为可复用 skill**
   - 触发：每次插件发布都手动跑 skill 脚本，流程已成熟但未固化。
   - 做法：在 `.agents/skills/` 新增 `forge-design-system-verify` skill。
   - 见 `ROADMAP.md §P2.3`。

### P3 · 生态

7. **Figma / Tokens Studio 插件**
   - 触发：设计团队真正开始消费 token。
   - 做法：由 `tokens.json` 生成 Design Tokens Format 兼容格式。
   - 见 `ROADMAP.md §P3.1`。

8. **社区预设市场**
   - 触发：用户开始贡献自己的行业画像 / 预设。
   - 做法：把 `presets.ts` 拆成按文件加载的 presets 目录，支持导入/导出预设。
   - 见 `ROADMAP.md §P3.2`。

---

## 九、历史与关键决策

- **2026-09-01**：v1.0.0 是静态 Stardust 展示柜（token/组件/页面），无生成能力。
- **2026-09-01**：v1.1.0 补齐设计工作台（ProductDesign 模型），引入生成、导出、持久化、StageDesignSystemPlugin 目标。
- **2026-09-02**：v1.2.0 重构为通用设计系统生成器：`DesignSystem` 通用模型、Stardust 降参考示例、外壳中性、展示页换肤、产出文件 6 类。

关键修复：
- 移除 CSS 顶部 `@import url(fonts.googleapis.com)`，避免渲染阻塞导致插件无样式。
- 补齐 `StageDesignSystemPlugin` MSBuild Target，否则 `dotnet publish` 不复制插件 DLL。
- token 单一事实源：`design/tokens.ts` → `scripts/gen-tokens-css.ts` → `styles/tokens.css`。

---

## 十、参考

- 插件前端运行时契约：见 `specs/010-plugin-frontend`（或宿主板对应的插件加载文档）。
- 发布/真宿主验证 SOP：`.agents/skills/plugin-publish-verify/SKILL.md`。
- 插件层 e2e SOP：`.agents/skills/e2e-testing/SKILL.md`。
- 路线图：`./ROADMAP.md`。
