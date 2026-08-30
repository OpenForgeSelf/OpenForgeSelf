# OpenForgeSelf 文档中心

> 入口 + 总索引。本文件回答两个问题：**这个文档体系里有什么**、**你要的东西该去哪个文档找**。
> 目录按**阅读时序**排序（理解 → 操作 → 决策 → 学习 → 规划 → 参考 → 记录 → 产出 → 归档），数字前缀即顺序，`ls` 自然排序。
> 维护规则：新增文档必须更新本索引；文档头部带 front-matter（功能编号/状态/最后更新）。

---

## 一、30 秒定位速查（按场景找文档）

| 你想…… | 去这里 |
|--------|--------|
| 判断一个新功能**该不该做**（裁判依据） | [`00-vision/03-principles.md`](00-vision/03-principles.md) |
| 了解项目**为什么存在、要去哪** | [`00-vision/01-vision.md`](00-vision/01-vision.md) |
| 看项目**现在处于哪个阶段、目标是什么** | [`00-vision/02-goals.md`](00-vision/02-goals.md) |
| 了解**系统整体怎么搭的**（技术架构） | [`01-architecture/overview.md`](01-architecture/overview.md) |
| 理解/修改**某个功能**（需求+设计+使用） | `02-features/`（[001](02-features/001-ai-provider-config.md) AI提供方 / [002](02-features/002-ai-models-list.md) 模型 / [003](02-features/003-api-server-settings.md) API服务器 / [004](02-features/004-provider-models-integration.md) 网关集成 / [005](02-features/005-todo-tracker.md) 待办 / [006](02-features/006-background-image.md) 背景图 / [007](02-features/007-background-visibility-opacity.md) 透明度 / [008](02-features/008-tray-service-autoupdate.md) 托盘自更新 / [009](02-features/009-web-port-token-security.md) 端口令牌 / [010](02-features/010-chat-session-aggregation.md) 会话聚合 / [011](02-features/011-multimodal-image-cache.md) 多模态缓存 / [027](02-features/027-cordis-kernel.md) Cordis内核 / [100](02-features/100-secret-encryption.md) 密钥加密） |
| 新功能**设计该遵循什么通用模式** | [`03-design/patterns.md`](03-design/patterns.md)（核心模式已下沉到具体功能文档） |
| 写代码前**必须符合什么规范**（命名/契约/错误码/提交） | [`04-standards/engineering.md`](04-standards/engineering.md)（另有 [`04-standards/doc-reverse-sync-sop.md`](04-standards/doc-reverse-sync-sop.md) 文档反向同步 SOP） |
| 做一件**具体的事**（操作步骤 SOP） | [`05-guides/add-ai-provider.md`](05-guides/add-ai-provider.md)（添加提供方/拉模型/加密/改端口/令牌） |
| 做任何事时**查流程/标准/工具/验证**（通用操作手册） | [`05-guides/software-engineering-lifecycle-manual.md`](05-guides/software-engineering-lifecycle-manual.md)（SEMS V1.3：系统设计方案 12 章 + 迭代方法论 6 章 + AI 迭代工程 MCP 设计 7 章（含实现路线图）+ 操作手册全量版（S01–S20 含流程图/快速参考卡/文档大全 42 份）+ 34 个文档模板 + 25 条反模式清单 + 术语表 38 条 + 填写示例 + 手册治理规则） |
| 做**跨功能技术选型/调研**（要不要换数据库、引新框架） | `06-research/`（[001 deepseek-harness 插件化调研](06-research/001-deepseek-harness-plugin-architecture.md)；单功能调研在 `specs/NNN-*/research.md`） |
| 想了解**当初为什么这么选**（决策理由 ADR） | `07-decisions/`（[001 Cordis 内核重构决策](07-decisions/001-cordis-kernel-architecture.md)；[「审慎不做」决策台账](07-decisions/not-taken-decisions.md) 记录明确不做/缓做的选择） |
| 涉及**密钥/权限/敏感数据**的操作 | [`08-security/baseline.md`](08-security/baseline.md) |
| **部署/发布/运维**服务器 | [`09-operations/deployment.md`](09-operations/deployment.md) |
| 写测试前了解**测试策略与铁律** | [`10-testing/strategy.md`](10-testing/strategy.md) |
| 遇到**报错/异常**想快速解决 | [`11-troubleshooting/index.md`](11-troubleshooting/index.md) |
| 有**高频疑问**想快速查答 | [`12-faq/index.md`](12-faq/index.md) |
| 遇到**不懂的术语** | [`13-glossary/terms.md`](13-glossary/terms.md) |
| **第一次接触项目**（环境搭建/上手路径） | [`14-onboarding/getting-started.md`](14-onboarding/getting-started.md) |
| 决定**下一步做什么**（排期/backlog） | [`15-roadmap/index.md`](15-roadmap/index.md)（活队列在 TODO.md；Cordis 内核改造见 [`15-roadmap/plugin-architecture.md`](15-roadmap/plugin-architecture.md)） |
| 查**接口契约/字段**（快速速查） | [`16-reference/api.md`](16-reference/api.md)（API 端点）、[`16-reference/data-model.md`](16-reference/data-model.md)（数据模型/实体字段）、[`16-reference/configuration.md`](16-reference/configuration.md)（配置项）、[`16-reference/backend-services.md`](16-reference/backend-services.md)（后端服务职责）、[`16-reference/frontend-architecture.md`](16-reference/frontend-architecture.md)（前端结构） |
| 看**版本演进/变更摘要** | [`17-changelog/index.md`](17-changelog/index.md) |
| **要写新文档**（格式模板） | `18-templates/feature.md` |
| 找**已废弃的历史文档** | `19-archive/`（暂无） |

