# Agent 工作流与工程规则规范（AGENTS.md 详细版 + 项目不变规则归档）

> 状态：已实施（2026-09-24 分层落地：AGENTS.md 瘦身为每次必守版，本文件承接详细版；原 `.forgeself/memory/MEMORY.md` 规则归档于此）
> 最后更新：2026-09-24
> 维护：新增/修改项目规则 → 更新本文对应小节；仅当规则升级为「每次必守」时同步回写 AGENTS.md。

> 本文档是 OpenForgeSelf（铸己匣）项目 **Agent 工作规则的完整规范**：
> - **Part A**：AGENTS.md 的详细版——AGENTS.md 只列「每次必守」要点，本文承载触发式细节（技能体系、验证决策表、迭代流程、文档工作流操作细则、汇报模板等），AGENTS.md 一律引用本文。
> - **Part B**：项目不变工程规则与踩坑规律——原 `.forgeself/memory/MEMORY.md` 的规则内容已归档于此（2026-09-24 起，`.forgeself` 被 git 忽略不入库，规则必须随 `docs/` 入库可追溯）。
> - **维护规则**：新增/修改项目规则 → 更新本文对应小节；仅当规则升级为「每次必守」时同步回写 AGENTS.md。MEMORY.md 不再承载不变规则，只存会话级索引与未沉淀的临时规律。

---

# Part A — 工作流细节（AGENTS.md 详细版）

## A1 技能体系（对应 AGENTS.md §2.4 详细）

技能位于 `.agents/skills/<name>/SKILL.md`。**涉及宿主 / 插件的任务，动手前必须先读对应技能**——历史上不止一次因为没读技能而漏掉发布、漏掉插件层 e2e，最后靠临时脚本自验就宣称完成。

| 技能 | 何时用 | 关键约束 |
|------|--------|----------|
| `plugin-development` | **插件任务总入口**：新建插件、把宿主页面迁移成独立插件、改完插件不知还要做什么 | 改完 = 门禁 + 插件 e2e + 发布 + 浏览器走查，四步缺一不算完成；完成后复盘回写技能 |
| `plugin-feasibility-study` | **新建插件第一步**（先于 `plugin-development`）：行业调研 → 可行性报告 → 设计方案 → **命名** → 决策拍板 | 不许直接开写代码；命名在功能定稿之后，须过「名实相符三问」 |
| `plugin-frontend-scaffold` | 从 AIAgent 模板生成插件 `web/` 前端骨架 | 产物入口固定 `web/dist/index.js`，导出名须等于 `views[0]` |
| `plugin-publish-verify` | 发布单插件到运行中的 publish 宿主 + 验证 | 宿主必须跑 `publish/`；活动插件目录只放插件自身 DLL；`plugin.json` 最后拷 |
| `e2e-testing` | 插件层 e2e（`e2e/plugins/<id>/<id>.spec.ts`）+ 截图读图 | 零 mock；禁止用一次性临时脚本代替 |
| `architecture-design` | 影响面较大的架构/设计决策 | 先查依据（调研/ADR/既有设计），禁止脱离依据自作设计 |

**选型顺序**：先判断「是不是插件任务」→ 是则先读 `plugin-development` → 它会在正文里把
你转派到 `plugin-feasibility-study`（**新建插件**时）/ `architecture-design`（涉及契约与内核接缝时）/
`plugin-frontend-scaffold` / `plugin-publish-verify` / `e2e-testing`。

**职责边界（勿混）**：立项 = 做什么 / 叫什么 / 做多大；架构 = 怎么接才合规（契约入 `Abstractions`、
事件族、能力供给选型）；开发 = 怎么落地；验证 = 怎么证明。遇到「契约放哪层」转 `architecture-design` 出 ADR。

**新建插件的硬顺序**：`plugin-feasibility-study`（调研→可行性→设计→命名→拍板）→ 用户确认 →
`plugin-development`（实现→门禁→e2e→发布→走查）→ **复盘回写技能**。跳过立项直接写代码的，
历史教训是插件名与职责错位（如把「管理其它 agent」叫 `CliAgent`，而 CLI 只是交互口之一）。

**新增技能必须同步登记到 AGENTS.md §2.4**（技能不在表里 = 等于不存在，后续会话必然再次漏读）。

**技能缺失策略**：任务拆解后先从技能目录挑流程执行；无对应流程的**先创建技能流程**（SKILL.md 沉淀并登记 §2.4）→ 执行 → 持续迭代完善；同类任务重复出现时，先选既有技能流程。

## A2 规划（对应 AGENTS.md §3 详细）

### 任务分解
收到任务后先分解为可独立验证的步骤：

```
任务 → [步骤1: 改什么文件, 预期什么结果]
     → [步骤2: ...]
     → [步骤N: 运行 Verify 全部通过]
```

每个步骤应满足：改完后可以立即运行某个检查命令确认对错。

### 影响评估
规划时回答：
- 本次变更影响哪些文件/模块？
- 是否涉及公共接口（API 契约、组件 props、路由）？若是，需额外谨慎。
- 是否涉及数据库迁移？若是，必须可回滚。
- 是否涉及设计稿对齐？若是，先截图确认设计稿当前状态。

### 风险分级
| 级别 | 场景 | 处理方式 |
|------|------|----------|
| 低 | 组件内部样式/逻辑调整 | 正常执行 |
| 中 | 公共组件/API 接口变更 | 执行前确认影响范围，执行后全量 Verify |
| 高 | 数据库迁移/依赖升级/架构调整 | 升级给人确认后再执行 |

## A3 编码规范（对应 AGENTS.md §4 详细）

### 安全约束
- **变更前备份**：修改用户文件前，确认项目在 Git 管理下（可 `git diff` 回溯）。若非 Git 管理，先备份原文件。
- **禁止永久删除**：不 `rm`/`del` 用户文件，需要删除时移入回收站或 `.trash/`。
- **敏感路径禁触**：`.env`、`appsettings.Production.json`、含密钥的配置文件 — 只读，不修改。
- **变更范围限制**：单次执行修改文件数不超过任务所需，不顺手重构无关代码。

### 前端
- 组件拆分：每个面板/功能区独立 `.vue` 文件，不允许大文件堆多面板逻辑
- 样式：使用 Element Plus 组件 + Tailwind 布局类，颜色只走 `--el-*` 变量
- **Element Plus 组件禁止显式 `import { ElXxx } from 'element-plus'`**（type 导入如 `FormInstance`/`FormRules` 除外）：本项目组件样式依赖 unplugin-vue-components 按需注入，显式导入会绕过自动解析导致组件无样式（已验证：TodoEditDialog 显式导入 ElDialog 致弹窗背景透明/无圆角）。统一在模板中使用 `<ElXxx>`，由 unplugin-vue-components 自动解析并注入样式
- 类型：所有新代码必须有 TypeScript 类型，不用 `any`
- 测试（TDD 优先）：新增逻辑先写 vitest 测试定义预期行为，再写实现使测试通过；修改逻辑先补/改测试覆盖新行为，再改实现
- 全屏背景图：用固定定位 `<img>` 元素（`position:fixed; inset:0; object-fit:cover; z-index:0; pointer-events:none`）+ 内容层 `z-index` 叠放，**不要**用 CSS `background-image: url(外链)`。本环境外链背景图不渲染（已验证：手动注入 `!important` 后截图仍纯白），`<img>` 方案可稳定显示

### 后端
- 遵循现有 Controllers/Services/Entities 分层
- 新插件按 `Plugins/` 目录 + `plugin.json` 模式注册
- 异步方法统一 `async/await`，不 `.Result` 阻塞
- 测试（TDD 优先）：新增/修改业务逻辑先写 xUnit 测试，再写实现

### 执行记录
每步执行后记录：改了什么、运行了什么命令、结果是什么。
失败时记录错误信息全文（不截断），作为 Iterate 环节的输入。

## A4 验证（对应 AGENTS.md §5 详细）

### 测试方式总览（项目唯一测试体系）
本项目有完整、可重复的测试体系，**禁止另起炉灶**。任何验证需求都从下表选对应入口，
**严禁**在 `temp/` 下手写一次性 `.cjs`（或散落脚本）做截图 / 视觉校验 / 浏览器驱动——
这类脚本无版本控制、不可重复、易腐烂。

| 层级 | 工具 | 入口命令 | 适用 | 归口 |
|------|------|----------|------|------|
| 前端单测 | vitest | `cd ForgeSelf.Web && pnpm run test` | 组件渲染 / 纯函数 / 关键交互逻辑 | `ForgeSelf.Web/src/**/*.spec.ts` |
| 前端 e2e | Playwright | `cd ForgeSelf.Web && pnpm run test:e2e` | 前后端集成、真实渲染、视觉检查 | `e2e-testing` 技能 + `ForgeSelf.Web/e2e/**` |
| 后端单测/集成 | xUnit | `cd ForgeSelf.Api.Tests && dotnet test` | 服务 / 实体 / 工具逻辑 | `ForgeSelf.Api.Tests/**` |
| 后端 HTTP 冒烟 | PowerShell | `pwsh test_all.ps1`（真实后端 `:7102`） | 已起宿主的接口联通性快检 | `test_all.ps1`（非 cjs，允许保留） |

**需求 → 该跑什么（对号入座，不要临场发明）：**

| 我想验证… | 跑这个（正规入口） | 禁止当成验证手段 |
|-----------|--------------------|------------------|
| 前端组件渲染 / 纯函数 / 关键交互逻辑 | `cd ForgeSelf.Web && pnpm run test`（vitest） | `temp/*.cjs` |
| 页面真实渲染 / 端到端交互 / 视觉检查（图标·间距·颜色·留白·对齐·溢出） | `cd ForgeSelf.Web && pnpm run test:e2e`（Playwright，走 `e2e-testing` 技能） | `temp/shots_*.cjs`、`temp/verify_*.cjs` |
| 后端服务 / 实体 / 工具逻辑 | `cd ForgeSelf.Api.Tests && dotnet test`（xUnit） | — |
| 已起宿主的接口联通性快检 | `pwsh test_all.ps1`（真实后端 `:7102`） | `temp/diag*.cjs` |
| 浏览器驱动 / 多步点击流程 | 写成 Playwright e2e 用例（`e2e/**/*.spec.ts`） | `temp/browser_drive/*.cjs` |
| 只想临时探一下 DOM / 网络 / 接口形状 | 一次性 node 探针（用完即删、**不提交、不留存 `temp/`**），结论沉淀为上述测试 | 把探针本身当验证结论 |
| 运行态宿主走查 / 拿真实 token / 验产物 DLL | 仓内工具 `scripts/get-forge-token.cjs` / `scripts/probe-dll-string.cjs`（见 B2） | 每次现写一次性脚本 |

**铁律：**
- 验证 / 截图 / 浏览器驱动类任务 → 写成 **Playwright e2e 用例**（`ForgeSelf.Web/e2e/**/*.spec.ts`），走 `e2e-testing` 技能 SOP（globalSetup 自动构建宿主 + 起 publish 宿主 + 解密真实 token，零 mock）。
- 纯逻辑 / 组件行为 → 写成 **vitest 单测**（`src/**/*.spec.ts`）。
- 一次性 `.cjs` 脚本只可作「探索期临时探针」，**不得作为验证手段提交或长期依赖**；结论必须沉淀为上述可重复测试，探针用完即删、不进 `temp/` 留存。
- e2e 视觉检查（图标 / 间距 / 颜色 / 留白 / 对齐 / 溢出）按 `e2e-testing` 技能 Level 3 清单读图核对，截图固定落 `ForgeSelf.Web/screenshots/e2e/<插件id>/`，**不用** `temp/*.cjs` 截图。

### 前端验证（修改 `ForgeSelf.Web/` 后必须执行）
```bash
cd ForgeSelf.Web
pnpm run check    # vue-tsc --noEmit && eslint（类型检查 + 代码规范）
pnpm run test     # vitest（单元测试）
```
判定：`check` 失败 → 先修类型错误再修 lint；`test` 失败 → 修测试或补测试（不允许跳过）；两项都过 → 前端验证通过。

### 后端验证（修改 `ForgeSelf.Api/` 后必须执行）
```bash
cd ForgeSelf.Api
dotnet build
```
后端测试（涉及逻辑变更时必须执行）：
```bash
cd ForgeSelf.Api.Tests
dotnet test
```
判定：构建失败 → 修编译错误；测试失败 → 修代码或修测试（不允许删除测试来"通过"）。

### 设计对齐验证（涉及 UI 变更时）
- 打开对应设计稿（`forgeself-design/pages/xxx.html`）对比
- 条件允许时截图对比（Playwright screenshot）
- 布局/间距/颜色须与设计稿一致，组件交互行为须与 spec 描述一致

