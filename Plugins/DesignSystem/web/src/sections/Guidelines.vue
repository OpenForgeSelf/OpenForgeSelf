<script setup lang="ts">
/**
 * 第 15 个工作台区：UX 规范（M3）。
 *
 * 三条不可让的口径，写在这里也写在后端：
 * 1. **数值不落地**：正文与规则文本里出现的 `space.6` 之类的路径，值一律由后端 `GuidelineRenderer` 现查，
 *    界面只读 `tokenValues` 显示 chip，绝不在前端再查一遍令牌 —— 那会给"这条规范说多少"写第二份真相。
 * 2. **编辑读原文、显示看括注**：输入框绑的是 `*Raw`（库里原文），只读展示才用括注后的文本。
 *    把括注写回输入框，等于把数字存进库里（下次令牌改值就成了旧数字）。
 * 3. **归档是状态不是删除**：本插件没有任何删除能力，所以这里只有「归档 / 恢复」，
 *    且两个动作都是"点一下就改变可见状态"→ 必须页内二次确认（不使用原生对话框，见 AC7 守卫）。
 *
 * 词表（分类 / 级别）读 `meta.guidelineCategories / meta.guidelineLevels`，前端不另列一份。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { api, type Guideline, type GuidelinePatch } from '../api'
import { ApiError } from '../http'
import { currentProject, meta, supports, unauthorized, type LoadState } from '../state'
import PanelState from '../components/PanelState.vue'

interface DraftRule {
  id: string
  level: string
  text: string
}

interface Draft {
  code: string
  title: string
  summary: string
  body: string
  category: string
  status: string
  rules: DraftRule[]
  /** 库里原文的 UpdatedAt：保存时作为 expectUpdatedAt 送回去，别人先改过就 409，不覆盖前人 */
  stamp: string
  source: string
  isNew: boolean
}

const rows = ref<Guideline[]>([])
const state = ref<LoadState>('idle')
const err = ref('')
const note = ref('')
const busy = ref(false)
const showArchived = ref(false)
const categoryFilter = ref('')
const keyword = ref('')
const selectedCode = ref('')
const draft = ref<Draft | null>(null)
/** 页内二次确认：null = 无待确认动作 */
const pending = ref<{ kind: 'generate' | 'archive' | 'restore' | 'create'; code?: string } | null>(null)
const overwriteManual = ref(false)
const newCode = ref('')

const categories = computed(() => meta.value?.guidelineCategories ?? [])
const levels = computed(() => meta.value?.guidelineLevels ?? [])
const ready = computed(() => !!currentProject.value && supports('guidelines'))

const visible = computed(() =>
  rows.value
    .filter((g) => !categoryFilter.value || g.category === categoryFilter.value)
    .filter((g) => !keyword.value.trim() || hits(g, keyword.value.trim()))
    .slice()
    .sort((a, b) => a.sortOrder - b.sortOrder || (a.code < b.code ? -1 : 1)),
)

const selected = computed(() => rows.value.find((g) => g.code === selectedCode.value) ?? null)
const archivedCount = computed(() => rows.value.filter((g) => g.status === 'archived').length)
const dirty = computed(() => {
  const d = draft.value
  const s = selected.value
  if (!d || !s) return false
  return (
    d.title !== s.title || d.summary !== s.summary || d.body !== s.bodyRaw || d.category !== s.category ||
    JSON.stringify(d.rules) !== JSON.stringify(s.rules.map((r) => ({ id: r.id, level: r.level, text: r.textRaw })))
  )
})

function hits(g: Guideline, q: string): boolean {
  const hay = [g.code, g.title, g.summary, g.bodyRaw, ...g.rules.map((r) => r.textRaw)].join('\n').toLowerCase()
  return hay.includes(q.toLowerCase())
}

