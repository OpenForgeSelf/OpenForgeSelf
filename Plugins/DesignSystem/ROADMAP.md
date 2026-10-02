# 设计插件 roadmap

> 本文件记录设计插件（design-system）接下来「做什么、为什么做、怎么做、做到什么程度算完成」。
> 按优先级 P1 > P2 > P3 排列；P1 是阻塞性/高价值缺口，P2 是体验增强，P3 是生态扩展。
> 每完成一项，请在对应 `- [ ]` 改为 `- [x]`，并在 `README.md §九` 历史里追加版本信息。

## 2026-09-29 状态更新（v2.0.0）

本文件的 P1/P2 多数条目已在 **v2.0.0 落地**，下面逐条给事实与出处；条目正文保留作"当初为什么做"的记录，不要再当待办读。

| 条目 | 状态 | 落地证据 |
|---|---|---|
| P1.1 后端持久化设计产物 | ✅ 已做 | 12 张表（`Data/Model.xml` + xcode 生成，二次生成逐字节一致）；令牌/组件/图标/资产/页面/字体/审计/发布全落 `~/.forgeself/Plugins/design-system/DesignSystem.db`。见 `docs/02-features/036-design-system.md` |
| P1.2 宿主插件版本/更新/安装路径 | ⚠️ 宿主侧 | 与本插件无关的宿主更新链路（另见宿主 update 相关提交），本插件只提供版本号与产物 |
| P1.3 pluginViewLoader 缓存键加 content-hash | ⬜ 未做 | 属宿主加载器；本插件无法自修（已记仓库 TODO） |
| P2.2 自动对比度与截图回归校验 | ✅ 已做 | 后端 `ContrastMath` + `AuditEngine`（WCAG 2.2 判级、critical 拦发布）；界面截图读图走 `ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts`。APCA **明确不作门禁**（`docs/07-decisions/not-taken-decisions.md` 009） |
| P2.3 发布+浏览器走查沉淀为 skill | ✅ 已做 | `design-system-verify` 技能已建并登记 `AGENTS.md` §2.4（四层门禁 + 假能力自查表）；走查读图仍共用 `e2e-testing` Level 3 |
| P1.4 组件变体 × 状态矩阵（新，缺口 G7） | ✅ 已做 | `SeedComponentCatalog` 除目录外还按 `component.<code>.<variant>.<part>[-<state>]` 真实路径推导格子；无令牌支撑的组合不落格。判据已达成：e2e `.cg__vars li >= 20` 且矩阵文本含 `size=(sm\|md\|lg)` + API 回读 `state` 含 `hover`、每格 `tokenRefsJson` 非空数组 |
| P1.5 宿主 SQLite 并发锁治理（新，缺口 G8） | 🟡 已缓解 | 连接串 `Busy Timeout=5000` + 审计/目录**整批一个事务** + 发布按项目**串行化**（同项目并发 POST 不再互相撞锁）；界面只读请求退避重试。跨请求写锁根治仍在宿主 DAL，需人拍板 |
| P1.6 品牌三表可读可写（新，缺口 G9 前半） | ✅ 已做（v2.2.0） | `SeedBrandCatalog` 落字体/起手屏/资产种子 + `POST projects/{id}/assets\|screens\|fonts` + 第 14 个 section「品牌资产」；重跑生成**只补空不覆盖**（用例 `重跑生成只补空_不得覆盖用户改过的品牌行`）；e2e 断言"读得到 + 写得进" |
| P1.7 品牌三表进导出投影（G9 后半） | ✅ 已做（v2.2.0） | CSS 只对登记了文件的字体出 `@font-face`（系统栈成员如实注释）；bundle 落 `brand/*.svg` + `fonts.json` + `screens.json`；DESIGN.md 增「品牌与资产」段。后端 3 条用例钉住（170/170） |
| P1.8 整包在浏览器侧下载（新，缺口 G10） | ❌ 未通 | 服务层产物已验证（体积/构件数），但 `page.evaluate(fetch)` → `Failed to fetch`、点 `<a download>` → `download.path: canceled`；已排除"太慢"（构建 605ms）。**用户点下载这条路径目前没有证据**，续做入口见 TODO |
| P1.9 预览取数竞态（新） | ✅ 已修（v2.2.0） | `loadSkin`/`loadEffective` 改为"只有最新一次调用可写回 ref"；`state.test.ts` 先复现（3 failed）后转绿，e2e 加连点三次守卫；根因与两跑一红一绿记录在 `05-evidence.md` |
| P1.10 版本快照与 diff 覆盖品牌/组件（新） | ✅ 已做（v2.3.0） | 快照 schema 1→2 新增 `specs`（component/variant/asset/screen/font，`kind+key+字段表`）；**规格进哈希**，"只换 logo"不再被幂等放行；schema 1 旧快照比对回 `specsComparable=false`（界面显示"不可比"）。后端 5 条用例 + e2e 在真实宿主里看到 `screen <code> · 新增`（175/175） |
| P1.11 组件规格供给进机器可读产物（新，缺口 G12） | ✅ 已做（v2.4.0） | registry 条目 = 组件目录 ∪ `component.*` 分组，`meta` 带 `anatomy/a11yNotes/states/variants[]/unresolvedTokenRefs`（引用不到的如实列出，不静默丢）；DESIGN.md 增「组件规格」逐蓝本列解剖/状态/轴/令牌条数/可达性要求。**顺带补齐 spec FR13 欠的一半**：`GET api/design-system/{id}/{entity}.json` 十类实体明细端点此前根本不存在，且清单 `total` 里 `design-icon/screen/font-face` 写死 0、`design-component` 数的是令牌条数 → 现在清单与明细同出 `EntityRows`。判据实测（e2e 真实宿主）：`design-color=141 design-shadow=4 design-motion=13 design-type-role=17 design-spacing=10 design-radius=8 design-icon=40 design-component=10 design-screen=5 design-font-face=14`，button `解剖=3 状态=active/default/disabled/hover 格子=13`；后端 4 条用例（178/178） |
| P1.12 Element Plus 换肤接缝（spec U3，缺口 G13） | ✅ 已做（v2.5.0） | 新增导出格式 `element-plus` + bundle 内 `element-plus/theme.<theme>.css`：`--el-*` 只引用同主题 `tokens.css` 真定义的 `--ds-*`（含 `color-mix` 派生与 `-rgb` 三元组），零字面色值；缺档如实列"未映射"并指名缺哪条令牌。EP 的 `light-N/dark-2` 照比例但混合目标改成本页底色/正文墨色（写死 white/black 在深色主题会把悬停洗成灰白）；`--el-color-white/black`、`--el-index-*`、`--el-transition-all/-fade*` 有意不映射并写明理由。判据实测（真实宿主）：**102 个 EP 变量、132 条令牌引用，全部可在 tokens.css 里解析**；后端 3 条用例（181/181） |
| P1.13 门禁盯住"手改之后"（新，缺口 G14） | ✅ 已做（v2.6.0，v2.6.1 修时长族空跑） | 审计新增四类维度，全部报 warning 不拦发布：`target-size`（`*.min-height` ≥24px，WCAG 2.2 2.5.8）/ `ramp-monotonic`（space·radius·duration 按声明序必须递增，档序读 `ScaleGenerators` 同一张表）/ `naming`（点分 kebab，否则投影静默产出坏键）/ `lifecycle-ref`（仍被引用的 deprecated·removed）；密度轴主题单独复查尺度。"生成产物自己必须先过"由用例钉住；两条新维度上线即抓到我自己的错（正则误判 `z-index.1`、消息只报档名不报完整路径），先红后绿修掉。后端 4 条用例（185/185）+ e2e 现场把 `component.button.sm.min-height` 压到 18px→抓到、改回 28px→消失 |
| P1.14 对比度门禁覆盖"库里的全部对"（新） | ✅ 已做（v2.6.2） | `contrast` 不再查一张写死的八对清单：按命名约定从库里推导 `component.<ns>.foreground[-状态]` → `.background[-状态]`/`.background`/`.tint[-状态]`/`.tint`，用户新增组件自动进入判定（实测覆盖 dialog/tooltip/select 等原先从未被查的对）。禁用态按 **WCAG 1.4.3「非活动构件」豁免**：只报读数不拦发布（`rule=wcag22-1.4.3-exempt`，退回 1.4.11 的 3.0 兜底线，≥3.0 info / <3.0 warning，永不 critical）—— 此前按 4.5 判一次冒出 9 条假 critical（按钮禁用态实测 3.41:1），哭狼式门禁会让人开始绕过它。顺带闭合同族第三个洞：`AuditRepository.Record` 现在清掉本轮未产出的旧行，避免"对象不再被判定、库里却留着上一轮 passed 行"被 `HasBlocking` 当绿灯（用旧结论冒充今天查过）。后端 4 条用例（190/190，两条先红后绿）；**v2.6.3 再补一半**：判不成的对象必须自己报 `wcag22-1.4.3-unresolved`（warning，不拦发布），否则它从审计里静默消失 = 与"查过且没问题"长得一样；e2e 现场把 `component.card.foreground` 别名到 `space.4` → 必须出现 unresolved、改回 `semantic.text-1` → 必须消失 |
| P1.15 顺序收成一份真相（新） | ✅ 已做（v2.6.4） | 新增 `DesignSystemConstants.VariantAxes`（`size` xs→xl / `role` / `state` default→…→pressed）：**生成器认轴、矩阵 `SortOrder`、DESIGN.md·registry·Stardust 实体三处排序**全部读它，表外值退回字母序；`GET /meta` 出 `stateOrder`/`sizeOrder` 给前端，界面不再 `.sort()`。动因是 e2e 新断言当场抓到三方不一致（界面 `default、disabled、hover、active` vs 产物 `default、hover、active、disabled`）——只改投影不够，读路径 `ListVariants` 也按 `State` 字母序排过。后端 2 条用例（192/192）+ e2e 钉"界面序 == 产物序 == 词表序" |
| P1.16 尺度档位序也交给后端词表（新） | ✅ 已做（v2.6.5） | `ScaleGenerators` 新增 `SpaceOrder`/`RadiusOrder`/`DurationOrder`（radius 含 `pill`/`full` 两个绝对值档，倍率表里没有它们），`GET /meta` 出 `scaleOrders`；`DensityScales` 删掉前端镜像的 `NAME_ORDER`，`ShadowMotion` 阴影列表改走同一比较器。附带修掉潜伏陷阱：`stepOf` 用 `parseInt` 把 `radius.2xl` 的 "2" 读成档位号，导致 `2xl` 被插进命名档中间（`pill`/`full` 反而排它前面）—— 现在只有整段是数字才算数值档。vitest 4 条新用例（53 passed）+ e2e 钉界面序 == `meta.scaleOrders` |
| P1.17 界面词表族收尾（新） | ✅ 已做（v2.6.6） | 最后三份手抄词表清零：`TIER_ORDER`→`meta.tiers`（含层级下拉）、`colorFamilies` 的 `brand/accent/…`→`meta.colorFamilies`、`AuditBoard.KINDS`→`meta.auditKinds`；补格子的 `state` 自由文本改下拉 + 显式「自定义」逃生口。两张新表都经"双向核对"：色族表由生成器逐族消费（族集合/顺序 == 表），审计表与 `AuditEngine` 产出互核（表内无产出 = 空声明、产出无表 = 界面筛不到）。守卫 `vocabulary.test.ts` 堵再抄（反向探针验证会红）；`/meta` 补 `roleOrder` |
| P1.18 契约可见 + 变体表单收口（新） | ✅ 已做（v2.6.7） | `/meta` 出 `entities`，「导出交付」新增十类逻辑实体插座表（真实 url + 页面**实际读回**的 `total`，换主题整表重读）；`ListVariants` 的**分组序**改读 `VariantAxes`（原先按 `VariantKey` 的 JSON 字典序 → `danger` 排在 `primary` 前）；三条并列的 `stateOrder`/`sizeOrder`/`roleOrder` 合并成一份 `variantAxes` 清单（加轴只改一处）；「新建变体」改成**先选轴再选档位**、JSON 由界面按后端 canonical 拼（多轴走显式「直接写 JSON」）；`.ds-mono` 补 `text-transform: none`（图标 code 曾被父级 uppercase 显示成 `DASHBOARD`） |
| P1.19 画布投影身份收成状态 + 导出读路径去 N+1（新） | ✅ 已做（v2.6.8） | 新增 `skinTheme`/`skinApplied`：**"画布实际是哪一档"从肉眼看明暗变成状态**，主题条按 `applied`/`pending`/`unloaded`/`unavailable` 四态说清（投影只在「看效果」页注入，在别的页切档不重取 → 同一步骤截图两次一暗一亮），预览画布顶部角标显示档位与 `surface-bg`/`brand` 字面值。`cssVar` 修好（原正则把 `--` 前缀拼错、且零调用点）并加 `resolveCssVar` 顺 `var()` 链，角标值与后端令牌、渲染像素三方逐位对齐。导出/快照的变体读从逐组件 N+1 改一次批量（行序一致性有断言钉），导出页读回限流 ≤3 —— 实测把宿主 SQLite 顶出 `database is locked` 随机 500。这**只降低争抢强度、没有消除**：连跑三轮 e2e 的只读 500 是 2/0/3（全靠只读退避重试兜住，用例才绿），宿主跨请求写锁仍待拍板。新增门禁「每种声明格式 × 每个主题都真能导出」（以前全格式只跑浅色档） |
| P1.20 DTCG 导入/回流（新） | ✅ 已做（v2.7.0） | `POST projects/{id}/import/preview` + `/import`（`format=dtcg`；`/meta` 声明 `importFormats`/`importLimits`，界面据此启用入口）。解析器 `DtcgImporter` 纯函数、不静默丢条目：`$type` 沿树继承（与导出侧互逆），推不出逐条拒；**库里已有路径层级以库为准**（按首段猜会把 `chart.series-1` 降级成 primitive，别名逆指上层触发整批图校验拒绝——实测踩过）；**不搬家**（库里有就写回原主题，否则 round-trip 把共享 primitive 复制进主题层）；provenance 只进列不进 `$extensions`。默认 `overwrite=false` 保护手改行（冲突逐条列出）；成环/悬空/同文件重复**整批不写**回逐条诊断；上限 5000 条 / 4MB 解析器内判。导入令牌进同一套对比度门禁。「导出交付」页导入回流块：选文件 → 预览（不落库）→ 确认 → 生效值可查；e2e 钉"导出→导入→导出 55517 字节逐字一致 + 成环拒写 + 别名顺到字面值"。后端 `ImportTests` 11 条（round-trip / 不搬家 / 层级以库为准 / 手改保护 / 上限拒 / 门禁联动等） |
| P1.21 Agent 工具层（新，缺口 G15） | ✅ 已做（v2.8.0，M1） | 向宿主工具注册表暴露 **8 个 `design_*` 工具**（读 6 写 2，唯一真源 `Agent/DesignToolIndex.cs`）：`design_guide/context/lookup/review/audit/presets/create/edit`；外部经 McpCenter 网关 `universal_tool` 转发、内置 AIAgent 白名单 `ToolScopePluginIds` 含 `design-system`。配套：REST 对等端点（`quick-create`/`brief`/`review`/`presets`/`agent-access`/`agent/tools`）、`brief`/`agent-rules` 导出格式、**写开关 AgentAccess**（fail-closed：文件损坏→只读）。消费侧指南见 `.agents/skills/design-system-consume/SKILL.md`；判据/偏差/证据见 `docs/ai/pilot/2026-10-01-design-system-m1-agent-tools/`。验证：后端 DesignSystem 过滤集 335/335（发现数==总数）、AIAgent\|McpCenter\|Sems 215/215、插件 web 三件绿、e2e 完整 design-system 目录 7/7 |
| P3.1 Figma / Tokens Studio | 🟡 半 | Tokens Studio `$themes` + 每主题 set 已可导出；**DTCG 单文件导入/回流已做（v2.7.0）**；剩余：Tokens Studio 完整格式（`$themes`/多 set 文件）导入、Figma 双向同步 |
| P3.2 社区预设市场 | ⬜ 未做 | 依赖宿主分发链路，未启动 |

