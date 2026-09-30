<script setup lang="ts">
/**
 * 排版标度：把后端 TypographyGenerator 算出的模块化比例"照抄"成样张。
 *
 * 硬约束（本次改造的灵魂）：
 * 1. 字号/行高/字距/字重一律直接绑后端返回的字符串（`16px` / `1.5` / `-0.02em` / `700`），
 *    前端不出现任何 `basePx * Math.pow(ratio, step)` 之类的推导——那是 C# 的活；
 * 2. fluid `clamp()` **只从后端 description 原文里读**（`size.*` / `type.*` 都带了），
 *    读不到就写"未提供"，绝不前端自算 clamp；
 * 3. 调参条只是"下一次生成的参数建议"：交给 `generate/preview` 让后端算，
 *    本页不据参数改任何一个字号。
 *
 * 数据源：`tokens/effective`（值）+ `tokens`（原行的 description，effective 不带）。
 */
import { computed, onMounted, ref, shallowRef, type CSSProperties } from 'vue'
import { api, type EffectiveToken, type Token } from '../api'
import { ApiError } from '../http'
import { currentProject, effective, effectiveState, lastError, loadEffective, themeCode, unauthorized } from '../state'
import { cssVarName, valueSummary } from '../design/derive'
import PanelState from '../components/PanelState.vue'

/**
 * 层级展示顺序（契约里的角色名，见后端 TypographyGenerator.Roles）。
 * 只用于"显示的先后"——不是数值推导；库里出现新角色时自动排在末尾。
 */
const ROLE_ORDER = ['display', 'h1', 'h2', 'h3', 'h4', 'h5', 'body-lg', 'body', 'small', 'caption', 'overline', 'code', 'code-small']

const rowsLoading = ref(false)
const rowsError = ref('')
/** path → 原行（只为拿 description：fluid clamp 的原文在后端 Description 里） */
const rawRows = shallowRef<Map<string, Token>>(new Map())
const truncated = ref(false)

const openJson = ref('')

/** 建议参数（只作为 generate/preview 的入参，本页不据此改任何值） */
const basePx = ref('')
const ratio = ref('')
const busy = ref(false)
const suggestionError = ref('')
const suggestion = shallowRef<{ seed: string; industry: string; hue: number; shared: number; notes: string[]; sample: { path: string; value?: string | null }[] } | null>(null)

const items = computed<EffectiveToken[]>(() => effective.value?.items ?? [])
const index = computed(() => new Map(items.value.map((t) => [t.path, t])))

function suffixOf(path: string): string {
  return path.slice(path.indexOf('.') + 1)
}

function underPrefix(prefix: string): EffectiveToken[] {
  return items.value.filter((t) => t.path.startsWith(prefix))
}

const roles = computed<string[]>(() => {
  const set = new Set<string>()
  for (const t of items.value) {
    for (const prefix of ['size.', 'leading.', 'tracking.', 'type.']) {
      if (t.path.startsWith(prefix)) set.add(suffixOf(t.path))
    }
  }
  return [...set].sort((a, b) => rank(a) - rank(b) || (a < b ? -1 : 1))
})

function rank(role: string): number {
  const i = ROLE_ORDER.indexOf(role)
  return i < 0 ? ROLE_ORDER.length : i
}

interface TypeRow {
  role: string
  size?: EffectiveToken
  leading?: EffectiveToken
  tracking?: EffectiveToken
  composite?: EffectiveToken
}

const rows = computed<TypeRow[]>(() =>
  roles.value.map((role) => ({
    role,
    size: index.value.get(`size.${role}`),
    leading: index.value.get(`leading.${role}`),
    tracking: index.value.get(`tracking.${role}`),
    composite: index.value.get(`type.${role}`),
  })),
)

/** 后端复合令牌的 JSON 成分（仅解析用于展示；不做任何换算） */
const COMPOSITE_KEYS = ['fontFamily', 'fontSize', 'fontWeight', 'lineHeight', 'letterSpacing', 'textTransform'] as const

function compositeParts(t: EffectiveToken | undefined): { key: string; raw: string; resolved: string }[] {
  if (!t) return []
  let parsed: Record<string, unknown>
  try {
    parsed = JSON.parse(t.value) as Record<string, unknown>
  } catch {
    return []
  }
  const known = COMPOSITE_KEYS.filter((k) => k in parsed)
  const keys = [...new Set([...known, ...Object.keys(parsed)])]
  return keys.map((key) => {
    const raw = String((parsed as Record<string, unknown>)[key] ?? '')
    const m = /^\{(.+)\}$/.exec(raw.trim())
    // 别名成分：查后端有效值表补齐（是查表，不是计算）
    const resolved = m ? (index.value.get(m[1])?.value ?? '（当前主题无此有效值）') : ''
    return { key, raw, resolved }
  })
}

