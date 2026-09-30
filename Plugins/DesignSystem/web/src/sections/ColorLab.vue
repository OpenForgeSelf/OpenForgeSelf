<script setup lang="ts">
/**
 * 色彩实验室：把后端算好的色阶与语义角色「看得见、说得清」。
 *
 * 本文件的灵魂约束 —— **一个色彩运算都不做**：
 * 1. 色值只取 `tokens/effective` 的 `colorHex`（后端 Oklch→sRGB 的结果），没有 hex 就显示 `value` 原文；
 * 2. 对比度与判级只取后端 `contrastRatio` / `wcagLevel`（`ContrastMath` 的结论），-1 一律显示"未测"；
 * 3. 色块上的文字一律画在色块**外面**——"块上用白字还是黑字"本身就是一次对比度计算，那是后端的事；
 * 4. 「试算」调 `generate/preview`：后端算，界面只展示，**不落库**；真正写入在「项目与生成」页。
 *
 * 数据源：当前主题走 `state.effective`（共享缓存），对照主题单独打一次 `api.effective`，
 * 于是同一角色在 light / dark 下并排可见（AC：主题是轴，不是另一套真相）。
 */
import { computed, onMounted, ref, shallowRef } from 'vue'
import { api, type EffectiveToken, type EffectiveView, type GeneratePreview } from '../api'
import { ApiError } from '../http'
import { currentProject, effective, effectiveState, lastError, loadEffective, meta, setTheme, themeCode, themes, unauthorized } from '../state'
import { aliasTarget, colorFamilies, isColorTheme, rampSteps, ratioText, wcagBadge } from '../design/derive'
import PanelState from '../components/PanelState.vue'

/** 别名链最多追 8 跳（后端上限是 16 层，界面只展示前 8 跳并注明截断） */
const MAX_HOPS = 8

const compareCode = ref('')
const compare = shallowRef<EffectiveView | null>(null)
const compareBusy = ref(false)
const compareError = ref('')

const expanded = ref('')
const busy = ref(false)
const previewError = ref('')
const preview = shallowRef<GeneratePreview | null>(null)

/** 试算输入：只是"下一次生成的参数"，界面绝不拿它们算色 */
const seedColor = ref('')
const hue = ref('')
const chroma = ref('')

const items = computed<EffectiveToken[]>(() => effective.value?.items ?? [])
const index = computed(() => new Map(items.value.map((t) => [t.path, t])))

const ramps = computed(() => colorFamilies(items.value, meta.value?.colorFamilies ?? []).map((family) => ({ family, steps: rampSteps(items.value, family) })))

/** 模板里不写 filter 回调：vue-tsc 推不出参数类型（TS7006），计数在脚本里算好 */
const colorStepCount = computed(() => items.value.filter((t) => t.path.startsWith('color.')).length)

/** 语义层的 color 角色：界面显示的一律是解析后的有效值 */
const semanticColors = computed(() => items.value.filter((t) => t.path.startsWith('semantic.') && t.type === 'color'))

const diagnostics = computed(() => effective.value?.diagnostics ?? [])

const colorThemes = computed(() => themes.value.filter((t) => isColorTheme(t.modeKind)))
const compareOptions = computed(() => colorThemes.value.filter((t) => t.code !== themeCode.value))
const compareItems = computed<EffectiveToken[]>(() => (compare.value && compare.value.theme !== themeCode.value ? compare.value.items : []))
const compareIndex = computed(() => new Map(compareItems.value.map((t) => [t.path, t])))

/** 阶名（50/100…950）：取路径末段原文，不做数值推导 */
function stepName(path: string): string {
  return path.split('.').pop() ?? path
}

/** 后端给的颜色文本：优先 colorHex，退到 value 原文（两者都是后端产物） */
function colorOf(t: EffectiveToken | undefined): string {
  if (!t) return ''
  return t.colorHex || t.value || ''
}

/**
 * 别名链：逐跳用 aliasTarget 追下一站，全部落在后端 effective 的节点上。
 * 成环 / 目标缺失 / 超 8 跳都显式标注，不静默截断。
 */
function chainOf(start: string): { path: string; target: string; hex: string; note: string }[] {
  const links: { path: string; target: string; hex: string; note: string }[] = []
  const seen = new Set<string>()
  let current = start
  let truncated = true
  for (let hop = 0; hop < MAX_HOPS; hop += 1) {
    const node = index.value.get(current)
    if (!node) {
      links.push({ path: current, target: '', hex: '', note: '该跳目标不在当前主题的有效值视图里' })
      truncated = false
      break
    }
    const looped = seen.has(current)
    seen.add(current)
    const target = aliasTarget(node)
    const missing = target.length > 0 && !index.value.has(target)
    links.push({
      path: current,
      target,
      hex: colorOf(node),
      note: looped ? '环 —— 到此为止' : missing ? `别名目标 ${target} 不存在` : target === '' ? (node.value ? '字面值（链的终点）' : '既无值也无别名') : '',
    })
    if (looped || missing || target === '') {
      truncated = false
      break
    }
    current = target
  }
  if (truncated) links.push({ path: '…', target: '', hex: '', note: `超过 ${MAX_HOPS} 跳，已截断（后端上限 16 层）` })
  return links
}