> 另外，v1 自评里"生产级（持久化）✅"是**错的**（当时只有 localStorage），README 已按 v2 事实重写；
> v1 的 `web/src/design/generate.ts` / `exporters.ts` / `presets.ts` 等前端自算实现已整体下线，
> 设计值只由后端 C# 产生（决策见 not-taken-decisions 010）。


---

## 2026-10-01 升级计划（v2.8.0 → v3.1.0；**规划已完成；M1/M2 已实现、待规划方复验，M3 待开工**）

> 用户目标（2026-09-30 原话）：「都能产出令人满意的设计，输出完整的设计系统。后续项目开发，关于前端 ui ux 的一切都依据该插件产出的设计系统，唯一真源，开发完页面审查也可作为依据」。
> 现状缺口：① 只能由人在界面里操作，Agent（MCP / 内置 AIAgent）没有工具可用；② 非设计师不会用——没有"试穿"体验与向导；③ 风格几乎只能换色；④ 只有 UI 令牌，没有 UX 规范，"唯一真源"只覆盖一半。
> 方案、契约、验收标准全部落在 `docs/ai/pilot/` 下三个任务目录（00–07 八件工件齐备，门禁脚本均 PASS）。**实现由其他 AI 完成，完成后由规划方独立验收**（验收清单已预注册在各自 `06-review.md`）。

