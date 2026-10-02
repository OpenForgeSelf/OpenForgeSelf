/**
 * 交付与接入页纯逻辑（FR13 / AC18 / AC21 的**唯一真源**）。
 *
 * 三件事都不碰 DOM、不发请求，纯粹由后端给的事实算出用户要复制的文本：
 * ① `mcpEndpoint` / `mcpSnippet`：把「MCP 网关地址」与「客户端配置片段」拼出来；
 * ② `byteLength` / `reviewTooLarge`：试审查的 200KB 上限按 UTF-8 字节算，客户端先拦；
 * ③ `DELIVERY_EXPORT_FORMATS` / `deliveryFormats`：交付首选导出格式，且只列后端**真声明**的。
 *
 * SECURITY（硬约束）：配置片段里**永不出现真实令牌**——只写占位符 `TOKEN_PLACEHOLDER`，
 * 连掩码都不带。凭据由用户自己在客户端填，前端只负责"告诉它该往哪填"。
 */
import type { McpConfig } from '../api'

/** 客户端配置片段里的令牌占位符（真实令牌绝不进片段，见文件头 SECURITY） */
export const TOKEN_PLACEHOLDER = '<你的令牌>'

/** 试审查入力上限（200 KB，按 UTF-8 字节计；与后端 ReviewService 口径一致） */
export const REVIEW_LIMIT_BYTES = 200 * 1024

/**
 * 交付首选导出格式（顺序即界面展示顺序）。
 * 只放"给人看/给人用"的交付物，调色板类中间格式（scss/less 等）不占交付首屏。
 */
export const DELIVERY_EXPORT_FORMATS: readonly string[] = ['design-md', 'css', 'tailwind', 'dtcg', 'bundle']

/** `mcpEndpoint` 的最小入参（只认 `listenUrl`，不依赖整个 McpConfig，便于测试） */
export type McpEndpointInput = Pick<McpConfig, 'listenUrl'>

/** `mcpSnippet` 的最小入参（地址 + 是否已配置令牌） */
export type McpSnippetInput = Pick<McpConfig, 'listenUrl' | 'hasToken'>

/**
 * MCP 网关地址：由后端 `listenUrl` 加 `/mcp` 路径推导（不另拼一份地址）。
 * 末尾多余斜杠先规整；地址为空（未运行/未配置）时回空串——宁可不给，不编造一个假地址。
 */
export function mcpEndpoint(input: McpEndpointInput): string {
  const base = (input.listenUrl ?? '').trim().replace(/\/+$/, '')
  return base ? `${base}/mcp` : ''
}

/**
 * 生成可粘贴到 MCP 客户端的配置片段（合法 JSON）。
 * `hasToken=true` 时补 `Authorization: Bearer <你的令牌>`，值恒为占位符；
 * `hasToken=false` 时不写 `headers`（缺凭据就不编造凭据）。
 */
export function mcpSnippet(input: McpSnippetInput): string {
  const url = mcpEndpoint(input)
  const server: { url: string; headers?: Record<string, string> } = { url }
  if (input.hasToken) server.headers = { Authorization: `Bearer ${TOKEN_PLACEHOLDER}` }
  return JSON.stringify({ mcpServers: { 'forge-design': server } }, null, 2)
}

/** 文本的 UTF-8 字节数（中文一字 3 字节；用 TextEncoder 保证与后端一致） */
export function byteLength(text: string): number {
  return new TextEncoder().encode(text).length
}

/** 试审查文本是否超过 200KB 上限（客户端先拦，不发一个注定被拒的请求） */
export function reviewTooLarge(text: string): boolean {
  return byteLength(text) > REVIEW_LIMIT_BYTES
}

/**
 * 交付格式清单：把首选清单按后端 `meta.exportFormats` 真声明过滤一遍。
 * 后端没声明的格式一律不出现——否则界面会给出"能下载"的假象。
 */
export function deliveryFormats(available: readonly string[]): string[] {
  return DELIVERY_EXPORT_FORMATS.filter((f) => available.includes(f))
}