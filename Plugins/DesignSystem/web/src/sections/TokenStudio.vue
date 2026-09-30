<script setup lang="ts">
/**
 * 令牌工作台（FR14/AC6/AC7）：全量令牌的读、改、别名跳转与退役。
 *
 * 三条不可让的约束：
 * 1. **显示的是"有效值"**（后端解析过别名链），不是原始 `{semantic.brand}` 字符串；
 * 2. **改完必须回读**（invalidate），界面自己证明写进去了，而不是弹个"成功"了事；
 * 3. **对比度等数字来自后端**（tokens/effective 已带 ratio/wcagLevel），前端一个都不算。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { api, type EffectiveToken, type Token } from '../api'
import { ApiError } from '../http'
import { currentProject, effective, effectiveState, invalidate, lastError, loadEffective, meta, themeCode, themes, unauthorized } from '../state'
import { aliasTarget, groupByRoot, matchesKeyword, ratioText, severityClass, tierRank, valueSummary, wcagBadge } from '../design/derive'
import PanelState from '../components/PanelState.vue'

const keyword = ref('')
const tierFilter = ref('')
const rootFilter = ref('')
const rows = ref<Map<string, Token>>(new Map())
const editing = ref<{ path: string; themeId: number; value: string } | null>(null)
const busy = ref(false)
const writeError = ref('')
const writeHint = ref('')

const items = computed<EffectiveToken[]>(() => effective.value?.items ?? [])

/** 层级词表（primitive/semantic/component）来自后端 /meta：排序与筛选下拉共用同一张表 */
const tiers = computed(() => meta.value?.tiers ?? [])

const roots = computed(() => [...new Set(items.value.map((t) => t.path.split('.')[0]))].sort())

const visible = computed(() => {
  const order = tiers.value
  const list = items.value
    .filter((t) => matchesKeyword(t.path, keyword.value))
    .filter((t) => !tierFilter.value || t.tier === tierFilter.value)
    .filter((t) => !rootFilter.value || t.path.startsWith(`${rootFilter.value}.`))
    .sort((a, b) => tierRank(a.tier, order) - tierRank(b.tier, order) || (a.path < b.path ? -1 : 1))
  return groupByRoot(list)
})

const diagnostics = computed(() => effective.value?.diagnostics ?? [])

/** 原始行按 `主题:路径` 索引；有效值里没有的元信息（生成器、生命周期、更新时间）从这里取 */
const currentThemeId = computed(() => themes.value.find((t) => t.code === themeCode.value)?.id ?? 0)

function rowOf(path: string): Token | undefined {
  return rows.value.get(`${currentThemeId.value}:${path}`) ?? rows.value.get(`0:${path}`)
}

async function loadRows(): Promise<void> {
  const p = currentProject.value
  if (!p) return
  try {
    const page = await api.listTokens(p.id, { pageSize: 2000 })
    const map = new Map<string, Token>()
    for (const r of page?.items ?? []) map.set(`${r.themeId}:${r.path}`, r)
    rows.value = map
  } catch (err) {
    lastError.value = err instanceof Error ? err.message : String(err)
  }
}

function startEdit(t: EffectiveToken): void {
  const row = rowOf(t.path)
  writeError.value = ''
  writeHint.value = ''
  editing.value = { path: t.path, themeId: row?.themeId ?? 0, value: t.value }
}

function cancelEdit(): void {
  editing.value = null
}

/** 保存到后端；409 与校验失败都必须把原因显示出来（操作失败必须可查） */
async function save(): Promise<void> {
  const p = currentProject.value
  const draft = editing.value
  if (!p || !draft) return
  busy.value = true
  writeError.value = ''
  try {
    const row = rowOf(draft.path)
    const res = await api.upsertToken(p.id, {
      path: draft.path,
      themeId: draft.themeId,
      value: draft.value,
      aliasPath: draft.value.startsWith('{') ? draft.value.slice(1, -1) : undefined,
      expectUpdatedAt: row?.updatedAt,
    })
    await Promise.all([invalidate(), loadRows()])
    writeHint.value = `已写入并回读：新建 ${res?.created ?? 0} / 更新 ${res?.updated ?? 0}`
    editing.value = null
  } catch (err) {
    writeError.value = err instanceof ApiError ? `${err.status} ${err.message}` : String(err)
  } finally {
    busy.value = false
  }
}

