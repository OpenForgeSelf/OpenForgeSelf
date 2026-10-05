---
name: design-system-verify
description: 设计系统插件（design-system）的验收与自查流程。四层门禁（后端测试 / 插件前端 check+test+build / 插件层 e2e 真实宿主 / 本地 zip 发布走查）加一张"假能力自查表"——防止"页面有数字但数字是假的""主题是装饰""导出是空声明"这类只有跑起来才暴露的缺陷。改动 Plugins/DesignSystem 任何一层后用本技能收口。
---

# 设计系统插件验收（design-system-verify）

> 适用：`Plugins/DesignSystem/`（后端 Services/Controllers/Data + 自带界面 `web/`）任何改动后的收口。
> 通用插件流程见 `plugin-development`；本技能只补 design-system 特有的**判据**。
> 立项目标与验收标准：`docs/ai/pilot/design-system-v2/02-spec.md`（AC1–AC25）、功能文档 `docs/02-features/036-design-system.md`。

## 一、四层门禁（顺序执行，全绿才算完）

```bash
# ① 后端：表/别名图/生成/审计/导出/版本/内置图标
#    必须带 verbose logger：quiet 模式下测试主机中途崩溃时会把"已跑条数"当总数并报"失败 0"（实测假绿）
dotnet build Plugins/DesignSystem/DesignSystem.csproj
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"
#   核对：报告里的"测试总数"必须等于 --list-tests 的发现数（M2 起为 362；数字随用例增长，一律以 --list-tests 为准）；不等就是事故，不算通过

# ② 插件前端（check/test 都借宿主工具链，插件本身不装 vue-tsc/vitest）
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build

# ③ 插件层 e2e（真实宿主 + 真实插件库 + 截图读图，零 mock）
#    目录含五个 spec + 一个 helpers：工作台 / Agent 工具 / M2 展厅（A 向导·展厅、B 场景·对比、C 交付·深链·可达性、D 视觉 QA 矩阵）
#                                          / M3 风格轴（style：轴进请求体 + 轴进交付 CSS）/ M3 UX 规范（guidelines：G1 入口与同源数值、G2 改与归档恢复、G3 只补空与新建草稿）
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system

# ④ 发布与走查（不打 tag，只出本地 zip；本地交付包必须带 -Sign，脚本执行统一 pwsh 见 AGENTS §2.3）
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\release\release-local.ps1 -Version vX.Y.Z -UpdateDir <目录> -Sign
```

判据（缺一不可，别用"看起来对"替代）：

| 层 | 通过判据 | 常见假绿 |
|---|---|---|
| ① | 测试总数与 discovery 一致（不是"失败0/总计1"）；生成/审计/导出/发布各有归属用例 | 只跑子集就当全绿 |
| ② | `check` 0 error（不是 0 failure）；`build` 产出 `dist/index.js` + `style.css` | 只 build 不 check，模板里的隐式 any 混过 |
| ③ | 关键状态用 `request`/`page.evaluate(fetch)` 复核**后端事实**；截图逐张读图 | 只看 DOM 消失 |
| ④ | 页面「更新源=本地目录」检查更新 → 下载 → 重启并更新由用户点；agent 只验证产物与哈希 | 停/启用户宿主（禁止） |

④ 的"产物验证"必须是**包内容**而不是"脚本跑成功"（本轮做法，可照抄）：

```bash
# 解包到仓库外（用完即删），用仓内正规探针核实现是否真进包
cd "$TEMP/dsz" && unzip -o -q <repo>/artifacts/release/OpenForgeSelf-<ver>-win-x64.zip "*DesignSystem.dll" "*web*index.js"
node <repo>/scripts/probe-dll-string.cjs "Plugins/DesignSystem/DesignSystem.dll" "SeedComponentCatalog"
# 前端 minified 产物探"实现指纹"（正则/常量/选择器字面量），不要探被压掉的函数名
grep -c -F -- '--ds-[\w-]+' Plugins/DesignSystem/web/dist/index.js
# 宿主改动同样探宿主程序集
node <repo>/scripts/probe-dll-string.cjs "ForgeSelf.dll" "Busy Timeout=5000"
```

- `SHA256SUMS.txt` 是 CRLF：`sha256sum -c` 会报 "No such file or directory"，**这不是包坏了**，逐字比对哈希或先 `tr -d '\r'`。
- ① 的"计数口径"要固定写进证据：本轮 `~Plugins.DesignSystemTests` 得 85、`~DesignSystem` 得 154（都 0 失败），
  差 69 项未解释前**禁止**对外说"共 N 项"（历史坑：同一目录三个数字 85/154/162 混用过）。

## 二、假能力自查表（本项目踩过的真实坑，逐条问"现在有证据吗"）

design-system 的失败模式很特殊：**界面有数字、有颜色、有主题，但下面没有真东西**。所以每次改完按表自查：

1. **主题是真的吗**：切到 `dark` 后 `semantic.surface-bg` 有效值是否真的不同？切 `compact` 后 `space.4` 是否真的变小？
   （历史坑：尺度令牌全在共享层，密度主题只换名字 → "mode 轴"是装饰。）
2. **导出是真值吗**：`export?format=css` 里 `--ds-shadow-elevation-*` 是否是空声明？`dtcg` 的 `$value` 是否复合类型给成空对象？
   （历史坑：复合令牌真源在 `ValueJson`，`RawValue` 只读 `Value`，导出 `--ds-x: ;`。）
3. **界面上的计数是真的吗**：项目列表"令牌数"是否与 `tokens/effective` 条数一致？
   （历史坑：`TokenCount` 是没人维护的缓存列，页面显示"有效令牌 250 / 令牌数 0"。）
4. **对比度是算出来的吗**：`tokens/effective` 的 `contrastRatio` 是否 > 0（不是写死 -1）？`colorHex` 是否来自解析后的颜色而不是原始 value 字段？
5. **门禁只报真账**：审计 critical 是否可能由"表头 vs 卡片底"这类无意义配对产生？空主题是否被当 80 条假 critical？
   门禁一次假警报，之后整套就会被无视。
6. **内置图标真的落地了吗**：`DesignIcon` 里 `Collection='forge'` 且 `License='Owned'` 的行数是否 ≥24、`SvgBody` 是否有真图形？
   （历史坑：`SeedBuiltinIcon` 写好了却没人调用。）
7. **预览与交付同源吗**：换肤是否走后端 `export?format=css`（`:root`→`.ds-skin` 收窄 + 变量别名），而不是前端再算一套？
8. **前端有没有偷偷长回第二套实现**：`web/src/design/` 里出现色彩/尺度/导出计算就是危险信号（v1 就是这么变成玩具的）。
9. **发布快照不可变**：同版本号不同内容必须被拒；旧快照文件在后续发布后字节不变。
10. **手改保护可见**：generate 后 `skippedProtected`/`conflicts` 是否回给了界面，还是静默跳过了几十行？
11. **组件库有东西吗**：`generate` 之后 `componentCount` 是否 >0、`.cg__card` 是否真渲染？
    （历史坑：生成器只写 `component.*` 令牌，`DesignComponent` 一直 0 行 → "组件库 0 个组件"，读图才发现。）
    目录的 `tokenRefsJson` 每一条都必须能在组件层令牌里查到 —— 清单只许从真实生成结果反推。
12. **换肤是不是"真的换"**：判据是 `.ds-skin` 计算底色 == 后端 `semantic.surface-bg` 的 hex→rgb（逐位），
    且切主题后注入 CSS 头部注释变成 `theme=<新档>`。
    （历史坑 A：别名指向未定义变量 → 整条声明 computed-value 失效 → 预览全透明。
    历史坑 B：不等注入完成就读底色 → 拿到上一档残留，"切了没变"的假失败。）
13. **控制台零已知可避免噪音**：`<input type="color">` 被塞空串会让浏览器每帧告警；
    e2e 已把"不得出现 does not conform to the required format"写成断言。形状不合就**不渲染**该控件。
14. **变体矩阵厚度诚实吗**：格子由 `component.<code>.<variant>.<part>[-<state>]` 反推，
    无令牌支撑的组合**不许落格**（当前 `card` 只有 base/default 一格是事实，不是 bug）。
    要变厚只能补真令牌（尺寸轴 / focus / disabled），不要往矩阵里塞假行。
15. **写路径是否整批一个事务**：审计落库、组件目录+矩阵都必须是**一次提交**；
    逐条 Save 在 SQLite 上等于把撞锁窗口拉长几百倍（实测 `SQLITE_BUSY` → `/releases` 500）。
    发布入口还要按项目**串行化**（`ReleaseService.ProjectGates`），否则并发/重复投递两边都失败。
16. **声明了 capabilities 就不能留空表**：`meta.capabilities` 每有一项（assets / screens / fonts / components…），
    就必须同时具备 ①生成时的种子（`SeedBrandCatalog` / `SeedComponentCatalog`）②写入口（POST + 界面表单）
    ③e2e 的"读得到 + 写得进"两条断言。三者缺一，界面就是一个永远显示"无…"的面板 = 假能力。
    （历史坑：这三项**只有 GET**、也没人种数据，M7 才补齐；自查方式是直接看页面文案有没有"无资产/无字体登记"。）
17. **模板里的共享词汇表类必须真存在**：`web/src/design/classes.test.ts` 守 `ds-*` 这一层
    （build 与 vue-tsc 都不检查 CSS 类，漏了 `ds-btn`/`ds-btn--ghost` 只会渲染成浏览器默认控件）。
    新 section 若用到 `ds-btn` / `ds-input`，要么依赖 `styles/*.css` 里的全局定义，要么在本组件 `<style scoped>` 自带 ——
    **别指望别的 section 的局部类会泄漏过来**。
