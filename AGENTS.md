# OpenForgeSelf — AI Agent 工作规则（Loop Engineering）

本文件是 AI Agent 在 OpenForgeSelf（铸己匣）项目中的**每次必守工作手册**（每次任务循环都必须执行的内容）。
采用 **Loop Engineering** 闭环模型：Goal → Context → Plan → Execute → Verify → Iterate，直到验证通过或升级给人。
所有**开发类任务**的流程**一律按 §11 AI-Native Engineering 九阶段闭环执行**（Repository Understanding → Intent → Spec → Plan → Task → Implement → Test → Evidence → Review；规范 `docs/04-standards/ai-native-engineering-workflow.md`，独立自含、闸门1/2/3 见其 §1.1）。该规范与其他流程描述冲突时，**以 §11 为准**（用户指令 seq14：不做体系映射）。

> **分层说明**：本文只列「每次都要遵守」的规则；「需要触发才用」的细节（技能职责边界、验证决策表、迭代细则、文档工作流操作规范、汇报完整模板、设计稿与 speckit 流程、项目工程规则与踩坑）一律引用 **`docs/04-standards/agent-workflow.md`**（Part A = 工作流细节，Part B = 项目不变工程规则，2026-09-24 起原 `.forgeself/memory/MEMORY.md` 的规则已归档于此，随 git 入库）。

---

# §0 预飞铁律（每个回合强制 · 不可跳过 · 任务大小不构成例外）

收到用户消息后严格按顺序执行；前一步未达成，不得进入下一步：

1. **【首动作·写日志】** 在调用任何读代码/搜索/编辑工具之前，先向当天工作日记追加一行：`.forgeself/memory/YYYY-MM-DD.md`（不存在则创建；目录跟项目走，换任何 agent 均可读写）。内容=输入原文（用户原始任务提示）+ 任务拆解清单（1-3 行）；**完成一项就把 `- [ ]` 勾为 `- [x]`**。
   ⛔ 未写此行，禁止调用任何文件读/写工具。
2. **【建待办】** 在 `TODO.md` 新建或引用一条待办（🔄 进行中，标 P1>P2>P3，来源:输入N）。**开发过程中发现的其他问题，当场记入 `TODO.md` 待办区（标 P1>P2>P3，来源:输入N），本次会话不顺手解决**（范围控制见 §1.3）；任务完成时该待办勾选归档到工作日记并**移除出 `TODO.md`**，保持队列新鲜不爆炸。
   ⛔ 无对应 TODO 项，禁止修改任何业务文件。
3. **【查技能】** 任务拆解后从技能目录（§2.4）挑一个流程执行：有对应技能则**先读再动手**；**技能清单无对应流程时，先创建一个技能流程（沉淀为 `.agents/skills/<name>/SKILL.md`，按 skill-creator 规范）→ 执行 → 持续迭代完善**；同类任务下次重复出现时，先选既有技能流程。
   宿主/插件类任务必须读 `plugin-development`（及其转派的专项技能）。
   ⛔ 未读技能就改宿主/插件代码，视为流程违规——历史上已因此漏发插件、漏跑插件层 e2e。
4. **【Context → Plan → Execute → Verify】** 按第 1-5 节执行；Verify 必须真跑（前端 `pnpm run check`+`pnpm run test` / 后端 `dotnet build`+`dotnet test`）。
5. **【出口·完成检查清单】** 回复"完成"前，必须：
   - Read 当天日记，确认含本次输入拆解 + 验证结果；
   - 确认 `TODO.md` 该待办已**从队列移除**（完成即移除，不留 ✅/[x] 堆积）；并确认相关文档（spec / `docs/` / README）已同步更新；
   - （如有可复用规律）已沉淀到 `docs/04-standards/agent-workflow.md` 对应小节；本次走通的流程若技能缺失或可优化，已回写/新建技能（§2.4 登记）；新增项目不变规范已入 agent-workflow.md 或本文。
   - 【插件任务硬性门禁】若本次改动涉及 `Plugins/<X>/web/` 或插件本体（`.cs` / `Controllers` / `Services` / …），须跑完 `plugin-development` §四 维护闭环**全部四步**：① 门禁（插件前端 `cd Plugins/<X>/web && pnpm run build` / 后端 `dotnet build`）② 插件层 e2e（`e2e/plugins/<id>`，实跑宿主走查用 `playwright.live.config.ts`）③ 发布到运行宿主（`scripts/publish-plugin.ps1` 或按 `plugin-publish-verify` 主路径热更）④ 浏览器走查（导航宿主、按用户视角点一遍、截图读图、清测试数据）。**四步缺一不可，缺失即视为未完成，禁止回复"任务完成"**。此四步属闭环内部标准动作，已由用户前瞻授权"以后自动执行"（含发布到实跑 `:51888` 实例、触碰运行服务），不再逐次询问；唯环境拉不起 / 外部阻塞时才升级给人。
   ⛔ 任一项不满足，禁止回复"任务完成"——先补齐再回。

