# 07 Final Report — 按高保真设计稿迁移（终态交付）

> 任务目录：`docs/ai/pilot/2026-10-11-design-overhaul/`
> 交付时间：2026-10-11 深夜 · 状态：✅ COMPLETED（M0-M8 迁移完成并验证；发布动作待用户确认后执行）

## 1. 任务

以 `D:\Users\12504\Downloads\design_handoff_forgeself_home_plugin_system`（入口 `design_files/index.html`）为唯一真源，对 OpenForgeSelf 宿主与插件 UI 做全部迁移；完善文档规范，保证后续新建页面/查询按规范执行；UI、UX、品牌三面一致。同时按用户 /goal 四条：发布/验证流程重排、DESIGN.md 门禁化、基础设施后自动迭代少打扰、完成全部迁移。

## 2. 完成内容与主要变更

**基础设施（用户 /goal ①②③，已生效）**
- 六步闭环（plugin-development 铁律 7/24/25 + plugin-publish-verify 技能）：① 门禁单测+集成 → ② 每插件仅 1 冒烟 e2e → ③ dev-stack 起源码本地前后端按验收标准验收 → ④ 发布默认本地目录 `release-local.ps1 -Sign -UpdateDir` → ⑤ 运行实例只读复验 → ⑥ 文档日志。发布方式=本地目录已按用户指令定稿。
- DESIGN.md 门禁化：`scripts/check-style-tokens.mjs`（棘轮，禁裸色值/fallback 上涨）+ `scripts/check-design-compliance.mjs`（a 新 token 族 / b 胶囊按钮 / c blur / d 蓝紫渐变）+ pre-commit hook 接线 + `ForgeSelf.Web/package.json "design:check"` 串联双守卫。
- AGENTS.md §8.5 自动迭代：决策四要素（决策→背景→理由→准则）留痕；仅破坏性/发布/高风险/新场景/3 次失败才请示用户；其余自主闭环按 §10.4 汇报。已按此规范执行全程（本任务仅 1 个待拍板项：发布）。

**迁移（M0-M8）**
- M0 资产本地化：6 背景图（含熔炉补图修复外链 404）+ 5 品牌 SVG → `ForgeSelf.Web/public/`；背景系统（appearance.ts 12 纯色/6 图/遮罩/`forgeself:bg` 持久化）。
- M1 token：`workshop-forge.css` 浅（宣纸）/暗（墨黑）两套 + 设计稿 token 族（--ink-*/--gold-*/--fg-*/--border-*/--paper-*/--bronze-*/--cinnabar-*）+ 玻璃参数 + 动效刻度（--dur-*）。
- M2 壳层：TopNavbar 玻璃条 + 金晕 logo + 金字 tab；StatusBar；BackgroundPanel；助手气泡。
- M3 首页 Home 插件重构（hero 问候卡/12 常用功能/5 列看板/底部两列，home.spec.ts 收敛 1 冒烟）。
- M4 宿主 22 屏 + 12 个含 web/ 插件前端冻结化迁移：PageHead（eyebrow 金 mono + serif title + actions slot）/ ForgeCard（玻璃卡）/ EmptyState（icon 52px 金底 + title serif + sub mono）三组件全量接入；裸色值/var fallback/蓝紫渐变清零；插件 UI 全部展示版本徽标（AIAgent v1.7.3 / AgentHub v1.0.10 / QuickLinks v1.0.1 / Sems / CostScope / DesignSystem / TodoTracker v1.1.3）。
- M5 Plugin-First：manifest surfaces 四类（tab/home-widget/quick-action/status-badge）后端规范化校验 + 前端按 surface 分发消费 + 测试 + 功能文档 040。
- M6 动效：ForgeCard/PageHead 挂载 --dur-* 刻度（进入 160/交互 220/面板 360）+ 一次性进入动画 + reduced-motion 降级 + 动效规范沉淀 ui-ux-design 技能。
- M7 文档守卫：docs/README.md 登记 DESIGN.md 唯一真源；AGENTS.md 更新；DESIGN.md 新增「新建页面与查询执行规范」+ 待对齐勾销。
- M8 收口：全量守卫终值绿 + 受阻 e2e 低负载复跑全绿 + 证据合并 + 06/07 收尾（本报告）。

## 3. 验证结果（只列实际执行）

