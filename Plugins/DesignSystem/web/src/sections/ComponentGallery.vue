<script setup lang="ts">
/**
 * 组件库（FR9 / FR15 / AC16）：**从后端设计系统库读组件与变体矩阵**，用原生 HTML + `component.*` 令牌真实渲染样张。
 *
 * v1 这里是写死的 SRE fixture（api-gateway/auth-service/billing-svc…）。v2 一条示例组件都不留：
 * - 组件来自 `api.listComponents(projectId)`，变体来自 `api.listVariants(projectId, code)`；
 * - 样张颜色一律取 `effective` 里存在的 `component.*` 令牌（`var(--ds-…)`），命中不到才退回语义槽位；
 * - 变体的 `variantJson`（canonical JSON）解析后作为标签展示；解析失败原样显示字符串，**绝不假装渲染成功**；
 * - 空态引导去「项目与生成」页生成设计系统。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { api, type Component, type Variant } from '../api'
import { ApiError } from '../http'
import { currentProject, effective, loadEffective, meta, unauthorized } from '../state'
import { axisValues, cssVarName, variantJsonOf } from '../design/derive'
import PanelState from '../components/PanelState.vue'

const components = ref<Component[]>([])
const variantsByCode = ref<Record<string, Variant[]>>({})
const loading = ref(false)
const err = ref('')

/* --------------------------- 令牌取值（真实渲染用） --------------------------- */
const tokenPaths = computed(() => new Set((effective.value?.items ?? []).map((t) => t.path)))

/** 按候选路径找第一个存在的令牌变量；都不存在则退回中性槽位（不写死设计值） */
function tok(candidates: string[], fallbackVar: string): string {
  for (const p of candidates) {
    if (tokenPaths.value.has(p)) return `var(${cssVarName(p)})`
  }
  return `var(${fallbackVar})`
}

function previewKind(c: Component): 'button' | 'input' | 'card' | 'badge' | 'table' | 'nav' | 'tabs' | 'dialog' | 'tooltip' | 'other' {
  const s = `${c.code} ${c.category ?? ''}`.toLowerCase()
  if (s.includes('button') || s.includes('btn') || s.includes('cta')) return 'button'
  if (s.includes('dialog') || s.includes('modal')) return 'dialog'
  if (s.includes('tooltip') || s.includes('popover')) return 'tooltip'
  if (s.includes('tab')) return 'tabs'
  if (s.includes('input') || s.includes('field') || s.includes('form')) return 'input'
  if (s.includes('card') || s.includes('tile')) return 'card'
  if (s.includes('badge') || s.includes('tag') || s.includes('status')) return 'badge'
  if (s.includes('table') || s.includes('row') || s.includes('grid')) return 'table'
  if (s.includes('nav') || s.includes('menu')) return 'nav'
  return 'other'
}