function toggle(path: string): void {
  expanded.value = expanded.value === path ? '' : path
}

function errText(err: unknown): string {
  return err instanceof ApiError ? `${err.status} ${err.message}` : err instanceof Error ? err.message : String(err)
}

/** 对照主题的选择：绝不与当前主题同码（否则"并排"是同义反复） */
function syncCompare(): void {
  if (!compareOptions.value.some((t) => t.code === compareCode.value)) compareCode.value = compareOptions.value[0]?.code ?? ''
}

async function loadCompare(): Promise<void> {
  const p = currentProject.value
  if (!p || !compareCode.value) {
    compare.value = null
    return
  }
  compareBusy.value = true
  compareError.value = ''
  try {
    compare.value = await api.effective(p.id, compareCode.value)
  } catch (err) {
    compare.value = null
    compareError.value = errText(err)
  } finally {
    compareBusy.value = false
  }
}

async function pickTheme(code: string): Promise<void> {
  await setTheme(code)
  syncCompare()
  await loadCompare()
}

async function refresh(): Promise<void> {
  await loadEffective(true)
  syncCompare()
  await loadCompare()
}

/** 试算：只把参数交给后端，界面不参与任何色彩推导 */
async function runPreview(): Promise<void> {
  busy.value = true
  previewError.value = ''
  try {
    preview.value = await api.generatePreview({
      seedColor: seedColor.value.trim() || null,
      hue: hue.value.trim() === '' ? null : Number(hue.value),
      chroma: chroma.value.trim() === '' ? null : Number(chroma.value),
      themes: colorThemes.value.length ? colorThemes.value.map((t) => t.code) : undefined,
    })
  } catch (err) {
    preview.value = null
    previewError.value = errText(err)
  } finally {
    busy.value = false
  }
}

onMounted(async () => {
  await loadEffective()
  syncCompare()
  await loadCompare()
})
</script>

