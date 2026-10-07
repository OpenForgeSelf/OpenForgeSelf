# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。
> Task ID：PILOT-053 ｜ 审查日期：2026-10-06 ｜ 审查者：本会话自审（**非独立验收方**，闸门2 仍需用户签）

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？**
   满足。Intent 的五段（提示词出去 / 调用进来 / 本机跑掉 / 结果原样送回 / 留档可回看）都有对应实现与证据：
   `GET prompt` 与 `POST parse/turn`、`FileExecutor`/`CommandExecutor` 真执行、`ResultFormatter` 两模式回粘、`TurnLedger` 落文件。
   e2e 的一轮完整回合给出真实数据（写 38 字节 → 读回原文 → `git version 2.49.0.windows.1` → `rm` 被守卫拒）。
2. **实现是否符合 Spec？**
   AC1~AC16 逐条有对应判据（05 的读数节）。两处与 Spec 措辞不同并已回写：端点数 7→8（03-plan 偏差表）；
   BC-11 危险根在 e2e 宿主形态下**默认根自身也在仓库树内**，用例改为"未确认拒 / 确认后放行"三步（05 E2E 节）。
3. **是否超出了 Scope？**
   未扩：不接 AI API、不做会话树、不注册宿主工具、不放宽守卫、不加删除/清理自动化（全部进 not-taken 034~037）。
   两处"看起来像扩"的说明：① `POST execute` 端点与 `GET turns/{id}` 是 02-spec Output 表既定项；
   ② 补 `pnpm-lock.yaml`/`pnpm-workspace.yaml` 属"让发布链不红"的必要件，Plan 期调研漏了这条（已记偏差）。
4. **是否修改了不应该修改的文件？**
   宿主侧只动两行 csproj（登记）；`Plugins/AIAgent/**`、`XCodeConfig.cs`、宿主前端源码、任何 `package.json`/vite/playwright 配置**零改动**
   （`git status --porcelain ForgeSelf.Web/src` 空输出为证）。文档面动了 4 个既有文件（技能×2、not-taken、TODO/日记），属 §7 要求的回写。
5. **测试是否覆盖 Acceptance Criteria？**
   覆盖 140 条后端用例 + 5 条 e2e。AC13（宿主产物判据）与 AC16（档位申报）是流程性判据，已按 05 的读数申报。
   缺口如实列出：无 WebApplicationFactory 集成层用例（靠反射守卫 + e2e 真 401）；AC15 的"三空态"目前只有 2 个有 e2e 断言（未粘贴 / 未识别），"台账空"态由页面文案与走查覆盖。
6. **是否存在明显回归风险？**
   低。新增面全在插件目录内；宿主唯一改动是构建顺序引用（缺它才是回归源）。
   真实风险点两条：① 守卫两份实现的漂移（已用常驻对账钉住，探针 A 证明能红）；② 台账只增不删（磁盘增长，已在文档与界面写明）。
7. **是否存在架构不一致？**
   一致：插件只引 Core/Abstractions；无实体 ⇒ 不碰 `PluginDbs`（决策 D2 与规范"不改 DB 结构"同向）；
   管理面类级鉴权（铁律 17）；界面归插件（铁律 3）；菜单/路由只在 `plugin.json` 声明一处（铁律 19①）；
   Apply 内不抛异常（SamplePlugin 权威注释）；数据落 `ctx.EnsurePluginDataDirectory()`。
   一处**有意偏离**并登记：命令守卫没做成一份共享实现（D1，理由与 ADR 触发条件见 not-taken 034）。
8. **Evidence 是否足以证明任务完成？**
   足以证明"代码闭环 + 已验证项"：所有 Verified 项都有命令与日志路径；未做项（发布③、运行实例复验⑤、全量对表终态）
   在 05 的 Unresolved 表里显式列着，**不得**被读成已完成。

## Requirement Check

**PASS**（Intent 五段 + AC1~AC16 均有实现与判据；U-1 真实样例缺口如实标 Unknown）

## Scope Check

**PASS**（Allowed/Forbidden 未被越；两处必要补充件已在 03-plan 偏差表登记）

## Test Check

**PASS_WITH_NOTE**
- PASS：本插件过滤集 140/140（Verified）、四条反向探针各自实红、插件层 e2e **6 passed**（真实前后端，含 execute 不落档与匿名 401）。
- NOTE 1：宿主 vitest 有 1 条**非本批**红（`SettingsView.test.ts` 计时 flake，两次读数 1↔3 摆动，宿主前端源码零 diff），已记 TODO。
- NOTE 2：**中档全量后端未跑成**（`testhost (34664)` 文件锁，属并行会话占用；未杀他人进程）。因此 AC16 的"全量 + 基线对表"目前是 **Unknown**，不得报成门禁全绿；等价证据（Release 全图 publish + Debug 全图 build + 过滤集）已列在 05。

## Architecture Check

