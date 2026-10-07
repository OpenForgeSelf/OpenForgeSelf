# OpenForgeSelf — AI Agent 工作规则（Loop Engineering）

本文件是 AI Agent 在 OpenForgeSelf（铸己匣）项目中的**每次必守工作手册**（每次任务循环都必须执行的内容）。
采用 **Loop Engineering** 闭环模型：Goal → Context → Plan → Execute → Verify → Iterate，直到验证通过或升级给人。
所有**开发类任务**的流程**一律按 §11 AI-Native Engineering 九阶段闭环执行**（Repository Understanding → Intent → Spec → Plan → Task → Implement → Test → Evidence → Review；规范 `docs/04-standards/ai-native-engineering-workflow.md`，独立自含、闸门1/2/3 见其 §1.1）。该规范与其他流程描述冲突时，**以 §11 为准**（用户指令 seq14：不做体系映射）。**本项目只有这一套开发流程**：`specs/`/`speckit` 等其余流程已全部弃用（§9），任何文档残留的相关指引一律无效。

> **分层说明**：本文只列「每次都要遵守」的规则；「需要触发才用」的细节（技能职责边界、验证决策表、迭代细则、文档工作流操作规范、汇报完整模板、设计稿工作流、项目工程规则与踩坑）一律引用 **`docs/04-standards/agent-workflow/README.md`**（Part A = 工作流细节，Part B = 项目不变工程规则，2026-09-24 起原 `.forgeself/memory/MEMORY.md` 的规则已归档于此，随 git 入库）。

---

# §0 预飞铁律（每个回合强制 · 不可跳过 · 任务大小不构成例外）

收到用户消息后严格按顺序执行；前一步未达成，不得进入下一步：

**纯问答回合（只是确认"这功能到底怎么做的 / 谁生效 / 放哪 / 从哪进"这类事实，不改业务文件）走 §2.5 唯一真源流程**：查 `docs/README.md` §一 → 判精度 → 读码或走现场 → **回写同一份真源** → 带出处回答。下面的工件链与闸门只约束"要改业务文件"的回合；问答回合的文档回写**免闸门1**，但**不得借机改业务代码**。

1. **【首动作·写日志】** 在调用任何读代码/搜索/编辑工具之前，先向当天工作日记追加一行：`.forgeself/memory/YYYY-MM-DD.md`（不存在则创建；目录跟项目走，换任何 agent 均可读写）。内容=输入原文（用户原始任务提示）+ 任务拆解清单（1-3 行）；**完成一项就把 `- [ ]` 勾为 `- [x]`**。
   ⛔ 未写此行，禁止调用任何文件读/写工具。
2. **【建待办】** 在 `TODO.md` 新建或引用一条待办（🔄 进行中，标 P1>P2>P3，来源:输入N）。**开发过程中发现的其他问题，当场记入 `TODO.md` 待办区（标 P1>P2>P3，来源:输入N），本次会话不顺手解决**（范围控制见 §1.3）；任务完成时该待办勾选归档到工作日记并**移除出 `TODO.md`**，保持队列新鲜不爆炸。
   ⛔ 无对应 TODO 项，禁止修改任何业务文件。
3. **【读规范·强制】** 开发类任务动手前必须**完整读** `docs/04-standards/ai-native-engineering-workflow.md`（v1.1.0，强制）——本项目**唯一**开发流程——并按其九阶段产出 `docs/ai/pilot/YYYY-MM-DD-<task-id>/` 工件：00-04（Repository Understanding / Intent / Spec / Plan / Task）先行，**闸门1 经用户批准后才允许 Implement**；05-07（Evidence / Review / Final Report）在验证与审查后补齐。模板 `docs/18-templates/ai-pilot/`，可参照既有 pilot 目录样例。
   ⛔ 未读规范、或闸门1 未批就改业务代码 = 流程违规（2026-09-30 实证缺口：AGENTS.md 残留的 specs/speckit 指引曾诱导跳过 pilot，已全部封堵）。
4. **【查技能】** 任务拆解后从技能目录（§2.4）挑一个流程执行：有对应技能则**先读再动手**；**技能清单无对应流程时，先创建一个技能流程（沉淀为 `.agents/skills/<name>/SKILL.md`，按 skill-creator 规范）→ 执行 → 持续迭代完善**；同类任务下次重复出现时，先选既有技能流程。
   宿主/插件类任务必须读 `plugin-development`（及其转派的专项技能）。
   ⛔ 未读技能就改宿主/插件代码，视为流程违规——历史上已因此漏发插件、漏跑插件层 e2e。