### 验证严重级别
| 级别 | 含义 | 处理 |
|------|------|------|
| **Critical** | 构建失败、测试失败、类型错误 | 必须修复，否则任务不完成 |
| **Warning** | ESLint warning、覆盖率低于阈值 | 记录到 TODO.md，不阻断当前任务 |
| **Info** | 代码风格建议、可选优化 | 记录但不处理 |

### 脚本速查
| 命令 | 作用 | 目录 |
|------|------|------|
| `pnpm run check` | 类型检查 + ESLint 联合校验 | Frontend |
| `pnpm run lint` | 仅 ESLint | Frontend |
| `pnpm run lint:fix` | ESLint 自动修复 | Frontend |
| `pnpm run type-check` | 仅 vue-tsc 类型检查 | Frontend |
| `pnpm run test` | 单元测试（vitest） | Frontend |
| `pnpm run test:e2e` | 端到端（Playwright 真实前后端，零 mock） | Frontend |
| `pnpm run test:e2e:ui` | e2e 调试 UI | Frontend |
| `pnpm run test:e2e:published` | 针对已发布宿主的 e2e | Frontend |
| `pnpm run build` | 完整构建（含类型检查） | Frontend |
| `dotnet build` | 后端构建 | Backend |
| `dotnet test` | 后端测试 | Backend.Tests |

## A5 迭代控制（对应 AGENTS.md §6 详细）

### 验证失败时的处理流程
```
Verify 失败
  ├─ 第 1 次失败 → 分析错误，调整实现，重新 Execute → Verify
  ├─ 第 2 次失败 → 换策略（不同实现路径），重新 Execute → Verify
  ├─ 第 3 次失败 → 回滚到最近通过状态，升级给人
  └─ 任何时候出现非预期副作用 → 立即回滚，升级给人
```

### Bug 修复 TDD 流程（禁止直接改实现代码）
```
1. Red   — 写一个测试精确复现问题（此时测试必须失败）
2. 确认  — 运行该测试，确认红灯（证明测试有效）
3. Green — 最小化修复实现代码，只改让测试通过所必需的部分
4. 确认  — 运行该测试，确认绿灯
5. 回归  — 运行完整 Verify（pnpm run test / dotnet test），确认无副作用
```
要点：第 1 步的测试即永久回归保护，不允许修复后删除；若无法写出复现测试（如纯 UI 视觉问题），用截图对比替代但须记录原因；修复范围严格限定，不顺手重构。

### 回滚策略
- **首选**：`git checkout -- <file>` 或 `git stash` 恢复到变更前
- **次选**：若已 commit，`git revert` 生成反向提交
- **底线**：从备份恢复（执行前已备份的情况）
- 回滚后必须重新运行 Verify 确认恢复成功

### 升级规则（必须停止循环、升级给人）
- 连续 3 次验证失败且无新思路
- 涉及高风险操作（数据库迁移、API 契约破坏性变更、主依赖大版本升级）
- 错误信息指向环境问题（非代码问题）
- 任务描述存在歧义，两种理解会导致不同实现
- 需要安装新依赖或修改构建配置

升级时提供：已尝试的方案、失败原因、建议的下一步。

### 循环终止条件
- **正常终止**：Verify 全部通过 → 进入 Persist
- **异常终止**：触发升级规则 → 报告给人
- **超时终止**：单任务迭代超过 5 轮仍无进展 → 升级

## A6 文档工作流（对应 AGENTS.md §7.5 详细）

> 目标：**TODO.md 永远是"今天和明天"的活队列**（不随迭代膨胀）；工作日记是"昨天"的归档；MEMORY 是"会话级索引"；docs 是"永远"的规则库。三者靠「检视回流」闭环，而非手动搬运维持。

### 文件职责（角色分工）
| 文件 | 角色 | 内容 | 写入时机 |
|------|------|------|----------|
| `TODO.md` | **队列**（今天/明天） | 🔄进行中 / ⬜待办（优先级 P1>P2>P3 + 来源引用）/ ⏸阻塞（记录不修） | 新输入解析出待办时；待办状态变化时；完成即移除 |
| `.forgeself/memory/YYYY-MM-DD.md` | **日志**（昨天） | 当天完成内容+验证结果、决策记录（为什么选 A）、遇到的问题与解决、输入原文与任务拆解、**「下一步」字段**（回流连接器） | 任务完成时追加当天文件 |
| `.forgeself/memory/MEMORY.md` | **会话级索引** | 项目规则索引（指向本文件）+ 未沉淀的临时规律 | 产生临时规律时；沉淀后迁入本文件 |
| `docs/04-standards/agent-workflow.md`（本文件） | **规则库**（永远，入库） | 工作流细节 + 项目不变工程规则与踩坑 | 产生可复用规律/规则变化时 |

### 强制顺序（带门禁）：Loop Engineering × 文档工作流（每轮任务循环，步骤为前置条件）
```
[0 输入到达] 用户消息
   │  ★ 立即写日志：当天工作日记「输入 N」条目（原文 + 任务拆解），不拖延
   ▼
[1 Goal]  解析目标 → TODO.md 生成待办项（🔄进行中/⬜待办，P1>P2>P3，来源:输入N）
   ▼
[2 Context] 收集上下文（spec / 设计稿 / 相关代码 / TODO 约束）
   ▼
[3 Plan]  规划步骤（拆分为可独立验证的子任务，写入输入 N 任务列表）
   ▼
[4 Execute] 边做边记 → 工作日记追加（完成内容、决策记录、遇到的问题与解决）
   ▼
[5 Verify] 验证门禁（pnpm run check / dotnet build / 测试）→ 失败转 [6 Iterate]
   ▼
[6 Iterate] 失败：分析→调整→重跑（第 3 次失败升级给人）；通过 → 继续
   ▼
[出口] 任务完成 → TODO 待办标记 ✅ 并移除；工作日记补全验证结果
   ▼
[提炼] 有无可复用规律？有 → MEMORY.md（会话级暂存）→ 沉淀为本文件规则 + 回写/新建技能流程（§2.4）；无 → 跳过
   ▼
[检视] 每日/每周：工作日记「下一步」→ 回流为 TODO 新待办；重复模式 → 提炼为本文件规则
   ▼
   └──────► 回到 [0 输入到达]（闭环）
```

### 操作规范
**① 新用户输入到达时**（每次收到用户消息，**第一步必做**）：
1. **立即写日志**：在当天工作日记 `.forgeself/memory/当天日期.md`（不存在则新建）写入「输入 N（日期）：<原文>」+ 任务拆解列表（`- [ ]` 子任务，倒序跟随；**完成一项勾选一项**，`- [x]`）
2. 在 TODO.md「⬜ 待办」区生成待办项，格式：`- [ ] <任务描述>（P<优先级>，来源:输入<N>）`
3. 开始执行时，把该待办移到「🔄 进行中」区

**② 任务执行过程中**：
- 边做边把「完成内容、验证结果、决策、问题」追加到当天工作记录 `.forgeself/memory/当天日期.md`
- 决策记录格式：`决策 → 背景 → 理由 → 准则`

**③ 任务完成时**（完成 = 代码闭环 + 文档同步 + 待办清除，三者缺一不可）：
1. 把当天工作记录追加完整（含验证结果、遇到的问题）
2. TODO.md 对应待办**直接移除**（完成即移除，不要只标 ✅/[x] 让它堆积；若该输入派生多个待办，逐个移除）
3. **同步相关文档**：若本次改动涉及功能/接口/架构，更新对应 spec、`docs/`、`README`，使文档与代码现状一致
4. 提炼：若有可复用规律（约定/踩坑/ADR）→ 暂存 MEMORY.md → 沉淀为本文件对应小节（入库）
5. **完善技能流程**：本次执行走通的流程若技能缺失或可优化，回写/新建对应技能（`.agents/skills/<name>/SKILL.md`）并登记 AGENTS.md §2.4；新形成的项目不变规范写入本文件或 AGENTS.md

**④ 每日/每周检视**（任务告一段落或收到"整理"指令时）：
1. 读取工作日记的「下一步」字段 → 尚未解决的生成/更新 TODO.md 待办（保留来源引用）
2. 检查工作日记中是否出现重复模式 → 提炼为本文件规则
3. 确认 TODO.md 只包含：🔄进行中 / ⬜待办 / ⏸阻塞；**发现任何已完成残留（✅/[x]）立即清除**

**⑤ 常见错误（必须避免）**：
- ❌ 把用户输入原文堆在 TODO.md（那是日志内容，应进工作日记）
- ❌ 在 MEMORY.md 复制待办列表（会漂移，唯一真源在 TODO.md）
- ❌ 任务完成不更新 TODO（TODO 会失真，失去"最近状态"价值）
- ❌ 完成任务只标 ✅/[x] 不移除（TODO 越堆越多，失去"活队列"意义）
- ❌ 不写「下一步」字段（检视回流会断链）

### 用户输入档案（记录所有用户输入的位置与追溯规则）
> 项目要求**完整保留每一次用户输入原文及拆解出的任务列表**（全局记忆约定）。

**存储位置（唯一真源 = 工作日记）：**
| 内容 | 存储位置 | 格式 |
|------|----------|------|
| 用户输入原文 | `.forgeself/memory/YYYY-MM-DD.md`（当天文件的「用户输入与任务记录」区） | `### 输入 N（YYYY-MM-DD）：<原文>` |
| 拆解出的任务列表 | 同上一行（输入记录下方，倒序跟随） | `- [x] <任务>（含结果）` |
| 活跃待办（未完成） | `TODO.md`（⬜ 待办区） | `- [ ] <任务>（P<优先级>，来源:输入<N>）` |

**规则：**
1. **每条用户输入必须归档**：收到输入 → 当天工作日记记录原文 + 拆解任务（**完成一项勾选一项**，`- [x]`）→ 未完成任务在 TODO.md 生成待办（标注 `来源:输入<N>`）
2. **追溯链**：TODO.md 待办上的 `来源:输入<N>` → 在 `YYYY-MM-DD.md` 的「输入 N」条目下找到**完整原文与任务拆解**
3. **TODO.md 不存输入原文**（只存待办摘要 + 来源引用）；原文与拆解永久保留在工作日记，不删除
4. **编号冲突处理**（多代理/并发会话）：若发现同一天已有相同「输入 N」编号，**保留各自完整内容**并在其中一条加注说明，**不得互相覆盖或删除**
5. **子代理（worker）任务边界**：派发子代理执行待办时，在任务说明中明确「只做编译/单元验证，不做运行时验证（重启后端/跑 e2e/git 提交）」，运行时验证由主代理完成后在 TODO 标注「（已实现，待运行时验证）」→ 验证通过后移除待办

### 文档反向同步（docs/ 落后于代码时）
> 详规与完整校验清单见 `doc-reverse-sync-sop.md`（同目录）。

**触发**：发现 `docs/` 与代码实现不一致（端口/类名/路由/状态过时或缺失）时，按 SOP 以代码为准反向更新 `docs/`，按 `docs/` 编号顺序（00→01→02→…→README）增量推进。

**铁律（写文档前必守）：**
1. **代码是唯一事实源**：文档中端口/类名/路由/路径/状态必须 `grep` 代码确认，禁止凭记忆或猜测；`specs/` 不纳入（开发期产物，职责分离）。
2. **不臆造**：代码没有的能力不写；已实现但文档缺失的，补；已变而过时的，改；缺口功能如实标「后端缺口/待补」并记 TODO。
3. **核心校验点（每篇必查）**：端口（后端 `7102`、前端 dev `7002`）；路由/端点须 grep 实际 `[Route]`/`[Http*]`（`[Route("api/[controller]")]` 实际路径 = `api/` + 小写控制器名，勿臆造连字符）；实体字段表须 `grep "BindColumn"` 取全量；插件路由前缀须 grep `[Route(` 取真实前缀（`Plugins/<Name>/Controllers/*.cs` → `api/<plugin>`），`Glob Plugins/*` 不递归会误判，须 `Grep`/`Glob Plugins/**`。
4. **登记**：新建文档须在 `docs/README.md` 速查表 + 已归档内容表登记；不顺手改本增量范围外的文档/代码。

## A7 设计稿工作流（对应 AGENTS.md §8）

设计稿位于 `forgeself-design/` 目录，HTML 文件可直接在浏览器打开预览。
- 实现层（Vue 组件）**必须**对齐设计稿的视觉与交互
- 设计稿中 `partials/app-shell.js` 提供共享外壳，`themes/` 提供 tokens
- 新增设置项 SOP：app-shell.js categories 加条目 → 复制现有设置页改 active 和内容区
- 设计稿 file:// 协议下禁止跨目录引用 CSS，主题文件须在 `forgeself-design/themes/` 放本地副本

