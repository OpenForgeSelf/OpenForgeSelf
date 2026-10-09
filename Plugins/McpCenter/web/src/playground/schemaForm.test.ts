/**
 * schemaForm 纯函数单测（MCP 中心 · 工具测试台 v2.3.0）。
 * 覆盖各 JSON Schema 分支；其中 anyOf 用例直接采用 DeepWiki MCP 的真实 inputSchema 文本。
 */
import { describe, expect, it } from 'vitest'
import {
  buildDefaultArguments,
  buildFieldDescriptors,
  missingRequiredFields,
  parseInputSchema,
  resolvePropertyType,
  serializeArguments,
} from './schemaForm'

/** DeepWiki MCP tools/list 实测返回的 ask_wiki_question.inputSchema（原文照抄）。 */
const DEEPWIKI_ASK_SCHEMA = JSON.stringify({
  properties: {
    repoName: {
      anyOf: [{ type: 'string' }, { items: { type: 'string' }, type: 'array' }],
      description: 'GitHub repository or list of repositories (max 10) in owner/repo format.',
    },
    question: { description: 'The question to ask about the repository.', type: 'string' },
  },
  required: ['repoName', 'question'],
  type: 'object',
})

describe('parseInputSchema', () => {
  it('正常 object 解析成功', () => {
    expect(parseInputSchema('{"type":"object","properties":{}}')).toEqual({
      type: 'object',
      properties: {},
    })
  })

  it('空串 / undefined / 非法 JSON → null（调用方据此降级）', () => {
    expect(parseInputSchema('')).toBeNull()
    expect(parseInputSchema(undefined)).toBeNull()
    expect(parseInputSchema('{ not json')).toBeNull()
  })

  it('非对象（数组 / 字符串）→ null', () => {
    expect(parseInputSchema('[]')).toBeNull()
    expect(parseInputSchema('"str"')).toBeNull()
    expect(parseInputSchema('null')).toBeNull()
  })
})

describe('resolvePropertyType', () => {
  it('基础类型直读', () => {
    expect(resolvePropertyType({ type: 'string' })).toBe('string')
    expect(resolvePropertyType({ type: 'number' })).toBe('number')
    expect(resolvePropertyType({ type: 'integer' })).toBe('integer')
    expect(resolvePropertyType({ type: 'boolean' })).toBe('boolean')
    expect(resolvePropertyType({ type: 'array' })).toBe('array')
    expect(resolvePropertyType({ type: 'object' })).toBe('object')
  })

  it('enum 优先于 type（渲染为下拉）', () => {
    expect(resolvePropertyType({ type: 'string', enum: ['a', 'b'] })).toBe('enum')
  })

  it('type 为数组时取首个非 null（["string","null"] → string）', () => {
    expect(resolvePropertyType({ type: ['string', 'null'] })).toBe('string')
    expect(resolvePropertyType({ type: ['null', 'integer'] })).toBe('integer')
  })

  it('anyOf 取首个带 type 的分支（DeepWiki repoName → string）', () => {
    expect(
      resolvePropertyType({
        anyOf: [{ type: 'string' }, { items: { type: 'string' }, type: 'array' }],
      }),
    ).toBe('string')
  })

  it('oneOf 同样处理；全部分支无 type → unknown', () => {
    expect(resolvePropertyType({ oneOf: [{ type: 'boolean' }, { type: 'string' }] })).toBe('boolean')
    expect(resolvePropertyType({ anyOf: [{ description: 'x' }] })).toBe('unknown')
  })

  it('无 type 且无 anyOf → unknown（不猜）', () => {
    expect(resolvePropertyType({ description: '只写了说明' })).toBe('unknown')
    expect(resolvePropertyType(null)).toBe('unknown')
  })
})

