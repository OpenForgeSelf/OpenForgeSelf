<template>
  <section class="specs" data-sems="specs">
    <div class="specs__head">
      <span class="specs__title">规格（{{ specs.length }}）</span>
      <button type="button" class="specs__btn specs__btn--primary" @click="openCreate">新建规格</button>
    </div>
    <p class="specs__note">只追加版本，已有版本不会被覆盖。已登记只表示写下了用例标识，不表示用例已通过。</p>

    <div v-if="phase === 'loading'" class="specs__hint">加载规格中…</div>
    <div v-else-if="phase === 'error'" class="specs__hint specs__hint--error">
      <p>加载失败：{{ error }}</p>
      <button type="button" class="specs__btn" @click="reload">重试</button>
    </div>
    <div v-else-if="phase === 'empty'" class="specs__hint">
      <p>还没有规格。</p>
      <p>点「新建规格」写第一版。标题和正文必填，验收标准可以以后再补。</p>
    </div>
    <ul v-else class="specs__list">
      <li v-for="spec in specs" :key="spec.specId">
        <button
          type="button"
          class="specs__row"
          :class="{ 'specs__row--on': spec.specId === selectedId }"
          @click="openSpec(spec.specId)"
        >
          <span class="specs__row-title">{{ spec.title }}</span>
          <span class="specs__row-meta">v{{ spec.version }} · {{ fmt(spec.createdAt) }}</span>
        </button>
      </li>
    </ul>

    <div v-if="selectedId" class="specs__detail">
      <p v-if="detailLoading" class="specs__hint">读取这一版…</p>
      <p v-if="detailError" class="specs__hint specs__hint--error">{{ detailError }}</p>
      <template v-if="viewing">
        <p v-if="viewing.version !== latestVersion" class="specs__banner">
          正在查看 v{{ viewing.version }}。列表上的版本号是最新版。
        </p>
        <h3 class="specs__detail-title">{{ viewing.title }} <span>v{{ viewing.version }}</span></h3>
        <p v-if="viewing.author || viewing.changeNote" class="specs__meta">
          <span v-if="viewing.author">作者 {{ viewing.author }}</span>
          <span v-if="viewing.changeNote">备注 {{ viewing.changeNote }}</span>
        </p>
        <pre class="specs__content">{{ viewing.content }}</pre>

        <h4 class="specs__h">验收标准</h4>
        <p v-if="!viewing.criteria || viewing.criteria.length === 0" class="specs__hint">这一版没有验收标准。</p>
        <ul v-else class="specs__criteria">
          <li v-for="item in coverageItems" :key="item.id">
            <span class="specs__cid">{{ item.id }}</span>
            <span class="specs__ctext">{{ item.text }}</span>
            <span class="specs__cstate">{{ coveragePhrase(item.linked, item.cases) }}</span>
          </li>
        </ul>

        <h4 class="specs__h">关联变更</h4>
        <p v-if="links.length === 0" class="specs__hint">还没有关联。可以记一条 commit、PR 或 Issue。</p>
        <ul v-else class="specs__links">
          <li v-for="(link, idx) in links" :key="idx">v{{ link.version }} · {{ link.kind }} · {{ link.ref }}</li>
        </ul>

        <div class="specs__ops">
          <label class="specs__ver">
            <span>版本号</span>
            <input v-model.trim="versionQuery" type="text" inputmode="numeric" placeholder="正整数" />
          </label>
          <button type="button" class="specs__btn" @click="readVersion">读取该版本</button>
          <button type="button" class="specs__btn" @click="openVersion">追加版本</button>
          <button type="button" class="specs__btn" @click="openLink">关联变更</button>
          <button
            type="button"
            class="specs__btn"
            :disabled="!viewing.criteria || viewing.criteria.length === 0"
            :title="!viewing.criteria || viewing.criteria.length === 0 ? '这一版没有验收标准' : '登记用例标识'"
            @click="openCase"
          >登记用例</button>
        </div>
      </template>
    </div>
    <p v-else-if="phase === 'list'" class="specs__hint">点一条规格查看正文、验收标准和关联。</p>

    <form class="specs__find" @submit.prevent="runFind">
      <span class="specs__find-label">按标识反查</span>
      <select v-model="findKind" aria-label="反查类型">
        <option value="commit">commit</option>
        <option value="PR">PR</option>
        <option value="Issue">Issue</option>
      </select>
      <input v-model.trim="findRef" type="text" aria-label="反查标识" placeholder="commit、PR 或 Issue 标识" />
      <button type="submit" class="specs__btn" :disabled="finding">{{ finding ? '查询中…' : '反查' }}</button>
    </form>
    <p v-if="findError" class="specs__hint specs__hint--error">{{ findError }}</p>
    <p v-else-if="findHits && findHits.length === 0" class="specs__hint">没有匹配的关联。</p>
    <ul v-else-if="findHits" class="specs__links">
      <li v-for="(hit, idx) in findHits" :key="idx">
        <button type="button" class="specs__hit" @click="openSpec(hit.specId, hit.version)">
          {{ hit.specId }} · v{{ hit.version }} · {{ hit.kind }} · {{ hit.ref }}
        </button>
      </li>
    </ul>

    <div v-if="dialog" class="specdlg">
      <div class="specdlg__mask" @click="closeDialog"></div>
      <div class="specdlg__box" role="dialog" aria-modal="true">
        <h3 class="specdlg__title">{{ dialogTitle }}</h3>

        <label v-if="dialog === 'create'" class="specdlg__field">
          <span>标题 *</span>
          <input v-model.trim="form.title" type="text" />
        </label>
        <label v-if="dialog === 'create' || dialog === 'version'" class="specdlg__field">
          <span>正文 *</span>
          <textarea v-model="form.content" rows="5"></textarea>
        </label>
        <label v-if="dialog === 'create' || dialog === 'version'" class="specdlg__field">
          <span>作者</span>
          <input v-model.trim="form.author" type="text" />
        </label>
        <label v-if="dialog === 'create' || dialog === 'version'" class="specdlg__field">
          <span>备注</span>
          <input v-model.trim="form.note" type="text" />
        </label>
        <label v-if="dialog === 'version'" class="specdlg__check">
          <input v-model="form.keepPrevious" type="checkbox" />
          <span>沿用上一版验收标准</span>
        </label>
        <label v-if="dialog === 'create' || (dialog === 'version' && !form.keepPrevious)" class="specdlg__field">
          <span>验收标准</span>
          <textarea v-model="form.criteriaText" rows="3" placeholder="每行一条：AC1 能看到标题"></textarea>
        </label>

        <label v-if="dialog === 'link' || dialog === 'case'" class="specdlg__field">
          <span>版本号 *</span>
          <input v-model.trim="form.version" type="text" inputmode="numeric" />
        </label>
        <label v-if="dialog === 'link'" class="specdlg__field">
          <span>类型 *</span>
          <select v-model="form.kind">
            <option value="commit">commit</option>
            <option value="PR">PR</option>
            <option value="Issue">Issue</option>
          </select>
        </label>
        <label v-if="dialog === 'link'" class="specdlg__field">
          <span>标识 *</span>
          <input v-model.trim="form.ref" type="text" placeholder="如 abc123 或 #12" />
        </label>
        <label v-if="dialog === 'case'" class="specdlg__field">
          <span>验收标准 *</span>
          <select v-model="form.criterionId">
            <option v-for="c in viewing?.criteria ?? []" :key="c.id" :value="c.id">{{ c.id }} {{ c.text }}</option>
          </select>
        </label>
        <label v-if="dialog === 'case'" class="specdlg__field">
          <span>用例标识 *</span>
          <input v-model.trim="form.caseRef" type="text" placeholder="如 sems.spec.ts::规格" />
        </label>

        <p v-if="formError" class="specdlg__err">{{ formError }}</p>
        <div class="specdlg__foot">
          <button type="button" class="specs__btn" @click="closeDialog">取消</button>
          <button type="button" class="specs__btn specs__btn--primary" :disabled="saving" @click="submit">
            {{ saving ? '保存中…' : '保存' }}
          </button>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * 项目卡片内的规格区。只调用已有 SpecsController，不新增路由。
 * 列表是各规格的最新版；旧版靠「读取该版本」按已知版本号打开。
 */
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { errorMessage } from './confirmOps'
import {
  addSpecVersion,
  createSpec,
  findSpecsByRef,
  getSpec,
  getSpecCoverage,
  linkSpec,
  linkSpecCase,
  listSpecLinks,
  listSpecs,
} from './specApi'
import {
  buildCaseBody,
  buildCreateBody,
  buildLinkBody,
  buildVersionBody,
  coveragePhrase,
  listPhase,
} from './specOps'
import type { SpecCoverage, SpecLink, SpecVersion } from './types'