## A8 speckit SDD 开发流程（对应 AGENTS.md §9）

功能开发走 speckit 的规格驱动开发（SDD）流程，命令文件位于 `.codebuddy/commands/speckit.*.md`：
```
specify → plan → tasks → implement → （analyze/converge 一致性检查）
```
产物在 `specs/NNN-功能名/`（spec.md / checklists / plan.md / research.md / data-model.md / contracts/ / quickstart.md / tasks.md）。
当前功能目录记录在 `.specify/feature.json` 的 `feature_directory` 字段。

**纪律**：
- 四步必须顺序执行，**不得跳步**：禁止出了 `spec.md` 就直接写代码
- 以「产物是否生成」判断本步完成，再进入下一步
- implement 步逐条执行任务，每完成一条把 `- [ ]` 改为 `- [X]`
- **implement 阶段禁止用 `git stash` / `git checkout` / `git reset` 等会改动工作区的命令**去"验证预先存在的状态"；需要对比基线时只用只读命令：`git diff` / `git show` / `git log`。

## A9 完成判定与汇报（对应 AGENTS.md §10 详细）

### 核心原则
- **"代码实现完成"不等于"任务完成"**。只有「需求满足 + 实现完成 + 经过实际验证」三者同时成立，才可判为已完成。
- **所有验证结果必须区分来源等级**：Verified（亲自跑过命令/测试/构建/截图/接口拿到真实输出）/ Inferred（凭代码推断未实跑）/ Unknown（未验证或无法确认）。
- **禁止虚构**：不得编造测试结果、截图、日志、接口响应或验证结论；存在未验证场景必须明说；存在阻塞不得宣称完成。
- 汇报以结果为中心，不以修改文件数量为中心；重要架构/设计决策做简要说明。

### 任务完成前的五步自检（强制）
1. **Requirement Check**：逐条核对用户提出的每一个需求是否都已满足（缺一条即未完）。
2. **Implementation Check**：代码/配置/接口/UI/业务逻辑是否完整、自洽。
3. **Validation Check**：尽可能选实际执行并检查的门禁（Unit/Component/E2E/Build/Lint/Type Check/UI/Screenshot/API/Runtime），每项标注 ✅/⚠️/❌ 及来源等级。
4. **Evidence Check**：记录可证明完成的真实证据（测试结果、构建输出、日志、截图、E2E 执行记录、真实接口响应）。**只列实际产生过的证据**。
5. **Risk Check**：查未覆盖场景、环境差异、外部依赖、兼容性或其他风险；无则写「无」。

### 最终状态（只能取其一）
| 状态 | 含义 |
|------|------|
| ✅ **COMPLETED** | 需求全满足、实现完成、关键验证全部 Verified、无阻塞 |
| ⚠️ **COMPLETED_WITH_RISK** | 已完成但有已知风险/未覆盖场景（须在「风险」显式列出） |
| 🟡 **PARTIALLY_COMPLETED** | 部分需求满足，其余未做/受阻 |
| 🔴 **NOT_COMPLETED** | 需求未满足或验证未过 |
| ⏸️ **BLOCKED** | 存在阻塞（缺信息/缺权限/环境/外部依赖），无法推进 |

### 汇报格式（强制使用）
```
[状态] 任务完成
任务：一句话说明本次完成的目标。

完成内容
- 最重要的完成项

主要变更
- 文件/模块：变更内容

验证结果
- Unit Test：✅ / ⚠️ / ❌（Verified/Inferred/Unknown）
- E2E：✅ / ⚠️ / ❌
- Build：✅ / ⚠️ / ❌
- UI / Screenshot：✅ / ⚠️ / ❌
（只列出实际执行过或确认过的验证项）

设计决策
（仅说明重要架构/业务/技术决策；无则省略）

代价 / 收益（运行经济学）
（成本侧 + 收益侧，量化尽量具体；一句话结论值得吗）

不做事决策（Decision NOT-to-do）
（审慎选择不做的事项：为何不做 → 什么时候该重新做；同步写入 docs/07-decisions/not-taken-decisions.md）

风险
（仍存在的问题、未验证场景或外部依赖；无则写「无」）

结论
- 是否真正完成：明确 ✅ / ⚠️ / 🟡 / 🔴 / ⏸️
- 是否可以交付：是 / 否
- 是否建议进入 Code Review：是 / 否
```

### 禁令
- 禁止使用模糊表达：「应该没问题」「基本完成」「看起来正常」「大概率是」「估计可以」。
- 必须给出**明确状态** + **验证依据**。拿不到依据就标 Unknown / 标风险，不得用猜测充当完成。

---

# Part B — 项目不变工程规则与踩坑规律（原 MEMORY.md 归档）

> 2026-09-24 起：`.forgeself` 被 git 忽略（不入库），`.forgeself/memory/MEMORY.md` 不再承载不变项目规则；以下规则自原 MEMORY.md 归档于此，随 `docs/` 入库可追溯。新增规则按 B 各小节归类追加。

## B1 全局约定

### 任务完成判定 = Verification-Centric Completion（最高优先级）
- **「代码实现完成」≠「任务完成」**：只有「需求满足 + 实现完成 + 实际验证通过」三者同时成立才算完成（详见 A9）。
- 验证结果须标注来源等级：**Verified**（亲自跑过命令/测试/构建/截图/接口拿到真实输出）/ **Inferred**（凭代码推断未实跑）/ **Unknown**（未验证或无法确认）。
- **禁止虚构**测试结果、截图、日志、接口响应、验证结论；存在未验证场景必须明说；存在阻塞不得宣称完成。
- **运行经济学**：时间资源有限 → 只做有意义的事；做不了/不做的选择必须记账——「审慎不做」台账 `docs/07-decisions/not-taken-decisions.md`（先 `ls` 确认编号/格式再追加，模板见该文件），避免下次重复评估/前后矛盾。

### 品牌命名（终决，2026-08-29，已落地）
- **全量命名 = `ForgeSelf`，不含 `Open` 前缀**。目录 `OpenForgeSelf.*`→`ForgeSelf.*`（Backend→Api、Frontend→Web、sln→`ForgeSelf.sln`）；`AssemblyName`/`RootNamespace`/namespace/using/`ConnName`/`ServiceName`/appName/库名 `ForgeSelf.db`/ico/http 变量/加密密钥 `FORGESELF_ENCRYPTION_KEY` 一律 `ForgeSelf`。
- **品牌展示也改 `ForgeSelf`**：DisplayName / Swagger 标题 / MCP 名 / AI 文案 / 关于页 / 脚本展示文案 / plugin.json Author 不再保留 `OpenForgeSelf`。中文品牌「铸己匣」不变。
- **已知保留项（非缺陷）**：`appsettings.Production.json` 占位域名 `openforgeself.example.com`（生产配置敏感只读）；各类 `.md` 文档保留历史原名；`temp_status.txt`；`forgeself-design/` 设计原型。

### Git 提交纪律（最高优先级）
- **未经用户显式要求，绝不执行 `git commit` / `git push`**：所有改动先留在工作区/暂存区，由用户 review 后确认无误，再由用户主动说「提交 / commit / 推 / 保存」才提交。Agent 自行提交 = 严重违反本纪律。
- **踩坑实例（2026-08-29）**：用户要求「库文件名改 {连接名}.db + 更新相关文档」，未说提交；Agent 却自动 `git commit`，被纠正后 `git reset --soft HEAD~1` 撤销、改动退回暂存区待审。
- 全局记忆 / `.forgeself/memory/*` 文件的写入可以（属 Agent 正常产出），但**不要顺手把这些文件或代码 `git commit`**，除非用户要求。
- 例外：仅当用户明确说「提交 / commit / 推 / 按功能块提交」等才提交。即便上轮已提交过，本轮若未再要求，仍不提交。

### TODO 清理/归类必须逐项先验证（2026-08-30 踩坑）
- **清理/归类 TODO 前，必须对每个遗留项实际核验**（grep 代码 / 读文件 / `dotnet build` / `pnpm test`）确认是否已解决；**已解决的标「✅ 已解决」并从活跃队列移除，不得凭印象归类**。
- 归类结论要可追溯：在 TODO 加「✅ 已解决」区块列证据，正文删去对应活跃条目，避免下次重复评估。
- **TODO.md 移出版本控制（2026-09-24）**：工作队列不入库（gitignore），原始历史已用 `git filter-branch` 全量重写剔除；工作区文件保留、TODO 流程不变（仅不再提交）。
- **历史重写补充规律**：`filter-branch` 会删工作区文件，重写后需从备份分支 `git show <branch>:<path>` 恢复；中文文件名 git 默认 `core.quotepath=true` 输出八进制转义，程序化处理用 `git -c core.quotepath=false`。

### 大特性按逻辑边界分批次提交（2026-08-30 实践）
跨多文件的大特性按「后端/前端桥/路由/插件/文档/skill/e2e/记忆」单一职责拆多批；用显式路径 `git add <paths>`（禁用 `git add -A` 防误带运行时日志）；同 shell 串行 `git commit` 避免并发竞争；临时调试文件（如 `e2e/temp-*.spec.ts`）删后再提交。

## B2 验证与测试铁律（e2e / Playwright / 工具）

### e2e 测试铁律
- **绝不 mock**：Playwright e2e 一律对接真实后端（`http://localhost:7102`）+ 真实认证（`e2e/helpers/real-auth.ts` 解密 ForgeSetting.config 注入 localStorage）；防破坏类隔离（如真实重启）除外并注释说明。
- 新增功能测试遵循「测试-修改-验证-推进」循环。
- **正常验证流程必须走 root `playwright.config.ts`（含 globalSetup），禁止 `E2E_SKIP_GLOBAL_SETUP=1` 直连 51888**：globalSetup 会 `dotnet publish` 临时宿主 7102 + 前端 dev 7002 + 首启 `GET /api/api-server/init-token` 拿明文 token 注入 `E2E_API_TOKEN`。跳过它 → token 未注入 → `real-auth.ts` 回退解密 `ForgeSetting.config`，而 030 已升级 v2 机器绑定令牌（`v2:` 前缀 + PBKDF2 机器派生），旧 v1 写法直接崩 → 鉴权全挂。
- **`real-auth.ts` 与 `host-api-token.ts` 解密算法必须一致**：均按 v2（PBKDF2 机器派生，与 `ForgeSelf-AIProvider-Default-Encryption-Key` 同源）优先、非 `v2:` 前缀再回退 v1。两处重复逻辑易漂移，改一处须同步另一处。
- **global-setup 复制 SQLite provider 须兼容双布局**：`System.Data.SQLite.dll`/`e_sqlite3.dll` 可能落在 `publish/` 根或 `publish/Plugins/`，复制源=根或 `Plugins/` 双候选、目的=临时 publish 根 + `Plugins/` 都放。
- **正常 e2e 用全新临时 DB**：会暴露宿主建表未覆盖的插件表缺失 bug（如 MemorySystem `chat/memories` 500）。51888 旧库已迁移故不显，勿以 51888 通过等同「全新库通过」。
- **共享可变外部状态的用例组必须 `test.describe.configure({ mode: 'serial' })`**：`playwright.config.ts` 是 `fullyParallel:true`——同文件多个用例默认并行；若用例共享同一端口/文件且某用例会临时改状态再改回（如 MCP 端口 PUT 热重启），并行用例会在切换窗口拿到 `ECONNREFUSED`/旧状态。修法：① 此类用例组从一开始就 serial；② 状态改回后轮询真实就绪信号（如 `/health` 200）再结束用例，不要只断言 PUT ok。
- **断言错误提示前先看真实响应原文**：不要凭实现猜测错误文案写 `toContain`——① 错误文本可能经 JSON 序列化（中文变 `\uXXXX`），直接 `toContain('中文')` 匹配不到，须 `JSON.parse` 后再断言；② 场景要选对（如「mcp.<id> 缺工具名段」才走格式提示，缺服务器 id 走未连接提示）。写断言前先跑一次拿真实响应。
- **浏览器走查截图立即存档**：`bu.screenshot()` 每次覆盖同一临时文件，多场景截图必须每张立即 copy 到项目 `screenshots/` 目录，不要攒到后面统一存。
- **e2e 标题断言 vs 版本徽标**：铁律要求插件根视图标题旁带版本徽标（`标题 v{{version}}`），Playwright `toHaveText('标题')` 是**精确匹配** → 必然失败。标题类断言一律用前缀匹配（`toHaveText(/^标题/)`）；任何含动态后缀（版本/计数）的文案同理。