/** 复合令牌的一行摘要（后端 valueSummary；无复合令牌就留空） */
function summaryOf(row: TypeRow): string {
  return row.composite ? valueSummary(row.composite) : ''
}

/** 样张的 :style —— 每个值都是后端字符串原样绑定（CSSProperties 只为满足类型，不做换算） */
function sampleStyle(row: TypeRow): CSSProperties {
  const style: CSSProperties = {}
  if (row.size?.value) style.fontSize = row.size.value
  if (row.leading?.value) style.lineHeight = row.leading.value
  if (row.tracking?.value) style.letterSpacing = row.tracking.value
  const parts = new Map(compositeParts(row.composite).map((p) => [p.key, p]))
  const weight = parts.get('fontWeight')
  if (weight?.resolved) style.fontWeight = weight.resolved
  const transform = parts.get('textTransform')
  if (transform?.resolved && transform.resolved !== 'none') style.textTransform = transform.resolved
  const family = parts.get('fontFamily')
  if (family?.resolved) style.fontFamily = family.resolved
  return style
}

/** fluid 信息：只读后端 description 原文，拿不到就说"未提供" */
function clampText(t: EffectiveToken | undefined): string {
  if (!t) return '（本页无此令牌）'
  const desc = rawRows.value.get(t.path)?.description ?? ''
  return desc.trim() === '' ? '未提供（effective 不带 description，原行也没有）' : desc
}

const fonts = computed(() => underPrefix('font.'))
const weights = computed(() => underPrefix('weight.'))
const scaleInfo = computed(() => underPrefix('scale.'))
const typePrefixCount = computed(() => underPrefix('type.').length)

function errText(err: unknown): string {
  return err instanceof ApiError ? `${err.status} ${err.message}` : err instanceof Error ? err.message : String(err)
}

async function loadRows(): Promise<void> {
  const p = currentProject.value
  if (!p) return
  rowsLoading.value = true
  rowsError.value = ''
  try {
    const page = await api.listTokens(p.id, { pageSize: 2000 })
    const map = new Map<string, Token>()
    for (const r of page?.items ?? []) if (!map.has(r.path)) map.set(r.path, r)
    rawRows.value = map
    truncated.value = (page?.total ?? 0) > (page?.items?.length ?? 0)
  } catch (err) {
    rowsError.value = errText(err)
  } finally {
    rowsLoading.value = false
  }
}

/** 把建议参数交给后端预览；界面只用返回结果说话 */
async function askBackend(): Promise<void> {
  busy.value = true
  suggestionError.value = ''
  try {
    const res = await api.generatePreview({
      typeBasePx: basePx.value.trim() === '' ? null : Number(basePx.value),
      typeRatio: ratio.value.trim() === '' ? null : Number(ratio.value),
      themes: [themeCode.value],
    })
    suggestion.value = { seed: res.seed, industry: res.industry, hue: res.hue, shared: res.shared, notes: res.notes, sample: res.sample }
  } catch (err) {
    suggestion.value = null
    suggestionError.value = errText(err)
  } finally {
    busy.value = false
  }
}

function toggleJson(role: string): void {
  openJson.value = openJson.value === role ? '' : role
}

async function refresh(): Promise<void> {
  await Promise.all([loadEffective(true), loadRows()])
}

onMounted(() => {
  void refresh()
})
</script>