5. **【Context → Plan → Execute → Verify】** 按第 1-5 节执行；Verify 必须真跑（前端 `pnpm run check`+`pnpm run test` / 后端 `dotnet build`+`dotnet test`）。
6. **【出口·完成检查清单】** 回复"完成"前，必须：
   - Read 当天日记，确认含本次输入拆解 + 验证结果；
   - 确认 `TODO.md` 该待办已**从队列移除**（完成即移除，不留 ✅/[x] 堆积）；并确认相关文档（`docs/ai/pilot/` 工件 / `docs/` / README）已同步更新；
   - （如有可复用规律）已沉淀到 `docs/04-standards/agent-workflow/README.md` 对应小节；本次走通的流程若技能缺失或可优化，已回写/新建技能（§2.4 登记）；新增项目不变规范已入 agent-workflow/README.md 或本文。
   - **【PILOT 工件链门禁】** 开发类任务（AI-Native 闭环）回复完成前，提交必须经 pre-commit hook 校验：`docs/ai/pilot/YYYY-MM-DD-<task-id>/` 的 00-07 八件工件（含关键节）齐全（目录日期前缀规则见规范 §0），缺失/缺位提交被拒；新 clone / 新环境第一步执行 `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1` 安装 hook（幂等；**脚本一律用 `pwsh` 跑，禁止 `powershell` 5.1**，见 §2.3）。
   - 【插件任务硬性门禁】若本次改动涉及 `Plugins/<X>/web/` 或插件本体（`.cs` / `Controllers` / `Services` / …），须跑完 `plugin-development` §四 维护闭环**全部五步**：① 门禁（插件前端 `cd Plugins/<X>/web && pnpm run build` / 后端 `dotnet build` + 测试）② 插件层 e2e（`e2e/plugins/<id>`，走 e2e 隔离实例，按 `e2e-testing` 技能）③ 发布（**打 tag 自动发布** → CI 打包 GitHub Release；或本地 `release-local.ps1 -UpdateDir` + 设置页本地目录更新源 + 页面自动更新）④ 走查（e2e 隔离实例按用户视角点一遍、截图读图、清测试数据）⑤ **运行实例只读复验**（触发：**用户已在 `:51888` 等运行实例启用/更新到新版本后**；动作：先验 token 有效（打一个需鉴权的只读端点，**401 立即上报，不许把空页当结论**）→ 注入 `forge_api_token` → 核 `.view-title` 版本徽标 → 走一条主链路并**采中间态**（占比 ≤100、合计闭合）→ 截图存 `ForgeSelf.Web/screenshots/live-<端口>/` 并读图；**只读、不点不可逆、不启停宿主、不调改状态的端点**，详见 `plugin-publish-verify`「运行实例只读复验」节）。**五步缺一不可，缺失即视为未完成，禁止回复"任务完成"**。**发布规范（2026-09-27 用户指令）**：**禁止 agent 停/启/杀任何用户运行中的宿主进程**（含 `D:\src\tools\ForgeSelf`、`:51888` 实例）；宿主升级一律由 update-agent 自更新（用户/页面点「自动更新」），agent 只负责打 tag 发布与验证发布产物。**铁律1 禁的是停/启/杀与不可逆写，不含只读走查**（2026-09-28 我曾据此漏掉第⑤步，被用户指出）。
   ⛔ 任一项不满足，禁止回复"任务完成"——先补齐再回。

> 验证通过（测试绿）≠ 流程完成。Persist 与 Verify 同属 DoD，缺一项即未达标。

### 🚫 红线（不可逾越 · 违反即流程违规）

**验证 / 截图 / 浏览器驱动类需求，禁止手写一次性 `temp/*.cjs`（或散落脚本）作为验证手段。**
必须走 §5.0 项目唯一测试体系（Playwright e2e / vitest / dotnet test）。历史教训：spec 006 期间在 `temp/` 产 49 个一次性 `.cjs`（截图/视觉校验/浏览器驱动），验证完即废、无版本控制、无法回归；且 `temp/browser_drive/round1.cjs` 的启发式判定（`发送键可用+文本>500`）曾漏掉 agent 真实 `write_file` 调用，误导结论。

- 临时探针**仅可作探索期一次性使用**：用完即删、**不进版本控制、不留存 `temp/`**，且**不得作为验证结论**（结论必须沉淀为 §5.0 的可重复测试）。
- 任何"我手跑个脚本看看"的冲动，先查 §5.0 决策表：有对应正规入口就走正规入口。

**开发类任务禁止跳过 AI-Native 闭环工件直接写代码。**
每个开发任务（新功能 / 缺陷修复 / 插件任务 / 宿主与 CI 变更）的流程**唯一依据是 §11 规范**（模板 `docs/18-templates/ai-pilot/`，产物 `docs/ai/pilot/YYYY-MM-DD-<task-id>/`；独立自含，不与其他流程体系做映射）；Evidence 或 Review 缺件即视为未完成。纯问答 / 查状态不占工件链。`specs/`/`speckit` 已弃用（见 §9），**禁止**再走 specs/speckit 流程。

核心原则：

1. **可验证性优先** — 任务是否完成由自动化检查判定，不靠"看起来对了"。
2. **失败是常态，回滚是基本功** — 每轮变更前确认可恢复点，验证失败立即回滚。
3. **人在回路上，不在回路中** — Agent 自主执行闭环，仅在超出能力时升级给人。
4. **重复动作脚本化优先** — 凡重复执行或可能多次重复执行的动作：优先固化为可复用脚本（纳入版本控制、可重复运行，禁止散落 `temp/` 或每次临场重写），形成技能 SOP 次之（沉淀为 `.agents/skills/<name>/SKILL.md` 并按 §2.4 登记）。判断线：同一动作出现第二次就该固化，而非等到第 N 次。

---

## 1. Goal — 目标与完成定义

### 1.1 任务来源
按优先级从高到低：用户直接指令（最高优先级）→ `TODO.md` 未完成条目 → Verify 环节发现的失败项。**开发类任务一律走 §11 AI-Native 闭环（产物 `docs/ai/pilot/YYYY-MM-DD-<task-id>/`），不再使用 `specs/`（已 gitignore）**。

### 1.2 完成标准（Definition of Done）
一个任务"完成"当且仅当：
- 对应 Verify 门禁全部通过（见第 5 节）
- 变更范围未超出任务边界（不顺手改无关代码）
- 若涉及 UI，与设计稿视觉对齐已确认
- 新增/修改的行为有对应测试覆盖
- TODO.md 中对应条目已更新状态

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
| `docs/ai/pilot/` | 开发任务工件（AI-Native 闭环） | 替代原 `specs/`（已 gitignore）；`YYYY-MM-DD-<task-id>/` 下 00-07 八件 |
| `openwiki/` | 自动生成文档 | 勿手动编辑，由 CI 刷新 |