18. **落库的东西必须进交付物**：新表/新字段一旦能被登记，就要问"它在 `css` / `bundle` / `design-md` 里长什么样"。
    （历史坑：品牌三表补齐了写入口，`ExportService` 却一行都不读 —— 库里有了、交付物里没有，是同一家族的另一半假能力。）
    反面纪律同样成立：**没有文件可指的字体不许出 `@font-face`**（那是假声明），如实注释比硬凑更像交付。
19. **异步取数必须有序号守卫**：任何"`await` 完直接写共享 ref"的地方，快速切换时**后到的旧响应会覆盖新状态**。
    （历史坑：切主题后预览停在上一档，表现成 e2e "同一份代码一跑失败一跑通过"。）
    判据是用受控 promise 写一条"旧的先到 / 新的后到"反例（`web/src/state.test.ts`），**先复现再修**；
    `reset()` 也要递增序号，否则上一轮迟到响应会污染换项目后的状态。

20. **"一红一绿"当缺陷查，不许重跑一次绿了就交**：偶发失败必有机制解释（竞态 / 顺序依赖 / 静态状态），
    解释不出来就把"未定位"写进 TODO 并在汇报里标 Unknown，而不是写成"环境抖动"。
21. **落库的东西必须同时进"交付物"和"版本"**：新节数据补齐后连问两句 —— 导出投影里有它吗？快照与 diff 里有它吗？（历史坑：品牌三表先补了读写，`ExportService` 与 `ReleaseService` 都看不见它，于是"可交付/可版本化"两个卖点各自只对一半东西成立。）
    规格进哈希是硬要求：不进哈希 = 只换 logo 时同版本号被幂等放行，版本化成摆设；
    读旧 schema 的快照**不许凭空补一节**，要回"不可比"（把结构差异报成内容变更是假警报）。
22. **产物里的每个"数字/引用/url"都必须能当场核对**（M10 的三类假声明同源于此）：
    ① 清单里写的 `total` 必须与明细 `data[]` **同出一处计算**（`ExportService.EntityRows`），
       历史坑：`CountFor` 另算一套 —— `design-component` 数的是令牌条数、`design-icon/screen/font-face` 写死 0，库里明明有行；
    ② 清单里给的 `url` 必须有真路由（`GET api/design-system/{id}/{entity}.json`），历史坑：FR13 只出了清单、路由从没实现；
    ③ 产物里列出的每条令牌引用都必须能在同一工件里查到，查不到的**如实**列进 `unresolvedTokenRefs`，不许静默丢。
    自查方式：e2e 里逐实体比对 `total == data.length`、比对 `design-component == GET components 条数`；
    单测里对 registry 的 `button` 断言 `anatomy/a11yNotes/states/variants[].axes` 都在（规格"进了产物"要以字段为证，不以标题为证）。
23. **"接缝类"产物（把我们的令牌映射到别家 CSS 变量体系）有三条硬判据**：
    ① 右侧只准出现 `var(--ds-…)` 或它的 `color-mix` 派生，**零字面色值**；
    ② 每个引用都必须能在**同主题**的 `tokens.css` 里找到定义（e2e 逐条比对，不是抽查）；库里没有的档位如实列"未映射"并指名缺哪条令牌；
    ③ 混合/派生的**目标色也要跟着主题走** —— 历史坑：EP 的 `light-N` 原义是"与白混合"，直接照抄到深色主题就把悬停态洗成灰白；
    另一处同类坑：`color-mix` 的第二参数写成裸 `--ds-…`（不是颜色值）→ 整条声明在 computed-value 阶段失效，表现是"组件掉回默认色"。
    （同一族缺陷的根：写死一个值 = 造出第二份真相；引用一个不存在的名字 = 声明静默失效。）
    ④ **承诺要划到能量化的那一层**：接缝这类产物的功能证据分两层 —— "变量被解析成我们的值"（可测、可断言）与
    "最终像素被我们盖住"（宿主里只要有直接写 `background-color` 的规则，任何主题都赢不了，包括宿主自己那份）。
    断言落在变量层（实测 `--el-button-bg-color` `#F59E0B → #6d28d9`），像素层只记录不假装；
    别为了"绿"去断言一个自己都不承诺的东西，也别悄悄把断言删掉。
24. **门禁必须对"用户手改之后"仍然成立**：审计不能只查生成器算得对不对 —— 库里的值谁都能改。
    加新维度时按这三条做：① **先断言"生成产物自己必须过"**（否则新维度第一次上线就是一片假红，之后没人再看它。
    实测：我第一版命名正则把生成器自己的 `z-index.1` 判成不合形）；
    ② **severity 要匹配规范的例外条款**：WCAG 2.5.8 有内联/浏览器控制/本质性小目标例外 → 只报 warning，
    别把它升成 critical 拦发布（"宁少勿假"是本仓既有纪律，假警报一次就废掉整套门禁的信任）；
    ③ **判据要能在真实宿主里被"改坏→抓到→改回→消失"三段验证**（e2e 步 9b 就是这个形状），
    只写单测等于只证明代码跑得过，没证明界面与端点读得到。
    另外：档位顺序一律读 `ScaleGenerators` 的 `SpaceSteps/RadiusSteps/DurationSteps`，**不要在审计里另列一份档名表**。
    ④ **新判据必须"故意造反例证明它真的会响"** —— 只断言"生成产物通过"证明不了检查在跑。
    实测：`ramp-monotonic` 第一版数值解析只认 `dimension + px`，而 `duration.*` 是 Duration 类型带 `ms` →
    时长族**静默空跑**，界面门禁口径却写着检三类。补了"把 duration.macro 压到 100ms 必须报"这条用例才暴露（v2.6.1 修）。

25. **审计的"覆盖面"和"结论的新鲜度"都要能被质疑**（v2.6.2 一次抓到三个洞）：
    ① **判据对象必须从库里推导，不能是代码里写死的清单** —— `contrast` 原来只查八对硬编码路径，
    生成器自己的 `dialog/tooltip/select` 前景从未被算过，用户新增组件直接绕过门禁，而界面仍显示一片绿。
    做法：按命名约定配对（`component.<ns>.foreground[-状态]` ↔ `.background[-状态]`/`.tint`），并断言"生成产物里每个组件名都出现在 checkedPaths"。
    ② **规范原文的豁免要照抄，不要拿它拦发布** —— WCAG 1.4.3 豁免"非活动界面构件"，禁用态按 4.5 判会一次产 9 条假 critical；
    但**豁免≠跳过**：读数必须带 `rule=…-exempt` 落库，判级退回 3.0 兜底线（info/warning，永不 critical），并造一个"退化到 1.0 必须升 warning"的反例。
    ③ **每次 Run 的产出必须等于该 scope 的全部结论，且"判不成"要自己报** —— `Record` 只覆盖本轮产出的键时，某条对不再被判定后，
    上一轮的 `Passed=true` 行留在库里，而 `HasBlocking` 读的就是它 = 用旧绿灯冒充今天查过（v2.6.2 起整批写入后清旧行）。
    但**清旧行只解决一半**：对象还在、只是这轮判不成（声明为 color 却解析不出颜色）时必须落一条 `…-unresolved` warning，
    否则它从审计里静默消失，界面上既不见红也不见黄 —— 与"查过且没问题"仍然长一样（v2.6.3 补）。
    写完各加一条用例：前者断言"本轮没判成的对象不得再有旧行"，后者断言"改坏→出现 unresolved→改回→消失"。

26. **"顺序"也是一份声明，必须只有一处定义**（v2.6.4）：档位序 / 状态序 / 轴序决定了产物能不能被设计师核对，
    而且它极易长出新真相 —— 实测同一份库曾有**三处各排各的**：投影按字母序、读路径 `ListVariants` 按 `State` 字母序、前端 `ComponentGallery` 再 `.sort()`。
    自查三问：① 这个序在**哪张表**里定义（答不出 = 它是"顺手 OrderBy 出来的"）；② 生成 / 读路径 / 投影 / 界面是不是都读它；
    ③ 前端要不要"排序"？要的话**词表必须从 `GET /meta` 拿**（`stateOrder`/`sizeOrder`），不许在 TS 里另列一份（决策 010 的顺序版）。
    反例证据：e2e 断言"界面状态序 == DESIGN.md 状态序"第一次跑就红（`default、disabled、hover、active` vs `default、hover、active、disabled`）——
    **只改投影不够**，读路径与界面也得回到同一张表；表外的值退回字母序，别编造没证据的顺序。
    ④ 词表必须**覆盖库里真实存在的档位**（v2.6.5）：`radius.pill` / `radius.full` 是绝对值档、不在倍率表 `RadiusSteps` 里，
    只把倍率表当词表就会漏档 —— 现在词表是 `ScaleGenerators.RadiusOrder`（倍率档 + 绝对值档）。
    ⑤ 档名里带数字不等于数值档：`stepOf` 若用 `parseInt` 取末段，`radius.2xl` 会被读成"第 2 档"，
    把 `2xl` 插进命名档中间。判数值档只认**整段是数字**（`space.4` ✓ / `radius.2xl` ✗），并给这条单独写用例。