/** 组件样张的 :style，全部取后端 component.* 令牌 */
function sampleStyle(c: Component): Record<string, string> {
  const k = previewKind(c)
  const base = `component.${c.code}`
  if (k === 'button') {
    return {
      background: tok([`${base}.background`, 'component.button.primary.background'], '--ds-color-primary'),
      color: tok([`${base}.foreground`, 'component.button.primary.foreground'], '--ds-surface-1'),
      'border-radius': tok([`${base}.radius`], '--ds-radius-md'),
    }
  }
  if (k === 'input') {
    return {
      background: tok([`${base}.background`, 'component.input.background'], '--ds-surface-1'),
      color: tok([`${base}.foreground`, 'component.input.foreground'], '--ds-fg-1'),
      'border-color': tok([`${base}.border`, 'component.input.border'], '--ds-border-1'),
      'border-radius': tok([`${base}.radius`], '--ds-radius-sm'),
    }
  }
  if (k === 'card') {
    return {
      background: tok([`${base}.background`, 'component.card.background'], '--ds-surface-1'),
      color: tok([`${base}.foreground`, 'component.card.foreground'], '--ds-fg-1'),
      'border-color': tok([`${base}.border`, 'component.card.border'], '--ds-border-1'),
      'border-radius': tok([`${base}.radius`, 'component.card.radius'], '--ds-radius-lg'),
    }
  }
  if (k === 'badge') {
    return {
      background: tok([`${base}.tint`, 'component.badge.tint'], '--ds-surface-2'),
      color: tok([`${base}.foreground`, 'component.badge.foreground'], '--ds-color-primary'),
      'border-color': tok([`${base}.border`, 'component.badge.border'], '--ds-border-1'),
    }
  }
  if (k === 'table') {
    return {
      background: tok([`${base}.header.background`, 'component.table.header.background'], '--ds-surface-2'),
      color: tok([`${base}.foreground`, 'component.card.foreground'], '--ds-fg-1'),
      'border-color': tok([`${base}.border`, 'component.table.border'], '--ds-border-1'),
    }
  }
  if (k === 'dialog') {
    return {
      background: tok([`${base}.background`, 'component.dialog.background'], '--ds-surface-1'),
      color: tok([`${base}.foreground`, 'component.dialog.foreground'], '--ds-fg-1'),
      'border-color': tok([`${base}.border`, 'component.dialog.border'], '--ds-border-1'),
      'border-radius': tok([`${base}.radius`, 'component.dialog.radius'], '--ds-radius-lg'),
      'box-shadow': tok([`${base}.shadow`, 'component.dialog.shadow'], '--ds-shadow-lg'),
    }
  }
  if (k === 'tooltip') {
    return {
      background: tok([`${base}.background`, 'component.tooltip.background'], '--ds-fg-1'),
      color: tok([`${base}.foreground`, 'component.tooltip.foreground'], '--ds-surface-1'),
      'border-radius': tok([`${base}.radius`, 'component.tooltip.radius'], '--ds-radius-sm'),
    }
  }
  if (k === 'tabs') {
    return {
      color: tok([`${base}.tab.foreground`, 'component.tabs.tab.foreground'], '--ds-fg-2'),
      'border-color': tok([`${base}.border`, 'component.tabs.border'], '--ds-border-1'),
      '--tab-active': tok(['component.tabs.tab.foreground-active'], '--ds-fg-1'),
      '--tab-indicator': tok(['component.tabs.indicator'], '--ds-color-primary'),
      '--tab-hover-bg': tok(['component.tabs.tab.background-hover'], '--ds-surface-2'),
    }
  }
  return {
    background: tok([`${base}.background`, 'component.nav.item.background-hover', 'component.surface-2'], '--ds-surface-2'),
    color: tok([`${base}.foreground`, 'component.nav.item.foreground'], '--ds-fg-2'),
  }
}

/* --------------------------- JSON 解析（不许假装成功） --------------------------- */
function variantLabels(v: Variant): string {
  const raw = v.variantJson ?? ''
  try {
    const obj = JSON.parse(raw) as Record<string, unknown>
    const keys = Object.keys(obj)
    // 空轴 = 该组件的基线形态；写 'default' 会和后面的状态芯片连读成"default default"，看着像 bug
    if (keys.length === 0) return 'base'
    return keys.map((key) => `${key}=${String(obj[key])}`).join(' · ')
  } catch {
    return raw // 解析失败：原样字符串，不臆造标签
  }
}

function refList(c: Component): string[] {
  const raw = c.tokenRefsJson
  if (!raw) return []
  try {
    const parsed: unknown = JSON.parse(raw)
    if (Array.isArray(parsed)) return parsed.map((x) => (typeof x === 'string' ? x : JSON.stringify(x)))
    if (parsed && typeof parsed === 'object') {
      return Object.entries(parsed as Record<string, unknown>).map(([key, val]) => `${key}: ${typeof val === 'object' ? JSON.stringify(val) : String(val)}`)
    }
    return [String(parsed)]
  } catch {
    return [raw]
  }
}

/**
 * 状态序 = 后端词表 `meta.variantAxes` 里的 state 轴（与 DESIGN.md / registry 同一张 `VariantAxes` 表）。
 * 词表里没有的状态排到最后并保持接口原序；前端**不另列一份词表**，也不 `.sort()` 出第二种顺序。
 */