---

## 二、各目录/文档说明（四问格式）

> 每个目录统一按四问阐述：
> - **做什么**：本目录产出/涵盖的内容
> - **不做什么**：明确排除的内容（防止放错地方）
> - **解释什么**：它能回答的问题
> - **不解释什么**：它不回答的问题 + 该去哪找答案

### 00-vision/ — 北极星（愿景/目标/原则/方向）

| 问题 | 说明 |
|------|------|
| **做什么** | 项目为什么存在、要去哪、什么做/什么不做——四个文件：`01-vision.md`（愿景）、`02-goals.md`（分阶段目标）、`03-principles.md`（原则：必做 P1–P8 / 红线 N1–N5）、`04-direction.md`（演进方向 + 优先级决策准则） |
| **不做什么** | 不写具体功能的设计、不排工期、不记录实现细节、不写代码 |
| **解释什么** | 功能取舍的**裁判依据**：新功能提案是否符合愿景与原则、当前阶段目标是什么 |
| **不解释什么** | 不解释"某功能怎么做"→ 去 `02-features/`；不解释"下一步具体排什么"→ 去 `15-roadmap/` |
| **何时读** | 任何决策前、新功能提案评审时；平时不必读 |

### 01-architecture/ — 架构总览

| 问题 | 说明 |
|------|------|
| **做什么** | 系统分层、模块关系、技术栈选型、核心数据流/请求链路的**全景图**（Backend/Frontend/插件体系如何协作） |
| **不做什么** | 不深入某个具体功能的内部实现、不列 API 接口契约、不写操作步骤 |
| **解释什么** | 系统"长什么样、为什么这么分层、模块之间怎么通信" |
| **不解释什么** | 不解释具体功能细节 → `02-features/`；不解释代码级实现 → `openwiki/`（自动生成） |
| **何时读** | 新接手项目、做架构级变更、排障时理解全局 |

### 02-features/ — 功能需求与设计

