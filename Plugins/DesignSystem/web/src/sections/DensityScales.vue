<script setup lang="ts">
/**
 * 尺度与密度：间距 / 圆角 / 描边 / 断点 / 层级的可视化，以及"密度是一条轴"的实证。
 *
 * 硬约束：本页**不做任何尺度运算**。
 * - 条形的宽度、方块的圆角、边框的粗细都直接绑后端返回的字符串（`16px` 就是 16px），
 *   不是按比例缩放的示意条；
 * - 排序只是显示顺序（数值档名优先，其次契约里的命名序），不产生任何新数值；
 * - 密度对比各调一次 `tokens/effective`，同名令牌是否"有差异"由**字符串相等**判定，
 *   差多少由后端说了算，界面只陈述事实并解释这事实意味着什么（见页尾注）。
 *
 * 数据源：`tokens/effective`（值）+ `tokens`（原行的 description，effective 不带）。
 */
import { computed, onMounted, ref, shallowRef } from 'vue'
import { api, type EffectiveToken, type EffectiveView, type Token } from '../api'
import { ApiError } from '../http'
import { currentProject, effective, effectiveState, lastError, loadEffective, meta, themeCode, themes, unauthorized } from '../state'
import { compareSteps, cssVarName } from '../design/derive'
import PanelState from '../components/PanelState.vue'

const rowsLoading = ref(false)
const rowsError = ref('')
/** path → 原行（只为拿 description："3 号间距 = 基准 4px × 1.5" 是后端写的） */
const rawRows = shallowRef<Map<string, Token>>(new Map())

/** 密度对比：两个主题各打一次 effective */
const themeA = ref('')
const themeB = ref('')
const viewA = shallowRef<EffectiveView | null>(null)
const viewB = shallowRef<EffectiveView | null>(null)
const compareBusy = ref(false)
const compareError = ref('')

const items = computed<EffectiveToken[]>(() => effective.value?.items ?? [])

/** 前缀 → 后端词表键（`space.` → `space`）。表外的前缀（breakpoint / z-index）只有数值档，词表留空 */
function vocabularyFor(prefix: string): string[] {
  return meta.value?.scaleOrders?.[prefix.replace(/\.$/, '')] ?? []
}

/** 同一前缀下的令牌：数值档按数值、命名档按**后端词表**、其余按字典序（纯排序，无换算） */
function ordered(prefix: string, source?: EffectiveToken[]): EffectiveToken[] {
  const order = vocabularyFor(prefix)
  return [...(source ?? items.value)]
    .filter((t) => t.path.startsWith(prefix))
    .sort((a, b) => compareSteps(a.path, b.path, order))
}

const spaces = computed(() => ordered('space.'))
const radii = computed(() => ordered('radius.'))
const breakpoints = computed(() => ordered('breakpoint.'))
const zIndexes = computed(() => ordered('z-index.'))

/** 描边：契约文案是 border-width.*，后端 ScaleGenerators.Border 实际写的是 border.* —— 两者都试，显示库里真实路径 */
const borderGroup = computed(() => {
  const asContract = ordered('border-width.')
  return asContract.length ? { prefix: 'border-width.', list: asContract } : { prefix: 'border.', list: ordered('border.') }
})

const otherThemes = computed(() => themes.value.filter((t) => t.code !== themeCode.value))
const spaceA = computed(() => ordered('space.', viewA.value?.items ?? []))
const spaceB = computed(() => new Map(ordered('space.', viewB.value?.items ?? []).map((t) => [t.path, t.value])))

/** 逐行并排：是否相同只比字符串，不解释成因 */
const diffRows = computed(() =>
  spaceA.value.map((a) => {
    const vb = spaceB.value.get(a.path)
    return {
      path: a.path,
      valueA: a.value,
      valueB: vb ?? '（该主题无此令牌）',
      same: vb !== undefined && vb === a.value,
      missing: vb === undefined,
    }
  }),
)
const diffCount = computed(() => diffRows.value.filter((r) => !r.same).length)
const compared = computed(() => viewA.value !== null && viewB.value !== null && diffRows.value.length > 0)

