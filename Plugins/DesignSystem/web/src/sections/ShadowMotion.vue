<script setup lang="ts">
/**
 * 阴影与动效：把后端的复合令牌（shadow / duration / ease / transition）摊开给人看。
 *
 * 硬约束（本页最容易破功，故逐条钉死）：
 * 1. **不合成 CSS**：示例方块的 box-shadow 直接绑后端 `tokens/effective` 给的 value 原文
 *    （后端 ShadowCss 已经把多层展开成串了）。若某条 shadow 的 value 是 JSON（即后端没给现成 CSS 串），
 *    本页就**只列每层参数表**并提示"完整 box-shadow 串见导出 CSS/令牌投影"，绝不自己拼；
 * 2. 逐层表格解析 `valueJson` 只为**展示每层字段**（offsetX/offsetY/blur/spread/color/alpha/inset），
 *    一个数字都不改；
 * 3. 播放条只**切换取值**：duration/ease 用哪一条由界面选，reduced 开关只是改用
 *    `duration.*-reduced` 的后端原文；唯一允许的字符串处理是把后端的 `[a, b, c, d]`
 *    数组原样包成 `cubic-bezier(a, b, c, d)`（不改任何数字，CSS 语法要求）。
 *
 * 数据源：`tokens/effective`（值）+ `tokens`（原行的 valueJson 分层，effective 不带）。
 */
import { computed, onMounted, ref, shallowRef, type CSSProperties } from 'vue'
import { api, type EffectiveToken, type Token } from '../api'
import { ApiError } from '../http'
import { currentProject, effective, effectiveState, lastError, loadEffective, themeCode, unauthorized } from '../state'
import { compareSteps, cssVarName, valueSummary } from '../design/derive'
import PanelState from '../components/PanelState.vue'

/** 后端 ScaleGenerators.Shadows 的分层字段（仅用于展示） */
interface ShadowLayer {
  offsetX?: number | string
  offsetY?: number | string
  blur?: number | string
  spread?: number | string
  color?: string
  alpha?: number | string
  inset?: boolean
}
const LAYER_FIELDS: (keyof ShadowLayer)[] = ['offsetX', 'offsetY', 'blur', 'spread', 'color', 'alpha', 'inset']

const rowsLoading = ref(false)
const rowsError = ref('')
/** `主题ID:路径` → 原行：shadow 的分层在 ValueJson 里，effective 视图不带 */
const rawRows = shallowRef<Map<string, Token>>(new Map())

const open = ref('')
const moved = ref(false)
const simulateReduced = ref(false)
const durationPath = ref('')
const easePath = ref('')

const items = computed<EffectiveToken[]>(() => effective.value?.items ?? [])
const index = computed(() => new Map(items.value.map((t) => [t.path, t])))
const currentThemeId = computed(() => effective.value?.themeId ?? 0)

function rowOf(path: string): Token | undefined {
  return rawRows.value.get(`${currentThemeId.value}:${path}`) ?? rawRows.value.get(`0:${path}`)
}

/** 阴影按层号排（`shadow.10` 不能在 `shadow.2` 前）：走同一个档位比较器，不在此处另定一套序 */
const shadows = computed(() =>
  items.value
    .filter((t) => t.path.startsWith('shadow.'))
    .sort((a, b) => compareSteps(a.path, b.path, [])),
)
const transitions = computed(() => items.value.filter((t) => t.path.startsWith('transition.')))
const eases = computed(() => items.value.filter((t) => t.path.startsWith('ease.')))

/** duration 与它的 -reduced 配对（后端 Motion() 成对生成，缺配就显式标出来） */
const durations = computed(() => {
  const all = items.value.filter((t) => t.path.startsWith('duration.'))
  const reduced = new Map(all.filter((t) => t.path.endsWith('-reduced')).map((t) => [t.path.slice(0, -'-reduced'.length), t.value]))
  return all
    .filter((t) => !t.path.endsWith('-reduced'))
    .sort((a, b) => (a.path < b.path ? -1 : 1))
    .map((t) => ({ base: t, reduced: reduced.get(t.path) ?? null }))
})
const orphanReduced = computed(() => {
  const bases = new Set(durations.value.map((d) => d.base.path))
  return items.value.filter((t) => t.path.endsWith('-reduced') && !bases.has(t.path.slice(0, -'-reduced'.length)))
})