| 里程碑 | 版本 | 范围 | 任务目录 | 闸门 / 前置 |
|---|---|---|---|---|
| **M1 · Agent 工具层** | 2.8.0 | 8 个 `design_*` 工具（经 McpCenter 网关 + 内置 AIAgent 白名单 `design-system`）、`brief`/`agent-rules` 导出、确定性审查引擎（硬编码色/魔法数字/未知令牌 + 最近令牌建议）、8 个风格预设与推荐、`design_create` 一步建系统、写开关 | `2026-10-01-design-system-m1-agent-tools/` | 闸门1 ✅；**实现完成**（05-evidence 已回填；后端 335/335、AIAgent·McpCenter·Sems 215/215、web 三件绿、e2e 7/7）；闸门2 ⬜（待规划方复验签 06/07） |
| **M2 · 展厅与向导** | 3.0.0 | 四模式外壳（开始/展厅/工作台/交付与接入）、"模特穿衣服"展厅（五类场景：后台/中台管理、工具/工作台、状态板、官网/落地页、移动端 H5）、向导、交付与接入页、术语词典、`generate/preview-css`（预览零写库且与导出同源）；原 14 个专业 section 与既有 e2e 断言不动 | `2026-10-01-design-system-m2-showroom-wizard/` | 前置 M1 闸门2；闸门1 ✅（2026-10-02 批准）；**实现完成**（05-evidence 已回填；插件 web 三件绿、后端过滤集总数==发现数、既有 e2e 无新增红、视觉 QA 30 张截图逐张读图）；闸门2 ⬜（待规划方复验签 06/07） |
| **M3 · 风格轴与 UX 规范** | 3.1.0 | 7 个风格轴（阴影/描边/中性色温/字体搭配/圆角风格/强调色策略，默认值逐字节兼容）、预设 8→13、新增 `DesignGuideline` 表与 14 条确定性默认 UX 规范（可编辑、进快照与全部交付物、工具与 checklist 可读写）、第 15 个 section「UX 规范」、快照 schema 3 | `2026-10-01-design-system-m3-style-guideline/` | 前置 M1、M2 闸门2；闸门1 ⬜（含新增表等 5 项显式批准，见其 `04-task.md`） |

