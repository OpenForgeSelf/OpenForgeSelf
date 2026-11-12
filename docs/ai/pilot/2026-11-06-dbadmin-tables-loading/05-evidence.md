# Evidence

> 阶段：Stage 7｜只记录实际发生的事；Verified / Inferred / Unknown 分级。
> 日志落 `.temp/*.log`（gitignored）。

## 0. AC 覆盖映射

| AC | 证据 |
|---|---|
| AC1 | §2 代码 + §5 e2e 运行态 loading（延迟真实请求 800ms 下 loading 出现并在完成后消失） |
| AC2 | §3 CT `tables-loading-1` 行为断言 + 基线截图读图 |
| AC3 | §3 CT update + `DbAdmin.ct.spec.ts` 25/25 绿；全量 CT 59/2（2 项为无关既有红） |
| AC4 | §2 插件 build PASS（dist/index.js 70.04 kB） |
| AC5 | §4 规范 5 处 diff 落地 |
| AC6 | §4 DbAdmin ui-design-brief / 06 评审稿补 loading |
| AC7 | 本文件 + 06 + 07 |

## 1. 实现（代码）

- `Plugins/DbAdmin/web/src/DbAdminView.vue`
  - 新增 `tablesLoading`（每连接）与 `tableLoadSeq`（请求序号）。
  - `loadTables`：开始置 loading、完成/失败 finally 清除；非 force 且加载中直接返回（去重）；force 重拉序号递增，旧响应丢弃。
  - 左栏系统/用户连接展开处：`tablesLoading[c.id]` 时渲染 `spinner + 加载表…`（`data-testid="tables-loading-<id>"`，`role=status`），`v-else` 才渲染表/（无表）。
  - CSS：`.node-loading` + `.spinner`（`--el-border-color` / `--el-color-primary`，`dba-spin .8s`，`prefers-reduced-motion` 关闭）。
- `Plugins/DbAdmin/web/src/types.ts`：`DbAdminFixture.tablesLoading?: Record<string, boolean>`。

## 2. 插件构建

```bash
cd Plugins/DbAdmin/web && pnpm run build
```

结果：**PASS**（`vite v6.4.5`，`dist/style.css 12.94 kB`、`dist/index.js 70.04 kB`，350ms）。日志 `.temp/dbadmin-web-build-loading.log`。

## 3. CT（画廊状态测试）

- 新增 story：`DbAdmin/TablesLoading`（`expanded:['c:1']`、`tables:{}`、`tablesLoading:{'1':true}`）。
- 新增 CT：`S2 点击连接加载表：显示局部 loading，空态不抢先`
  - 断言 `tables-loading-1` 含「加载表」、`.spinner` visible、页面无「（无表）」、`toHaveScreenshot()`。
- `CT_FRESH=1 pnpm run test:ct:update`（首次）：**58 passed / 3 failed**；其中新增 loading 用例 **✓**，基线生成；3 红为：
  1. `DbAdmin · S4 破坏性`：CT 断言「破坏性」，当前 UI 标签为「不可恢复」（HEAD 已入库文案，CT 断言陈旧）；
  2. `Gallery · 侧栏导航`：`AIAgent/MessageText` 不可见 → 30s 超时（长列表未滚动，既有 flake）；
  3. `SmartTerminal · 审计抽屉过滤`：过滤 command 后仍含 `permission_mode`（既有断言与实现不一致）。
- 同文件同步陈旧断言（`不可恢复`）并仅对 `DbAdmin.ct.spec.ts` 重生成基线：
  - `pnpm exec playwright test --config playwright-ct.config.ts src/components/DbAdmin.ct.spec.ts --update-snapshots` → **25 passed (15.5s)**
  - 复查（不更新基线）→ **25 passed (11.9s)**
- 基线截图（已读图）：
  `ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts-snapshots/S2-点击连接加载表：显示局部-loading，空态不抢先-1-ct-host-win32.png`
  —— 左栏 `demo.db` 选中，节点下有 spinner + 「加载表…」，**没有「（无表）」**。
- 全量 `CT_FRESH=1 pnpm run test:ct`（10:37）：**59 passed / 2 failed**，剩余 2 项均为上述 Gallery / SmartTerminal 既有红，与 DbAdmin loading 无关；已逐字进 TODO。

## 4. 规范/文档补强

- `AGENTS.md` §8 设计门禁第 3 步：异步动作 ≤100ms 局部 loading、空态不抢先、去重/旧响应不覆盖。
- `DESIGN.md` Do's：异步动作可见反馈 + 失败可重试。
- `.agents/skills/ui-ux-design/SKILL.md` G：异步加载不抢先空态 + 竞态 + 失败可重试两条。
- `.agents/skills/plugin-development/SKILL.md` §3.4：设计第 9 条、验证第 5/6 条（CT 状态回归）。
- `docs/18-templates/ai-pilot/02-spec.tpl.md`：交互验收标准增「异步加载」。
- `Plugins/DbAdmin/docs/ui-design-brief.md`：五态增「表加载」。
- `Plugins/DbAdmin/docs/06-UI交互评审稿.md`：S2 验收增 loading 不抢先空态。

## 5. 运行态 e2e（真实宿主）

```bash
cd ForgeSelf.Web && CI=1 E2E_CLEAN=1 pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

- 结果：**1 passed (51.3s)**。
- 新增运行态 loading 守卫：对真实 `/api/db-admin/connections/*/tables` 延迟 800ms 一次（`page.route`，不 mock 数据），断言 `[data-testid^="tables-loading-"]` 可见、无「（无表）」、完成后 loading 消失。
- 同用例仍输出 `[db-admin] 选中行对比度 light=10.41:1 dark=7.98:1`（上一批 PC-025 守卫未回归）。
- 日志 `.temp/dbadmin-e2e-loading2.log`。

## 6. 其它门禁

| 项 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 前端 check | `cd ForgeSelf.Web && pnpm run check` | **exit 0**；0 errors / 166 warnings；`[component-conventions] PASS（story 6 / ct.spec 6）` | Verified |
| 前端单测 | `pnpm run test` | **108 files / 1160 tests passed** | Verified |
| DbAdmin 单测 | `pnpm exec vitest run ../Plugins/DbAdmin/web/src` | **9 files / 61 tests passed** | Verified |
| 设计守卫 | `pnpm run design:check` | PASS（1761/2501；compliance a=209 b/c/d=0） | Verified |
| 本批 host 文件 ESLint | `pnpm exec eslint --config eslint.config.mjs src/components/DbAdmin.story.ts src/components/DbAdmin.ct.spec.ts` | exit 0（插件文件在宿主 eslint base 外，由 build/CT 编译覆盖） | Verified |

## 7. 已知限制

- 全量 CT 仍有 2 项无关既有红（Gallery 导航长列表、SmartTerminal 审计过滤），本批未修，已进 TODO；DbAdmin 子集 25/25 绿。
- 真实后端接口通常 <100ms，loading 在正常网络下可能一闪而过；e2e 用 800ms 真实请求延迟证明它确实存在且不抢先空态。