| 问题 | 说明 |
|------|------|
| **做什么** | 每个功能的完整档案：背景、目标、设计选型、实现位置、使用指南、注意事项、测试覆盖。文件命名 `<编号>-<功能名>.md`（001–099 与 specs 同号；100+ 为无 spec 的独立文档） |
| **不做什么** | 不写跨功能通用规范、不写排期、不写操作 SOP |
| **解释什么** | "某个功能做成什么样、为什么这么设计、怎么用、有什么坑" |
| **不解释什么** | 不解释通用设计模式 → `03-design/`；不解释工程规范 → `04-standards/`；不解释全局架构 → `01-architecture/` |
| **何时读** | 修改/维护某个功能前必读；新功能实现后在此沉淀 |

### 03-design/ — 设计规范

| 问题 | 说明 |
|------|------|
| **做什么** | 跨功能共享的**设计模式与约定**：UI 规范、交互模式、接口设计范式、通用组件方案 |
| **不做什么** | 不针对单个功能（那是 `02-features/`）、不写代码风格类硬规则（那是 `04-standards/`） |
| **解释什么** | "多个功能共同遵守的设计准则是什么" |
| **不解释什么** | 不解释某个功能的定制实现 → `02-features/` |
| **何时读** | 设计新功能、做方案评审时对照 |

### 04-standards/ — 工程规范

| 问题 | 说明 |
|------|------|
| **做什么** | 代码必须符合的**硬规则**：命名规范、代码风格、API 契约格式、错误码约定、提交规范、目录结构约定 |
| **不做什么** | 不写操作流程（那是 `05-guides/`）、不写设计模式（那是 `03-design/`）、不写"为什么"（那是 `07-decisions/`） |
| **解释什么** | "代码/接口/提交必须长成什么样" |
| **不解释什么** | 不解释为什么定这条规则 → `07-decisions/`（ADR 记录理由） |
| **何时读** | 写代码前、Review 时对照；与 AGENTS.md 分工：AGENTS 管"怎么干活"，standards 管"干成什么样" |

### 05-guides/ — 操作指南（SOP）

| 问题 | 说明 |
|------|------|
| **做什么** | 具体事情的**操作步骤**：如何添加 AI 提供者、如何加密一个密钥、如何发布版本；另含**团队级通用 SEMS 完整文档**（[`software-engineering-lifecycle-manual.md`](05-guides/software-engineering-lifecycle-manual.md)，SEMS-DOC-2026-001 整合 SEMS-OM-2026-001，V1.3，2026-08-24）：系统设计方案（12 章，含门禁验证手段/度量操作化）、迭代变更影响分析与回归验证方法论（6 章）、AI 迭代工程 MCP 设计（16 Tools/10 Resources/6 Prompts + 实现代码框架 + 实现状态路线图）、操作手册全量版（S01–S20 场景流程含 ASCII 流程图、快速参考卡、文档大全 42 份、术语表 38 条）、模板与清单库（34 个模板）、反模式清单（25 条）、填写示例集、手册治理规则 |
| **不做什么** | 不解释原理（那是 `02-features/` 设计）、不写规范（那是 `04-standards/`） |
| **解释什么** | "这件事一步一步怎么做"；通用场景"走什么流程、依据什么标准、用什么工具、产出什么、怎么验证" |
| **不解释什么** | 不解释"为什么这么做" → `07-decisions/` |
| **何时读** | 干活时照着做；不确定流程/标准/验证方式时先查通用操作手册 |

### 06-research/ — 跨功能调研

| 问题 | 说明 |
|------|------|
| **做什么** | 跨功能的**长期技术调研与选型对比**：如"是否换数据库/引入新框架/重构插件机制"。记录调研背景、候选方案对比（优劣势/证据）、初步倾向与待验证项 |
| **不做什么** | 不做单功能调研（那在 `specs/NNN-*/research.md`，speckit 产物）；不下最终决策（那是 `07-decisions/` 的活） |
| **解释什么** | "这个技术方向调研了什么、比了哪些方案、证据是什么、倾向哪个" |
| **不解释什么** | 不解释最终拍板理由 → `07-decisions/`（决策落地时把调研结论引过去）；不解释功能实现 → `02-features/` |
| **何时读** | 做技术选型、评估架构级变更前；调研定论后升级为 ADR 决策 |