**明确不做（本轮边界，记为后续候选）**：`DesignGuideline` 不进 Stardust 十类实体（外部契约不动）；不新增审计类别（规范引用失效仅 `brokenRefs` 提示，候选）；不扩充组件蓝本（候选 M4，会改变默认产物）；不回填存量项目的规范；不接 LLM。

---

## P1 · 现在就应该做

### P1.1 后端持久化设计产物

**现状**：当前工作区与历史存在 `localStorage` 中。刷新页面可恢复，但换浏览器/清缓存即丢失；无法跨设备共享，也无法作为「设计资产」沉淀。

**目标**：让生成的 `DesignSystem` 可保存、可列表、可载入、可删除，并成为项目内的可追踪资产。

**实现方案**：
1. 后端新增实体 `DesignRecord`（XCode 实体，按现有 `ForgeSelf.Api` 分层）：
   ```csharp
   public class DesignRecord {
       public long Id { get; set; }
       public string PluginId { get; set; } = "design-system";
       public string Name { get; set; } = "";
       public string Brief { get; set; } = "";
       public string Industry { get; set; } = "";
       public string SeedJson { get; set; } = "";          // hue/accentHue/fontKey
       public string DesignSystemJson { get; set; } = ""; // 完整 DesignSystem 序列化
       public long UserId { get; set; }
       public DateTime CreatedAt { get; set; }
       public DateTime UpdatedAt { get; set; }
   }
   ```
