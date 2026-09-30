<script setup lang="ts">
/**
 * 主题实验室（FR2/FR17；对应 AC3/AC17 的前端面）。
 *
 * 本面板要回答的只有一个问题：**这个主题到底改变了什么**。
 * 后端把主题建模成"模式轴上的一个取值"（`DesignTheme` + 该主题相对共享层的覆盖行），
 * 有效值 = 共享层 ∪ 覆盖层按 `Path` 合并 —— 所以"新建主题"不铺任何令牌，令牌是 generate 的 `themes` 参数写的。
 *
 * 三条不可让的约束：
 * 1. **mode 是轴不是另一套令牌**：切主题走 `state.setTheme`（重读 effective），界面绝不自己合并两套值；
 *    密度轴（compact）改的是 space/radius/shadow 的取值方向，不该被当配色切换用，所以列表按 modeKind 分组显示。
 * 2. **生效要拿数据证明**：不写"切换成功"这种自夸文案，而是逐项比 `effective(target)` 与 `effective('light')`
 *    的 `value`，把**不同的那一条条**摊出来；两边都有 `semantic.surface-bg` 就并排出色块。
 * 3. **只读为主**：后端没有删除主题的端点（`api.ts` 里就没有），界面上也不做出"看起来能删"的入口。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { api, type EffectiveToken, type Theme } from '../api'
import { ApiError } from '../http'
import { currentProject, loadProjects, setTheme, themeCode, themes, unauthorized } from '../state'
import { isColorTheme, valueSummary } from '../design/derive'
import PanelState from '../components/PanelState.vue'

/** 基线固定 light：后端 `SemanticResolver.IsDark` 也是以 light/compact 为亮方向锚点的 */
const BASE_THEME = 'light'
const SURFACE_BG = 'semantic.surface-bg'
/** 明细一次摊 200 条足够看出"主题确实生效"，全量比对交给令牌工作台，不在这页打爆 DOM */
const DIFF_LIMIT = 200
/** `modeKind` 的封闭取值（后端 ThemeModeKinds）；自由输入会写出一个没人认的轴，所以只给下拉 */
const MODE_KINDS = ['color', 'density', 'brand']

const counts = ref<Record<number, number | undefined>>({})
const sharedCount = ref<number | null>(null)
const countsBusy = ref(false)
const countsError = ref('')

/* ------------------------------------------------------------------ */
/* 覆盖计数：每主题的令牌行数（themeId=0 是跨主题共享层）                */
/* ------------------------------------------------------------------ */
async function loadCounts(): Promise<void> {
  const p = currentProject.value
  if (!p) {
    counts.value = {}
    sharedCount.value = null
    return
  }
  countsBusy.value = true
  countsError.value = ''
  try {
    const list = themes.value
    const pages = await Promise.all(list.map((t) => api.listTokens(p.id, { themeId: t.id, pageSize: 2000 })))
    const next: Record<number, number> = {}
    list.forEach((t, i) => {
      next[t.id] = pages[i].total
    })
    counts.value = next
    // pageSize 被后端 clamp 到 2000：总数取服务端 total，不取本页条数，避免"恰好 2000"看着像截断
    sharedCount.value = (await api.listTokens(p.id, { themeId: 0, pageSize: 2000 })).total
  } catch (err) {
    countsError.value = err instanceof ApiError ? `${err.status} ${err.message}` : String(err)
  } finally {
    countsBusy.value = false
  }
}

const colorThemes = computed(() => themes.value.filter((t) => isColorTheme(t.modeKind)))
const densityThemes = computed(() => themes.value.filter((t) => t.modeKind === 'density'))

function overrideOf(t: Theme): string {
  const n = counts.value[t.id]
  if (n === undefined) return countsBusy.value ? '读取中…' : '—'
  return n === 0 ? '0 条 · 空主题（只有共享层）' : `${n} 条覆盖`
}

async function preview(t: Theme): Promise<void> {
  await setTheme(t.code)
}

/* ------------------------------------------------------------------ */
/* 新建主题（只建行，不铺令牌）                                          */
/* ------------------------------------------------------------------ */
const tCode = ref('')
const tName = ref('')
const tMode = ref('color')
const tDefault = ref(false)
const adding = ref(false)
const themeError = ref('')
const themeHint = ref('')