27. **词表要"被消费"，不能只是"被引用"**（v2.6.6，同族第 4 次收口）：把界面手抄的词表改成读 `GET /meta` 只是第一步，
    第二步是证明**后端那张表就是实现本身**，否则只是把抄本搬了个地方：
    ① 表要有生产者 —— `ColorFamilies.All` 是生成器 `RampFor` 逐族产阶遍历的那张表（表里有族而没规则 = 抛），
    用例再反向核对"产出的族集合与顺序 == 表"；
    ② 表要与产出双向核对 —— `AuditKinds.All` vs `AuditEngine` 真跑出来的 kind：
    **表里有却造不出触发场景 = 空声明**（界面上是个永远为空的筛选项），**产出了表外 = 界面筛不到**；
    ③ 反射核对"const 与 All 一一对应"（加了常量忘了进表，前两条都发现不了）；
    ④ 写触发场景前先证明前提成立：`UpsertBatch` 有"全图校验、破损整批拒绝"的守卫，
    把"坏别名"和别的补丁放同一批 = 整批被回滚，测试看着在测其实什么都没写（本轮就这么红过一次，加 `Created+Updated` 断言才现形）。
    同类收尾动作：词表覆盖到的输入位**改成下拉**（`state` 自由文本能敲出 `hove` 这种幽灵状态），逃生口是"显式选自定义"而不是回到自由输入；
    并留一道守卫（`web/src/design/vocabulary.test.ts`：同一行 ≥3 个互不相同的已知成员字面量清单即红），**守卫要用反向探针证过它会红**才算数。
28. **截图必须拍到"被断言的东西已就绪"**（同一课踩过两次）：v2.6.5 是密度表"后端说明"整列 `—`，v2.6.6 是 `05c` 拍在
    `组件库 0 个组件 / 正在读取设计系统库…` 的半加载态 —— 图看着完整，实际什么也证明不了。
    规矩：`shot()` 之前对**同一块区域**断一条数据到位的判据（`expect(行/卡片).toBeVisible()` 或 `poll(值).not.toBe('—')`），
    被断言的控件在首屏之下还要 `scrollIntoViewIfNeeded()`。补的是断言，不是"再截一张"——图不合格往往意味着证据链本身有洞。
29. **机器可读契约在界面上出现时，必须带一次真读回**（v2.6.7）：「导出交付」那十行如果只列 url，就是把后端契约抄到界面上当摆设 ——
    用户看不见"里面有没有货"。做法：每行按当前主题真打一次 `GET .../{entity}.json` 并把 `total` 显示出来，
    断言"每一行的行数必须是数字"（卡在读取中 / 报错都算不合格），换主题要整表重读。
    同族判据：**词表/清单不要在契约里抄多遍** —— `stateOrder`/`sizeOrder`/`roleOrder` 三条并列字段合成一份 `variantAxes` 清单，
    加轴只改一处；合并属**破坏性变更**，要在 README/证据里写明"外部读 `meta.stateOrder` 会拿 undefined"。
30. **"预览显示的是哪一份"必须是状态，不能靠肉眼看明暗**（v2.6.8）：状态里只存投影 CSS 文本、不存它属于哪个档，
    于是「界面选中的主题」与「画布实际用的主题」可以静默分家（截图两次运行一暗一亮，读图 QA 也无从判断）。
    判据三条：① 界面把档位显示出来（角标 + 主题条四态 `applied`/`pending`/`unloaded`/`unavailable`，
    **"未取"和"待重取"不能混成一句文案** —— e2e 就是被这个区分逼出来的）；
    ② 角标的颜色值必须是**字面值**（别名要顺到原语层，`var(--ds-color-neutral-950)` 用户解不出来）；
    ③ e2e 把 **角标值 == 后端令牌 == 画布 `background-color` 渲染值** 三方逐位钉死，截图前先等角标认账。
31. **一个只读页面不要同时打二十个请求到 SQLite**（v2.6.8）：导出页 13 份格式预览 + 10 类实体行数全并发，
    实测把宿主 SQLite 顶出 `database is locked`（500）。**只读退避重试会把它兜住 ⇒ 界面看不出来，别把"没报错"当没发生**；
    量化证据是网络日志里的 500 计数，但**必须跨轮看分布**（v2.6.8 连跑三轮 = 2 / 0 / 3，落点每轮不同）：
    单轮 0 次只是运气，把它写成"已收敛"就是拿代理信号当结论（本轮真犯过一次，已回改文档）。
    两处一起收：后端读路径去掉 N+1（逐组件查 → 一次批量，
    且要有"批量行序 == 逐组件行序"的一致性断言），界面侧批量读限流（`design/pool.ts` 的 `mapLimit`，≤3）。
32. **"声明了却没人调用"的工具函数等于假能力**（v2.6.8）：`cssVar` 的注释写着"界面各处取色不再自己算"，实际零调用点、
    且正则把 `--` 前缀拼错（一条也取不到）。自查时对每个"给界面用"的 helper 问一句：有调用点吗？有测试吗？
    没有就把注释改成实话或删掉 —— 留着它，下一个人就会以为这条路已经通了。
33. **导入/回流四问**（v2.7.0）：① 预览真的不落库吗（preview 与 import 必须共用同一份解析，不是各算一遍）？
    ② 库里已有路径的层级/主题是不是**以库为准**（文件里没有 tier，按路径猜会把语义层降级成 primitive，
    其别名"逆向指向上层"触发整批图校验拒绝——实测踩过）？③ round-trip（导出→导入→导出）逐字一致吗？
    有断言钉字节数吗？④ 每一条被拒/冲突的条目**有名字和原因吗**（不许"好的进去了、坏的没人知道"）？
    另注意：e2e 里"导入写进哪个主题档"要从**页面自己的下拉 value** 取（可见文本被 `.ds-micro` uppercase 成 `DARK`，
    靠 SQLite 大小写不敏感才碰巧相等——这种巧合不算证据）。
34. **插件前端 dist 与源码不同步 = 新失败模式**（v2.8.0）：改过插件 `web/src` 后忘了 `pnpm run build`，
    页面会加载**旧产物**——e2e 断言 `.ds-badge` 之类新 UI 时表现为"代码明明改了却找不到"。
    判据：跑 e2e 前先 `pnpm run build`（或断言产物含新指纹），并在走查页核对版本徽标；
    dist 重建后体积变化（本批 index.js 297.56kB / style.css 58.38kB）记入证据。
35. **Agent 通道必须与 REST/导出同源**（v2.8.0，M1）：工具、REST、导出调用同一服务函数；
    判据 = 同一输入下工具返回与 REST 关键字段**逐字段一致**（说明书里的颜色/变量名 == `tokens/effective`；
    e2e 断言 `semantic.surface-bg` 的 hex 逐位相等）。
36. **审查类判据用"自产语料零误报 + 每条规则反例必响"双向验证**（v2.8.0，M1）：本插件自己导出的 CSS（全主题）
    必须 0 命中；每条规则至少一个反例必触发（只测其一都会骗人）。注意 **hardcoded 默认 warning 级、
    `strict:true` 才升 error**——断言 `errors≥1` 必须传 strict，否则拿 0 当红。
37. **Agent 写能力必须有开关、默认值与降级**（v2.8.0，M1）：关写后写工具被拒且文案指向开关路径；
    配置文件损坏→**fail-closed 只读**而不是可写；PUT 立即生效、新实例保持（`AgentAccess` 现读文件不缓存）。
38. **干跑（`apply=false`）零写库**（v2.8.0，M1）：前后项目数与 `DesignToken` 行数不变；不要只看返回值。
    REST 侧注意语义相反：`DryRun=false` = 落库。
39. **工具数量与 prompt 预算是声明也是风险**（v2.8.0，M1）：对内置 agent 的工具数量用真实 `ToolRegistry` 计数
    断言（≤8，AIAgent `ToolScopePluginIds` 白名单 = `[ownPluginId, "memory-system", "design-system"]`）；
    文档写明降级预案。工具唯一真源 = `DesignToolIndex.All`（读 6 写 2），`meta.agentTools` 即其 Name 数组。
40. **网关直连契约是"封套叠封套"**（v2.8.0，M1 实测）：`universal_tool` 转发本身有 `{isError, content}`，
    design_* 工具结果再套 `{success, data}`（data 才是载荷）；**`list_tools` 例外无封套**（直接是数据）。
    另：`EffectiveToken.ColorHex` 序列化后是 **`colorHex`** 不是 `hex`（`DesignMapper.cs:24`）；
    C# 元组直接 `Data(...)` 序列化会**丢字段名**（agent-access 初版 allowWrite=undefined）——REST 返回一律用显式匿名对象。
41. **窄舞台下的"取景"必须量化，不能拿被裁的图当完整证据**（v3.0.0，M2）：展厅三栏 `240px / 1fr / 240px`
    叠加插件内容 `max-width:1240`（`DesignSystemView.vue`）→ 舞台可见宽永远小于桌面档框宽 1280，
    右侧被 `.ds-stage__viewport` 横向滚动裁掉（§FR9 的既定行为）。截图取证要拍**用户所见视口**（`.ds-stage__viewport`）
    并把「框宽 / 可见宽」写进证据；读图时对裁切区标 Unknown，**不要把"看不到"当成"没问题"**。
    （拍框元素会把相邻微调面板的像素也框进图里，是复合图，会误导读图。）
42. **插件自路由的 URL fragment 会被宿主抹掉**（v3.0.0，M2 实测根因）：宿主 `authInit.consumeTokenFromHash()`
    无条件 `replaceState(pathname+search)`（**没有 token 也照抹**）、`main.ts` 的 `router.beforeEach` 重写地址时只带 `fullPath`。
    凡新增"哈希自路由 / 深链 / 初始模式判定"，**必须**有一条 e2e 断言 `location.hash` 在导航后仍在
    （并可选加 `history.replaceState/pushState` 入参探针）——否则深链与初始模式会静默失效，
    页面看起来"正常"（落默认态），这是最难肉眼发现的一类红。