2. 新增控制器 `DesignRecordsController`（路由 `api/design-records`）：
   - `GET /api/design-records` — 当前用户的列表（支持分页）
   - `GET /api/design-records/{id}` — 详情
   - `POST /api/design-records` — 保存（name/brief/industry/seed/designSystem）
   - `PUT /api/design-records/{id}` — 更新
   - `DELETE /api/design-records/{id}` — 删除
3. 前端 `DesignStudio.vue`：
   - 历史区从 `localStorage` 改从 API 拉取；保留 localStorage 作为离线降级。
   - 生成后自动 POST 保存（或提示保存）。
   - 新增「云端历史」vs「本地历史」切换/合并。

**验收标准**：
- `dotnet build` + `dotnet test`（新增 xUnit 覆盖 CRUD）通过。
- 前端 `pnpm run check` + `pnpm run test` 通过。
- e2e 验证：生成设计 → 刷新 → 从云端列表载入 → 展示页换肤一致。

**风险**：涉及数据库迁移、鉴权、用户隔离。按 AGENTS §6.4，**必须升级给人确认 schema 与数据归属策略后再写代码**。

**不做的替代方案**：继续增强 localStorage（导出/导入 JSON），直到有明确的团队协作需求。

---

### P1.2 修复宿主插件版本/更新/安装路径