const CODE_RE = /^[a-z0-9][a-z0-9-]{0,31}$/
const canAdd = computed(() => !!currentProject.value && !adding.value && CODE_RE.test(tCode.value.trim()))

async function submitTheme(): Promise<void> {
  const p = currentProject.value
  if (!p || !canAdd.value) return
  adding.value = true
  themeError.value = ''
  themeHint.value = ''
  try {
    const created = await api.addTheme(p.id, {
      code: tCode.value.trim().toLowerCase(),
      name: tName.value.trim() || undefined,
      modeKind: tMode.value,
      isDefault: tDefault.value,
    })
    // 重新拉数（主题表以库为准）；新建主题不含任何令牌行，effective 未变，故不必 invalidate
    themes.value = await api.listThemes(p.id)
    themeHint.value = `已新建主题 ${created.code}（modeKind=${created.modeKind}，覆盖 0 条）—— 要让它有颜色，去「项目与生成」把它的 code 加进 themes 再生成`
    tCode.value = ''
    tName.value = ''
    tDefault.value = false
  } catch (err) {
    // 后端原文（如「主题 x 已存在于项目 N」= 409）必须原样可见
    themeError.value = err instanceof ApiError ? `${err.status} ${err.message}` : String(err)
  } finally {
    adding.value = false
  }
}

/* ------------------------------------------------------------------ */
/* 与 light 的差异（主题生效的证据）                                     */
/* ------------------------------------------------------------------ */
interface DiffRow {
  path: string
  tier: string
  type: string
  base: string
  target: string
  baseHex: string
  targetHex: string
}

const diffTarget = ref('')
const diffRows = ref<DiffRow[]>([])
const diffTotal = ref(0)
const diffCounts = ref<{ target: number; base: number } | null>(null)
const diffBg = ref<{ base: string; target: string } | null>(null)
const diffBusy = ref(false)
const diffError = ref('')
const diffHint = ref('')

const diffCandidates = computed(() => themes.value.filter((t) => t.code !== BASE_THEME))

/** 从差异块选定目标主题并立刻比对（模板里不写多语句，保持"一个动作一个入口"） */
function compare(code: string): void {
  diffTarget.value = code
  void runDiff()
}

async function runDiff(): Promise<void> {
  const p = currentProject.value
  const target = diffTarget.value
  if (!p || !target || diffBusy.value) return
  diffBusy.value = true
  diffError.value = ''
  diffHint.value = ''
  try {
    const [tv, bv] = await Promise.all([api.effective(p.id, target), api.effective(p.id, BASE_THEME)])
    const baseMap = new Map<string, EffectiveToken>()
    for (const t of bv.items) baseMap.set(t.path, t)
    const targetMap = new Map<string, EffectiveToken>()
    for (const t of tv.items) targetMap.set(t.path, t)
    const rows: DiffRow[] = []
    for (const t of tv.items) {
      const b = baseMap.get(t.path)
      if (b && b.value === t.value) continue
      rows.push({
        path: t.path,
        tier: t.tier,
        type: t.type,
        base: b ? valueSummary(b) : '—（只有目标主题有）',
        target: valueSummary(t),
        baseHex: b?.colorHex ?? '',
        targetHex: t.colorHex ?? '',
      })
    }
    for (const t of bv.items) {
      if (targetMap.has(t.path)) continue
      rows.push({
        path: t.path,
        tier: t.tier,
        type: t.type,
        base: valueSummary(t),
        target: '—（目标主题没有这行，会回落到共享层）',
        baseHex: t.colorHex ?? '',
        targetHex: '',
      })
    }
    diffTotal.value = rows.length
    diffRows.value = rows.slice(0, DIFF_LIMIT)
    diffCounts.value = { target: tv.count, base: bv.count }
    const bgT = targetMap.get(SURFACE_BG)
    const bgB = baseMap.get(SURFACE_BG)
    diffBg.value = bgT && bgB ? { base: bgB.value, target: bgT.value } : null
    diffHint.value = rows.length
      ? `${target} 与 ${BASE_THEME} 有 ${rows.length} 条取值不同（下面列前 ${diffRows.value.length} 条）—— 主题不是装饰，它真的改了有效值`
      : `${target} 与 ${BASE_THEME} 的有效值完全一致：该主题没有任何覆盖（多半是生成时没把 ${target} 放进 themes）`
  } catch (err) {
    diffRows.value = []
    diffTotal.value = 0
    diffBg.value = null
    diffCounts.value = null
    diffError.value = err instanceof ApiError ? `${err.status} ${err.message}` : String(err)
  } finally {
    diffBusy.value = false
  }
}

