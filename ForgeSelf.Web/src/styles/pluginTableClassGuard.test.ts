import { readFileSync, readdirSync, statSync } from 'node:fs'
import path from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * PC-026 G2 守卫：插件 `<table>` 不得使用宿主 Tailwind 的「显示方式」工具类。
 *
 * 背景（2026-11-06 用户输入3）：DbAdmin 结果表用 `class="grid"`，与宿主全局 `.grid{display:grid}`
 * 同名。表格被改成 display:grid 后，thead/tbody 变成独立 grid 项，列宽各自计算 →
 * 表头与内容逐列错位（实测 name 列表头 left 318.8 / 数据 left 507.6，错位 188.8px）。
 * 插件是独立预编译产物，但仍挂在宿主文档里，宿主 Tailwind 全局类会命中插件 DOM。
 *
 * 守卫：枚举全部插件 web/src 目录下的 .vue 文件，检查每个 `<table>` 标签的静态 class；若含
 * `grid / flex / block / inline / inline-block / inline-flex / contents / hidden` 即红灯。
 * 这是类级（所有插件）静态守卫；运行态另有 DbAdmin e2e 断言 display === 'table' + 列左边界对齐。
 */

const PLUGINS_ROOT = path.resolve(process.cwd(), '..', 'Plugins')
/** 宿主 Tailwind 中会改变 display 的工具类（作用于 table 会破坏表格布局） */
const DISPLAY_UTILITY_CLASSES = new Set([
  'grid',
  'flex',
  'inline-flex',
  'block',
  'inline',
  'inline-block',
  'contents',
  'hidden',
])

function walkVue(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = path.join(dir, name)
    const st = statSync(full)
    if (st.isDirectory()) {
      if (name === 'node_modules' || name === 'dist') continue
      walkVue(full, out)
    } else if (name.endsWith('.vue')) {
      out.push(full)
    }
  }
  return out
}

function vueFiles(): string[] {
  const files: string[] = []
  for (const plugin of readdirSync(PLUGINS_ROOT)) {
    const src = path.join(PLUGINS_ROOT, plugin, 'web', 'src')
    try {
      if (statSync(src).isDirectory()) walkVue(src, files)
    } catch {
      // 无 web/src 的纯后端插件跳过
    }
  }
  return files
}

describe('插件 table 标签与宿主 Tailwind 类名冲突守卫（PC-026）', () => {
  it('所有插件 <table> 的静态 class 不得使用显示方式工具类', () => {
    const hits: string[] = []
    for (const file of vueFiles()) {
      // 先剥模板注释，避免注释里的示例代码误报
      const text = readFileSync(file, 'utf8').replace(/<!--[\s\S]*?-->/g, '')
      for (const tag of text.matchAll(/<table\b[^>]*>/gi)) {
        const classMatch = tag[0].match(/\bclass\s*=\s*["']([^"']*)["']/i)
        const classes = (classMatch?.[1] ?? '').split(/\s+/).filter(Boolean)
        const bad = classes.filter((c) => DISPLAY_UTILITY_CLASSES.has(c))
        if (bad.length > 0) {
          hits.push(`${path.relative(PLUGINS_ROOT, file)}: <table class="${classMatch?.[1]}"> 命中 ${bad.join(',')}`)
        }
      }
    }
    expect(hits, `插件 <table> 使用了宿主 Tailwind 显示工具类，会破坏表格列布局：\n${hits.join('\n')}`).toEqual([])
  })
})