function statesOf(code: string): string[] {
  const order = stateChoices.value
  const rank = (s: string): number => {
    const i = order.indexOf(s)
    return i < 0 ? Number.MAX_SAFE_INTEGER : i
  }
  return [...new Set((variantsByCode.value[code] ?? []).map((v) => v.state))]
    .map((s, i) => ({ s, i }))
    .sort((a, b) => rank(a.s) - rank(b.s) || a.i - b.i)
    .map((x) => x.s)
}

/* ---------------------------------- 读取 ---------------------------------- */
async function loadVariants(list: Component[]): Promise<void> {
  const project = currentProject.value
  if (!project) return
  const map: Record<string, Variant[]> = {}
  await Promise.all(
    list.map(async (c) => {
      try {
        map[c.code] = await api.listVariants(project.id, c.code)
      } catch (e) {
        map[c.code] = []
        err.value = e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
      }
    }),
  )
  variantsByCode.value = map
}

async function load(): Promise<void> {
  const project = currentProject.value
  err.value = ''
  if (!project) {
    components.value = []
    variantsByCode.value = {}
    return
  }
  if (!effective.value) await loadEffective()
  loading.value = true
  try {
    const list = await api.listComponents(project.id)
    components.value = list
    await loadVariants(list)
  } catch (e) {
    err.value = e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
    components.value = []
  } finally {
    loading.value = false
  }
}

/* ------------------------------- 新建表单 ------------------------------- */
const emptyComp = { code: '', name: '', category: 'button', interactive: true, description: '' }
const compForm = ref({ ...emptyComp })
const compSaving = ref(false)
const compErr = ref('')
const compHint = ref('')

const emptyVar = { code: '', componentCode: '' }
const varForm = ref({ ...emptyVar, componentCode: components.value[0]?.code ?? '' })
const varSaving = ref(false)
const varErr = ref('')
const varHint = ref('')

/** 组件清单是异步到的，`varForm` 初始化时它还是空的 → 到货后补一次默认值（不覆盖用户已选的） */
watch(components, (list) => {
  if (!varForm.value.componentCode && list.length) varForm.value.componentCode = list[0].code
})

/**
 * 状态取值改为下拉，词表 = `GET /meta.variantAxes` 里的 state 轴（后端 `VariantAxes`，与 DESIGN.md / registry 同一张表）。
 * 之前这里是自由文本：手打一个 `hove` 就能造出第 8 种"幽灵状态"——它进得了库、进不了任何投影口径。
 * 但词表也不能变成牢笼：真有词表外的状态时，用户必须**显式选「自定义」**再另起一个框填。
 */
const CUSTOM_STATE = '__custom__'
const CUSTOM_VALUE = '__custom__'
const CUSTOM_AXIS = '__custom__'
const stateChoice = ref('default')
const stateCustom = ref('')
const stateChoices = computed(() => axisValues(meta.value?.variantAxes ?? [], 'state'))
const stateFreeText = computed(() => stateChoices.value.length === 0 || stateChoice.value === CUSTOM_STATE)
const stateValue = computed(() => (stateFreeText.value ? stateCustom.value.trim() : stateChoice.value))

/**
 * 变体轴同样"先选后拼"：轴清单与档位序都来自 `meta.variantAxes`（后端 `VariantAxes.Vocabulary()`），
 * 界面不再让用户敲 `{"size":"md"}` 这种能拼错的东西。多轴组合留一个显式的"直接写 JSON"开关 ——
 * 那是后端 canonical JSON 的口径，前端不发明第二套序列化。
 */