43. **交付/接入类页面不得出现令牌明文或掩码**（v3.0.0，M2）：断言 `page.content()` 既不含真实 api key、
    也不含 `tokenMasked`（掩码含真令牌首尾片段），配置片段只写 `Bearer <你的令牌>` 占位符。
44. **规范正文里不许出现数字**（v3.1.0，M3）：库里 `Body/RulesJson` 只存反引号令牌路径，数值由
    `GuidelineRenderer.Annotate` 现查。历史坑：界面把括注后的展示文本回填进输入框，用户点一次保存就把
    "24px"写进库里 —— 令牌改值后那条规范变成旧数字。守卫两条：编辑器绑 `*Raw`（e2e 断言输入框内容 == 库里原文
    且 `/\d+(px|rem|ms)\b/` 不命中），chip 的值必须逐字等于 `tokens/effective`（前端不得再查一遍当第二份真相）。
45. **规范取值主题 ≠ 导出请求主题**（v3.1.0，M3 实测）：`semantic.*` / `shadow.*` 只在主题层，用共享层视图
    （`theme=null`）或密度主题当视图会把整片引用判成"令牌已不存在"（假断链）。口径＝**参考主题 = 项目默认色彩主题**
    （`ExportService.GuidelineView`），出参带 `valueTheme`，界面必须写明它是哪个主题。回归用例：
    `GuidelineExportTests.AC16_compact主题不抛_且不出现假断链`。
    另注：REST 夹具装配 `ExportService` 时**必须**挂 `Guidelines` 仓储，忘了会得到"每条规范都断链"的假红/假绿。
46. **"生成了但一条没建"必须回计数**（v3.1.0，M3）：`Generate` 回 `created/skipped/skippedProtected/overwritten/total`，
    生成链路播种 `SeedGuidelines` 回的是**库里现存未归档条数**而不是"本次新增数"（第二次生成新增=0，
    拿它当响应会让界面上的规范凭空变 0）。判重必须连 archived 一起看，否则"归档后重新生成"撞唯一索引。
    本插件**没有任何 DELETE**：归档=软删，恢复=PUT 带 `status=adopted`（`GuidelineSchemaTests` 反射断言控制器零 `HttpDeleteAttribute`）。
47. **默认产物兼容靠录制器，不靠嘴**（v3.1.0，M3-A）：新增风格轴/预设时，默认档各层令牌文本的 SHA-256 必须逐条不变
    （`StyleAxisGoldenTests`，50 条基线）；要改基线必须显式 `DS_RECORD_GOLDEN=1` 重录并在 05-evidence 交代为什么允许变。
    同一条规矩适用于规范清单：`DS_DUMP_GUIDELINES=1` 才产出交用户审阅的全文清单（不是临时脚本，是测试资产）。
48. **数值轴的"边界值"必须在投影层验，生成层绿不代表交付绿**（v3.1.0，M3 缺陷 #8）：`shadowStrength=0` 在生成结果里
    alpha 确实是 0（单测绿），但 `ExportService.ShadowCss` 写的是 `alpha > 0 && alpha < 1 ? color-mix(...) : color`
    → 0 落进 else，**交付 CSS 里变成不透明实心投影**（"把阴影调到 0 反而得到最重的黑投影"）。由视觉矩阵 V3 读卡片
    `box-shadow` 计算值抓到（`rgb(15, 23, 42) 0px 2px 9.1px -1px`）。常驻守卫：
    `PreviewCssTests.AC3_阴影强度0_投影必须是transparent_而不是实心色`（同时核默认与拉满两端）。
    推广做法：任何"乘数/开关型"数值参数，判据要落在**最终产物文本**上（`export?format=css` / `generate/preview-css`），
    只比 `TokenPatch.Value` 会漏掉序列化分支。

49. **轴/新参数的落库判据要断"整族齐全"，不能只断一条变量**（v3.1.0，M3 批 C 一次不可复现红逼出来的）：e2e 里出现过
    quick-create 新建项目的导出 CSS **整条 `--ds-shadow-elevation-3` 不见**（同判据单跑绿、复跑也绿，宿主日志已被下一次 globalSetup 覆盖 → 机制 Unknown）。
    只断"某一条变量的值随轴变"挡不住"这一档根本没写出来"。常驻判据：`QuickCreateServiceTests.Create_带轴项目_导出CSS整族齐全且形状随轴变`
    ——同库先后建两个项目（默认 / 带轴），断言**五档 elevation 一档都不许少** + 带轴那档逐字对公式（`0px 3px 7.9px 0px color-mix(in oklab, … 22%, transparent)`、
    且只有一个 `color-mix` = 单层）。e2e 侧配套加"现场诊断行"（导出字符数 + 该族变量清单 + 目标整行），下次再红会直接点名缺哪一档，
    而不是留一句"看起来是环境问题"。
50. **同一台机器有并行会话时，长批 e2e 必须钉端口**（v3.1.0，M3 批 C 环境红）：见 §四"长批 e2e 一律显式钉端口"。

51. **"点一遍界面"必须是常驻用例，不能是一次性走查**（v3.1.0，M3 G4）：§G8 的 DOM 契约此前只有我读代码说"逐项都在"，没人跑过。
    现在由 `design-system-guidelines.spec.ts` 的 **G4** 承担：列表条数 == REST、来源徽标取值 ∈ `generated|manual`、
    三个详情控件 `toBeEditable()`、正文 `tagName === 'TEXTAREA'`（这条同时钉住 Forbidden"不许 v-html 渲染规范正文"）、
    分类/级别下拉候选 == `meta` 词表、规则行三件套、「添加规则」真加一行、chip 文本形状 `path = 值` 且值 == 后端 `tokenValues`。
    **走查还声明"零写入"**——这类"断言某件事没发生"的判据最容易空转，所以它自带反证：同一个收集器必须先看到 N 条 GET
    （`reads.length > 0`）才允许宣布 `writes == []`。凡是"没有发生 X"型断言，都要配一条"我确实在听"的证据。
52. **写成功后立刻读，可能读到旧视图（本插件真实存在，e2e 判据必须按"轮询 + 记读数"写）**（v3.1.0，M3 批 C）：
    两条独立证据——① `quick-create` 回 200 后第一次 `export?format=css` 读到 **7628 字符 / `--ds-shadow-*` 整族为 0**，
    **同一瞬间** `tokens/effective` 已有 5 条 shadow，~400ms 后第二次读即五档齐全；② 另一跑里宿主日志已写
    `[DesignSystem] 项目已创建 ds-0cdb81`，而紧接着的 `GET projects` **查不到这条新项目**（用例红在"应新增一个项目 0≠1"）。
    现场：宿主 `XCode.config` `DataCacheExpire 0 / EntityCacheExpire 10 / SingleCacheExpire 10`，写入侧全同步 `entity.Save()`。
    所以：**"保存后清单必须多一条""导出必须整族齐全"这类判据一律写成 `expect.poll` + 每次尝试把长度/条数 mark 进证据**，
    并且把**写请求的 200 响应**也抓下来——这样"写失败""写成功但读侧滞后""产物天生残缺"三种解释一次跑就能分辨。
    根因修法（关缓存 / 直查 / 导出前失效）属跨切面读路径 → 已记 TODO P1 交用户拍板，别在收口时顺手改。
53. **四层门禁绿 ≠ 插件已交付：收口必须交"五步对账表"，点名哪步被谁 gate 住**（v3.1.0，M3 收尾）：
    本文第一节管"测得对不对"，`plugin-development` §四 五步管"有没有真交付到用户手里"，两套口径不可互相替代——
    历史上出现过"门禁全绿就直接回复完成"，而 AGENTS §0 明文记着 2026-09-28 曾因把铁律读成"不许走查"而漏掉第⑤步。
    落地写法（M3 已照此做，可抄）：在 `05-evidence.md` 单列一节表格，五步逐行写**本批做到哪 + 状态/被谁 gate**：
    ① 门禁（报数==发现数）② 插件层 e2e（隔离实例）③ 发布（**tag / push / `-UpdateDir` 三样一件都没做 = 待用户授权**，
    不许用"本地 zip 已验"冒充"已发布"）④ 走查（**做成常驻用例**而不是手点，含零写入反证；真人外行走查没做就写没做）
    ⑤ 运行实例只读复验（**前提=用户已把宿主更到新版**；没更新就写"未做+前提不成立"，不许推断成通过）。
    同一条适用于"不做事决策"：本轮否掉的方案一律进 `docs/07-decisions/not-taken-decisions.md` 编号登记（M3 = 024–027），
    只在聊天里说"没做"＝下次被当成漏做。

54. **同一个测试项目不许并发两轮 `dotnet test`，跑测期间也不要改测试源码**（v3.1.0，M3 收口 2026-10-03 17:42 实测）：
    上一轮还在跑时再起一轮 → 编译期 `MSB3026` 重试十几次后 `MSB3027/MSB3021 无法将 …ForgeSelf.Api.Tests.dll 复制`，
    **退出码 1 但一条测试都没跑**——把它误读成"我的新用例红了"就是假红。三条纪律：
    ① 后端门禁**串行**（要并行就并行 e2e，它跑的是已发布产物不是本仓库构建输出）；
    ② 全量跑期间**冻结** `ForgeSelf.Api.Tests/**` 的写入（我这次就是在 8 分钟跑测中途加了新用例，只能再跑一轮，白等）；
    ③ 判成败只看落盘日志里的 `通过数/失败数` 与 `已通过 / 失败` 行，不看退出码（本条已在 #exit-code 教训里说过，这次是它的编译期变体）。