const props = defineProps<{ projectId: number }>()

const specs = ref<SpecVersion[]>([])
const loading = ref(true)
const error = ref('')
const selectedId = ref('')
const viewing = ref<SpecVersion | null>(null)
const coverage = ref<SpecCoverage | null>(null)
const links = ref<SpecLink[]>([])
const detailError = ref('')
const detailLoading = ref(false)
const versionQuery = ref('')

type DialogKind = 'create' | 'version' | 'link' | 'case'
const dialog = ref<DialogKind | null>(null)
const saving = ref(false)
const formError = ref('')
const form = ref(emptyForm())

const findKind = ref('commit')
const findRef = ref('')
const findHits = ref<SpecLink[] | null>(null)
const findError = ref('')
const finding = ref(false)

let loadGen = 0

const phase = computed(() => listPhase(loading.value, error.value, specs.value.length))
const latestVersion = computed(() => specs.value.find((s) => s.specId === selectedId.value)?.version ?? 0)
const dialogTitle = computed(() => {
  if (dialog.value === 'create') return '新建规格'
  if (dialog.value === 'version') return '追加版本'
  if (dialog.value === 'link') return '关联变更'
  return '登记用例'
})

const coverageItems = computed(() => {
  if (coverage.value && coverage.value.version === viewing.value?.version) return coverage.value.items
  return (viewing.value?.criteria ?? []).map((c) => ({ id: c.id, text: c.text, cases: [] as string[], linked: false }))
})