async function retire(t: EffectiveToken): Promise<void> {
  const p = currentProject.value
  if (!p) return
  if (!window.confirm(`退役 ${t.path}？（软删：保留记录，仅从发布快照中消失）`)) return
  try {
    await api.retireToken(p.id, rowOf(t.path)?.themeId ?? 0, t.path)
    await Promise.all([invalidate(), loadRows()])
  } catch (err) {
    writeError.value = err instanceof Error ? err.message : String(err)
  }
}

/** 别名跳转：把目标路径填进检索框，链路一眼可见（不做"隐藏跳转"） */
function followAlias(t: EffectiveToken): void {
  const target = aliasTarget(t)
  if (target) keyword.value = target
}

async function reload(): Promise<void> {
  await Promise.all([loadEffective(true), loadRows()])
}

watch(themeCode, () => void reload())
watch(() => currentProject.value?.id, () => void reload())
onMounted(() => {
  void reload()
})
</script>

<template>
  <section class="ts">
    <header class="ts__bar">
      <div class="ts__filters">
        <input v-model="keyword" class="ds-input" type="search" placeholder="按路径检索（如 brand / semantic / focus）" />
        <select v-model="tierFilter" class="ds-input ds-input--narrow" aria-label="层级">
          <option value="">全部层级</option>
          <option v-for="t in tiers" :key="t" :value="t">{{ t }}</option>
        </select>
        <select v-model="rootFilter" class="ds-input ds-input--narrow" aria-label="分组">
          <option value="">全部分组</option>
          <option v-for="r in roots" :key="r" :value="r">{{ r }}</option>
        </select>
      </div>
      <div class="ts__meta ds-micro">
        主题 <strong>{{ themeCode }}</strong> · 有效令牌 <strong>{{ items.length }}</strong>
        · <button class="ds-link" type="button" @click="reload">刷新</button>
      </div>
    </header>

    <p v-if="writeError" class="ts__error" role="alert">{{ writeError }}</p>
    <p v-if="writeHint" class="ts__hint">{{ writeHint }}</p>
    <p v-if="diagnostics.length" class="ts__error" role="alert">
      别名解析异常 {{ diagnostics.length }} 条：
      <span v-for="d in diagnostics.slice(0, 4)" :key="d.path" class="ts__diag">{{ d.path }}（{{ d.message }}）</span>
    </p>

    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState v-else-if="effectiveState === 'loading' && !items.length" state="loading" />
    <PanelState v-else-if="!currentProject" state="empty" title="还没有选中设计系统项目" hint="先到「项目」页新建一个项目，或在这里选一个已有项目。" />
    <PanelState v-else-if="!items.length" state="empty" title="该项目暂无令牌" hint="到「项目」页点「生成」，后端会按 oklch 色阶 + 对比度定向铺出三层令牌。" />

    <div v-else class="ts__groups">
      <div v-for="g in visible" :key="g.root" class="ts__group">
        <h4 class="ds-h4 ts__group-title">{{ g.root }}<span class="ds-micro"> · {{ g.items.length }}</span></h4>
        <table class="ts__table">
          <thead>
            <tr>
              <th>路径</th>
              <th>值</th>
              <th>来源</th>
              <th>对比度</th>
              <th>状态</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="t in g.items" :key="t.path" :class="{ 'ts__row--broken': !t.resolved }">
              <td class="ds-mono ts__path">{{ t.path }}</td>
              <td>
                <template v-if="editing && editing.path === t.path && editing.themeId === (rowOf(t.path)?.themeId ?? 0)">
                  <input v-model="editing.value" class="ds-input ts__editor" :disabled="busy" @keyup.esc="cancelEdit" @keyup.enter="save" />
                  <button class="ds-mini" :disabled="busy" @click="save">{{ busy ? '保存中…' : '保存' }}</button>
                  <button class="ds-mini" :disabled="busy" @click="cancelEdit">取消</button>
                </template>
                <template v-else>
                  <span v-if="t.type === 'color' && (t.colorHex || t.value)" class="ts__swatch" :style="{ background: t.colorHex || t.value }" aria-hidden="true"></span>
                  <span class="ds-mono">{{ valueSummary(t) }}</span>
                </template>
              </td>
              <td class="ds-micro">
                <button v-if="aliasTarget(t)" class="ds-link" type="button" @click="followAlias(t)">→ {{ aliasTarget(t) }}</button>
                <span v-else-if="t.sourcePath !== t.path">← {{ t.sourcePath }}</span>
                <span v-else>字面值</span>
              </td>
              <td :class="`wcag--${wcagBadge(t.contrastRatio).level}`">
                {{ ratioText(t.contrastRatio) }}
                <span v-if="t.wcagLevel" class="ds-micro">（{{ t.wcagLevel }}）</span>
              </td>
              <td class="ds-micro">
                <span v-if="rowOf(t.path)?.lifecycle === 'removed'" :class="`tag tag--${severityClass('warning')}`">已退役</span>
                <span v-else-if="rowOf(t.path)?.generator && rowOf(t.path)?.generator !== 'manual'" class="tag">{{ rowOf(t.path)?.generator }}</span>
                <span v-else class="tag tag--manual">人工</span>
              </td>
              <td class="ts__ops">
                <button v-if="!editing || editing.path !== t.path" class="ds-mini" @click="startEdit(t)">改</button>
                <button v-if="rowOf(t.path)" class="ds-mini ds-mini--danger" @click="retire(t)">退役</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<style scoped>
