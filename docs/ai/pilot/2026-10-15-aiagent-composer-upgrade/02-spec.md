# 02 Spec — 新组件 composer 升级

## 1. 功能需求

### FR-1 上下文 chips 行（G1）

| 项 | 规则 |
| --- | --- |
| 位置 | 输入区上方（`.aiw-input` 之上），与旧版 chips 行同位置 |
| 内容 | 项目 / 工具×n / 技能×n / Agent / 模型 五项，有值才渲染（空态不占位） |
| 交互 | 每项可点「×」删除；点项目项 → 打开项目选择；其余项 → 打开对应上下文面板 |
| 语义 | 「所见即所发」—— chips 就是本次发送会带上的上下文 |
| 组件契约 | 组件只渲染 `config.context.chips`（`{ key, label, icon?, removable, onClick }`），**数据与行为由宿主注入** |

### FR-2 上下文入口（G1）

- 输入区左侧一个「上下文」按钮（图标 + 可选计数），点开面板含五个分区：项目 / 工具 / 技能 / Agent / 模型；
- 工具、技能为多选 + 搜索；Agent、模型单选；项目详见 FR-4；
- **不恢复 5 个常驻 popover 按钮**（闸门1 待拍板 P1，推荐行内触发为主）。

### FR-3 `/` 命令面板（G2）

| 项 | 规则 |
| --- | --- |
| 触发 | 输入 `/` 且位于**行首或前一个字符是空白**时弹出（避免路径 `/api/foo` 误触发） |
| 内容（全部混合） | 四组：**内置命令** / **工具** / **技能** / **脚本**，分组标题 + 组内按名称排序 |
| 过滤 | 输入后续字符按「名称 + 描述 + pluginId/source」模糊匹配；无匹配显示空态 |
| 键盘 | ↑↓ 移动、Enter 选中、Esc 关闭、Tab 同 ↓；选中后关闭面板 |
| 选中语义 | 内置命令 → **立即执行**（如 `/clear` 清空）；工具 / 技能 → **挂上下文 chip**，不写进输入文本；脚本 → 插入 `/run <脚本名>` 或按 P3 拍板 |
| 组件契约 | `config.triggers: [{ char: '/', groups: () => Promise<TriggerGroup[]> }]`；组件只管弹层与键盘，不碰数据源 |

### FR-4 项目选择（替代旧「目录」）（G1/I2）

- 面板内分区：① **已登记项目**（批次 2 后端 `GET /api/project/list`，按最近活动倒序）② **浏览其它目录…**（复用 `GET /api/project/browse-directories?path=`）；
- 选中后走既有 `POST /api/project/directory` → 刷新 `projectDir` → 连带刷新技能列表（既有 `watch(projectDir)` 已实现）；
- 文案统一为「项目」（后端 URI 与 `IProjectRegistryService` 术语本就是 project，零后端改造即可对齐）。

### FR-5 `@` 文件提及（G3）

| 项 | 规则 |
| --- | --- |
| 触发 | 输入 `@` 且位于行首或空白后 |
| 候选 | 当前项目目录下的文件；批次 1 用 `GET /api/project/files?path=` 逐级遍历缓存（限深 + 排除 `node_modules/.git/dist` 等），批次 2 视体验决定是否补后端搜索端点 |
| 过滤 | 按路径/文件名模糊匹配，最多展示 50 条 |
| 选中 | 在光标处插入 `@<相对路径> ` 并继续输入 |

### FR-6 组件可独立交付（G4）

- 新增能力**全部默认关闭**：不传 `config.triggers` / `config.context` 时，组件行为与今天完全一致；
- 组件内不出现任何 `ai-agent` / `forgeself` 业务字样与端点。

## 2. 非功能需求

| 编号 | 要求 |
| --- | --- |
| NFR-1 | `0 自定义 token`：新样式只用 `--el-*` 与既有 `--aiw-*`，`design:check` 不得新增违规 |
| NFR-2 | 无障碍：面板 `role="listbox"` + 选项 `role="option"` + `aria-activedescendant`；Esc 关闭并把焦点还给 textarea |
| NFR-3 | 输入法安全：`/` `@` 判定与 Enter 发送同样遵守 `isComposing` 保护 |
| NFR-4 | 性能：工具 112 条、脚本若干，面板打开时**一次性拉取 + 内存过滤**，不做每次输入请求；文件树按目录懒加载 |

## 3. 验收标准（可自动化）

| # | 验收点 | 手段 |
| --- | --- | --- |
| AC-1 | chips 行按当前上下文渲染，删 chip 后状态同步、后续发送不带该项 | vitest（组件）+ 现场 |
| AC-2 | 输入 `/` 弹面板，四组齐全；输入字符后过滤正确；↑↓/Enter/Esc 可用 | vitest + e2e |
| AC-3 | 路径文本（如 `/api/foo`、`a@b.com`）**不**触发面板 | vitest |
| AC-4 | 输入 `@` 弹文件面板，选中插入 `@相对路径 ` | vitest + 现场 |
| AC-5 | 不传 `config.triggers/context` 时组件行为零变化 | vitest（回归） |
| AC-6 | 项目面板能列出已登记项目（批次 2）并切换生效 | 现场 + e2e |
| AC-7 | `design:check` PASS、宿主 check 0 error、全量单测不新增红 | 门禁 |

> AC 编号说明：门禁脚本按 `AC\d+` 归一化比对，AC-1…AC-7 即 AC1…AC7。

## 4. 接口与数据源事实（2026-10-16 补记，内容抄自 00/01 与实现实测，无新增事实）

| 能力 | 端点/契约 | 备注 |
| --- | --- | --- |
| 工具列表 | `GET /api/ai-agent/chat/tools` | 112 条实测量级 |
| 技能 | `GET /api/project/skills` / `GET /api/skills` | 随 projectDir 切换 |
| 脚本 | `GET /api/scripts` | 本环境无数据（空组不渲染） |
| Agent / 模型 | `GET /api/agents` / `GET /api/ai-models` | 单选 |
| 文件 | `GET /api/project/files?path=` | 响应字段是 `files`（非 `items`，实测），只列当前层 |
| 项目目录 | `POST /api/project/directory` + `GET /api/project/browse-directories` | 批次 2 才补 `GET /api/project/list` |

组件契约：`config.triggers: [{ char, groups: () => Promise<TriggerGroup[]> }]`、`config.context: { chips }`，默认 `null` = 完全关闭（FR-6 可独立交付）。

## 5. 范围边界（2026-10-16 补记，与闸门1 拍板 P2 一致）

- **批次 1（本批）**：纯前端——ComposeTrigger/ContextChips/trigger.js/ChatWidget 挂载/config 键/composer.ts 组装层/AiAgentView 接线 + 单测 + 门禁 + 现场。
- **批次 2（不做）**：`GET /api/project/list` 端点、文件搜索端点、「上下文」入口按钮（批次 1.5）——见 06 §4 风险与遗留。
- **禁止**：后端任何改动；`agent-core` 内出现业务字样与端点（FR-6）；新增自定义颜色 token（NFR-1）。
