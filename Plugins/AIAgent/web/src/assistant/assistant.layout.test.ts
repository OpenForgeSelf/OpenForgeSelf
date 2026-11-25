import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

/**
 * 智能体助手页对话列布局契约。
 * 锁两件事：有消息时从顶部往下排；对话列铺满中间栏，输入框单独限宽并居中。
 * 扫的是去掉块注释后的样式表，说明文字里提到旧写法不会误伤。
 */
const cssPath = join(dirname(fileURLToPath(import.meta.url)), 'assistant.css')
const plain = readFileSync(cssPath, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '')

function bodies(exactSelector: string): string[] {
  return [...plain.matchAll(/([^{}]+)\{([^{}]*)\}/g)]
    .filter((match) => match[1].split(',').some((part) => part.trim() === exactSelector))
    .map((match) => match[2])
}

function decl(body: string, prop: string): string | undefined {
  const matched = body.match(new RegExp(`(?:^|;)\\s*${prop}\\s*:\\s*([^;]+)`))
  return matched?.[1].trim()
}

describe('assistant transcript column', () => {
  it('starts messages at the top for both short threads and an open rail', () => {
    expect(plain).not.toMatch(/margin-top:\s*auto/)
    expect(plain).not.toMatch(/\.aa-stage\.has-rail/)
  })

  it('fills the middle column while the composer stays narrower and centered', () => {
    const col = bodies('.aa-col')
    expect(col).toHaveLength(1)
    expect(decl(col[0], 'width')).toBe('100%')
    expect(decl(col[0], 'margin-inline')).toBe('auto')
    expect(decl(col[0], 'flex-shrink')).toBe('0')
    expect(decl(col[0], 'margin-top')).toBeUndefined()

    const dock = bodies('.aa-dock-inner')
    expect(dock).toHaveLength(1)
    expect(decl(dock[0], 'width')).toBe('min(720px, 100%)')
    expect(decl(dock[0], 'margin')).toBe('0 auto')
  })

  it('keeps the empty stage vertically centered', () => {
    const empty = bodies('.aa-stage.is-empty')
    expect(empty.join('\n')).toMatch(/justify-content:\s*center/)
  })
})