.ts {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-4);
}
.ts__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ds-space-4);
  flex-wrap: wrap;
}
.ts__filters {
  display: flex;
  gap: var(--ds-space-2);
  flex-wrap: wrap;
}
.ts__meta {
  color: var(--ds-fg-3);
}
.ts__error {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.ts__hint {
  color: var(--ds-success);
  font-size: var(--ds-fs-small);
}
.ts__diag {
  margin-right: var(--ds-space-2);
}
.ts__groups {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-5);
}
.ts__group-title {
  margin-bottom: var(--ds-space-2);
}
.ts__table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.ts__table th {
  text-align: left;
  color: var(--ds-fg-3);
  font-weight: var(--ds-fw-medium, 500);
  border-bottom: 1px solid var(--ds-border-1);
  padding: 6px var(--ds-space-2);
}
.ts__table td {
  padding: 6px var(--ds-space-2);
  border-bottom: 1px solid var(--ds-border-1);
  vertical-align: middle;
}
.ts__row--broken td {
  background: color-mix(in oklab, var(--ds-danger) 12%, transparent);
}
.ts__path {
  white-space: nowrap;
}
.ts__swatch {
  display: inline-block;
  width: 14px;
  height: 14px;
  border-radius: 3px;
  border: 1px solid var(--ds-border-2);
  margin-right: 6px;
  vertical-align: -2px;
}
.ts__ops {
  white-space: nowrap;
  text-align: right;
}
.ds-input {
  font: inherit;
  color: var(--ds-fg-1);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-3);
}
.ds-input--narrow {
  width: auto;
}
.ts__editor {
  width: 18ch;
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
.ds-mini--danger {
  color: var(--ds-danger);
}
.tag {
  font-size: var(--ds-fs-micro);
  padding: 1px 6px;
  border-radius: var(--ds-radius-pill);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
}
.tag--warning {
  color: var(--ds-warning);
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