> 验证通过（测试绿）≠ 流程完成。Persist 与 Verify 同属 DoD，缺一项即未达标。

### 🚫 红线（不可逾越 · 违反即流程违规）

**验证 / 截图 / 浏览器驱动类需求，禁止手写一次性 `temp/*.cjs`（或散落脚本）作为验证手段。**
必须走 §5.0 项目唯一测试体系（Playwright e2e / vitest / dotnet test）。历史教训：spec 006 期间在 `temp/` 产 49 个一次性 `.cjs`（截图/视觉校验/浏览器驱动），验证完即废、无版本控制、无法回归；且 `temp/browser_drive/round1.cjs` 的启发式判定（`发送键可用+文本>500`）曾漏掉 agent 真实 `write_file` 调用，误导结论。

- 临时探针**仅可作探索期一次性使用**：用完即删、**不进版本控制、不留存 `temp/`**，且**不得作为验证结论**（结论必须沉淀为 §5.0 的可重复测试）。
- 任何"我手跑个脚本看看"的冲动，先查 §5.0 决策表：有对应正规入口就走正规入口。

**开发类任务禁止跳过 AI-Native 闭环工件直接写代码。**
每个开发任务（新功能 / 缺陷修复 / 插件任务 / 宿主与 CI 变更）的流程**唯一依据是 §11 规范**（模板 `docs/18-templates/ai-pilot/`，产物 `docs/ai/pilot/<task-id>/`；独立自含，不与其他流程体系做映射）；Evidence 或 Review 缺件即视为未完成。纯问答 / 查状态不占工件链。

核心原则：

1. **可验证性优先** — 任务是否完成由自动化检查判定，不靠"看起来对了"。
2. **失败是常态，回滚是基本功** — 每轮变更前确认可恢复点，验证失败立即回滚。
3. **人在回路上，不在回路中** — Agent 自主执行闭环，仅在超出能力时升级给人。
4. **重复动作脚本化优先** — 凡重复执行或可能多次重复执行的动作：优先固化为可复用脚本（纳入版本控制、可重复运行，禁止散落 `temp/` 或每次临场重写），形成技能 SOP 次之（沉淀为 `.agents/skills/<name>/SKILL.md` 并按 §2.4 登记）。判断线：同一动作出现第二次就该固化，而非等到第 N 次。

---

## 1. Goal — 目标与完成定义

### 1.1 任务来源
按优先级从高到低：用户直接指令（最高优先级）→ `TODO.md` 未完成条目 → `specs/` 当前阶段待实现任务 → Verify 环节发现的失败项。

### 1.2 完成标准（Definition of Done）
一个任务"完成"当且仅当：
- 对应 Verify 门禁全部通过（见第 5 节）
- 变更范围未超出任务边界（不顺手改无关代码）
- 若涉及 UI，与设计稿视觉对齐已确认
- 新增/修改的行为有对应测试覆盖
- TODO.md 或 specs 中对应条目已更新状态

### 1.3 范围控制
- 做且只做明确要求的事；发现的额外问题记入 `TODO.md`，不自行扩展。
- 单次变更尽量原子化：一个任务一个 commit（或一组紧密相关的 commits）。

---

## 2. Context — 上下文收集