### 仓内验证工具（禁止每次现写）
- **插件页走查** = 跑 `e2e/plugin-store.spec.ts`（globalSetup 自动构建宿主→起实例→解密注入→断言版本徽标/启用标签/截图），**必须** `--output=<空目录>`。
- **运行态宿主手工 token** = `node scripts/get-forge-token.cjs`（默认读 `~/.forgeself/Config/ForgeSetting.config`，v2 PBKDF2-SHA256 210k + AES-256-CBC；与 real-auth.ts 同算法）。
- **产物 DLL 字符串验证** = `node scripts/probe-dll-string.cjs <dll> <str> [--expect-absent]`（UTF-8+UTF-16LE 双检；expect-absent 验证「已删实现不在产物」）。
- 新验证场景缺正规入口 → 先补工具/用例再走，不现写一次性脚本（教训：2026-09-24 用户质询「为何反复手工、未上报/记待办」）。

### 统一 e2e 测试体系（2026-08-31 建立）
- 现状：**前端 e2e 是项目唯一前后端集成测试手段**（无独立单测体系）。既有 `ForgeSelf.Web/e2e/*.spec.ts` 28 个（应用层）+ 插件层 e2e，统一归口 `e2e-testing` 技能，**单一 Playwright 配置 + 单一 globalSetup**。
- 关键代码事实：后端默认端口 `7102`（`ForgeSetting.Current.PortNumber`，config 可改；51888 是历史手动冷启验收端口非默认）；前端 dev `7002`；token 键 `localStorage['forge_api_token']`；插件前端路由 = `plugin.json` 的 `frontend.route`（sems=`/sems`），宿主经 `/plugin-view/<id>` 命名空间注册（仅冲突回退时）。
- **宿主全局 Mutex 需实例标识**：`Program.cs` 硬编码 `Global\ForgeSelf-{GUID}` 单例锁；e2e 经 `FORGESelf_INSTANCE_ID`（env）或 `--instance-id=`（CLI）以独立 Mutex 并存。
- **数据目录隔离**：发布版 exe 设 `ASPNETCORE_ENVIRONMENT=Development` → 数据根=发布目录/Data（每次 temp 全新），天然隔离 `~/.forgeself`。
- **token**：宿主启动幂等生成 `ApiToken`，globalSetup 调 `GET /api/api-server/init-token`（首启无鉴权）拿明文注入 `E2E_API_TOKEN`。

### WebApplicationFactory 测试宿主
- **AddControllers 必须显式 AddApplicationPart**：宿主 AppBuilder.cs 用 AddControllers() 裸调用，WAF 测试宿主下 entry assembly 是 testhost → 控制器扫描不到宿主程序集（40 例全 404）。修法：`AddControllers().AddApplicationPart(typeof(AppBuilder).Assembly)`（生产幂等）。
- 残余（已登记 TODO）：**插件控制器**在 WAF 下未注册（插件 Apply 的控制器注册机制与 WAF host 重建时序不兼容），需插件框架专项。
- 改写进程级全局状态的测试（ForgeConfig/ConfigUnifier/ProxyCapture/CaptureEngine 单例/ScriptRunner）需在用例内自行 **save+restore**（保存原 `Config<T>.Provider.FileName` 并 finally 还原）。
- `RealLLMIntegrationTests` 依赖真实 AI 端点 → 用 `Tests/TestDoubles/FakeAIService`（固定返回，无网络）替代 `AIService`。

## B3 前端工程规则

### Element Plus 组件导入
- **禁止显式 `import { ElXxx } from 'element-plus'`**（type 导入除外）：组件样式依赖 unplugin-vue-components 按需注入，显式导入会绕过自动解析导致组件无样式（已踩坑：TodoEditDialog 弹窗样式缺失）。统一在模板中使用 `<ElXxx>` 自动解析。
- 例外（API 调用型组件，unplugin 不解析 JS 调用，必须显式导入）：`ElMessage`、`ElMessageBox`、`ElNotification`、`ElLoading`。
- ESLint 已配 `no-restricted-imports` 拦截。

### 前端 DTO 枚举形态必须与后端 JSON 序列化一致（2026-09-22 踩坑）
- **坑**：后端 `PluginState` 是**默认数字枚举 0-9**（JSON `state`=数字），前端 `types/plugin.ts` 却定义成**字符串枚举**（'Running'…）→ 模板 `plugin.state.toLowerCase()` 在数字上调用抛 `TypeError` → 插件市场列表渲染即白屏。此前 404 空列表掩盖了此 bug。
- **对策**：前端枚举成员值必须与后端序列化形态对齐（后端数字 → 前端数字）。数字枚举**反查** `PluginState[state]` 返回**成员名 PascalCase**（4→'Running'）；状态文案 switch 须覆盖**全部**枚举成员（曾漏 Stopping=5 → 徽标显示「未知」）。
- **排查口诀**：前端对后端返回字段做 `.toLowerCase()`/字符串比较却报 TypeError 时，先查后端 DTO 该字段是数字枚举还是字符串。

### 前端统一 request 助手：204 / 空 body 必须短路（2026-09-09 踩坑）
- **坑**：`request` 助手对成功响应**无条件 `res.json()`**；后端 DELETE 等端点返回 204 No Content（空 body）时抛 `Unexpected end of JSON input` → emit 链中断（列表不刷新）+ 对话框残留报错。
- **对策**：通用 `request` 层先判 `res.status === 204` 直接 `return undefined`，再 `await res.json().catch(() => undefined)` + 空值短路；不能假定成功响应必有 JSON body。
- **完成信号**：删除类操作 = 「列表已刷新（emit 链走完）」+「无报错」二者同验才闭环（已在 ai-agent e2e 固化为回归断言）。

### 宿主前端 service 的 parseResponse 已解包（2026-09-22 踩坑）
- 前端 service 的 `parseResponse` 已解包 `json.data`（返回 T 本身）：新增 fetch 函数**直接按 T 收**，禁止再取 `.data`/`.stats`——fetchPluginVersion、fetchTraffic 各踩一次（恒返回 undefined → 空列表/空版本，页面无错但功能静默失效）。

### 收尾工作树的四桶分类 + 引用完整性校验（2026-09-09）
- **分类**：`git status` 把未提交项先分四桶——①源码 ②文档反同步 ③技能/工作日记 ④scratch，分别处置；源码改动才有门禁，文档/技能/日记走一致性核对即可提交。
- **不臆造引用**：`docs/` 引用的图/文/档案，提交前必须 `Test-Path` 确认存在（杜绝文档链到不存在的文件）。
- **会话工具产物**：`.playwright-mcp/`、`.serena/` 随跑随生，已入 `.gitignore`；scratch 一律移 `.trash/`（不永删），`.trash/` 本身已在 .gitignore。

### 前端测试与构建约定
- **单测后缀必须是 `.test.ts`**：vitest `exclude` 含 spec.ts 后缀（那是 Playwright e2e 的约定），写成 spec.ts 会报 "No test files found"。
- **代码注释里禁止出现 `*/` 序列**：例如写 `**/*.spec.ts` 会因含 `*/` 提前终止块注释 → esbuild "Unexpected *"。注释中改用文字描述。
- **前端 ElSelect 在 jsdom/vitest 的无限递归坑**：ElSelect 空值（undefined）归一化在 jsdom 下 `Maximum recursive updates exceeded`。规避：①页面用 `:model-value` + `@update:model-value` + `@change` 触发业务，**别用 v-model 绑 ref<number|undefined> + clearable**；②组件测试直接 stub `ElSelect/ElOption`。
- **ElForm validate 在 jsdom 不拦截**：EP 表单 rules 在 jsdom 测试环境下可能直接通过（required 未生效），关键业务校验须在 handleSubmit 内手动兜底，勿只依赖 EP validator。
- **全屏背景图**：用固定定位 `<img>` 元素，**不要**用 CSS `background-image: url(外链)`（本环境外链背景图不渲染）。
- **CSS 顶部禁止外链 `@import`**：浏览器须先 fetch 第三方域，离线/受限网络 → 整个文件悬挂，`:root` 变量与基础规则全部失效（渲染为无样式裸 HTML）。字体用系统 fallback 已足够。触发条件：写任何共享 CSS 入口时。

### 插件前端（web/）工程规则
- **前端产物** = Vite lib 模式 ESM，入口 `web/dist/index.js` + 同目录 `style.css`（宿主 `pluginViewLoader` 按约定注入 `<link>`）。
- **vue / vue-router / pinia / element-plus 必须 external**（宿主 import map 解析到同一份实例，防 Vue 双实例——双实例下 `ref`/`reactive` 响应式与组件通信全部失效）。
- 导出：必须 `export { XxxView as default }` 且命名导出名 = `plugin.json` `frontend.views[0]`，宿主按名字取、取不到回退 default。
- **共享依赖 = Import Map + 宿主共享桥 shim**：① `ForgeSelf.Web/src/shared/exposeSharedDeps.ts` 把真实模块挂到 `window.__FORGE_SHARED__`（main.ts 顶部调用，MUST 早于任何插件界面加载）；② `ForgeSelf.Web/public/shared/{vue,vue-router,pinia,element-plus}.js` 为 shim，从该全局**具名再导出**（ESM 不支持动态 `export * from window.xxx`，必须枚举）；③ `index.html` 注入 `<script type="importmap">` 映射裸模块名到 shim URL；④ 插件产物把共享依赖声明 external。
- **shim 导出清单必须脚本生成**：`ForgeSelf.Web/scripts/generate-shared-shims.mjs` 从真实包自动生成（vue 172 / vue-router 23 / pinia 16 个导出）。手工枚举必漏。
- **插件新增 EP 组件 = 一条宿主 shim 更新链，发布前必须整体走完**（单测/e2e 全绿不代表生产可用——e2e 每次全新 publish 宿主天然带最新 shim，测不出「增量发布后宿主静态资源陈旧」）：① `exposeSharedDeps.ts` 导出组件；② `shared/element-plus.js` shim 具名导出；③ `index.html` import map `?v=N` **递增**（浏览器缓存键，不 bump 则生产仍加载旧 shim → 插件白屏 "does not provide an export named 'ElXxx'"）；④ `pnpm run build` 重建宿主前端（输出到 `ForgeSelf.Api/wwwroot`）；⑤ 覆盖 `publish/wwwroot`；⑥ **删除 publish/wwwroot 下全部旧 `.br`/`.gz` 预压缩文件**（StaticFiles 对 Accept-Encoding 优先回旧压缩内容）；⑦ 走查前先 `curl http://host/shared/element-plus.js` 核对新导出存在。
- **@element-plus/icons-vue 在宿主共享桥下的 sizing 陷阱**：插件代码里写 `<Folder :size="14" />`，prop 转换依赖宿主打包器构建时处理，共享桥下不触发 → **必须给 SVG 添加 CSS 强制 `width: [N]px; height: [N]px;`**，否则图标被父容器撑大到默认尺寸（>20px）视觉失衡。
- **Vite lib 模式的 CSS 不会被产物 JS 引用**：约定 `assetFileNames:'style[extname]'` 固定输出 `style.css`，由宿主加载器按「入口同目录」注入 `<link>`（幂等，`data-plugin-style` 去重）。
- **plugin.json 序列化是 camelCase**：磁盘上写 `frontend.entry`（小写），反序列化到 C# 属性 `FrontendContributes.Entry`（PascalCase）。新插件 MUST 用 camelCase。
- **插件 web/ 构建（pnpm v11 esbuild 放行）**：`pnpm build` 报 `ERR_PNPM_IGNORED_BUILDS` → pnpm v11 把构建白名单移到 `pnpm-workspace.yaml` 的 `allowBuilds` / `onlyBuiltDependencies` 字段。标准样板：插件 `web/` 下建 `pnpm-workspace.yaml`（`allowBuilds.esbuild: true` + `onlyBuiltDependencies: [esbuild]`）并删掉 `package.json` 里失效的 `pnpm` 字段。触发：新建/构建任何 `Plugins/*/web` 前端时直接照搬。
- **sems 前端构建用 pnpm（非 npm）**：`Plugins/Sems/web` 所在仓库是 pnpm workspace（`link:` 协议），`npm install` 直接失败；命令 = `cd .../web && pnpm install && pnpm run build`。
- **前端路由接管约定**：插件清单 `frontend.route` **直接作为最终路径**（`/ai-agent`），不前缀化；仅当被宿主静态路由占用时回退 `/plugin-view/<id>`（`MANIFEST_ROUTE_PREFIX`）。判定靠 `isPathTaken`（精确匹配 + 参数化路由同段数比对），冲突先回退、回退仍冲突则跳过，**绝不覆盖宿主页面**。
- **宿主不再内置插件页**：插件界面一律由清单驱动远程加载，宿主不再手写插件页静态路由。
- **pluginViewLoader 缓存键 = 版本号**：入口 URL `/plugins/{id}/web/dist/index.js?v={version}`；版本未变时浏览器从磁盘缓存取旧 bundle，**同版本内的纯前端改动（如文案/icon）用户看不到，需硬刷**。对策：(a) 纯文案/样式改 → 文档化为已知限制；(b) 行为变更 → 升 patch 版本触发重抓；(c) 未来优化缓存键为 `version + hash`。
- **项目 logo/图标三处落点，换 logo 必须三处全更**：① web 前端源 `ForgeSelf.Web/public/`（favicon.ico + `logo/*.png`）；② 构建中间产物 `ForgeSelf.Api/wwwroot/`（陈旧则发布回旧图）；③ exe 图标源 `ForgeSelf.Api/Assets/ForgeSelf.ico`（**不会从 web logo 自动派生**，须显式重新生成 + 重编译 exe 才生效）。