### 2.2 收集清单
开始编码前按需确认：**查 §2.4 技能清单，读取对应技能**（宿主/插件任务必读）→ 阅读相关 `docs/ai/pilot/` 工件（同域历史任务的 02-spec/03-plan）→ 查看设计稿（`forgeself-design/pages/`）→ 用 CodeGraph 或 grep 定位相关代码 → 阅读待修改文件的当前内容（不凭记忆改代码）→ 确认 `TODO.md` 中该任务的上下文和约束。

### 2.3 关键约定速记
- 包管理器：**pnpm**（不是 npm），Node >= 20
- **脚本执行统一用 `pwsh`（PowerShell 7），禁止 `powershell` / `powershell.exe`（= Windows PowerShell 5.1）**（2026-10-04 输入12 立；本机 `pwsh` 实测 7.6.6，CI `.github/workflows/release.yml` 早已是 `shell: pwsh`）：涵盖跑仓内 `*.ps1`、从 Git Bash / Node `spawn` 调 PowerShell 的一切场景，写法 `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\<某脚本>.ps1 …`。三条**实测**代价（不是风格偏好）：① 5.1 按控制台码页（GBK/936）写重定向日志并解码外部命令输出 → 中文变乱码、乱码还会被烤进交付物（2026-10-04：`release-local -Sign` 的签名段整段 GBK；更早 `make-release-notes` 把乱码写进 RELEASE-NOTES，现由脚本内 UTF-8 切码页 + 仓库级守卫 `RepositoryScriptTests` 封堵）；② 5.1 从 Node `spawn` 时**没有 `Cert:` 提供程序** → Authenticode 签名必失败（2026-10-01 实证）；③ 5.1 靠 BOM 判定 `.ps1` 编码，UTF-8 无 BOM 脚本被当 GBK 解析 → 解析期崩溃、连日志都写不出（update-agent 曾据此把 51888 实例弄下线）。**唯一保留例外**：专门复现 5.1 解析/编码行为以证明某缺陷时反而必须用 `powershell`（Core 复现不出来），见 `docs/04-standards/agent-workflow/b-engineering-daily.md` §B6；除此之外一律 `pwsh`。脚本文件自身**仍须带 UTF-8 BOM**（随包分发时可能被旧 shell 拉起）。既有偏差登记：`scripts/hooks/pre-commit` 仍在用 `powershell.exe` 调工件门禁（见 TODO）
- 前端样式体系：Element Plus 官方 `--el-*` 变量 + Tailwind 布局原语，不定义独立色值
- 设计稿与前端共享 `themes/` 下的 tokens（单一来源）
- 后端插件通过 `Plugins/` 目录 + `plugin.json` 清单注册
- 运行端口：Backend `:7102`，Frontend `:7002`（**默认回落值**）；本环境长期运行的 publish 实例用 `:51888`。**端口覆盖（PILOT-050，2026-09-30）**：宿主支持 `FORGESELF_PORT` 环境变量（优先）或 `--server-port` 参数动态覆盖，覆盖即落盘 `ForgeSetting.config`（重启一致）；e2e/多 worktree 并行一律走动态端口（认领注册表 + 稳定目录 `wt-<hash>`），e2e 侧地址真源 = `e2e/helpers/e2e-env.ts`（env → current.json → 默认），**禁止新代码硬编码 7102/7002**
- **发布规范（2026-09-27 起；输入42 补签名、输入2 2026-09-30 改默认关闭）**：打 tag 自动发布（CI 打包 GitHub Release）+ 页面「自动更新」；或本地目录更新源（`release-local.ps1 -UpdateDir` + 设置页填写本地目录）。**签名策略（2026-10-04 定稿）：本地发布给人装的宿主包必须带 `-Sign` Authenticode 签名（签名无效的包不得交付）；CI 流水线默认不传 ⇒ 不签**（`scripts/sign-publish.ps1`，自签证书自动生成/复用 + **RFC3161 时间戳多点回退**（2026-10-06 起：sectigo→digicert→globalsign→comodoca，单点 digicert 间歇返回无效响应曾把整条发布链挡在签名段、zip 根本不产出；禁用 `-NoTimestamp` 交付），商业证书 -PfxPath 可插拔；**CI 默认不签**，签名策略真源见 `docs/04-standards/packaging-upgrade-backup.md` §1.1）。**禁止 agent 停/启/杀宿主进程**，宿主由 update-agent 自更新（见 §0 门禁 / plugin-publish-verify）
- **版本号规则（2026-10-02 输入9/输入10 起，输入12 补预览版）**：发行串 = `<major>.<minor>.<patch>.<yyMMddHHmm>`（10 位时间码 = yyMMddHHmm，例 `2.3.0.2609161125`；发行线自 2.3 起）；git tag / `versions/<ver>/` 目录名 / zip 名 / 两个 exe 的 FileVersion+ProductVersion / 设置页「当前版本」**同一串**。生成入口 `scripts/release/new-version.ps1 -Version 2.3.0`（只打印完整串与 `git tag` 命令，不做任何 git 写操作）；发行串由 `release-local.ps1` 单点补时间码并贯穿发布链。**预览版 tag** = `v<三段号>-preview`（如 `v2.3.0-preview`）：tag / `versions/` 目录名 / zip 名 / Release 页带 `-preview` 后缀（并自动发为 GitHub prerelease，仅 `channel != stable` 的实例会收到），**exe 文件版本不带后缀**（PE 段只接受纯数字）。完整规则、代价与踩坑（CS7035 / NuGet NU1105 / 世代比较 / 预览版后缀）→ 真源 `docs/04-standards/packaging-upgrade-backup.md` §4-R10
- **打包/升级/备份/缓存/安装目录结构（真源引用，2026-09-28 输入30；目录命名统一小写 2026-09-29 输入37）**：相关规则的**唯一真源 = docs/04-standards/packaging-upgrade-backup.md**——含现状盘点、空间浪费点、**QQNT 式目标目录结构**（公共外置 + 宿主每次更新的内容入 versions/<ver>/ + plugins/ 与 versions/ 并排 + **去插件备份/_backups**，插件多版本共存即回滚能力）与生命周期规则（§3/§4/R9）与**程序架构分层**（§1.6：入口=根启动器 / 业务+服务+托盘=版本层宿主 / 更新=update-agent / 插件=隔离程序集）；**目录命名统一小写**（安装/运行布局：plugins/data/log/config；源码工程目录 ForgeSelf.Api/Plugins/Plugins/<X> 保持 PascalCase 不动）。AGENTS.md / agent-workflow/README.md / 功能文档 / 技能只保留操作流程与踩坑，不再重复承载结构事实；规则冲突以该真源为准。**⚠️ 落位/裁决/动线类问题先查真源 §1.7（插件落位与生效裁决：两路同名谁胜、比的是哪份版本号、目录名为何 PascalCase、只发插件放哪）与 §1.8（用户更新动线 + 三条不重启口子 + 已知宿主缺陷），查不到再读代码；读完代码必须回写这两节**（2026-10-07 实证缺口：§1.6 只写"按版本号裁决"一句，精度不足致我重复读码并脑补了一个不存在的 UI 入口）。
- 更全的工程规则/踩坑（数据落盘、XCode、DLL 锁、PS 编码等）→ `docs/04-standards/agent-workflow/README.md` Part B

