/**
 * 交付与接入页纯逻辑单测（FR13 / AC18 / AC21）。
 *
 * 重点守三件事：
 * ① 网关地址必须由 `listenUrl` 推导（`+ '/mcp'`），不另拼一份；
 * ② 配置片段里**永不出现真实令牌**（SECURITY：占位符 `<你的令牌>`，明文绝不渲染）；
 * ③ 试审查的 200KB 上限按**字节**算（UTF-8），客户端先拦、不发请求。
 */
import { describe, expect, it } from 'vitest'
import {
  DELIVERY_EXPORT_FORMATS,
  REVIEW_LIMIT_BYTES,
  TOKEN_PLACEHOLDER,
  byteLength,
  deliveryFormats,
  mcpEndpoint,
  mcpSnippet,
  reviewTooLarge,
} from './snippets'

const CFG = { listenUrl: 'http://127.0.0.1:51888', hasToken: true, isRunning: true, tokenMasked: 'abcd…wxyz' }

describe('mcpEndpoint', () => {
  it('网关地址 = listenUrl + /mcp（AC18 判据）', () => {
    expect(mcpEndpoint(CFG)).toBe('http://127.0.0.1:51888/mcp')
  })

  it('末尾多余斜杠被规整，空地址回空串（不编造）', () => {
    expect(mcpEndpoint({ listenUrl: 'http://x:1/' })).toBe('http://x:1/mcp')
    expect(mcpEndpoint({ listenUrl: '  ' })).toBe('')
  })
})

describe('mcpSnippet', () => {
  it('是合法 JSON，url 指向 /mcp；hasToken 时带 Authorization 且值为占位符', () => {
    const doc = JSON.parse(mcpSnippet(CFG)) as {
      mcpServers: Record<string, { url: string; headers?: Record<string, string> }>
    }
    const server = doc.mcpServers['forge-design']
    expect(server.url).toBe('http://127.0.0.1:51888/mcp')
    expect(server.headers?.Authorization).toBe(`Bearer ${TOKEN_PLACEHOLDER}`)
  })

  it('片段里绝不出现真实令牌（即便 tokenMasked 传入也不当凭据用）', () => {
    const snippet = mcpSnippet(CFG)
    expect(snippet).toContain(TOKEN_PLACEHOLDER)
    expect(snippet).not.toContain('abcd') // 掩码都不带，更别说明文
  })

  it('无令牌时不编造 Authorization 头', () => {
    const doc = JSON.parse(mcpSnippet({ ...CFG, hasToken: false })) as {
      mcpServers: Record<string, { headers?: Record<string, string> }>
    }
    expect(doc.mcpServers['forge-design'].headers).toBeUndefined()
  })
})

describe('byteLength / reviewTooLarge', () => {
  it('按 UTF-8 字节数算：中文一字 3 字节', () => {
    expect(byteLength('abc')).toBe(3)
    expect(byteLength('中')).toBe(3)
    expect(byteLength('中a')).toBe(4)
  })

  it('恰好 200KB 放行，超 1 字节拦截（AC21）', () => {
    expect(reviewTooLarge('a'.repeat(REVIEW_LIMIT_BYTES))).toBe(false)
    expect(reviewTooLarge('a'.repeat(REVIEW_LIMIT_BYTES + 1))).toBe(true)
  })

  it('空文本不拦', () => {
    expect(reviewTooLarge('')).toBe(false)
  })
})

describe('deliveryFormats', () => {
  it('只列后端真声明且属于交付首选的格式，顺序稳定（不另立词表）', () => {
    expect(deliveryFormats(['css', 'dtcg', 'tailwind', 'bundle', 'design-md', 'scss', 'less'])).toEqual([
      'design-md',
      'css',
      'tailwind',
      'dtcg',
      'bundle',
    ])
  })

  it('后端没声明的格式不出现（不给"能下载"的假象）', () => {
    expect(deliveryFormats(['css'])).toEqual(['css'])
    expect(deliveryFormats([])).toEqual([])
  })

  it('首选清单本身非空且与后端枚举口径一致（防词表被改空）', () => {
    expect(DELIVERY_EXPORT_FORMATS.length).toBe(5)
    expect(deliveryFormats(DELIVERY_EXPORT_FORMATS)).toEqual([...DELIVERY_EXPORT_FORMATS])
  })
})