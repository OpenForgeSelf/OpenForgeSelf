/**
 * 术语词典单测 + 使用守卫（AC9）。
 *
 * 判据：① 每个词条 plain/pro 非空且不相等；② 开关默认大白话、切换后持久化；
 * ③ 新模式源码里每个 `term('key')` 字面量都必须在词典里有词条（漏词条是红，不是静默原样返回）。
 * 读盘写法同 classes.test.ts：源码从磁盘读，不依赖 vitest 的 import stub。
 */
import path from 'node:path'
import { readdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { GLOSSARY, proTerms, term, toggleProTerms } from './glossary'

function toFsPath(url: string): string {
  const u = new URL(url)
  if (u.protocol === 'file:') return fileURLToPath(u)
  return decodeURIComponent(u.pathname).replace(/^\/@fs/, '').replace(/^\/([A-Za-z]:)/, '$1')
}

const SRC_DIR = path.resolve(path.dirname(toFsPath(import.meta.url)), '..')

function walk(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const full = path.join(dir, e.name)
    if (e.isDirectory()) return full.includes('node_modules') ? [] : walk(full)
    return e.isFile() ? [full] : []
  })
}

/** 界面源码（排除单测自身）：路径 → 文本 */
function uiSources(): { file: string; source: string }[] {
  return walk(SRC_DIR)
    .filter((f) => /\.(ts|vue)$/.test(f) && !f.endsWith('.test.ts'))
    .map((f) => ({ file: path.relative(SRC_DIR, f).replace(/\\/g, '/'), source: readFileSync(f, 'utf8') }))
}

describe('词条本身', () => {
  it('每个词条 plain 与 pro 都非空且互不相等', () => {
    for (const [key, entry] of Object.entries(GLOSSARY)) {
      expect(entry.plain.trim(), `词条 ${key} 的 plain 为空`).not.toBe('')
      expect(entry.pro.trim(), `词条 ${key} 的 pro 为空`).not.toBe('')
      expect(entry.plain, `词条 ${key} 的 plain 与 pro 不应相同`).not.toBe(entry.pro)
    }
  })
})

describe('开关与取值', () => {
  it('默认大白话（proTerms 初始为 false）', () => {
    expect(proTerms.value).toBe(false)
  })

  it('proTerms=true 时 term() 返回专业词，false 返回大白话', () => {
    const prev = proTerms.value
    try {
      proTerms.value = false
      expect(term('token')).toBe('设计变量')
      proTerms.value = true
      expect(term('token')).toBe('令牌 Token')
    } finally {
      proTerms.value = prev
    }
  })

  it('toggleProTerms 切换并持久化', () => {
    const prev = proTerms.value
    try {
      proTerms.value = false
      toggleProTerms()
      expect(proTerms.value).toBe(true)
      expect(localStorage.getItem('ds.pro-terms')).toBe('1')
      toggleProTerms()
      expect(proTerms.value).toBe(false)
      expect(localStorage.getItem('ds.pro-terms')).toBe('0')
    } finally {
      proTerms.value = prev
    }
  })

  it('未知 key 原样返回（不炸界面，守卫负责揪漏词条）', () => {
    expect(term('no-such-term')).toBe('no-such-term')
  })
})

describe('使用守卫：源码里每个 term() 字面量都在词典里', () => {
  const files = uiSources()

  it('扫到了界面源码（一条没扫到 = 守卫本身失效）', () => {
    expect(files.length).toBeGreaterThan(10)
  })

  it('term("…") / term(\'…\') 的 key 都在 GLOSSARY 里', () => {
    const missing: string[] = []
    for (const { file, source } of files) {
      for (const m of source.matchAll(/\bterm\(\s*['"]([^'"]+)['"]\s*\)/g)) {
        const key = m[1]
        if (!(key in GLOSSARY)) missing.push(`${file} → term('${key}')`)
      }
    }
    expect(missing, `词典缺失词条（应为"大白话+专业词"两条）：\n${missing.join('\n')}`).toEqual([])
  })

  it('反向探针：往临时串里造 term(\'none\') 守卫必须红', () => {
    const fake = `<template>${'{{ term(\'no-such\') }}'}</template>`
    const keys = [...fake.matchAll(/\bterm\(\s*['"]([^'"]+)['"]\s*\)/g)].map((m) => m[1])
    expect(keys).toContain('no-such')
    expect(keys.every((k) => k in GLOSSARY)).toBe(false)
  })
})