55. **"两个视图互比"钉不住"值有没有被抄成第二份"**（v3.1.0，M3 收口补 V10）：`导出括注 == tokens/effective` 这类同源用例，
    比的是**同一时刻**的两个视图；真把数字固化进正文，两边照样相等 ⇒ 永远不红。
    凡承诺"数值是现查的"，必须有一条**动态**判据：改值 → 重新渲染 → 新值出现 + 旧值消失 + 存储行指纹逐字段不变。
    见 `GuidelineExportTests.V10_改令牌值_规范渲染的数跟着变_而库里规范文本一字不动`（含"不重渲染就必须红"的反例）。

56. **循环/参数化型用例必须断言"循环真的跑了几次"**（v3.1.0，M3 收口补 V6）：`foreach (var x in FreshRequests())` 一旦数据源退化
    （少一条、被误改成空枚举），断言全部**在零次迭代里跑过** ⇒ 用例照样绿，这是最隐蔽的假绿。
    写法：循环里 `ran++`，末尾 `ran.Should().Be(3, "…循环体一旦空转，本用例就退化成只在一条输入上判")`。
    与 #24（判据要能红）同族但不同：**#24 防"判据太松"，#56 防"根本没跑到"**；两者都要做，缺一不可。
57. **同一个量词不要既指"条目"又指"轮次"**（v3.1.0，M3 收口）：05 写"四轮红账"、TODO 写"六轮红账"，其实一个是归因条目、一个是红跑轮数。
    统一成「**四条归因，覆盖六轮红跑**」。另注意：e2e 证据文件按**用例名**落盘（`style-S1-passed.log`），复跑即覆盖 ⇒
    **FAILED 日志份数不能当轮数依据**，只有归因条目是耐久的。
58. **对预注册验收清单要按「每一格的全文分段」清点，不能按编号打勾**（v3.1.0，M3 收口自审 V10/V13）：一格编号里常含**多段**判据 —— 06 的 V10 一格写了三段（①抽 3 个令牌 × brief/design-md/bundle 三种交付物；②对生成器 14 条 × 3 kind 自跑数字/十六进制正则；③手写规范不受守卫误拒且原样渲染），我只落了①的"一个令牌 + 一种交付物"就把它记成 Verified。**证据宽度不足比缺证据更危险**：缺证据会被追问，宽度不足会让人以为已经测过。两种性质要分开写清 —— ②是**登记缺失**（`AC13_数字守卫_全部生成文本零命中` 早就存在且比要求更宽，只是没映射到 V10 编号），③是**能力缺失**（真没做）。同族的字面陷阱：预注册把形状写死（V13「`ReleaseBoard.vue` 的 diff **仅一处**新增」）而实测为 `+4/−1`、2 个 hunk ⇒ **按实测形状登记**，既不改判据迁就实现，也不把"顺带必然同步的那一行"说成"不算一处"。

**配套机械核对（改 05/07 的表格后必跑，别靠眼看）**：05 的表格单元里满是 `|`（路径、正则、用例过滤器），Edit 的锚点一圈大就会掉格或漏转义 —— 本会话实际翻车三次（掉 `## Static Analysis` 标题、AC3 行掉「来源等级」格、`Value|AliasPath|ValueJson` 未转义变成多列）。核对办法是把转义过的 `\|` 先吃掉再数分隔符，行内代码里的裸 `|` 就是真缺陷：