<template>
  <section class="tp ds-stack ds-gap-5">
    <header class="ds-section-title">
      <h3 class="ds-h3">排版标度</h3>
      <span class="ds-small">
        字号/行高/字距/字重全部来自 <span class="ds-mono">tokens/effective</span>（主题 <strong>{{ themeCode }}</strong>）·
        模块化比例由后端 <span class="ds-mono">TypographyGenerator</span> 计算，本页一个数都不推
      </span>
    </header>

    <p v-if="rowsError || lastError" class="tp__error" role="alert">{{ rowsError || lastError }}</p>
    <p v-if="truncated" class="tp__error" role="alert">令牌行数超过单次读取上限，description（fluid clamp 原文）可能显示不全。</p>

    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState v-else-if="effectiveState === 'loading' && !items.length" state="loading" />
    <PanelState v-else-if="!currentProject" state="empty" title="还没有选中设计系统项目" hint="到「项目与生成」页选择项目后，这里才有排版令牌。" />
    <PanelState v-else-if="!rows.length" state="empty" title="该项目没有排版令牌" hint="后端生成会铺出 size./leading./tracking./type. 四组角色；若为空说明该项目尚未生成或生成器未含排版层。" />

    <template v-else>
      <!-- 1) 字族 / 字重 / 比例：后端给什么显示什么 -->
      <div class="ds-stack ds-gap-3">
        <h4 class="ds-h4">字族与字重 <span class="ds-micro">font.* · weight.* · scale.*</span></h4>
        <div class="tp__facts">
          <div v-for="t in [...fonts, ...weights, ...scaleInfo]" :key="t.path" class="tp__fact">
            <span class="ds-mono tp__fact-path">{{ t.path }}</span>
            <span class="ds-small">{{ t.value || '（空）' }}</span>
            <span class="ds-micro">{{ cssVarName(t.path) }}</span>
          </div>
        </div>
        <p v-if="!weights.length" class="ds-small tp__muted">后端未返回 <span class="ds-mono">weight.*</span> 令牌——本页不猜字重，样张的字重只取 <span class="ds-mono">type.*</span> 里的成分。</p>
      </div>

      <!-- 2) 层级样张表 -->
      <div class="ds-stack ds-gap-3">
        <h4 class="ds-h4">层级样张 <span class="ds-micro">{{ rows.length }} 个角色 · 复合令牌 {{ typePrefixCount }} 个</span></h4>
        <table class="tp__table">
          <thead>
            <tr>
              <th>角色</th>
              <th>size.*</th>
              <th>leading.*</th>
              <th>tracking.*</th>
              <th>fluid（后端 description 原文）</th>
              <th>样张（按左列值渲染）</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="r in rows" :key="r.role">
              <tr>
                <td class="ds-mono">{{ r.role }}</td>
                <td class="ds-num">{{ r.size?.value ?? '—' }}</td>
                <td class="ds-num">{{ r.leading?.value ?? '—' }}</td>
                <td class="ds-num">{{ r.tracking?.value ?? '—' }}</td>
                <td class="ds-small tp__clamp">{{ clampText(r.size) }}</td>
                <td>
                  <span class="tp__sample" :style="sampleStyle(r)">排版标度 Ag 123</span>
                  <button v-if="r.composite" class="ds-link" type="button" @click="toggleJson(r.role)">
                    {{ openJson === r.role ? '收起复合令牌' : '展开复合令牌' }}
                  </button>
                  <span v-if="r.composite" class="ds-mono tp__summary">{{ summaryOf(r) }}</span>
                </td>
              </tr>
              <tr v-if="r.composite && openJson === r.role">
                <td colspan="6" class="tp__json">
                  <div class="ds-micro">type.{{ r.role }}（复合令牌：成分是后端别名与字面值，界面只查表补齐）</div>
                  <table class="tp__parts">
                    <tbody>
                      <tr v-for="p in compositeParts(r.composite)" :key="p.key">
                        <td class="ds-mono">{{ p.key }}</td>
                        <td class="ds-mono">{{ p.raw }}</td>
                        <td class="ds-small">
                          <template v-if="p.resolved">查表 → <span class="ds-mono">{{ p.resolved }}</span></template>
                          <template v-else>字面成分</template>
                        </td>
                      </tr>
                    </tbody>
                  </table>
                  <div class="ds-micro">fluid（后端 description 原文）：{{ clampText(r.composite) }}</div>
                  <pre class="tp__pre">{{ r.composite?.value }}</pre>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>

      <!-- 3) 建议参数条：交给后端算，界面不推导 -->
      <div class="tp__ask ds-surface-2">
        <h4 class="ds-h4">基准字号 + 比例（仅作下一次生成的建议）</h4>
        <p class="ds-small">
          这两格改动<strong>不会影响本表</strong>：本表永远是库里已落地的值。点「问后端要一份」是把参数交给
          <span class="ds-mono">generate/preview</span>（不落库）。常用比例清单在后端 <span class="ds-mono">TypographyGenerator.PresetRatios</span>，
          界面不复制一份以免漂移。
        </p>
        <div class="ds-row ds-gap-3 ds-wrap">
          <label class="ds-stack ds-gap-1">
            <span class="ds-micro">typeBasePx</span>
            <input v-model="basePx" class="ds-input ds-input--narrow" type="number" min="8" max="32" step="0.5" placeholder="16" />
          </label>
          <label class="ds-stack ds-gap-1">
            <span class="ds-micro">typeRatio</span>
            <input v-model="ratio" class="ds-input ds-input--narrow" type="number" min="1" max="2" step="0.001" placeholder="1.25" />
          </label>
          <button class="ds-btn" type="button" :disabled="busy" @click="askBackend">{{ busy ? '后端计算中…' : '问后端要一份' }}</button>
          <button class="ds-link" type="button" @click="refresh">重新读取本库</button>
        </div>
        <p v-if="suggestionError" class="tp__error" role="alert">{{ suggestionError }}</p>
        <template v-if="suggestion && !busy">
          <div class="ds-row ds-gap-4 ds-wrap ds-small">
            <span>seed <span class="ds-mono">{{ suggestion.seed }}</span></span>
            <span>行业 <strong>{{ suggestion.industry }}</strong></span>
            <span>色相 <span class="ds-num">{{ suggestion.hue }}</span>°</span>
            <span>共享层 <span class="ds-num">{{ suggestion.shared }}</span> 条</span>
          </div>
          <p class="ds-small tp__muted">
            注意：preview 的 <span class="ds-mono">sample</span> 由后端限定为 <strong>color 类型</strong>前 12 条，
            因此建议参数下的字号档位不会出现在这里；要看它们须在「项目与生成」页真正写入后回读本表。本页不代算。
          </p>
          <div class="tp__strip">
            <span v-for="s in suggestion.sample" :key="s.path" class="tp__sample-cell">
              <span class="tp__swatch" :style="{ background: s.value || 'transparent' }" aria-hidden="true"></span>
              <span class="ds-mono">{{ s.path }}</span>
              <span class="ds-micro">{{ s.value ?? '—' }}</span>
            </span>
          </div>
          <p v-for="n in suggestion.notes" :key="n" class="ds-micro tp__note">{{ n }}</p>
        </template>
        <p v-if="rowsLoading" class="ds-small">正在读取原行 description…</p>
      </div>
    </template>
  </section>