| 项 | 结果 | 来源等级 |
|----|------|---------|
| `npm run design:check`（双守卫） | style-tokens **1470**（≤2484，较基线 2501 减 1031）；compliance **a209 b0 c0 d0**（≤a244 d1） | ✅ Verified |
| `pnpm run check` | 0 errors / 145 warnings（无新增，全为宿主既有 SVG 格式项） | ✅ Verified |
| `pnpm run test`（vitest） | 70 files / 792 tests 全绿（M6 新增 12 例） | ✅ Verified |
| 12 插件 `pnpm run build` | 全 PASS（含 DesignSystem 445.75kB / AIAgent 127KB 等） | ✅ Verified |
| 后端 `dotnet build` + `dotnet test`（M5 中档） | build 0 err；全量 2891 过 / 13 失败全为预存基线红，M5 新增 0 红 | ✅ Verified |
| e2e 定向冒烟（--workers=1） | 插件批 24 过/7 失/3 跳、宿主批 30 过/5 失；受阻项复跑全绿（ai-agent 7/7、todo-tracker 14/14、menu-route 4/4、prompts-skills 7/7、home 1/1）；失败项定性均非迁移回归（SQLite 写锁 flake / 后端 MCP 500 / live 变体 harness 错配 / 数据依赖） | ✅ Verified |
| dev-stack 起源码验收（③） | 宿主路由全 200、鉴权 API 带 token 200 无 token 401 | ✅ Verified |
| 走查截图读图 | home / agents / update-panel / code-snippets / workflows / plugin-store / ai-agent / todo-tracker / sems / file-tools / design-system / agent-hub：页头三件套/玻璃卡/空态/版本徽标/无溢出无白屏 | ✅ Verified |
| 桌面 Chrome 走查 | 未做（Windows 锁屏阻塞，全程以 e2e 隔离实例全页截图为准） | ⚠️ 环境阻塞 |

## 4. 设计决策

- 六条决策四要素已入 05-evidence §6（ForgeCard role=button 与 .glass 分工 / 插件 fallback 剥离模式 / DesignSystem 别名重映射 / 版本徽标铁律 / 冒烟 e2e 策略 / M5 surfaces 兼容策略）。
- 核心原则：功能保留 > 严格视觉拷贝；token 单一来源（禁裸色值/禁新 token 族/禁 var fallback）；玻璃不过头（blur 按设计稿 24/20px）；金/主色收敛到 --gold-* 族。

## 5. 代价与收益

- 投入：本任务全程（M0-M8）117 个文件变更未提交（符合禁 git 约束）；并发 5 批编排 + 低负载复跑；环境高负载反复 flake 通过单 worker/E2E_SIGN_EXE=false/低负载窗口缓解。
- 收益：全产品 UI/UX/品牌三面与设计稿一致且由机器守卫强制；新建页面/查询有规范 + 门禁；发布/验证流程可重复闭环；后续迭代少打扰自主闭环。

## 6. 不做事决策（NOT-to-do）

- 不修 CostScope DI 缺口（`ITurnTelemetryQuery` 构造注入 vs Cordis 子容器不转发）→ 已记 TODO P1（修法=IContext 运行期解析或补入 HostProvidedServiceContracts），属后端独立任务。
- 不修后端 13 个预存基线红 + WorkflowServiceResolver 预存编译错误（2026-10-10 提交遗留）→ 已记 TODO。
- 不执行发布（本地目录打包）→ 发布类动作待用户确认 UpdateDir 与版本号。
- 不 git 提交（用户硬约束：未经许可禁 git 状态操作）。
- quick-links live 变体 spec 不改写（须走 playwright.live.config 对接 :51888，隔离 harness 错配非代码问题）。

## 7. 风险

- CostScope 数据面板在 dev-stack 起源码下 500（后端接线缺口），数据态视觉未取证；宿主侧运行实例同样会 500 直到后端修复。
- 后端全量测试 13 个预存基线红 + 1 个预存编译错误（测试工程默认无法编译，M5 临时解锁验证后已恢复原状）。
- e2e 深档（全量）未跑（本批定向复跑 + 各批冒烟已覆盖迁移面）；CI/低负载窗口可补。
- PersonalLibraryView 为孤儿视图（未注册路由，仅 features.ts 信号引用），无 URL 直达路径，视觉仅静态验证。

## 8. 结论

- 状态：✅ **COMPLETED**（M0-M8 迁移完成并验证；基础设施生效；未发现迁移引入回归）。
- 可否交付：可交付本批全部迁移成果（工作树 117 文件待用户确认提交）；**不可宣称「已发布到宿主」**——本地目录发布待用户确认 UpdateDir/版本号后执行，随后用户点「自动更新」再做运行实例只读复验。
- 是否建议 Code Review：建议（改动面大：宿主 22 屏 + 12 插件前端 + 后端契约 + 文档守卫；但各批证据齐备、守卫全绿、无迁移回归）。
- 下一步（待用户拍板）：① 本地目录发布（`release-local.ps1 -Version <vX.Y.Z> -Sign -UpdateDir <目录>`——需确认版本号与 UpdateDir 指向哪个运行实例的更新源）；② 是否 git 提交本批 117 文件（现禁 git 状态操作）。