function emptyForm() {
  return {
    title: '',
    content: '',
    author: '',
    note: '',
    criteriaText: '',
    keepPrevious: true,
    version: '',
    kind: 'commit',
    ref: '',
    criterionId: '',
    caseRef: '',
  }
}

function fmt(value?: string): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  const p = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${p(date.getMonth() + 1)}-${p(date.getDate())} ${p(date.getHours())}:${p(date.getMinutes())}`
}

async function reload() {
  const gen = ++loadGen
  if (specs.value.length === 0) loading.value = true
  error.value = ''
  try {
    const list = await listSpecs(props.projectId)
    if (gen !== loadGen) return
    specs.value = list
  } catch (e) {
    if (gen !== loadGen) return
    error.value = errorMessage(e)
  } finally {
    if (gen === loadGen) loading.value = false
  }
}

async function openSpec(specId: string, version?: number) {
  selectedId.value = specId
  detailLoading.value = true
  detailError.value = ''
  try {
    const [got, cov, foundLinks] = await Promise.all([
      getSpec(props.projectId, specId, version),
      getSpecCoverage(props.projectId, specId, version),
      listSpecLinks(props.projectId, specId),
    ])
    viewing.value = got
    coverage.value = cov
    links.value = foundLinks
    versionQuery.value = String(got.version)
  } catch (e) {
    detailError.value = errorMessage(e)
  } finally {
    detailLoading.value = false
  }
}

function closeDialog() {
  if (saving.value) return
  dialog.value = null
  formError.value = ''
}

function openCreate() {
  form.value = emptyForm()
  formError.value = ''
  dialog.value = 'create'
}

function openVersion() {
  form.value = { ...emptyForm(), keepPrevious: true, version: String(viewing.value?.version ?? '') }
  formError.value = ''
  dialog.value = 'version'
}

function openLink() {
  form.value = { ...emptyForm(), version: String(viewing.value?.version ?? ''), kind: 'commit' }
  formError.value = ''
  dialog.value = 'link'
}

function openCase() {
  form.value = {
    ...emptyForm(),
    version: String(viewing.value?.version ?? ''),
    criterionId: viewing.value?.criteria?.[0]?.id ?? '',
  }
  formError.value = ''
  dialog.value = 'case'
}

async function readVersion() {
  if (!selectedId.value) return
  const version = Number(versionQuery.value)
  if (!Number.isInteger(version) || version < 1) {
    detailError.value = 'version 必须是正整数'
    return
  }
  await openSpec(selectedId.value, version)
}

async function runFind() {
  findError.value = ''
  findHits.value = null
  if (!findKind.value.trim() || !findRef.value.trim()) {
    findError.value = 'kind 与 ref 必填'
    return
  }
  finding.value = true
  try {
    findHits.value = await findSpecsByRef(props.projectId, findKind.value.trim(), findRef.value.trim())
  } catch (e) {
    findError.value = errorMessage(e)
  } finally {
    finding.value = false
  }
}

async function submit() {
  if (saving.value || !dialog.value) return
  formError.value = ''
  if (dialog.value === 'create') {
    const built = buildCreateBody(form.value)
    if (!built.ok) {
      formError.value = built.message
      return
    }
    saving.value = true
    try {
      const spec = await createSpec(props.projectId, built.body)
      dialog.value = null
      ElMessage.success(`已创建规格「${spec.title}」v${spec.version}`)
      await reload()
      await openSpec(spec.specId, spec.version)
    } catch (e) {
      formError.value = errorMessage(e)
    } finally {
      saving.value = false
    }
    return
  }

  if (dialog.value === 'version') {
    const built = buildVersionBody(form.value)
    if (!built.ok) {
      formError.value = built.message
      return
    }
    saving.value = true
    try {
      const spec = await addSpecVersion(props.projectId, selectedId.value, built.body)
      dialog.value = null
      ElMessage.success(`已追加 v${spec.version}`)
      await reload()
      await openSpec(spec.specId, spec.version)
    } catch (e) {
      formError.value = errorMessage(e)
    } finally {
      saving.value = false
    }
    return
  }

  if (dialog.value === 'link') {
    const built = buildLinkBody(form.value)
    if (!built.ok) {
      formError.value = built.message
      return
    }
    saving.value = true
    try {
      const link = await linkSpec(props.projectId, selectedId.value, built.body)
      dialog.value = null
      ElMessage.success(`已关联 ${link.kind} ${link.ref}`)
      links.value = await listSpecLinks(props.projectId, selectedId.value)
    } catch (e) {
      formError.value = errorMessage(e)
    } finally {
      saving.value = false
    }
    return
  }

  const built = buildCaseBody(form.value)
  if (!built.ok) {
    formError.value = built.message
    return
  }
  saving.value = true
  try {
    await linkSpecCase(props.projectId, selectedId.value, built.body)
    dialog.value = null
    ElMessage.success('已登记用例标识')
    coverage.value = await getSpecCoverage(props.projectId, selectedId.value, built.body.version)
    if (viewing.value && viewing.value.version !== built.body.version) {
      await openSpec(selectedId.value, built.body.version)
    }
  } catch (e) {
    formError.value = errorMessage(e)
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  void reload()
})
</script>

<style scoped>
.specs {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.specs__head,
.specs__ops,
.specs__find,
.specdlg__foot {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.specs__head {
  justify-content: space-between;
}

.specs__title,
.specs__detail-title,
.specdlg__title {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--fg-primary, var(--el-text-color-primary));
}

.specs__detail-title span,
.specs__row-meta,
.specs__meta,
.specs__note,
.specs__hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.specs__note,
.specs__hint,
.specs__meta {
  margin: 0;
}

.specs__hint--error,
.specdlg__err {
  color: var(--el-color-danger);
}

.specs__list,
.specs__criteria,
.specs__links {
  list-style: none;
  margin: 0;
  padding: 0;
}

.specs__row,
.specs__hit {
  width: 100%;
  text-align: left;
  border: 1px solid var(--el-border-color);
  border-radius: var(--radius-sm, 6px);
  background: transparent;
  color: inherit;
  cursor: pointer;
  padding: 8px 10px;
}

.specs__row--on {
  border-color: var(--gold-400, var(--el-color-primary));
}

.specs__row-title,
.specs__cid {
  font-weight: 600;
}

.specs__row {
  display: flex;
  justify-content: space-between;
  gap: 8px;
}

.specs__criteria li,
.specs__links li {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  padding: 6px 0;
  border-bottom: 1px solid var(--el-border-color);
  font-size: 13px;
}

.specs__content {
  margin: 0;
  white-space: pre-wrap;
  font-family: var(--font-mono, var(--el-font-family-mono, monospace));
  font-size: 12px;
  line-height: 1.5;
  color: var(--fg-regular, var(--el-text-color-regular));
}

.specs__banner {
  margin: 0;
  padding: 6px 8px;
  border: 1px solid var(--el-border-color);
  border-radius: var(--radius-sm, 6px);
  font-size: 12px;
  color: var(--gold-400, var(--el-color-warning));
}

.specs__h {
  margin: 4px 0 0;
  font-size: 12px;
  font-weight: 600;
}

.specs__ver {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
}

.specs__btn,
.specs__find input,
.specs__find select,
.specs__ver input,
.specdlg__field input,
.specdlg__field textarea,
.specdlg__field select {
  border: 1px solid var(--el-border-color);
  border-radius: var(--radius-md, 6px);
  background: transparent;
  color: var(--fg-regular, var(--el-text-color-regular));
  font-size: 13px;
}

.specs__btn {
  height: 30px;
  padding: 0 12px;
  cursor: pointer;
}

.specs__btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.specs__btn--primary {
  border-color: var(--gold-400, var(--el-color-primary));
  background: var(--gold-400, var(--el-color-primary));
  color: var(--ink-950, var(--el-color-white));
  font-weight: 600;
}

.specs__find input,
.specs__ver input,
.specdlg__field input,
.specdlg__field textarea,
.specdlg__field select {
  padding: 6px 8px;
  min-width: 0;
}

.specs__find input {
  flex: 1;
}

.specdlg {
  position: fixed;
  inset: 0;
  z-index: 30;
}

.specdlg__mask {
  position: absolute;
  inset: 0;
  background: var(--el-overlay-color, var(--el-mask-color));
}

.specdlg__box {
  position: relative;
  width: min(480px, calc(100% - 32px));
  margin: 10vh auto 0;
  padding: 16px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: var(--radius-md, 8px);
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.specdlg__field,
.specdlg__check {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.specdlg__check {
  flex-direction: row;
  align-items: center;
}

.specdlg__err {
  margin: 0;
  font-size: 12px;
}
</style>