### 2.4 技能清单（Skills — 动手前必查）
技能位于 `.agents/skills/<name>/SKILL.md`。**涉及宿主 / 插件的任务，动手前必须先读对应技能**——历史上不止一次因为没读技能而漏掉发布、漏掉插件层 e2e。

| 技能 | 何时用 | 关键约束 |
|------|--------|----------|
| `plugin-development` | **插件任务总入口**：新建插件、把宿主页面迁移成独立插件、改完插件不知还要做什么、**用户说"我看看效果/先别发布/本地跑一下"（→ §四 的 👀 本地预览）** | 改完 = 门禁 + 插件 e2e + 发布 + 隔离实例走查 + **运行实例只读复验**，五步缺一不算完成；本地预览不替代任何一步；完成后复盘回写技能 |
| `plugin-feasibility-study` | **新建插件第一步**（先于 `plugin-development`）：调研 → 可行性报告 → 设计方案 → **命名** → 决策拍板 | 不许直接开写代码；命名在功能定稿之后，须过「名实相符三问」 |
| `pilot-handoff` | **跨 AI 规划交接**：用户要求「只做规划、实现交别的 AI、完成后你验收」时；产出 00–07 交接包 + 预注册验收清单后停下 | 05 只留骨架/栏位；**06 验收清单必须在实现开始前写好**（防看实现定标准）；实现方不得改 06/07 结论栏；收尾不提交 git |
| `plugin-frontend-scaffold` | 从 AIAgent 模板生成插件 `web/` 前端骨架 | 产物入口固定 `web/dist/index.js`，导出名须等于 `views[0]` |
| `plugin-publish-verify` | 发布与验证：主路径 = 打 tag 自动发布 + 页面自动更新；本地目录更新源；插件侧载（须用户同意） | **禁止 agent 停/启/杀宿主**；宿主升级由 update-agent 自更新；活动插件目录只放插件自身 DLL |
| `e2e-testing` | 插件层 e2e（`e2e/plugins/<id>/<id>.spec.ts`）+ 截图读图 | 零 mock；禁止用一次性临时脚本代替 |
| `design-system-verify` | **设计系统插件（design-system）专用收口**：四层门禁 + 「假能力」自查表（条目按编号递增，去技能里读最新全表，别在此记条数） | 改过 `Plugins/DesignSystem` 任何一层必用；计数/主题/导出/门禁/图标都要逐条问"现在有证据吗" |
| `design-system-consume` | **设计系统插件消费侧**：外部/内置 agent 如何发现与调用 8 个 design_* 工具（封套、写开关、REST 对等、常见坑） | 写集成/测试时用；维护工具本身走 design-system-verify；出参键 camelCase；`list_tools` 无封套 |
| `architecture-design` | 影响面较大的架构/设计决策 | 先查依据（调研/ADR/既有设计），禁止脱离依据自作设计 |

**选型顺序**：先判断「是不是插件任务」→ 是则先读 `plugin-development` → 它会转派到 `plugin-feasibility-study`（**新建插件**时）/ `architecture-design`（涉及契约与内核接缝时）/ `plugin-frontend-scaffold` / `plugin-publish-verify` / `e2e-testing`；**规划与实现分属不同 AI/会话**（用户要求「只做规划、实现交他人、完成后由本会话验收」）→ 先读 `pilot-handoff`。职责边界、新建插件硬顺序、登记规则、技能缺失策略 → `docs/04-standards/agent-workflow/a-workflow-core.md` §A1。
**新增技能必须同步登记到本节**（技能不在表里 = 等于不存在，后续会话必然再次漏读）。

### 2.5 功能确认问答流程（唯一真源优先 · 强制 · 2026-10-07 用户立）
> 立规背景：用户 2026-10-07 连问「同名插件放两个根哪个生效」「内置根目录名为何 PascalCase」「页面上哪里更新插件」，
> 我每次都**重新读一遍代码**才回答；文档里其实有一句（`packaging-upgrade-backup.md` §1.6「按版本号裁决」），但**精度不够**，
> 且没有条文规定"答事实问题必须先查真源、读完代码必须回写" ⇒ 文档没兜住问题，我也没回去补，同一问题下次还得再读一遍码。
> §1.7/§1.8 只补了这一个主题，**本节补的是流程本身**。