## B4 后端工程规则

### 分层 / 鉴权 / 配置
- 分层：Controllers/Services/Entities；新插件走 `Plugins/` + `plugin.json` 注册；异步统一 `async/await`，不 `.Result` 阻塞。
- **管理面/CRUD 控制器必须类级 `[Authorize("ApiKeyPolicy")]`**（命名策略 = `ApiKeyAuthenticationHandler` Bearer 方案，注册于 AppBuilder.cs）：前端 `request.ts` 已全局注入 Authorization 头，勿因"前端会带 token"而漏加鉴权（踩坑：AIProviderController 曾无 [Authorize]；PluginController 2026-09-24 补课）。
- **鉴权 token 唯一真源 = `ForgeSetting.config` 的 `ApiToken`（AES-256-CBC 解密），绝非 appsettings 的 "ApiKey"**：`ApiKeyAuthenticationHandler` 比对的正是 `ForgeSetting.Current.ApiToken`（解密后）；appsettings 的 "ApiKey" 是 AI provider key，误用会 401。解密用 `AesSecretEncryptionService`（密钥 `Encryption:Key`/`FORGESELF_ENCRYPTION_KEY`/默认 `ForgeSelf-AIProvider-Default-Encryption-Key`）。
- **`ForgeConfig<T>` 路径覆盖触发铁律**：`ForgeConfig<TConfig> : Config<TConfig>` 基类在静态构造里把 `FileConfigProvider.FileName` 覆盖为绝对数据根路径（`数据根/Config/{Name}.config`）。该静态构造**只在 `TConfig` 实例被创建时触发**（经 `ForgeSetting.Current` 的 `new TConfig()` 路径），**只读 `ForgeSetting.Provider` 不触发**。断言用 `BeAssignableTo<FileConfigProvider>()`（勿 `BeOfType`，默认 XmlConfigProvider 继承 FileConfigProvider 但精确类型断言失败）。新增 `Config<T>` 子类须继承 `ForgeConfig<T>` 且至少经一次 `.Current` 访问。
- **启动后端必须用 `dotnet run`（不带 `--urls`）**：NewLife.Agent 宿主会**吞掉** `--urls` 参数并回退到默认 5000。正确命令：工作目录 `ForgeSelf.Api` 跑 `dotnet run`（端口取 `ForgeSetting.config` 的 `PortNumber`，默认 7102）。**改端口改 `ForgeSetting.config` 的 `PortNumber`，勿用 `--urls`**。
- **宿主启动会写回 ForgeSetting.config（端口事故 2026-09-23）**：宿主启动流程会持久化自身运行端口（SettingsController/ApiServerController 的 ForgeSetting.Save 路径）。起宿主/发布前先读配置记录原值；冷启动后立即核对配置未被改写，被改写则恢复；发布脚本起宿主前加「备份配置 + 起后核对」防护（进 035 待办）。

### 运行时数据落盘铁律
- **唯一入口是 `IDataLocationService`**：开发态→程序目录 `Data/`，发布/服务态→`~/.forgeself`。任何写盘（SQLite 库、插件数据、配置 `*.config`、图片缓存）都必须经它，禁止再写 `AppContext.BaseDirectory` 字面量。
- **目录布局**：宿主数据/库 → 数据根（`~/.forgeself/OpenForgeSelf.db`）；插件数据与插件库 → `数据根/Plugins/{插件Id}/`。**库文件名 = XCode 连接名（PascalCase）**，目录名仍是 kebab 运行时 Id。目录名常量是 `IDataLocationService.PluginDataRootName`，**禁止两侧各自写字面量**（漂移过一次：一侧 `plugins` 一侧 `Plugins`）。
- **appsettings 的 `ConnectionStrings:{连接名}` 只认绝对路径**：相对路径会被 NewLife 解析到程序目录、绕过数据根 → SQLite Error 14。`XCodeConfig.AddXCode` 已实现「相对路径忽略 + 告警，绝对路径才采纳」。新增库一律只在 `XCodeConfig.HostDbs`/`PluginDbs` 加一行。
- **插件禁止自注册 `DAL.AddConnStr`**（会覆盖宿主路径造出第二份互不可见的库，QuickLinks 踩过）；只做 `DAL.Create(ConnName)` + 探活。
- **SQLite 不自建父目录**：落在 `Plugins/{id}/` 子目录的库，必须在 `AddXCode`/`InitializeXCodeDatabase` 里先 `Directory.CreateDirectory`，否则 open 报 Error 14。
- **`ConfigUnifier.UnifyAllConfigFiles` 必须是进程里最早的 NewLife 调用**（已前置到 `Program.cs` 顶部）。否则框架先按默认相对路径初始化 `FileConfigProvider` 并对程序目录建 FileSystemWatcher → 发布目录无该目录，启动首行即报「FileSystemWatcher 创建失败」。
- **SQLite 探活统一用 `dal.Db.ServerVersion`，不要用 `dal.Session.Query("SELECT 1")`**：后者在库文件尚未创建时抛 `NullReferenceException`，导致每次启动误报「数据库初始化失败」。
- **发布产物要区分「运行时生成物」与「部署资产」**：`Data/`、`Log/` 是运行 exe 产生的遗留，构建脚本可排除；`Plugins/` 承载插件程序集属部署资产，**绝不能排除**。Web SDK 默认只把 `Plugins/**/plugin.json` 当内容发布，插件 DLL 靠 csproj 的 `StagePluginsToPublish`（`AfterTargets="Publish"`）补齐。

### XCode 实体加列/改字段流程
- 改实体字段的**唯一真源是 `Data/Model.xml`**（不是手改 `Entities/*.cs`）：在 xml 所在目录运行 `xcode Model.xml`，由 NewLife.XCode 工具重新生成 `Entities/*.cs`。
- 生成的实体文件由工具生成；**手写业务代码必须放在 `*.Biz.cs`**（如 `ChatRecord.Biz.cs`），重生成不会被覆盖。
- 数据库列随 XCode `Meta.CreateTable()` 自动同步，无需手写迁移脚本。
- **非主键列必须 `Nullable="True"`**：非主键列若不设 Nullable，XCode 生成的建表 SQL 会对非 INTEGER 主键列生成 `AUTOINCREMENT`，SQLite 报 `AUTOINCREMENT is only allowed on an INTEGER PRIMARY KEY`。非空约束靠业务层保证。
- **新增字段必须注册到 `this[name]` 索引器**：XCode 通过 `public override Object this[String name]` 读写列，不是 C# 属性。漏同步索引器 → ORM Insert 静默丢弃、读回 null。新增/修改字段后：① `FindById` 读回断言非 null；② 用 `PRAGMA table_info(...)` 核对物理列存在；③ 若表名与类名不同，勿用类名查。
- **插件必须自建表（XCode 建表职责边界）**：宿主 `XCodeConfig.EnsureTablesCreated` 只反射扫描**启动时已加载**的程序集，插件实体（ConnName=插件独立库）不在其列 → 全新库下 `FindAll` 报 `SQL logic error`/`no such table`。**修法**：插件 init 时自行 `DAL.Create(ConnName)` 先初始化连接，再反射取实体 `Meta` 静态属性调用 `Meta.Resolve()`（检查+创建，幂等）。注意时序——首个实体 `CreateTable` 在连接未就绪时只建空库不建表。`EnsureCreated()` 正确写法：先 `EntityFactory.InitConnection(ConnName)` 全量建表，再探活确认。
- **XCode 分页 TotalCount 铁律**：**`PageParameter` 不设 `RetrieveTotalCount = true` 时 `FindAll` 后 `pageParam.TotalCount` 恒为 0**——所有分页接口 total=0 的统一根因。读 `TotalCount` 的分页查询**必须**显式设该标志；例外：`UsageStatsService` 用 `FindCount(exp)` 显式 COUNT。新增分页优先沿用这两种写法之一。
- 扫描 `Plugins/**` 断言清单唯一性的测试必须**排除 `_` 前缀工具目录**（`_backups/`、`_published/`），否则发布流程产生的版本副本造成 Id「假性重复」断言失败。

### 后端 DI 作用域安全
- **root provider 不能解析 Scoped 服务**：宿主 `WebApplication` 在 Development 默认 `ValidateScopes=true`，从 root provider `GetService(Scoped契约)` 抛 `Cannot resolve scoped service ... from root provider`。**正解**：解析 scoped 一律走 `HttpContext.RequestServices`（request scope）或 `hostProvider.CreateScope()` 后 `scope.ServiceProvider`。
- **插件子 provider 手动 `BuildServiceProvider` 默认 `ValidateScopes=false`** → 插件内 `AddScoped` 服务解析为「俘获单例」，符合 Cordis「每上下文单例」设计（027-cordis-kernel），**不 500**；已显式锁定该契约，防未来误开校验重引入 500。
- **新增 Backend 插件报 CS0579「assemblyinfo 特性重复」** → 查 `ForgeSelf.Api.csproj` 的 `<Compile Remove>` 列表是否漏该插件（插件 .cs 被 Api 与插件自身 csproj 双重编译）；补 `<Compile Remove="Plugins\<Id>\**\*.cs" />` + bin/obj `<Content Remove>` 即解。**注意区分内嵌/拆分**：运行时依赖宿主程序集内嵌类型的插件禁止整目录 `Compile Remove`（类型会从宿主消失），只排除嵌套 `obj\**`/`bin\**` 止血；拆分插件才补全套 Remove+引用+staging。
- **解决方案文件 = `ForgeSelf.slnx`**（2026-09-21 由 .sln 迁移，19 项目全量保留）。解决方案级构建命令 = `dotnet build ForgeSelf.slnx`。
- **dotnet test 被 dev server 锁 exe 的规避**：运行中 dev server 持有 exe，`dotnet test`（Debug 构建）拷贝 apphost→exe 会 MSB3027/3021 失败。规避：`dotnet test -p:UseAppHost=false`；若 DLL 也被锁（加载中程序集），构建/测试一律加 `-p:OutDir=<临时目录>` 旁路验证。
- **git worktree 新检出目录首构失败 ≠ 代码事实**：全新目录无 obj/project.assets.json，restore/构建顺序问题所致（报 CS0246 但 HEAD 代码自洽）。判定「某提交能否编译/测试」不要用未先 restore 的 worktree；用 `git show HEAD:path` 核对代码事实，或先 `dotnet restore && dotnet build`。

## B5 插件体系与发布