### 2.1 项目地图
| 目录 | 用途 | 技术栈 |
|------|------|--------|
| `ForgeSelf.Web/` | Vue 3 SPA | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia + pnpm |
| `ForgeSelf.Api/` | ASP.NET Core API | .NET 10 + SQLite + NewLife.XCode（唯一 ORM）+ 插件架构 |
| `ForgeSelf.Api.Tests/` | 后端测试 | xUnit + Moq + FluentAssertions + Coverlet |
| `forgeself-design/` | 设计原型 | 自包含 HTML + Element Plus CDN + Tailwind CDN |
| `specs/` | 功能规格 | 编号 001–005，spec → plan → tasks 流程 |
| `openwiki/` | 自动生成文档 | 勿手动编辑，由 CI 刷新 |

### 2.2 收集清单
开始编码前按需确认：**查 §2.4 技能清单，读取对应技能**（宿主/插件任务必读）→ 阅读相关 spec → 查看设计稿（`forgeself-design/pages/`）→ 用 CodeGraph 或 grep 定位相关代码 → 阅读待修改文件的当前内容（不凭记忆改代码）→ 确认 `TODO.md` 中该任务的上下文和约束。

### 2.3 关键约定速记
- 包管理器：**pnpm**（不是 npm），Node >= 20
- 前端样式体系：Element Plus 官方 `--el-*` 变量 + Tailwind 布局原语，不定义独立色值
- 设计稿与前端共享 `themes/` 下的 tokens（单一来源）
- 后端插件通过 `Plugins/` 目录 + `plugin.json` 清单注册
- 运行端口：Backend `:7102`，Frontend `:7002`；本环境长期运行的 publish 实例用 `:51888`
- 更全的工程规则/踩坑（数据落盘、XCode、DLL 锁、PS 编码等）→ `docs/04-standards/agent-workflow.md` Part B

### 2.4 技能清单（Skills — 动手前必查）
技能位于 `.agents/skills/<name>/SKILL.md`。**涉及宿主 / 插件的任务，动手前必须先读对应技能**——历史上不止一次因为没读技能而漏掉发布、漏掉插件层 e2e。

| 技能 | 何时用 | 关键约束 |
|------|--------|----------|
| `plugin-development` | **插件任务总入口**：新建插件、把宿主页面迁移成独立插件、改完插件不知还要做什么 | 改完 = 门禁 + 插件 e2e + 发布 + 浏览器走查，四步缺一不算完成；完成后复盘回写技能 |
| `plugin-feasibility-study` | **新建插件第一步**（先于 `plugin-development`）：调研 → 可行性报告 → 设计方案 → **命名** → 决策拍板 | 不许直接开写代码；命名在功能定稿之后，须过「名实相符三问」 |
| `plugin-frontend-scaffold` | 从 AIAgent 模板生成插件 `web/` 前端骨架 | 产物入口固定 `web/dist/index.js`，导出名须等于 `views[0]` |
| `plugin-publish-verify` | 发布单插件到运行中的 publish 宿主 + 验证 | 宿主必须跑 `publish/`；活动插件目录只放插件自身 DLL；`plugin.json` 最后拷 |
| `e2e-testing` | 插件层 e2e（`e2e/plugins/<id>/<id>.spec.ts`）+ 截图读图 | 零 mock；禁止用一次性临时脚本代替 |
| `architecture-design` | 影响面较大的架构/设计决策 | 先查依据（调研/ADR/既有设计），禁止脱离依据自作设计 |

**选型顺序**：先判断「是不是插件任务」→ 是则先读 `plugin-development` → 它会转派到 `plugin-feasibility-study`（**新建插件**时）/ `architecture-design`（涉及契约与内核接缝时）/ `plugin-frontend-scaffold` / `plugin-publish-verify` / `e2e-testing`。职责边界、新建插件硬顺序、登记规则、技能缺失策略 → `docs/04-standards/agent-workflow.md` §A1。
**新增技能必须同步登记到本节**（技能不在表里 = 等于不存在，后续会话必然再次漏读）。

---

## 3. Plan — 规划

