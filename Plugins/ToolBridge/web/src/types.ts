/** 与后端 Models 对应的界面类型（后端出参 camelCase）。 */

export interface ParsedCall {
  tool: string
  rawName: string
  args: Record<string, unknown>
  via: string
  start: number
  fragment: string
}

export interface UnknownCall {
  rawName: string
  fragment: string
  reason: string
  suggestion?: string | null
}

export interface UnparsedFragment {
  fragment: string
  reason: string
}

export interface ParseStats {
  recognized: number
  unknown: number
  unparsed: number
  executed?: number
  rejected?: number
  durationMs?: number
}

export interface ParseResult {
  calls: ParsedCall[]
  unknown: UnknownCall[]
  unparsed: UnparsedFragment[]
  stats: ParseStats
}

export interface ToolResult {
  tool: string
  rawName?: string
  ok: boolean
  args?: Record<string, unknown>
  result?: Record<string, unknown> | null
  error?: string | null
  reason?: string | null
  truncated?: boolean
  originalBytes?: number | null
  durationMs?: number
}

export interface TurnResponse {
  turnId: string
  calls: ParsedCall[]
  unknown: UnknownCall[]
  unparsed: UnparsedFragment[]
  results: ToolResult[]
  resultTextJson: string
  resultTextPlain: string
  stats: ParseStats
  ledgerError?: string | null
}

export interface ExecuteResponse {
  results: ToolResult[]
  resultTextJson: string
  resultTextPlain: string
  executed: number
  rejected: number
  durationMs: number
}

export interface PromptData {
  text: string
  specVersion: string
  tools: { name: string; description: string; parametersSchema?: unknown; aliases?: string[]; required?: string[] }[]
}

export interface WorkspaceInfo {
  root: string
  exists: boolean
  defaultRoot: string
  source: string
  dangerous: boolean
}

export interface TurnSummary {
  turnId: string
  createdAt: string
  stats: ParseStats
  preview: string
  corrupt?: boolean
}

export interface TurnList {
  items: TurnSummary[]
  total: number
  ledgerDirectory: string
  note: string
}