const axes = computed(() => meta.value?.variantAxes ?? [])
const axisChoice = ref('')
const axisCustomName = ref('')
const valueChoice = ref('')
const valueCustomText = ref('')
const rawJson = ref('{}')
const jsonMode = ref(false)
const axisName = computed(() => (axisChoice.value === CUSTOM_AXIS ? axisCustomName.value.trim() : axisChoice.value))
const valueOptions = computed(() => axisValues(axes.value, axisName.value))
const valueText = computed(() => (valueChoice.value === CUSTOM_VALUE ? valueCustomText.value.trim() : valueChoice.value))
const variantPayload = computed(() =>
  jsonMode.value ? rawJson.value.trim() || '{}' : variantJsonOf(axisName.value, valueText.value),
)

async function saveComponent(): Promise<void> {
  const project = currentProject.value
  compErr.value = ''
  compHint.value = ''
  if (!project) {
    compErr.value = '未选中项目。'
    return
  }
  if (!compForm.value.code.trim()) {
    compErr.value = '组件 code 不能为空。'
    return
  }
  compSaving.value = true
  try {
    await api.saveComponent(project.id, {
      code: compForm.value.code.trim(),
      name: compForm.value.name.trim() || undefined,
      category: compForm.value.category.trim() || undefined,
      interactive: compForm.value.interactive,
      description: compForm.value.description.trim() || undefined,
    })
    compHint.value = `已写入组件 ${compForm.value.code.trim()}`
    compForm.value = { ...emptyComp }
    await load()
    varForm.value.componentCode = components.value[0]?.code ?? ''
  } catch (e) {
    compErr.value = e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
  } finally {
    compSaving.value = false
  }
}

async function saveVariant(): Promise<void> {
  const project = currentProject.value
  varErr.value = ''
  varHint.value = ''
  if (!project) {
    varErr.value = '未选中项目。'
    return
  }
  if (!varForm.value.componentCode) {
    varErr.value = '请选择要加变体的组件。'
    return
  }
  if (stateFreeText.value && !stateValue.value) {
    varErr.value = stateChoices.value.length === 0
      ? '后端词表没回来（/meta 不可用），这一格先别补：状态名没有词表可依。'
      : '选了「自定义」就得填状态名；词表里有的状态请直接下拉选。'
    return
  }
  if (jsonMode.value) {
    try {
      const parsed: unknown = JSON.parse(variantPayload.value)
      if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('不是 JSON 对象')
    } catch (e) {
      varErr.value = `variantJson 不合法（${e instanceof Error ? e.message : String(e)}）：修好再存，别让坏键进库。`
      return
    }
  } else if (!axisName.value || !valueText.value) {
    varErr.value = '请选变体的轴与档位（词表外的可选「自定义」，多轴组合可切到「直接写 JSON」）。'
    return
  }
  varSaving.value = true
  try {
    await api.saveVariant(project.id, varForm.value.componentCode, {
      code: varForm.value.code.trim() || undefined,
      variantJson: variantPayload.value,
      state: stateValue.value || 'default',
    })
    varHint.value = `已为 ${varForm.value.componentCode} 新增变体（${variantPayload.value} · state=${stateValue.value || 'default'}）`
    varForm.value = { ...emptyVar, componentCode: varForm.value.componentCode }
    stateChoice.value = 'default'
    stateCustom.value = ''
    axisChoice.value = ''
    axisCustomName.value = ''
    valueChoice.value = ''
    valueCustomText.value = ''
    rawJson.value = '{}'
    jsonMode.value = false
    await loadVariants(components.value)
  } catch (e) {
    varErr.value = e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
  } finally {
    varSaving.value = false
  }
}

watch(() => currentProject.value?.id, () => void load())
onMounted(() => void load())
</script>

