# 07 Final Report — AIAgent composer 升级（批次 1）

## 交付

| 需求 | 交付 |
| --- | --- |
| 输入框加功能 | chips 行 + `/` 命令面板 + `@` 文件面板 + 触发判定（路径/邮箱不误弹） |
| 项目选择（替代目录） | 项目目录可用 + chips 显示；已登记项目列表留批次 2 |
| Agent / 技能 / 工具 / 模型 | 全部回到 `/` 面板，选中即挂上下文 chip（不插文本） |
| 斜杠命令列表 + 技能 | 五组 149 项（内置命令 / 工具 / 技能 / 切换 Agent / 切换模型；脚本组本环境无数据） |
| `@` 引用文件 | 1500 项 BFS 候选，选中插入 `@AGENTS.md ` |

## 改动文件

| 文件 | 变更 |
| --- | --- |
| `Plugins/AIAgent/web/src/agent-core/components/ComposeTrigger.vue` | **新增** 触发面板骨架（分组/过滤/键盘/无障碍），零业务 |
| `Plugins/AIAgent/web/src/agent-core/components/ContextChips.vue` | **新增** chips 行（数据与回调全由 config 注入） |
| `Plugins/AIAgent/web/src/agent-core/utils/trigger.js` | **新增** 触发判定与过滤纯逻辑（与单测同源） |
| `.../agent-core/components/ChatWidget.vue` | 挂载两个新组件；`onKeydown` 面板优先；`detectTrigger`/`loadTriggerGroups`(WeakMap 缓存)/`pickTrigger`；发送后关面板；流式消息标 `streaming` |
| `.../agent-core/config.js` | 新增 `triggers` / `context` 两个默认 `null` 的键 |
| `.../agent-core/styles/widget.css` | `.aiw-trg*` 面板样式 + `.aiw-chips`/`.aiw-chip-ctx*` |
| `.../agent-core/types.d.ts` | `ChatWidgetExpose.exportSession` |
| `Plugins/AIAgent/web/src/composer.ts` | **新增** 宿主组装层：命令分组 / pickItem 分派 / BFS 文件收集 / 排除规则 / chips 生成 |
| `Plugins/AIAgent/web/src/AiAgentView.vue` | 加载脚本与文件树；`composerSources/Actions/rebuildTriggers`；chips watchEffect；`/export` 落盘；onMounted 接线 |
| `Plugins/AIAgent/web/src/composer.test.ts`、`agent-core/trigger.test.ts`、`.../ComposeTrigger.test.ts` | **新增** 59 条单测 |
| `Plugins/AIAgent/web/dist/*` | 重新构建产物 |

未碰：后端（本批零后端改动）、e2e 既有用例、宿主 `ForgeSelf.Web/src/**`。

## 验证

- 门禁：插件 build ✅ / 宿主 check 0 error ✅ / design:check PASS ✅ / 全量 vitest 863 过（2 红既有）✅ / 新增单测 59 条全绿 ✅ / 冒烟 e2e 1 passed ✅
- 现场：`/` 五组 149 项、`/read` 过滤到 4 项、选中工具 draft 清空 + chip 出现；`src/api/foo` 与 `a@b.com` 均不弹；`@` 1500 项浅层优先、`@AGENTS` 精确命中、选中插入 `@AGENTS.md `；项目 chips 与技能联动
- 截图：`screenshots/dev/ai-agent/composer-01-slash-panel.png`、`composer-02-at-files.png`

## 修掉的 3 个真 bug

1. **WeakMap 键用错**（改容器没改键）→ 面板永远 0 项 + console TypeError；
2. **`/api/project/files` 字段名读错**（`items` vs `files`）→ `@` 永远空；
3. **DFS + 无噪声过滤** → 1500 条全被 `.log` 和深层模板占满，源码被挤出。
另修一个时序隐患：`watch(projectDir)` 异步遍历途中重建 triggers 会把空列表缓存进组件。

## 状态

✅ 批次 1（零后端）已实现并验收。
⬜ 批次 2（`GET /api/project/list` + 文件搜索端点）+ 「上下文」入口按钮，待排期。
⏸ 发布 / 运行实例只读复验按用户要求暂缓；git 未动。

## Validation / Review（2026-10-16 补记，指向既有证据）

- Validation：见 05-evidence（静态门禁表 + 现场验收 + §5 AC 覆盖对照，来源等级 Verified）；
- Review：见 06-review（§6 Final Decision = APPROVED，批次 1 范围）。