async function load(): Promise<void> {
  const p = currentProject.value
  if (!p) {
    rows.value = []
    state.value = 'idle'
    return
  }
  state.value = 'loading'
  err.value = ''
  try {
    rows.value = await api.listGuidelines(p.id, { status: showArchived.value ? 'all' : undefined })
    state.value = 'ready'
    if (!rows.value.some((g) => g.code === selectedCode.value)) select(rows.value[0]?.code ?? '')
  } catch (e) {
    state.value = 'error'
    err.value = describe(e)
  }
}

function select(code: string): void {
  selectedCode.value = code
  const g = rows.value.find((x) => x.code === code)
  pending.value = null
  if (!g) {
    draft.value = null
    return
  }
  draft.value = {
    code: g.code,
    title: g.title ?? '',
    summary: g.summary ?? '',
    // 编辑必须落在原文上：括注过的文本写回库里就等于把数字存进规范（口径 2）
    body: g.bodyRaw ?? '',
    category: g.category,
    status: g.status,
    rules: g.rules.map((r) => ({ id: r.id, level: r.level, text: r.textRaw })),
    stamp: g.updatedAt,
    source: g.source,
    isNew: false,
  }
}

function startCreate(): void {
  pending.value = { kind: 'create' }
}

function confirmCreate(): void {
  const code = newCode.value.trim()
  if (!code) {
    note.value = '先填规范标识（小写 kebab，如 layout-grid）'
    return
  }
  draft.value = {
    code,
    title: '',
    summary: '',
    body: '',
    category: categories.value[0] ?? '',
    // 级别缺省交给后端（GuidelineRepository.NormalizeRule），前端不猜一个
    status: 'adopted',
    rules: [{ id: '', level: levels.value[0] ?? '', text: '' }],
    stamp: '',
    source: 'manual',
    isNew: true,
  }
  selectedCode.value = code
  pending.value = null
  note.value = `新规范 ${code} 还没保存：填完标题与规则后点「保存规范」才会落库`
}

function addRule(): void {
  const d = draft.value
  if (!d) return
  d.rules.push({ id: '', level: levels.value[0] ?? '', text: '' })
}

function removeRule(index: number): void {
  const d = draft.value
  if (!d) return
  d.rules.splice(index, 1)
}

async function save(): Promise<void> {
  const p = currentProject.value
  const d = draft.value
  if (!p || !d) return
  busy.value = true
  err.value = ''
  note.value = ''
  try {
    const patch: GuidelinePatch = {
      title: d.title.trim(),
      summary: d.summary.trim(),
      body: d.body,
      category: d.category || undefined,
      status: d.status,
      rules: d.rules
        .filter((r) => r.text.trim() !== '')
        .map((r) => ({ id: r.id || undefined, level: r.level || undefined, text: r.text.trim() })),
    }
    if (!d.isNew && d.stamp) patch.expectUpdatedAt = d.stamp
    const saved = await api.saveGuideline(p.id, d.code, patch)
    note.value = d.isNew ? `已新增规范 ${saved.code}` : `已保存规范 ${saved.code}`
    await load()
    select(saved.code)
  } catch (e) {
    if (e instanceof ApiError && e.status === 409) {
      err.value = `${e.message}（别人在你之后改过这条：点「放弃修改」重新载入后再编辑，不要直接覆盖）`
    } else err.value = describe(e)
  } finally {
    busy.value = false
  }
}

async function doGenerate(): Promise<void> {
  const p = currentProject.value
  if (!p || !pending.value) return
  busy.value = true
  err.value = ''
  try {
    const r = await api.generateGuidelines(p.id, overwriteManual.value)
    const parts = [
      `新建 ${r.created.length}`,
      `已存在未动 ${r.skipped.length}`,
      `手改受保护 ${r.skippedProtected.length}`,
      `覆盖 ${r.overwritten.length}`,
    ]
    note.value = `重新生成完成：${parts.join(' / ')}（总数 ${r.total}）`
    if (!overwriteManual.value && r.skippedProtected.length)
      note.value += `；手改过的 ${r.skippedProtected.join('、')} 未被覆盖 —— 要覆盖请勾「覆盖手改规范」`
    await load()
  } catch (e) {
    err.value = describe(e)
  } finally {
    busy.value = false
    pending.value = null
  }
}

