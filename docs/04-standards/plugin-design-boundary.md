# 插件功能边界设计标准（Plugin Design Boundary）

> 版本 v1.0.0（2026-10-28）· 立标 pilot：`docs/ai/pilot/2026-10-28-plugin-design-boundary/` · 审计同日 20 插件全量完成（见 04-task 审计节）
> 立标缘起：AIAgent 曾实现文件三件套 + 万能工具（844 行全家桶），text-tools 与 dev-tools 三对同功能双实现，文件域被 mcp-center 与 file-tools 劈成两半。本文是「一个插件应该设计什么功能、保证不多不少」的唯一标准。

## 1. 适用范围

所有 `Plugins/` 下插件的功能设计与评审；新增插件立项（`plugin-feasibility-study`）；既有插件的越界/缺位判定。宿主内核（非插件）不适用，但内核与插件的职责切分参照本文 T0 定义。

## 2. 插件分类学（T0-T4 五层）

| 层 | 定义 | 现有实例 | 允许 | 禁止 |
|----|------|----------|------|------|
| T0 宿主内核 | 非插件框架层 | ToolRegistry / EventBus / IContext / ExtensionPointManager | 契约、注册、调度、公共机制 | 承载任何业务语义 |
| T1 平台公共插件 | 被多个互不依赖的插件需要的公共能力唯一宿主 | mcp-center（MCP 协议网关 + 公共工具宿主，双职责） | 公共工具（跨域基础小工具级）、协议网关、工具发现/转发/测试台 | 吞并某个领域插件的完整域 |
| T2 领域插件 | 一个内聚问题域的全部能力 | file-tools / dev-tools / memory-system / workflow-engine / system-monitor / script-runner / quick-links / scheduler / design-system / sems / im-gateway / cost-scope / todo-tracker | 域内工具 + 服务 + 持久化 + 界面 + 菜单 | 实现其他域的核心能力；依赖 T3 |
| T3 编排插件 | 消费并组合 T1/T2 能力驱动任务 | ai-agent / agent-hub | 消费、编排、协议词工具（plan/submit/finish 级） | 实现领域能力（§3.2 消费≠实现） |
| T4 壳/演示 | 界面壳、示例、测试台 | home / sample / tool-bridge | 壳逻辑、演示、测试台 | 注册正式业务工具（sample 演示除外） |

**T1 双职责立界**：mcp-center 的「网关面」（协议/listChanged/外部 MCP 管理/工具测试台）与「公共工具面」（universal_tool/run_terminal_command/get_current_time/calculate）是两个角色恰同住一插件。红线：公共工具面只收**跨域基础小工具**（无持久化实体、无专属 UI、任何插件都可能用）；某领域插件的完整域（如文件管理、文本处理）禁入。

## 3. 七条可执行条款

### 3.1 一插件一域

plugin.json `Description` 首句即**域声明**；该插件全部工具/服务/持久化实体/菜单必须能归入域声明。写不下首句 = 域未想清，先回 `plugin-feasibility-study`。

### 3.2 消费 ≠ 实现（T3 铁律）

T3 需要领域能力 → 经工具注册表 / `ctx.Get<T>()` 消费；能力不存在 → 给属主插件提需求，或立项新插件。**禁止「因为我要用所以我自己实现」**。反例复盘：AIAgent 曾因「agent 循环需要读写文件」而实现 read_file/write_file/list_files + universal_tool/search_tools（2026-10-28 迁出：文件三件套→file-tools，网关→mcp-center）。

### 3.3 公共能力唯一归属

被 ≥2 个互不依赖的插件需要的能力，唯一归属 T1 或宿主内核；**T2 之间禁止同功能双实现**。反例（已修）：text-tools 与 dev-tools 曾各有 base64/hash/format_json 三对同功能工具（2026-10-28 用户拍板 text-tools 整插件并入 dev-tools）。

### 3.4 依赖方向

T3 → T2/T1 单向消费；T2 ↔ T2 只经 `ForgeSelf.Abstractions` 契约 + `ctx.Get<T>()`（L1 默认；L2 事件需确认调研偏差#5 已修）；**禁止 T2 → T3、禁止循环**。契约一律入 Abstractions（architecture-design 铁律 2/4）。

### 3.5 名字稳定与归属解耦

工具实现可迁属主插件，**平台保留名/已发布工具名不因迁移改名**（运行时协议稳定性优先）。实践：文件三件套 2026-10-28 从 mcp-center 迁 file-tools，名字仍是保留名 read_file/write_file/list_files；text-stats 并入 dev-tools 后无保留名义务，故改名 dev_text_stats（ADR-004 前缀规则）。

### 3.6 退役与合并

插件合并时：同功能工具**退役**（属主侧已有等价实现），特有功能**改名迁入**（按属主前缀）；宿主前端页面与路由**不动**（页面属宿主，后端换家）；插件目录移除 + slnx/csproj 引用清理 + 前缀表/RenameRows 真源同步。退役插件不进 RenameRows（守卫 S3 对账要求源插件目录存在），插件级合并对账记入对应 pilot 文档。

### 3.7 执行类工具注册前对表

裸终端（run_terminal_command）/多语言脚本（script_run）/项目登记命令（sems_run_command）是三个**语义可区分但能力重叠**的执行通道。新增执行类工具前必须对照此清单：语义等价于既有通道 = 禁止第三实现；确有差异 = 在 02-spec 写明差异并过评审。

## 4. 域判定三问（「不多不少」操作化）

1. **越界问（不多）**：删掉这个功能，插件的核心使命是否依旧完整？——是 → 越界，迁出或退役。
2. **缺位问（不少）**：插件域内的自然需求，是否必须跨插件拼凑才能完成？——是 → 评估补齐，或显式声明消费关系（走 §3.4）。
3. **归属问（唯一）**：这个功能换个插件是否同样放得下？——是 → 定唯一归属：**实体归属**（功能的实体/数据/表在哪边）优先于**消费便利**（谁先用谁实现）。

## 5. 新增功能准入流程

```
新功能 → 它是哪个域的？ ──是某 T2 域内 → 找属主插件（不存在则 plugin-feasibility-study 立项）
       ├─ 跨域基础小工具（无实体/无专属UI/通用） → T1 mcp-center 公共工具面（守卫豁免走 ADR-004）
       ├─ ≥2 插件需要的能力但不满足小工具级 → 立项新 T2/T1（禁止塞进先到的插件）
       └─ 宿主内核级机制 → ADR + 升级给人（§8.5）
```

## 6. 守卫与自动化

- **命名与重名**：`ToolNamespaceGuardTests`（S1-S5，含 mcp-center 豁免断言）+ ADR-004。
- **插件清单一致性**：S1 扫 plugin.json toolPrefix 与真源一致；插件增删后 `docs/02-features/021-ai-agent.md` 工具清单为唯一文档真源。
- **语义重复**（同功能双实现）无法纯静态判定，准入流程（§5）+ 评审把关；本标准 §3.3 为判据。

## 7. 违规处置分级

| 级别 | 定义 | 处置 |
|------|------|------|
| ❌ 违规 | 违反 §3.2/§3.3/§3.4（消费≠实现、双实现、依赖反向） | 列入审计（03-plan），用户拍板后另开任务搬迁 |
| ⚠️ 疑点 | 域边界可辩护但需立界（桶化倾向、能力重叠） | 本标准立界条款 + not-taken 台账登记触发条件 |
| ✅ 合规 | 域内聚、判定三问全过 | 每次审计例行复核 |