<template>
  <section class="cg">
    <header class="cg__bar">
      <div class="ds-section-title">
        <h2 class="ds-h2">组件库</h2>
        <span class="ds-micro">{{ components.length }} 个组件 · 数据来自后端库</span>
      </div>
      <button class="cg__refresh" type="button" @click="load">刷新</button>
    </header>

    <p v-if="err" class="cg__err" role="alert">{{ err }}</p>

    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState v-else-if="loading && !components.length" state="loading" />
    <PanelState v-else-if="!currentProject" state="empty" title="还没有选中设计系统项目" hint="先在顶部选择一个设计系统项目。" />
    <PanelState v-else-if="!components.length" state="empty" title="该项目没有组件记录" hint="去「项目与生成」页生成设计系统——生成后组件层令牌与组件清单会出现在这里。" />

    <div v-else class="cg__grid">
      <article v-for="c in components" :key="c.id" class="cg__card ds-surface">
        <div class="cg__preview">
          <button v-if="previewKind(c) === 'button'" class="cg__btn" :style="sampleStyle(c)">{{ c.name || c.code }}</button>
          <input v-else-if="previewKind(c) === 'input'" class="cg__input" :style="sampleStyle(c)" :placeholder="c.name || c.code" />
          <div v-else-if="previewKind(c) === 'card'" class="cg__cardbox" :style="sampleStyle(c)">
            <div class="cg__cardbox-title">{{ c.name || c.code }}</div>
            <div class="cg__cardbox-body">卡片样张（取 component.* 令牌）</div>
          </div>
          <span v-else-if="previewKind(c) === 'badge'" class="cg__badge" :style="sampleStyle(c)">{{ c.name || c.code }}</span>
          <table v-else-if="previewKind(c) === 'table'" class="cg__table">
            <thead><tr><th :style="sampleStyle(c)">列 A</th><th :style="sampleStyle(c)">列 B</th></tr></thead>
            <tbody><tr><td>{{ c.name || c.code }}</td><td>样张</td></tr></tbody>
          </table>
          <div v-else-if="previewKind(c) === 'nav'" class="cg__nav" :style="sampleStyle(c)">{{ c.name || c.code }}</div>
          <div v-else-if="previewKind(c) === 'tabs'" class="cg__tabs" :style="sampleStyle(c)">
            <span class="cg__tab cg__tab--on">概览</span>
            <span class="cg__tab">令牌</span>
            <span class="cg__tab">审计</span>
          </div>
          <div v-else-if="previewKind(c) === 'dialog'" class="cg__dialog" :style="sampleStyle(c)">
            <div class="cg__dialog-title">{{ c.name || c.code }}</div>
            <div class="cg__dialog-body">对话框样张（遮罩/圆角/阴影取 component.dialog.*）</div>
            <div class="cg__dialog-actions">
              <span class="cg__dialog-btn">取消</span>
              <span class="cg__dialog-btn cg__dialog-btn--primary">确定</span>
            </div>
          </div>
          <span v-else-if="previewKind(c) === 'tooltip'" class="cg__tooltip" :style="sampleStyle(c)">{{ c.name || c.code }}</span>
          <div v-else class="cg__other" :style="sampleStyle(c)">{{ c.name || c.code }}</div>
        </div>

        <div class="cg__body">
          <div class="cg__head">
            <span class="ds-mono cg__code">{{ c.code }}</span>
            <span class="ds-micro cg__cat">{{ c.category || '未分类' }}</span>
            <span class="cg__flag" :class="c.interactive ? 'cg__flag--on' : ''">{{ c.interactive ? '可交互' : '静态' }}</span>
            <span class="cg__flag">{{ c.status || '—' }}</span>
          </div>
          <p v-if="c.description" class="ds-small cg__desc">{{ c.description }}</p>

          <div v-if="refList(c).length" class="cg__refs">
            <span class="ds-micro cg__refs-label">tokenRefs</span>
            <code v-for="(r, i) in refList(c)" :key="i" class="ds-mono cg__ref">{{ r }}</code>
          </div>

          <div class="cg__matrix">
            <div class="ds-micro">变体 × 状态矩阵（{{ (variantsByCode[c.code] ?? []).length }}）</div>
            <div v-if="statesOf(c.code).length" class="cg__states">
              <span class="cg__state-label">状态</span>
              <span v-for="s in statesOf(c.code)" :key="s" class="cg__state-chip">{{ s }}</span>
            </div>
            <ul v-if="(variantsByCode[c.code] ?? []).length" class="cg__vars">
              <li v-for="v in variantsByCode[c.code]" :key="v.id" class="ds-small">
                <span class="ds-mono">{{ variantLabels(v) }}</span>
                <span class="cg__var-state">{{ v.state }}</span>
                <span v-if="typeof v.variantJson === 'string' && !v.variantJson.trim().startsWith('{')" class="cg__raw">（未解析：原样显示）</span>
              </li>
            </ul>
            <p v-else class="ds-micro cg__novar">无变体</p>
          </div>
        </div>
      </article>
    </div>

    <!-- 新建：组件 / 变体（最小表单，失败显示后端原文） -->
    <div class="cg__forms">
      <div class="cg__form ds-surface">
        <h4 class="ds-h4">新建组件</h4>
        <div class="cg__form-row">
          <label class="cg__field"><span class="ds-micro">code *</span><input v-model="compForm.code" class="cg__input-el" /></label>
          <label class="cg__field"><span class="ds-micro">name</span><input v-model="compForm.name" class="cg__input-el" /></label>
          <label class="cg__field"><span class="ds-micro">category</span><input v-model="compForm.category" class="cg__input-el" /></label>
          <label class="cg__field cg__field--check"><span class="ds-micro">interactive</span><input v-model="compForm.interactive" type="checkbox" /></label>
        </div>
        <label class="cg__field cg__field--wide"><span class="ds-micro">description</span><input v-model="compForm.description" class="cg__input-el" /></label>
        <p v-if="compErr" class="cg__err" role="alert">{{ compErr }}</p>
        <p v-if="compHint" class="cg__hint">{{ compHint }}</p>
        <button class="cg__submit" :disabled="compSaving || !currentProject" @click="saveComponent">{{ compSaving ? '写入中…' : '保存组件' }}</button>
      </div>

      <div class="cg__form ds-surface">
        <h4 class="ds-h4">新建变体</h4>
        <div class="cg__form-row">
          <label class="cg__field"><span class="ds-micro">组件 *</span>
            <select v-model="varForm.componentCode" class="cg__input-el" aria-label="变体组件">
              <option value="" disabled>选择组件…</option>
              <option v-for="c in components" :key="c.id" :value="c.code">{{ c.code }}</option>
            </select>
          </label>
          <label class="cg__field"><span class="ds-micro">state</span>
            <select v-model="stateChoice" class="cg__input-el" aria-label="交互态">
              <option v-for="s in stateChoices" :key="s" :value="s">{{ s }}</option>
              <option :value="CUSTOM_STATE">自定义…</option>
            </select>
          </label>
          <!-- 词表内的状态只能选不能敲；真要词表外的，选「自定义」后在右边这格填（下拉留着，随时选回去） -->
          <label v-if="stateFreeText" class="cg__field"><span class="ds-micro">自定义状态名</span>
            <input v-model="stateCustom" class="cg__input-el" aria-label="自定义状态名"
                   :placeholder="stateChoices.length ? '词表外的状态名，如 long-press' : '词表未就绪（/meta 没回来）'" />
          </label>
          <label class="cg__field"><span class="ds-micro">code（可选）</span><input v-model="varForm.code" class="cg__input-el" /></label>
        </div>
        <div class="cg__form-row">
          <label class="cg__field"><span class="ds-micro">轴</span>
            <select v-model="axisChoice" class="cg__input-el" aria-label="变体轴">
              <option value="" disabled>选择轴…</option>
              <option v-for="a in axes" :key="a.axis" :value="a.axis">{{ a.axis }}</option>
              <option :value="CUSTOM_AXIS">自定义轴…</option>
            </select>
          </label>
          <label v-if="axisChoice === CUSTOM_AXIS" class="cg__field"><span class="ds-micro">轴名</span>
            <input v-model="axisCustomName" class="cg__input-el" aria-label="自定义轴名" placeholder="词表外的轴，如 density" />
          </label>
          <label class="cg__field"><span class="ds-micro">档位</span>
            <select v-model="valueChoice" class="cg__input-el" aria-label="变体档位"
                    :disabled="!axisName || (valueOptions.length === 0 && axisChoice !== CUSTOM_AXIS)">
              <option value="" disabled>选择档位…</option>
              <option v-for="v in valueOptions" :key="v" :value="v">{{ v }}</option>
              <option :value="CUSTOM_VALUE">自定义档位…</option>
            </select>
          </label>
          <label v-if="valueChoice === CUSTOM_VALUE" class="cg__field"><span class="ds-micro">档位名</span>
            <input v-model="valueCustomText" class="cg__input-el" aria-label="自定义档位" placeholder="词表外的档位，如 2xl" />
          </label>
        </div>
        <div class="cg__form-row">
          <label class="cg__field cg__field--wide">
            <span class="ds-micro">{{ jsonMode ? 'variantJson（直接写 JSON，多轴组合用这个）' : 'variantJson（按轴选，自动拼好；只读）' }}</span>
            <input v-if="jsonMode" v-model="rawJson" class="cg__input-el cg__mono-el" aria-label="variantJson"
                   placeholder='{"size":"md","role":"primary"}' />
            <input v-else :value="variantPayload" class="cg__input-el cg__mono-el" aria-label="拼好的 variantJson" readonly />
          </label>
          <button type="button" class="ds-link cg__toggle" aria-label="切换 variantJson 输入方式" @click="jsonMode = !jsonMode">
            {{ jsonMode ? '回到按轴选' : '多轴？直接写 JSON' }}
          </button>
        </div>
        <p v-if="varErr" class="cg__err" role="alert">{{ varErr }}</p>
        <p v-if="varHint" class="cg__hint">{{ varHint }}</p>
        <button class="cg__submit" :disabled="varSaving || !currentProject" @click="saveVariant">{{ varSaving ? '写入中…' : '保存变体' }}</button>
      </div>
    </div>
  </section>