**现状**：
- `PluginVersionService.Initialize(pluginsPath)` 从未被调用，导致 `/api/plugin/updates|update|versions|rollback` 死代码。
- `POST /api/plugin/install` 把插件包解压到 exe 所在目录，而不是 `publish/plugins/<id>/`（活动代码目录）。
- 新插件热重载（FileSystemWatcher）只对已存在插件生效；全新插件必须 install 触发 `DiscoverPlugins()`。

**目标**：让插件版本管理、热更新、全新安装都走正确路径，发布插件不再依赖人肉目录搬运。

**实现方案**：
1. 在宿主启动流程中调用 `_pluginVersionService.Initialize(pluginsPath)`（pluginsPath = `AppContext.BaseDirectory/plugins`）。
2. 修复 `PluginInstallerService.InstallFromPackage`：
   - 包体应解压到 `pluginsPath/<id>/` 而不是 `AppContext.BaseDirectory/<id>/`。
   - 解压后调用 `_pluginManager.DiscoverPlugins()` 并返回新插件信息。
3. 修复 `run-plugin-publish-verify.ps1` 中的版本化 API 调用：当前因 `Initialize` 未调用，脚本实际走的是热重载/安装兜底；若 P1.2 修复后，应优先使用版本化 API。

**验收标准**：
- 安装新插件后，文件正确出现在 `publish/plugins/<id>/`。
- `/api/plugin/updates` 返回当前可用更新列表（不再 500/空）。
- `run-plugin-publish-verify.ps1` 在不重启宿主的情况下完成「首次安装 → 启用 → 版本切换」全流程。

**风险**：改动宿主核心插件管理，影响所有插件。需完整回归所有插件 e2e。

---

### P1.3 pluginViewLoader 缓存键加入 content-hash

**现状**：前端入口 URL 是 `/plugins/{id}/web/dist/index.js?v={version}`。版本号不变时，浏览器从磁盘缓存取旧 bundle；前端小改动（文案、icon）用户必须硬刷才看得到。

**目标**：同版本内的前端改动也能即时生效，同时保留版本号用于兼容性校验。

**实现方案**：
1. 插件构建脚本生成 `dist/index.js.sha256`（或把 hash 写入 plugin.json `frontend.hash`）。
2. 宿主 `plugin.json` 解析时读取 hash，清单 URL 输出 `?v=1.2.0&h=abc123`。
3. 前端 `pluginViewLoader` 以 `v + h` 作为缓存键；hash 变化即重新 fetch。

**验收标准**：
- 不升版本号，仅改前端文案并热发布后，新窗口访问立即看到新文案。
- 升版本号时 hash 同时变化，旧缓存失效。

**风险**：需改动宿主清单生成逻辑；要确保插件旧版本（无 hash）兼容。

---

## P2 · 体验增强

### P2.1 LLM 增强模式（可选开关）

**现状**：生成引擎是确定性模板。对「我需要一套面向印度二三线城市、低带宽、多语言、RTL 适配的 B2B 批发 App」这类复杂需求，只能给出通用电商骨架。

**目标**：在保持确定性引擎作为 fallback 与回归基线的前提下，给用户提供「智能生成」开关。

**实现方案**：
1. 前端新增「智能生成」toggle（默认关闭）。
2. 后端新增 `DesignSystemController` 端点 `POST /api/design-system/generate`：
   - 接收 brief + industry hint。
   - 调用 LLM（复用项目内现有 AI 网关 / OpenAI / Anthropic provider）。
   - LLM prompt 要求按 `DesignSystem` schema 输出 JSON。
   - 输出经校验后回退到确定性引擎（LLM 失败/超时/格式错误）。