<template>
  <section class="cl ds-stack ds-gap-5">
    <header class="ds-section-title">
      <h3 class="ds-h3">色彩实验室</h3>
      <span class="ds-small">色阶与语义角色均来自 <span class="ds-mono">tokens/effective</span>（主题 <strong>{{ themeCode }}</strong>）· 本页不做任何色彩运算</span>
    </header>

    <p v-if="lastError" class="cl__error" role="alert">{{ lastError }}</p>
    <p v-if="diagnostics.length" class="cl__error" role="alert">
      别名解析异常 {{ diagnostics.length }} 条：
      <span v-for="d in diagnostics.slice(0, 4)" :key="d.path" class="ds-mono">{{ d.path }}（{{ d.status }}）</span>
    </p>

    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState v-else-if="effectiveState === 'loading' && !items.length" state="loading" />
    <PanelState v-else-if="!currentProject" state="empty" title="还没有选中设计系统项目" hint="到「项目与生成」页新建或选择一个项目，本页才有可读的令牌。" />
    <PanelState v-else-if="!items.length" state="empty" title="该项目暂无有效令牌" hint="到「项目与生成」页执行生成，后端会铺出 primitive 色阶与各主题的语义角色。" />

    <template v-else>
      <!-- 1) 色阶条带：一格一色，档名与 hex 一律画在色块外（块内选字色＝对比度计算，不属前端） -->
      <div class="ds-stack ds-gap-4">
        <h4 class="ds-h4">色阶条带 <span class="ds-micro">primitive · {{ ramps.length }} 族 · {{ colorStepCount }} 阶</span></h4>
        <div v-for="r in ramps" :key="r.family" class="cl__ramp">
          <div class="ds-row ds-gap-3">
            <span class="ds-mono cl__family">{{ r.family }}</span>
            <span class="ds-micro">{{ r.steps.length }} 阶</span>
          </div>
          <div class="cl__strip">
            <div v-for="s in r.steps" :key="s.path" class="cl__cell">
              <span class="cl__chip" :style="{ background: colorOf(s) || 'transparent' }" :title="`${s.path} = ${colorOf(s) || '（无值）'}`"></span>
              <span class="ds-micro cl__step">{{ stepName(s.path) }}</span>
              <span class="ds-mono cl__hex">{{ colorOf(s) || '—' }}</span>
              <span v-if="s.error" class="ds-micro cl__warn">{{ s.error }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 2) 语义角色：对比度来自后端；点一行展开它的别名链 -->
      <div class="ds-stack ds-gap-4">
        <div class="ds-row ds-gap-3 ds-wrap">
          <h4 class="ds-h4">语义角色</h4>
          <label class="ds-row ds-gap-2">
            <span class="ds-micro">对照主题</span>
            <select v-model="compareCode" class="ds-input" :disabled="!compareOptions.length" @change="loadCompare">
              <option v-if="!compareOptions.length" value="">（无其他配色主题）</option>
              <option v-for="t in compareOptions" :key="t.code" :value="t.code">{{ t.name || t.code }}</option>
            </select>
          </label>
          <span class="ds-micro">
            配色主题
            <button v-for="t in colorThemes" :key="t.code" class="ds-link" type="button" @click="pickTheme(t.code)">{{ t.code }}</button>
            · <button class="ds-link" type="button" @click="refresh">刷新</button>
          </span>
        </div>
        <p v-if="compareBusy" class="ds-small">正在读取对照主题的令牌…</p>
        <p v-else-if="compareError" class="cl__error" role="alert">对照主题读取失败：{{ compareError }}</p>

        <table class="cl__table">
          <thead>
            <tr>
              <th>角色</th>
              <th>{{ themeCode }}</th>
              <th>对比度（后端判级）</th>
              <th>别名指向</th>
              <th v-if="compareCode">{{ compareCode }}</th>
              <th v-if="compareCode">对照对比度</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="t in semanticColors" :key="t.path">
              <tr
                class="cl__row"
                :class="{ 'cl__row--on': expanded === t.path, 'cl__row--broken': !t.resolved }"
                tabindex="0"
                :aria-expanded="expanded === t.path"
                @click="toggle(t.path)"
                @keyup.enter="toggle(t.path)"
              >
                <td class="ds-mono">{{ t.path }}</td>
                <td>
                  <span class="cl__chip cl__chip--inline" :style="{ background: colorOf(t) || 'transparent' }" aria-hidden="true"></span>
                  <span class="ds-mono">{{ colorOf(t) || t.value || '—' }}</span>
                </td>
                <td :class="`wcag--${wcagBadge(t.contrastRatio).level}`">
                  {{ ratioText(t.contrastRatio) }}
                  <span v-if="t.wcagLevel" class="ds-micro">（{{ t.wcagLevel }}）</span>
                </td>
                <td class="ds-micro">
                  <span v-if="aliasTarget(t)" class="ds-mono">→ {{ aliasTarget(t) }}</span>
                  <span v-else>字面值</span>
                </td>
                <td v-if="compareCode">
                  <span class="cl__chip cl__chip--inline" :style="{ background: colorOf(compareIndex.get(t.path)) || 'transparent' }" aria-hidden="true"></span>
                  <span class="ds-mono">{{ colorOf(compareIndex.get(t.path)) || '—' }}</span>
                </td>
                <td v-if="compareCode" class="ds-mono">{{ ratioText(compareIndex.get(t.path)?.contrastRatio ?? -1) }}</td>
              </tr>
              <tr v-if="expanded === t.path">
                <td colspan="6" class="cl__chain">
                  <div class="ds-micro">别名链（逐跳取自后端有效值，最多 {{ MAX_HOPS }} 跳）</div>
                  <ol class="cl__hops">
                    <li v-for="(l, i) in chainOf(t.path)" :key="`${l.path}-${i}`" class="ds-row ds-gap-2">
                      <span class="ds-mono">{{ l.path }}</span>
                      <span v-if="l.hex" class="cl__chip cl__chip--inline" :style="{ background: l.hex }" aria-hidden="true"></span>
                      <span v-if="l.hex" class="ds-mono">{{ l.hex }}</span>
                      <span v-if="l.target" class="ds-micro">→ {{ l.target }}</span>
                      <span v-if="l.note" class="cl__note">{{ l.note }}</span>
                    </li>
                  </ol>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </template>

    <!-- 3) 试算：只展示后端算出的 sample，不落库 -->
    <div class="cl__preview ds-surface-2">
      <h4 class="ds-h4">试算（generate/preview · 只看不写）</h4>
      <p class="ds-small">
        给个种子色或色相，后端按 OKLCH 包络重算整套色阶；本页只显示返回的 sample。
        <strong>真正写入去「项目与生成」页</strong>——这里没有任何保存入口。
      </p>
      <div class="ds-row ds-gap-3 ds-wrap">
        <label class="ds-stack ds-gap-1">
          <span class="ds-micro">种子色（hex / oklch）</span>
          <input v-model="seedColor" class="ds-input" type="text" placeholder="#7c3aed" />
        </label>
        <label class="ds-stack ds-gap-1">
          <span class="ds-micro">色相 0~360</span>
          <input v-model="hue" class="ds-input ds-input--narrow" type="number" min="0" max="360" step="1" />
        </label>
        <label class="ds-stack ds-gap-1">
          <span class="ds-micro">彩度 0~0.37</span>
          <input v-model="chroma" class="ds-input ds-input--narrow" type="number" min="0" max="0.37" step="0.01" />
        </label>
        <button class="ds-btn" type="button" :disabled="busy" @click="runPreview">{{ busy ? '试算中…' : '试算' }}</button>
      </div>
      <p v-if="busy" class="ds-small">后端计算中…</p>
      <p v-if="previewError" class="cl__error" role="alert">{{ previewError }}</p>
      <template v-if="preview && !busy">
        <div class="ds-row ds-gap-4 ds-wrap ds-small">
          <span>seed <span class="ds-mono">{{ preview.seed }}</span></span>
          <span>行业 <strong>{{ preview.industry }}</strong></span>
          <span>色相 <span class="ds-num">{{ preview.hue }}</span>°</span>
          <span>共享层 <span class="ds-num">{{ preview.shared }}</span> 条</span>
        </div>
        <div class="ds-row ds-gap-3 ds-wrap">
          <span v-for="[code, count] in Object.entries(preview.themes)" :key="code" class="cl__tag">{{ code }} · {{ count }} 条主题层令牌</span>
        </div>
        <p v-for="n in preview.notes" :key="n" class="ds-micro cl__note">{{ n }}</p>
        <div class="cl__strip">
          <div v-for="s in preview.sample" :key="s.path" class="cl__cell">
            <span class="cl__chip" :style="{ background: s.value || 'transparent' }" aria-hidden="true"></span>
            <span class="ds-micro cl__step">{{ stepName(s.path) }}</span>
            <span class="ds-mono cl__hex">{{ s.value ?? '—' }}</span>
          </div>
        </div>
      </template>
    </div>
  </section>