### 插件体系（ForgeSelf.Api/Plugins）
- **两层命名（刻意设计，非不一致）**：目录/程序集(.dll)/EntryType 用 PascalCase（代码身份）；`plugin.json` 的 `Id`/数据目录/`~/.forgeself/Plugins/{id}`/路由/库文件用 kebab-case（运行时身份）。
- **实际 13 个插件**：ai-agent / dev-tools / file-tools / memory-system / proxy-capture / quick-links / sample / system-monitor / script-runner / scheduler / text-tools / todo-tracker / workflow-engine。
- 插件禁止自注册 `DAL.AddConnStr`，只 `DAL.Create` 探活（统一用 `dal.Db.ServerVersion`，禁用 `dal.Session.Query("SELECT 1")`）。
- **启动装配唯一路径 = `PluginManager.RegisterAllServices`**（AppBuilder Build 前调用，内部 Discover→拓扑排序→Resolve→MountPlugin→`fiber.Mount(Apply)` 一步到位，状态即 Running）；`LoadAndStartAllPlugins`/`InitializePlugin` 定义存在但**无外部调用者（未接线）**，勿误以为启动需另调；热启/热重载走 `EnablePlugin→LoadPlugin→InitializePlugin`。
- **宿主契约要分两批 seed 进插件上下文（时序铁律）**：`PluginManager.RegisterAllServices()` 在 `builder.Build()` **之前**执行并立刻触发插件 `Apply`，而 `ProvideHostServices(app.Services)` 必须等 Build 之后才有 DI 可解析。因此「Apply 期就要用」的契约（典型：`IDataLocationService`）必须用 `PluginManager.ProvideHostService(contract, instance)` 在 `RegisterAllServices` **之前**手工 seed 同一实例；其余契约仍走 Build 后的 `ProvideHostServices`。违反后果：插件抛「未注册到插件上下文」并整体注册失败，日志只表现为插件数变少、扩展点消失，不易定位。
- **跨插件解耦数据共享**：不互相注册服务/不直接调用，而是经 `IDataLocationService.GetHostDataDirectory()` + Abstractions 中公共常量落共享 JSON 文件，各自 `ctx.Get<IDataLocationService>()` 解析后读写。**插件服务注入宿主契约必须用构造函数注入 `IContext` + `ctx.Get<T>()`**，禁止直接构造注入宿主服务（子容器三无，直接注入必 500）。
- **ctx 与 IServiceCollection 是两套存储（根因级）**：插件 Apply 里 `ctx.Get<IServiceCollection>()` 拿到宿主 DI 容器，`services.AddSingleton<T>()` 只进宿主 DI（供控制器构造注入）；运行时 `ctx.Get<T>()` 查 **ctx 自有共享服务表**。两不相通。**正确模式**：`var inst = new T(); services?.AddSingleton<T>(inst); ctx.Register<T>(inst);` 同一实例双注册。
- **内核插件间服务互通（Cordis 共享服务表）**：`Context.Register<T>` = **全局服务**（root 共享服务表，兄弟 Fiber 可见，注册即走 `ctx.Effect` 逆序回滚自动摘除）；`Context.RegisterLocal<T>` = **本地值**（自身字典，不进共享表）；`Get<T>` 解析顺序 = **本地值 → 共享表**。宿主 seed 契约常驻 app 生命周期、不摘除。消费方 = `ctx.Get<T>()` 软依赖探测（null 走降级）、**禁止缓存实例为字段**（每次用每次 Get，防热重载悬空）。提供方 = **eager 单例**。坑：插件 `Apply` **先于**宿主 `ProvideHostServices` seed 契约执行，故服务对宿主契约须**构造时注入 `IContext` + 首次使用时 `ctx.Get<T>()` 懒解析**。
- **插件工具注册走「属性暴露 + ExtensionPointManager 自动发现」，禁止在 Apply 里手动 RegisterTool**：插件装配期 `ctx.Get<IToolRegistry>()` 为 null，`Apply` 里取注册表注册工具必然失败。正确姿势（AIAgent 同款）：工具实例持 `IContext`，`ExecuteAsync` 时运行期 `_ctx.GetService(typeof(IToolRegistry))` 解析；插件暴露 `public List<IToolFunctionExtension> ToolExtensions { get; }` 属性，宿主 `ExtensionPointManager.DiscoverExtensionsFromPlugin` 自动注册 + 热重载自动注销。
- **插件控制器构造注入宿主契约 = 激活 500（根因级）**：插件子容器（只含插件自身服务 + IContext）解析不到宿主契约 → `PluginAwareControllerActivator` 激活即 500「Unable to resolve service for type ...」。铁律：插件**控制器/服务**一律注入 `IContext`，宿主契约（IToolRegistry/IConfigurationService/ILogService…）运行期 `ctx.Get<T>()` 取。排障：`grep "public \w+Controller\(.*IToolRegistry"` 可扫出此类雷。
- **宿主契约解析不到的三步修法（ScriptRunner DI 实证）**：① `HostProvidedServiceContracts` 补齐契约清单；② 服务构造改 `IContext`，宿主契约走惰性属性 `_ctx.Get<T>() ?? throw`（带强文案）；③ **`ProvideHostServices` 给 `IServiceProvider` 特判——必须 seed 宿主根 provider 本体，不是本次 scope 的 provider**（scope 释放后取会抛 ObjectDisposedException）。
- **插件必须自建表**（见 B4 XCode 流程）；**EnsureCreated 必须 `EntityFactory.InitConnection(ConnName)` 全量建表再探活**（AgentHub 2026-09-23 教训：依赖「Migration=On 首次连接自动建表」是错误假设）。
- **插件内起服务勿用 IHostedService**（PluginServiceRegistry 按 ServiceType 单写覆盖，多插件只启一个）——用静态单例引擎 + `ctx.Effect` 卸载。
- **实体 ConnName 保留（插件改名迁移规律）**：插件改名时实体 `[BindTable(ConnName="X")]` 与宿主 `XCodeConfig.PluginDbs` 的 **key（连接名）保留**，只改 value（插件 Id）→ 库文件名/表不变，数据目录整目录 Move 即完成数据零迁移。

### 插件运行时显式更新（2026-09-24 起：无自动热重载）
- `PluginVersionService.UpdatePlugin`：版本比较（≤ 已加载版本直接跳过）+ 切 `current` 指针 + 卸载旧 ALC + 加载新 DLL + 端点动态刷新（`ApplicationPartManager` + `MvcActionDescriptorChangeProvider.NotifyChange`）。
- **触发方式（唯一）**：`POST /api/plugin/update/{id}` 版本化显式更新（`scripts/publish-plugin.ps1` 只做编译+staged 复制，不自动调 API）。`PluginHotReloadWatcher` 已一刀切移除（2026-09-24 输入 8 用户拍板，宿主不再自动监听插件目录）。冷启动宿主 = 兜底。
- **版本目录布局**：`Plugins/{id}/versions/<ver>/<entry>` + `Plugins/{id}/current` 文本指针；staged 入口在 `Plugins/_backups/{id}/{ver}/`。
- **失败回退**：`ReloadPlugin` 异常自动回退 `current` 到上一可用版本；`POST /api/plugins/rollback/{id}` body `{"version":"x"}` 手动回滚。
- **DLL 锁处理**：`PluginAssemblyUnloader.ForceCollect`（两轮 GC + 终结器）+ `TryOpenExclusive`（`FileShare.None`）+ `TryDeleteDirectory`（占用时跳过下轮重试）。
- **发布脚本用法**：`./scripts/publish-plugin.ps1 -Plugin AIAgent`（`-Plugin` 是目录名 PascalCase，非 id）；幂等（staged 已存在则 skip，需 `-Force` 覆盖）；`-DryRun` 仅打印。技能一键跑：`pwsh .agents/skills/plugin-publish-verify/scripts/run-plugin-publish-verify.ps1 -Plugin <PascalCase目录>`（`-BumpVersion` 自动升版本、`-SkipPublish`、`-Force`、`-ReloadWaitSec`）。
- **覆盖顺序：payload（DLL、web/dist）先，`plugin.json` 最后**。先写清单会在拷贝中途触发重载，后续 DLL 拷贝报 `Could not find file`。
- **更新成功判定（用户约定）**：取接口 `GET /api/plugin` 返回的该插件 `version`，与**当前活动目录 `plugin.json`（清单文件）**的 `Version` 比对，**一致即认为更新成功**。清单文件是版本号唯一真源。
- **端点前缀是单数 `api/plugin`**（`[Route("api/[controller]")]` + `PluginController`）。`publish-plugin.ps1` 结尾打印的 `/api/plugins/...` 是**错的**，实测 404。
- **假成功陷阱**：版本号来自 `plugin.json`，改 C# 代码时若入口 DLL 没真正替换，会出现「版本显示新值但跑旧二进制」。必须比对哈希：`publish/Plugins/<Dir>/<Dir>.dll` vs `_backups/<id>/<ver>/<Dir>.dll`（脚本已内置该校验）。
- **「版本号升了」≠「新代码生效」**：版本化布局（versions/ + current）只证明「切换动作完成」；Middleware/Controller 等宿主代码改动必须重启宿主（新二进制）后才生效；插件自身 DLL 生效判据 = 版本快照 DLL hash == staged hash。
- **web/dist 版本化读取**：`PluginFrontendFileMiddleware.ResolveFrontendRoot` 版本化优先（current 指针存在且 `versions/<current>/web` 存在 → 从版本快照读，否则回退扁平 `{插件目录}/web`）。
- **插件 config.json 手写键大小写**：插件 config.json 属用户可手改文件，`System.Text.Json` 默认大小写敏感——手写 `{"port":...}` 会被静默忽略、绑定回退默认端口。加载器必须 `JsonSerializerOptions { PropertyNameCaseInsensitive = true }`。

### 发布坑（反复踩，全量 build 前必读）
- 🔴 **宿主进程锁致 build.ps1 发布漏更宿主 DLL**：`build.ps1` 覆盖 publish/ 用 `Copy-Item -ErrorAction SilentlyContinue` 或 `robocopy /E`——**运行中宿主锁定的文件（ForgeSelf.dll）被静默跳过**，publish 里宿主 DLL 保持旧版 → 与插件同路由控制器**歧义** → 界面/API 500 `AmbiguousMatchException`。**教训**：① 替换宿主自身二进制前先 `Stop-Process -Name ForgeSelf` 释放锁；② 发布后核对 `publish/ForgeSelf.dll` 时间戳与 `ForgeSelf.Api/bin/Release/.../ForgeSelf.dll` 一致；③ 排查「插件控制器 500 且无 action 日志」优先怀疑**路由歧义/控制器残留**。
- 🔴 **PS 5.1 `Copy-Item 'dir\*' -Recurse` 通配符 bug 会静默漏拷**：`build.ps1` 第 3 步曾用 `Copy-Item (Join-Path $stagingDir '*') $publishDir -Recurse -ErrorAction SilentlyContinue` 在 PS 5.1 下**不拷全**（报错或 exit 0 假成功）。**修复**：改用 **`robocopy $stagingDir $publishDir /E`**（exit 0-7 均成功）+ 两处 robocopy 后 `$LASTEXITCODE = 0` 复位 + 脚本末尾 `exit 0`。**教训**：发布动作一律走 `run-plugin-publish-verify.ps1`（技能唯一入口），**禁止手动 Copy-Item / robocopy 进 publish/**。
- 🔴 **重复插件 id 目录致宿主启动崩溃**：`publish/Plugins` 同时存在两个 plugin.json 的 `Id` 相同目录 → `TopologicalSort` 撞 key → **宿主启动即崩**。发布/归档后检查 `publish/Plugins` 下 plugin.json `Id` 无重复；冗余目录移 `_backups/`（勿删）。
- 🔴 **活动插件目录/版本快照只放插件自身程序集**：**绝不能**放入 `XCode.dll`/`NewLife.Core.dll`/`NewLife.Agent.dll`/`NewLife.Remoting.dll`/`ForgeSelf.Abstractions.dll`/`ForgeSelf.Core.dll`/`Stardust.dll`。否则 `PluginLoadContext` 再加载一份 → 类型标识分裂（「插件类型未实现 IPlugin 接口」）+ ALC 卸载中加载 → `FileLoadException`，宿主**启动即崩**。脚本已内置白名单过滤 + 防御性清理。
- 🔴 **宿主运行时入口 DLL 被独占锁，无法覆盖**：`plugin.json` 与 `web/dist` 可热覆盖；`<Dir>.dll` 被 ALC 锁定，独占 open 报「being used by another process」，而 `Copy-Item` **误报**成 `FileNotFoundException`（排查时勿被误导）。改 C# 代码靠 side-by-side 版本化更新或停宿主。
- 🔴 **全量 `build.ps1` 会删外部放置的 `publish/Plugins/System.Data.SQLite.dll`**：`System.Data.SQLite.dll`+`e_sqlite3.dll` 不是任何 csproj/deps 的包依赖（NewLife.XCode 运行时**探测**该文件），是**外部放置的运行时构件**。全量重发后从旧 publish 或 `.temp/e2e/*/publish/Plugins/` 回补这两个 DLL 到 `publish/Plugins/`。
- **验证已部署前端资产必须用 `/assets/` 前缀路径**：`vite.config.ts` 的 `build.assetsDir` 默认把 JS/CSS 产物输出到 `wwwroot/assets/`，宿主 `index.html` 引用 `/assets/index-xxxx.js`。直接 `curl http://host/index-xxxx.js` 会 **404 误判"修复未上线"**。确认线上确为修复后构建的最稳妥办法：`diff <(curl -s http://host/assets/<file>) <(cat publish/wwwroot/assets/<file>)` 应 `IDENTICAL`。
- **发布目录运行时遗留坑**：`publish/` 下的 `Data/`、`Log/`、`Config/` 是**运行 exe 时动态生成在程序目录**（非 dotnet publish 拷贝）。发布脚本清理发布目录应**整目录删除重建**而非仅删文件。safe-delete shim 拦 `Remove-Item`：build.ps1 清 publish/Data|Log 被 fail-closed 拦退码 1（发布实质完成），并产生 `publish/publish/` 嵌套残留；规避 `-ErrorAction SilentlyContinue`。
- **PowerShell 数组 splat 是按位置传参**：`& $f @arr` 会把 `-PluginsRoot` 塞进前一个位置参数（实测撞上 `-Configuration` 的 ValidateSet）。跨脚本调用一律**显式具名传参**。
- **publish-plugin.ps1 子目录拷贝两处 bug（2026-09-23 修复）**：① `Split-Path -LiteralPath -Parent` 参数集冲突（-LiteralPath 不支持 -Parent）→ 改用 `[System.IO.Path]::GetDirectoryName`；② `FullName.IndexOf()` 在路径含 AppData 等包含 Data 的父路径时截错 → 改用 `Substring(.Length).TrimStart('\','/')` 按前缀长度精确截取相对路径。
- **运行宿主版本化布局判定**：插件热更新后 `versions/<v>/` + `current` 指针已切换、但活动根目录旧 DLL 被 ALC 占用 → 实际加载的仍是旧程序集。**判定**：`/api/plugin` 显示新版本 ≠ 加载新 DLL；须冷启动宿主后新 controller 才生效。
- **发布后必须冷启动验证**：插件整体改名（新 id 是新插件目录）或宿主核心改动后 watcher 不发现，必须冷启动（停 → 覆盖 → 起）。