**触发**：任何"确认一个功能到底怎么做 / 谁生效 / 放在哪 / 从哪进 / 会不会重启 / 这字段什么含义"的**事实型问题**（我自己需要先确认事实才能回答的，同样适用）。

**五步（顺序不可换；第 4 步没做完，不许发答案）**
1. **定位真源** — 查 `docs/README.md` §一「30 秒定位速查」取该主题的**唯一真源文件**（插件/功能事实另看 `docs/02-features/<NNN>-*.md`）。表里没有这一行 = **真源缺失**，直接进第 3 步。
2. **判精度（有 ≠ 够）** — 要答的每条结论必须能**指回文档原文一句**；文档只给结论、给不出"比的是哪个字段 / 哪段代码 / 界面上点哪"，判**精度不足**，与"无真源"同样处理。
3. **补齐** — 读代码得出的结论一律配 `文件:行号`；界面/入口类结论必须**真实走一遍**（运行实例只读走查或 e2e 截图），**禁止由后端能力/路由表反推 UI**。
4. **回写同一份真源** — 补进第 1 步那份文件的对应小节（主题无归属就先在 `docs/README.md` §一 **加一行**再建文件）；**同一事实只允许一处承载，禁止在第二份文档复制**。
5. **带出处回答** — 每条结论标 `文件:节号/行`，并区分「文档转述」与「实测」；现场事实**只有实测算依据**。

⛔ **读完代码只回答、不回写 = 该问题仍未解决**（下一个人还得重读一遍）。
细则（真源归属表、回写三件套格式、与「文档反向同步 SOP」的分工）→ `docs/04-standards/agent-workflow/a-workflow-docs-report.md` §A6。
**问答回合免闸门1**：本流程不改业务文件，第 4 步的文档写入属**免批动作**（否则"先补文档"永远排不到），但**不得借机改业务代码**。

---

## 3. Plan — 规划

- **任务分解**：收到任务后先分解为可独立验证的步骤（`任务 → [步骤1: 改什么文件, 预期什么结果] → ... → [步骤N: 运行 Verify 全部通过]`）；每步应满足改完后可立即运行检查命令确认对错。
- **影响评估**：本次变更影响哪些文件/模块？是否涉及公共接口（API 契约、组件 props、路由）？是否涉及数据库迁移（必须可回滚）？是否涉及设计稿对齐（先截图确认当前状态）？
- **风险分级**：低（组件内部调整，正常执行）/ 中（公共组件/API 变更，执行前确认影响范围 + 全量 Verify）/ 高（数据库迁移/依赖升级/架构调整，升级给人确认后再执行）。

### 3.1 对外方案格式（六段 · 硬约束 · 2026-09-28 用户立）
> 立规背景：用户「每次都给一堆，没有重点」——我把实测表、差异分析、优化清单、合规交代全塞进一条回复，**要批的方案被埋在最后**，用户拿不到「你要我拍什么」。

给用户的方案**只能是这六段，每段 ≤3 行，总长控制在半屏内**：

1. **目标** — 一句话 + **可验证的成功判据**（数字/命令输出，不是「更快」）。
2. **改动** — `文件:行 → 动作`，**≤5 条**；超 5 条说明方案该拆。
3. **不改** — 明确边界（哪些看起来相关但本批不碰）。
4. **验证** — 跑什么命令 + 判定标准（对齐 §5.6 分档，写明跑哪一档）。
5. **代价与风险** — ≤2 行，含回滚点。
6. **待你拍板** — ≤2 个问题，**每个都带默认推荐项**（无问题就写「无，默认按上述执行」）。

**不进回复正文的东西**：实测数据表、算法分析、参考实现对照、探针/合规交代、历史过程 → 一律落 `.forgeself/memory/YYYY-MM-DD.md` 或 `docs/ai/pilot/YYYY-MM-DD-<task-id>/`，正文最多一句话指路（「数据见日记 输入16」）。
**汇报同理**（见 §10.4）：结论先行，证据在文档里，不在聊天里堆。

**方案必须落盘为文件，聊天里只给路径**（2026-09-28 用户立，硬性）：
- 六段方案**先写成文件**再回复用户；文件落点 = `docs/ai/pilot/YYYY-MM-DD-<task-id>/`（每任务一目录，日期前缀规则见规范 §0），格式**只按** `docs/18-templates/ai-pilot/` 模板与规范 §4：全量 = `00`~`06` 七件；轻量（≤3 文件缺陷修复/边界测试）= 单文件 **`mini-task.md`**（合并 Intent/Spec/Plan/Task，Evidence/Review 完成后单独产出 `05-evidence.md`/`06-review.md`）。**禁止自创文件名、自创章节结构、塞进别人任务的目录**（2026-09-28 我曾产出 `08-mini-task-perf-1.1.2.md`，三项全违，已纠）。
- **回复正文只允许三样**：① 文件路径（用户点开就能审）② 一句话摘要 ③ 待拍板项。
- ⛔ 只在聊天里"给方案"而不落盘、或落盘了却不报路径 = 视同没给（用户原话：「给完了怎么不提在哪个文件夹让我审核？？？谁特么知道你给了」）。

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
（完整规范与更多坑 → `docs/04-standards/agent-workflow/a-workflow-core.md` §A3 / Part B）

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
- **e2e 后端/前端地址一律取自 `e2e/helpers/e2e-env.ts`**（`backendUrl()`/`frontendUrl()`，env → current.json → 默认回落），禁止在 spec 里硬编码 `localhost:7102/7002`（PILOT-050 起，多 worktree 动态端口）。
- **纯逻辑 / 组件行为 → 写成 vitest 单测**（`src/**/*.spec.ts`）。
- 一次性 `.cjs` 脚本只可作「探索期临时探针」，**不得作为验证手段提交或长期依赖**；结论必须沉淀为可重复测试，探针用完即删、不进 `temp/` 留存。
- e2e 视觉检查按 `e2e-testing` 技能 Level 3 清单读图核对，截图固定落 `ForgeSelf.Web/screenshots/e2e/<插件id>/`。
- **需求 → 该跑什么的完整决策表**（含仓内工具 `scripts/get-forge-token.cjs` / `scripts/probe-dll-string.cjs` 的正规入口）→ `docs/04-standards/agent-workflow/a-workflow-core.md` §A4。