**PASS**（分层/依赖边界/鉴权/数据落点全部对齐既有规范；唯一偏离 D1 有记录、有对账判据、有 ADR 触发条件）

## Risk

**L2**
- 依据：新增独立插件（不改存量行为）+ 会在用户机器上写文件与起进程（已由白名单、沙箱根、类级鉴权三层约束）；
  无 DB 结构变更、无依赖新增、无生产/发布动作。

## Findings

### Critical

无。

### Major

1. **发布链缺件**（已由本批修掉并留证据）：新建带界面插件若缺 `pnpm-lock.yaml`，CI 的 `pnpm install --frozen-lockfile` 必红。
   已回写为 plugin-development 铁律 20 —— 下一个插件不该再靠运气发现。
2. **UI 竞态**（已由 e2e 抓到并修）：首屏渲染早于数据返回，迟到的 `loadAll()` 会覆盖用户正在输入的工作根。
   修法不是"用例多等一下"，而是视图侧 `workspaceTouched` 守卫；e2e 同时补"等数据真回来"的等待点。

### Minor

1. 探针 B 首次写成不可达代码 ⇒ 假绿一轮。教训已入技能（探针必须体现为"结果变化"）。
2. 提示词/工具描述里出现过手抄的白名单副本（探针 C 暴露）⇒ 已改为从守卫常量派生；同类"第二份真相"在 `ToolSpec` 内已清。
3. `ResultFormatter` plain 模式最初用 `AppendLine()`（CRLF）会破坏"原文逐字节"判据 ⇒ 改显式 `\n`；这条属跨平台文本处理的通用坑，值得进 agent-workflow（本批未写，避免动全局规范文档时与他人改动冲突）。
4. 界面「保存工作根」按钮被 flex 拉满整行（截图读图发现）⇒ 已修，并在 21:15:57 的新截图里复验消失（三个动作按钮按内容宽排在右侧）。
5. 04-task 里"README 同步一行"经实测是错的方向（该表派生自 `features.ts`，插件功能不入表）⇒ 改为 not-taken 036 记录。

## Final Decision

**APPROVED_WITH_PENDING**（本会话自审结论，**非独立验收方**，闸门2 仍需用户签）
- 已批准推进到"交用户验收（闸门2）"：实现完整、判据齐备、无 Critical/Major 未决项；e2e 6 条全绿、读图已核对。

## 补记（2026-10-06 23:5x）：上述结论被现场推翻一项

**本审查的 "无 Critical/Major 未决项" 判错了。** 用户实际使用（把网页 AI 回复粘进界面点「解析」）当场暴露 **Critical**：
`CallParser` 只在 ``` 围栏内试 JSON ⇒ **裸 JSON（无围栏）整段判未解析**，而这正是"从网页聊天复制代码块"最常见的形态。
读数、红→绿过程与修复见 `05-evidence.md` 补记第 7~14 行（ToolBridge 层回归 **145/145**）。

**为什么审查没抓到（这是流程缺陷，不是运气差）**：
1. **审查八问第 5 问"测试是否覆盖 Acceptance Criteria"我当时是按"用例数量与 AC 映射表"打的勾**——
   而 145 条用例的输入全部由我自己构造、且**统一带围栏**，等于拿同一个假设自查三遍；
   真实输入（U-1）在规格里已标 `Unknown`，**却没被当成收口阻塞项**，"未知"因此没拦住"完成"。
2. **e2e 用的 `AI_STYLE_TEXT` 也是我自己写的样本** ⇒ 深档（插件层 e2e）与快档（单测）同源，链路上没有任何一环引入过外部真实产物。
3. 出包链连续三轮被并行会话阻塞后，注意力被"发出去"吸走，没有先做一轮"以用户视角把主流输入走一遍"的走查（本批只读图看了界面，没实测输入形状）。

**已机制化**：`plugin-development` 新增 **铁律 21「输入形状矩阵」**——真实样例驱动 / 每种格式「带围栏 + 裸发」成对 +
裸发必配"像结构却不是调用"的反向护栏 / 空态文案要显示解析器给的 `reason` 原文；
并记 `dotnet test` 的 TEMP **必须在 pwsh 内部赋值**（Git-Bash `export` 不传进 testhost ⇒ 一片 `UnauthorizedAccessException` 假红）。
**闸门2 状态更正**：本补记之前签的 APPROVED_WITH_PENDING 不再有效，需按修复后的 1.0.2 重新验收。

- **不得**据此宣称任务完成，仍待：① **中档全量后端 `dotnet test`**（被并行会话 `testhost` 文件锁挡住，Unknown）；② 用户验收（闸门2）→ 提交授权（闸门3）；③ 发布③（打 tag / 本地更新源）与运行实例 :51888 **只读复验⑤**（需用户先升级宿主）。
- 若用户验收要求改动 ⇒ 回退到 Stage 5（Implement）并按需重跑 Test/Evidence。