- **任务分解**：收到任务后先分解为可独立验证的步骤（`任务 → [步骤1: 改什么文件, 预期什么结果] → ... → [步骤N: 运行 Verify 全部通过]`）；每步应满足改完后可立即运行检查命令确认对错。
- **影响评估**：本次变更影响哪些文件/模块？是否涉及公共接口（API 契约、组件 props、路由）？是否涉及数据库迁移（必须可回滚）？是否涉及设计稿对齐（先截图确认当前状态）？
- **风险分级**：低（组件内部调整，正常执行）/ 中（公共组件/API 变更，执行前确认影响范围 + 全量 Verify）/ 高（数据库迁移/依赖升级/架构调整，升级给人确认后再执行）。

---

## 4. Execute — 执行

### 4.1 安全约束
- **变更前备份**：确认项目在 Git 管理下（可 `git diff` 回溯）；若非 Git 管理，先备份原文件。
- **禁止永久删除**：不 `rm`/`del` 用户文件，需要删除时移入回收站或 `.trash/`。
- **敏感路径禁触**：`.env`、`appsettings.Production.json`、含密钥的配置文件 — 只读，不修改。
- **变更范围限制**：单次执行修改文件数不超过任务所需，不顺手重构无关代码。

### 4.2 编码规范（精华）
**前端**：组件拆分独立 `.vue`；样式走 `--el-*` 变量 + Tailwind；**禁止显式 `import { ElXxx } from 'element-plus'`**（type 导入除外，统一模板 `<ElXxx>` 自动解析）；新代码必须有 TS 类型不用 `any`；TDD 优先；全屏背景图用固定定位 `<img>`（不用 CSS 外链 background-image）。
**后端**：遵循 Controllers/Services/Entities 分层；新插件按 `Plugins/` + `plugin.json` 注册；异步统一 `async/await` 不 `.Result`；TDD 优先。
**执行记录**：每步记录改了什么、运行了什么、结果是什么；失败记录错误信息全文，作为 Iterate 输入。
（完整规范与更多坑 → `docs/04-standards/agent-workflow.md` §A3 / Part B）

---

## 5. Verify — 验证门禁

验证是闭环的核心。不通过 Verify 的变更 **不算完成**。

### 5.1 前端验证（修改 `ForgeSelf.Web/` 后必须执行）
```bash
cd ForgeSelf.Web
pnpm run check    # vue-tsc --noEmit && eslint（类型检查 + 代码规范）
pnpm run test     # vitest（单元测试）
```
判定：`check` 失败 → 先修类型错误再修 lint；`test` 失败 → 修测试或补测试，**不允许跳过**；两项都过 → 前端验证通过。

### 5.2 后端验证（修改 `ForgeSelf.Api/` 后必须执行）
```bash
cd ForgeSelf.Api
dotnet build
```
后端测试（涉及逻辑变更时必须执行）：
```bash
cd ForgeSelf.Api.Tests
dotnet test
```
判定：构建失败 → 修编译错误；测试失败 → 修代码或修测试（不允许删除测试来"通过"）；构建+测试通过 → 后端验证通过。

### 5.3 测试方式铁律（项目唯一测试体系，禁止另起炉灶）
- **验证 / 截图 / 浏览器驱动类任务 → 写成 Playwright e2e 用例**（`ForgeSelf.Web/e2e/**/*.spec.ts`），走 `e2e-testing` 技能 SOP（globalSetup 自动构建宿主 + 起 publish 宿主 + 解密真实 token，零 mock）。
- **纯逻辑 / 组件行为 → 写成 vitest 单测**（`src/**/*.spec.ts`）。
- 一次性 `.cjs` 脚本只可作「探索期临时探针」，**不得作为验证手段提交或长期依赖**；结论必须沉淀为可重复测试，探针用完即删、不进 `temp/` 留存。
- e2e 视觉检查按 `e2e-testing` 技能 Level 3 清单读图核对，截图固定落 `ForgeSelf.Web/screenshots/e2e/<插件id>/`。
- **需求 → 该跑什么的完整决策表**（含仓内工具 `scripts/get-forge-token.cjs` / `scripts/probe-dll-string.cjs` 的正规入口）→ `docs/04-standards/agent-workflow.md` §A4。

### 5.4 设计对齐验证（涉及 UI 变更时）
打开对应设计稿（`forgeself-design/pages/xxx.html`）对比，条件允许时截图对比；布局/间距/颜色须与设计稿一致，组件交互行为须与 spec 描述一致。