</template>

<style scoped>
.cl__error {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.cl__table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.cl__table th {
  text-align: left;
  color: var(--ds-fg-3);
  font-weight: var(--ds-fw-medium);
  border-bottom: 1px solid var(--ds-border-1);
  padding: var(--ds-space-2);
}
.cl__table td {
  padding: var(--ds-space-2);
  border-bottom: 1px solid var(--ds-border-1);
  vertical-align: middle;
}
.cl__row {
  cursor: pointer;
}
.cl__row:hover td {
  background: var(--ds-surface-2);
}
.cl__row:focus-visible {
  outline: 2px solid var(--ds-color-primary);
  outline-offset: -2px;
}
.cl__row--on td {
  background: var(--ds-surface-3);
}
.cl__row--broken td {
  background: color-mix(in oklab, var(--ds-danger) 12%, transparent);
}
.cl__chain {
  background: var(--ds-surface-2);
}
.cl__hops {
  margin: var(--ds-space-2) 0 0;
  padding-left: var(--ds-space-5);
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-1);
  font-size: var(--ds-fs-small);
}
.cl__note {
  color: var(--ds-warning);
  text-transform: none;
  letter-spacing: normal;
}
.cl__ramp {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-2);
}
.cl__family {
  color: var(--ds-fg-1);
}
.cl__strip {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(88px, 1fr));
  gap: var(--ds-space-2);
}
.cl__cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}
.cl__chip {
  display: block;
  height: var(--ds-space-8);
  border: 1px solid var(--ds-border-2);
  border-radius: var(--ds-radius-sm);
}
.cl__chip--inline {
  display: inline-block;
  width: var(--ds-space-4);
  height: var(--ds-space-4);
  vertical-align: -2px;
  margin-right: var(--ds-space-2);
}
.cl__step {
  text-transform: none;
  letter-spacing: normal;
  color: var(--ds-fg-2);
}
.cl__hex {
  overflow-wrap: anywhere;
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-3);
}
.cl__warn {
  color: var(--ds-warning);
  text-transform: none;
  letter-spacing: normal;
}
.cl__preview {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
  padding: var(--ds-space-4);
}
.cl__tag {
  font-size: var(--ds-fs-micro);
  padding: 2px var(--ds-space-3);
  border-radius: var(--ds-radius-pill);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  color: var(--ds-fg-2);
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
  width: 10ch;
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
.wcag--fail {
  color: var(--ds-danger);
}
.wcag--large {
  color: var(--ds-warning);
}
.wcag--aa,
.wcag--aaa {
  color: var(--ds-success);
}
.wcag--unknown {
  color: var(--ds-fg-3);
}
</style>