### 07-decisions/ — 决策记录（ADR）

| 问题 | 说明 |
|------|------|
| **做什么** | 关键决策的档案：背景、可选方案、选择理由、后果。命名 `<编号>-<标题>.md` 从 001 递增；可引用 `06-research/` 的调研结论。**另设「审慎不做」台账 [`not-taken-decisions.md`](07-decisions/not-taken-decisions.md)**：登记明确**不做/缓做/否掉**的选择及触发条件，避免重复评估与前后矛盾 |
| **不做什么** | 不写操作步骤（那是 `05-guides/`）、不写当前状态说明 |
| **解释什么** | "当初为什么这么选、否掉了什么方案" |
| **不解释什么** | 不解释"现在怎么操作" → `05-guides/`；不解释"现在系统长什么样" → `01-architecture/` |
| **何时读** | 想推翻/质疑现有设计时、做类似决策参考历史时 |

### 08-security/ — 安全基线

| 问题 | 说明 |
|------|------|
| **做什么** | 安全底线：密钥管理与加密规范（如 AES-256-CBC 落库）、鉴权要求、敏感操作清单、安全 review 清单 |
| **不做什么** | 不写功能设计（那是 `02-features/`）、不写通用运维步骤（那是 `09-operations/`） |
| **解释什么** | "涉及密钥/权限/敏感数据时必须满足什么" |
| **不解释什么** | 不解释加密算法怎么用 → `02-features/100-secret-encryption.md` |
| **何时读** | 任何涉及密钥、鉴权、用户数据的功能开发/评审时 |

### 09-operations/ — 运维手册

| 问题 | 说明 |
|------|------|
| **做什么** | 部署、发布、服务管理（托盘/Windows 服务）、日志、故障恢复、备份还原 |
| **不做什么** | 不写开发指南、不写功能设计 |
| **解释什么** | "生产/运行环境怎么维护" |
| **不解释什么** | 不解释开发期操作 → `14-onboarding/` |
| **何时读** | 上线、发布、服务器出问题时 |

### 10-testing/ — 测试指南

| 问题 | 说明 |
|------|------|
| **做什么** | 测试策略：单测/集成/e2e 的分工与铁律（如 e2e 绝不 mock、对接真实后端）、运行命令速查、覆盖约定 |
| **不做什么** | 不记录某次测试的运行结果（结果在 CI/日志）、不写功能需求 |
| **解释什么** | "测试应该怎么组织、怎么跑、什么算通过" |
| **不解释什么** | 不解释某个功能测什么 → 该功能在 `02-features/` 的"测试覆盖"章节 |
| **何时读** | 写测试前、测试失败判断是否为环境问题时 |

### 11-troubleshooting/ — 排障手册

| 问题 | 说明 |
|------|------|
| **做什么** | 常见问题的排查记录：**错误现象 → 根因 → 解法**，一条一例 |
| **不做什么** | 不解释正常流程、不写预防性规范（那是 `04-standards/`） |
| **解释什么** | "这个报错/现象是怎么回事、怎么解决" |
| **不解释什么** | 不解释"没出问题时的标准做法" → `05-guides/` |
| **何时读** | 遇到报错先来这里搜；没找到再现场排查并**把新案例补进来** |

### 12-faq/ — 常见问题

| 问题 | 说明 |
|------|------|
| **做什么** | 高频问答（一句话问、几句话答），覆盖概念类疑问 |
| **不做什么** | 不写详细排查步骤（那是 `11-troubleshooting/`）、不写长文 |
| **解释什么** | "这个项目里 X 是什么意思/为什么这样" |
| **不解释什么** | 不解释报错解法 → `11-troubleshooting/` |
| **何时读** | 快速查答，不深入 |

