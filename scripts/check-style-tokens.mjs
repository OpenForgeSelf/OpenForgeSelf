#!/usr/bin/env node
// 禁止「新增」硬编码色值（棘轮 ratchet）。
//
// 背景：AGENTS.md §7 规定「样式只用 --el-* 与 Tailwind 布局原语，不硬编码色值」，
// 但实测仓库存在大量历史硬编码色值（首次测量：ForgeSelf.Web/src 1449 处、Plugins/*/web/src 777 处）。
// 一刀切设为 ESLint error 会直接炸掉 `pnpm run check`，故采用棘轮：记录基线，只禁止增量上涨。
//
// 用法：
//   node scripts/check-style-tokens.mjs            # 对比基线，超出则 exit 1
//   node scripts/check-style-tokens.mjs --update   # 用当前实测值重写基线
//
// 基线文件：scripts/style-token-baseline.json（随代码入库）。

import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BASELINE = path.join(ROOT, 'scripts', 'style-token-baseline.json')

// 目标目录（相对仓库根）
const targets = ['ForgeSelf.Web/src']
const pluginsDir = path.join(ROOT, 'Plugins')
if (fs.existsSync(pluginsDir)) {
  for (const d of fs.readdirSync(pluginsDir)) {
    const rel = path.posix.join('Plugins', d, 'web', 'src')
    if (fs.existsSync(path.join(ROOT, rel))) targets.push(rel)
  }
}

const EXT = new Set(['.vue', '.ts', '.tsx', '.css', '.scss', '.less'])
// 硬编码色值：hex / rgb(a) / oklch / hsl(a)
const COLOR_RE = /#[0-9a-fA-F]{3,8}\b|\brgba?\(|\boklch\(|\bhsla?\(/g

function walk(dir, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name)
    if (e.isDirectory()) walk(p, out)
    else if (EXT.has(path.extname(e.name).toLowerCase())) out.push(p)
  }
  return out
}

function countIn(rel) {
  const abs = path.join(ROOT, rel)
  if (!fs.existsSync(abs)) return { count: 0, files: 0 }
  let count = 0
  let files = 0
  for (const f of walk(abs)) {
    const txt = fs.readFileSync(f, 'utf8')
    const m = txt.match(COLOR_RE)
    if (m && m.length) { count += m.length; files++ }
  }
  return { count, files }
}

const current = {}
let total = 0
for (const t of targets) {
  const r = countIn(t)
  current[t] = r.count
  total += r.count
}

if (process.argv.includes('--update')) {
  const data = { generatedAt: new Date().toISOString().slice(0, 10), total, dirs: current }
  fs.writeFileSync(BASELINE, JSON.stringify(data, null, 2) + '\n', 'utf8')
  console.log(`[style-tokens] 基线已更新 -> ${path.relative(ROOT, BASELINE)}`)
  console.log(`[style-tokens] 合计 ${total} 处（${Object.entries(current).map(([k, v]) => `${k}:${v}`).join(', ')}）`)
  process.exit(0)
}

if (!fs.existsSync(BASELINE)) {
  console.log('[style-tokens] 无基线文件，跳过（先运行：node scripts/check-style-tokens.mjs --update）')
  process.exit(0)
}

const base = JSON.parse(fs.readFileSync(BASELINE, 'utf8'))
const regressions = []
for (const t of targets) {
  const now = current[t] || 0
  const was = base.dirs?.[t] ?? 0
  if (now > was) regressions.push({ target: t, was, now, delta: now - was })
}

console.log(`[style-tokens] 硬编码色值合计 ${total}（基线 ${base.total}）`)
if (regressions.length === 0) {
  const improved = total < (base.total || 0) ? `，较基线减少 ${(base.total || 0) - total} 处` : ''
  console.log(`[style-tokens] PASS：无新增硬编码色值${improved}`)
  process.exit(0)
}

console.log('[style-tokens] FAIL：以下目录新增了硬编码色值（AGENTS.md §7 禁止）：')
for (const r of regressions) {
  console.log(`  - ${r.target}：${r.was} -> ${r.now}（+${r.delta}）`)
}
console.log('[style-tokens] 请改用 Element Plus 官方 --el-* 变量或 Tailwind 布局原语；')
console.log('[style-tokens] 若确为合理新增（如第三方集成），先与维护者确认，勿直接跑 --update 抹平。')
process.exit(1)
