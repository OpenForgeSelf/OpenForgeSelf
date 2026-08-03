# OpenForgeSelf — AI Agent 工作规则（Loop Engineering）

本文件是 AI Agent 在 OpenForgeSelf（铸己匣）项目中的唯一工作手册。
采用 **Loop Engineering** 闭环模型：每次任务都经过 Goal → Context → Plan → Execute → Verify → Iterate 六步循环，直到验证通过或升级给人。

核心原则：

1. **可验证性优先** — 任务是否完成由自动化检查判定，不靠"看起来对了"。
2. **失败是常态，回滚是基本功** — 每轮变更前确认可恢复点，验证失败立即回滚。
3. **人在回路上，不在回路中** — Agent 自主执行闭环，仅在超出能力时升级给人。

---

## 1. Goal — 目标与完成定义

### 1.1 任务来源

按优先级从高到低：

1. 用户直接指令（最高优先级）
2. `TODO.md` 中未完成条目
3. `specs/` 目录中当前阶段的待实现任务
4. Verify 环节发现的失败项（测试/lint/构建）

### 1.2 完成标准（Definition of Done）

一个任务"完成"当且仅当：

- 对应 Verify 门禁全部通过（见第 5 节）
- 变更范围未超出任务边界（不顺手改无关代码）
- 若涉及 UI，与设计稿视觉对齐已确认
- 新增/修改的行为有对应测试覆盖
- TODO.md 或 specs 中对应条目已更新状态

### 1.3 范围控制

- 做且只做明确要求的事。
- 发现的额外问题记入 `TODO.md`，不自行扩展。
- 单次变更尽量原子化：一个任务一个 commit（或一组紧密相关的 commits）。

---

## 2. Context — 上下文收集

每次开始任务前，**必须**收集足够上下文，避免盲改。

### 2.1 项目地图

| 目录 | 用途 | 技术栈 |
|------|------|--------|
| `OpenForgeSelf.Frontend/` | Vue 3 SPA | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia + pnpm |
| `OpenForgeSelf.Backend/` | ASP.NET Core API | .NET 10 + EF Core + SQLite + NewLife.XCode + 插件架构 |
| `OpenForgeSelf.Backend.Tests/` | 后端测试 | xUnit + Moq + FluentAssertions + Coverlet |
| `forgeself-design/` | 设计原型 | 自包含 HTML + Element Plus CDN + Tailwind CDN |
| `specs/` | 功能规格 | 编号 001–005，spec → plan → tasks 流程 |
| `openwiki/` | 自动生成文档 | 勿手动编辑，由 CI 刷新 |

### 2.2 收集清单

开始编码前按需确认：

- [ ] 阅读相关 spec（`specs/NNN-*/spec.md`）
- [ ] 查看设计稿（`forgeself-design/pages/` 对应页面）
- [ ] 用 CodeGraph 或 grep 定位相关代码（见附录 B）
- [ ] 阅读待修改文件的当前内容（不凭记忆改代码）
- [ ] 确认 `TODO.md` 中该任务的上下文和约束

### 2.3 关键约定速记

- 包管理器：**pnpm**（不是 npm），Node >= 20
- 前端样式体系：Element Plus 官方 `--el-*` 变量 + Tailwind 布局原语，不定义独立色值
- 设计稿与前端共享 `themes/` 下的 tokens（单一来源）
- 后端插件通过 `Plugins/` 目录 + `plugin.json` 清单注册
- 运行端口：Backend `:7102`，Frontend `:7002`

---

## 3. Plan — 规划

### 3.1 任务分解

收到任务后先分解为可独立验证的步骤：

```
任务 → [步骤1: 改什么文件, 预期什么结果]
     → [步骤2: ...]
     → [步骤N: 运行 Verify 全部通过]
```

每个步骤应满足：改完后可以立即运行某个检查命令确认对错。

### 3.2 影响评估

规划时回答：

- 本次变更影响哪些文件/模块？
- 是否涉及公共接口（API 契约、组件 props、路由）？若是，需额外谨慎。
- 是否涉及数据库迁移？若是，必须可回滚。
- 是否涉及设计稿对齐？若是，先截图确认设计稿当前状态。

### 3.3 风险分级

| 级别 | 场景 | 处理方式 |
|------|------|----------|
| 低 | 组件内部样式/逻辑调整 | 正常执行 |
| 中 | 公共组件/API 接口变更 | 执行前确认影响范围，执行后全量 Verify |
| 高 | 数据库迁移/依赖升级/架构调整 | 升级给人确认后再执行 |

---

## 4. Execute — 执行

### 4.1 安全约束

- **变更前备份**：修改用户文件前，确认项目在 Git 管理下（可 `git diff` 回溯）。若非 Git 管理，先备份原文件。
- **禁止永久删除**：不 `rm`/`del` 用户文件，需要删除时移入回收站或 `.trash/`。
- **敏感路径禁触**：`.env`、`appsettings.Production.json`、含密钥的配置文件 — 只读，不修改。
- **变更范围限制**：单次执行修改文件数不超过任务所需，不顺手重构无关代码。