</template>

<style scoped>
.tp__error {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.tp__muted {
  color: var(--ds-fg-3);
}
.tp__note {
  color: var(--ds-warning);
  text-transform: none;
  letter-spacing: normal;
}
.tp__facts {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: var(--ds-space-3);
}
.tp__fact {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: var(--ds-space-3);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-1);
}
.tp__fact-path {
  color: var(--ds-fg-1);
}
.tp__table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.tp__table th {
  text-align: left;
  color: var(--ds-fg-3);
  font-weight: var(--ds-fw-medium);
  border-bottom: 1px solid var(--ds-border-1);
  padding: var(--ds-space-2);
  white-space: nowrap;
}
.tp__table td {
  padding: var(--ds-space-2);
  border-bottom: 1px solid var(--ds-border-1);
  vertical-align: middle;
}
.tp__clamp {
  color: var(--ds-fg-3);
  max-width: 32ch;
}
.tp__sample {
  display: block;
  color: var(--ds-fg-1);
  margin-bottom: var(--ds-space-1);
  overflow-wrap: anywhere;
}
.tp__summary {
  color: var(--ds-fg-3);
  font-size: var(--ds-fs-micro);
  overflow-wrap: anywhere;
}
.tp__json {
  background: var(--ds-surface-2);
}
.tp__parts {
  margin: var(--ds-space-2) 0;
  border-collapse: collapse;
  font-size: var(--ds-fs-micro);
}
.tp__parts td {
  padding: 2px var(--ds-space-3) 2px 0;
  vertical-align: top;
}
.tp__pre {
  margin: var(--ds-space-2) 0 0;
  padding: var(--ds-space-3);
  overflow-x: auto;
  font-family: var(--ds-font-mono);
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-2);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
}
.tp__ask {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
  padding: var(--ds-space-4);
}
.tp__strip {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ds-space-3);
}
.tp__sample-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}
.tp__swatch {
  width: var(--ds-space-8);
  height: var(--ds-space-4);
  border: 1px solid var(--ds-border-2);
  border-radius: var(--ds-radius-sm);
}
.ds-input {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: var(--ds-space-2) var(--ds-space-3);
}
.ds-input--narrow {
  width: 12ch;
}
.ds-btn {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-color-primary);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-2);
  border-radius: var(--ds-radius-sm);
  padding: var(--ds-space-2) var(--ds-space-4);
  cursor: pointer;
}
</style>