/** 后端是否给出现成 CSS 串（JSON 开头就说明没有） */
function cssLiteral(t: EffectiveToken): string {
  const raw = (t.value ?? '').trim()
  return raw.startsWith('{') || raw.startsWith('[') ? '' : raw
}

/** 分层：只解析后端 valueJson 用于展示，不做任何合成 */
function layersOf(path: string): ShadowLayer[] {
  const json = rowOf(path)?.valueJson ?? ''
  if (!json.trim().startsWith('[')) return []
  try {
    return JSON.parse(json) as ShadowLayer[]
  } catch {
    return []
  }
}

function layerCount(t: EffectiveToken): number {
  return layersOf(t.path).length
}

/** 唯一的字符串处理：后端数组 → CSS 函数语法，数字逐个原样搬运 */
function cubicBezierCss(raw: string): string {
  const m = /^\[([^\]]+)\]$/.exec(raw.trim())
  return m ? `cubic-bezier(${m[1]})` : ''
}

function fmtLayer(layer: ShadowLayer, field: keyof ShadowLayer): string {
  const v = layer[field]
  if (v === undefined) return '—'
  if (typeof v === 'boolean') return v ? '是' : '否'
  return String(v)
}

const currentDuration = computed(() => durations.value.find((d) => d.base.path === durationPath.value) ?? durations.value[0] ?? null)
const currentEase = computed(() => eases.value.find((e) => e.path === easePath.value) ?? eases.value[0] ?? null)
/** 实际用于播放条的时长：开关只决定取哪一条后端令牌，不换算 */
const activeDuration = computed(() => {
  const pair = currentDuration.value
  if (!pair) return null
  if (simulateReduced.value) return pair.reduced === null ? null : { path: `${pair.base.path}-reduced`, value: pair.reduced }
  return { path: pair.base.path, value: pair.base.value }
})

function playerStyle(): CSSProperties {
  const style: CSSProperties = { transitionProperty: 'transform' }
  if (activeDuration.value?.value) style.transitionDuration = activeDuration.value.value
  const ease = cubicBezierCss(currentEase.value?.value ?? '')
  if (ease) style.transitionTimingFunction = ease
  return style
}

/** transition.* 的成分：别名成分按后端有效值查表补齐 */
function partsOf(t: EffectiveToken): { key: string; raw: string; resolved: string }[] {
  const raw = (t.value ?? '').trim()
  if (!raw.startsWith('{')) return []
  let obj: Record<string, unknown>
  try {
    obj = JSON.parse(raw) as Record<string, unknown>
  } catch {
    return []
  }
  return Object.keys(obj).map((key) => {
    const value = String(obj[key] ?? '')
    const m = /^\{(.+)\}$/.exec(value.trim())
    return { key, raw: value, resolved: m ? (index.value.get(m[1])?.value ?? '（当前主题无此有效值）') : '' }
  })
}

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
    for (const r of page?.items ?? []) map.set(`${r.themeId}:${r.path}`, r)
    rawRows.value = map
  } catch (err) {
    rowsError.value = errText(err)
  } finally {
    rowsLoading.value = false
  }
}

function toggle(path: string): void {
  open.value = open.value === path ? '' : path
}

async function refresh(): Promise<void> {
  await Promise.all([loadEffective(true), loadRows()])
  syncPickers()
}

/** 播放条默认选中后端给出的第一条 duration / ease（不猜、不造值） */
function syncPickers(): void {
  const hasDuration = durations.value.some((d) => d.base.path === durationPath.value)
  if (!hasDuration) durationPath.value = durations.value[0]?.base.path ?? ''
  const hasEase = eases.value.some((e) => e.path === easePath.value)
  if (!hasEase) easePath.value = eases.value[0]?.path ?? ''
}

