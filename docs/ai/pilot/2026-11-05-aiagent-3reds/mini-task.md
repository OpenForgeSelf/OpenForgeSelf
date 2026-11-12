# mini-task：AIAgent 前端三红收编（task-19，轻量路径）

## Intent（为什么/做什么/到什么程度）
晨检 AIAgent 插件前端 vitest 过滤集 3 红（widget-contract 棘轮 ×2 + agent-core free 分流 ×1），隔离复跑 ×2 稳定复现 = 真实漂移/违例（非类序、非 PC-014 数据根族）。目标：三红清零、过滤集不新增红、修复路由按 lead 裁定小步一红一 commit。

## Spec（行为定义 · 含 Interaction Design）
- 红2（交互规格）：`.aiw-runstrip__btn` 必须有 hover 反馈——Interaction Design：鼠标悬停运行条按钮时边框转 `--aiw-primary`、底色转 `--aiw-primary-16`（与 `.aiw-todoentry__btn` 房式惯例一致）；键盘 Tab 聚焦时 `:focus-visible` 出 2px `--aiw-primary` 外描边（offset 1px）。禁裸色值（design 门禁）。
- 红1（动效棘轮）：7 处硬编码 animation 时长 > 上限 6。优先抽 `--aiw-dur-*` token；纯装饰长循环（burnin 240s/120s）不属于交互档位，按棘轮纪律在测试上方注释立项理由后上限 6→7，禁止无理由抬限。
- 红3（行为，最后修）：buildAgentChatConfig free 路径增量管道断——onDelta 一次未调（`expected [] to deeply equal ['你','好']`）。修实现不改断言；若牵出 AIAgent 后端行为问题即停并升级 lead。
- 边界：不改 widget-contract.test.ts 的断言语义（红1 若抬上限仅改常量与注释，纪律留痕）；不碰 qa task-12 数据根隔离框架。

## Plan（落点）
- `Plugins/AIAgent/web/src/agent-core/styles/widget.css`（红2 hover/focus；红1 token 抽取或计数归属）
- `Plugins/AIAgent/web/src/agent-core/widget-contract.test.ts`（仅当红1 按纪律抬上限）
- `Plugins/AIAgent/web/src/agent-core/agentBridge.ts` 或相邻配置源（红3 onDelta 接线）

## Task（验证命令）
每红：`node node_modules/vitest/vitest.mjs run agent-core`（ForgeSelf.Web 下，归口 vitest.config include ../Plugins/*/web/src/**/*.test.ts）；红2 后补 `pnpm run check`；三红清零后 AIAgent 过滤集全绿 + check 绿报 lead 复验。
