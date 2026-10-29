# 03 · Plan

> **闸门1 拍板（2026-10-28 22:47，经用户询问工具）**：① 批准按本方案实施；② **D-V1 = text-tools 全部合并到 dev-tools**（超出推荐项：不是退役 3 件而是整插件并入）；③ **D-V2 = 方案 A**（文件三件套实现迁 file-tools，名字维持保留名）。两项拍板均**随本批实施**（03-plan 批次扩为 B1-B6）。

## 批次（实施记录）

| 批 | 动作 | 文件 | 内容 |
|----|------|------|------|
| B1 | 标准文档 | `docs/04-standards/plugin-design-boundary.md`（新） | v1.0.0：T0-T4 分类学、判定三问、七条款、AIAgent 反例复盘、准入流程 ✅ |
| B2 | 审计报告 | 本文件审计节 + `docs/02-features/021-ai-agent.md` 划分小节指引 | 20/20 插件逐行判定；违规项给建议 ✅ |
| B3 | 登记 | `docs/README.md` | 标准文档登记速查 ✅ |
| B4 | 台账 | `docs/07-decisions/not-taken-decisions.md` | 046 执行三通道不合并 / 047 委派双通道立界 / 048 颜色三件套留 dev ✅ |
| B5 | **D-V2 实施** | FileToolFunctions/FileToolOps → `Plugins/FileTools/Services/`（命名空间+Id/PluginId 改 file-tools，名字保留名不变）；FileToolsPlugin 注册三件套；McpCenterPlugin 删注册；守卫 MovedTo +3 行；FailEnvelope internal→public；mcp-center 2.8.0 / file-tools 1.2.0 ✅ |
| B6 | **D-V1 实施** | TextStatsService(+ITextStatsService/TextStatsResult) 迁 `Plugins/DevTools/Services/`；TextStatsToolFunction（dev_text_stats）迁 `Plugins/DevTools/`；4 个同功能工具退役；TextTools 目录移 `.trash/2026-10-28-texttools-merged/`；slnx/宿主 csproj/Tests csproj 清引用；4 个插件载具测试换 SamplePlugin（lifecycle 测试断言类型改 ISampleGreetingService）；DevToolsPlugin 注册+菜单项；plugin.json 1.1.0 ✅ |
| B7 | 文档同步 | 021 清单（mcp-center 6/FileTools 9/DevTools 27/TextTools 行删/总数 124）、ADR-004 修订节（D-V1/D-V2）、宿主 features.ts signals、AIAgent composerContext 名册 ✅ |
| B8 | 验证 | 全量编译 0 err / 过滤集 183/183 / vitest 1035/1035 / check 0 / AIAgent web build / mcp-center 冒烟 8/8 ✅ |

## 审计初稿（依据 021 清单 + plugin.json 实测，B2 落稿）

### ❌ 违规（功能重复，条款 3）

| # | 发现 | 建议 | 代价 | 触发条件 |
|---|------|------|------|----------|
| V1 | text-tools 的 `text_base64_encode/decode`、`text_hash`、`text_format_json` 与 dev-tools 的 `dev_base64_*`、`dev_hash`、`dev_format_json` **同功能双实现**（021 :215-216 实锤；027 命名空间统一只清了名、没清功能） | 归属 dev-tools（开发者工具域，且 dev 版参数更全）；text-tools 退役 3 个重复工具保留 `text_stats`（真文本统计）。反向方案：dev 退役留 text——按「实体归属+功能完整度」裁 | 每工具删注册+UI 面板项+文档行；无数据迁移（无持久化实体） | 用户拍板归属方向后另开小任务 |
| V2 | 文件域劈半：读写在 mcp-center（保留名 read_file/write_file/list_files）、管理在 file-tools（rename/cleanup/compress/stats/extract/dir_stats） | **方案 A（推荐）**：三件套实现迁 file-tools，名字维持保留名不变（FR-5 名字稳定与归属解耦）——文件域归一；mcp-center 保留网关+发现+公共小工具。**方案 B**：维持现状，mcp-center 双职责含文件面（理由：运行时协议面高频、跨插件依赖底座） | A：3 类迁 Plugins/FileTools + FileToolOps 引用面 + 守卫豁免清单更新（名字不变故风险低）；B：零改动但文件域永久劈半 | 闸门1 用户拍板 A/B |

### ⚠️ 疑点（可辩护，标准里立界）

| # | 发现 | 处置 |
|---|------|------|
| W1 | mcp-center 五合一（网关+公共工具+小工具+测试台+外部 MCP 管理）——桶化倾向 | 标准里 T1 立红线：公共工具面仅收「跨域基础小工具」（calculate/get_current_time 级），领域完整域禁入；网关面=协议+发现+转发 |
| W2 | 执行三通道：`run_terminal_command`（裸终端）/`script_run`（多语言脚本）/`sems_run_command`（项目登记命令）——语义有差但能力重叠 | 标准条款：执行类工具注册前对照执行域清单；本批不合并（各有 UI/数据依赖）→ not-taken 登记 |
| W3 | 委派双通道：`todo_dispatch`（todo 域内派发 agent）vs `hub_delegate_task`（通用委派总线） | 标准条款：特化委派应底层调用通用委派（经契约）而非平行实现；本批不动，触发条件=第二个特化委派出现 |
| W4 | dev-tools 的颜色三件套（dev_color_*）与 design-system 的设计域边缘重叠 | 可辩护（换算小工具 vs 设计资产库）；登记观察 |

### ✅ 合规（16 个）

ai-agent（修订后 4 工具全协议/编排，反例已修）、agent-hub、file-tools（域内 6 件）、memory-system、workflow-engine、system-monitor、script-runner、quick-links、scheduler、design-system（8 件域内聚）、sems（22 件域内，除 W2 边界）、todo-tracker（8 件，除 W3）、im-gateway、cost-scope、home、sample、tool-bridge（T4 测试台）。

## 争议点（闸门1 请拍板）

- **D-V1**：text/dev 三对重复的归属方向（推荐：留 dev 退役 text 三件）。
- **D-V2**：文件三件套归属——方案 A 迁 file-tools（推荐，文件域归一）vs 方案 B 维持现状。

## Allowed / Forbidden

- Allowed：新增标准文档、README 登记、not-taken 登记、021 增加划分小节指引、pilot 工件。
- Forbidden：**不搬任何工具实现代码**（V1/V2 修订实施是拍板后的独立任务）；不改 ADR-004 已拍板内容；不碰外部会话 WIP 文件。

## 验证命令

- 标准文档：人工评审（用户闸门1/2）；`pwsh scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-28-plugin-design-boundary` PASS。
- 审计完整性：20 插件逐行覆盖核对（计数=20）。