function errText(err: unknown): string {
  return err instanceof ApiError ? `${err.status} ${err.message}` : err instanceof Error ? err.message : String(err)
}

/** 后端 description 原文；没有就留空由模板显示占位 */
function descOf(path: string): string {
  return rawRows.value.get(path)?.description ?? ''
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
  } catch (err) {
    rowsError.value = errText(err)
  } finally {
    rowsLoading.value = false
  }
}

async function fetchOne(which: 'A' | 'B'): Promise<void> {
  const p = currentProject.value
  const code = which === 'A' ? themeA.value : themeB.value
  if (!p || !code) return
  compareBusy.value = true
  compareError.value = ''
  try {
    const view = await api.effective(p.id, code)
    if (which === 'A') viewA.value = view
    else viewB.value = view
  } catch (err) {
    compareError.value = `${code}：${errText(err)}`
  } finally {
    compareBusy.value = false
  }
}

async function loadCompare(): Promise<void> {
  if (!themeA.value || !themeB.value) {
    compareError.value = '没有可对比的主题：先到「项目与生成」页为该项目添加主题（例如 modeKind=density 的 compact），再回来并排看。'
    return
  }
  await Promise.all([fetchOne('A'), fetchOne('B')])
}

async function refresh(): Promise<void> {
  await Promise.all([loadEffective(true), loadRows()])
  themeA.value = themeCode.value
  if (themeB.value === themeA.value) themeB.value = otherThemes.value[0]?.code ?? ''
  await loadCompare()
}

onMounted(async () => {
  await Promise.all([loadEffective(), loadRows()])
  themeA.value = themeCode.value
  themeB.value = themes.value.find((t) => t.modeKind === 'density')?.code ?? otherThemes.value[0]?.code ?? ''
  await loadCompare()
})
</script>