### 5.5 验证严重级别
**Critical**（构建失败/测试失败/类型错误）→ 必须修复否则不完成；**Warning**（ESLint warning/覆盖率低）→ 记录 TODO 不阻断；**Info** → 记录不处理。

---

## 6. Iterate — 迭代控制

```
Verify 失败
  ├─ 第 1 次失败 → 分析错误，调整实现，重新 Execute → Verify
  ├─ 第 2 次失败 → 换策略（不同实现路径），重新 Execute → Verify
  ├─ 第 3 次失败 → 回滚到最近通过状态，升级给人
  └─ 任何时候出现非预期副作用 → 立即回滚，升级给人
```
- **Bug 修复走 TDD**：Red（写测试复现，必须失败）→ 确认红灯 → Green（最小化修复）→ 确认绿灯 → 回归（完整 Verify）。禁止直接改实现代码。
- **升级规则**（必须停止循环升级给人）：连续 3 次验证失败无新思路 / 高风险操作（数据库迁移、API 契约破坏性变更、主依赖大版本升级）/ 环境问题 / 任务歧义 / 需装新依赖或改构建配置。
- 循环终止：正常（Verify 通过 → Persist）/ 异常（触发升级）/ 超时（单任务迭代超 5 轮无进展 → 升级）。
- 详细流程（回滚策略等）→ `docs/04-standards/agent-workflow.md` §A5。

---

## 7. Persist — 经验沉淀

每轮循环结束后（无论成功或升级）：
1. **更新 TODO.md**：完成的待办**立即移除**（不留 ✅ 历史堆积，保持队列新鲜）；仅记录新发现的问题与未完成项
2. **更新 specs 状态**：若任务对应某个 spec 的 task，标记进度
3. **记录决策**：重要技术决策写入对应 spec 或 TODO.md，格式：决策 → 背景 → 理由 → 准则
4. **沉淀规律**：可复用规律（约定/踩坑/ADR）→ 写入 `docs/04-standards/agent-workflow.md` 对应小节（入库）；走通的流程技能缺失或可优化 → 回写/新建技能并登记 §2.4；新形成的项目不变规范 → agent-workflow.md 或本文

---

## 7.5 文档工作流 — 必须严格遵守（换 AI 也照此执行）

> 目标：**TODO.md 永远是"今天和明天"的活队列**（不随迭代膨胀）；工作日记是"昨天"的归档；MEMORY 是"会话级索引"；docs 是"永远"的规则库。三者靠「检视回流」闭环。
> 完整操作规范（强制顺序图、操作细则、输入档案规则、反同步 SOP）→ `docs/04-standards/agent-workflow.md` §A6。

### 7.5.1 四文件职责（角色分工）
| 文件 | 角色 | 内容 | 写入时机 |
|------|------|------|----------|
| `TODO.md` | **队列**（今天/明天） | 🔄进行中 / ⬜待办（优先级 P1>P2>P3 + 来源引用）/ ⏸阻塞（记录不修） | 新输入解析出待办时；待办状态变化时；完成即移除 |
| `.forgeself/memory/YYYY-MM-DD.md` | **日志**（昨天） | 当天完成内容+验证结果、决策记录、遇到的问题与解决、输入原文与任务拆解、**「下一步」字段**（回流连接器） | **输入到达立即写**；执行过程中随做随记；完成时补全 |
| `.forgeself/memory/MEMORY.md` | **会话级索引** | 项目规则索引（指向 agent-workflow.md）+ 未沉淀的临时规律；**不再承载不变项目规则**（`.forgeself` 不入库） | 产生临时规律时；沉淀后迁入 agent-workflow.md |
| `docs/04-standards/agent-workflow.md` | **规则库**（永远，入库） | 工作流细节（Part A）+ 项目不变工程规则与踩坑（Part B） | 产生可复用规律/规则变化时 |

