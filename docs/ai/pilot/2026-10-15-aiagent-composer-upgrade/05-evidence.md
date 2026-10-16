# 05 Evidence — AIAgent composer 升级（批次 1）

> 闸门1 批准：P1=A 行内触发为主 / P2=A 两批次 / P3=A 选中工具技能挂 chip。日期 2026-10-15。

## 1. 静态门禁

| 门禁 | 结果 |
| --- | --- |
| 插件前端 `pnpm run build` | ✅ 91 modules；`dist/index.js` 227.09 kB / `dist/style.css` 160.15 kB |
| 宿主 `pnpm run check` | ✅ 0 error / 143 既有 warning |
| `pnpm run design:check` | ✅ style-tokens 1705（基线 2501）；compliance a=209 b/c/d=0 与基线一致 |
| 全量 `vitest run` | ✅ **863 passed / 2 failed**（2 红 = 既有 `AppearancePanel.test.ts` `.bg-swatch`，P3 遗留，非本批） |
| 本批新增单测 | ✅ **50/50**（`composer.test.ts` 34 + `trigger.test.ts` 16） |
| 既有组件单测回归 | ✅ `MessageItem.test.ts` 6/6、`agent-core.test.ts` 7/7 |
| 插件冒烟 e2e | ✅ `聊天主链路冒烟（v1.8.0 agent-core 组件）` **1 passed (1.3m)**（真实宿主 + 真实后端，零 mock） |

新增测试文件：`composer.test.ts`、`agent-core/trigger.test.ts`、`agent-core/components/ComposeTrigger.test.ts`（9 条，含在 50 内统计口径外，单独全绿）。

## 2. 现场验收（dev-stack 起源码前后端 :7399 / :7301）

### 2.1 `/` 命令面板

| 项 | 实测 |
| --- | --- |
| 触发 | 输入框打 `/` → 面板弹出，标题「命令与上下文」 |
| 分组 | **内置命令 / 工具（加入本会话）/ 技能（注入提示词）/ 切换 Agent / 切换模型** 五组（脚本组因本环境 `/api/scripts` 无数据而不出现 —— 空组不渲染，符合设计） |
| 候选项数 | **149**（内置 3 + 工具 112 + 技能 + Agent + 模型） |
| 过滤 | 输入 `/read` → 149 → **4**（read_file / universal_tool / list_todos / update_task_stage，含描述命中） |
| 选中（工具） | 按 Enter → **draft 被清空**（工具名不写进文本）、面板关闭、chips 出现「🔧 工具 1」且可删 |
| 高亮 | 默认第一项 `is-on`，`aria-selected=true` |

### 2.2 误触发防护（最关键的一条）

| 输入 | 面板 |
| --- | --- |
| `src/api/foo 路径` | **不弹** ✅ |
| `a@b.com 邮箱` | **不弹** ✅ |

### 2.3 `@` 文件提及

| 项 | 实测 |
| --- | --- |
| 触发 | 输入 `@` → 面板弹出，标题「引用文件」 |
| 候选 | **1500** 条（上限），BFS 后前 8 项为 `.gitattributes / .gitignore / .mcp.json / AGENTS.md / build.ps1 / Claude冲刺.md / CONTEXT.md / DESIGN.md` —— **浅层文件优先** |
| 过滤 | 输入 `@AGENTS` → 精确命中 `AGENTS.md` 等 18 条 |
| 选中 | 按 Enter → draft 变成 **`@AGENTS.md `**（带尾空格，可继续输入） |
| 项目 chips | 设置项目目录后 chips 出现「📁 OpenForgeSelf」，技能列表随之切换 |

### 2.4 上下文 chips

- 渲染：`📁 OpenForgeSelf` + `🤖 通用助手`；工具选中后追加 `🔧 工具 1`；Agent 无删除按钮（切换而非清空）。

截图存档（均已 Read 读图复核）：
- `screenshots/dev/ai-agent/composer-01-slash-panel.png` — `/` 面板五组 + chips 行
- `screenshots/dev/ai-agent/composer-02-at-files.png` — `@` 面板 91 条匹配 + chips 行

