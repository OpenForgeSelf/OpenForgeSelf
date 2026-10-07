# Agent 工作流细则 A-1（A1-A5：技能体系 / 规划 / 编码规范 / 验证 / 迭代控制）

> 本文件是 `agent-workflow.md`（2026-10-07 起拆分）的**一个分册**。§编号（A1-A10 / B1-B12）与规则文字**未作任何改动**，
> 索引与「§编号 → 文件」地址表见同目录 [`README.md`](README.md)；旧路径 `../agent-workflow.md` 保留为薄指路文件。

<!-- ===== 以下为原文（自 docs/04-standards/agent-workflow.md 按行区间拆入，未作任何改写） ===== -->
# Part A — 工作流细节（AGENTS.md 详细版）

## A1 技能体系（对应 AGENTS.md §2.4 详细）

技能位于 `.agents/skills/<name>/SKILL.md`。**涉及宿主 / 插件的任务，动手前必须先读对应技能**——历史上不止一次因为没读技能而漏掉发布、漏掉插件层 e2e，最后靠临时脚本自验就宣称完成。

| 技能 | 何时用 | 关键约束 |
|------|--------|----------|
| `plugin-development` | **插件任务总入口**：新建插件、把宿主页面迁移成独立插件、改完插件不知还要做什么 | 改完 = 门禁 + 插件 e2e + 发布 + 浏览器走查，四步缺一不算完成；完成后复盘回写技能 |
| `plugin-feasibility-study` | **新建插件第一步**（先于 `plugin-development`）：行业调研 → 可行性报告 → 设计方案 → **命名** → 决策拍板 | 不许直接开写代码；命名在功能定稿之后，须过「名实相符三问」 |
| `plugin-frontend-scaffold` | 从 AIAgent 模板生成插件 `web/` 前端骨架 | 产物入口固定 `web/dist/index.js`，导出名须等于 `views[0]` |
| `plugin-publish-verify` | 发布与验证：主路径 = 打 tag 自动发布 + 页面自动更新（禁止 agent 停宿主）；本地目录更新源；插件版本化侧载（须用户同意） | 活动插件目录只放插件自身 DLL；`plugin.json` 最后拷 |
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

