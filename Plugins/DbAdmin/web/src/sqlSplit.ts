/**
 * 客户端多语句切分（FR-15 / BR-13~14）。
 * 语义镜像后端 `Plugins/DbAdmin/Services/SqlGuard.cs` 的 Strip + SplitStatements：
 * - 顶层 `;` 切分；跳过字符串、引号标识符、`--` 行注释、块注释、`$tag$` 美元引号；
 * - 仅当语句以 `CREATE [TEMP|TEMPORARY] TRIGGER …` 开头时，`BEGIN…END` 块内分号不切（CASE…END 单独配对）；
 * - 未闭合引号/注释 → 抛错（调用方显示红条且不发请求）。
 * 后端 `POST /query` 仍硬拒多语句（SqlGuard.cs），故由前端切分后逐条发送，不改契约。
 */

export class SqlSplitError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'SqlSplitError'
  }
}

export interface SqlStatement {
  /** 语句原文（保留用户写的引号/注释） */
  text: string
  /** 去掉注释与空白后是否还有内容（纯注释/空白片段为 false，被丢弃） */
  hasCode: boolean
}

function isAlnum(c: string): boolean {
  return /[A-Za-z0-9_]/.test(c)
}

function isWordStart(c: string): boolean {
  return /[A-Za-z_]/.test(c)
}

function isWordChar(c: string): boolean {
  return /[A-Za-z0-9_$]/.test(c)
}

/** `$tag$` 识别（镜像 SqlGuard.TryDollarTag）：`$` 前不得是字母数字下划线，`$1` 位置参数不算 */
function tryDollarTag(sql: string, i: number): string | null {
  if (i > 0 && isAlnum(sql[i - 1])) return null
  let j = i + 1
  while (j < sql.length && isWordChar(sql[j]) && sql[j] !== '$') j++
  if (j < sql.length && sql[j] === '$') {
    if (j > i + 1 && /[0-9]/.test(sql[i + 1])) return null
    return sql.slice(i, j + 1)
  }
  return null
}

/** 跳过引号段；双写闭引号视为转义；allowBackslash 时 `\x` 跳过两字符 */
function skipQuoted(sql: string, start: number, open: string, close: string, allowBackslash: boolean): number {
  let i = start + 1
  while (i < sql.length) {
    if (allowBackslash && sql[i] === '\\') { i += 2; continue }
    if (sql[i] === close) {
      if (i + 1 < sql.length && sql[i + 1] === close) { i += 2; continue }
      return i + 1
    }
    i++
  }
  throw new SqlSplitError(`引号 ${open} 未闭合`)
}

/** 按顶层 `;` 切分；返回原文片段与「是否还有有效内容」 */
export function splitStatements(sql: string): SqlStatement[] {
  const out: SqlStatement[] = []
  let segStart = 0
  /** 去注释后的内容（判断 hasCode） */
  let code = ''
  // TRIGGER 块状态（镜像 SplitStatements）
  let blockDepth = 0
  let caseDepth = 0
  let trigger = false
  let isCreate = false
  let wordIndex = 0
  let wordBuf = ''

  function flushWord() {
    if (!wordBuf) return
    const w = wordBuf.toUpperCase()
    if (wordIndex === 0) isCreate = w === 'CREATE'
    else if (isCreate && !trigger && wordIndex <= 2 && w === 'TRIGGER') trigger = true
    wordIndex++
    wordBuf = ''
    if (!trigger) return
    switch (w) {
      case 'CASE': caseDepth++; break
      case 'BEGIN': blockDepth++; break
      case 'END':
        if (caseDepth > 0) caseDepth--
        else if (blockDepth > 0) blockDepth--
        break
    }
  }

  function pushSegment(end: number) {
    flushWord()
    const text = sql.slice(segStart, end)
    out.push({ text, hasCode: code.trim().length > 0 })
    segStart = end + 1
    code = ''
    blockDepth = caseDepth = wordIndex = 0
    trigger = isCreate = false
  }

  let i = 0
  while (i < sql.length) {
    const c = sql[i]
    const n = i + 1 < sql.length ? sql[i + 1] : ''

    // 行注释
    if (c === '-' && n === '-') {
      while (i < sql.length && sql[i] !== '\n') i++
      continue
    }
    // 块注释
    if (c === '/' && n === '*') {
      const end = sql.indexOf('*/', i + 2)
      if (end < 0) throw new SqlSplitError('块注释未闭合')
      i = end + 2
      continue
    }
    // 字符串（含 E'' / N'' 前缀）
    if (c === "'" || ((c === 'E' || c === 'e' || c === 'N' || c === 'n') && n === "'" && (i === 0 || !isAlnum(sql[i - 1]))) ) {
      const allowBackslash = c === 'E' || c === 'e'
      const quoteAt = c === "'" ? i : i + 1
      i = skipQuoted(sql, quoteAt, "'", "'", allowBackslash)
      code += "'"
      continue
    }
    // 引号标识符
    if (c === '"' || c === '`') {
      i = skipQuoted(sql, i, c, c, false)
      code += '_q_'
      continue
    }
    if (c === '[') {
      i = skipQuoted(sql, i, '[', ']', false)
      code += '_q_'
      continue
    }
    // 美元引号
    if (c === '$') {
      const tag = tryDollarTag(sql, i)
      if (tag) {
        const end = sql.indexOf(tag, i + tag.length)
        if (end < 0) throw new SqlSplitError('美元引号字符串未闭合')
        i = end + tag.length
        code += "''"
        continue
      }
    }
    // 顶层分号
    if (c === ';') {
      flushWord()
      if (trigger && blockDepth > 0) {
        // 触发器块内分号：留在原文，不切分
        code += c
        i++
        continue
      }
      pushSegment(i)
      i++
      continue
    }
    // 标识符词
    if (isWordChar(c)) {
      if (!wordBuf) {
        if (isWordStart(c)) wordBuf = c
        else code += c // 纯数字：不参与 CREATE TRIGGER 判定
        if (!isWordStart(c)) { i++; continue }
        code += c
        i++
        continue
      }
      wordBuf += c
      code += c
      i++
      continue
    }
    flushWord()
    code += c
    i++
  }
  flushWord()
  pushSegment(sql.length)
  return out.filter(s => s.hasCode)
}

/** 空语句/仅注释 → 返回空数组；调用方据此显示「没有可执行的语句」 */
export function splitExecutable(sql: string): string[] {
  return splitStatements(sql).map(s => s.text.trim()).filter(t => t.length > 0)
}