3. 前端同时保留离线能力：LLM 不可用时自动切回本地 `generateDesignSystem`。

**验收标准**：
- 关闭智能生成：同需求两次生成完全一致（回归测试）。
- 开启智能生成：复杂需求输出比模板更贴合语义。
- LLM 失败时无白屏，自动降级。

**风险**：引入外部依赖与成本；输出不可完全回归，需要 separate e2e 路径。

---

### P2.2 自动对比度与截图回归校验

**现状**：设计自检第 2 条（对比度）靠人读；展示页换肤后无自动视觉回归保护。

**目标**：把 WCAG 对比度校验与视觉回归纳入 e2e，防止换肤后出现不可读文本或布局崩坏。

**实现方案**：
1. 在 e2e 中用 Playwright 生成常见 hue（紫/蓝/橙/绿）的设计系统，对每个生成结果：
   - 读取 `tokens.color.text.fg-1` vs `tokens.color.surface.surface-1`，用 `colorjs.io` 或内置公式计算对比度。
   - 断言正文对比度 ≥ 4.5:1，大文本/按钮 ≥ 3:1。
2. 建立截图基线（golden shots）：
   - 基线目录 `e2e/snapshots/design-system/`。
   - 每次 e2e 对「设计令牌 / 组件库 / 实时预览 / 控制台 UI Kit」截图并与基线做像素 diff（阈值可配置）。
3. 基线更新命令：`pnpm test:e2e:update-snapshot`。

**验收标准**：
- 新增对比度断言，失败时报告具体 hue 与颜色对。
- 基线 diff 失败时 e2e 不通过（除非显式更新）。

**风险**：不同机器/字体可能导致截图抖动；需要稳定的 headless 环境。

---

### P2.3 把真宿主发布 + 浏览器走查沉淀为可复用 skill

**现状**：每次真宿主验证都要读 `.agents/skills/plugin-publish-verify/SKILL.md` 并手动组合 PowerShell 命令 + Playwright MCP。流程已跑通但未封装。

**目标**：新增 `.agents/skills/forge-design-system-verify/SKILL.md` + 脚本，支持一键：
- 发布/热重载到指定宿主；
- 用 Playwright 打开插件；
- 生成设计系统；
- 切页截图；
- 下载产物并校验。

**验收标准**：新 skill 能在一次调用内完成上述全部步骤，输出通过/失败报告与截图路径。

---

## P3 · 生态

### P3.1 Figma / Tokens Studio 插件

**现状**：`tokens.json` 是自定义 schema，设计工具无法直接消费。

**目标**：让生成的 token 能导入 Figma / Tokens Studio。

**实现方案**：
1. 在 `exporters.ts` 新增 `toDesignTokensFormat(ds)`，输出 [Design Tokens Format](https://design-tokens.github.io/community-group/format/) 兼容 JSON。
2. 提供 `figma-tokens.json` 下载选项。
3. 文档说明导入步骤。

**验收标准**：导出的 `figma-tokens.json` 可被 Tokens Studio 插件成功导入，颜色、字号、间距分类正确。

---

### P3.2 社区预设市场

**现状**：预设硬编码在 `presets.ts` 中，新增预设需改代码并重新构建插件。

**目标**：支持从外部文件/目录加载预设，用户可分享自己的设计系统预设。

**实现方案**：
1. 在插件数据目录 `~/.forgeself/plugins/design-system/presets/` 下读取 `*.preset.json`。
2. 新增 `PRESET_LOADER` 在启动时扫描并合并到 `PRESETS` 列表。
3. 工作台新增「加载预设」下拉，支持一键把预设设为 activeDs。
4. 支持导出当前生成结果为 `.preset.json`。

**验收标准**：
- 把一个符合 schema 的 `.preset.json` 放入 presets 目录，重启宿主后在工作台可见。
- 导出预设再导入，内容一致。

---

## 附录：roadmap 维护规则

- 新增条目使用 `- [ ] 编号 标题（P级别）` 格式，放在对应优先级分区末尾。
- 完成条目改为 `- [x]`，并在 `README.md §九` 历史里追加版本与日期。
- P1 条目若涉及后端 schema / 迁移 / 鉴权 / 宿主核心，必须先走 AGENTS §6.4 升级给人确认。
- 每轮任务结束时，检查本 roadmap 是否有过时条目（已完成的移至历史、已放弃标为不做的决策）。
