import { describe, expect, it } from 'vitest'
import { readFileSync, readdirSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

/**
 * 组件契约守护测试（041 设计 token 专项 + 交互规格专项）
 *
 * 为什么不写在 CSS 里、而要写成测试：
 *   设计规范（.agents/skills/ui-ux-design/SKILL.md）写了没人看，评审时靠人眼扫，
 *   扫不出来就混进代码。这里把规范里**可机械判定**的四条变成断言 ——
 *   以后谁再硬编码 0.16s、谁再加一个没有 hover 的按钮，CI 当场拦下。
 *
 * 覆盖四条：
 *   1. token 族完备：颜色 / 字号 / 间距 / 圆角 / 动效 五族都要有，缺一族就等于没体系；
 *   2. 动效不硬编码秒数，且回弹曲线不滥用于普通 hover（SKILL 明令：回弹只给 logo）；
 *   3. 无限循环动画要登记（数量受控，新增需显式放行）；
 *   4. 交互规格：模板里出现的每个按钮类都要有 hover 反馈 + 键盘焦点环。
 */

const here = dirname(fileURLToPath(import.meta.url))
const cssPath = join(here, 'styles/widget.css')
const componentsDir = join(here, 'components')
const blocksDir = join(here, 'components/blocks')
const css = readFileSync(cssPath, 'utf8')

/** 收集目录下所有 .vue 的模板文本 */
function readVueTemplates(dir: string): string[] {
  return readdirSync(dir)
    .filter((f) => f.endsWith('.vue'))
    .map((f) => readFileSync(join(dir, f), 'utf8'))
}

/** 从模板里抽出所有 <button> 上挂的类名。
    按钮是多行书写的（class 在下一行），所以按 <button 起手向后取 3 行再捞 class。 */
function collectButtonClasses(): string[] {
  const templates = [...readVueTemplates(componentsDir), ...readVueTemplates(blocksDir)]
  const out = new Set<string>()
  for (const tpl of templates) {
    const lines = tpl.split(/\r?\n/)
    lines.forEach((line, i) => {
      if (!line.includes('<button')) return
      const chunk = lines.slice(i, i + 3).join('\n')
      const m = /class="([^"]+)"/.exec(chunk)
      if (!m) return
      for (const cls of m[1].split(/\s+/)) {
        if (cls.startsWith('aiw-')) out.add(cls)
      }
    })
  }
  return [...out].sort()
}

/** 这些类名是**容器/图标/徽标**，自身不可点，不承担交互态 */
const NON_INTERACTIVE = new Set([
  'aiw-think__spark', 'aiw-think__label', 'aiw-tool__icon', 'aiw-tool__name',
  'aiw-history__list', 'aiw-fallback', 'aiw-pair__expired', 'aiw-grp__title',
  'aiw-grp__badge', 'aiw-html__frame', 'aiw-code', 'aiw-ask__ico',
])

/** CSS 中带某伪类选择器的类名集合（支持 .a:hover 与 .a--b:hover 两种写法） */
function selectorsWith(pseudo: string): Set<string> {
  const out = new Set<string>()
  const re = new RegExp(`\\.([\\w-]+)${pseudo}\\b`, 'g')
  let m: RegExpExecArray | null
  while ((m = re.exec(css))) out.add(m[1])
  return out
}

describe('设计 token：token 族完备性', () => {
  it('字号要有阶梯 token（--aiw-fs-*），不能全篇硬编码 px', () => {
    const has = /--aiw-fs-/.test(css)
    expect(has, 'widget.css 缺少 --aiw-fs-* 字号 token 族').toBe(true)
  })

  it('间距要有台阶 token（--aiw-space-*）', () => {
    expect(/--aiw-space-/.test(css), 'widget.css 缺少 --aiw-space-* 间距 token 族').toBe(true)
  })

  it('圆角要有分档 token（--aiw-radius-*），且至少 3 档（小/中/大）', () => {
    const radii = [...css.matchAll(/--aiw-radius(?:-([\w]+))?\s*:/g)].map((m) => m[1] || 'base')
    expect(radii.length, `圆角 token 只有 ${radii.length} 个（${radii.join(',')}），不够分档`).toBeGreaterThanOrEqual(3)
  })

  it('动效时长要有 token（--aiw-dur-*）+ 缓动 token（--aiw-ease-*）', () => {
    expect(/--aiw-dur-/.test(css), '缺 --aiw-dur-* 时长 token').toBe(true)
    expect(/--aiw-ease-/.test(css), '缺 --aiw-ease-* 缓动 token').toBe(true)
  })

  it('颜色 token 已存在（既有能力，防回归）', () => {
    for (const v of ['--aiw-primary', '--aiw-text', '--aiw-line', '--aiw-bg']) {
      expect(css, `缺颜色 token ${v}`).toContain(v)
    }
  })
})