### 13-glossary/ — 术语表

| 问题 | 说明 |
|------|------|
| **做什么** | 领域术语定义：AI Provider、chatModelId、插件、token、ADR 等 |
| **不做什么** | 不展开功能细节、不写用法 |
| **解释什么** | "这个术语指的是什么" |
| **不解释什么** | 不解释术语背后的功能实现 → `02-features/` |
| **何时读** | 对话/文档中遇到不懂的词 |

### 14-onboarding/ — 上手指南

| 问题 | 说明 |
|------|------|
| **做什么** | 从零上手路径：环境要求（Node/pnpm/.NET）、启动步骤（端口 7102/7002）、先读哪些文档、常见命令 |
| **不做什么** | 不重复其他目录的内容，只**指路**（链接到各处） |
| **解释什么** | "第一次接触这个项目，怎么跑起来、看什么" |
| **不解释什么** | 不解释深度内容 → 按链接去对应目录 |
| **何时读** | 新会话/新成员第一次开始工作 |

### 15-roadmap/ — 路线图（执行层）

| 问题 | 说明 |
|------|------|
| **做什么** | 下一步做什么：已排期任务、backlog、优先级排序；新功能提案的评审结果落在这里 |
| **不做什么** | 不写"为什么做"（那是 `00-vision/`）、不写实现细节 |
| **解释什么** | "接下来按什么顺序做什么" |
| **不解释什么** | 不解释"该不该做" → `00-vision/03-principles.md`（裁判）；不解释"做成什么样" → `02-features/` |
| **何时读** | 决定下一步、认领任务时 |

### 16-reference/ — 接口参考

| 问题 | 说明 |
|------|------|
| **做什么** | 接口/字段/常量的**人工提炼速查**：常用 API 契约（[`api.md`](16-reference/api.md)）、数据模型与实体字段（[`data-model.md`](16-reference/data-model.md)）、配置项结构（[`configuration.md`](16-reference/configuration.md)）、后端服务职责与依赖（[`backend-services.md`](16-reference/backend-services.md)）、前端组件/路由/store 结构（[`frontend-architecture.md`](16-reference/frontend-architecture.md)） |
| **不做什么** | 不追求全量覆盖（全量在 `openwiki/`，CI 自动生成，勿手编） |
| **解释什么** | "这个接口怎么调、字段怎么传、服务什么职责、前端什么结构" |
| **不解释什么** | 不解释代码实现 → `openwiki/`；不解释功能设计 → `02-features/` |
| **何时读** | 前端/测试写调用时快速查；新功能开发前了解现有结构 |

### 17-changelog/ — 变更记录

| 问题 | 说明 |
|------|------|
| **做什么** | 版本/阶段的**变更摘要**：每次重要变更记一条（是什么、为什么、影响），是 git log 的人工提炼 |
| **不做什么** | 不写代码级 diff（git 里有）、不逐条记琐碎提交 |
| **解释什么** | "最近项目发生了什么变化、为什么" |
| **不解释什么** | 不解释细节 → 看对应 commit / `02-features/` |
| **何时读** | 回顾演进、写总结时 |

### 18-templates/ — 文档模板

| 问题 | 说明 |
|------|------|
| **做什么** | 各类文档的标准模板（当前有 `feature.md` 功能文档模板），新文档照填保证格式统一 |
| **不做什么** | 不存放实际内容（那是各目录的活） |
| **解释什么** | "新文档应该长什么结构" |
| **不解释什么** | 不解释内容怎么写 → 按模板章节填 |
| **何时读** | 要产出新文档时 |

### 19-archive/ — 归档区

| 问题 | 说明 |
|------|------|
| **做什么** | 废弃/过期文档的**留档**：从正式位置移入，保留历史上下文 |
| **不做什么** | 不删除、不更新、不作为当前依据 |
| **解释什么** | "历史上曾怎么做的" |
| **不解释什么** | 不解释当前状态（当前依据在对应正式目录） |
| **何时读** | 极少：追溯历史时；当前内容一律以正式目录为准 |

