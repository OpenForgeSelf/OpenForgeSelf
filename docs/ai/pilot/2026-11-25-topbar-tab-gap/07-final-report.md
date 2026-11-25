# 07 Final Report — topbar-tab-gap（顶部栏页签间距 + logo/关闭图标同位互换）

date: 2026-11-25 ｜ 任务协调人指令: 输入28（v1 间距）+ 追加轮（v2 重设计）+ v3（图标在左）｜ 状态: ✅ COMPLETED（未提交，待验收）

## 结论

三轮需求全部落地并验证：页签左右间距收紧（文字间距 30→20px）；交互模型为「**左侧** 16px 槽默认显示功能/插件 logo（v3 按用户指令从右移左，`[logo][标签]` 浏览器页签式布局），悬浮时 logo 同位变为关闭/取消钉住图标」——原保留槽空白变成有意义的 logo 内容，「边距太大」从根上解决；悬浮零位移不变量（PC-042）由「槽常驻预留」升级为「同盒同位互换」，结构上根除。快档门禁全绿（check / vitest 123 文件 1278 / design:check）+ 守卫 9/9 + 四态截图读图通过。**未 commit**（未经批准）。

## 交付物

| 文件 | 说明 |
| --- | --- |
| `ForgeSelf.Web/src/components/TopNavbar.vue` | padding 9px / 下划线 inset 7px / `.tab-icon-slot` + `.tab-logo` + `.close-x` 同位互换 |
| `ForgeSelf.Web/src/__tests__/TopNavbar.test.ts` | 守卫改造 8→9 条（不变量载体随 v2 结构升级） |
| `docs/ai/pilot/2026-11-25-topbar-tab-gap/mini-task.md` | 轻量工件（含 v1→v2 需求变更记录） |
| `05-evidence.md` / `06-review.md` / 本文件 | 证据 / 审查（APPROVED）/ 收尾报告 |
| `ForgeSelf.Web/screenshots/dev/gap2-{idle,hover}-{light,dark}.png` | 四态走查读图证据（gitignored） |

## 验证汇总（全部 Verified，除注明）

- 守卫套件 9/9（v3 复跑，15.12s）
- 快档：`pnpm run check` PASS / `pnpm exec vitest run` **123 文件 1278 全绿**（v3 轮因机器内存压力限 `--maxWorkers=2`，154.76s）/ `pnpm run design:check` 双段 PASS
- 走查：logo SVG 4/4 渲染、**图标在左**（槽 right ≤ label left 实测）；hover 前后页签宽零变化（44.5/79.8/93.1/93.1/93.1）；相邻文字间距 30→20px；亮暗四态读图通过（`screenshots/dev/gap3-*.png`）
- Inferred 2 项：插件贡献页签 logo 实渲染（数据面有缺省 `package`，本次未开插件页签）；focus-within 键盘互换（与 hover 同源 CSS 规则）
- 环境事件归因：14:01-14:16 门禁批遭遇 pwsh 宿主 OOM/栈溢出 ×2、Node OOM ×1、tinypool `Channel closed` ×1——同源机器瞬时内存压力，非代码缺陷，分段重跑后逐段取证

## 设计决策

- **logo 数据源**：复用功能单一真源 `features.ts mergeFeatureList`（内置 + 插件清单）+ `featureIcons.ts` lucide 单一表（与「所有功能」页同源），不引入第二套图标体系 → 符合「单一真源、少耦合」准则。
- **同位互换而非双图标位**：用户指定「logo 变成关闭图标」；同盒 absolute 覆盖使零位移成为结构性质而非靠预留位凑数，PC-042 守卫语义保持且更强。
- **未知图标/无匹配页签容错**：空占位不报错（featureIcons 既有约定），槽常驻保证可关闭与零位移。

## 代价·收益

- 收益：页签区信息密度提升（logo 识别功能）、交互与浏览器页签心智一致、悬浮关闭不再引起整排位移（本就零位移，现结构更强）、守卫从「槽预留」升级为「互换不变量」。
- 代价：2 文件 +~60 行（模板/script/样式/守卫）；无依赖新增；无运行时开销（一次 Map 构建 + 静态 SVG 字符串）。

## 不做事决策（NOT-to-do，已同步 docs/07-decisions/not-taken-decisions.md）

1. **不改 tabs store 数据结构**（不给 NavTab 加 icon 字段）——logo 可由 path 派生，存储冗余会在功能表改名后失同步；按需 computed 匹配即可。若未来出现「同 path 多图标语境」再议。
2. **不为图标互换写 e2e**——hover 显隐是纯 CSS 结构性质，G1 组件守卫（读源断言 visibility/display 语义）已锁定根因面；e2e 对此覆盖增益低于成本。快档已含全量 vitest。
3. **不删除 📌 语义特例**（钉住项 hover 显示图钉而非 ×）——保留既有「取消钉住」交互契约，仅换触发外观。

## 风险

- 插件页签 logo 实渲染为 Inferred（见上）；首次打开插件页签时可肉眼复核，不阻塞交付。
- 暗色态 logo 用 `--el-text-color-secondary`（currentColor），与文字同对比度，无新增对比度风险面。

## 后续（不阻塞）

- 无强制后续。若用户仍觉得间距大，下一旋钮是 `.tab-item` gap（label↔槽 6px→4px）与 nav gap（2px→0），改动面同本批。
