# Evidence

> 阶段：Stage 7｜只记录**实际发生的事情**；结果分级：Verified（亲自跑过，拿到真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。
> Task ID：2026-10-26-component-ct-gallery ｜ 日期：2026-10-26

## Changed Files（实际落地）

### 新增

| 文件 | 说明 |
| --- | --- |
| `ForgeSelf.Web/ct/index.html` | 画廊壳 + 侧栏样式 |
| `ForgeSelf.Web/ct/main.ts` | 挂载契约 `mountStory/setStoryProps/unmountStory`、侧栏、深链、重复 id 校验、Teleport 清理 |
| `ForgeSelf.Web/ct/stories.ts` | 显式 import 聚合 11 条故事 |
| `ForgeSelf.Web/ct/types.ts` | `ComponentStory`（含 `slots`） |
| `ForgeSelf.Web/ct/vite.config.ts` | 端口 5190、`@` 别名、共享依赖别名到宿主 node_modules、EP 自动导入 |
| `ForgeSelf.Web/ct/README.md` | 三条命令 + 深链 + 加故事四步 |
| `ForgeSelf.Web/playwright-ct.config.ts` | CT 配置（testDir `src/components`、channel chrome、webServer 起 ct vite） |
| `ForgeSelf.Web/tsconfig.ct.json` | `ct/**` 与 CT 配置的类型检查 |
| `ForgeSelf.Web/scripts/check-component-conventions.mjs` | 组件规范守卫 |
| `ForgeSelf.Web/src/components/ForgeCard.story.ts` | 3 个变体 |
| `ForgeSelf.Web/src/components/PageHead.story.ts` | 2 个变体 |
| `ForgeSelf.Web/src/components/EmptyState.story.ts` | 2 个变体 |
| `ForgeSelf.Web/src/components/AIAgentMessageStoryHost.vue` | AIAgent 插件聊天组件的宿主薄壳（含 `.aiw-root` 容器与两套插件 CSS） |
| `ForgeSelf.Web/src/components/AIAgentMessage.story.ts` | 4 个变体（纯文本/工具卡+正文/失败可续跑/用户） |
| `ForgeSelf.Web/src/components/ForgeCard.ct.spec.ts` | 3 个用例 |
| `ForgeSelf.Web/src/components/EmptyState.ct.spec.ts` | 2 个用例 |
| `ForgeSelf.Web/src/components/AIAgentMessage.ct.spec.ts` | 4 个用例 |
| `ForgeSelf.Web/src/components/Gallery.ct.spec.ts` | 3 个用例（导航/深链/未知 id） |
| `ForgeSelf.Web/src/components/ct-support.ts` | CT 共享助手 `openStory` |
| `docs/04-standards/frontend-component-development.md` | 组件开发规范（唯一真源） |
| `.agents/skills/component-visual-loop/SKILL.md` | 组件可视化闭环技能 |

### 修改

| 文件 | 改动 |
| --- | --- |
| `ForgeSelf.Web/package.json` | 新增 `ct:server` / `test:ct` / `test:ct:update`；`check` 追加组件规范守卫 |
| `ForgeSelf.Web/eslint.config.mjs` | 新增 `ct/**/*.{ts,vue}` 的 browser+node globals 覆盖 |
| `ForgeSelf.Web/tsconfig.json` | 引用新增 `tsconfig.ct.json` |
| `ForgeSelf.Web/tsconfig.app.json` | 新增 `allowJs: true`（跨包 story 壳 import 插件 JS SFC） |
| `.gitignore` | 新增 `ForgeSelf.Web/**/*-snapshots/`（CT 基线不入库） |
| `docs/README.md` | §一 新增一行（组件开发规范 + 画廊入口） |
| `AGENTS.md` | §2.4 新增技能行 `component-visual-loop` |

### 与 03-plan 的偏差（已在 03 偏差表登记）

| 偏差 | 原计划 | 实际 |
| --- | --- | --- |
| AIAgent story 落点 | 计划里未列插件组件 | 用户要求「用 AI Agent 插件的聊天助手测试」⇒ 增 3 个宿主侧文件（薄壳 + story + ct.spec）；插件目录零改动 |
| CT 共享助手 | 未列 | 新增 `src/components/ct-support.ts`（3 个宿主 spec 共用，避免复制粘贴） |
| 画廊壳用例 | 未列 | 新增 `Gallery.ct.spec.ts`（AC2/AC9 需要；守卫对其「无同名 story」显式豁免） |
| 类型配置 | 未列 `allowJs` | 跨包 import 插件 JS SFC 触发 TS7016，`tsconfig.app.json` 增 `allowJs: true` |

## 验证结果

