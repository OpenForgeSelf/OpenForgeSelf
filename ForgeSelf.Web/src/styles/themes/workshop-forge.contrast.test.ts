import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * 主题 token 明暗语义守卫（PC-025 G2）。
 *
 * 背景（2026-11-06 用户输入1）：暗色下 DbAdmin 选中连接行背景 `--el-color-primary-light-8`
 * 被定义成亮金 #FDE68A，文字 `--el-text-color-primary` 为 #F4F0E7，实测对比度 1.10:1。
 * 根因是 `html.dark` 的 `--el-color-primary-light-3..9` 比主色更亮，与 Element Plus 暗色语义
 * （light-N = 主色与黑混合、N 越大越暗的**背景级** token）相反。
 *
 * 本测试直接解析主题真源文件，枚举两套主题的 token 并断言：
 * 1) 暗色 light-N 必须逐级变暗且都低于主色亮度；
 * 2) primary 浅底与常用前景的 WCAG 对比度 ≥4.5；
 * 3) 浅色选中行同样 ≥4.5（防修暗色时压坏浅色）。
 *
 * 注意：文件必须以 `.test.ts` 结尾——`vitest.config.ts` 的 exclude 含 `**\/*.spec.ts`
 *（`.spec.ts` 留给 Playwright e2e），命名为 spec 会被静默跳过。
 */

// vitest 由 Vite 转译，import.meta.url 是 http(s) 形态，不能直接交给 readFileSync；
// 兼容「从 ForgeSelf.Web 运行」与「从仓库根运行」两种 cwd。
const CSS_PATH = [
  path.resolve(process.cwd(), 'src/styles/themes/workshop-forge.css'),
  path.resolve(process.cwd(), 'ForgeSelf.Web/src/styles/themes/workshop-forge.css'),
].find((candidate) => existsSync(candidate))
if (!CSS_PATH) throw new Error('未找到 workshop-forge.css（cwd 应为 ForgeSelf.Web 或仓库根）')
const css = readFileSync(CSS_PATH, 'utf8')

function extractBlock(selector: string): string {
  const selectorIndex = css.indexOf(`${selector} {`)
  if (selectorIndex < 0) {
    throw new Error(`主题文件缺少选择器 ${selector}（解析失败即红灯，不允许静默跳过）`)
  }
  const open = css.indexOf('{', selectorIndex)
  let depth = 0
  for (let i = open; i < css.length; i++) {
    if (css[i] === '{') depth++
    else if (css[i] === '}') {
      depth--
      if (depth === 0) return css.slice(open + 1, i)
    }
  }
  throw new Error(`主题文件 ${selector} 块未闭合`)
}

function parseTokens(block: string): Record<string, string> {
  const tokens: Record<string, string> = {}
  for (const match of block.matchAll(/(--[a-zA-Z][\w-]*)\s*:\s*([^;]+);/g)) {
    tokens[match[1]] = match[2].trim()
  }
  return tokens
}

const lightTokens = parseTokens(extractBlock(':root'))
const darkTokens = parseTokens(extractBlock('html.dark'))

function hexToRgb(hex: string): [number, number, number] {
  const value = hex.trim().toLowerCase()
  if (!/^#[0-9a-f]{3,8}$/.test(value)) throw new Error(`不是 hex 色值: ${hex}`)
  if (value.length === 4) {
    return [
      parseInt(value[1] + value[1], 16),
      parseInt(value[2] + value[2], 16),
      parseInt(value[3] + value[3], 16),
    ]
  }
  return [
    parseInt(value.slice(1, 3), 16),
    parseInt(value.slice(3, 5), 16),
    parseInt(value.slice(5, 7), 16),
  ]
}

function luminance(hex: string): number {
  const channels = hexToRgb(hex).map((channel) => {
    const ratio = channel / 255
    return ratio <= 0.03928 ? ratio / 12.92 : ((ratio + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2]
}

function contrast(a: string, b: string): number {
  const [lighter, darker] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (lighter + 0.05) / (darker + 0.05)
}

function token(tokens: Record<string, string>, name: string): string {
  const value = tokens[name]
  if (!value) throw new Error(`主题块缺少 token ${name}`)
  return value
}

describe('workshop-forge 主题 token 明暗语义', () => {
  it('阳性对照：两套主题都解析到 primary 阶梯', () => {
    for (const name of ['--el-color-primary', '--el-color-primary-light-3', '--el-color-primary-light-9']) {
      expect(token(lightTokens, name), `light ${name}`).toMatch(/^#[0-9a-fA-F]{3,8}$/)
      expect(token(darkTokens, name), `dark ${name}`).toMatch(/^#[0-9a-fA-F]{3,8}$/)
    }
  })

  it('暗色 primary light-3/5/7/8/9 必须逐级更暗且都低于主色亮度', () => {
    const base = luminance(token(darkTokens, '--el-color-primary'))
    const steps = ['3', '5', '7', '8', '9'].map((n) => {
      const name = `--el-color-primary-light-${n}`
      const value = token(darkTokens, name)
      const lum = luminance(value)
      expect(lum, `${name}=${value} 必须比 --el-color-primary 更暗（当前 ${lum.toFixed(4)} >= ${base.toFixed(4)}）`).toBeLessThan(base)
      return { name, value, lum }
    })

    for (let i = 1; i < steps.length; i++) {
      expect(
        steps[i].lum,
        `${steps[i].name}=${steps[i].value} 必须比 ${steps[i - 1].name}=${steps[i - 1].value} 更暗（N 越大越暗）`,
      ).toBeLessThan(steps[i - 1].lum)
    }
  })

  it('暗色选中行（light-8 底 + text-primary 字）对比度 ≥4.5', () => {
    const background = token(darkTokens, '--el-color-primary-light-8')
    const foreground = token(darkTokens, '--el-text-color-primary')
    const ratio = contrast(background, foreground)
    expect(ratio, `dark light-8 ${background} vs text-primary ${foreground} = ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(4.5)
  })

  it('暗色主色标签/横幅（light-9 底 + primary 字）对比度 ≥4.5', () => {
    const background = token(darkTokens, '--el-color-primary-light-9')
    const foreground = token(darkTokens, '--el-color-primary')
    const ratio = contrast(background, foreground)
    expect(ratio, `dark light-9 ${background} vs primary ${foreground} = ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(4.5)
  })

  it('浅色选中行（light-8 底 + text-primary 字）对比度 ≥4.5', () => {
    const background = token(lightTokens, '--el-color-primary-light-8')
    const foreground = token(lightTokens, '--el-text-color-primary')
    const ratio = contrast(background, foreground)
    expect(ratio, `light light-8 ${background} vs text-primary ${foreground} = ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(4.5)
  })
})