### 5.4 设计对齐验证（涉及 UI 变更时）
打开对应设计稿（`forgeself-design/pages/xxx.html`）对比，条件允许时截图对比；布局/间距/颜色须与设计稿一致，组件交互行为须与 `02-spec` 工件描述一致。

### 5.5 验证严重级别
**Critical**（构建失败/测试失败/类型错误）→ 必须修复否则不完成；**Warning**（ESLint warning/覆盖率低）→ 记录 TODO 不阻断；**Info** → 记录不处理。

### 5.6 门禁分档（快 / 中 / 深）— 按**改动面**决定跑多深，不再临场判断
> 立规背景（2026-09-28 用户指令）：只改一个插件却每次都跑全量，全量 e2e 一轮 ≈38 分钟；但全量也确实抓到过我引入的红（缺 BOM 的发版脚本）。故不一刀切「永远全量」，改为按触发条件分档。

| 档 | 跑什么 | 量级 | 触发条件（满足任一即跑） |
|----|--------|------|--------------------------|
| **快** | 本批相关过滤集（`dotnet test --filter`）+ `pnpm run check` + `pnpm run test` + 本插件 e2e | 分钟级 | **每次改完代码必跑**（§5.1/§5.2 的默认形态） |
| **中** | 后端**全量** `dotnet test` | ~15 分钟 | 碰了①宿主源码（`ForgeSelf.Api/**`，含 `XCodeConfig`/`AppBuilder`）②仓库级脚本（`scripts/**`、`*.ps1`）③共享测试基建（`ForgeSelf.Api.Tests` 夹具）—— 理由：`RepositoryScriptTests` 这类**仓库级守卫**只有全量才扫得到 |
| **深** | **全量 e2e**（单配置整跑） | ~38 分钟 | 碰了①`e2e/global-setup.ts` / `playwright.*.config.ts` / `e2e/fixtures/**`（所有插件 e2e 共用）②发版/tag 前③专跑「e2e 基线治理」批次 |

- **禁止拿「快」的结果报「门禁绿」**：汇报必须写明跑的是哪一档、覆盖哪些；跨切面改动停在「快」就报绿 = 违规（本条由 2026-09-28 我拿三入口子集报绿、被用户追问后补跑全量才发现自己引入的 B6 红 而确立）。
- **本地复现 CI/发布链必须同参数、不跳段**（2026-10-02 输入12 立）：例如 `release-local.ps1` 带 `-SkipFrontend` 会跳过前端构建（`vue-tsc -b`），我曾据此报「本地发布链全绿」而 CI 恰在该段失败；本地验证若跳过某段，汇报必须写明「本段未验证」，不得以「本地已通过」代替。
- **基线红先对表再判责**：本 worktree 全量并非全绿（后端 1516/13 红、e2e 102 passed/82 failed，多为环境依赖与陈旧断言；PILOT-050 起后端测试总数因 040-B1 新增已超 1686，基线数字以最近一次全量日志为准）。跑完全量先比对基线清单（项目记忆 `project-baseline-test-reds` / `TODO.md`），**新增的红才是我的**；非我的红也要给真实报错 + 归属并记 TODO，不许一句「无关」带过。
- **深档成本可控化**：全量 e2e 用 4 worker、`--output=<空目录>`；只需复验单插件时仍走 `e2e/plugins/<id>` 定向跑。
- **本节所有 PowerShell 入口一律 `pwsh` 调用**（`release-local.ps1`、`verify-pilot-artifacts.ps1`、`install-git-hooks.ps1`、`package-plugin.ps1`、`sign-publish.ps1`…；写法 `pwsh -NoProfile -ExecutionPolicy Bypass -File <脚本> …`），**禁止 `powershell`（5.1）**——理由与例外见 §2.3；跑门禁的判定要看**日志正文**（`PASS`/`通过数`），不许拿 exit code 当证据。

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
- 详细流程（回滚策略等）→ `docs/04-standards/agent-workflow/a-workflow-core.md` §A5。

---

## 7. Persist — 经验沉淀

每轮循环结束后（无论成功或升级）：
1. **更新 TODO.md**：完成的待办**立即移除**（不留 ✅ 历史堆积，保持队列新鲜）；仅记录新发现的问题与未完成项
2. **更新工件状态**：确认对应 `docs/ai/pilot/YYYY-MM-DD-<task-id>/` 的 00-07 随任务推进补齐（Implement 后补 05-07）
3. **记录决策**：重要技术决策写入对应 `docs/ai/pilot/YYYY-MM-DD-<task-id>/` 或 TODO.md，格式：决策 → 背景 → 理由 → 准则
4. **沉淀规律**：可复用规律（约定/踩坑/ADR）→ 写入 `docs/04-standards/agent-workflow/README.md` 对应小节（入库）；走通的流程技能缺失或可优化 → 回写/新建技能并登记 §2.4；新形成的项目不变规范 → agent-workflow/README.md 或本文

---