```python
import io
PIPE = chr(124)          # 竖线一律不写进源码字面量：它既是表格分隔符又要被转义，写死必打架
ESC = chr(92) + PIPE     # 已转义的竖线（\|）
for p in ['05-evidence.md', '03-plan.md', '07-final-report.md']:
    head = None
    for i, raw in enumerate(io.open(p, encoding='utf-8').read().splitlines(), 1):
        s = raw.strip()
        if not s.startswith(PIPE):
            head = None; continue
        s = s.replace(ESC, '')                       # 先把 \| 吃掉，剩下的裸 | 才是真分隔符
        if set(s) <= set(PIPE + ' -:|'):             # 表头下的分隔行
            continue
        cols = len(s.split(PIPE)) - 2
        if head is None:
            head = cols; continue
        if cols != head:
            print(p, i, cols, 'expected', head, s[:60])
            head = None                              # 撞了就不假设后面还是同一张表
```
**实测噪声白名单（19:33 重测，别再逐条追）**：① AC 矩阵的「输出摘要」格为空时行尾直接省略 ⇒ 同一张表 5 列与 6 列混用（实测表头 6 格；AC1–AC9 与 AC19 是 6 格，AC10–AC18、AC20–AC26 是 5 格、证据写在「来源等级」格里），GFM 按格序左填，**不会错位**，属已知形态；②**行尾多写了一根竖线**（本批实测 `04-task.md:11` 结尾是 `…是本格的核心待认项）||` ⇒ 比表头多出一个空格子，已当场修掉——这类是真报出，别归到噪声里）。③ 反引号里也可能有 `**`（`05:89/95/96`、`03-plan:333`、`README:327` 的"粗体不配对"报警全部来自代码段内）。⇒ **校验脚本必须先剥 `` `...` `` 代码段，再数竖线、再查粗体奇偶**，否则整片假阳。**两条同类假阳（19:35 实测，都是脚本的错不是文档的错）**：③ 粗体奇偶**不能按行判**——一个 `**…**` 跨两行写（技能正文的软换行段落）就会各报一次；按「逻辑段落」（空行切块）数才准；④ 行内含 glob/正则里的 `**`（如 README 的 `Plugins/*/web/src/**/*.test.ts`）时，该行在 ``` 围栏代码块里，**校验必须整块跳过围栏**。本批终检按此口径：pilot 05/06/07/03/04 + README + ROADMAP + TODO ⇒ `EXTRA-CELL=0`、超表头的行 0（17 行是「尾列留空」的已知形态），三处 ODD-BOLD 全属上述两类假阳。
**除此以外的报出都要修**（本批据此修掉 `Value|AliasPath|ValueJson`、`admin-calm|dark`、`shared|component.*.radius` 等未转义竖线与一行缺格；真缺陷长这样：某一格整个丢了 ⇒ 该行比表头少**中间**一格、或结尾 `| Verified |` 被锚点过宽的 Edit 吃掉）。

59. **「0 警告」要问三个问题：是不是全量重建、警告归谁、判据能不能红**（v3.1.0，M3 收口重测 V3 时抓到我自己写错的账）：
    - **是不是全量重建**：`dotnet build Plugins/DesignSystem/DesignSystem.csproj` 在源码未变时是 **no-op**，直接打印 `0 个警告 0 个错误` —— 05 里的「0 警告」就是这么来的。判「新增代码 0 warning」**必须 `--no-incremental`**，实测差值是 **0 → 371**。
    - **警告归谁**：371 里 **368 条落在 `Data/Entities/*.cs`**（xcode 生成物的 CS8618/8601/8603，宿主既有形态，改它=改生成物，Forbidden），手写码只剩 1 条。归因命令：`grep warning 日志 | cut -d'(' -f1 | sed 's/.*DesignSystem.//' | sort | uniq -c | sort -rn`。**拿总数当判据会把 V3 误判成红；拿 no-op 的 0 当判据会把 V3 误判成绿**，两头都错。
    - **判据能不能红**：本批把 `DesignReviewService.SeverityOf(String? level)` 收成 `String level`（CS8602 消失，372→371）。这不是崩溃——`GuidelineRepository.ReadRules` 已把缺失级别规一成 `SHOULD`。所以配套的常驻用例 `AC18_脏规则行_级别为null_checklist不抛且回落SHOULD` 判的是**这条不变量**（断 `ReadRules` 规一 + checklist 出条目 + 严重度==warning）：谁把规一去掉，它就红（并把 NRE 当场暴露）。**收紧签名这类"注释级修复"没有 red/green 可言，别硬凑一个跑不过的测试来假装 TDD**，要凑的是"不变量掉了会红"的那种。

60. **「这一层不必重跑」的依据要按【那层实际加载了什么产物】算，不按【我改了哪类文件】算**（v3.1.0，M3 收口我自己写错又更正）：18:52 我写下「本轮改动只在 `Plugins/DesignSystem/Services/*.cs`，前端源码零改动 ⇒ e2e 不必重跑」——**推理有洞**：`Services/*.cs` 编进 `DesignSystem.dll`，而插件层 e2e 跑的正是**真实宿主加载这个 DLL** 的链路（`design_review mode=checklist` 恰好经过当天改动的 `SeverityOf`）。前端零改动只能免除**门禁②（web check/test/build）**，免除不了**③（插件层 e2e）**。正确写法：先问「这层进程里加载了哪些产物」，再问「这些产物的源码有没有净改动」——`find -newermt` 只证前者的一半。已把 e2e 全目录重跑排进终数轮。
61. **夹具把 `Meta.Cache.Expire` 设成 0，等于把一类生产缺陷从测试面上抹掉**（v3.1.0，M3 收口读生成物时发现）：本插件每个测试夹具构造时都写 `DesignX.Meta.Cache.Expire = 0`（为了隔离跑序），而生产侧 `XCode.config` 是 `EntityCacheExpire 10`。于是「写后立读读到旧视图」这类缺陷（G15）在测试里**结构上不可见**——全绿不能证明它不存在。更要紧的是生成物里**令牌、项目、规范（`DesignGuideline.cs` 五条 Find/FindAll，阈值被 xcode 内联成字面量 1000，不引用 Biz 里的 `MaxCacheCount`）都走 `Meta.Cache`**，所以覆盖面不止令牌与导出。**做法要求**：凡主张「读侧没问题」，必须三选一并写明——① 测试里临时把 Expire 调回生产值做前后对照（这才叫能红）；② 判据挂在**不吃缓存的权威源**上（`generate/preview-css` 零写库、当场生成）；③ 标注 **代码级 Verified / 产品级 Inferred**。不许沉默地拿「夹具绿」当「生产没问题」。

62. **`expect(locator).toBeVisible()` 轮询的是 DOM，不是服务端**（v3.1.0，M3 全目录 e2e 首跑 G1 红出来的机制）：本插件 `Projects.vue` 的挂载读取写成 `if (!projects.value.length) void loadProjects()` ⇒ **清单非空就永不重读**。于是"先用 REST 建资源、再看界面有没有它"这类用例在**全目录串行**下必红（前面别的 spec 已经往同一个库建过项目），单独跑那个 spec 却 4/4 绿——**"单跑绿 / 全跑红"就是顺序依赖型 UI 缓存的信号**。三步定位法（本批实测有效）：① 读失败快照 `error-context.md` 看界面当时到底有几条（"2 个且不含新行"直接排除了空表和库被重建）；② 读隔离实例 `backend.log` 的时间戳证明写侧成功（`[DesignSystem] 项目已创建 <code>`）；③ 回读该 section 的 `onMounted` 有没有真去取数。**修法是走用户的真实出口**（点表头「重新读取」后再断言），并把"刷新第几次才出现"记成日志读数（**但别把它当定量指标**：`expect.poll` 的首次迭代会在请求往返内立刻返回 false，`attempts>1` 混着"正常网络延迟"与"界面根本不重读"两种解释——缺陷的实据是 `onMounted` 那句条件 + 失败快照里的条数，读数只作线索；要真量化得"点一次后等请求落地再判定"）。把超时从 20s 拉到 60s 或改成 `page.reload()` 都是在掩盖。另记 README **G18** 与 TODO P2：产品侧要么挂载即读，要么非空也静默刷新。

63. **闸门2 之前做一次「引用解析」：每个被引用的证据名必须能在磁盘上落到一个真实路径**（v3.1.0，M3 收口自查抓出三处）：验收方不会照着形容词复跑，他照着**路径**去开文件。做法是脚本化，不是肉眼——从 05/07/README/TODO 抽出所有 `` `*.log` `` 与所有相对路径串，逐条 `os.path.exists` / 在源码里 `in` 检查，再**逐处判定语境**：① 裸文件名（`style-V3-passed.log`）——它的真身若不在该句已经点名的目录里，就补成全路径；本批实测两处 `style-V*-passed.log` 的实体在 `ForgeSelf.Web/screenshots/e2e/design-system/m2/`（e2e 的 `[evidence]` 落点），不在 `.temp/ds-m1/logs/`；② **指向命令的路径也要真跑得通**——README 让人 `node ForgeSelf.Web/scripts/get-forge-token.cjs`，实测 `find` 只有仓库根 `scripts/get-forge-token.cjs`（AGENTS §5.3 也是这么写的），照着敲的人第一步就撞墙；③ 引用了**不入库目录**里的唯一证据（`screenshots/` 命中 `.gitignore:33`、`.temp/`）时，必须在工件里写一句耐久性交代 + 可重跑入口，别让人以为提交后文件还在。**假阳照常存在**：正则会把 `guideline-G1-FAILED.log` 这类带连字符的名字截断，也会把 TODO 里「**计划产出**的路径」（`docs/01-architecture/dsh-alignment-tasks-b2-b8.md`，条目本身是 🔄 未做）算成缺失——这两类不是错，判定规则同下一条 #64：它到底在宣称"已存在"还是"将要产出"。

64. **终数每变一次，要做一次「旧值全文扫」并逐处判定语境**（v3.1.0，M3 收口连抓四轮同一类失实）：新增一条用例/重跑一轮之后，旧值不会消失——它以「上一轮的终态口吻」留在别处。本批实测抓到的四处都是同一个形状：`05 AC23` 挂着旧包号 `2610031557`（15:57）、`07 §12②` 写着 e2e「终态 16:41 批 C 复跑 11」（实际权威轮 19:21）、`04-task:68` 同一句、`README` 验证块「全目录三批串行 30 条 0 红」（"三批"是 05:37→06:06 那批的跑法，终态是单次串行整目录）。**做法**：① 旧值 + 描述性词一起 `grep -rn`（`518|517|514|513|509`、旧包号、`三批`、`终态`、`不必重跑`、`已跑`），范围含 pilot 八件 + README + ROADMAP + TODO + 两个技能 + `docs/02-features/036`；② 每一处命中只问一句话——它是**历史语境**（有"上一轮/⑦/降为历史/该轮"之类限定词）还是**终态语境**（在宣称"权威/终态/结果"）；后者必须改，前者保留并确认限定词真的在旁边；③ 改完立刻用同一串再扫一遍，命中数只降不升。**配套**：`036` 这类"永不漂"的用户文档**不写会漂的数字**（测试数、包号、分钟数），只写版本与契约——本批实测 `grep` 036 里没有 519/包号，这是它对的原因，不是它漏了。**补一条同族（22:11 实测）**：**enumerate 型描述一样会漂**——`036` 的 `design_guide` 行写着「三条工作流」，而 M3 给它加了**第四条**（`guideline`），这条漂移**不是靠扫数字发现的，是靠 e2e G7 打印出的网关读数 `workflows=consume/create/maintain/guideline` 撞出来的**。⇒ 全文扫的关键词里除了旧数字，必须一起扫「三/四/两 + 条|类|个|档」这类量词短语（以及"两条路径/三种交付物/四条判据"这种结构说法），改结构比改数字更容易留下过期话术；能挂上常驻判据（把枚举打印出来）比人工扫更可靠。

65. **「AC 矩阵逐行 Verified」不等于「02-spec 全文覆盖」——每一节都要逐条回读：无编号的四节要反向审计，**有编号的 FR 段也要做需求→落点映射**（v3.1.0，M3 闸门2 前自查抓出四条漏测 + 22:40 补第三遍）：AC1–AC26 是**我自己写的**判据，全绿只证明"写了编号的做到了"。02-spec 的 **Business Rules / Boundary Conditions / Error Handling / Non-functional Requirements** 四节**没有 AC 号**，于是可以整节没人测而矩阵照样绿。本批实测漏网四条，全部当场补了落点：① **NFR 性能**——规格自己写着「实测记 Evidence」（非默认轴 ≤ 默认 1.5 倍、`GuidelineGenerator` < 50ms），此前**一次都没测**，现由 `StyleAxisPerformanceTests` 9 条常驻（warm-up + 交替取样取中位数，比值才敢用 1.5× 这种紧阈值；实测 0.86–1.15×、生成器 p95 0.010ms）；② Boundary「`font.display` 存在才登记 `role=display`」——令牌侧有测（AC3/AC4），**品牌库那一格没人看**，现 `StyleAxisAuditTests.Boundary_editorial才登记display字族_默认档不造假登记`；③ Boundary「存量项目无规范 → 界面空态引导」——只有 `Guidelines.vue:347` 的实现，现 e2e **G5**（空态出现 → 生成 → 空态必须消失，条件渲染的反证）；④ Boundary「窄屏第 15 区纵向堆叠」——零证据，现 e2e **G6**（computed 轨道数 == 1 + 详情顶 ≥ 列表底 + 本区零横向溢出，不看肉眼）。**还有一条测不出来只能登记**：Error Handling 写着 `SQLITE BUSY` 读重试/写绝不重试，全仓 `grep BUSY` 只有两处注释 + 发布串行化，**规格与实现名实不符** → README G19/TODO P2。**做法**：闸门2 前把那四节逐条抄成清单，每条只问「05 里有没有一个真打开的落点（用例名 / e2e 名 / 日志 / 代码行）」；没有就补，补不动就写进 Unresolved 并给 README 编号——**不许用"AC 全绿"代替这一遍**。**三条配套口径（20:45 补两条、22:40 补第三条）**：① **列进矩阵的用例名必须逐个回源码 `grep`**（本批 32 个名字逐个核过，缺失 0）——照记忆写名字就是新的假引用来源；② 审计表要**全量清点**（四节共 29 条：BR 9 + B 7 + E 8 + N 5），只列"本轮有动作的格子"是**抽样冒充清点**，验收方会读成"审计=那几行"；同时分清三种不同状态并分开写：**已有落点**、**用例存在但 05 没引用**（本批抓到 `AC13_未知kind与industry有回落不抛`——"存在"和"被指到"是两件事）、**结构上没有可测面**（如 NFR 的"不接受服务器路径输入"，规范入参根本没有路径字段 ⇒ 写"不成立"，不许写"已测"）；③ **有编号的小节同样要单独回读**（22:40 实测）：FR 段带 FRn 编号，于是两遍审计都默认"AC 全绿即覆盖它"，但脚本核对下来 **17 条 FR 里 12 条在 03/04/05 连编号都没出现**（FR2/3/4/7/8/9/10/11/12/14/15/17）——不是没实现没测，是**需求→落点的映射没有留痕**，验收方没法照 FR 复验。做法：把每条 FR 钉到一个**回源码 grep 过的真实落点**（类名/方法名/文件名/e2e 编号），做成表；结论要分清"缺能力"与"缺留痕"（本批结论是后者：17 条全有落点）。**AC 是判据清单、FR 是需求清单，两者不是一对一，任何一遍都不能拿另一遍代替。**

66. **凡"把真源复制进工件"的耐久副本，闸门2 前必须做一次逐行对账，并写明比对口径**（v3.1.0，M3 收口第三条）：AC26 的规范全文、黄金基线这类东西**本身就是第二份存在**（为了让验收方能离线读、为了防 `.temp` 不入库），所以它们会漂。做法：① 重跑**产出器**（`DS_DUMP_GUIDELINES=1 dotnet test --filter ~清单产出器`），别手改副本；② 与工件里嵌入的那份**逐行比**（本批实测 403 行 vs 403 行、剥掉标题降级后差异 0）；③ 把"怎么比"写进证据，否则复验者会撞同一个假报——嵌入副本为挂在小节下**整体降一级标题**（`##`→`###`），直接 diff 假报 **389 处差异**，必须先剥行首 `#+` 再比；④ **优先把副本类判据升级成"会自动响的守卫"**：黄金基线那份就是正例（`StyleAxisGoldenTests` 每次跑都比对，不需要人工对账），而规范全文这份目前只有人工对账 ⇒ 若以后要求"文案不许漂"，应加一条用例断"生成器产出 == 工件嵌入段"（或干脆只留路径不留正文，代价是验收方要点开 `.temp`）。**判据**：任何一份"我把它抄进文档了所以算耐久"的说法，都要能回答"上一次和真源逐行对过账是什么时候"——答不出就是没对过。

67. **判"包内产物 == 当前源码"不许用跨构建哈希；三步是 确定性自证 → `find -newer` 新鲜度 → UTF-16LE 实现串探针**（v3.1.0，M3 收口把两句推理变成测量时抓到自己的两个误测）：本批实测——同源码两次 `dotnet build -c Release --no-incremental` **逐字节相同**（构建是确定性的），而包内 `DesignSystem.dll` 与本地重建差 **384 字节 / 0.0369%**（57 段，从 PE 头 `0x88` 起）⇒ 差异来自 `release-local.ps1` 传的构建属性（版本串 / SourceRevisionId），**不是源码不同**。所以"哈希不一样"既不能当"包是旧的"的证据，"哈希一样"也不能当"包是新的"的证据；能用的组合是：① `find <src> -type f -newer <artifact>` 必须 0（新鲜度）；② 实现串 FOUND（内容性）；③ 前端产物可以走真哈希——`zip 内 index.js/style.css` 与盘上 `web/dist/` 本批 **SHA 逐字节 MATCH**（同一构建配置）。**两个我踩过的误测**：(a) 用 ASCII `grep`/`in` 搜 .NET 元数据串会假报 absent（**元数据是 UTF-16LE**，必须用 `scripts/probe-dll-string.cjs`——它的注释就写着这条，我这次是工具在旁边没用）；(b) `--expect-absent` 的反向探针**只对"全装配唯一的名字"有证明力**：我拿 `MaxCacheCount` 测"死字段已删"，结果 12 个同族实体的 Biz 文件都有这个真字段，FOUND 是必然的，而真正的证据是 `grep -nE "Int32 MaxCacheCount" DesignGuideline.Biz.cs` ⇒ 0（声明已删，只剩注释）。

68. **跨插件的「工具注册链」必须有一条端到端断言，两侧各自的单测加起来不算覆盖**（v3.1.0，M3 闸门2 前 X1 审计）：这条链是三跳——插件把工具塞进 `ToolExtensions`（`DesignSystemPlugin.cs:86-93`）→ 宿主 `ExtensionPointManager.cs:174/238` 收进 `IToolRegistry` → MCP 网关 `universal_tool` 按名转发（`UniversalToolForwarder.cs:98-121`，结果原样透传）。所以**两侧测试可以各自都"以为测过了"而缝隙在中间**：DesignSystem 侧的 `design_*` 测试全是 `new DesignGuideTool(kit)` 直构对象（`DesignAgentToolTests.cs:21-28`），一次没经过注册表；McpCenter 侧 `UniversalToolForwarderTests` 用的是 `Mock<IToolRegistry>`，证明的是"按名分发"这件通用行为。于是"插件把 8 件交给宿主"与"网关真能调到 design_guide"两跳**只有日志证据**（隔离实例 `backend.log` 的「设计系统插件已注册 8 个工具函数」）。**做法**：插件层 e2e 走一遍真实链（常驻用例 **G7**）——`GET /api/mcp-center/config` 取**被测实例自己的** `listenUrl`（端口真源在 `playwright.config.ts:39`，按 worktree hash 钉 `FORGESELF_MCP_GATEWAY_PORT`，**禁止硬编码**）→ `tools/list` 必须只有 `universal_tool` → `list_tools {keyword:"design"}` 按返回项的 `pluginId` 归因，名单**必须等于** `meta.agentTools` → 经网关调 `design_guide` 核对 `version==meta.modelVersion`、`tools` 集合、`workflows` 含本里程碑那条工作流、`discovery` 仍指向 `universal_tool` → **两条反向腿常驻**（`design_nope_*` 必 `isError` 且含 `unknown tool`；把 `design_guide` 当对外工具名直接 `tools/call` 必被 JSON-RPC 层拒），否则"网关对什么都回一大坨"也能让前面的相等断言全绿。**另记一条口径给 #65**：反向审计的范围不止 spec 的四节——**产物文本自己对外的承诺也是判据**（`discovery` 这类字段是印在交付物里的使用说明，写了就要能跑）。本批实测：期望源改错（临时塞一件不存在的工具名）必红（`e2e-m3-g7-probe.log`），首跑与还原后各绿一次（`e2e-m3-g7-gateway.log`）。

69. **工件里的"结构事实"也要实测，而且同串不能全文替换**（v3.1.0，M3 21:45 收口结构自查抓出两条）：闸门2 前除了引用解析（#63）与旧值全文扫（#64），还要机械数一遍**表格结构**——本批实测两条：① 探针台账某行因为单元格里写了解释性的 `|Δ条数| ≤ 2`（两个**裸竖线**）而比表头多出两格，渲染时那一行会错位；正确写法是 `\|Δ条数\| ≤ 2`（数法：把 `\|` 先替换成占位符再数 `|`，别直接 `count('|')`，也别用正则 `(?<!\\)\|` 走 bash heredoc——反斜杠会被吞）。② AC→证据矩阵表头声明 6 列、逐行数下来**只有 AC1–AC9 + AC19 是 6 格，其余 16 行是 5 格**（关键原文被就地写进了「结果」格）——这一形态 **19:33 已登记在本节末尾的"实测噪声白名单"第①条**（GFM 按格序左填，不会错位），不是本轮新发现；本轮补的是**给验收方的一句话**：矩阵上方写明"第 6 格故意为空、空≠没做摘要"，否则读表的人会误判。**不许为了对齐把同一句话抄两遍**（抄两遍就是第二份真相）。**踩过的坑**：修 ① 时我用同一串做了全文替换，结果把**正文段落里那处**（不需要转义）也改成了 `\|` —— 立刻回退。⇒ 同串在表格内/正文里的正确写法不同，**必须按位置逐处判定**，禁止一把梭。

70. **未复现的缺陷不得写成"已证根因"，注释不是行为证据**（v3.1.0，M3 G15 整条归因被用户当场推翻，2026-10-04）：我曾把"写成功后立刻读到残缺交付产物"归因成「读路径走了 XCode 实体缓存」，"依据"是 `TokenRepository` 类注释里那句「缓存是 AsyncLocal 每执行上下文一份…会读出幽灵行」。实测结果是这条归因**不成立**：`Find` / `FindAll` / `FindCount` **不读实体缓存**，走缓存的只有显式 `Meta.Cache.*` 与生成器给每个实体另造的 `FindByXxx` / `FindAllByXxx` 助手（函数体首行 `if (Meta.Session.Count < 1000) return Meta.Cache...`）；本插件对这两类的**调用数为 0**，`Meta.Cache` 在插件里只出现在两行注释里。⇒ 教训三条：① **引用注释当证据时必须标"代码级确认、产品级未实测"**，注释表达的是作者意图不是运行时行为；② 给缺陷配修法前先做一次**根因可复现探针**——把怀疑对象摆到生产值（我把 `Meta.Cache.Expire` 从夹具的 0 改成宿主的 10）看症状是否随它出现/消失，不随它变就不是它的锅；③ **三个复现尝试全部读数完整时，唯一诚实的状态是"未定案/待复现"**，不能因为"已经选了修法"就把它写成"已修"。（连带产物：全插件 47 处实体查询包进 `.Biz.cs` 的高级查询是用户指定的**读路径单一出口改造**，与本缺陷无关，文档里必须这样写，见 #71 的守卫。）

71. **静态扫描类守卫：锚点用仓库根独有的文件，"0 违规"必须自带阳性对照**（v3.1.0，2026-10-04 新常驻用例 `BizDirectQueryGuardTests` 落地时踩到的两条，第一条是它自己抓出来的）：① 找仓库根时 `Directory.Exists(<repo>/Plugins/DesignSystem)` **会被测试输出目录截胡**——`ForgeSelf.Api.Tests/bin/Debug/net10.0-windows/Plugins/DesignSystem/` 确实存在（里面只放 `DesignSystem.dll`），于是从 `AppContext.BaseDirectory` 上溯时先命中它、扫到 **0 个源文件**，"零违规"变成**永久假绿**。正确锚点＝只有仓库根才有的**文件**（`Plugins/DesignSystem/DesignSystem.csproj`）。② 这类"应当为空"的断言必须配两条阳性对照并常驻打印读数：`文件数 > 0` 与「被允许的调用形状在这批文件里出现 N 次」（本用例实测读数：`扫描 57 个生产码文件；Biz 高级查询调用 47 处；裸实体查询违规 0 处`）；没有这两条，今天那次空扫描就会一路绿到验收。③ **反向探针要真插一行**：源码扫描型判据读的是磁盘上的源文件，所以往真实生产文件里插一行 `DesignGuideline.FindAllByProjectId(projectId)`、用 `--no-build` 直接重跑那一类（**不必重编译**，探针行不参与编译也无妨）→ 实红并点名 `GuidelineRepository.cs:53`，还原后复绿。④ 顺带一条会骗人的坑：改完测试代码后如果**编译失败**，`--no-build` 跑的是**上一个 DLL**，我据此一度误判"加宽正则后仍有真违规"；判绿前先确认这次编译真的成功了（`已成功生成` / `error CS` 都要看）。

72. **"预览看得全"这类界面承诺，判据必须是量出来的，而且成因可能有第二个**（v3.1.0，2026-10-05 输入22 展厅视图档）：用户报"预览区太小、看不全"，我先只做了等比缩放（`fitScale`），写宽窗口那条用例时才发现**展厅栏与工作台共用了 `.ds-mode-pane { max-width:1240px }`** ⇒ 把窗口从 1372 放大到 1920，画布可用宽**一点没变**（648）。⇒ ① 报"改好了"之前要用 e2e 量三件事：**不裁**（`scrollWidth-clientWidth ≤ 2` + 被预览元素的右缘不越过容器内容右缘）、**可达**（滚到末端后右缘进入可见区）、**变宽**（换 `setViewportSize` 后同一元素真的变宽了）；只报"我加了缩放"= 只证了一半。② 缩放比读数要有**唯一出口**（`[data-stage-zoom]`），e2e 读它而不是读组件内部变量——否则"界面显示 100% 但实际缩了"这种假象挡不住。③ 反向探针在两级都要实红：单测（`fitScale` 写死 1 → 红 2 条）+ e2e（E1/E4 红），只红一级说明另一级的判据是空的。④ 顺带两条滚动条坑见 `docs/04-standards/agent-workflow.md` §B3（**分轴** + 本机 Chrome 浮层条 ⇒ 不得钉"条占位>0"）。

## 三、结构变更前必做（表）

- 只改 `Plugins/DesignSystem/Data/Model.xml`，然后 `xcode Model.xml` 生成；**生成物不手改**，业务码进 `.Biz.cs`。
- 生成后跑两次比对：产物逐字节一致 + `BindColumn` 集合不漂移；不一致 = Model.xml 与实体不同步，先修再提交。
- 新表/新列必须在 `DesignSystemTables.EntityTypes` 登记，并在宿主 `ForgeSelf.Api/Data/XCodeConfig.cs` 的 `PluginDbs` 里有 `DesignSystem → design-system` 映射（建表路径的唯一真源）。

## 四、e2e 写法约束（这个插件特有的）

- 断言用**自洽判据**：版本对齐 `plugin.json.Version == meta.modelVersion == 界面徽标`，不写死历史数字；数量断言只设"下限 + 语义"（>100 令牌、≥24 图标），不钉当前实现刚好产生的条数。
- 界面文案不硬编码：主题条显示的是主题名（中文），匹配用 `/深色|dark/i`。
- 写操作两条路径都要测（归档：取消不改后端状态、确认才改），弹窗用 `page.once('dialog', ...)`。
- 每次跑用唯一项目码（`e2e-<时间戳>`），数据隔离在 `.temp/e2e/<ts>/publish/Data`，不碰用户宿主。
- **改了 `Plugins/DesignSystem/web/**/*.vue` 必须先 `pnpm run build` 再跑 e2e**：e2e 走 dev server 读的是 `web/dist`，
  `globalSetup` 只建宿主不建插件前端 —— 否则你验的是改之前的产物（M3 踩过：修完 .vue 直接跑 e2e，红还是那条红）。
- **别硬点「选为工作项目」**：`state.loadProjects()` 会把项目清单第一条自动选为工作项目，此时行内按钮渲染成禁用的「当前」，
  用例判据要写成"这一行成为工作项目"（已是「当前」就直接过），否则隔离库里只有一个项目时永远等不到（M3 G1 实测超时 5 分钟）。
- **结果反馈（成功/失败一句话）要挂在 section 级而不是详情面板里**：归档/删除类动作会把详情面板关掉（选中行从清单消失 → `draft=null`），
  面板内的提示语跟着消失＝"点完没反应"。M3 的真实缺陷 #7 就是这条，e2e 断言 `getByText(/已归档 <code>/)` 才把它逼出来。
- **读"界面派生态"（注入的 `<style>` 文本、computed 值）必须等它「等于权威源」，"非空"和"和上次不同"都不够**：
  `OutfitScope` 的 `<style>` 节点先渲染、`loadCss()` 的取数随后才填进来 → 直接读会拿到空串（M3 实测：单独跑绿，与 guidelines + V 片
  **同一进程连跑**就红 `Received string: ""`，衣柜衣服变多 + 并发 3 改变了时序）。
  第二次红更阴：换装瞬间 `Stage.vue` 把**新衣服 id + 旧的 css 文本**一起交给 `OutfitScope`（`Showroom.vue:loadCss()` 取数期间不清空
  `css`，Vue 原地复用同一个 div），于是 DOM 上是「挂着新 id 的上一件皮肤」——**非空判据与"变化"判据都挡不住**（S1 读 `preset:tech-crisp`
  拿到上一件的青绿强调色）。唯一站得住的判据＝**注入文本的声明行逐条等于这件衣服自己的交付 CSS**：预设走
  `presets/recommend{limit:999}` 拿 request → `generate/preview-css{…request, theme, density}`（与 `outfits.ts:toPreviewInput` 同一份字段表），
  项目衣服走 `export?format=css&theme=`。顺带正面证明同源纪律（画布上看到的就是导出交付的那一份）。
  产品侧根因（`data-outfit` 与正文在取数窗口内不一致）已记 TODO，改它要动 M2 的 `Showroom.vue`/`Stage.vue`，需用户点头。
- **多 spec 连跑 ≠ 单跑**：全目录回归必须按 §一 ③ 真跑整个目录（`workers=1` 为权威），只报单 spec 绿会把时序缺陷报成"没有"。
- **本机有并行会话时，长批 e2e 一律显式钉端口**：`playwright.config.ts` 会"认领端口 + `reuseExistingServer` 复用现有 vite"，
  两条机制叠起来时**别人 teardown 会打死我的 webServer**（M3 批 C 实测：`net::ERR_CONNECTION_REFUSED at :7002`，2 failed / 7 did not run，
  与判据无关）。跑法＝`E2E_FRONTEND_PORT=7402 E2E_BACKEND_PORT=7502 node node_modules/@playwright/test/cli.js test …`（config:28-29 支持 env 覆盖，
  跳过认领并强制自起 server）。失败先分辨"环境红 vs 判据红"，别拿环境红去改用例。
- 环境缺件先补正规入口：缺 SQLite provider → 从 `build/runtime/Plugins/` 备到 `publish/Plugins/`（gitignored）；缺浏览器 → `pnpm run browsers:install`。禁止改用一次性临时脚本当验证。

## 五、已知未做（不要假装完成）

- **复合排版令牌导出时 `fontFamily` 被丢弃**（v3.1.0，M3 发现，用户拍板本批不修 → TODO）：`ExportService.TypographyCss()` 读了 `fontFamily` 却不输出 →
  "选了 editorial-serif 预设后标题在展厅里真是衬线"这条**还不成立**。e2e 把事实拍下来（断言标题字族 == 正文字族）而不是假装通过；改它要先定"默认产物是否允许变"（会动 golden 基线）。
- **XCode 冷进程建表不可在共享测试进程里复现**：按需建表只在实体 Meta 首次初始化时决定，别的用例先热了 Meta 就再也看不到"冷启动建表"那条路径。
  `GuidelineUpgradeTests` 因此写成顺序无关（自己 `EnsureCreated()` + 临时库），并在类注释里写明这条限制 —— 别把它当成"建表路径已被冷启动验证"。
- **`RequireProject(id)` 在 `Guard` 之外**（全站既有形态，M3 规范端点沿用）：项目不存在实际回 **500 而不是 404**（宿主没有全局异常中间件）。
  改它＝改全站状态码口径，需用户拍板；用例按现状断言抛出 `KeyNotFoundException` 并注释指向 TODO。

- **e2e 里 `export?format=bundle` 经 dev 代理取回会 `Failed to fetch`**，根因未定位（已排除"整包太慢"：后端实测构建 605ms）。
  整包体积/耗时改由后端用例 `ExportProjectionTests` 的 `[T302]` 给数字；**用户真实下载路径（生产无代理）仍未核验** → TODO。
- 插件 src 无 eslint 入口（宿主 flat config 覆盖不到 `../Plugins/**`）。
- `plugin.json` entry 无 content-hash 缓存键（宿主加载器职责）。
- **宿主 SQLite 并发 BUSY 未根治**（G8 / ROADMAP P1.5）：现在靠 `Busy Timeout=5000` + 只读重试 + 整批事务 + 按项目串行化顶着，
  去掉重试仍会 500；治理方案需人拍板。
- ~~组件变体 × 状态矩阵为空~~（已做：`SeedComponentCatalog` 落 10 个蓝本 + 三轴格子，e2e 断言 `.cg__vars li` ≥20）。
- ~~导出体积未实测~~（已做：后端 `[T302]` + e2e 打印 CSS / data.sql / DTCG 三个 KiB 数字）。

## 六、复盘回写义务

跑完本技能若发现**新的失败模式**，把它加进第二节自查表（一句话："历史坑：…"）；若属通用工程规则，写 `docs/04-standards/agent-workflow.md` Part B。技能与文档不一致时，以能跑通的命令为准并回写本文。