### 4.2 编码规范

**前端：**
- 组件拆分：每个面板/功能区独立 `.vue` 文件，不允许大文件堆多面板逻辑
- 样式：使用 Element Plus 组件 + Tailwind 布局类，颜色只走 `--el-*` 变量
- 类型：所有新代码必须有 TypeScript 类型，不用 `any`
- 测试（TDD 优先）：新增逻辑先写 vitest 测试定义预期行为，再写实现使测试通过；修改逻辑先补/改测试覆盖新行为，再改实现
- 全屏背景图：用固定定位 `<img>` 元素（`position:fixed; inset:0; object-fit:cover; z-index:0; pointer-events:none`）+ 内容层 `z-index` 叠放，**不要**用 CSS `background-image: url(外链)`。本环境外链背景图不渲染（已验证：手动注入 `!important` 后截图仍纯白），`<img>` 方案可稳定显示

**后端：**
- 遵循现有 Controllers/Services/Entities 分层
- 新插件按 `Plugins/` 目录 + `plugin.json` 模式注册
- 异步方法统一 `async/await`，不 `.Result` 阻塞
- 测试（TDD 优先）：新增/修改业务逻辑先写 xUnit 测试，再写实现

### 4.3 执行记录

每步执行后记录：改了什么、运行了什么命令、结果是什么。
失败时记录错误信息全文（不截断），作为 Iterate 环节的输入。

---

## 5. Verify — 验证门禁

验证是闭环的核心。不通过 Verify 的变更 **不算完成**。

### 5.1 前端验证（修改 `OpenForgeSelf.Frontend/` 后必须执行）

```bash
cd OpenForgeSelf.Frontend
pnpm run check    # vue-tsc --noEmit && eslint（类型检查 + 代码规范）
pnpm run test     # vitest（单元测试）
```

判定规则：
- `check` 失败 → 先修类型错误，再修 lint 问题
- `test` 失败 → 修测试或补测试，**不允许跳过**
- 两项都通过 → 前端验证通过

### 5.2 后端验证（修改 `OpenForgeSelf.Backend/` 后必须执行）

```bash
cd OpenForgeSelf.Backend
dotnet build
```

后端测试（涉及逻辑变更时必须执行）：

```bash
cd OpenForgeSelf.Backend.Tests
dotnet test
```

判定规则：
- 构建失败 → 修编译错误
- 测试失败 → 修代码或修测试（不允许删除测试来"通过"）
- 构建 + 测试通过 → 后端验证通过

### 5.3 设计对齐验证（涉及 UI 变更时）

- 打开对应设计稿（`forgeself-design/pages/xxx.html`）对比
- 条件允许时截图对比（Playwright screenshot）
- 布局/间距/颜色须与设计稿一致，组件交互行为须与 spec 描述一致

### 5.4 验证严重级别

| 级别 | 含义 | 处理 |
|------|------|------|
| **Critical** | 构建失败、测试失败、类型错误 | 必须修复，否则任务不完成 |
| **Warning** | ESLint warning、覆盖率低于阈值 | 记录到 TODO.md，不阻断当前任务 |
| **Info** | 代码风格建议、可选优化 | 记录但不处理 |

### 5.5 脚本速查

| 命令 | 作用 | 目录 |
|------|------|------|
| `pnpm run check` | 类型检查 + ESLint 联合校验 | Frontend |
| `pnpm run lint` | 仅 ESLint | Frontend |
| `pnpm run lint:fix` | ESLint 自动修复 | Frontend |
| `pnpm run type-check` | 仅 vue-tsc 类型检查 | Frontend |
| `pnpm run test` | 单元测试（vitest） | Frontend |
| `pnpm run build` | 完整构建（含类型检查） | Frontend |
| `dotnet build` | 后端构建 | Backend |
| `dotnet test` | 后端测试 | Backend.Tests |

---

## 6. Iterate — 迭代控制

### 6.1 验证失败时的处理流程

```
Verify 失败
  ├─ 第 1 次失败 → 分析错误，调整实现，重新 Execute → Verify
  ├─ 第 2 次失败 → 换策略（不同实现路径），重新 Execute → Verify
  ├─ 第 3 次失败 → 回滚到最近通过状态，升级给人
  └─ 任何时候出现非预期副作用 → 立即回滚，升级给人
```

### 6.2 Bug 修复 TDD 流程

发现 bug（测试失败、用户报告、Verify 暴露）时，**禁止直接改实现代码**。标准路径：

```
1. Red   — 写一个测试精确复现问题（此时测试必须失败）
2. 确认  — 运行该测试，确认红灯（证明测试有效）
3. Green — 最小化修复实现代码，只改让测试通过所必需的部分
4. 确认  — 运行该测试，确认绿灯
5. 回归  — 运行完整 Verify（pnpm run test / dotnet test），确认无副作用
```