async function doArchive(code: string): Promise<void> {
  const p = currentProject.value
  if (!p) return
  busy.value = true
  err.value = ''
  try {
    await api.archiveGuideline(p.id, code)
    note.value = `已归档 ${code}：数据仍在库里，勾选「显示已归档」可恢复`
    await load()
    select(code)
  } catch (e) {
    err.value = describe(e)
  } finally {
    busy.value = false
    pending.value = null
  }
}

async function doRestore(code: string): Promise<void> {
  const p = currentProject.value
  if (!p) return
  busy.value = true
  err.value = ''
  try {
    // 恢复 = PUT 回 adopted（本插件没有删除，所以也没有"反删除"接口）
    await api.saveGuideline(p.id, code, { status: 'adopted' })
    note.value = `已恢复 ${code}`
    await load()
    select(code)
  } catch (e) {
    err.value = describe(e)
  } finally {
    busy.value = false
    pending.value = null
  }
}

function discard(): void {
  select(selectedCode.value)
  note.value = '已放弃本地未保存的修改'
}

function describe(e: unknown): string {
  if (e instanceof ApiError) return `${e.status} ${e.message}`
  return e instanceof Error ? e.message : String(e)
}

function sourceLabel(s: string): string {
  return s === 'manual' ? '手改（重新生成会保护）' : '生成器产出'
}

onMounted(() => {
  void load()
})
watch(() => currentProject.value?.id, () => void load())
watch(showArchived, () => void load())
</script>

