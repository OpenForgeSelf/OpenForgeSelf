#!/usr/bin/env node
// DESIGN.md 设计规范静态守卫（门禁化，2026-10-11 用户输入3 立）。
//
// 背景：用户要求「框架变更、页面开发等都按设计规范来，做成门禁而不是靠自觉」。
// 本脚本把 DESIGN.md 中**可机器判定的**硬性条款做成棘轮守卫（记录基线，只禁止增量上涨），
// 与 scripts/check-style-tokens.mjs（禁新增硬编码色值）互为补充：
//   - check-style-tokens.mjs  → 禁「新增」裸色值
//   - check-design-compliance.mjs → 禁「新增」设计规范违反（自定义 token 族 / 胶囊按钮 / 玻璃过头 / 蓝紫渐变）
// 不可机器判定的部分（可爱风、品牌语汇、UX 一致性）由 ui-ux-design 走查 + Spec「Interaction Design」节把关。
//
// 规则（对应 DESIGN.md「三禁红线」与「token 单一来源」）：
//   A. token 族白名单：组件层禁止定义白名单之外的 CSS 变量（token 单一来源）
//      允许族：--el-*（Element Plus 官方）、--ink-* --gold-* --paper-* --cinnabar-* --bronze-*
//              --fg-* --border-* --font-* --dur-* --ease-* --shadow-* --radius-* --glass-*
//              --tw-*（Tailwind）、--webkit-*（引擎私有）、--*（THEME 定义层豁免）
//      特别禁止（DESIGN.md 明文）：--surface-* / --forge-* / --app-*
//   B. 禁胶囊按钮：border-radius 9999px/999px，或 rounded-full 用在按钮上下文
//   C. 禁玻璃过头：backdrop-filter/filter 的 blur 值 > 40px（规范玻璃条为 18-24px）
//   D. 禁蓝紫渐变：linear-gradient 出现 indigo/violet/purple 系色值（#4f46e5 #6366f1 #8b5cf6
//      #a855f7 #7c3aed #6d28d9 #9333ea 等，均不在 DESIGN 色板内）
//
// 用法：
//   node scripts/check-design-compliance.mjs            # 对比基线，超出则 exit 1
//   node scripts/check-design-compliance.mjs --update   # 用当前实测值重写基线
//
// 基线文件：scripts/design-compliance-baseline.json（随代码入库）。
// 接入：pre-commit hook 与交付门禁（AGENTS.md §8 设计门禁三步）各调一次。

import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BASELINE = path.join(ROOT, 'scripts', 'design-compliance-baseline.json')

// 目标目录（相对仓库根）
const targets = ['ForgeSelf.Web/src']
// token 定义层豁免：主题 token 文件天然承载设计 token 族（DESIGN.md「token 单一来源」），
// 组件层（*.vue / *.ts / 非 themes/ 的 css）仍受全部规则约束。
const THEME_SUFFIX = '/styles/themes/'

const pluginsDir = path.join(ROOT, 'Plugins')
if (fs.existsSync(pluginsDir)) {
  for (const d of fs.readdirSync(pluginsDir)) {
    const rel = path.posix.join('Plugins', d, 'web', 'src')
    if (fs.existsSync(path.join(ROOT, rel))) targets.push(rel)
  }
}

const EXT = new Set(['.vue', '.ts', '.tsx', '.css', '.scss', '.less'])

function walk(dir, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name)
    if (e.isDirectory()) walk(p, out)
    else if (EXT.has(path.extname(e.name).toLowerCase())) out.push(p)
  }
  return out
}