onMounted(async () => {
  await Promise.all([loadEffective(), loadRows()])
  syncPickers()
})
</script>

<template>
  <section class="sm ds-stack ds-gap-6">
    <header class="ds-section-title">
      <h3 class="ds-h3">阴影与动效</h3>
      <span class="ds-small">
        <span class="ds-mono">shadow.* / duration.* / ease.* / transition.*</span> 取自
        <span class="ds-mono">tokens/effective</span>（主题 <strong>{{ themeCode }}</strong>）·
        本页不合成 CSS，示例方块用的就是后端给的那串值
      </span>
    </header>

    <p v-if="rowsError || lastError" class="sm__error" role="alert">{{ rowsError || lastError }}</p>
    <p v-else-if="rowsLoading" class="ds-small sm__dim">正在读取原行（取后端 valueJson 分层）…</p>

    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState v-else-if="effectiveState === 'loading' && !items.length" state="loading" />
    <PanelState v-else-if="!currentProject" state="empty" title="还没有选中设计系统项目" hint="阴影与动效令牌随项目生成；先选一个项目。" />
    <PanelState
      v-else-if="!shadows.length && !durations.length && !eases.length"
      state="empty"
      title="该项目没有阴影/动效令牌"
      hint="后端 Motion() 与 Shadows() 会铺出 duration./ease./transition./shadow. 四组；若为空说明该项目未生成。"
    />

    <template v-else>
      <!-- 1) 阴影：示例方块用后端 CSS 串；分层参数表来自后端 valueJson -->
      <div class="ds-stack ds-gap-3">
        <div class="ds-row ds-gap-3 ds-wrap">
          <h4 class="ds-h4">阴影层级 <span class="ds-micro">shadow.* · {{ shadows.length }} 档</span></h4>
          <button class="ds-link" type="button" @click="refresh">重新读取本库</button>
        </div>
        <p v-if="rowsLoading" class="ds-small sm__dim">分层参数需读原行的 valueJson，正在加载…</p>
        <table class="sm__table">
          <thead>
            <tr>
              <th>令牌</th>
              <th>层数</th>
              <th>示例（boxShadow = 后端 value 原文）</th>
              <th>值摘要（valueSummary）</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <template v-for="t in shadows" :key="t.path">
              <tr>
                <td class="ds-mono">{{ t.path }}</td>
                <td class="ds-num">{{ layerCount(t) || '—' }}</td>
                <td>
                  <span
                    v-if="cssLiteral(t)"
                    class="sm__box"
                    :style="{ boxShadow: cssLiteral(t) }"
                    :title="`${t.path} = ${cssLiteral(t)}`"
                  ></span>
                  <span v-else class="ds-small sm__dim">后端未给现成 CSS 串，只列分层</span>
                </td>
                <td class="ds-mono sm__value">{{ valueSummary(t) }}</td>
                <td class="sm__ops">
                  <button class="ds-link" type="button" @click="toggle(t.path)">{{ open === t.path ? '收起分层' : '展开分层' }}</button>
                </td>
              </tr>
              <tr v-if="open === t.path">
                <td colspan="5" class="sm__detail">
                  <div class="ds-micro">分层（字段取自后端 valueJson，界面未改任何数字）</div>
                  <table v-if="layersOf(t.path).length" class="sm__table sm__table--inner">
                    <thead>
                      <tr>
                        <th>层</th>
                        <th v-for="f in LAYER_FIELDS" :key="f">{{ f }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="(l, i) in layersOf(t.path)" :key="i">
                        <td class="ds-num">{{ i + 1 }}</td>
                        <td v-for="f in LAYER_FIELDS" :key="f" class="ds-num">{{ fmtLayer(l, f) }}</td>
                      </tr>
                    </tbody>
                  </table>
                  <p v-else class="ds-small sm__warn">
                    原行里没有可解析的 <span class="ds-mono">valueJson</span> 分层（主题 {{ currentThemeId }}）。
                    本页不代算分层——完整 box-shadow 串见导出 CSS / 令牌投影（「导出交付」页）。
                  </p>
                  <p v-if="!cssLiteral(t)" class="ds-small sm__dim">
                    该令牌的 value 是 JSON 而非现成 CSS 串：完整 box-shadow 串见导出 CSS / 令牌投影，本页不自行拼接。
                  </p>
                  <div class="ds-micro">CSS 变量：<span class="ds-mono">{{ cssVarName(t.path) }}</span></div>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>

      <!-- 2) 时长与 reduced 配对：动效可关闭必须是数据，不是文档提醒 -->
      <div class="ds-stack ds-gap-3">
        <h4 class="ds-h4">时长与"可关闭"配对 <span class="ds-micro">duration.* ↔ duration.*-reduced</span></h4>
        <table class="sm__table">
          <thead>
            <tr>
              <th>令牌</th>
              <th>时长</th>
              <th>reduced 令牌</th>
              <th>reduced 值</th>
              <th>对应关系</th>
              <th>后端说明</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="d in durations" :key="d.base.path">
              <td class="ds-mono">{{ d.base.path }}</td>
              <td class="ds-num">{{ d.base.value }}</td>
              <td class="ds-mono">{{ d.reduced ? `${d.base.path}-reduced` : '—' }}</td>
              <td class="ds-num">{{ d.reduced ?? '—' }}</td>
              <td class="ds-small">
                <span v-if="d.reduced" class="sm__ok">动效可关闭（reduced 已令牌化）</span>
                <span v-else class="sm__warn">缺 -reduced 配对</span>
              </td>
              <td class="ds-small sm__dim">{{ rowOf(d.base.path)?.description ?? '—' }}</td>
            </tr>
          </tbody>
        </table>
        <p v-if="orphanReduced.length" class="ds-small sm__warn">
          有 {{ orphanReduced.length }} 条 <span class="ds-mono">*-reduced</span> 找不到基准配对：
          <span v-for="o in orphanReduced" :key="o.path" class="ds-mono">{{ o.path }}</span>
        </p>
      </div>

      <!-- 3) 缓动与复合 transition -->
      <div class="ds-stack ds-gap-3">
        <h4 class="ds-h4">缓动 <span class="ds-micro">ease.* · 数组原文</span></h4>
        <table class="sm__table">
          <thead>
            <tr>
              <th>令牌</th>
              <th>后端返回的数组原文</th>
              <th>CSS 语法包装（数字未改）</th>
              <th>CSS 变量</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="e in eases" :key="e.path">
              <td class="ds-mono">{{ e.path }}</td>
              <td class="ds-mono">{{ e.value }}</td>
              <td class="ds-mono sm__dim">{{ cubicBezierCss(e.value) || '（不是 [a,b,c,d] 数组，原样使用）' }}</td>
              <td class="ds-mono sm__dim">{{ cssVarName(e.path) }}</td>
            </tr>
          </tbody>
        </table>

        <h4 class="ds-h4">复合 transition <span class="ds-micro">transition.* · {{ transitions.length }} 条</span></h4>
        <table class="sm__table">
          <thead>
            <tr>
              <th>令牌</th>
              <th>值摘要（valueSummary）</th>
              <th>成分（别名按后端有效值查表）</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="t in transitions" :key="t.path">
              <td class="ds-mono">{{ t.path }}</td>
              <td class="ds-mono sm__value">{{ valueSummary(t) }}</td>
              <td>
                <div v-for="p in partsOf(t)" :key="p.key" class="ds-small">
                  <span class="ds-mono">{{ p.key }}</span> = <span class="ds-mono">{{ p.raw }}</span>
                  <span v-if="p.resolved" class="sm__dim">→ 查表 <span class="ds-mono">{{ p.resolved }}</span></span>
                </div>
                <span v-if="!partsOf(t).length" class="ds-small sm__dim">value 不是 JSON 对象</span>
              </td>
            </tr>
            <tr v-if="!transitions.length">
              <td colspan="3" class="ds-small sm__dim">无 transition.* 令牌</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 4) 播放条：只切换"取哪条后端令牌"，不做任何换算 -->
      <div class="sm__player ds-surface-2">
        <h4 class="ds-h4">播放条（值全部来自上方令牌）</h4>
        <div class="ds-row ds-gap-4 ds-wrap">
          <label class="ds-row ds-gap-2">
            <span class="ds-micro">duration</span>
            <select v-model="durationPath" class="ds-input">
              <option v-for="d in durations" :key="d.base.path" :value="d.base.path">{{ d.base.path }}（{{ d.base.value }}）</option>
            </select>
          </label>
          <label class="ds-row ds-gap-2">
            <span class="ds-micro">ease</span>
            <select v-model="easePath" class="ds-input">
              <option v-for="e in eases" :key="e.path" :value="e.path">{{ e.path }} {{ e.value }}</option>
            </select>
          </label>
          <label class="ds-row ds-gap-2">
            <span class="ds-micro">模拟 prefers-reduced-motion</span>
            <input v-model="simulateReduced" type="checkbox" />
          </label>
          <button class="ds-btn" type="button" @click="moved = !moved">{{ moved ? '移回' : '移动' }}</button>
        </div>
        <p class="ds-small sm__dim">
          当前生效：<span class="ds-mono">{{ activeDuration?.path ?? '（无可用 duration）' }}</span> =
          <strong>{{ activeDuration?.value ?? '—' }}</strong> ·
          <span class="ds-mono">{{ currentEase?.path ?? '（无 ease）' }}</span> =
          <span class="ds-mono">{{ currentEase?.value ?? '—' }}</span>
          <span v-if="simulateReduced && !activeDuration" class="sm__warn">该 duration 没有 -reduced 配对，因此本次不加时长（界面不代算）。</span>
        </p>
        <div class="sm__track">
          <span class="sm__cube" :class="{ 'sm__cube--moved': moved }" :style="playerStyle()"></span>
        </div>
        <p class="ds-micro sm__dim">
          开关只决定读 <span class="ds-mono">duration.X</span> 还是 <span class="ds-mono">duration.X-reduced</span>——两个都是后端原文，界面无插值。
        </p>
      </div>
    </template>
  </section>