<template>
  <section class="gl" data-guidelines>
    <header class="gl__head">
      <div>
        <h3 class="ds-h3">UX 规范</h3>
        <p class="ds-micro">
          共 {{ rows.length }} 条（含已归档 {{ archivedCount }} 条）· 正文只写令牌路径，数值由后端按取值主题
          <b class="ds-mono">{{ selected?.valueTheme || '默认色彩主题' }}</b> 现查；切到别的主题不改规范内容。
        </p>
      </div>
      <div class="gl__ops">
        <label class="ds-small ds-row ds-gap-2">
          <input v-model="showArchived" type="checkbox" data-show-archived />
          <span>显示已归档</span>
        </label>
        <button class="gl__btn" type="button" data-new-guideline :disabled="!ready || busy" @click="startCreate">
          新建规范
        </button>
        <button class="gl__btn" type="button" data-regenerate :disabled="!ready || busy" @click="pending = pending?.kind === 'generate' ? null : { kind: 'generate' }">
          重新生成默认规范
        </button>
      </div>
    </header>

    <div v-if="pending?.kind === 'create'" class="gl__confirm" role="group" aria-label="新建规范确认">
      <label class="ds-small ds-row ds-gap-2" for="gl-new-code">
        <span>规范标识（小写 kebab，项目内唯一）</span>
        <input id="gl-new-code" v-model="newCode" class="ds-input ds-input--narrow" aria-label="规范标识" placeholder="layout-grid" @keyup.enter="confirmCreate" />
      </label>
      <div class="ds-row ds-gap-2">
        <button class="gl__btn gl__btn--primary" type="button" data-confirm-create :disabled="busy" @click="confirmCreate">新建</button>
        <button class="gl__btn" type="button" @click="pending = null">取消</button>
      </div>
      <span class="ds-micro">新建只在工作台里建草稿：点「保存规范」才会落库（此时不会覆盖任何已有规范）。</span>
    </div>

    <div v-if="pending?.kind === 'generate'" class="gl__confirm" role="group" aria-label="重新生成确认">
      <span>重新生成只会补空：已有的规范不动、你手改过的更不动。要连手改的一起重写？</span>
      <label class="ds-small ds-row ds-gap-2">
        <input v-model="overwriteManual" type="checkbox" />
        <span>覆盖手改规范（不可逆地丢掉你的改动，建议先发布一版）</span>
      </label>
      <div class="ds-row ds-gap-2">
        <button class="gl__btn gl__btn--primary" type="button" data-confirm-generate :disabled="busy" @click="doGenerate">确认重新生成</button>
        <button class="gl__btn" type="button" @click="pending = null">取消</button>
      </div>
    </div>

    <p v-if="unauthorized" class="gl__note" role="status">未登录宿主：规范读不到后端数据，请先在宿主登录后再回来。</p>
    <p v-else-if="!ready" class="gl__note" role="status">
      {{ currentProject ? '当前后端版本不含规范能力（meta.capabilities 缺 guidelines）：这一区暂不可用，升级插件后即可。' : '先在「项目与生成」里选一个设计系统项目。' }}
    </p>

    <!-- 结果反馈放在 section 级而不是详情面板里：归档会把详情面板关掉，
         写在面板内的话「已归档」这句话连同面板一起消失＝点完没有任何回应。 -->
    <p v-if="note" class="gl__note" role="status">{{ note }}</p>
    <p v-if="err && state !== 'error'" class="gl__bad" role="alert">{{ err }}</p>

    <div v-if="state === 'loading'" class="gl__cols">
      <PanelState state="loading" />
    </div>
    <div v-else-if="state === 'error'" class="gl__cols">
      <PanelState state="error" :hint="err" />
    </div>
    <div v-else-if="state === 'ready' && !rows.length" class="gl__cols">
      <PanelState state="empty" title="这个项目还没有 UX 规范" hint="点「重新生成默认规范」按项目用途 / 行业 / 密度生成 14 条默认规范；生成后仍可逐条改。" />
    </div>

    <div v-else class="gl__cols">
      <div class="gl__col">
        <div class="gl__filters">
          <input v-model="keyword" class="ds-input" type="search" aria-label="规范关键词" placeholder="搜标识 / 标题 / 规则" />
          <select v-model="categoryFilter" class="ds-input ds-input--narrow" aria-label="规范分类筛选">
            <option value="">全部分类</option>
            <option v-for="c in categories" :key="c" :value="c">{{ c }}</option>
          </select>
        </div>
        <p class="ds-micro" role="status">列出 {{ visible.length }} / {{ rows.length }} 条</p>
        <ul class="gl__list">
          <li v-for="g in visible" :key="g.code">
            <button
              class="gl__item"
              :class="{ 'gl__item--on': g.code === selectedCode }"
              type="button"
              :data-guideline-code="g.code"
              :aria-current="g.code === selectedCode ? 'true' : undefined"
              @click="select(g.code)"
            >
              <span class="gl__item-title">{{ g.title || g.code }}</span>
              <span class="gl__item-meta">
                <span class="gl__pill">{{ g.categoryLabel }}</span>
                <span class="gl__pill gl__pill--muted">{{ g.rules.length }} 条规则</span>
                <span v-if="g.status === 'archived'" class="gl__pill gl__pill--archived">已归档</span>
                <span v-if="g.brokenRefs.length" class="gl__pill gl__pill--bad" data-broken-refs>断链 {{ g.brokenRefs.length }}</span>
                <span class="gl__pill" :data-source="g.source">{{ sourceLabel(g.source) }}</span>
              </span>
            </button>
          </li>
        </ul>
      </div>

      <!-- 新建草稿没有对应的库内行（selected 为空），但编辑器必须出现——否则「新建规范」点了个寂寞 -->
      <div v-if="draft && (selected || draft.isNew)" class="gl__col gl__col--wide">
        <div class="gl__detail">
          <p class="ds-micro">
            标识 <b class="ds-mono">{{ draft.code }}</b>
            <template v-if="!draft.isNew"> · 来源 {{ sourceLabel(draft.source) }} · 生成器 v{{ selected.generatorVersion }} · 种子 <span class="ds-mono">{{ selected.generatorSeed }}</span></template>
          </p>
          <label class="gl__label" for="gl-title">标题</label>
          <input id="gl-title" v-model="draft.title" class="ds-input" aria-label="规范标题" maxlength="100" />
          <label class="gl__label" for="gl-summary">摘要</label>
          <input id="gl-summary" v-model="draft.summary" class="ds-input" aria-label="规范摘要" maxlength="500" />
          <label class="gl__label" for="gl-body">正文（写令牌路径，不要写数字）</label>
          <textarea id="gl-body" v-model="draft.body" class="ds-input gl__body" aria-label="规范正文" rows="6" />
          <label class="gl__label" for="gl-category">分类</label>
          <select id="gl-category" v-model="draft.category" class="ds-input ds-input--narrow" aria-label="规范分类">
            <option v-for="c in categories" :key="c" :value="c">{{ c }}</option>
          </select>

          <h4 class="ds-h4">规则（{{ draft.rules.length }} 条）</h4>
          <div v-for="(r, i) in draft.rules" :key="i" class="gl__rule" :data-rule-level="r.level || 'unset'">
            <select v-model="r.level" class="ds-input ds-input--narrow" aria-label="规则级别">
              <option v-for="l in levels" :key="l" :value="l">{{ l }}</option>
              <option v-if="r.level && !levels.includes(r.level)" :value="r.level">{{ r.level }}</option>
            </select>
            <textarea v-model="r.text" class="ds-input gl__rule-text" rows="2" aria-label="规则文本" placeholder="例：卡片内边距只用 `space.5`，不自写像素" />
            <button class="gl__mini" type="button" aria-label="删除这条规则" @click="removeRule(i)">删</button>
          </div>
          <button class="gl__btn" type="button" data-add-rule @click="addRule">添加规则</button>

          <template v-if="selected">
            <h4 class="ds-h4">引用令牌（当前值）</h4>
            <p v-if="!selected.tokenRefs.length" class="ds-micro">这条规范没有登记引用令牌：chip 与导出的引用令牌行都会是空的。</p>
            <div class="gl__chips">
              <span v-for="p in selected.tokenRefs" :key="p" class="gl__chip" data-token-chip>
                {{ p }} = {{ selected.tokenValues[p] || '（取值主题里没有这个令牌）' }}
              </span>
            </div>
            <p v-if="selected.brokenRefs.length" class="gl__bad" role="alert">
              这些引用在取值主题里取不到值（令牌被删或改名）：{{ selected.brokenRefs.join('、') }}
            </p>
          </template>
          <p v-else class="ds-micro">新建的规范还没有引用令牌登记：界面编辑的是标题 / 摘要 / 正文 / 规则，引用登记由生成器与工具写入。</p>

          <div class="gl__foot">
            <button class="gl__btn gl__btn--primary" type="button" data-save-guideline :disabled="busy || !draft.title.trim()" @click="save">
              保存规范
            </button>
            <button v-if="dirty" class="gl__btn" type="button" @click="discard">放弃修改</button>
            <button
              v-if="selected && selected.status !== 'archived'"
              class="gl__btn"
              type="button"
              data-archive
              :disabled="busy"
              @click="pending = pending?.kind === 'archive' ? null : { kind: 'archive', code: selected.code }"
            >
              归档
            </button>
            <button
              v-else-if="selected"
              class="gl__btn"
              type="button"
              data-restore
              :disabled="busy"
              @click="pending = pending?.kind === 'restore' ? null : { kind: 'restore', code: selected.code }"
            >
              恢复
            </button>
            <span v-if="dirty" class="ds-micro" role="status">有未保存的改动</span>
          </div>

          <div v-if="pending?.code" class="gl__confirm" role="group" aria-label="归档或恢复确认">
            <span v-if="pending.kind === 'archive'">
              归档 <b class="ds-mono">{{ pending.code }}</b> ？数据保留在库里，勾「显示已归档」可恢复；导出与交付不再带它。
            </span>
            <span v-else>恢复 <b class="ds-mono">{{ pending.code }}</b> 为「采用」状态？</span>
            <div class="ds-row ds-gap-2">
              <button
                class="gl__btn gl__btn--primary"
                type="button"
                :data-confirm="pending.kind"
                :disabled="busy"
                @click="pending.kind === 'archive' ? doArchive(pending.code!) : doRestore(pending.code!)"
              >
                确认
              </button>
              <button class="gl__btn" type="button" @click="pending = null">取消</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