---

## 三、排序逻辑（为什么这么排）

按**阅读时序**组织：**理解 → 操作 → 决策 → 学习 → 规划 → 参考 → 记录 → 产出 → 归档**。

| 层 | 目录 | 什么时候看 |
|----|------|-----------|
| 0 入口 | `README.md` | 永远第一站 |
| 1 理解 | `00-vision` → `01-architecture` → `02-features` → `03-design` | 新接手、要改东西前 |
| 2 操作 | `04-standards` → `05-guides` | 干活、走流程时 |
| 3 决策 | `06-research` → `07-decisions` | 做选型、追溯决策时（先调研后决策） |
| 4 合规运维 | `08-security` → `09-operations` | 涉及安全/上线时 |
| 5 学习 | `10-testing` → `11-troubleshooting` → `12-faq` → `13-glossary` → `14-onboarding` | 遇到问题、带新人时 |
| 6 规划 | `15-roadmap` | 决定下一步做什么时 |
| 7 参考 | `16-reference` | 查接口、查字段时 |
| 8 记录 | `17-changelog` | 看版本演进时 |
| 9 产出 | `18-templates` | 要写新文档时 |
| 10 归档 | `19-archive` | 追溯历史时 |

**决策链**：`06-research`（调研证据）→ `07-decisions`（拍板理由）→ `15-roadmap`（排期）→ `02-features`（落地）。

---

## 四、与项目其他文档体系的分工

| 体系 | 管什么 | 生命周期 |
|------|--------|----------|
| `docs/`（本目录） | 愿景、设计、规范、指南、调研、决策等**人工沉淀知识** | 长期维护 |
| `specs/NNN-*/` | 开发中功能的规格/计划/任务（speckit SDD），内含**单功能** research.md | 功能开发期间 |
| `openwiki/` | 自动生成的架构/代码文档（全量、代码级） | CI 自动刷新，**不手编** |
| `.forgeself/memory/` | 按天工作记录 + 长期笔记 | 会话级沉淀 |

**一句话分工**：`docs/` 管"应该是什么"，`specs/` 管"正在做什么"，`openwiki/` 管"代码实际是什么"，`memory/` 管"每天发生了什么"。

---

## 五、文档编号规则

- 功能文档：`docs/02-features/<NNN>-<功能名>.md`；`001–099` 与 specs 同号，`100+` 为无 spec 的独立文档（如 `100-secret-encryption.md`）；
- 决策文档：`docs/07-decisions/<NNN>-<标题>.md`，从 001 递增；
- 调研文档：`docs/06-research/<NNN>-<主题>.md`，从 001 递增；单功能调研仍在 `specs/NNN-*/research.md`，不进本目录；
- 北极星文档：`docs/00-vision/01-vision.md` ~ `04-direction.md`，序号即阅读顺序；
- 其余目录内文件按需命名，序号 01 起；
- 编号是**身份标识**不是排序手段，排序由本 README 索引表决定。

---

## 六、目录治理约定

1. **默认不新增目录**：新内容一律放进现有 19 个目录（`00-vision` ~ `19-archive`）；
2. **新增目录的唯一条件**：出现"现有目录都装不下、且不属于 archive"的内容类型——例如本次 `06-research/`（跨功能调研，specs 单功能 research 装不下）；
3. 新增目录须**插在语义相邻位置**（如调研紧邻决策），后续编号整体顺延，并同步更新本 README 的速查表、四问说明、排序逻辑与编号规则；
4. 已预留 `19+` 号段空间，扩展不破坏现有目录。

## 已归档内容