### 宿主/插件运行形态（数据根与端口）
- **数据根二选一**：`ASPNETCORE_ENVIRONMENT=Development` → `程序目录/Data`，否则 → `~/.forgeself`；重启 51888 复用真实配置必须**不设** Development。NewLife 日志按 CWD 写（publish/Log），与数据根无关。
- **活动代码目录 vs 数据目录**：`publish/Plugins/<Dir>/`（dll + plugin.json + web/dist）= 宿主启动扫描 + 加载；`~/.forgeself/Plugins/<id>/` = 插件自有 sqlite/缓存（运行时生成，**不**放代码）。不要即兴：猜目录、擅自重启宿主、绕开技能自创流程 → 几乎必踩坑（曾把插件代码误装进数据目录）。
- **本地代理拦截 localhost 致宿主访问 LM Studio 502**：本机 `HTTP_PROXY=127.0.0.1:10808` 时，从该 shell 起的宿主进程会用代理访问 `localhost:1234`（LM Studio）→ 502。**宿主须带 `NO_PROXY=localhost,127.0.0.1` 启动**（curl/Playwright 同理）。
- **插件新目录两条生效路**：① `POST /api/plugin/install` 上传 `.forgeself-plugin` 包触发 `DiscoverPlugins()`；② **冷启动宿主**。运行中更新**已加载**插件的入口 DLL 会被 ALC 锁死（`File.Copy` 覆盖报「找不到文件」实为锁）——走 `POST /api/plugin/update/{id}` 或冷启动。
- **MCP 中心整合兼容决策**：环境变量前缀**保留旧名** `FORGESELF_MCP_GATEWAY_*`、类名保留（McpGatewayConfig/Server/...），仅 namespace/日志前缀/`serverInfo.name` 改——兼容既有运维/e2e 环境变量写法，避免破坏性改名。

## B6 PowerShell 工程坑（本项目高频）

- **铁律**：涉及中文的 PowerShell 脚本/写文件，绝不能直接用 `Get-Content`/`Add-Content`/here-string 管道。PS 5.1 默认按 ANSI 处理 → (1) 脚本自身解析失败（UTF-8 无 BOM 含中文 → 括号失配 → `ParserError`）；(2) 写入内容损坏。
- **修既有中文脚本首选「前置 UTF-8 BOM」**（字节级、**不重编码**、零内容损坏）：`$b=[System.IO.File]::ReadAllBytes($p); if(-not($b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)){ $n=New-Object byte[]($b.Length+3); $n[0]=0xEF;$n[1]=0xBB;$n[2]=0xBF; [Array]::Copy($b,0,$n,3,$b.Length); [System.IO.File]::WriteAllBytes($p,$n) }`
- 新脚本内容全英文（最省事）；读写文件用 .NET API（`ReadAllText($p,[System.Text.Encoding]::UTF8)` / `WriteAllText($p,$s,(New-Object System.Text.UTF8Encoding($false)))`）。
- **排查/验证**：`[System.Management.Automation.Language.Parser]::ParseFile($p,[ref]$null,[ref]$errs)` 看 `$errs.Count`；**必须用 `powershell`（5.1）而非 `pwsh` 复现**——PS Core 解析无 BOM UTF-8 正常，用 Core 验证会漏判。
- **本坑的实弹代价（2026-09-26 spec 036）**：`update-agent.ps1`（随发布包分发、由宿主用 powershell 5.1 拉起）UTF-8 无 BOM → 解析期崩溃、**连日志都写不出**，宿主自停后无人换文件重启，51888 实例直接下线。自动化守卫：`ForgeSelf.Api.Tests/RepositoryScriptTests.cs`（scripts/ 下含非 ASCII 的 .ps1 必须带 BOM），新增中文脚本先跑它。
- **PowerShell here-string `$x=@'...'@` 等号后必须换行**：`@'` 必须独占一行，凡模板/多行替换一律拆行写。
- **构造 JSON 请求体一律 `ConvertTo-Json`，禁止手工拼字符串/正则 hack**（少一层闭合括号 → JSON-RPC parse error 返回 error 而非 result → `Invoke-RestMethod` 返回 null 数组才暴露）。发送前先核响应结构（有 `result` 还是 `error`），再取字段。
- **安全删除钩子**：`Remove-Item` 被包装成"移到回收站"，锁文件会失败；`cmd /c "rd/del ..."` 等价删除会被 **safe-delete fail-closed** 直接拒绝（明确 "Do not retry"）。Agent 不可绕过；需物理删除时请用户手动执行。
- **pnpm 的 ignored-builds 会拦截 esbuild**：`pnpm install` / `pnpm run build` 报 "Ignored build scripts: esbuild" 直接失败。可用绕法：直接执行 `node_modules\.bin\vite.cmd build` 跳过 pnpm 的 deps-check。
- **脚本内避免复杂管道**：`Get-ChildItem -LiteralPath X -File -Recurse | ForEach-Object { }` 在**脚本内**报 `AmbiguousParameterSet`（同样语句在 `-Command` 下正常）。递归复制一律用 `Copy-Item -Recurse`。
- **PowerShell 追加文件后若再用 WriteAllText 覆盖整个内容，会丢掉刚追加的文本**（AppendAllText 与 WriteAllText 混用顺序错误）——追加类/内容后用 ReadAllText 校验。
- **核验 .NET DLL 内字符串必须原始字节 hex（UTF-16LE）或 ildasm，禁止 shell 中文字面量**：PowerShell→`python -c` 传中文搜索词跨进程编码损坏、UTF-8 解码 #US 堆都会假阴性。定论手段：`ildasm /text` 看 IL（`ldstr bytearray`）+ Python `bytes.fromhex(...) in data` 精确比对 UTF-16LE 字节。
- **PS 5.1 读写中文路径/内容用 .NET API + `-Encoding UTF8` 显式**；`Get-Content` 默认 ANSI 解码 UTF-8 无 BOM 文件显示乱码 ≠ 文件损坏（两文件哈希一致即文件正常）。
- **`Edit` 工具反复报 "File has not been read yet"**：改用 PowerShell `[IO.File]::ReadAllText/WriteAllText(UTF8Encoding(false))` 精确替换；若用 .Replace，替换后先 `if($t -ne $o)` 判断再写，命中失败打印 'NO MATCH'。

## B7 环境速查

- 后端端口 `:7102`（`ForgeSetting.config` PortNumber 可改；当前 publish 实例 51888）；前端 dev `:7002`（vs 旧 Stardust 6680/6681，勿混淆）。`vite.config.ts` 的 dev 代理固定指 7102——与 publish 实例的 51888 不冲突（dev 与 publish 是两种运行形态，dev 数据根在程序目录 `Data/`）。
- 运行模式：托盘应用（`publish/OpenForgeSelf.exe --console`），前端由后端 wwwroot 托管。
- **前端 UI 库是 Element Plus 2.14.3**（Vue 3.5.13 / vue-router 4.5 / pinia 3.0 / vite 6.2.4）。`.specify/memory/constitution.md` 写的"Naive UI"已过时，以 `package.json` 为准。
- 包管理器 pnpm；Node >= 20。全屏背景图用固定定位 `<img>`（非 CSS `background-image` 外链，本环境外链不渲染）。
- 认证：API 密钥 AES-256-CBC 加密存 ForgeSetting.config ApiToken，`ApiKeyPolicy` Bearer 校验。数据库：SQLite + EF Core + NewLife.XCode 并存；插件有各自 DB 文件。
- 托盘：`TrayIconManager`（H.NotifyIcon），右键菜单含「铸己匣」标题、打开主界面、检查更新、服务管理、关于、退出；「退出」用 Task.Run 避免消息循环死锁。
- 前端构建：`pnpm run check`（vue-tsc + eslint）为提交门禁；CI workflow 已配置。

## B8 架构要点