<template>
  <section class="dc ds-stack ds-gap-6">
    <header class="ds-section-title">
      <h3 class="ds-h3">尺度与密度</h3>
      <span class="ds-small">
        <span class="ds-mono">space.* / radius.* / border / breakpoint.* / z-index.*</span> 取自
        <span class="ds-mono">tokens/effective</span>（主题 <strong>{{ themeCode }}</strong>，共 {{ items.length }} 条有效值）·
        图形尺寸就是令牌值本身
      </span>
    </header>

    <p v-if="rowsError || compareError || lastError" class="dc__error" role="alert">{{ rowsError || compareError || lastError }}</p>
    <p v-else-if="rowsLoading" class="ds-small dc__dim">正在读取原行（取后端 description）…</p>
    <p v-else-if="compareBusy" class="ds-small dc__dim">正在读取密度对比所需的两个主题…</p>

    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState v-else-if="effectiveState === 'loading' && !items.length" state="loading" />
    <PanelState v-else-if="!currentProject" state="empty" title="还没有选中设计系统项目" hint="尺度令牌随项目生成；先选一个项目。" />
    <PanelState v-else-if="!spaces.length && !radii.length" state="empty" title="该项目没有尺度令牌" hint="后端生成会铺出 space./radius./border 三组 primitive；若为空说明该项目未生成，或生成器跳过了尺度层。" />

    <template v-else>
      <!-- 1) 间距 -->
      <div class="ds-stack ds-gap-3">
        <div class="ds-row ds-gap-3 ds-wrap">
          <h4 class="ds-h4">间距 <span class="ds-micro">space.* · {{ spaces.length }} 档 · 宽度 = 值本身（未按比例缩放）</span></h4>
          <button class="ds-link" type="button" @click="refresh">重新读取本库</button>
        </div>
        <table class="dc__table">
          <thead>
            <tr>
              <th>令牌</th>
              <th>值</th>
              <th>CSS 变量</th>
              <th>可视化</th>
              <th>后端说明</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="t in spaces" :key="t.path">
              <td class="ds-mono">{{ t.path }}</td>
              <td class="ds-num">{{ t.value }}</td>
              <td class="ds-mono dc__dim">{{ cssVarName(t.path) }}</td>
              <td><span class="dc__bar" :style="{ width: t.value }" :title="`${t.path} = ${t.value}`"></span></td>
              <td class="ds-small dc__dim">{{ descOf(t.path) || '—' }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 2) 圆角 -->
      <div class="ds-stack ds-gap-3">
        <h4 class="ds-h4">圆角 <span class="ds-micro">radius.* · {{ radii.length }} 档 · 方块圆角 = 值本身</span></h4>
        <div class="ds-row ds-gap-4 ds-wrap">
          <div v-for="t in radii" :key="t.path" class="dc__radius">
            <span class="dc__box" :style="{ borderRadius: t.value }" :title="`${t.path} = ${t.value}`"></span>
            <span class="ds-mono dc__small">{{ t.path }}</span>
            <span class="ds-num dc__small">{{ t.value }}</span>
          </div>
        </div>
      </div>

      <!-- 3) 描边宽度：真实边框；颜色走 currentColor，不新造设计值 -->
      <div class="ds-stack ds-gap-3">
        <h4 class="ds-h4">描边宽度 <span class="ds-micro">{{ borderGroup.prefix }}* · {{ borderGroup.list.length }} 档</span></h4>
        <p v-if="borderGroup.prefix === 'border.'" class="ds-small dc__dim">
          注：本项目的描边令牌实际落在 <span class="ds-mono">border.*</span>（后端 ScaleGenerators.Border），
          不是契约文案里的 <span class="ds-mono">border-width.*</span>——本页显示的是库里真实存在的路径。
        </p>
        <table class="dc__table">
          <thead>
            <tr>
              <th>令牌</th>
              <th>值</th>
              <th>真实边框</th>
              <th>后端说明</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="t in borderGroup.list" :key="t.path">
              <td class="ds-mono">{{ t.path }}</td>
              <td class="ds-num">{{ t.value }}</td>
              <td><span class="dc__bordered" :style="{ borderWidth: t.value }">边框示例</span></td>
              <td class="ds-small dc__dim">{{ descOf(t.path) || '—' }}</td>
            </tr>
            <tr v-if="!borderGroup.list.length">
              <td colspan="4" class="ds-small dc__dim">无 border / border-width 令牌</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 4) 密度对比：两主题各一次 effective，逐行比字面值 -->
      <div class="ds-stack ds-gap-3">
        <h4 class="ds-h4">
          密度对比
          <span class="ds-micro">
            {{ themeA }} vs {{ themeB || '（无第二个主题）' }} · space.* 差异 {{ diffCount }} / {{ diffRows.length }}
          </span>
        </h4>
        <div class="ds-row ds-gap-3 ds-wrap">
          <label class="ds-row ds-gap-2">
            <span class="ds-micro">主题 A</span>
            <select v-model="themeA" class="ds-input" @change="fetchOne('A')">
              <option v-for="t in themes" :key="t.code" :value="t.code">{{ t.name || t.code }}（{{ t.modeKind }}）</option>
            </select>
          </label>
          <label class="ds-row ds-gap-2">
            <span class="ds-micro">主题 B</span>
            <select v-model="themeB" class="ds-input" :disabled="!otherThemes.length" @change="fetchOne('B')">
              <option v-if="!otherThemes.length" value="">（只有一个主题）</option>
              <option v-for="t in otherThemes" :key="t.code" :value="t.code">{{ t.name || t.code }}（{{ t.modeKind }}）</option>
            </select>
          </label>
          <button class="ds-btn" type="button" :disabled="compareBusy" @click="loadCompare">并排读一次</button>
        </div>

        <table v-if="compared" class="dc__table">
          <thead>
            <tr>
              <th>令牌</th>
              <th>{{ themeA }}</th>
              <th>{{ themeB }}</th>
              <th>结论</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in diffRows" :key="r.path" :class="{ 'dc__row--diff': !r.same }">
              <td class="ds-mono">{{ r.path }}</td>
              <td class="ds-num">{{ r.valueA }}</td>
              <td class="ds-num">{{ r.valueB }}</td>
              <td class="ds-small">
                <span v-if="r.missing">B 主题缺该令牌</span>
                <span v-else-if="!r.same" class="dc__diff">值不同</span>
                <span v-else class="dc__dim">相同</span>
              </td>
            </tr>
          </tbody>
        </table>
        <p v-if="compared && diffCount === 0" class="ds-small dc__dim">
          两列的 <span class="ds-mono">space.*</span> 完全相同：说明该项目的间距只写在共享层（由生成时的
          <span class="ds-mono">density</span> 参数决定），主题覆盖层并未按"密度轴"给出第二套尺度。
          密度是轴不是配色——要让它真的成为轴，需在「项目与生成」页用不同 density 参数分别生成，并由后端把结果落到对应主题层。
        </p>
        <p v-else-if="compared" class="ds-small dc__dim">
          有 {{ diffCount }} 行取值不同：这些差异全部来自后端（不同主题读到的字面值），本页未做任何插值。
        </p>
      </div>

      <!-- 5) 断点与层级 -->
      <div class="ds-stack ds-gap-3">
        <h4 class="ds-h4">断点与层级 <span class="ds-micro">breakpoint.* · z-index.*</span></h4>
        <p class="ds-small dc__dim">
          <span class="ds-mono">breakpoint.*</span> 是给响应式布局用的：媒体查询的 min-width 刻度，消费方是栅格与断点工具类；
          <span class="ds-mono">z-index.*</span> 是给层叠上下文用的：弹层、吸顶、对话框的叠放顺序刻度，消费方是组件样式。
          两者都是"给写组件的人对齐用的标尺"，不参与换肤取色。
        </p>
        <div class="dc__two">
          <table class="dc__table">
            <thead>
              <tr>
                <th>令牌</th>
                <th>值</th>
                <th>后端说明</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="t in breakpoints" :key="t.path">
                <td class="ds-mono">{{ t.path }}</td>
                <td class="ds-num">{{ t.value }}</td>
                <td class="ds-small dc__dim">{{ descOf(t.path) || '—' }}</td>
              </tr>
              <tr v-if="!breakpoints.length">
                <td colspan="3" class="ds-small dc__dim">无 breakpoint.* 令牌</td>
              </tr>
            </tbody>
          </table>
          <table class="dc__table">
            <thead>
              <tr>
                <th>令牌</th>
                <th>值</th>
                <th>CSS 变量</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="t in zIndexes" :key="t.path">
                <td class="ds-mono">{{ t.path }}</td>
                <td class="ds-num">{{ t.value }}</td>
                <td class="ds-mono dc__dim">{{ cssVarName(t.path) }}</td>
              </tr>
              <tr v-if="!zIndexes.length">
                <td colspan="3" class="ds-small dc__dim">无 z-index.* 令牌</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </template>
  </section>