| # | 命令（cwd = `ForgeSelf.Web`） | 结果 | 等级 |
| --- | --- | --- | --- |
| V1 | `pnpm run type-check`（vue-tsc -b，4 个 project） | exit 0，日志无 error | Verified |
| V2 | `npx eslint ct src/components/{ForgeCard,PageHead,EmptyState,AIAgentMessage}.story.ts src/components/*.ct.spec.ts src/components/ct-support.ts src/components/AIAgentMessageStoryHost.vue scripts/check-component-conventions.mjs` | exit 0（本批新文件 0 error 0 warning） | Verified |
| V3 | `node scripts/check-component-conventions.mjs` | `PASS（story 4 个 / ct.spec 4 个，均已注册、配对、标注）` | Verified |
| V4 | `pnpm run test:ct:update` | `9 passed` 首跑生成基线；日志逐条写 `A snapshot doesn't exist ... writing actual` | Verified |
| V5 | `pnpm run test:ct` | 首轮 `9 passed`；加画廊壳用例后 `12 passed (8.7s)` | Verified |
| V6 | `pnpm run design:check` | `[style-tokens] 1761（基线 2501）PASS`；`[design-compliance] a=209 b=0 c=0 d=0（基线同值）PASS` | Verified |
| V7 | 读图：`ForgeCard...默认态...png` | 深色玻璃卡、圆角/边框可见，标题与 mono 副文案两行正常 | Verified |
| V8 | 读图：`AIAgent...工具卡与正文共存...png` | 工具卡（`run_terminal_command` / 完成 / 420ms / 入参 JSON）与正文「命令已执行完毕，工作区是干净的。」同时可见 | Verified |
| V9 | 读图：`EmptyState...完整空态...png` | 金色图标块 + 主文案 + mono 副文案 + 金边按钮齐全、居中无错位 | Verified |
| V10 | 守卫红灯探针（两枚临时文件 `__Probe.story.ts` / `__ProbeOnly.ct.spec.ts`） | `[FAIL]` 三条：未注册 / 缺同名 story / 缺「测试状态」，exit 1；删除后重跑 PASS | Verified |
| V11 | `pnpm run check`（全量） | exit 1：**唯一 error** 在 `e2e/plugins/ai-agent/ai-agent.live.spec.ts:202 no-useless-assignment`（该文件为并行会话当日在写的 live 用例，`git status` 显示未跟踪）；本批文件 0 error，vue-tsc 与守卫均通过 | Verified（含归因） |
| V12 | `npx vitest run` | 1025 用例：1024 passed / 1 failed；失败项 `Plugins/AIAgent/web/src/agent-core.test.ts > 错误事件：transport 内转为 Error 抛给 ctx.onError`（并行会话在改 `useChat.js`，非本批）；日志里无任何 `.ct.spec` 被收集 | Verified（含归因） |
| V13 | 画廊人工浏览（侧栏点击 / 深链 / 未知 id） | 由 `Gallery.ct.spec.ts` 三条用例在真实浏览器中断言（侧栏列出 4 个组件族、点击切换、深链保留侧栏并高亮、`chrome=0` 无侧栏、未知 id 列出可用 id） | Verified |
| V14 | 侧边栏导航配置（2026-10-26 输入15，独立工件 `2026-10-26-ct-nav-config`） | `CT_FRESH=1 pnpm run test:ct` **13 passed**；type-check / 本批 eslint / 组件守卫 / design:check 全绿；5190 现场三地址浏览器走查 PASS | Verified |

## AC 覆盖

| AC | 证据 |
| --- | --- |
| AC1 深链渲染（人类保留侧栏 / chrome=0 关闭） | V5 + V14（`Gallery.ct.spec.ts` 第 34/46 行两条用例：深链 active 高亮；`chrome=0` 时 `.nav-group` count=0、`body` 无 `has-nav`） |
| AC2 侧栏列出全部故事 + 点击切换 | V5（`Gallery.ct.spec.ts` 第 10 行用例，四个组件族 id 都在，点击后 `.aiw-msg` 出现、`.forge-card` 消失、active 跟随） |
| AC3 基线生成 + 二次全绿 + 每例含断言与截图 | V4/V5；每个用例均「先 expect 后 toHaveScreenshot」，基线文件 9 张在 `*-snapshots/` |
| AC4 `pnpm run check` 通过 | V1（type-check）+ V2（本批 lint）+ V3（守卫）各自 exit 0；全量 check 的唯一 error 归因于并行会话文件（V11），本批不引入 |
| AC5 `pnpm run test` 全绿且不收集 ct.spec | V12：无 `.ct.spec` 被收集；1 条失败属并行会话在改的插件用例，本批未触碰 |
| AC6 `design:check` 通过且棘轮不涨 | V6（token 族计数 209 = 基线 209，裸色值 1761 < 基线 2501） |
| AC7 规范文档 7 项 + 两处登记 | 读回 `frontend-component-development.md`（§1 四层/§2 硬约束/§3 命名/§4 测试矩阵/§5 CT 三约定/§6 准入/§7 关系），`docs/README.md:23`、`AGENTS.md:117` 各一行 |
| AC8 守卫三类违规红/恢复绿 | V10 |
| AC9 未知 story id 不白屏 | V5（`Gallery.ct.spec.ts` 第 42 行用例） |

## Known Limitations

1. **全量 `pnpm run check` 与 `vitest` 各有一条外部红灯**（V11/V12），均为同一工作树里并行会话当日的 AIAgent live 验证改动；本批未触碰这些文件，也未修（范围控制）。
2. 画廊只提供 Pinia、不提供 vue-router；依赖路由的组件目前需要 props 注入或暂不接入。
3. 像素基线不入库（照 Cube.Vue）：换机器/换 Chrome 版本后需 `test:ct:update` 重建。
4. 跨包组件目前用手写薄壳（`AIAgentMessageStoryHost.vue`）；插件 story 目录约定暂统一放在宿主 `src/components`，未实现「插件内 story 自动发现」。

## Unresolved Issues

| # | 内容 | 归属 |
| --- | --- | --- |
| U-1 | `e2e/plugins/ai-agent/ai-agent.live.spec.ts:202` lint error（`sawRunning` 赋值未使用） | 并行会话（live 验收批） |
| U-2 | `Plugins/AIAgent/web/src/agent-core.test.ts` 失败用例（transport 错误事件） | 并行会话（`useChat.js` 在改） |
| U-3 | 存量 `src/views/` 15 个大文件尚未按新规范拆分 | 后续批次（规范 §2.7 棘轮） |