function resetDiff(): void {
  diffRows.value = []
  diffTotal.value = 0
  diffBg.value = null
  diffCounts.value = null
  diffHint.value = ''
  diffError.value = ''
  diffTarget.value = themeCode.value !== BASE_THEME ? themeCode.value : ''
}

onMounted(() => {
  if (!currentProject.value) void loadProjects()
  resetDiff()
  void loadCounts()
})
watch([() => currentProject.value?.id, () => themes.value.length], () => {
  resetDiff()
  void loadCounts()
})
</script>

<template>
  <section class="tl">
    <div class="ds-section-title">
      <h3 class="ds-h3">主题实验室</h3>
      <span class="ds-small">
        mode 是轴不是另一套令牌：主题只存"相对共享层的覆盖"，有效值 = 共享层 + 覆盖层。本页只读为主，不做删除（后端没有该端点）。
      </span>
    </div>

    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState
      v-else-if="!currentProject"
      state="empty"
      title="没有选中设计系统项目"
      hint="先到「项目与生成」选一个项目 —— 主题是项目下的轴，离开项目没有主题可读。"
    />

    <template v-else>
      <div class="tl__head ds-row ds-wrap ds-gap-3">
        <span class="ds-small">
          项目 <strong>{{ currentProject.code }}</strong> · 共享层
          <strong class="ds-num">{{ sharedCount ?? '…' }}</strong> 条（所有主题共用的底）
        </span>
        <span class="ds-micro">
          <button class="ds-link" type="button" :disabled="countsBusy" @click="loadCounts">{{ countsBusy ? '计数中…' : '重新计数' }}</button>
          · 当前预览主题 <strong>{{ themeCode }}</strong>
        </span>
      </div>

      <p v-if="countsError" class="tl__error" role="alert">后端原文：{{ countsError }}</p>

      <div class="tl__cols">
        <!-- 色向 / 品牌轴 -->
        <div class="tl__tablewrap ds-surface">
          <h4 class="ds-h4">色向与品牌主题（modeKind = color / brand）</h4>
          <PanelState v-if="!colorThemes.length" state="empty" title="该项目没有色向主题" hint="后端建项目时会自动配 light/dark/high-contrast；看不到说明库里这个项目被手工清过，去「项目与生成」新建主题或用 generate 的 themes 铺令牌。" />
          <table v-else class="tl__table">
            <thead>
              <tr>
                <th>code</th>
                <th>名称</th>
                <th>modeKind</th>
                <th>默认</th>
                <th>baseThemeId</th>
                <th>覆盖令牌</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="t in colorThemes" :key="t.code" :class="{ 'tl__row--on': t.code === themeCode }">
                <td class="ds-mono">{{ t.code }}</td>
                <td>{{ t.name || '—' }}</td>
                <td class="ds-small">{{ t.modeKind }}</td>
                <td class="ds-small">{{ t.isDefault ? '是' : '' }}</td>
                <td class="ds-num">{{ t.baseThemeId === 0 ? '0（共享层）' : t.baseThemeId }}</td>
                <td class="ds-small">{{ overrideOf(t) }}</td>
                <td class="tl__ops">
                  <button class="ds-mini" type="button" :disabled="t.code === themeCode" @click="preview(t)">
                    {{ t.code === themeCode ? '预览中' : '切预览' }}
                  </button>
                  <button class="ds-mini" type="button" :disabled="diffBusy || t.code === BASE_THEME" @click="compare(t.code)">比对</button>
                </td>
              </tr>
            </tbody>
          </table>

          <h4 class="ds-h4 tl__sub">密度主题（modeKind = density：改的是尺度取值方向，不是配色）</h4>
          <PanelState v-if="!densityThemes.length" state="empty" title="没有密度轴主题" hint="后端建项目时默认给一个 compact；若为空，用下面的表单补一个（modeKind 选 density）。" />
          <table v-else class="tl__table">
            <thead>
              <tr>
                <th>code</th>
                <th>名称</th>
                <th>默认</th>
                <th>覆盖令牌</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="t in densityThemes" :key="t.code" :class="{ 'tl__row--on': t.code === themeCode }">
                <td class="ds-mono">{{ t.code }}</td>
                <td>{{ t.name || '—' }}</td>
                <td class="ds-small">{{ t.isDefault ? '是' : '' }}</td>
                <td class="ds-small">{{ overrideOf(t) }}</td>
                <td class="tl__ops">
                  <button class="ds-mini" type="button" :disabled="t.code === themeCode" @click="preview(t)">
                    {{ t.code === themeCode ? '预览中' : '切预览' }}
                  </button>
                  <button class="ds-mini" type="button" :disabled="diffBusy || t.code === BASE_THEME" @click="compare(t.code)">比对</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- 新建主题 -->
        <form class="tl__new ds-surface-2" @submit.prevent="submitTheme">
          <h4 class="ds-h4">新建主题（只建轴，不铺令牌）</h4>
          <label class="tl__field">
            <span class="ds-micro">code</span>
            <input v-model="tCode" class="ds-input" type="text" placeholder="如 dark-brand-a" autocomplete="off" />
            <span class="ds-micro tl__note">小写字母/数字/短横线；同项目重复后端回 409</span>
          </label>
          <label class="tl__field">
            <span class="ds-micro">name（可空 = 用 code）</span>
            <input v-model="tName" class="ds-input" type="text" autocomplete="off" />
          </label>
          <label class="tl__field">
            <span class="ds-micro">modeKind（封闭取值）</span>
            <select v-model="tMode" class="ds-input">
              <option v-for="m in MODE_KINDS" :key="m" :value="m">{{ m }}</option>
            </select>
          </label>
          <label class="tl__check">
            <input v-model="tDefault" type="checkbox" />
            <span>设为默认主题（后端会把其它主题的 isDefault 清掉）</span>
          </label>
          <button class="ds-btn" type="submit" :disabled="!canAdd">{{ adding ? '写入中…' : '新建主题（POST themes）' }}</button>
          <p v-if="themeHint" class="tl__hint">{{ themeHint }}</p>
          <p v-if="themeError" class="tl__error" role="alert">后端原文：{{ themeError }}</p>
          <p class="ds-micro tl__note">
            说明：mode 是轴不是另一套令牌 —— 新主题建成时覆盖 0 条，它读到的全是共享层；要有自己的颜色，去「项目与生成」把它的 code 加进 generate 的 themes。
          </p>
        </form>
      </div>

      <!-- 差异证据 -->
      <div class="tl__diff ds-surface">
        <div class="tl__head">
          <h4 class="ds-h4">与 {{ BASE_THEME }} 的差异（主题生效的证据）</h4>
          <span class="ds-micro">基线固定为 {{ BASE_THEME }}：后端判明暗也是以它为锚</span>
        </div>
        <div class="ds-row ds-wrap ds-gap-3">
          <label class="tl__field tl__field--inline">
            <span class="ds-micro">目标主题</span>
            <select v-model="diffTarget" class="ds-input">
              <option value="">（选一个）</option>
              <option v-for="t in diffCandidates" :key="t.code" :value="t.code">{{ t.code }}（{{ t.modeKind }}）</option>
            </select>
          </label>
          <button class="ds-btn" type="button" :disabled="!diffTarget || diffBusy" @click="runDiff">{{ diffBusy ? '比对中…' : '逐项比对' }}</button>
        </div>
        <p class="ds-micro tl__note">比的是两次 tokens/effective 的解析结果（共享层 + 覆盖层、别名已解），不是原始行里的 value 字段。</p>
        <p v-if="diffError" class="tl__error" role="alert">后端原文：{{ diffError }}</p>
        <p v-else-if="diffHint" class="tl__hint">{{ diffHint }}</p>

        <div v-if="diffBg" class="tl__bg">
          <div class="tl__bg-cell">
            <span class="tl__swatch" :style="{ background: diffBg.base }" aria-hidden="true"></span>
            <span class="ds-mono">{{ BASE_THEME }} · {{ diffBg.base }}</span>
          </div>
          <div class="tl__bg-cell">
            <span class="tl__swatch" :style="{ background: diffBg.target }" aria-hidden="true"></span>
            <span class="ds-mono">{{ diffTarget }} · {{ diffBg.target }}</span>
          </div>
          <span class="ds-small">{{ SURFACE_BG }} —— 正文色的判级基准，两边并排即可看出主题换了底。</span>
        </div>

        <p v-if="diffCounts" class="ds-small">有效令牌条数：{{ BASE_THEME }} {{ diffCounts.base }} · 目标 {{ diffCounts.target }}</p>

        <table v-if="diffRows.length" class="tl__table">
          <thead>
            <tr>
              <th>路径</th>
              <th>层级</th>
              <th>{{ BASE_THEME }}</th>
              <th>目标主题</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in diffRows" :key="r.path">
              <td class="ds-mono">{{ r.path }}<span class="ds-micro tl__type"> {{ r.type }}</span></td>
              <td class="ds-small">{{ r.tier }}</td>
              <td>
                <span v-if="r.baseHex" class="tl__swatch" :style="{ background: r.baseHex }" aria-hidden="true"></span>
                <span class="ds-mono">{{ r.base }}</span>
              </td>
              <td>
                <span v-if="r.targetHex" class="tl__swatch" :style="{ background: r.targetHex }" aria-hidden="true"></span>
                <span class="ds-mono">{{ r.target }}</span>
              </td>
            </tr>
          </tbody>
        </table>
        <p v-if="diffTotal > diffRows.length" class="ds-micro tl__note">只列前 {{ diffRows.length }} 条（共 {{ diffTotal }} 条不同）；全量看「令牌工作台」。</p>
      </div>
    </template>
  </section>
