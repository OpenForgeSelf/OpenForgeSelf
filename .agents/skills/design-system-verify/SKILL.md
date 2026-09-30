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
#   核对：报告里的"测试总数"必须等于 --list-tests 的发现数（当前 164）；不等就是事故，不算通过

# ② 插件前端（check/test 都借宿主工具链，插件本身不装 vue-tsc/vitest）
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build

# ③ 插件层 e2e（真实宿主 + 真实插件库 + 截图读图，零 mock）
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system

# ④ 发布与走查（不打 tag，只出本地 zip）
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\release\release-local.ps1 -Version vX.Y.Z -UpdateDir <目录>
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

## 三、结构变更前必做（表）

- 只改 `Plugins/DesignSystem/Data/Model.xml`，然后 `xcode Model.xml` 生成；**生成物不手改**，业务码进 `.Biz.cs`。
- 生成后跑两次比对：产物逐字节一致 + `BindColumn` 集合不漂移；不一致 = Model.xml 与实体不同步，先修再提交。
- 新表/新列必须在 `DesignSystemTables.EntityTypes` 登记，并在宿主 `ForgeSelf.Api/Data/XCodeConfig.cs` 的 `PluginDbs` 里有 `DesignSystem → design-system` 映射（建表路径的唯一真源）。

## 四、e2e 写法约束（这个插件特有的）

- 断言用**自洽判据**：版本对齐 `plugin.json.Version == meta.modelVersion == 界面徽标`，不写死历史数字；数量断言只设"下限 + 语义"（>100 令牌、≥24 图标），不钉当前实现刚好产生的条数。
- 界面文案不硬编码：主题条显示的是主题名（中文），匹配用 `/深色|dark/i`。
- 写操作两条路径都要测（归档：取消不改后端状态、确认才改），弹窗用 `page.once('dialog', ...)`。
- 每次跑用唯一项目码（`e2e-<时间戳>`），数据隔离在 `.temp/e2e/<ts>/publish/Data`，不碰用户宿主。
- 环境缺件先补正规入口：缺 SQLite provider → 从 `build/runtime/Plugins/` 备到 `publish/Plugins/`（gitignored）；缺浏览器 → `pnpm run browsers:install`。禁止改用一次性临时脚本当验证。

## 五、已知未做（不要假装完成）

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