- **架构设计/变更统一走 `architecture-design` 技能**（`.agents/skills/architecture-design/SKILL.md`）：先查依据（调研 `docs/06-research/001` → 既有架构文档 → ADR/台账），不足再调研并回写调研文档；禁止脱离依据自作设计。铁律：契约入 Abstractions；禁止共享文件/静态类当跨插件契约；插件服务取宿主契约一律 ctor 注入 `IContext` + `ctx.Get<T>()`（禁缓存实例）；高风险先升级。
- **宿主→插件能力供给三层模型**（设计：`docs/01-architecture/host-capability-seams.md`）：L1 服务契约（pull，默认主力：契约在 Abstractions + Provider eager `ctx.Register` + 消费 `ctx.Get` 软依赖 null 降级）；L2 事件通知（push 补充）；L3 waterfall/serial 拦截管道（特例，多方竞争改写才用）。选型决策树：能降级→L1 停；需实时→补 L2；需拦截改写→L3；缺了不该启动→硬依赖 Consumes（缓建）。
- **插件拆分模板**：参照 MemorySystem——csproj = EF Core 包（10.0.8）+ `NewLife.Core` + `NewLife.XCode` + FrameworkReference Microsoft.AspNetCore.App + 引用 Core/Abstractions；plugin.json 需 `EntryAssembly="X.dll"`；`Apply(IContext)` 内 `DAL.Create(ConnName)` 探活。System.Diagnostics.PerformanceCounter 仅 SystemMonitor 需要。
- **拆插件独立程序集后测试宿主必须同步插件 DLL**：Backend 的 `Stage*`（AfterTargets=Build）只复制插件 DLL 到 Backend 自身输出，**不流入 Tests 输出**。Backend.Tests.csproj 需 `StagePluginDllsToTestOutput` 目标（AfterTargets=Build，从 Backend bin 按 `%(RecursiveDir)` 同步到 `$(OutDir)Plugins\`），否则插件加载失败、其 DI 服务未注册 → 控制器 GetRequiredService 抛异常（WorkflowEngine 实测）。
- **宿主 `IAIService` 无工具调用能力**：要做「工具循环 + 按 chatModelId 路由 + 流式」只能复用宿主 `IAIProvider`/`AIProviderRegistry`（已下沉 Abstractions + `HostProvidedServiceContracts` seed），插件 `ctx.Get<IAIProviderRegistry>()` 解析；`chatModelId`（`provider:upstreamId`）剥 `:` 前缀取上游模型 id。
- **图片识别缓存**：统一网关 `/v1/chat/completions` 多模态图片识别 = 逐图识别+逐图缓存。`IImageRecognitionCache` → `LocalFileImageRecognitionCache` 存 `ContentRootPath/Data/ImageRecognitionCache/<会话键>/<sha256>.json`（临时文件+原子改名；会话键非法字符替换、截断 64、防路径穿越）。缓存键 = SHA256(视觉模型id|来源|detail或mediaType|url或base64)。**失败占位结果不写缓存**，下轮重试。当前无过期/清理策略。
- **聊天记录实时流式**：`ChatRecordStreamRecorder` 采用 tee 流——代理 LLM 流式响应时一边回给调用方，一边按「每满 1000 字符或 500ms」节流 `UpsertRecordAsync` 增量落库（同一 `RequestId` 首插后更新），并通过 `/ws` WebSocket 推送 `chat_record_chunk` / `chat_record_completed`。复用现有原生 WS 通道（`wsService`），不引入 SignalR。
- **流式 token 用量（usage）透传铁律**：三段任缺一段都丢——① provider `OpenAIStreamChunk` DTO 必须含 `usage`（空 `Choices` 哨兵分片不能 `continue` 丢弃）；② 网关 DTO 必须含 `usage` 且 `HandleStreamAsync` 透传到 SSE 分片（哨兵保持 `choices:[]`）；③ 上游仅在 `stream_options.include_usage=true` 时才回 usage——provider 对 `Stream=true` **强制 `include_usage=true`**。回归测试：`UnifiedAIGatewayIntegrationTests.ChatCompletions_Streaming_PropagatesUsageField`。
- **插件 SSE 事件序列化必须 camelCase**：控制器 `JsonSerializer.Serialize(payload)` 默认 PascalCase，嵌套对象（如 `UnifiedUsage.PromptTokens`）会输出 PascalCase，前端按 camelCase 解析恒为 0。**统一 `new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }`**。
- **证书/加密数据加载失败 → 容错重建不整体禁用**：插件初始化路径上的持久化证书/加密数据加载失败 → **catch 后改名保留原件（`.bad-<时间戳>`，不删除）→ 重建 → 告警**，不得让单点失败禁用整个插件（`CryptographicException: 指定的网络密码不正确` = `X509Certificate2(pfx, password)` 密码不匹配/文件损坏的典型错误）。
- **`CertificateRequest.CreateSelfSigned` 返回的证书已含私钥**：再 `cert.CopyWithPrivateKey(rsa)` 抛「The certificate already has an associated private key」。直接 `cert.Export(X509ContentType.Pfx, pwd)` 即可。
- **WinDivert 部署结构（FlowForge）**：`WinDivert.dll` 必须与 `WinDivert64.sys` 同在 exe 根目录（csproj `<None Include>` 加 `<TargetPath>X</TargetPath>` 落到根目录）；驱动文件被内核锁定删除报访问被拒，`sc stop WinDivert` 需管理员；驱动文件不随业务代码变化时跳过 sys 覆盖即可。

## B9 测试覆盖与功能规格

### 真机走查（playwright.live.config.ts 打运行中实例）
- 🔴 **live 配置不走 globalSetup**：`helpers/real-auth.getRealApiKey()` 的取值优先级是 `E2E_API_TOKEN` > 最近一次 globalSetup 落盘的 `.temp/e2e/<ts>/state.json`。对 51888 这类常驻实例跑 live 用例**必须显式注入当前真实密钥**（`E2E_API_TOKEN=$(node scripts/get-forge-token.cjs …)`），否则读到过期 state token → 401 空响应 → 「Unexpected end of JSON input」假象（spec 036 首轮实测）。
- 破坏性 live 用例（会重启/换版实例）必须双显式门控（env 开关 + 显式 token），普通套件绝不命中；见 `e2e/update-live-apply.spec.ts` 的 `test.skip` 模式。

### 功能测试覆盖（e2e，2026-08-06 归档）
21 项功能全覆盖、36 用例全通过（对接真实后端 + 真实认证，绝不 mock）：

| # | 功能 | e2e 文件 |
|---|------|----------|
| 1 | 首页 | app.spec.ts |
| 2 | 聊天 | chat.spec.ts |
| 3 | 待办 CRUD | todo.spec.ts |
| 4 | AI 提供方 | ai-providers.spec.ts |
| 5 | AI 提供者全流程 | ai-provider-chat.spec.ts |
| 6 | API 服务器/端口/Token | port-config + token-*.spec.ts |
| 7 | 快捷链接 CRUD | quicklinks.spec.ts |
| 8 | 聊天记录 | chat-records.spec.ts |
| 9 | 插件商店 | plugin-store.spec.ts |
| 10 | API 网关 | api-gateway.spec.ts |
| 11 | SPA 回退 | spa-fallback.spec.ts |
| 12 | Prompt/技能 | prompts-skills.spec.ts |
| 13 | MCP 工具 | mcp-tools.spec.ts |
| 14 | 记忆系统 | memory.spec.ts |
| 15 | Agent 管理 | agents.spec.ts |
| 16 | 系统监控 | system-monitor.spec.ts |
| 17 | 代码片段 | code-snippets.spec.ts |
| 18 | 工作流 | workflows.spec.ts |
| 19 | 个人中心 | profile.spec.ts |
| 20 | AI Agent | ai-agent.spec.ts |
| 21 | 全部功能/插件子页 | all-features-plugins.spec.ts |

**测试驱动修复**：memoryApi POST、MemoryView storeToRefs、后端软删除过滤、systemMonitorApi 路径/解包/字段映射、AgentType/source 数字枚举、listSnippets/listWorkflows 解包、chatApi 路径与请求体契约。

### 已完成功能规格（specs/，tasks 全部 [X]）
| Spec | 功能 | 任务 |
|------|------|------|
| 001 | AI 提供方配置数据库化 | 25/25 |
| 002 | Provider 模型列表管理 | 35/35 |
| 003 | API 服务器设置 | 22/22 |
| 004 | 模型列表与网关集成 | 25/25 |
| 005 | 待办追踪插件 | 32/32 |
| 007 | 背景图可见度/透明度 | 33/33 |
| 008 | 托盘服务与自动更新 | 19/19 |
| 009 | Web 端口与 Token 安全 | 23/23 |

（006-set-background-image 仅有 spec.md，未生成 tasks）

## B10 CI 自动发布（tag → GitHub Actions，2026-09-26 落地）

**入口**：`git tag -a v<X.Y.Z> -m "发版说明" && git push github v<X.Y.Z>` → Actions 自动构建打包并创建 GitHub Release（xxred/OpenForgeSelf，当前 **private**）。

**设计契约（用户拍板）**：workflow 只做「装工具链 + 调脚本」，全部发布动作封装在 `scripts/release/*.ps1`，本地与 CI 跑同一条命令——`pwsh scripts/release/release-local.ps1 -Version v0.1.0`。

| 脚本 | 职责 |
|------|------|
| `release-local.ps1` | 一键编排：前端→宿主 publish→打包→发版说明（= CI 唯一构建入口） |
| `build-frontend.ps1` | 宿主 web（→`ForgeSelf.Api/wwwroot`）+ 8 个插件 web（→`Plugins/<X>/web/dist`），逐包 `pnpm install --frozen-lockfile`；`-HostOnly/-PluginsOnly/-Plugin` 分步调试 |
| `publish-host.ps1` | `dotnet publish -c Release -r win-x64 --self-contained true -p:Version=<ver>`（下载即用 exe，无需装 .NET） |
| `package-release.ps1` | 注入 `build/runtime/Plugins` 运行时构件 → 清 Data/Log/Config/_backups/pdb → zip + SHA256SUMS；`-Sign` 可选接 sign-publish.ps1 |
| `make-release-notes.ps1` | tag 注解 + 上一 tag 以来 commit 生成 RELEASE-NOTES（需 fetch-depth 0） |
| `publish-release.ps1` | `gh release create`（tag/资产/notes）；本地凭 gh keyring，CI 凭 `secrets.GITHUB_TOKEN`；版本含 `-` 后缀自动 prerelease |

**硬规则与坑（本轮实战）**：
- 🔴 **System.Data.SQLite.dll / e_sqlite3.dll 必须入库**（`build/runtime/Plugins/`，打包时注入 `Plugins/`）：XCode 运行时探测文件、非 NuGet 依赖，历史上只手工放在 `publish/`，干净构建必缺 → 发布版 SQLite 崩。
- 🔴 **`.github/` 曾被 .gitignore 屏蔽**（历史清理误伤），workflow 必须入库才生效——已在 .gitignore 解除并注释。
- 宿主 SPA 与插件 `web/dist` 全部是 gitignore 的生成物：CI 必须先跑 `build-frontend.ps1` 再 `dotnet publish`（`StageAllPlugins` 只拷已存在的 dist）。
- 单实例 Mutex 挡冒烟测试：本机已跑 publish 宿主时，起第二个实例用 `FORGESelf_INSTANCE_ID=smoke`（Program.cs:47）；exe 固定监听 7102，忽略 ASPNETCORE_URLS。
- PowerShell：`Invoke-ReleaseStep { … }` 的 scriptblock 内对脚本级变量赋值**不回传**（子作用域），路径等结果须在块外先算好；`gh run watch` 非交互必须显式传 run-id，否则立即退出且管道后 exit 0 假成功。
- 🔴 **CI Node 必须 ≥22**：DesignSystem 插件 `web/package.json` build 直跑 `node scripts/gen-tokens-css.ts`，依赖 Node 原生 type-stripping（Node 20 报 `ERR_UNKNOWN_FILE_EXTENSION`）。本地 Node 26 掩盖了此依赖；workflow `setup-node` 已钉 22。
- 网络受限环境取 CI 日志：`results-receiver.actions.githubusercontent.com` 可能不可达（`--log-failed` 失败），改用 `gh api repos/<o>/<r>/actions/runs/<id>/logs > ci.zip` 下载解压按步骤 txt 定位。
- 重打测试 tag：`git tag -d` + `gh api -X DELETE .../git/refs/tags/<tag>` + 重新 `git tag -a` 推送即可再触发（`gh run rerun` 会复用旧 tag commit 的 workflow，改了 workflow 时**不要用 rerun**）。
- 产物实测：self-contained zip ≈ 74MB / 562 文件；windows-latest 全流程 ≈ 6-10 分钟。
- 验收流：测试 tag（如 `v0.0.0-ci-test`，自动标 prerelease）先跑通 → 下载 Release 资产核对 SHA256 → 再打正式 tag。
- 🔴 **git push github 走系统代理**：外网通时 `git push github` 直连失败（Recv failure / 443 不通）而 `gh` 可用——gh 走 WinHTTP 代理、git 不走。绕行：`HTTP_PROXY=http://127.0.0.1:10808 HTTPS_PROXY=同值 git push github <ref>`（端口以 `netstat` 实测本机代理为准）。持久化可 `git config --global http.https://github.com.proxy http://127.0.0.1:10808`（用户侧决定，勿擅改全局）。
- 🔴 **GitHub 私有仓库下载资产必须用资产 API 直链**（`api.github.com/repos/<o>/<r>/releases/assets/{id}` + `Accept: application/octet-stream` + Bearer）；`browser_download_url`（github.com/…/releases/download/…）带 PAT 会 **404**。单测 mock fixture 必须同时含 `url` 与 `browser_download_url`（spec 036 教训：fixture 缺 `url` 字段导致 13 项单测全绿没拦住线上 404）。
- 🔴 **「启动后台任务 + 前端轮询」协议必须在持锁临界区内预置首个进行中状态**：`StartDownload` 旧实现先 `Task.Run` 再由任务置 `downloading`，POST 响应/首轮轮询读到 `checked` → 前端把 checked 当终止态永久停轮询 → UI 等不到「重启并更新」（51888 实机两次复现，4ef4b5c 修 + 回归单测）。泛化：**状态机对外可见的状态序列不允许出现协议里的"终止态"夹在启动与进行中之间**。
- 🔴 **并发写路径下，状态机每一侧的写入（不只启动侧）都必须守卫「在途状态」**：CheckAsync 完成时无条件 `Set("checked"/"idle"/"failed")`，会覆盖在途的 downloading → 前端又停轮询（51888 第 6 轮插桩实锤：`/download→downloading` 后 2 秒 `/progress→checked`；6b09654 修：首写 checking 与终写均在 lock 内、当前状态 ∈ {downloading,verifying,extracting,ready,applying} 时跳过回写 + 红灯回归单测）。检查/下载两条并发路径写同一状态机时，**终止态回写必须条件化**，且单测必须构造真实并发时序（慢速 mock HTTP + 交错调用）而非顺序调用。
- **随包分发的 .ps1 由 powershell 5.1 拉起时同样受 BOM 铁律约束**（见 B6）：update-agent 无 BOM → 解析崩、日志写不出、宿主自停后无人重启，实例整段下线。
- `appsettings.json` 有明文 ApiKey 入历史：**仓库转 public 前必须先处置**（见 TODO 批次 D）。

---

## Part C — 变更记录

| 日期 | 变更 |
|------|------|
| 2026-09-26 | spec 036 自动更新落地：B6 补 update-agent BOM 实弹代价与守卫测试；B9 新增「真机走查」小节（live 配置必须显式 E2E_API_TOKEN、破坏性用例双门控）；B10 补 git push 代理绕行与私有仓库资产 API 直链下载两条硬规则。 |
| 2026-09-26 | spec 036 端到端验收全绿（live 一次性 49.5s，0.1.0→v0.2.4）：B10 补「并发写路径下状态机每侧写入都要守卫在途状态」硬规则（D-036-5，6b09654）。 |
| 2026-09-24 | 本文档创建：Part A 承接 AGENTS.md 触发式细节；Part B 承接原 `.forgeself/memory/MEMORY.md` 项目不变规则归档（随 docs 入库）；AGENTS.md 瘦身为「每次必守 + 引用本文」；MEMORY.md 改为会话级索引。