## 7.5 文档工作流 — 必须严格遵守（换 AI 也照此执行）

> 目标：**TODO.md 永远是"今天和明天"的活队列**（不随迭代膨胀）；工作日记是"昨天"的归档；MEMORY 是"会话级索引"；docs 是"永远"的规则库。三者靠「检视回流」闭环。
> 完整操作规范（强制顺序图、操作细则、输入档案规则、反同步 SOP）→ `docs/04-standards/agent-workflow/a-workflow-docs-report.md` §A6。

### 7.5.1 四文件职责（角色分工）
| 文件 | 角色 | 内容 | 写入时机 |
|------|------|------|----------|
| `TODO.md` | **队列**（今天/明天） | 🔄进行中 / ⬜待办（优先级 P1>P2>P3 + 来源引用）/ ⏸阻塞（记录不修） | 新输入解析出待办时；待办状态变化时；完成即移除 |
| `.forgeself/memory/YYYY-MM-DD.md` | **日志**（昨天） | 当天完成内容+验证结果、决策记录、遇到的问题与解决、输入原文与任务拆解、**「下一步」字段**（回流连接器） | **输入到达立即写**；执行过程中随做随记；完成时补全 |
| `.forgeself/memory/MEMORY.md` | **会话级索引** | 项目规则索引（指向 agent-workflow/README.md）+ 未沉淀的临时规律；**不再承载不变项目规则**（`.forgeself` 不入库） | 产生临时规律时；沉淀后迁入 agent-workflow/README.md |
| `docs/04-standards/agent-workflow/README.md` | **规则库**（永远，入库） | 工作流细节（Part A）+ 项目不变工程规则与踩坑（Part B） | 产生可复用规律/规则变化时 |

### 7.5.2 强制顺序（带门禁）：每轮任务循环步骤为前置条件
```
[0 输入到达] 用户消息
   │  ★ 立即写日志：当天工作日记「输入 N」条目（原文 + 任务拆解），不拖延
   ▼
[1 Goal]  解析目标 → TODO.md 生成待办项（🔄/⬜，P1>P2>P3，来源:输入N）
   ▼
[2 Context] 收集上下文（docs/ai/pilot 工件 / 设计稿 / 相关代码 / TODO 约束）
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
[提炼] 可复用规律？有 → MEMORY.md 暂存 → 沉淀为 agent-workflow/README.md 规则 + 回写/新建技能（§2.4）；无 → 跳过
   ▼
[检视] 每日/每周：日记「下一步」→ 回流 TODO 新待办；重复模式 → 提炼为规则
   ▼
   └──────► 回到 [0 输入到达]（闭环）
```

### 7.5.3 操作规范要点（完整版 → §A6）
**① 新用户输入到达时**（第一步必做）：立即写日志（输入原文 + 任务拆解 `- [ ]` 清单，**完成一项勾选一项**）→ TODO.md 生成待办（`- [ ] <任务>（P<优先级>，来源:输入<N>）`）→ 开始执行时移到「🔄 进行中」。
**② 执行过程中**：边做边追加当天工作记录；决策记录格式：`决策 → 背景 → 理由 → 准则`。
**③ 任务完成时**（完成 = 代码闭环 + 文档同步 + 待办清除，三者缺一不可）：追加完整工作记录（含验证结果）→ TODO 待办**直接移除**（不留 ✅/[x] 堆积）→ 同步相关文档（docs/ai/pilot 工件 / docs / README）→ 提炼规律入 agent-workflow/README.md → 完善/新建技能并登记 §2.4。
**④ 每日/每周检视**：日记「下一步」回流 TODO；重复模式提炼为规则；清除 TODO 中已完成残留（✅/[x]），保持队列新鲜。
**⑤ 逐回合原文记录（用户 2026-09-28 立，强制）**：工作日记与项目文档必须**逐回合**记录每个对话方（用户、项目管理员哥、需求分析师、测试审查、DevOps哥、项管哥等）的**发言原文 + 序号(seqN)**；Agent 自己的发言同样**原样记录**。**禁止只记结论或转述**。落地写法：每输入一批次先落「原文」，收尾时补「逐回合原文记录」小节（T1..Tn 或真实 seqN），含自己每一次对外汇报的原文要点；被推翻的判断与自己的错误陈述也要原文留档，不得只留更正后的结论。
**⑥ 常见错误（必须避免）**：把输入原文堆在 TODO.md；在 MEMORY 复制待办列表；任务完成不更新 TODO；完成任务只标 ✅ 不移除；不写「下一步」字段；把用户或群内角色的发言写成我的转述/结论。

---

## 8. 设计稿工作流

设计稿位于 `forgeself-design/`，实现层必须对齐其视觉与交互。新增设置项 SOP、file:// 协议限制等 → `docs/04-standards/agent-workflow/a-workflow-docs-report.md` §A7。

---

## 9. 功能开发流程（specs/speckit 已弃用，统一为 §11 AI-Native 闭环）

功能开发流程已统一为 **§11 AI-Native Engineering 九阶段闭环**（产物 `docs/ai/pilot/YYYY-MM-DD-<task-id>/`，见规范）。原 `specs/`（speckit 规格驱动开发：`specify → plan → tasks → implement`）**已弃用**：`specs/` 进入 gitignore 不再入库，相关历史内容以 `docs/ai/pilot/` 工件为准。**禁止再走 speckit 流程或新建 `specs/` 目录**；`.codebuddy/commands/speckit.*.md` 与 `.specify/feature.json` 不再使用。

---

## 10. 任务完成判定与汇报铁律（Verification-Centric Completion）