### 7.5.2 强制顺序（带门禁）：每轮任务循环步骤为前置条件
```
[0 输入到达] 用户消息
   │  ★ 立即写日志：当天工作日记「输入 N」条目（原文 + 任务拆解），不拖延
   ▼
[1 Goal]  解析目标 → TODO.md 生成待办项（🔄/⬜，P1>P2>P3，来源:输入N）
   ▼
[2 Context] 收集上下文（spec / 设计稿 / 相关代码 / TODO 约束）
   ▼
[3 Plan]  规划步骤（拆分为可独立验证的子任务）
   ▼
[4 Execute] 边做边记 → 工作日记追加（完成内容、决策记录、问题与解决）
   ▼
[5 Verify] 验证门禁（pnpm run check / dotnet build / 测试）→ 失败转 [6]
   ▼
[6 Iterate] 失败：分析→调整→重跑（第 3 次失败升级给人）；通过 → 继续
   ▼
[出口] 任务完成 → TODO 待办 ✅ 移除；工作日记补全验证结果
   ▼
[提炼] 可复用规律？有 → MEMORY.md 暂存 → 沉淀为 agent-workflow.md 规则 + 回写/新建技能（§2.4）；无 → 跳过
   ▼
[检视] 每日/每周：日记「下一步」→ 回流 TODO 新待办；重复模式 → 提炼为规则
   ▼
   └──────► 回到 [0 输入到达]（闭环）
```

### 7.5.3 操作规范要点（完整版 → §A6）
**① 新用户输入到达时**（第一步必做）：立即写日志（输入原文 + 任务拆解 `- [ ]` 清单，**完成一项勾选一项**）→ TODO.md 生成待办（`- [ ] <任务>（P<优先级>，来源:输入<N>）`）→ 开始执行时移到「🔄 进行中」。
**② 执行过程中**：边做边追加当天工作记录；决策记录格式：`决策 → 背景 → 理由 → 准则`。
**③ 任务完成时**（完成 = 代码闭环 + 文档同步 + 待办清除，三者缺一不可）：追加完整工作记录（含验证结果）→ TODO 待办**直接移除**（不留 ✅/[x] 堆积）→ 同步相关文档（spec / docs / README）→ 提炼规律入 agent-workflow.md → 完善/新建技能并登记 §2.4。
**④ 每日/每周检视**：日记「下一步」回流 TODO；重复模式提炼为规则；清除 TODO 中已完成残留（✅/[x]），保持队列新鲜。
**⑤ 常见错误（必须避免）**：把输入原文堆在 TODO.md；在 MEMORY 复制待办列表；任务完成不更新 TODO；完成任务只标 ✅ 不移除；不写「下一步」字段。

---

## 8. 设计稿工作流

设计稿位于 `forgeself-design/`，实现层必须对齐其视觉与交互。新增设置项 SOP、file:// 协议限制等 → `docs/04-standards/agent-workflow.md` §A7。

---

## 9. speckit SDD 开发流程

功能开发走 speckit 规格驱动开发（specify → plan → tasks → implement），命令文件在 `.codebuddy/commands/speckit.*.md`，产物在 `specs/NNN-功能名/`，当前功能目录记录在 `.specify/feature.json`。四步顺序执行不得跳步；implement 阶段禁止 `git stash/checkout/reset` 改工作区。详见 `docs/04-standards/agent-workflow.md` §A8。

---

## 10. 任务完成判定与汇报铁律（Verification-Centric Completion）

> 「代码改完 ≠ 任务完成」的硬约束。完整五步自检、汇报模板、示例 → `docs/04-standards/agent-workflow.md` §A9。

### 10.1 核心原则
- **"代码实现完成"不等于"任务完成"**。只有「需求满足 + 实现完成 + 经过实际验证」三者同时成立，才可判为已完成。
- **验证结果必须区分来源等级**：**Verified**（亲自跑过拿到真实输出）/ **Inferred**（凭代码推断）/ **Unknown**（未验证）。禁止混用。
- **禁止虚构**：不得编造测试结果、截图、日志、接口响应或验证结论；存在未验证场景必须明说；存在阻塞不得宣称完成。

### 10.2 任务完成前的五步自检（强制）
1. **Requirement Check**：逐条核对用户每个需求都已满足（缺一条即未完）。
2. **Implementation Check**：代码/配置/接口/UI/业务逻辑完整、自洽。
3. **Validation Check**：实际执行并检查的门禁，每项标 ✅/⚠️/❌ 及来源等级。
4. **Evidence Check**：记录可证明完成的真实证据（**只列实际产生过的**）。
5. **Risk Check**：查未覆盖场景、环境差异、外部依赖；无则写「无」。