</template>

<style scoped>
.sm__error {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.sm__dim {
  color: var(--ds-fg-3);
}
.sm__warn {
  color: var(--ds-warning);
}
.sm__ok {
  color: var(--ds-success);
}
.sm__table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.sm__table th {
  text-align: left;
  color: var(--ds-fg-3);
  font-weight: var(--ds-fw-medium);
  border-bottom: 1px solid var(--ds-border-1);
  padding: var(--ds-space-2);
  white-space: nowrap;
}
.sm__table td {
  padding: var(--ds-space-2);
  border-bottom: 1px solid var(--ds-border-1);
  vertical-align: middle;
}
.sm__table--inner {
  margin: var(--ds-space-2) 0;
  font-size: var(--ds-fs-micro);
}
.sm__value {
  overflow-wrap: anywhere;
  color: var(--ds-fg-2);
  max-width: 42ch;
}
.sm__ops {
  text-align: right;
  white-space: nowrap;
}
.sm__detail {
  background: var(--ds-surface-2);
}
.sm__box {
  display: inline-block;
  width: var(--ds-space-8);
  height: var(--ds-space-8);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
}
.sm__player {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
  padding: var(--ds-space-4);
}
.sm__track {
  display: flex;
  padding: var(--ds-space-4);
  border: 1px dashed var(--ds-border-2);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-1);
}
.sm__cube {
  display: block;
  width: var(--ds-space-7);
  height: var(--ds-space-7);
  background: var(--ds-color-primary);
  border-radius: var(--ds-radius-sm);
}
.sm__cube--moved {
  transform: translateX(var(--ds-space-9));
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