/* 外壳一律用插件自己的 --ds-* 令牌（与 ReleaseBoard/BrandAssets 同一套），不引宿主品牌色 */
.gl {
  display: grid;
  gap: var(--ds-space-4);
}
.gl__head {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--ds-space-4);
}
.gl__ops {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--ds-space-3);
}
.gl__cols {
  display: grid;
  grid-template-columns: minmax(220px, 300px) minmax(0, 1fr);
  gap: var(--ds-space-6);
}
.gl__col {
  min-width: 0;
}
.gl__filters {
  display: flex;
  gap: var(--ds-space-2);
  margin-bottom: var(--ds-space-2);
}
.gl__list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: var(--ds-space-2);
  max-height: 60vh;
  overflow: auto;
}
.gl__item {
  width: 100%;
  display: grid;
  gap: var(--ds-space-1);
  text-align: left;
  font: inherit;
  font-size: var(--ds-fs-small);
  padding: var(--ds-space-2) var(--ds-space-3);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-1);
  color: var(--ds-fg-1);
  cursor: pointer;
}
.gl__item--on {
  border-color: var(--ds-color-primary);
  background: var(--ds-surface-2);
}
.gl__item-title {
  font-weight: var(--ds-fw-semibold);
  color: var(--ds-fg-1);
}
.gl__item-meta {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ds-space-1);
}
.gl__pill {
  font-size: var(--ds-fs-micro);
  padding: 0 var(--ds-space-2);
  border-radius: var(--ds-radius-pill);
  border: 1px solid var(--ds-border-1);
  color: var(--ds-fg-3);
}
.gl__pill--muted {
  border-style: dashed;
}
.gl__pill--archived {
  color: var(--ds-fg-4);
}
.gl__pill--bad {
  color: var(--ds-danger);
  border-color: var(--ds-danger);
}
.gl__detail {
  display: grid;
  gap: var(--ds-space-2);
  padding: var(--ds-space-5);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-lg);
  background: var(--ds-surface-1);
}
.gl__label {
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-3);
}
.gl__body {
  font-family: var(--ds-font-mono);
  resize: vertical;
}
.gl__rule {
  display: grid;
  grid-template-columns: 104px minmax(0, 1fr) 34px;
  gap: var(--ds-space-2);
  align-items: start;
}
.gl__rule-text {
  resize: vertical;
}
.gl__mini {
  font: inherit;
  padding: 4px 0;
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  background: var(--ds-surface-2);
  color: var(--ds-fg-3);
  cursor: pointer;
}
.gl__chips {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ds-space-2);
}
.gl__chip {
  font-family: var(--ds-font-mono);
  font-size: var(--ds-fs-micro);
  padding: 2px var(--ds-space-3);
  border-radius: var(--ds-radius-pill);
  border: 1px solid var(--ds-border-1);
  background: var(--ds-surface-2);
  color: var(--ds-fg-2);
}
.gl__foot {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--ds-space-3);
  margin-top: var(--ds-space-2);
}
.gl__btn {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-2);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-4);
  cursor: pointer;
}
.gl__btn--primary {
  color: var(--ds-surface-1);
  background: var(--ds-color-primary);
  border-color: var(--ds-color-primary);
}
.gl__btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.gl__confirm {
  display: grid;
  gap: var(--ds-space-3);
  padding: var(--ds-space-4);
  border: 1px dashed var(--ds-border-2);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-2);
}
.gl__note {
  margin: 0;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-3);
}
.gl__bad {
  margin: 0;
  font-size: var(--ds-fs-small);
  color: var(--ds-danger);
}
.ds-input {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-3);
}
.ds-input--narrow {
  width: auto;
}
@media (max-width: 900px) {
  .gl__cols {
    grid-template-columns: minmax(0, 1fr);
  }
  .gl__list {
    max-height: none;
  }
}
</style>