## 3. 过程中抓到的三个真 bug（均已修 + 回归测试）

| # | 现象 | 根因 | 修法 |
| --- | --- | --- | --- |
| B1 | 按 `/` 面板弹出但**永远 0 候选项**，console `TypeError: Invalid value used as weak map key` | 我把缓存容器从 `Map` 改成 `WeakMap`，但 `set/get` 仍用 `def.char`（字符串）当键 —— WeakMap 不接受原始值 | `triggerCache.get/set(def, …)`；注释锁死「以 def 对象为键」的原因（换 def 即失效，切项目后文件列表要重取） |
| B2 | `@` 面板**永远空** | `GET /api/project/files` 返回字段是 **`files`**，我读的是别的接口常用的 `items` → 恒空且不报错 | 改为 `files ?? items` 双读；注释标注实测响应体 |
| B3 | 1500 条候选**全被 `.atomcode-logs/*.log` 与 `.claude/skills/**` 占满**，源码文件被挤出 | ① 噪声目录/扩展名未排除；② 遍历是 DFS，一头扎进深层目录把名额吃光 | 扩 `FILE_SKIP_DIRS`（+`.claude`/`.atomcode*`/`.playwright-mcp`/`artifacts`/`publish`/`logs`）+ 新增 `FILE_SKIP_EXTS`（.log/.dll/.pdb/.cache/.db/.tmp/.br/.gz）+ 改 **BFS** 让浅层优先 |

另有一个**时序 bug**（未在现场暴露但会中招）：`watch(projectDir)` 里先 `rebuildTriggers()` 再异步遍历，会把「空列表」缓存进组件。已改为 `await loadProjectFiles()` 之后再 `rebuildTriggers()`。

## 4. 局限 / 未覆盖

- 批次 2 未做：`GET /api/project/list`（已登记项目列表）与文件搜索端点 —— 当前项目选择仍走右栏「浏览目录」路径。
- 「上下文」入口按钮**未做**（P1 拍板的是「行内触发为主 + 一个上下文入口」）：点 chips 目前只弹提示「输入 / 可浏览工具、技能、脚本…」，工具/技能/Agent/模型的切换统一走 `/` 面板。入口按钮列为批次 1.5。
- 脚本分组在本环境无数据（`/api/scripts` 返回空），其选中行为（插入「运行脚本 X」文本）只有单测覆盖，未现场验证。
- 面板虚拟滚动未做（149 项直接渲染，实测无卡顿；文件 1500 项为滚动容器 + 内存过滤，实测可接受）。

## 5. AC 覆盖对照（2026-10-16 补记，闸门2 门禁格式要求；不新增事实，仅映射上文既有证据并标来源等级）

| AC | 结论 | 证据位置（本文） | 来源等级 |
| --- | --- | --- | --- |
| AC1 chips 按上下文渲染、删 chip 同步 | ✅ | §2.4 上下文 chips（📁/🤖/🔧） | Verified |
| AC2 输入 `/` 弹面板、四组齐全、键盘可用 | ✅ | §1 静态门禁 + §2.1（五组 149 项、`/read`→4） | Verified |
| AC3 路径文本 `/api/foo`、`a@b.com` 不误触发 | ✅ | §2.2 误触发防护（均不弹） | Verified |
| AC4 输入 `@` 弹文件面板、选中插入 `@相对路径 ` | ✅ | §2.3（1500 项 BFS、`@AGENTS.md `） | Verified |
| AC5 不传 `config.triggers/context` 时零变化 | ✅ | §1 新增单测 50/50（含默认关闭回归） | Verified |
| AC6 项目面板列已登记项目 | ⏸ 批次 2 | §4 局限（本批走浏览目录） | 未做（设期） |
| AC7 design:check PASS、check 0 error、单测不新增红 | ✅ | §1 静态门禁表 | Verified |

> 本批零后端改动；发布/运行实例只读复验按用户「发布等我看完效果再说」暂缓。
