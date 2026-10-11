# M4c 插件前端迁移 · 批次执行规格（冻结模式 · 各插件批次共用）

> 来源：总方案 `docs/ai/pilot/2026-10-11-design-overhaul/03-plan.md` M4 节（已批准）；DESIGN.md「Plugin-First 架构约定」+「功能屏」组件规格 + Do's and Don'ts + 新建页面规范 + 门禁化说明；宿主批次规格 `m4-execution-spec.md`（同目录，仅 §0/§1 中与插件相关的部分适用）。
> 设计稿唯一真源：`D:\Users\12504\Downloads\design_handoff_forgeself_home_plugin_system\design_files\`（OtherScreens.jsx / kit-styles.css / colors_and_type.css / ../README.md）。

## 0. 流程合规（同宿主批，AGENTS.md §0）
1. 动手前写 `.forgeself/memory/2026-10-11.md` 日记条目 + TODO.md 🔄 待办；完成勾除。
2. 属于已批准方案，不新建 pilot；批证据写 `docs/ai/pilot/2026-10-11-design-overhaul/m4c-plugin-evidence-<批次>.md`（每批一份）；记录决策四要素。
3. 禁 git 状态操作；禁停/启/杀宿主进程；不删他人文件。
4. 判据看日志正文（重定向后 Read），不看 exit code。

## 1. 插件冻结模式（每个插件必须满足）
- 目标：插件前端（`Plugins/<X>/web/src/**`）按冻结模式统一——玻璃卡 + token 化 + 空态/加载/错误三态 + 品牌语汇；**功能保留 > 严格视觉拷贝**。
- **token 可用性**：插件页面由宿主 SPA 运行时加载，宿主 :root 的 `workshop-forge.css`（`--ink-*`/`--gold-*`/`--fg-*`/`--border-*`/`--paper-*`/`--bronze-*`/`--cinnabar-*`/`--font-*`/`--dur-*`/`--ease-*`）与 EP `--el-*` 变量会级联进插件 DOM，插件样式直接引用即可；**禁裸 hex/rgb（含内联 style）、禁 `var(--x,#hex)` fallback、禁新增自定义 token 族**（compliance 规则 A；插件自带 Tailwind 时用 `@theme inline` 内联 token，**不向 :root 发射 --color-***）。
- **组件约束**：插件模板**禁止 `<ElXxx>`**（沿用 `docs/05-guides/plugin-frontend-development.md`：用原生 HTML + token 变量或显式 import）；页头/卡片/空态可参照宿主三组件结构手写等价实现（插件不得 import 宿主组件），规格一致（eyebrow mono 10px 大写 0.18em 金 + title display 26px/700；玻璃卡 blur ≤24px；空态 icon 52px 金底 + serif 引言 + 可选主操作）。
- **品牌语汇**：主操作每屏仅一个（金底/金描边 chip）；破坏性 `--cinnabar-400`；状态点 on 金/off 灰/青铜绿；无 emoji 装饰、无胶囊按钮（极小 tag/chip 例外）、无蓝紫渐变、无可爱插画；hover `translateY(-1px)`/opacity .85，active `translateY(1px)`+阴影收紧，时长 `--dur-*`。
- **插件 UI 必须展示版本号**（plugin-development 铁律 13；缺失则补，取 plugin.json 版本）。
- **三态**：列表/面板空态、加载态、错误态齐全，不留白屏（错误可见留痕 + 重试入口）。
- **不动的文件**：插件 `web/dist/`、宿主视图、三组件、workshop-forge.css、路由/后端（M5 专属）、其他插件的文件。

## 2. 每插件步骤
1. Read 插件 `web/src/**` 当前实现 + 设计稿对应原型（如无直接原型按冻结模式生成）+ DESIGN.md 相关组件规格。
2. 按 §1 改造；保留全部功能与交互（数据取数、事件、props、路由桥、API 调用不动）。
3. 全文件扫裸色值与 fallback 并 token 化；确认版本号展示。
4. 该插件若有 vitest 单测（`Plugins/<X>/web` 下），跑之并同步断言。

## 3. 验证（快档，顺序执行；PowerShell 工具，日志重定向后 Read）
环境前置：`$env:NO_PROXY='localhost,127.0.0.1,::1'`；`$env:no_proxy` 一并；`$env:TEMP=$env:TMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\tmp'`
1. **插件构建**：`cd Plugins/<X>/web; pnpm run build`（*> 日志）→ PASS（记录 dist 产物与耗时）。每个插件都跑。
2. **守卫**：`cd ForgeSelf.Web; npm run design:check` → style-tokens ≤2484；compliance a≤244 b=0 c=0 d≤1（全仓扫描，含插件）。
3. **宿主门禁**（插件改动不影响但按规矩跑）：`pnpm run check`（0 errors / 76 既有 warnings 不新增）；`pnpm run test`（67 files / 771+ tests；若资源紧张可用 `npx vitest run --maxWorkers=2`，记录）。
4. **冒烟 e2e**（每插件 1 冒烟原则）：跑该插件的既有 spec——`cd ForgeSelf.Web; node node_modules/@playwright/test/cli.js test --config=playwright.config.ts e2e/plugins/<id> --output=<空目录>`（如 quick-links / mcp-center / todo-tracker / file-tools / sems / tool-bridge / im-gateway / agent-hub / ai-agent / design-system 的主 spec；CostScope 无 spec 则以 dev-stack 路由 200 + build 通过为准）。spec 未截图补 `page.screenshot()`（落 `ForgeSelf.Web\screenshots\e2e\<插件id>\`）；选择器/断言因改造失效同步更新（回归维护，非新增用例）；**Read 截图读图**（页头/卡片/空态/无溢出/无白屏/0 守卫命中）。若并发高负载导致 globalSetup 就绪超时（health 探测失败）或清理失败：可设 `$env:E2E_SIGN_EXE='false'` 跳过签名段重试一次（M4a-2 实证可行）；仍失败则如实记录为环境阻塞，不反复重试（留待低负载窗口统一复跑）。
5. **不跑全量 e2e**；不新增插件级 e2e 用例（只维护既有）；桌面 GUI 截图被 Windows 锁屏拒绝勿反复尝试。

## 4. 证据与汇报
- 批证据 `m4c-plugin-evidence-<批次>.md`：每插件执行清单（插件 / 文件数 / 动作 / 验证结果 / 证据等级）+ 决策四要素 + 未做/风险如实声明。
- 回复：每插件完成清单（绝对路径）、构建+守卫+e2e 结果摘要、风险与未做项；六段式 ≤半屏；中文；无 emoji。