</template>

<style scoped>
.cg {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-5);
}
.cg__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ds-space-4);
}
.cg__refresh {
  font: inherit;
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-2);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 5px var(--ds-space-3);
  cursor: pointer;
}
.cg__err {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.cg__hint {
  color: var(--ds-success);
  font-size: var(--ds-fs-small);
}
.cg__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: var(--ds-space-4);
}
.cg__card {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.cg__preview {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: var(--ds-space-5);
  min-height: 96px;
  background: var(--ds-surface-2);
  border-bottom: 1px solid var(--ds-border-1);
}
.cg__btn {
  font: inherit;
  border: none;
  padding: var(--ds-space-2) var(--ds-space-4);
  cursor: default;
}
.cg__input {
  font: inherit;
  border-width: 1px;
  border-style: solid;
  padding: var(--ds-space-2) var(--ds-space-3);
  min-width: 180px;
}
.cg__cardbox {
  border-width: 1px;
  border-style: solid;
  padding: var(--ds-space-4);
  width: 100%;
}
.cg__cardbox-title {
  font-size: var(--ds-fs-body);
  font-weight: var(--ds-fw-semibold);
}
.cg__cardbox-body {
  font-size: var(--ds-fs-small);
  opacity: 0.8;
}
.cg__badge {
  font-size: var(--ds-fs-micro);
  padding: 2px var(--ds-space-3);
  border-width: 1px;
  border-style: solid;
  border-radius: var(--ds-radius-pill);
}
.cg__table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.cg__table th,
.cg__table td {
  border: 1px solid var(--ds-border-1);
  padding: var(--ds-space-1) var(--ds-space-2);
  text-align: left;
}
.cg__nav,
.cg__other {
  padding: var(--ds-space-2) var(--ds-space-4);
  border-radius: var(--ds-radius-sm);
  font-size: var(--ds-fs-body);
}
/* 选项卡样张：颜色全部来自 :style 注入的 --tab-* 自定义属性（它们指向 component.tabs.* 令牌） */
.cg__tabs {
  display: flex;
  align-items: flex-end;
  gap: var(--ds-space-4);
  border-bottom: 1px solid var(--ds-border-1);
}
.cg__tab {
  padding: var(--ds-space-2) var(--ds-space-1);
  font-size: var(--ds-fs-small);
  border-bottom: 2px solid transparent;
}
.cg__tab:hover {
  background: var(--tab-hover-bg);
}
.cg__tab--on {
  color: var(--tab-active);
  border-bottom-color: var(--tab-indicator);
}
.cg__dialog {
  inline-size: 100%;
  max-inline-size: 260px;
  padding: var(--ds-space-4);
  border: 1px solid;
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-2);
}
.cg__dialog-title {
  font-size: var(--ds-fs-h4);
  font-weight: 600;
}
.cg__dialog-body {
  font-size: var(--ds-fs-small);
  opacity: 0.85;
}
.cg__dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--ds-space-2);
  margin-top: var(--ds-space-2);
}
.cg__dialog-btn {
  padding: var(--ds-space-1) var(--ds-space-3);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  font-size: var(--ds-fs-small);
}
.cg__dialog-btn--primary {
  background: var(--ds-color-primary);
  color: var(--ds-surface-1);
  border-color: transparent;
}
.cg__tooltip {
  padding: var(--ds-space-1) var(--ds-space-2);
  font-size: var(--ds-fs-micro);
}
.cg__body {
  padding: var(--ds-space-4);
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-2);
}
.cg__head {
  display: flex;
  align-items: center;
  gap: var(--ds-space-2);
  flex-wrap: wrap;
}
.cg__code {
  color: var(--ds-fg-1);
}
.cg__cat {
  color: var(--ds-fg-3);
}
.cg__flag {
  font-size: var(--ds-fs-micro);
  padding: 1px 8px;
  border-radius: var(--ds-radius-pill);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  color: var(--ds-fg-3);
}
.cg__flag--on {
  color: var(--ds-success);
}
.cg__desc {
  color: var(--ds-fg-3);
}
.cg__refs {
  display: flex;
  flex-wrap: wrap;
  gap: 4px var(--ds-space-2);
  align-items: center;
}
.cg__refs-label {
  color: var(--ds-fg-4);
}
.cg__ref {
  font-size: var(--ds-fs-micro);
  background: var(--ds-surface-2);
  border-radius: var(--ds-radius-sm);
  padding: 1px 6px;
  color: var(--ds-fg-2);
}
.cg__matrix {
  border-top: 1px solid var(--ds-border-1);
  padding-top: var(--ds-space-2);
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-1);
}
.cg__states {
  display: flex;
  align-items: center;
  gap: var(--ds-space-2);
  flex-wrap: wrap;
}
.cg__state-label {
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-4);
}
.cg__state-chip {
  font-size: var(--ds-fs-micro);
  padding: 1px 6px;
  border-radius: var(--ds-radius-pill);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
}
.cg__vars {
  margin: 0;
  padding-left: var(--ds-space-4);
}
.cg__var-state {
  margin-left: var(--ds-space-2);
  color: var(--ds-fg-4);
  font-size: var(--ds-fs-micro);
}
.cg__raw {
  color: var(--ds-warning);
  font-size: var(--ds-fs-micro);
}
.cg__novar {
  color: var(--ds-fg-4);
}
.cg__forms {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: var(--ds-space-4);
}
.cg__form {
  padding: var(--ds-space-4);
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-2);
}
.cg__form-row {
  display: flex;
  gap: var(--ds-space-3);
  flex-wrap: wrap;
}
/* 切换按钮与右边的输入框底部对齐（上面那行是字段名，按钮没有标签所以要自己贴底） */
.cg__toggle {
  align-self: flex-end;
  padding-bottom: 8px;
}
.cg__field {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.cg__field--wide {
  max-width: 520px;
}
.cg__field--check {
  justify-content: flex-end;
}
.cg__input-el {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-3);
}
.cg__mono-el {
  font-family: var(--ds-font-mono);
}
.cg__submit {
  align-self: flex-start;
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-surface-1);
  background: var(--ds-color-primary);
  border: 1px solid var(--ds-color-primary);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-4);
  cursor: pointer;
}
.cg__submit:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