</template>

<style scoped>
.dc__error {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.dc__dim {
  color: var(--ds-fg-3);
}
.dc__small {
  font-size: var(--ds-fs-micro);
}
.dc__table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.dc__table th {
  text-align: left;
  color: var(--ds-fg-3);
  font-weight: var(--ds-fw-medium);
  border-bottom: 1px solid var(--ds-border-1);
  padding: var(--ds-space-2);
  white-space: nowrap;
}
.dc__table td {
  padding: var(--ds-space-2);
  border-bottom: 1px solid var(--ds-border-1);
  vertical-align: middle;
}
.dc__bar {
  display: block;
  height: var(--ds-space-4);
  background: var(--ds-color-primary);
  border-radius: var(--ds-radius-xs);
}
.dc__row--diff td {
  background: color-mix(in oklab, var(--ds-warning) 14%, transparent);
}
.dc__diff {
  color: var(--ds-warning);
  font-weight: var(--ds-fw-medium);
}
.dc__radius {
  display: flex;
  flex-direction: column;
  gap: 2px;
  align-items: flex-start;
}
.dc__box {
  display: block;
  width: var(--ds-space-8);
  height: var(--ds-space-8);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-2);
}
.dc__bordered {
  display: inline-block;
  padding: var(--ds-space-2) var(--ds-space-3);
  border-color: currentColor;
  border-style: solid;
  border-radius: var(--ds-radius-sm);
  color: var(--ds-fg-2);
}
.dc__two {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: var(--ds-space-4);
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