要点：
- 第 1 步的测试即永久回归保护，不允许修复后删除
- 若无法写出复现测试（如纯 UI 视觉问题），用截图对比替代，但须记录原因
- 修复范围严格限定：只改让红灯变绿的最小代码，不顺手重构

### 6.3 回滚策略

- **首选**：`git checkout -- <file>` 或 `git stash` 恢复到变更前
- **次选**：若已 commit，`git revert` 生成反向提交
- **底线**：从备份恢复（执行前已备份的情况）
- 回滚后必须重新运行 Verify 确认恢复成功

### 6.4 升级规则

以下情况 **必须**停止循环、升级给人：

- 连续 3 次验证失败且无新思路
- 涉及高风险操作（数据库迁移、API 契约破坏性变更、主依赖大版本升级）
- 错误信息指向环境问题（非代码问题）
- 任务描述存在歧义，两种理解会导致不同实现
- 需要安装新依赖或修改构建配置

升级时提供：已尝试的方案、失败原因、建议的下一步。

### 6.5 循环终止条件

- **正常终止**：Verify 全部通过 → 进入 Persist
- **异常终止**：触发升级规则 → 报告给人
- **超时终止**：单任务迭代超过 5 轮仍无进展 → 升级

---

## 7. Persist — 经验沉淀

每轮循环结束后（无论成功或升级）：

1. **更新 TODO.md**：标记完成/记录新发现的问题
2. **更新 specs 状态**：若任务对应某个 spec 的 task，标记进度
3. **记录决策**：重要技术决策写入对应 spec 或 TODO.md，格式：决策 → 背景 → 理由 → 准则
4. **不重复犯错**：若某类错误反复出现，在对应位置加注释或补测试防回归

---

## 8. 设计稿工作流

设计稿位于 `forgeself-design/` 目录，HTML 文件可直接在浏览器打开预览。

- 实现层（Vue 组件）**必须**对齐设计稿的视觉与交互
- 设计稿中 `partials/app-shell.js` 提供共享外壳，`themes/` 提供 tokens
- 新增设置项 SOP：app-shell.js categories 加条目 → 复制现有设置页改 active 和内容区
- 设计稿 file:// 协议下禁止跨目录引用 CSS，主题文件须在 `forgeself-design/themes/` 放本地副本

---

## 9. speckit SDD 开发流程

功能开发走 speckit 的规格驱动开发（SDD）流程，命令文件位于 `.codebuddy/commands/speckit.*.md`：

```
specify → plan → tasks → implement → （analyze/converge 一致性检查）
```

### 9.1 流程与产物

每步产物都生成在当前功能目录 `specs/NNN-功能名/` 下（以 `005-todo-tracker` 为完整示例）：

| 步骤 | 命令文件 | 生成产物 |
|------|----------|----------|
| specify | `speckit.specify.md` | `spec.md`（功能规格）、`checklists/requirements.md`（质量清单） |
| plan | `speckit.plan.md` | `plan.md`（技术计划）、`research.md`（调研决策）、`data-model.md`（数据模型）、`contracts/`（接口契约）、`quickstart.md`（验证指南） |
| tasks | `speckit.tasks.md` | `tasks.md`（分阶段任务清单，格式 `- [ ] T00N [P] [USx] 描述 + 文件路径`） |
| implement | `speckit.implement.md` | 按 `tasks.md` 逐条执行并把完成项勾选为 `[X]` |

### 9.2 当前在做哪个 spec

记录在 `.specify/feature.json` 的 `feature_directory` 字段，例如：

```json
{ "feature_directory": "specs/005-todo-tracker" }
```

plan / tasks / implement 等后续命令都通过此文件定位当前功能目录。

### 9.3 判断进行到哪一步、下一步做什么

根据当前功能目录下已存在的产物判断：

| 功能目录已有产物 | 进度判断 | 下一步 |
|------------------|----------|--------|
| 仅 `spec.md`（+`checklists/`） | specify 完成 | 运行 plan |
| 有 `plan.md` / `research.md` / `data-model.md` | plan 完成 | 运行 tasks |
| 有 `tasks.md`，含未勾选 `- [ ]` | tasks 完成或 implement 进行中 | 运行 implement |
| `tasks.md` 全部 `- [X]` | implement 完成 | Verify / converge |

### 9.4 纪律

- 四步必须顺序执行，**不得跳步**：禁止出了 `spec.md` 就直接写代码
- 以「产物是否生成」判断本步完成，再进入下一步
- implement 步逐条执行任务，每完成一条把 `- [ ]` 改为 `- [X]`
- **implement 阶段禁止用 `git stash` / `git checkout` / `git reset` 等会改动工作区的命令**去"验证预先存在的状态"（如确认某个构建失败在本次变更前就有）。调度可能中途超时，此类操作会把未提交的工作一起卷走。需要对比基线时只用只读命令：`git diff` / `git show` / `git log`。

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