describe('buildFieldDescriptors', () => {
  it('DeepWiki 真实 schema：2 字段、都必填、repoName 解析为 string 且带说明', () => {
    const fields = buildFieldDescriptors(parseInputSchema(DEEPWIKI_ASK_SCHEMA))
    expect(fields).toHaveLength(2)
    expect(fields[0]).toMatchObject({
      name: 'repoName',
      type: 'string',
      required: true,
      description: 'GitHub repository or list of repositories (max 10) in owner/repo format.',
    })
    expect(fields[1]).toMatchObject({ name: 'question', type: 'string', required: true })
  })

  it('required 只对清单内的字段生效', () => {
    const fields = buildFieldDescriptors(
      parseInputSchema('{"properties":{"a":{"type":"string"},"b":{"type":"number"}},"required":["b"]}'),
    )
    expect(fields.find((f) => f.name === 'a')?.required).toBe(false)
    expect(fields.find((f) => f.name === 'b')?.required).toBe(true)
  })

  it('default 与 enum 一并带出', () => {
    const fields = buildFieldDescriptors(
      parseInputSchema(
        '{"properties":{"mode":{"type":"string","enum":["fast","slow"],"default":"fast"},"n":{"type":"integer","default":3}},"required":["mode"]}',
      ),
    )
    const mode = fields.find((f) => f.name === 'mode')!
    expect(mode.type).toBe('enum')
    expect(mode.enumValues).toEqual(['fast', 'slow'])
    expect(mode.default).toBe('fast')
    expect(fields.find((f) => f.name === 'n')?.default).toBe(3)
  })

  it('array 带出元素类型', () => {
    const fields = buildFieldDescriptors(
      parseInputSchema('{"properties":{"tags":{"type":"array","items":{"type":"string"}}}}'),
    )
    expect(fields[0].itemType).toBe('string')
  })

  it('空 / 无 properties 的 schema → 空数组', () => {
    expect(buildFieldDescriptors(null)).toEqual([])
    expect(buildFieldDescriptors(parseInputSchema('{"type":"object"}'))).toEqual([])
  })
})

describe('buildDefaultArguments', () => {
  it('只预填声明了 default 的字段', () => {
    const fields = buildFieldDescriptors(
      parseInputSchema('{"properties":{"a":{"type":"string","default":"x"},"b":{"type":"string"}}}'),
    )
    expect(buildDefaultArguments(fields)).toEqual({ a: 'x' })
  })

  it('无 default → 空对象（不把空串当真值传出去）', () => {
    const fields = buildFieldDescriptors(parseInputSchema('{"properties":{"a":{"type":"string"}}}'))
    expect(buildDefaultArguments(fields)).toEqual({})
  })
})

describe('serializeArguments', () => {
  const fields = buildFieldDescriptors(
    parseInputSchema(
      '{"properties":{"s":{"type":"string"},"n":{"type":"number"},"i":{"type":"integer"},"b":{"type":"boolean"},"opt":{"type":"string"},"obj":{"type":"object"}},"required":["s"]}',
    ),
  )

  it('空的可选字段被丢弃；空的必填字段保留（让远端给明确报错）', () => {
    const out = JSON.parse(
      serializeArguments(fields, { s: '', opt: '', n: 1 }),
    ) as Record<string, unknown>
    expect(out).not.toHaveProperty('opt')
    expect(out.s).toBe('')
    expect(out.n).toBe(1)
  })

  it('number / integer 做数值转换（integer 截断）', () => {
    const out = JSON.parse(serializeArguments(fields, { n: '2.5', i: '7.9' })) as Record<
      string,
      unknown
    >
    expect(out.n).toBe(2.5)
    expect(out.i).toBe(7)
  })

  it('number 转换失败时原样保留（用户能看到远端的类型错误，而不是被静默丢弃）', () => {
    const out = JSON.parse(serializeArguments(fields, { n: 'abc' })) as Record<string, unknown>
    expect(out.n).toBe('abc')
  })

  it('boolean 按 true / "true" 判定', () => {
    expect(JSON.parse(serializeArguments(fields, { b: 'true' }))).toEqual({ b: true })
    expect(JSON.parse(serializeArguments(fields, { b: false }))).toEqual({ b: false })
  })

  it('object / array 字段：字符串能解析就用解析结果，解析失败原样保留', () => {
    expect(JSON.parse(serializeArguments(fields, { obj: '{"k":1}' }))).toEqual({ obj: { k: 1 } })
    expect(JSON.parse(serializeArguments(fields, { obj: 'not json' }))).toEqual({
      obj: 'not json',
    })
  })

  it('undefined / null 一律不出现在结果里', () => {
    expect(JSON.parse(serializeArguments(fields, { s: undefined, opt: null }))).toEqual({})
  })
})

describe('missingRequiredFields', () => {
  it('空串视为缺失；boolean false 与数字 0 不算缺失', () => {
    const fields = buildFieldDescriptors(
      parseInputSchema(
        '{"properties":{"s":{"type":"string"},"b":{"type":"boolean"},"n":{"type":"number"}},"required":["s","b","n"]}',
      ),
    )
    expect(missingRequiredFields(fields, {})).toEqual(['s', 'b', 'n'])
    expect(missingRequiredFields(fields, { s: 'x', b: false, n: 0 })).toEqual([])
  })
})