### 10.3 最终状态（只能取其一）
| 状态 | 含义 |
|------|------|
| ✅ **COMPLETED** | 需求全满足、实现完成、关键验证全部 Verified、无阻塞 |
| ⚠️ **COMPLETED_WITH_RISK** | 已完成但有已知风险/未覆盖场景（须在「风险」显式列出） |
| 🟡 **PARTIALLY_COMPLETED** | 部分需求满足，其余未做/受阻 |
| 🔴 **NOT_COMPLETED** | 需求未满足或验证未过 |
| ⏸️ **BLOCKED** | 存在阻塞（缺信息/缺权限/环境/外部依赖），无法推进 |

### 10.4 汇报格式（强制使用，模板见 §A9）
```
[状态] 任务完成
任务：一句话说明本次完成的目标。
完成内容 / 主要变更 / 验证结果（只列实际执行项，标 ✅/⚠️/❌ + Verified/Inferred/Unknown）/
设计决策 / 代价·收益（运行经济学）/ 不做事决策（NOT-to-do，同步入 docs/07-decisions/not-taken-decisions.md）/
风险 / 结论（是否完成 · 可否交付 · 是否建议 Code Review）
```

### 10.5 禁令
- 禁止使用模糊表达：「应该没问题」「基本完成」「看起来正常」「大概率是」「估计可以」。
- 必须给出**明确状态** + **验证依据**。拿不到依据就标 Unknown / 标风险，不得用猜测充当完成。

---

## 11. AI-Native Engineering 闭环（开发任务唯一流程 · 独立自洽）

> 完整规范（九阶段定义、硬性约束、闸门1/2/3 自含定义、裁剪规则、禁止事项）→ `docs/04-standards/ai-native-engineering-workflow.md`（v1.1.0，强制）
> 文档模板 → `docs/18-templates/ai-pilot/`（00-repository-understanding ~ 06-review + 07-final-report）
> 产物落点 → `docs/ai/pilot/<task-id>/`（每任务一目录）｜群 SOP → `ai-native-engineering-loop`
> **用户指令（seq14）**：本流程不与其他体系做映射；开发任务的流程以本节及其规范为唯一依据，与本文或其他文档的流程描述冲突时以 §11 为准。

九阶段：`Repository Understanding → Intent → Spec → Plan → Task → Implement → Test → Evidence → Review`。要点：

1. **先理解仓库再写代码**：技术栈/架构/测试方式必须从真实仓库内容确认，禁止常识推测。
2. **工件链不得跳步**：Intent（为什么/做什么/到什么程度）→ Spec（九节，不确定点标 `Unknown`）→ Plan（具体到真实文件，偏差先记录再修正）→ Task（Allowed/Forbidden + 验证命令）→ 才允许 Implement。
3. **Test/Evidence/Review 不豁免**（任何任务级别）：验证跑真实命令记真实结果；Evidence 只记实际发生（Verified/Inferred/Unknown 分级）；Review 出八问 + Final Decision（APPROVED / CHANGES_REQUIRED / BLOCKED）。
4. **闸门（规范 §1.1 自含）**：闸门1=Intent/Spec/Plan/Task 经用户确认后开工；闸门2=Evidence+Review 齐备后交用户验收，通过前不提交代码；闸门3=验收后提交归档。放权表述只豁免过程汇报频率，不豁免闸门。
5. **裁剪**：≤3 文件的缺陷修复可将 Intent/Spec/Plan/Task 合并为 `mini-task.md`（五要素齐备，仍占闸门1）；全量/轻量由任务协调人裁定并记录。

---

<!-- OPENWIKI:START -->

## OpenWiki

This repository uses OpenWiki for recurring code documentation. Start with `openwiki/quickstart.md`, then follow its links to architecture, workflows, domain concepts, operations, integrations, testing guidance, and source maps.

The scheduled OpenWiki GitHub Actions workflow refreshes the repository wiki. Do not hand-edit generated OpenWiki pages unless explicitly asked; prefer updating source code/docs and letting OpenWiki regenerate.

<!-- OPENWIKI:END -->

<!-- CODEGRAPH_START -->
## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