// ---- 规则 A：token 族白名单 ----
// 匹配 CSS 变量「定义处」（--name: 出现在样式块），排除使用处（var(--name)）。
const VAR_DEF_RE = /(?:^|[;{,])\s*(--[a-zA-Z][\w-]*)\s*:/g
const ALLOWED_TOKEN_FAMILIES = [
  '--el-', '--tw-', '--webkit-', // 框架/引擎
  '--aiw-', // 迁移组件库 ai-assistant-widget 的私有 token 命名空间（Plugins/AIAgent/web/src/agent-core，与 --el- 同性质的组件库封装变量；主题化只允许把它桥接到 --el-*，不得在其中发明新色值）
  '--ink-', '--gold-', '--paper-', '--cinnabar-', '--bronze-', // DESIGN 色板族
  '--fg-', '--border-', '--font-', '--dur-', '--ease-', // 语义族
  '--shadow-', '--radius-', '--glass-', // 组件族
]
const FORBIDDEN_TOKEN_FAMILIES = ['--surface-', '--forge-', '--app-']

function isAllowedTokenFamily(name) {
  return ALLOWED_TOKEN_FAMILIES.some((f) => name.startsWith(f))
}
function isForbiddenTokenFamily(name) {
  return FORBIDDEN_TOKEN_FAMILIES.some((f) => name.startsWith(f))
}

// ---- 规则 B：胶囊按钮 ----
// 按钮上下文：类名/样式块包含 button / btn
const CAPSULE_BTN_RE = /border-radius\s*:\s*(9999|999)px|rounded-full/g
const BTN_CTX_RE = /(?:^|\s)(?:el-button|btn|button)(?:\s|\.|,|\{)/i

// ---- 规则 C：玻璃过头 ----
const BLUR_RE = /(?:backdrop-filter|filter)\s*:[^;]*?blur\(\s*(\d+(?:\.\d+)?)px/g
const BLUR_MAX = 40

// ---- 规则 D：蓝紫渐变 ----
const VIOLET_HEX = /#(?:4f46e5|6366f1|8b5cf6|a855f7|7c3aed|6d28d9|9333ea|c026d3|d946ef|7c3aed|5b21b6)/i
const GRADIENT_RE = /linear-gradient\([^)]*\)/g

function countIn(rel) {
  const abs = path.join(ROOT, rel)
  const result = { a: 0, b: 0, c: 0, d: 0, files: 0 }
  if (!fs.existsSync(abs)) return result
  for (const f of walk(abs)) {
    const relF = path.posix.join(rel, path.relative(abs, f).split(path.sep).join('/'))
    const isTheme = relF.includes(THEME_SUFFIX)
    const txt = fs.readFileSync(f, 'utf8')
    let hit = false

    // A：token 族（themes/ 豁免——那是 token 定义层）
    if (!isTheme) {
      let m
      VAR_DEF_RE.lastIndex = 0
      while ((m = VAR_DEF_RE.exec(txt)) !== null) {
        const name = m[1]
        if (isForbiddenTokenFamily(name) || !isAllowedTokenFamily(name)) {
          result.a++
          hit = true
        }
      }
    }

    // B：胶囊按钮
    if (BTN_CTX_RE.test(txt) && CAPSULE_BTN_RE.test(txt)) {
      // 逐处计数
      CAPSULE_BTN_RE.lastIndex = 0
      let m
      while ((m = CAPSULE_BTN_RE.exec(txt)) !== null) {
        // 粗略限定在按钮行内
        const lineStart = txt.lastIndexOf('\n', m.index)
        const lineEnd = txt.indexOf('\n', m.index)
        const line = txt.slice(lineStart + 1, lineEnd < 0 ? txt.length : lineEnd)
        if (BTN_CTX_RE.test(line) || /btn|button/i.test(line)) {
          result.b++
          hit = true
        }
      }
    }

    // C：玻璃过头
    BLUR_RE.lastIndex = 0
    let mc
    while ((mc = BLUR_RE.exec(txt)) !== null) {
      const v = parseFloat(mc[1])
      if (v > BLUR_MAX) {
        result.c++
        hit = true
      }
    }

    // D：蓝紫渐变
    GRADIENT_RE.lastIndex = 0
    let md
    while ((md = GRADIENT_RE.exec(txt)) !== null) {
      if (VIOLET_HEX.test(md[0])) {
        result.d++
        hit = true
      }
    }

    if (hit) result.files++
  }
  return result
}

const current = {}
const total = { a: 0, b: 0, c: 0, d: 0 }
for (const t of targets) {
  const r = countIn(t)
  current[t] = { a: r.a, b: r.b, c: r.c, d: r.d }
  total.a += r.a
  total.b += r.b
  total.c += r.c
  total.d += r.d
}

function fmt(r) {
  return `a(token族)=${r.a} b(胶囊)=${r.b} c(玻璃过头)=${r.c} d(蓝紫渐变)=${r.d}`
}

if (process.argv.includes('--update')) {
  const data = {
    generatedAt: new Date().toISOString().slice(0, 10),
    rules: { a: 'token族白名单', b: '禁胶囊按钮', c: '禁玻璃过头blur>40px', d: '禁蓝紫渐变' },
    total: { a: total.a, b: total.b, c: total.c, d: total.d },
    dirs: current,
  }
  fs.writeFileSync(BASELINE, JSON.stringify(data, null, 2) + '\n', 'utf8')
  console.log(`[design-compliance] 基线已更新 -> ${path.relative(ROOT, BASELINE)}`)
  console.log(`[design-compliance] 合计 ${fmt(total)}`)
  process.exit(0)
}

if (!fs.existsSync(BASELINE)) {
  console.log('[design-compliance] 无基线文件，跳过（先运行：node scripts/check-design-compliance.mjs --update）')
  process.exit(0)
}

const base = JSON.parse(fs.readFileSync(BASELINE, 'utf8'))
const regressions = []
for (const t of targets) {
  const now = current[t] || { a: 0, b: 0, c: 0, d: 0 }
  const was = base.dirs?.[t] ?? { a: 0, b: 0, c: 0, d: 0 }
  for (const k of ['a', 'b', 'c', 'd']) {
    if ((now[k] || 0) > (was[k] || 0)) {
      regressions.push({ target: t, rule: k, was: was[k] || 0, now: now[k] || 0, delta: (now[k] || 0) - (was[k] || 0) })
    }
  }
}

console.log(`[design-compliance] ${fmt(total)}（基线 ${fmt(base.total ?? { a: 0, b: 0, c: 0, d: 0 })}）`)
if (regressions.length === 0) {
  console.log('[design-compliance] PASS：无新增设计规范违反（DESIGN.md 门禁）')
  process.exit(0)
}

console.log('[design-compliance] FAIL：以下目录新增了设计规范违反（DESIGN.md 三禁红线 / token 单一来源）：')
for (const r of regressions) {
  const ruleName = { a: 'token族白名单', b: '禁胶囊按钮', c: '禁玻璃过头', d: '禁蓝紫渐变' }[r.rule]
  console.log(`  - ${r.target} [${ruleName}]：${r.was} -> ${r.now}（+${r.delta}）`)
}
console.log('[design-compliance] 请按 DESIGN.md「token 单一来源 / 三禁红线」整改：')
console.log('[design-compliance]   A → 用 DESIGN token 族（--ink-* --gold-* --paper-* --fg-* --glass-* 等）或 --el-*；')
console.log('[design-compliance]   B → 按钮用 6px/8px 圆角（--radius-*），禁 9999px 胶囊；')
console.log('[design-compliance]   C → 玻璃 blur 保持 18-24px（--glass 工具类），禁 >40px；')
console.log('[design-compliance]   D → 渐变只用 DESIGN 色板内的色（墨/金/宣纸/朱砂/青铜/天青/深蓝/深绿/深紫），禁 indigo/violet 系。')
console.log('[design-compliance] 若确为合理新增，先与维护者确认，勿直接跑 --update 抹平。')
process.exit(1)