| 功能编号 | 文档 | 状态 |
|----------|------|------|
| 001 | [`02-features/001-ai-provider-config.md`](02-features/001-ai-provider-config.md) | 已实现 |
| 002 | [`02-features/002-ai-models-list.md`](02-features/002-ai-models-list.md) | 已实现 |
| 003 | [`02-features/003-api-server-settings.md`](02-features/003-api-server-settings.md) | 已实现 |
| 004 | [`02-features/004-provider-models-integration.md`](02-features/004-provider-models-integration.md) | 已实现 |
| 005 | [`02-features/005-todo-tracker.md`](02-features/005-todo-tracker.md) | 已实现 |
| 006 | [`02-features/006-background-image.md`](02-features/006-background-image.md) | 已实现 |
| 007 | [`02-features/007-background-visibility-opacity.md`](02-features/007-background-visibility-opacity.md) | 已实现 |
| 008 | [`02-features/008-tray-service-autoupdate.md`](02-features/008-tray-service-autoupdate.md) | 已实现 |
| 009 | [`02-features/009-web-port-token-security.md`](02-features/009-web-port-token-security.md) | 已实现 |
| 010 | [`02-features/010-chat-session-aggregation.md`](02-features/010-chat-session-aggregation.md) | 已实现（已提交 9d84e48） |
| 011 | [`02-features/011-multimodal-image-cache.md`](02-features/011-multimodal-image-cache.md) | 已实现 |
| 012 | [`02-features/012-code-snippets.md`](02-features/012-code-snippets.md) | 已实现（ScriptRunner 插件） |
| 013 | [`02-features/013-workflow-engine.md`](02-features/013-workflow-engine.md) | 已实现 |
| 014 | [`02-features/014-memory-system.md`](02-features/014-memory-system.md) | 已实现 |
| 015 | [`02-features/015-system-monitor.md`](02-features/015-system-monitor.md) | 已实现 |
| 016 | [`02-features/016-dev-tools.md`](02-features/016-dev-tools.md) | 已实现 |
| 017 | [`02-features/017-file-tools.md`](02-features/017-file-tools.md) | 已实现 |
| 018 | [`02-features/018-text-tools.md`](02-features/018-text-tools.md) | 已实现 |
| 019 | [`02-features/019-script-runner.md`](02-features/019-script-runner.md) | 已实现 |
| 020 | [`02-features/020-quick-links.md`](02-features/020-quick-links.md) | 已实现 |
| 021 | [`02-features/021-ai-agent.md`](02-features/021-ai-agent.md) | 已实现 |
| 022 | [`02-features/022-mcp-tools.md`](02-features/022-mcp-tools.md) | 已实现（核心控制器） |
| 023 | [`02-features/023-skills.md`](02-features/023-skills.md) | 已实现（核心控制器） |
| 024 | [`02-features/024-usage-stats.md`](02-features/024-usage-stats.md) | 已实现（核心服务，无独立控制器） |
| 025 | [`02-features/025-user-profile.md`](02-features/025-user-profile.md) | 前端已实现；后端暂无独立模块（缺口） |
| 026 | [`02-features/026-plugin-marketplace.md`](02-features/026-plugin-marketplace.md) | 已实现 |
| 027 | [`02-features/027-cordis-kernel.md`](02-features/027-cordis-kernel.md) | 已实现（内核/契约/插件自注册/热更新/前端清单驱动；剩余项见档案） |
| 100 | [`02-features/100-secret-encryption.md`](02-features/100-secret-encryption.md) | 已实现 |

> 编号说明：001–009 与 `specs/` 同号；010/011 为无独立 spec 的能力特性（会话聚合、多模态缓存）；012–021 为插件化功能（P0）；022–026 为核心/体系功能（P1）；027 为 Cordis 内核（一切皆插件运行时，已实现，剩余项见档案）；100 为早期核心安全基础设施（密钥加密）。025 为后端缺口文档，如实标注。
> 另有 `docs/chat-record-session-redesign.md` 为 010 会话聚合的**根因分析稿**（ChatRecord→ChatTurn 重构方案），已随 010 落地，其顶部"落地状态"段指向 `010-chat-session-aggregation.md`。
