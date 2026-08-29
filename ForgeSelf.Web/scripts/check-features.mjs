// 功能清单一致性校验（单一真源防护）
//
// 作用：防止「功能已在代码中实现，却没登记进功能列表」的脱节。
//   - 幻影检查（硬失败）：features.ts 中登记的 signals 必须能在代码中找到对应产物
//     （views -> src/views/<key>View.vue；controllers -> Controllers/<key>Controller.cs
//      或 Controllers/UnifiedAI/<key>Controller.cs；plugins -> Plugins/<key>/ 目录）
//   - 孤儿检查（硬失败）：代码中的控制器 / 插件 / 功能页，必须被某个功能的 signals 覆盖
//
// 新增功能：先在 src/data/features.ts 登记并填 signals；本脚本会随 CI 自动校验。
// 运行：node scripts/check-features.mjs  （或 pnpm run check:features）

import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { join, resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const FE = resolve(__dirname, '..') // ForgeSelf.Web
const BE = resolve(__dirname, '..', '..', 'ForgeSelf.Api')

const featuresFile = join(FE, 'src', 'data', 'features.ts')
const viewsDir = join(FE, 'src', 'views')
const controllersDir = join(BE, 'Controllers')
const pluginsDir = join(BE, 'Plugins')

// 基础设施类产物：存在但不作为用户功能，不计入孤儿检查
const CONTROLLER_DENY = new Set([
  'Health', // 健康检查
  'UsageStats', // 用量统计（基础设施）
  'OpenAIResponses', // 统一 AI 网关内部
  'OpenAIChat', // 统一 AI 网关内部
  'AnthropicMessages', // 统一 AI 网关内部
  'Models', // 统一 AI 网关内部（UnifiedAI/ModelsController.cs）
  'AgentChat', // 统一 AI 网关内部（UnifiedAI/AgentChatController.cs，Agent Framework 实验通道）
  'AgentDemoTools', // Agent Framework 实验性演示工具（非控制器）
  'AgentStreamTranslator', // Agent Framework 实验性流翻译器（非控制器）
])
const PLUGIN_DENY = new Set(['Abstractions', 'Services', 'SamplePlugin'])
const VIEW_DENY = new Set(['HomeView', 'AllFeaturesView', 'PluginPage'])

const errors = []
const info = []

function fail(msg) {
  errors.push(msg)
}
function note(msg) {
  info.push(msg)
}

// ---- 解析 features.ts ----
if (!existsSync(featuresFile)) {
  fail(`找不到功能清单文件：${featuresFile}`)
  finish()
}

const src = readFileSync(featuresFile, 'utf8')
// 每个功能对象：取 id，再取其 signals（signals 必须置于对象末位）
const blockRe = /id:\s*'([^']+)'[\s\S]*?signals:\s*\{([\s\S]*?)\}/g
const features = []
let m
while ((m = blockRe.exec(src)) !== null) {
  const id = m[1]
  const sigInner = m[2]
  const views = parseArr(sigInner, 'views')
  const controllers = parseArr(sigInner, 'controllers')
  const plugins = parseArr(sigInner, 'plugins')
  features.push({ id, views, controllers, plugins })
}

function parseArr(inner, key) {
  const km = inner.match(new RegExp(key + '\\s*:\\s*\\[([^\\]]*)\\]'))
  if (!km) return []
  return [...km[1].matchAll(/'([^']+)'/g)].map((x) => x[1])
}

if (features.length === 0) {
  fail('未能从 features.ts 解析出任何功能，请检查文件格式（signals 需置于对象末位）。')
  finish()
}

// 汇总所有已登记的代码产物键
const listedViews = new Set()
const listedControllers = new Set()
const listedPlugins = new Set()
for (const f of features) {
  f.views.forEach((v) => listedViews.add(v))
  f.controllers.forEach((c) => listedControllers.add(c))
  f.plugins.forEach((p) => listedPlugins.add(p))
}

// ---- 幻影检查：登记的功能必须有代码产物 ----
for (const f of features) {
  for (const v of f.views) {
    // 视图文件名本身不统一（有的带 View 后缀，有的不带），signals.views 写精确 stem
    const p = join(viewsDir, `${v}.vue`)
    if (!existsSync(p)) fail(`功能「${f.id}」登记的视图信号 '${v}' 不存在：${p}`)
  }
  for (const c of f.controllers) {
    const direct = join(controllersDir, `${c}Controller.cs`)
    const unified = join(controllersDir, 'UnifiedAI', `${c}Controller.cs`)
    if (!existsSync(direct) && !existsSync(unified))
      fail(`功能「${f.id}」登记的控制器信号 '${c}' 不存在：${direct}`)
  }
  for (const p of f.plugins) {
    const dir = join(pluginsDir, p)
    if (!existsSync(dir) || !statSync(dir).isDirectory())
      fail(`功能「${f.id}」登记的插件信号 '${p}' 不存在：${dir}`)
  }
}

// ---- 孤儿检查：代码产物必须被某个功能覆盖 ----
// 视图
if (existsSync(viewsDir)) {
  for (const file of readdirSync(viewsDir)) {
    if (!file.endsWith('.vue')) continue
    const key = file.replace(/\.vue$/, '')
    if (VIEW_DENY.has(key)) continue
    if (!listedViews.has(key))
      fail(`视图 ${file} 未在任何功能的 signals.views 中登记（孤儿）。`)
  }
}
// 控制器
if (existsSync(controllersDir)) {
  scanControllers(controllersDir)
}
// 插件目录
if (existsSync(pluginsDir)) {
  for (const entry of readdirSync(pluginsDir)) {
    const dir = join(pluginsDir, entry)
    if (!statSync(dir).isDirectory()) continue // 跳过 .cs / .md 等文件
    if (PLUGIN_DENY.has(entry)) continue
    if (!listedPlugins.has(entry))
      fail(`插件目录 ${entry} 未在任何功能的 signals.plugins 中登记（孤儿）。`)
  }
}

function scanControllers(dir) {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry)
    if (statSync(full).isDirectory()) {
      scanControllers(full) // 递归（如 UnifiedAI/）
      continue
    }
    if (!entry.endsWith('.cs')) continue
    // key 归一：先去 .cs 再去 Controller 后缀，使非控制器 .cs 文件（如 AgentDemoTools.cs）也能正确匹配 DENY/登记
    const key = entry.replace(/\.cs$/, '').replace(/Controller$/, '')
    if (CONTROLLER_DENY.has(key)) continue
    if (!listedControllers.has(key))
      fail(`控制器 ${entry} 未在任何功能的 signals.controllers 中登记（孤儿）。`)
  }
}

note(`功能清单共登记 ${features.length} 项，视图信号 ${listedViews.size} 个、控制器信号 ${listedControllers.size} 个、插件信号 ${listedPlugins.size} 个。`)
finish()

function finish() {
  for (const i of info) console.log(`[info] ${i}`)
  if (errors.length > 0) {
    console.error('\n❌ 功能清单校验失败：')
    for (const e of errors) console.error(`  - ${e}`)
    console.error(`\n共 ${errors.length} 处不一致。请同步更新 src/data/features.ts 或对应代码。`)
    process.exit(1)
  }
  console.log(`\n✅ 功能清单校验通过（${features.length} 项，代码与清单一致）。`)
  process.exit(0)
}