/** 先用 token 的写法长这样，不含秒数字面量：`transition: opacity var(--aiw-dur-instant) …`。
    所以「出现 \d+s」就等价于「没走 token」，可以直接当计数信号用。
    注释要先剥掉：注释里讨论「以前写 0.16s」也会被算成一处，基数就不准了。 */
function stripComments(s: string): string {
  return s.replace(/\/\*[\s\S]*?\*\//g, '')
}
const cssCode = stripComments(css)

/* 棘轮（ratchet）基数 = 2026-10-16 实测存量。
   为什么不直接要求「一条都不许有」：那是 1300 行存量一次性重写的账单，
   一次交不清，结果是门禁长期红着、最后被加 !important 绕过或被删掉。
   棘轮的效果是：新增硬编码**立刻**红（数量只许减），
   每替掉一处就可以把基数下调 1 —— 债务单调递减，且永远有新代码守着。 */
const HARDCODED_TRANSITION_BUDGET = 13
const HARDCODED_ANIMATION_BUDGET = 6

describe('设计 token：动效规范（SKILL 动效章节）', () => {
  it('transition 硬编码秒数只减不增（棘轮，替换一处就下调 1）', () => {
    const hits = cssCode.match(/transition[^;]*?\d*\.?\d+s/g) || []
    expect(
      hits.length,
      `硬编码 transition ${hits.length} 处 > 棘轮上限 ${HARDCODED_TRANSITION_BUDGET}；` +
        `新写的 transition 必须用 var(--aiw-dur-*)，替掉存量后把基数下调 1。样本：${hits.slice(0, 5).join(' | ')}`
    ).toBeLessThanOrEqual(HARDCODED_TRANSITION_BUDGET)
  })

  it('animation 硬编码秒数只减不增（棘轮）', () => {
    const hits = cssCode.match(/animation:[^;]*?\d*\.?\d+s/g) || []
    expect(
      hits.length,
      `硬编码 animation ${hits.length} 处 > 棘轮上限 ${HARDCODED_ANIMATION_BUDGET}；` +
        `循环动画时长不属于交互档位，需单独立项。样本：${hits.slice(0, 5).join(' | ')}`
    ).toBeLessThanOrEqual(HARDCODED_ANIMATION_BUDGET)
  })

  it('新规则一律用动效 token —— 抽查：文件里必须真的出现 var(--aiw-dur-*) 引用', () => {
    expect(/var\(--aiw-dur-/.test(cssCode), 'token 定义了却没人用，等于没建').toBe(true)
    expect(/var\(--aiw-ease-/.test(cssCode)).toBe(true)
  })

  it('回弹曲线（0.34,1.56,0.64,1）不得用在普通 hover 上 —— 它只属于 logo/重点出现时刻', () => {
    const bad = [...css.matchAll(/([^{}]+)\{[^}]*cubic-bezier\(\s*0\.34\s*,\s*1\.56\s*,\s*0\.64\s*,\s*1\s*\)[^}]*\}/g)]
      .map((m) => m[1].trim())
      .filter((sel) => !/:hover/.test(sel))
    // 这是「误报方向」的反向断言：找出**确实**挂在 hover 上的回弹曲线
    const hoverBounce = [...css.matchAll(/([^{}]+:hover)\s*\{[^}]*cubic-bezier\(\s*0\.34\s*,\s*1\.56\s*,\s*0\.64\s*,\s*1\s*\)/g)].map(
      (m) => m[1].trim()
    )
    expect(hoverBounce, `这些 hover 用了锤落回弹曲线（应改 --aiw-ease-out）：${hoverBounce.join(' | ')}`).toEqual([])
    expect(bad.length).toBeGreaterThanOrEqual(0) // 非 hover 场景允许保留（如 launcher 出场）
  })

  it('必须尊重 prefers-reduced-motion（既有能力，防回归）', () => {
    expect(css).toContain('prefers-reduced-motion')
  })
})

describe('设计 token：无限动画受控', () => {
  it('无限循环动画必须在登记名单内（新增无限动画需显式放行）', () => {
    // 允许清单：这些是有信息价值的（等待/连接中/防烧屏），不是纯装饰
    const allowed = ['aiw-pulse', 'aiw-bounce', 'aiw-burnin-drift', 'aiw-burnin-breathe', 'aiw-reconnect-pulse', 'aiw-remote-blink']
    const used = [...css.matchAll(/animation:\s*([\w-]+)[^;]*infinite/g)].map((m) => m[1])
    const unknown = used.filter((n) => !allowed.includes(n))
    expect(unknown, `未在白名单的无限动画：${unknown.join(', ')}（装饰性无限动画一律不做）`).toEqual([])
  })
})

describe('交互规格：每个按钮都要有 hover 与键盘焦点环', () => {
  const buttons = collectButtonClasses().filter((c) => !NON_INTERACTIVE.has(c))
  const hoverSel = selectorsWith(':hover')
  const focusSel = selectorsWith(':focus-visible')

  it('能抽到按钮类（防测试自身写空）', () => {
    expect(buttons.length).toBeGreaterThan(5)
  })

  it('每个按钮类都要有 hover 反馈', () => {
    const missing = buttons.filter((c) => !hoverSel.has(c))
    expect(missing, `这些可点元素没有任何 hover 反馈：\n  ${missing.join('\n  ')}`).toEqual([])
  })

  it('每个按钮类都要有键盘焦点环（含通用兜底规则）', () => {
    const hasGeneric = /\.aiw-root\s+button:focus-visible/.test(css)
    const missing = buttons.filter((c) => !focusSel.has(c) && !hasGeneric)
    expect(missing, `这些按钮键盘聚焦时看不到焦点：${missing.join(', ')}`).toEqual([])
  })

  it('焦点环不能用 outline:none 抹掉（除非有等效替代，如输入框分隔线加粗）', () => {
    // 输入框那条是用户明确要求的「不要聚焦边框色」，且有分隔线加粗顶上，属已登记的例外
    const exceptions = new Set(['aiw-panel', 'aiw-history'])
    const killed = [...css.matchAll(/\.([\w-]+):focus(?:-visible)?\s*\{[^}]*outline:\s*none/g)].map((m) => m[1])
    const illegal = killed.filter((c) => !exceptions.has(c) && !c.includes('textarea'))
    expect(illegal, `这些元素把焦点环抹了却没替代手段：${illegal.join(', ')}`).toEqual([])
  })
})

/* ============================================================
   边框归属：只工具调用有框（2026-10-26 用户拍板）
   ------------------------------------------------------------
   为什么要有这条：边框是「谁都可以随手加一条 border」的东西，加回去不会让
   任何其它测试变红 —— 上一版正文气泡、思考块、步骤分组都带框，就是在
   真实会话里被用户重新指出来才发现的。把「过程/正文不许带框」写成断言，
   下次谁再把框加回来，这里当场红。
   保留边框的（有意为之，不在此断言里）：工具调用展开体 .aiw-tool__body
   （唯一给工具用的框）、危险审批（风险色语义）、数据卡片类
   （.aiw-chart / .aiw-kpi__item / .aiw-tablewrap / .aiw-cite /
   .aiw-html / .aiw-codeblk / .aiw-img / .aiw-plan）。
   ============================================================ */
const blocksCssPath = join(here, 'styles/blocks.css')
const blocksCss = readFileSync(blocksCssPath, 'utf8')

/** 取某个选择器的第一条规则正文（没找到返回空串，断言会跟着红） */
function ruleBody(src: string, selector: string): string {
  const esc = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const m = new RegExp(`(?:^|\\n)\\s*${esc}\\s*\\{([^}]*)\\}`).exec(src)
  return m ? m[1] : ''
}

/** border-radius 属圆角，不算「框」；其余 border* 都算 */
const hasBorder = (body: string) => /border(?!-radius)\s*:/.test(body)
const hasShadow = (body: string) => /box-shadow\s*:/.test(body)

describe('边框归属：仅工具调用有边框，过程与正文无边框', () => {
  it('正文气泡无边框无阴影（去框后靠底色区分，不再切盒子）', () => {
    const body = ruleBody(css, '.aiw-msg__bubble')
    expect(body, '.aiw-msg__bubble 规则没找到').not.toBe('')
    expect(hasBorder(body), '.aiw-msg__bubble 又把 border 加回来了').toBe(false)
    expect(hasShadow(body), '.aiw-msg__bubble 又把 box-shadow 加回来了').toBe(false)
  })

  it('思考块与步骤分组无边框（过程行不再套框）', () => {
    for (const sel of ['.aiw-think', '.aiw-grp']) {
      const body = ruleBody(blocksCss, sel)
      expect(body, `${sel} 规则没找到`).not.toBe('')
      expect(hasBorder(body), `${sel} 又把 border 加回来了`).toBe(false)
    }
  })

  it('工具调用展开体必须保留边框（它是唯一允许的框）', () => {
    const body = ruleBody(blocksCss, '.aiw-tool__body')
    expect(body, '.aiw-tool__body 规则没找到').not.toBe('')
    expect(hasBorder(body), '.aiw-tool__body 的框被误删了').toBe(true)
  })

  it('行首图标槽固定宽度（槽内切 opacity，不得用 display 切换撑动布局）', () => {
    const body = ruleBody(blocksCss, '.aiw-ico')
    expect(body).toContain('22px')
    expect(body, '.aiw-ico 必须 fixed 宽度（flex: 0 0 …），否则悬浮时会位移').toMatch(/flex:\s*0 0 22px/)
  })
})
