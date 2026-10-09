/**
 * inputSchema → 动态参数表单（MCP 中心 · 工具测试台 v2.3.0）。
 *
 * 设计约束：
 * 1. **纯函数、零 UI 依赖**（不 import vue / element-plus），便于 vitest 直接锁定各 schema 分支。
 * 2. 后端 `McpExternalToolDto.InputSchemaJson` 是 schema **原文字符串**，解析在前端做（不改后端契约）。
 * 3. 只做「够用且可预测」的 JSON Schema 子集：
 *    string / number / integer / boolean / array / object / enum / anyOf / required / default / description。
 *    无法识别的形态一律降级为 unknown，**不猜、不静默丢字段**。
 */

/** 字段渲染类型。enum 单独成类（渲染为下拉）；unknown 表示 schema 没说清，交给 JSON 模式。 */
export type SchemaFieldType =
  | 'string'
  | 'number'
  | 'integer'
  | 'boolean'
  | 'array'
  | 'object'
  | 'enum'
  | 'unknown'

/** 单个参数的渲染描述符。 */
export interface FieldDescriptor {
  /** 参数名（对应 arguments 的 key）。 */
  name: string
  /** 渲染类型。 */
  type: SchemaFieldType
  /** 是否必填（来自 schema.required）。 */
  required: boolean
  /** 参数说明（来自 description，空串表示无）。 */
  description: string
  /** schema 声明的默认值，未声明为 undefined。 */
  default?: unknown
  /** type==='enum' 时的候选值。 */
  enumValues?: unknown[]
  /** type==='array' 时元素的类型。 */
  itemType?: SchemaFieldType
}

/** 只关心本模块用到的 JSON Schema 形状。 */
export interface JsonSchemaObject {
  type?: unknown
  properties?: Record<string, unknown>
  required?: unknown
  [key: string]: unknown
}

const PRIMITIVE_TYPES: readonly SchemaFieldType[] = [
  'string',
  'number',
  'integer',
  'boolean',
  'array',
  'object',
]

/** 把 schema 里的 type 取值归一化：字符串 / 字符串数组（取首个非 'null'）/ 其他 → unknown。 */
function normalizeType(raw: unknown): SchemaFieldType | null {
  if (typeof raw === 'string') {
    return (PRIMITIVE_TYPES as readonly string[]).includes(raw)
      ? (raw as SchemaFieldType)
      : null
  }
  if (Array.isArray(raw)) {
    for (const item of raw) {
      if (typeof item === 'string' && item !== 'null') {
        const t = normalizeType(item)
        if (t) return t
      }
    }
  }
  return null
}

/**
 * 解析 inputSchema 原文。非对象 / 非法 JSON / 空串一律返回 null（调用方据此降级）。
 */
export function parseInputSchema(json: string | undefined | null): JsonSchemaObject | null {
  if (!json || json.trim() === '') return null
  let parsed: unknown
  try {
    parsed = JSON.parse(json)
  } catch {
    return null
  }
  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) return null
  return parsed as JsonSchemaObject
}

/**
 * 解析单个属性的渲染类型，优先级：enum > type > anyOf/oneOf（首个带 type 的分支）> unknown。
 */
export function resolvePropertyType(prop: unknown): SchemaFieldType {
  if (prop === null || typeof prop !== 'object' || Array.isArray(prop)) return 'unknown'
  const p = prop as Record<string, unknown>

  if (Array.isArray(p.enum) && p.enum.length > 0) return 'enum'

  const direct = normalizeType(p.type)
  if (direct) return direct

  // anyOf / oneOf：取第一个能解析出类型的分支（DeepWiki 的 repoName 即 anyOf[string, array<string>] → string）
  for (const key of ['anyOf', 'oneOf'] as const) {
    const branches = p[key]
    if (!Array.isArray(branches)) continue
    for (const branch of branches) {
      const t = resolvePropertyType(branch)
      if (t !== 'unknown') return t
    }
  }
  return 'unknown'
}

function asStringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string') : []
}

/**
 * schema → 字段描述符数组。属性顺序按 schema.properties 的声明顺序（Object.keys）。
 */
export function buildFieldDescriptors(schema: JsonSchemaObject | null): FieldDescriptor[] {
  if (!schema) return []
  const props = schema.properties
  if (!props || typeof props !== 'object') return []
  const required = asStringArray(schema.required)

  return Object.keys(props).map((name) => {
    const prop = props[name]
    const p =
      prop !== null && typeof prop === 'object' && !Array.isArray(prop)
        ? (prop as Record<string, unknown>)
        : {}
    const type = resolvePropertyType(prop)
    const field: FieldDescriptor = {
      name,
      type,
      required: required.includes(name),
      description: typeof p.description === 'string' ? p.description : '',
    }
    if ('default' in p && p.default !== undefined) field.default = p.default
    if (type === 'enum') field.enumValues = Array.isArray(p.enum) ? p.enum : []
    if (type === 'array') {
      field.itemType = p.items === undefined ? 'unknown' : normalizeTypeOrUnknown(p.items)
    }
    return field
  })
}

function normalizeTypeOrUnknown(items: unknown): SchemaFieldType {
  if (items !== null && typeof items === 'object' && !Array.isArray(items)) {
    const t = normalizeType((items as Record<string, unknown>).type)
    if (t) return t
  }
  return normalizeType(items) ?? 'unknown'
}

/** 按 default 预填参数（没声明 default 的字段不出现在结果里，避免把空串当真值传出去）。 */
export function buildDefaultArguments(fields: FieldDescriptor[]): Record<string, unknown> {
  const out: Record<string, unknown> = {}
  for (const f of fields) {
    if (f.default !== undefined) out[f.name] = f.default
  }
  return out
}

function isBlank(value: unknown): boolean {
  return value === undefined || value === null || (typeof value === 'string' && value.trim() === '')
}

/**
 * 表单值 → arguments JSON 字符串。
 * 规则（可预测优先）：
 *  - 空的可选字段**丢弃**（不传）；空的必填字段保留空串，让远端给出明确报错而不是悄悄改成 null。
 *  - number/integer：能转成有限数字就转，转不了就**原样保留**（用户能立刻看到远端的类型错误）。
 *  - boolean：'true' / true 为真，其余为假。
 *  - array/object：字符串先试 JSON.parse，成功用解析结果，失败原样保留。
 */
export function serializeArguments(
  fields: FieldDescriptor[],
  values: Record<string, unknown>,
): string {
  const out: Record<string, unknown> = {}
  for (const f of fields) {
    const raw = values[f.name]
    if (raw === undefined || raw === null) continue
    if (isBlank(raw) && !f.required) continue

    switch (f.type) {
      case 'number':
      case 'integer': {
        const n = typeof raw === 'number' ? raw : Number(raw)
        out[f.name] = Number.isFinite(n) ? (f.type === 'integer' ? Math.trunc(n) : n) : raw
        break
      }
      case 'boolean':
        out[f.name] = raw === true || raw === 'true'
        break
      case 'array':
      case 'object': {
        if (typeof raw === 'string') {
          try {
            out[f.name] = JSON.parse(raw)
          } catch {
            out[f.name] = raw
          }
        } else {
          out[f.name] = raw
        }
        break
      }
      default:
        out[f.name] = raw
    }
  }
  return JSON.stringify(out)
}

/**
 * 返回缺失的必填字段名（空数组表示都可以调用）。
 * 判定：undefined/null/空串 视为缺失；boolean false 与数字 0 **不算缺失**（否则永远调不了）。
 */
export function missingRequiredFields(
  fields: FieldDescriptor[],
  values: Record<string, unknown>,
): string[] {
  return fields.filter((f) => f.required && isBlank(values[f.name])).map((f) => f.name)
}