> 「代码改完 ≠ 任务完成」的硬约束。完整五步自检、汇报模板、示例 → `docs/04-standards/agent-workflow/a-workflow-docs-report.md` §A9。

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
> 产物落点 → `docs/ai/pilot/YYYY-MM-DD-<task-id>/`（每任务一目录，日期前缀规则见规范 §0）｜群 SOP → `ai-native-engineering-loop`
> ⚠️ **唯一开发流程**：任何开发类任务都必须走本闭环并产出 00-07 工件，禁止跳过工件直接写代码，也禁止用已弃用的 `specs/`/`speckit` 替代。
> **用户指令（seq14）**：本流程不与其他体系做映射；开发任务的流程以本节及其规范为唯一依据，与本文或其他文档的流程描述冲突时以 §11 为准。

九阶段：`Repository Understanding → Intent → Spec → Plan → Task → Implement → Test → Evidence → Review`。要点：

1. **先理解仓库再写代码**：技术栈/架构/测试方式必须从真实仓库内容确认，禁止常识推测。
2. **工件链不得跳步**：Intent（为什么/做什么/到什么程度）→ Spec（九节，不确定点标 `Unknown`）→ Plan（具体到真实文件，偏差先记录再修正）→ Task（Allowed/Forbidden + 验证命令）→ 才允许 Implement。
3. **Test/Evidence/Review 不豁免**（任何任务级别）：验证跑真实命令记真实结果；Evidence 只记实际发生（Verified/Inferred/Unknown 分级）；Review 出八问 + Final Decision（APPROVED / CHANGES_REQUIRED / BLOCKED）。
4. **闸门（规范 §1.1 自含）**：闸门1=Intent/Spec/Plan/Task 经用户确认后开工；闸门2=Evidence+Review 齐备后交用户验收，通过前不提交代码；闸门3=验收后提交归档。放权表述只豁免过程汇报频率，不豁免闸门。**工件缺件由 pre-commit hook 硬拦**（`scripts/verify-pilot-artifacts.ps1` 校验 `docs/ai/pilot/YYYY-MM-DD-<task-id>/` 00-07 八件，缺件 `git commit` 直接失败）。
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
### 5.0 本机环境前置（跑测试 / e2e / 发布链前必做 · 2026-10-05 输入2 立）
> 立规背景：以下三条在同一天里分别让快档门禁**假红**、e2e **白跑两轮**、本地发布链**首跑即失败**，且**都不在仓库全量基线里**（「基线红先对表」查不出来）⇒ 先排环境，再判责。
- **代理**：`$env:NO_PROXY='localhost,127.0.0.1,::1'`（小写 `no_proxy` 一并设）。本机注入了 `HTTP_PROXY/HTTPS_PROXY=http://127.0.0.1:10808` 而无 `NO_PROXY` 时，Playwright 对 `localhost:<port>` 的可用性探测**走代理恒 502** ⇒ e2e 报 `Timed out waiting 120000ms from config.webServer`（此时 vite 其实早已 ready，极易误判成前端构建问题）。**不要为此改仓库配置/代码。**
- **临时目录**：`$env:TEMP = $env:TMP = '<repo>\.temp\tmp'`。本机 `%TEMP%` 拒写，同一成因在三面各红一次：① 后端测试 `Temp\<前缀>_<guid>` 被拒 ⇒ 整片假红（形似大面积回归）；② 插件 e2e 的 vite 依赖预构建写 `node_modules/.vite/deps_temp_*` 被拒 ⇒ dev server 退出、`ERR_CONNECTION_REFUSED`；③ **本地发布链宿主前端 `vite build` 的 esbuild 临时文件清理被拒 ⇒ `build-frontend` 段整体失败**。
- **取读数**：判据一律看**日志正文**（`*> <log>` 重定向后读），**不许拿 exit code 当证据**；PowerShell 工具**可能不回显 stdout**，没回显 ≠ 没跑（先重定向再读，别重复执行）。
- 三种形态的实测、控制实验与判据 → `docs/04-standards/agent-workflow.md` §B2。
- **全新 worktree 跑插件 UI e2e 前先补两件缺件（2026-10-07 输入2 立）**：① **SQLite provider** —— `e2e/global-setup.ts:250` 要 `System.Data.SQLite.dll` + `e_sqlite3.dll`，注释声称"仓内 `build/runtime/Plugins` 受版本控制"但**该目录不在仓库里**（`git ls-files build/runtime` 为空），新 worktree 也没有 gitignored 的 `publish/` ⇒ 直接中止。就地解封：从 `ForgeSelf.Api/bin/Debug/net10.0-windows/`（及其 `runtimes/win-x64/native/`）复制到 `build/runtime/Plugins/`。② **各插件前端产物** —— `Plugins/*/web/dist` 是 gitignored，新 worktree 里一个都没有 ⇒ 远程加载的插件页全落在 `.plugin-view-state--error`，表现为「`menu-route-consistency` 某路由停在错误态」「首页面板没数据」，**与被测改动无关**。跑 `for d in Plugins/*/web; do (cd "$d" && pnpm install && pnpm build); done` 后再跑 e2e。两类都记入 `TODO.md` 待固化（入库 or 改 globalSetup 的候选源/加自动构建）。
- **同源自洽的断言不算证据**：路径类断言（`归一后 == REPO_ROOT`）若两边都来自同一个可能写错的变量，错了也会绿。判据要锚在**仓库独有文件**（`ForgeSelf.slnx` 存在）或独立来源上，并加一句"前提自查"阳性对照（`existsSync(<项目根>/docs/ai/pilot/<task-id>)`）。实测：本批 e2e 的 `REPO_ROOT` 少写一层 `../` 拿到 `ForgeSelf.Web`，关联项目断言照样绿，直到工件清单接口回「该项目没有 docs\ai\pilot 目录」才暴露。