</template>

<style scoped>
.tl {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-5);
}
.tl__head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--ds-space-3);
  flex-wrap: wrap;
  margin-bottom: var(--ds-space-3);
}
.tl__cols {
  display: grid;
  grid-template-columns: minmax(0, 1.7fr) minmax(260px, 1fr);
  gap: var(--ds-space-4);
  align-items: start;
}
@media (max-width: 1100px) {
  .tl__cols {
    grid-template-columns: 1fr;
  }
}
.tl__tablewrap,
.tl__new,
.tl__diff {
  padding: var(--ds-space-4);
  min-width: 0;
}
.tl__sub {
  margin-top: var(--ds-space-5);
}
.tl__table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.tl__table th {
  text-align: left;
  color: var(--ds-fg-3);
  font-weight: var(--ds-fw-medium);
  border-bottom: 1px solid var(--ds-border-1);
  padding: 6px var(--ds-space-2);
  white-space: nowrap;
}
.tl__table td {
  padding: 6px var(--ds-space-2);
  border-bottom: 1px solid var(--ds-border-1);
  vertical-align: middle;
}
.tl__row--on td {
  background: color-mix(in oklab, var(--ds-color-primary) 10%, transparent);
}
.tl__ops {
  white-space: nowrap;
  text-align: right;
}
.tl__type {
  margin-left: 4px;
}
.tl__field {
  display: flex;
  flex-direction: column;
  gap: 2px;
  margin-bottom: var(--ds-space-3);
  min-width: 0;
}
.tl__field--inline {
  margin-bottom: 0;
}
.tl__check {
  display: flex;
  align-items: flex-start;
  gap: 6px;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-2);
  margin-bottom: var(--ds-space-3);
  cursor: pointer;
}
.tl__note {
  color: var(--ds-fg-4);
  text-transform: none;
  letter-spacing: 0;
  margin-top: var(--ds-space-2);
}
.tl__bg {
  display: flex;
  align-items: center;
  gap: var(--ds-space-4);
  flex-wrap: wrap;
  padding: var(--ds-space-3);
  margin: var(--ds-space-3) 0;
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-1);
}
.tl__bg-cell {
  display: flex;
  align-items: center;
  gap: var(--ds-space-2);
}
.tl__swatch {
  display: inline-block;
  width: 26px;
  height: 18px;
  border-radius: var(--ds-radius-xs);
  border: 1px solid var(--ds-border-2);
  margin-right: 6px;
  vertical-align: -3px;
}
.tl__bg .tl__swatch {
  width: 40px;
  height: 26px;
  margin-right: 0;
}
.tl__error {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.tl__hint {
  color: var(--ds-success);
  font-size: var(--ds-fs-small);
}
.ds-input {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-3);
  min-width: 0;
}
.ds-btn {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-4);
  cursor: pointer;
}
.ds-btn:disabled,
.ds-mini:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}
.ds-mini {
  font: inherit;
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-2);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 2px 8px;
  margin-left: 4px;
  cursor: pointer;
}
</style>
