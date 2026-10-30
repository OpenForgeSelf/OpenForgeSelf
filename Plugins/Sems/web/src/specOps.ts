/**
 * 规格界面的纯逻辑：表单校验、请求体、列表三态、写请求排队。
 * 不依赖 Vue / fetch，供 vitest 直接锁住「空标题不发请求体」「追加版本默认沿用验收标准」。
 */

export interface CriterionInput {
  id: string
  text: string
}

export type CriteriaResult =
  | { ok: true; criteria?: CriterionInput[] }
  | { ok: false; message: string }

export type BodyResult<T> = { ok: true; body: T } | { ok: false; message: string }

export interface CreateSpecBody {
  title: string
  content: string
  author?: string
  note?: string
  criteria?: CriterionInput[]
}

export interface VersionSpecBody {
  content: string
  author?: string
  note?: string
  criteria?: CriterionInput[]
}

export interface LinkSpecBody {
  version: number
  kind: string
  ref: string
}

export interface CaseSpecBody {
  version: number
  criterionId: string
  case: string
}

export type ListPhase = 'loading' | 'error' | 'empty' | 'list'

/**
 * 空文本 = 不传 criteria。
 * 非空时每行「id 文案」；id 与文案都必填，id 不区分大小写唯一。
 */
export function parseCriteria(raw: string): CriteriaResult {
  const lines = raw.split(/\r?\n/).map((line) => line.trim()).filter(Boolean)
  if (lines.length === 0) return { ok: true }

  const criteria: CriterionInput[] = []
  const seen = new Set<string>()
  for (const line of lines) {
    const match = /^(\S+)\s+(.+)$/.exec(line)
    if (!match) return { ok: false, message: 'criteria 每条的 id 与 text 必填' }
    const id = match[1]
    const text = match[2].trim()
    if (!text) return { ok: false, message: 'criteria 每条的 id 与 text 必填' }
    const key = id.toLowerCase()
    if (seen.has(key)) return { ok: false, message: `criteria 的 id 重复：${id}` }
    seen.add(key)
    criteria.push({ id, text })
  }
  return { ok: true, criteria }
}

function optionalText(value: string): string | undefined {
  const trimmed = value.trim()
  return trimmed ? trimmed : undefined
}

export function buildCreateBody(input: {
  title: string
  content: string
  author: string
  note: string
  criteriaText: string
}): BodyResult<CreateSpecBody> {
  if (!input.title.trim()) return { ok: false, message: 'title 必填' }
  if (!input.content.trim()) return { ok: false, message: 'content 必填' }
  const criteria = parseCriteria(input.criteriaText)
  if (!criteria.ok) return criteria
  return {
    ok: true,
    body: {
      title: input.title.trim(),
      content: input.content,
      author: optionalText(input.author),
      note: optionalText(input.note),
      criteria: criteria.criteria,
    },
  }
}

/**
 * keepPrevious=true 时不带 criteria，后端沿用上一版。
 * keepPrevious=false 且文本为空时发送空数组，表示这一版不要验收标准。
 */
export function buildVersionBody(input: {
  content: string
  author: string
  note: string
  criteriaText: string
  keepPrevious: boolean
}): BodyResult<VersionSpecBody> {
  if (!input.content.trim()) return { ok: false, message: 'content 必填' }
  const body: VersionSpecBody = {
    content: input.content,
    author: optionalText(input.author),
    note: optionalText(input.note),
  }
  if (!input.keepPrevious) {
    const criteria = parseCriteria(input.criteriaText)
    if (!criteria.ok) return criteria
    body.criteria = criteria.criteria ?? []
  }
  return { ok: true, body }
}

export function buildLinkBody(input: {
  version: string
  kind: string
  ref: string
}): BodyResult<LinkSpecBody> {
  const version = Number(input.version)
  if (!Number.isInteger(version) || version < 1) return { ok: false, message: 'version 必须是正整数' }
  if (!input.kind.trim() || !input.ref.trim()) return { ok: false, message: 'kind 与 ref 必填' }
  return { ok: true, body: { version, kind: input.kind.trim(), ref: input.ref.trim() } }
}

export function buildCaseBody(input: {
  version: string
  criterionId: string
  caseRef: string
}): BodyResult<CaseSpecBody> {
  const version = Number(input.version)
  if (!Number.isInteger(version) || version < 1) return { ok: false, message: 'version 必须是正整数' }
  if (!input.criterionId.trim() || !input.caseRef.trim()) {
    return { ok: false, message: 'criterionId 与 case 必填' }
  }
  return {
    ok: true,
    body: {
      version,
      criterionId: input.criterionId.trim(),
      case: input.caseRef.trim(),
    },
  }
}

/** 状态只写登记条数。是否跑过由面板说明承担，状态文案本身不写「已通过」。 */
export function coveragePhrase(linked: boolean, cases: readonly string[]): string {
  if (!linked) return '未登记'
  return `已登记 ${cases.length} 条`
}

/**
 * 首载才占满三态。已有列表时后台失败仍留在 list，避免把展开中的规格拆掉。
 */
export function listPhase(loading: boolean, error: string, count: number): ListPhase {
  if (count > 0) return 'list'
  if (loading) return 'loading'
  if (error) return 'error'
  return 'empty'
}

/** 写请求按发起顺序执行。前一个失败也不丢掉后面的写。 */
export function createWriteQueue(): <T>(run: () => Promise<T>) => Promise<T> {
  let chain: Promise<unknown> = Promise.resolve()
  return function enqueue<T>(run: () => Promise<T>): Promise<T> {
    const next = chain.then(run, run)
    chain = next.then(
      () => undefined,
      () => undefined,
    )
    return next
  }
}